namespace MajakServer.Engine;

/// <summary>
/// Core game state machine — port of HMajakGameLogic.cpp/h.
/// Handles the full hanchan lifecycle: InitHanchan → ProcessAction → EndHanchan.
/// </summary>
public class MajakGameLogic
{
    // ─── Public State (read by server/room) ─────────────────────────────────
    public readonly EnginePlayer[] Player = Enumerable.Range(0, 4)
                                                       .Select(_ => new EnginePlayer())
                                                       .ToArray();
    public HanchanInfo  HanchanInfo;
    public KyokuInfo    KyokuInfo;
    public GameStatus   GameStatus  { get; private set; } = GameStatus.NotPlaying;
    public KyokuEnd     KyokuEnd    { get; private set; } = KyokuEnd.None;
    public GameEnd      GameEnd     { get; private set; } = GameEnd.None;
    public KyoResultSnapshot LastKyoResult { get; } = new();
    public RuleInfo Rule => _rule;
    public int DebugHaipaiYaku { get; private set; } = -1;

    // ─── Private State ───────────────────────────────────────────────────────
    private RuleInfo _rule;
    private readonly Bipai _bipai       = new();
    private readonly Bipai _bipaiBuffer = new();
    private bool     _tsumikomi;
    private int      _currOrder;
    private Act      _currAct   = Act.Inv;
    private PaiCode  _currTapai;
    private bool     _isFirstTurn;
    private bool     _isChankan;
    private bool     _isRinshan;
    private int      _idxOyaOrderTmp = MajakConst.InvalidOrder;
    private bool     _debugEndAfterEast1;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    public void Init()
    {
        GameStatus  = GameStatus.NotPlaying;
        _tsumikomi  = false;
        DebugHaipaiYaku = -1;
    }

    /// <summary>Override tile wall for debug / tsumikomi mode.</summary>
    public void SetBipai(PaiCode[] pai, int wareme)
    {
        _bipaiBuffer.SetBipai(pai, wareme);
        _tsumikomi = true;
    }

    public void SetDebugHaipaiYaku(int yaku)
    {
        DebugHaipaiYaku = yaku;
    }

    public void SetDebugEndAfterEast1(bool enabled)
    {
        _debugEndAfterEast1 = enabled;
    }

    public void InitHanchan(in RuleInfo rule)
    {
        _rule = rule;
        HanchanInfo = new HanchanInfo
        {
            Chicha        = 0,
            CurKyoku      = 0,
            RenchanCount  = 0,
            Player        = new int[] { 0, 1, 2, 3 },
        };
        // Random seat shuffle
        var p = HanchanInfo.Player;
        for (int i = 0; i < 4; i++)
        {
            int j = Random.Shared.Next(4);
            (p[j], p[i]) = (p[i], p[j]);
        }
        KyokuInfo = new KyokuInfo { RibouCount = 0, Dora = new PaiCode[5], UraDora = new PaiCode[5], Dice = new int[MajakConst.DiceCount] };
        for (int i = 0; i < 4; i++) Player[i].InitHanchan(i, _rule);
        _bipai.Init(rule.AkaDora, 0);
        InitKyoku();
    }

    // ─── Main Action Processor ───────────────────────────────────────────────

    public ActionResult ProcessAction(int playOrder, Act action, int[] bipaiIndex, int bipaiCount)
    {
        GameStatus = GameStatus.Playing;
        if (playOrder < 0 || playOrder >= 4) return ActionResult.ErrInvalidOrder;

        var player = Player[playOrder];
        int expected = action switch
        {
            Act.Pas or Act.Ron or Act.Tsu or Act.Tao => 0,
            Act.Tap or Act.Ric or Act.Cha or Act.Hua => 1,
            Act.Chi or Act.Pon                        => 2,
            Act.Kan                                   => 3,
            Act.Ank                                   => 4,
            _ => -1,
        };
        if (expected < 0) return ActionResult.ErrInvalidAction;
        if (bipaiCount != expected) return ActionResult.ErrInvalidPaiCount;

        // Validate indices are in hand
        bool[] used = new bool[14];
        int[] tehaiIdx = new int[4];
        for (int i = 0; i < bipaiCount; i++)
        {
            if (bipaiIndex[i] < 0 || bipaiIndex[i] >= 136) return ActionResult.ErrInvalidBipaiIndex;
            int found = -1;
            int j = 0;
            foreach (var t in player.Tehai)
            {
                if (t.BipaiIndex == bipaiIndex[i] && !used[j]) { found = j; used[j] = true; break; }
                j++;
            }
            if (found < 0) return ActionResult.ErrPaiNotFoundInHand;
            tehaiIdx[i] = found;
        }

        return player.Mode switch
        {
            PlayerMode.Kyo  => ProcessModeKyo(player, action),
            PlayerMode.Aga  => ProcessModeAga(player, action),
            PlayerMode.Turn => ProcessTurn(player, action, bipaiIndex),
            PlayerMode.Chan => action is Act.Pas or Act.Ron
                ? ProcessFuro(player, action, bipaiIndex)
                : ActionResult.ErrInvalidMode,
            PlayerMode.Furo => ProcessFuro(player, action, bipaiIndex),
            _ => ActionResult.ErrInvalidMode,
        };
    }

    // ─── MODE_KYO ────────────────────────────────────────────────────────────

    private ActionResult ProcessModeKyo(EnginePlayer player, Act action)
    {
        if (action != Act.Pas) return ActionResult.ErrInvalidMode;

        player.Mode = PlayerMode.None;
        for (int i = 0; i < 4; i++)
            if (Player[i].Mode != PlayerMode.None) return ActionResult.Ok;

        // All players passed → advance kyoku
        if (KyokuInfo.Renchan) HanchanInfo.RenchanCount++;
        else
        {
            HanchanInfo.CurKyoku++;
            HanchanInfo.RenchanCount = KyokuInfo.EndKyokuWithHora ? 0 : HanchanInfo.RenchanCount + 1;
        }

        // Tobi check (except in contest mode)
        if (_rule.Contest != 1)
        {
            foreach (var p in Player)
                if (p.GamePoint < 0) { GameEnd = GameEnd.Tobi; ProcessEndHanchan(); return ActionResult.Ok; }
        }

        int lastKyoku = _debugEndAfterEast1 ? 1 : (_rule.Hanchan ? 8 : 4);
        // トーナメント強制終了 — 原典: _TOURNAMENT_MODE bCutGame
        // 原典では nLastKyoku 計算後、ラスト判定の前に CutGame を判定する。
        // この順序を守らないと、最終局で CutGame がセットされたとき GE_STOP ではなく
        // GE_SET が発火してしまう。また、Contest!=0 (cup) でも CutGame が効かなくなる。
        if (KyokuInfo.CutGame) { GameEnd = GameEnd.Stop; ProcessEndHanchan(); return ActionResult.Ok; }
        if (lastKyoku == HanchanInfo.CurKyoku) { GameEnd = GameEnd.Set; ProcessEndHanchan(); return ActionResult.Ok; }
        if (_rule.Contest != 0) { InitKyoku(); return ActionResult.Ok; }

        // Renchan at last kyoku for oya
        if (lastKyoku - 1 == HanchanInfo.CurKyoku && HanchanInfo.RenchanCount > 0
            && KyokuInfo.EndKyokuWithHora)
        {
            int top = 0, max = 0;
            for (int i = 0; i < 4; i++)
            {
                int idx = (HanchanInfo.Chicha + i) % 4;
                if (Player[idx].GamePoint > max) { max = Player[idx].GamePoint; top = idx; }
            }
            if (top == KyokuInfo.OyaOrder)
            { GameEnd = GameEnd.Hora; ProcessEndHanchan(); return ActionResult.Ok; }
            Player[KyokuInfo.OyaOrder].Mode = PlayerMode.Aga;
        }
        else InitKyoku();

        return ActionResult.Ok;
    }

    // ─── MODE_AGA ────────────────────────────────────────────────────────────

    private ActionResult ProcessModeAga(EnginePlayer player, Act action)
    {
        switch (action)
        {
            case Act.Ron:
                player.Mode = PlayerMode.None;
                InitKyoku();
                return ActionResult.Ok;
            case Act.Pas:
                player.Mode = PlayerMode.None;
                GameEnd = GameEnd.Stop;
                ProcessEndHanchan();
                return ActionResult.Ok;
            default:
                return ActionResult.ErrInvalidMode;
        }
    }

    // ─── MODE_TURN ───────────────────────────────────────────────────────────

    private ActionResult ProcessTurn(EnginePlayer player, Act action, int[] bipaiIndex)
    {
        switch (action)
        {
            case Act.Tsu:
                if (_currAct is Act.Chi or Act.Pon) return ActionResult.ErrAfterFuro;
                if (!CheckHoraYaku(player, true)) return ActionResult.ErrCannotHora;
                player.CurAct = Act.Tsu;
                player.Mode   = PlayerMode.None;
                KyokuEnd      = KyokuEnd.Hora;
                break;

            case Act.Tap:
                if (player.RichiType != RichiType.None)
                    if (player.Tehai.Last().BipaiIndex != bipaiIndex[0]) return ActionResult.ErrAfterRichi;
                var tapTile = _bipai.GetPai(bipaiIndex[0]);
                var tapRet  = player.Tapai(tapTile);
                if (tapRet != ActionResult.Ok) return tapRet;
                _bipai.Open(bipaiIndex[0]);
                EnterFuroMode(Act.Tap, tapTile, PlayerMode.Furo);
                break;

            case Act.Ric:
                if (_bipai.GetBipaiCount() < 4) return ActionResult.ErrToolate;
                if (player.GamePoint < 1000 && _rule.Contest != 1) return ActionResult.ErrPointNotEnough;
                var ricTile = _bipai.GetPai(bipaiIndex[0]);
                var ricRet  = player.Richi(ricTile);
                if (ricRet != ActionResult.Ok) return ricRet;
                _bipai.Open(bipaiIndex[0]);
                EnterFuroMode(Act.Ric, ricTile, PlayerMode.Furo);
                break;

            case Act.Tao:
                if (!_isFirstTurn) return ActionResult.ErrToolate;
                var taoRet = player.Taopai();
                if (taoRet != ActionResult.Ok) return taoRet;
                KyokuEnd = KyokuEnd.Taopai;
                break;

            case Act.Ank:
                if (_currAct is Act.Chi or Act.Pon) return ActionResult.ErrAfterFuro;
                if (_bipai.GetBipaiCount() == 0) return ActionResult.ErrToolate;
                if (KyokuInfo.KanCount >= 4 && _rule.Contest != 0) return ActionResult.ErrKanAfter4Kan;
                var ankPai = _bipai.GetPai(bipaiIndex[0]);
                if (ankPai.IsHuapai) return ActionResult.ErrHuapai;
                for (int i = 1; i < 4; i++)
                    if (_bipai.GetPai(bipaiIndex[i]) != ankPai) return ActionResult.ErrPaiNotMatch;
                if (player.RichiType != RichiType.None
                    && player.Tehai.Last() != ankPai) return ActionResult.ErrAfterRichi;
                var ankRet = player.AnKan(bipaiIndex);
                if (ankRet != ActionResult.Ok) return ankRet;
                player.CurAct = Act.Ank;
                for (int i = 0; i < 4; i++) _bipai.Open(bipaiIndex[i]);
                ProcessKan(player);
                break;

            case Act.Cha:
                if (_currAct is Act.Chi or Act.Pon) return ActionResult.ErrAfterFuro;
                if (_bipai.GetBipaiCount() == 0) return ActionResult.ErrToolate;
                if (KyokuInfo.KanCount >= 4) return ActionResult.ErrKanAfter4Kan;
                var chaPai = _bipai.GetPai(bipaiIndex[0]);
                var chaRet = player.ChaKan(chaPai);
                if (chaRet != ActionResult.Ok) return chaRet;
                _isChankan = true;
                EnterFuroMode(Act.Cha, chaPai, PlayerMode.Chan);
                break;

            case Act.Hua:
                if (_currAct is Act.Chi or Act.Pon) return ActionResult.ErrAfterFuro;
                if (_bipai.GetBipaiCount() == 0) return ActionResult.ErrToolate;
                if (player.RichiType != RichiType.None
                    && player.Tehai.Last().BipaiIndex != bipaiIndex[0]) return ActionResult.ErrAfterRichi;
                var huaPai = _bipai.GetPai(bipaiIndex[0]);
                var huaRet = player.Hua(huaPai);
                if (huaRet != ActionResult.Ok) return huaRet;
                EnterFuroMode(Act.Hua, huaPai, PlayerMode.Chan);
                break;

            default:
                return ActionResult.ErrInvalidMode;
        }

        player.ResultRecord.TurnCnt++;
        if (KyokuEnd != KyokuEnd.None)
        {
            if (KyokuEnd == KyokuEnd.Hora) ProcessHora(true);
            else ProcessPinchui();
        }
        return ActionResult.Ok;
    }

    // ─── MODE_FURO / CHAN ─────────────────────────────────────────────────────

    private ActionResult ProcessFuro(EnginePlayer player, Act action, int[] bipaiIndex)
    {
        if (action != Act.Pas && player.Order == _currOrder) return ActionResult.ErrSelf;

        switch (action)
        {
            case Act.Pas: break;

            case Act.Chi:
                if (player.Order != (_currOrder + 1) % 4) return ActionResult.ErrNotNextOrder;
                if (_bipai.GetBipaiCount() == 0)           return ActionResult.ErrToolate;
                if (_currTapai.IsHuapai)                   return ActionResult.ErrHuapai;
                if (player.RichiType != RichiType.None)    return ActionResult.ErrAfterRichi;
                if (_currTapai.IsTsupai)                   return ActionResult.ErrPaiNotMatch;
                {
                    var p0 = _bipai.GetPai(bipaiIndex[0]);
                    var p1 = _bipai.GetPai(bipaiIndex[1]);
                    if (p0.GetKind() != _currTapai.GetKind() || p1.GetKind() != _currTapai.GetKind())
                        return ActionResult.ErrPaiNotMatch;
                    int n = _currTapai.GetNumber(), n0 = p0.GetNumber(), n1 = p1.GetNumber();
                    if (n0 > n1) (n0, n1) = (n1, n0);
                    if (!((n0 == n - 2 && n1 == n - 1) || (n0 == n - 1 && n1 == n + 1) || (n0 == n + 1 && n1 == n + 2)))
                        return ActionResult.ErrPaiNotMatch;
                }
                break;

            case Act.Pon:
                if (_bipai.GetBipaiCount() == 0) return ActionResult.ErrToolate;
                if (_currTapai.IsHuapai)         return ActionResult.ErrHuapai;
                if (player.RichiType != RichiType.None) return ActionResult.ErrAfterRichi;
                for (int i = 0; i < 2; i++)
                    if (_bipai.GetPai(bipaiIndex[i]) != _currTapai) return ActionResult.ErrPaiNotMatch;
                break;

            case Act.Kan:
                if (_bipai.GetBipaiCount() == 0) return ActionResult.ErrToolate;
                if (KyokuInfo.KanCount >= 4 && _rule.Contest != 0) return ActionResult.ErrKanAfter4Kan;
                if (_currTapai.IsHuapai) return ActionResult.ErrHuapai;
                if (player.RichiType != RichiType.None) return ActionResult.ErrAfterRichi;
                for (int i = 0; i < 3; i++)
                    if (_bipai.GetPai(bipaiIndex[i]) != _currTapai) return ActionResult.ErrPaiNotMatch;
                break;

            case Act.Ron:
                if (!player.IsHoraForm) return ActionResult.ErrNotHoraForm;
                if (player.CheckFuriten()) return ActionResult.ErrFuriten;
                player.Tsumo(_currTapai);
                if (!CheckHoraYaku(player, false)) { player.Tehai.RemoveAt(player.Tehai.Count - 1); return ActionResult.ErrNoYaku; }
                break;

            default:
                return ActionResult.ErrInvalidMode;
        }

        // Set temp furiten if player had hora form but didn't ron
        if (action != Act.Ron && player.IsHoraForm) player.SetTempFuriten();
        player.Mode   = PlayerMode.None;
        player.CurAct = action;
        if (bipaiIndex.Length >= 2) { player.CurBipaiIndex[0] = bipaiIndex[0]; player.CurBipaiIndex[1] = bipaiIndex[1]; }
        if (bipaiIndex.Length >= 3)   player.CurBipaiIndex[2] = bipaiIndex[2];

        // Wait for all players to respond
        for (int i = 0; i < 4; i++)
            if (Player[i].Mode != PlayerMode.None) return ActionResult.Ok;

        // Resolve priority
        Act  best    = Act.Pas;
        int  bestOdr = MajakConst.InvalidOrder;
        int  ronCnt  = 0;

        for (int j = 0; j < 4; j++)
        {
            int odr = (_currOrder + j) % 4;
            switch (Player[odr].CurAct)
            {
                case Act.Ron:
                    if (_rule.Ron < 2 && ++ronCnt == 3)
                    {
                        foreach (var p2 in Player) p2.CurAct = Act.Pas;
                        KyokuEnd = KyokuEnd.Sanchaho;
                        ProcessPinchui();
                        return ActionResult.Ok;
                    }
                    if (best == Act.Ron && _rule.Ron == 0) Player[odr].CurAct = Act.Pas;
                    KyokuEnd = KyokuEnd.Hora;
                    best = Act.Ron;
                    break;
                case Act.Pon or Act.Kan:
                    if (best == Act.Pas || best == Act.Chi) { bestOdr = odr; best = Player[odr].CurAct; }
                    break;
                case Act.Chi:
                    if (best == Act.Pas) { bestOdr = odr; best = Act.Chi; }
                    break;
            }
        }

        if (best == Act.Ron) { ProcessHora(false); return ActionResult.Ok; }

        _isChankan = false;

        // Confirm riichi
        if (_currAct == Act.Ric)
        {
            var ricPlayer = Player[_currOrder];
            if (ricPlayer.IsIppatsu)
            {
                ricPlayer.SetRichi(_isFirstTurn);
                KyokuInfo.RibouCount++;
                // Sucharichi check
                bool allRichi = true;
                for (int i = 0; i < 4; i++) if (Player[i].RichiType == RichiType.None) { allRichi = false; break; }
                if (allRichi) { KyokuEnd = KyokuEnd.Sucharichi; ProcessPinchui(); return ActionResult.Ok; }
            }
        }

        if (_bipai.GetBipaiCount() == 0) { KyokuEnd = KyokuEnd.Hoanpai; ProcessRyuukyoku(); return ActionResult.Ok; }

        // Sufontsurenta check
        if (_isFirstTurn)
        {
            if (_idxOyaOrderTmp < 0) _idxOyaOrderTmp = KyokuInfo.OyaOrder;
            var firstDiscard = Player[_idxOyaOrderTmp].Sutehai.FirstOrDefault();
            if (firstDiscard.IsWind && firstDiscard.IsValid)
            {
                bool all = true;
                for (int i = 0; i < 4; i++)
                {
                    var d = Player[i].Sutehai.FirstOrDefault();
                    if (!d.IsValid || d != firstDiscard) { all = false; break; }
                }
                if (all) { KyokuEnd = KyokuEnd.Sufontsurenta; ProcessPinchui(); return ActionResult.Ok; }
            }
        }

        if (best == Act.Pas)
        {
            var curr = Player[_currOrder];
            switch (_currAct)
            {
                case Act.Cha:
                    ProcessKan(curr);
                    break;
                case Act.Hua:
                    ClearAllIppatsu();
                    curr.Tsumo(_bipai.GetNextRinshan(_currOrder));
                    break;
                case Act.Tap or Act.Ric:
                    _currOrder = (_currOrder + 1) % 4;
                    Player[_currOrder].Tsumo(_bipai.GetNextTsumo(_currOrder));
                    if (_isFirstTurn && _currOrder == KyokuInfo.OyaOrder) _isFirstTurn = false;
                    break;
            }
        }
        else
        {
            int prev = _currOrder;
            _currAct    = best;
            _currOrder  = bestOdr;
            Player[prev].ClearNagashiMangan();
            var curr = Player[_currOrder];
            switch (_currAct)
            {
                case Act.Chi:
                    curr.Chi(prev, _currTapai, curr.CurBipaiIndex);
                    _bipai.Open(curr.CurBipaiIndex[0]);
                    _bipai.Open(curr.CurBipaiIndex[1]);
                    ClearAllIppatsu();
                    break;
                case Act.Pon:
                    curr.Pon(prev, _currTapai, curr.CurBipaiIndex);
                    _bipai.Open(curr.CurBipaiIndex[0]);
                    _bipai.Open(curr.CurBipaiIndex[1]);
                    ClearAllIppatsu();
                    break;
                case Act.Kan:
                    curr.MinKan(prev, _currTapai, curr.CurBipaiIndex);
                    _bipai.Open(curr.CurBipaiIndex[0]);
                    _bipai.Open(curr.CurBipaiIndex[1]);
                    _bipai.Open(curr.CurBipaiIndex[2]);
                    ProcessKan(curr);
                    break;
            }
        }

        if (KyokuEnd != KyokuEnd.None) ProcessPinchui();
        else Player[_currOrder].Mode = PlayerMode.Turn;

        return ActionResult.Ok;
    }

    // ─── Public Accessors ────────────────────────────────────────────────────

    public void GetBipai(ref BipaiInfo buf, int openMask, int skipMask)
        => _bipai.GetPaiInfo(ref buf, openMask, skipMask);

    public PaiCode GetBipaiPai(int index) => _bipai.GetPai(index);
    public int     GetBipaiCount()        => _bipai.GetBipaiCount();
    public int     GetHojuOrder()         => _currOrder;

    /// <summary>
    /// トーナメント強制終了フラグをセットする — 原典: _TOURNAMENT_MODE bCutGame
    /// 現在の局が終わった後 (MODE_KYO で全員 PAS を返したとき) に GE_STOP を発火する。
    /// </summary>
    public void SetCutGame() => KyokuInfo.CutGame = true;

    /// <summary>
    /// 指定プレイヤーが現在取れる有効アクション一覧を返す。
    /// クライアントの UI 予測検証・ProxyPlay の賢いタイ選択に使用する。
    /// 原典: 各 ProcessTurn / ProcessFuro の条件分岐を読み取り専用で再現。
    /// </summary>
    public ValidActions GetValidActions(int order)
    {
        var result = new ValidActions { Order = order };
        var player = Player[order];

        if (player.Mode == PlayerMode.None) return result;

        if (player.Mode == PlayerMode.Aga)
        {
            result.CanRon = true;   // 続ける
            result.CanPass = true;  // やめる
            return result;
        }

        if (player.Mode == PlayerMode.Kyo)
        {
            result.CanPass = true;
            return result;
        }

        if (player.Mode == PlayerMode.Turn)
        {
            // ツモ和了
            if (CheckHoraYaku(player, true))
                result.CanTsumo = true;
            else if (player.CheckHoraForm())
                result.HoraErrorReason = "invalid";

            // 打牌 (リーチ後は最後の牌のみ)
            if (player.RichiType != RichiType.None)
            {
                result.TapCandidates.Add(player.Tehai.Last().BipaiIndex);
            }
            else
            {
                foreach (var t in player.Tehai)
                    result.TapCandidates.Add(t.BipaiIndex);
            }

            // リーチ (リーチ前、残り牌4枚以上、点数1000以上 or コンテスト)
            if (player.IsMenzen
                && player.RichiType == RichiType.None
                && _bipai.GetBipaiCount() >= 4
                && (player.GamePoint >= 1000 || _rule.Contest == 1))
            {
                var hand = new Hand(player);
                foreach (var t in player.Tehai)
                {
                    if (hand.CheckTempai(t.GetSerial()))
                        result.RichiCandidates.Add(t.BipaiIndex);
                }
            }

            // 暗槓
            if (_bipai.GetBipaiCount() > 0
                && !(_currAct is Act.Chi or Act.Pon)
                && KyokuInfo.KanCount < 4)
            {
                if (player.RichiType != RichiType.None)
                {
                    var last = player.Tehai.Last();
                    var tiles = player.Tehai.Where(t => t == last).ToArray();
                    if (tiles.Length == 4 && (last.GetSerial() >= 27 || new Hand(player).CheckAnkan(last.GetSerial())))
                        result.AnkanCandidates.Add(tiles.Select(t => t.BipaiIndex).ToArray());
                }
                else
                {
                    // 手牌内で4枚同じ牌があるか
                    var groups = player.Tehai.GroupBy(t => t.GetSerial())
                        .Where(g => g.Count() == 4);
                    foreach (var g in groups)
                    {
                        var tiles = g.ToArray();
                        result.AnkanCandidates.Add(tiles.Select(t => t.BipaiIndex).ToArray());
                    }
                }
            }

            // 加槓 (ポン済みの牌と同じ牌を持っている)
            if (player.RichiType == RichiType.None
                && _bipai.GetBipaiCount() > 0
                && !(_currAct is Act.Chi or Act.Pon)
                && KyokuInfo.KanCount < 4)
            {
                foreach (var furo in player.Furo.Where(f => f.Act == Act.Pon))
                {
                    var match = player.Tehai.FirstOrDefault(t => t == furo.Tiles[0]);
                    if (match.IsValid)
                        result.ChakanCandidates.Add(match.BipaiIndex);
                }
            }

            // 花牌
            if (_bipai.GetBipaiCount() > 0
                && !(_currAct is Act.Chi or Act.Pon))
            {
                if (player.RichiType != RichiType.None)
                {
                    var last = player.Tehai.LastOrDefault();
                    if (last.IsHuapai)
                        result.HuaCandidates.Add(last.BipaiIndex);
                }
                else
                {
                    foreach (var tile in player.Tehai.Where(t => t.IsHuapai))
                        result.HuaCandidates.Add(tile.BipaiIndex);
                }
            }

            // 九種九牌 (第一ターン)
            if (_isFirstTurn && player.Furo.Count == 0)
            {
                int yaochuCount = player.Tehai.Select(t => t.GetSerial())
                    .Distinct()
                    .Count(s =>
                    {
                        var p = PaiCode.MakeSerial(s);
                        return p.IsYaochupai;
                    });
                if (yaochuCount >= 9) result.CanTaopai = true;
            }
        }
        else if (player.Mode is PlayerMode.Furo or PlayerMode.Chan)
        {
            // パス常に可能
            result.CanPass = true;

            if (player.Order == _currOrder) return result; // 捨て牌プレイヤーはパスのみ

            // ロン
            if (player.IsHoraForm && !player.CheckFuriten())
            {
                // 仮ツモしてヤク確認
                var tmp = new EnginePlayer();
                // 本格的な検証は ProcessFuro の Ron ブランチで行われる
                result.CanRon = true;
            }
            else if (player.IsHoraForm && player.CheckFuriten())
            {
                result.HoraErrorReason = player.IsFuriten ? "sameTurnFuriten" : "furiten";
            }

            if (player.Mode == PlayerMode.Chan) return result;

            // ポン (役牌以外も可)
            if (player.RichiType == RichiType.None && !_currTapai.IsHuapai
                && _bipai.GetBipaiCount() > 0)
            {
                var pairs = player.Tehai
                    .Where(t => t == _currTapai)
                    .Select(t => t.BipaiIndex)
                    .ToArray();
                if (pairs.Length >= 2)
                    result.PonCandidates.Add(new[] { pairs[0], pairs[1] });
            }

            // カン (ミンカン)
            if (player.RichiType == RichiType.None && !_currTapai.IsHuapai
                && _bipai.GetBipaiCount() > 0
                && KyokuInfo.KanCount < 4)
            {
                var triples = player.Tehai
                    .Where(t => t == _currTapai)
                    .Select(t => t.BipaiIndex)
                    .ToArray();
                if (triples.Length >= 3)
                    result.KanCandidates.Add(new[] { triples[0], triples[1], triples[2] });
            }

            // チー (下家のみ、数牌のみ)
            if (player.Order == (_currOrder + 1) % 4
                && player.RichiType == RichiType.None
                && !_currTapai.IsTsupai && !_currTapai.IsHuapai
                && _bipai.GetBipaiCount() > 0)
            {
                int n = _currTapai.GetNumber();
                var sameKind = player.Tehai
                    .Where(t => t.GetKind() == _currTapai.GetKind())
                    .ToArray();
                // 下-2,下-1 / 下-1,上+1 / 上+1,上+2
                var patterns = new[] {
                    new[]{ n-2, n-1 }, new[]{ n-1, n+1 }, new[]{ n+1, n+2 }
                };
                foreach (var pat in patterns)
                {
                    if (pat[0] < 1 || pat[1] > 9) continue;
                    var t0 = sameKind.FirstOrDefault(t => t.GetNumber() == pat[0]);
                    var t1 = sameKind.FirstOrDefault(t => t.GetNumber() == pat[1] && t.BipaiIndex != t0.BipaiIndex);
                    if (t0.IsValid && t1.IsValid)
                        result.ChiCandidates.Add(new[] { t0.BipaiIndex, t1.BipaiIndex });
                }
            }
        }

        return result;
    }


    public void GetHoraYaku(EnginePlayer player, bool isTsumo, Yaku yaku)
        => GetHoraYaku(player, isTsumo, yaku, player.IsMenzen, includeFirstTurn: true);

    private void GetHoraYaku(
        EnginePlayer player,
        bool isTsumo,
        Yaku yaku,
        bool isMenzen,
        bool includeFirstTurn)
    {
        int chanfon      = HanchanInfo.CurKyoku / 4;
        int menfon       = (player.Order - KyokuInfo.OyaOrder + 4) % 4;
        // 段位戦のみ bRevaluate=false (シングル役満) — 原典: bRevaluate = !m_bGradeGame
        // 段位戦以外 (通常/カップ/トーナメント) は doubleYakuman=true で四暗刻単騎・国士13面・九蓮9面が2倍。
        bool doubleYakuman = !_rule.GradeGame;
        new Hand(player).GetYaku(yaku, _rule.Kuitan, isTsumo, isMenzen, chanfon, menfon,
                                 player.Tehai.Last().GetSerial(), doubleYakuman);

        // First turn yakuman
        if (includeFirstTurn && _isFirstTurn && isTsumo)
        {
            if (!yaku.IsYakuman) { yaku.List.Clear(); yaku.HanSum = 0; }
            yaku.AddYakuman(player.Order == KyokuInfo.OyaOrder ? HoraYaku.Tenhou : HoraYaku.Chihou, 1);
        }

        if (!yaku.IsYakuman)
        {
            switch (player.RichiType)
            {
                case RichiType.Richi:  yaku.AddYaku(HoraYaku.Richi, 1);  break;
                case RichiType.Wrichi: yaku.AddYaku(HoraYaku.Wrichi, 2); break;
            }
            if (player.IsIppatsu && _rule.Contest != 1) { yaku.AddYaku(HoraYaku.Ippatsu, 1); yaku.Chip++; }
            if (isTsumo && isMenzen) yaku.AddYaku(HoraYaku.Tsumo, 1);
            if (_isRinshan) yaku.AddYaku(HoraYaku.Rinshan, 1);
            else if (_isChankan) yaku.AddYaku(HoraYaku.Chankan, 1);
            else if (_bipai.GetBipaiCount() == 0) yaku.AddYaku(isTsumo ? HoraYaku.Haitei : HoraYaku.Houtei, 1);

            // Dora
            int totalDora = 0;
            foreach (var t in player.Tehai)   totalDora += CountDora(yaku, t, player.RichiType != RichiType.None);
            foreach (var f in player.Furo)    foreach (var t in f.Tiles) totalDora += CountDora(yaku, t, player.RichiType != RichiType.None);
            foreach (var t in player.NukiDora) { yaku.DoraCnt[3]++; totalDora += 1 + CountDora(yaku, t, player.RichiType != RichiType.None); }
            if (totalDora > 0) yaku.AddYaku(HoraYaku.Dora, totalDora);
        }

        yaku.CalcHoraTen();
        if (yaku.IsYakuman) yaku.Chip = (isTsumo ? 5 : 10) * yaku.HanSum;
        else if (yaku.Mangan == 5) yaku.Chip = isTsumo ? 5 : 10;
    }

    internal (int Tsumo, int Ron, int RiichiTsumo, int RiichiRon) EvaluateTrainingAiHoraPoints(
        int engineOrder,
        int[] handCounts,
        PaiCode winningTile)
    {
        EnginePlayer source = Player[engineOrder];
        var snapshot = new EnginePlayer
        {
            Order = source.Order,
            GamePoint = source.GamePoint,
        };
        snapshot.Furo.AddRange(source.Furo);
        snapshot.NukiDora.AddRange(source.NukiDora);

        int winningSerial = winningTile.GetSerial();
        for (int serial = 0; serial < 34; serial++)
        {
            int count = handCounts[serial] - (serial == winningSerial ? 1 : 0);
            for (int copy = 0; copy < count; copy++)
                snapshot.Tehai.Add(PaiCode.MakeSerial(serial));
        }
        snapshot.Tehai.Add(PaiCode.MakeSerial(winningSerial));

        bool canRiichi = GetBipaiCount() >= MajakConst.PlayerMaxCount
            && (source.GamePoint >= 1000 || _rule.Contest == 1)
            && source.IsMenzen
            && source.RichiType == RichiType.None;

        (int points, int riichiPoints) Evaluate(bool isTsumo)
        {
            var yaku = new Yaku();
            GetHoraYaku(snapshot, isTsumo, yaku, source.IsMenzen, includeFirstTurn: false);
            int points = isTsumo || yaku.HanSum != 0 ? yaku.Ten : 0;
            int riichiPoints = 0;
            if (canRiichi)
            {
                yaku.AddYaku(HoraYaku.Richi, 1);
                yaku.CalcHoraTen();
                riichiPoints = yaku.Ten;
            }
            return (points, riichiPoints);
        }

        var tsumo = Evaluate(isTsumo: true);
        var ron = Evaluate(isTsumo: false);
        return (tsumo.points, ron.points, tsumo.riichiPoints, ron.riichiPoints);
    }

    // ─── Private Helpers ─────────────────────────────────────────────────────

    private void InitKyoku()
    {
        foreach (var p in Player)
        {
            p.InitKyoku();
            p.ResultRecordSave = p.ResultRecord;
        }
        KyokuInfo.KanCount  = 0;
        KyokuInfo.Dora      = new PaiCode[5];
        KyokuInfo.UraDora   = new PaiCode[5];
        KyokuInfo.Dice    ??= new int[MajakConst.DiceCount];
        _currAct            = Act.Inv;
        _idxOyaOrderTmp     = MajakConst.InvalidOrder;

        for (int i = 0; i < MajakConst.DiceCount; i++) KyokuInfo.Dice[i] = Random.Shared.Next(6);
        KyokuInfo.OyaOrder = (HanchanInfo.Chicha + HanchanInfo.CurKyoku) % 4;

        int wareme     = KyokuInfo.Dice[0] + KyokuInfo.Dice[1] + 2;
        int haipaiPos  = (wareme + (12 + 4 - KyokuInfo.OyaOrder - wareme + 1) % 4 * 17) * 2;

        if (DebugHaipaiYaku > -1)
        {
            bool oyaOrder = DebugHaipaiYaku is not (1031 or 1032 or 1033 or 1034 or 1035);
            _bipai.ChipaiYakuDebug(haipaiPos, DebugHaipaiYaku, _rule.AkaDora, 0, oyaOrder);
            DebugHaipaiYaku = -1;
        }
        else
        {
            _bipai.Chipai();
        }

        if (_tsumikomi)
        {
            var tmp = new PaiCode[136];
            _bipaiBuffer.GetBipai(tmp);
            _bipai.SetBipai(tmp, haipaiPos);
            _tsumikomi = false;
        }
        _bipai.SetOpenIdx(haipaiPos);

        _currOrder = KyokuInfo.OyaOrder;
        // Haipai distribution
        int seatOdr = KyokuInfo.OyaOrder;
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 4; j++)
            {
                for (int k = 0; k < 4; k++) Player[seatOdr].Tsumo(_bipai.GetNextTsumo(seatOdr));
                seatOdr = (seatOdr + 1) % 4;
            }
        for (int j = 0; j < 4; j++) { Player[seatOdr].Tsumo(_bipai.GetNextTsumo(seatOdr)); seatOdr = (seatOdr + 1) % 4; }

        // Oya first tsumo
        Player[KyokuInfo.OyaOrder].Tsumo(_bipai.GetNextTsumo(KyokuInfo.OyaOrder));
        Player[KyokuInfo.OyaOrder].Mode = PlayerMode.Turn;

        // Dora
        int doraIdx = _bipai.GetDoraIdx(0, false);
        KyokuInfo.Dora[0] = _bipai.GetPai(doraIdx);
        _bipai.Open(doraIdx);
        int uraIdx = _bipai.GetDoraIdx(0, true);
        KyokuInfo.UraDora[0] = _bipai.GetPai(uraIdx);

        _isFirstTurn = true;
        _isChankan   = false;
        _isRinshan   = false;

        GameStatus = GameStatus.NewKyoku;
        KyokuEnd   = KyokuEnd.None;
    }

    private void EnterFuroMode(Act act, PaiCode pai, PlayerMode waitMode)
    {
        _isRinshan   = false;
        _bipai.Open(pai.BipaiIndex);
        _currTapai   = pai;
        _currAct     = act;
        for (int i = 0; i < 4; i++)
        {
            Player[i].Mode       = waitMode;
            Player[i].IsHoraForm = (i != _currOrder) && Player[i].CheckHoraForm(pai);
        }
    }

    private void ClearAllIppatsu()
    {
        foreach (var p in Player) p.ClearIppatsu();
        _isFirstTurn = false;
    }

    private void ProcessKan(EnginePlayer player)
    {
        if (_rule.Contest == 0 && (KyokuInfo.KanCount == 4 ||
            (KyokuInfo.KanCount == 3 && player.KanCnt != 4)))
        { KyokuEnd = KyokuEnd.Sukaikan; return; }

        KyokuInfo.KanCount++;
        if (_rule.Contest != 1)
        {
            int di = _bipai.GetDoraIdx(KyokuInfo.KanCount, false);
            KyokuInfo.Dora[KyokuInfo.KanCount] = _bipai.GetPai(di);
            _bipai.Open(di);
            int ui = _bipai.GetDoraIdx(KyokuInfo.KanCount, true);
            KyokuInfo.UraDora[KyokuInfo.KanCount] = _bipai.GetPai(ui);
        }
        ClearAllIppatsu();
        player.Tsumo(_bipai.GetNextRinshan(player.Order));
        _isRinshan = true;
    }

    private bool CheckHoraYaku(EnginePlayer player, bool tsumo)
    {
        if (!player.CheckHoraForm()) return false;
        if (player.RichiType != RichiType.None || _isRinshan || _isChankan || _bipai.GetBipaiCount() == 0)
            return true;
        if (tsumo && player.IsMenzen) return true;
        int chanfon = HanchanInfo.CurKyoku / 4;
        int menfon  = (player.Order - KyokuInfo.OyaOrder + 4) % 4;
        return new Hand(player).CheckYaku(_rule.Kuitan, tsumo, player.IsMenzen, chanfon, menfon, player.Tehai.Last().GetSerial());
    }

    private void ProcessHora(bool tsumo)
    {
        bool renchan = false;
        LastKyoResult.Clear(tsumo ? KyoResultPin.Tsumo : KyoResultPin.Ron);
        if (!tsumo)
        {
            var hoju = Player[_currOrder];
            LastKyoResult.HojuOrder = hoju.Order;
            bool first = true;
            for (int i = 1; i < 4; i++)
            {
                int odr = (_currOrder + i) % 4;
                if (Player[odr].CurAct == Act.Ron)
                {
                    ProcessHoraPlayer(Player[odr], hoju, first);
                    if (first) { first = false; if (Player[odr].Order == KyokuInfo.OyaOrder) renchan = true; }
                }
            }
            hoju.ResultRecord.HojuCnt++;
        }
        else
        {
            var hora = Player[_currOrder];
            ProcessHoraPlayer(hora, null, true);
            if (hora.Order == KyokuInfo.OyaOrder) renchan = true;
        }
        if (_rule.Contest != 1) CheckTobiAndRecord();
        ProcessEndKyoku(true, renchan);
    }

    private void ProcessHoraPlayer(EnginePlayer hora, EnginePlayer? hoju, bool getBonus)
    {
        hora.Yaku.Clear();
        GetHoraYaku(hora, hoju == null, hora.Yaku);
        var yaku = hora.Yaku;

        int[] tenPts = new int[4];
        int[] paoPts = new int[4];
        int[] warPts = new int[4];
        int[] tips   = new int[4];
        bool tsumo = hoju == null;
        LastKyoResult.Hora[hora.Order] = true;

        if (yaku.IsYakuman)
        {
            foreach (var yi in yaku.List)
            {
                int ten = 8000 * yi.Han;
                if (tsumo)
                {
                    int mul = hora.Order == KyokuInfo.OyaOrder ? 2 : 1;
                    for (int i = 0; i < 4; i++)
                        if (i != hora.Order)
                        {
                            int pay = ten * (i == KyokuInfo.OyaOrder ? mul * 2 : mul);
                            tenPts[hora.Order] += pay;
                            tenPts[i] -= pay;
                            if (hora.IsPao && (yi.Name == HoraYaku.Daisangen || yi.Name == HoraYaku.Daisuushi))
                            { paoPts[i] += pay; paoPts[hora.PaoOrder] -= pay; }
                        }
                }
                else
                {
                    int pay = ten * (hora.Order == KyokuInfo.OyaOrder ? 6 : 4);
                    tenPts[hora.Order] += pay;
                    tenPts[hoju!.Order] -= pay;
                    if (hora.IsPao && (yi.Name == HoraYaku.Daisangen || yi.Name == HoraYaku.Daisuushi))
                    { paoPts[hoju.Order] += pay / 2; paoPts[hora.PaoOrder] -= pay / 2; }
                }
            }
        }
        else
        {
            if (!tsumo)
            {
                int pay = ((yaku.Ten * (hora.Order == KyokuInfo.OyaOrder ? 6 : 4)) + 99) / 100 * 100;
                tenPts[hora.Order] = pay; tenPts[hoju!.Order] = -pay;
            }
            else
            {
                int baseP = yaku.Ten * (hora.Order == KyokuInfo.OyaOrder ? 2 : 1);
                for (int i = 0; i < 4; i++)
                    if (i != hora.Order)
                    {
                        int pay = ((i == KyokuInfo.OyaOrder ? baseP * 2 : baseP) + 99) / 100 * 100;
                        tenPts[hora.Order] += pay; tenPts[i] = -pay;
                    }
            }
        }

        // Chip
        if (!tsumo)
        {
            tips[hora.Order] = yaku.Chip; tips[hoju!.Order] = -yaku.Chip;
        }
        else
        {
            tips[hora.Order] = yaku.Chip * 3;
            for (int i = 0; i < 4; i++) if (i != hora.Order) tips[i] = -yaku.Chip;
        }

        // Wareme
        if (_rule.Wareme)
        {
            int wareme = (KyokuInfo.OyaOrder + KyokuInfo.Dice[0] + KyokuInfo.Dice[1] + 1) % 4;
            int sum = 0;
            for (int i = 0; i < 4; i++)
                if (i != hora.Order && (hora.Order == wareme || i == wareme))
                {
                    int bal = tenPts[i] + paoPts[i];
                    sum -= bal;
                    warPts[i] += bal;
                }
            warPts[hora.Order] += sum;
        }

        // Apply points + tips
        for (int i = 0; i < 4; i++)
        {
            if (_rule.Tip) Player[i].Tip += tips[i];
            int total = tenPts[i] + paoPts[i] + warPts[i];
            Player[i].GamePoint  += total;
            Player[i].KyokuPoint += total;
            LastKyoResult.TenBal[i] += tenPts[i];
            LastKyoResult.PaoBal[i] += paoPts[i];
            LastKyoResult.WarBal[i] += warPts[i];
            LastKyoResult.TipBal[i] += tips[i];
        }

        // Renchan bonus + ribou
        if (getBonus)
        {
            int ren = HanchanInfo.RenchanCount * 100;
            LastKyoResult.RenBal[hora.Order] += ren * 3;
            hora.GamePoint += ren * 3;
            if (tsumo)
            {
                for (int i = 1; i < 4; i++)
                {
                    int order = (hora.Order + i) % 4;
                    LastKyoResult.RenBal[order] -= ren;
                    Player[order].GamePoint -= ren;
                }
            }
            else
            {
                LastKyoResult.RenBal[hoju!.Order] -= ren * 3;
                hoju.GamePoint -= ren * 3;
            }
            int rib = KyokuInfo.RibouCount * 1000;
            LastKyoResult.RibBal[hora.Order] += rib;
            hora.GamePoint += rib;
            KyokuInfo.RibouCount = 0;
        }

        hora.ClearYakitori();
        hora.ResultRecord.DoraCnt    += yaku.DoraCnt[0] + yaku.DoraCnt[2];
        hora.ResultRecord.UraDoraCnt += yaku.DoraCnt[1];
        hora.ResultRecord.HoraPoint  += tenPts[hora.Order] + paoPts[hora.Order] + warPts[hora.Order];
        hora.ResultRecord.HoraCnt++;
        if (hora.RichiType != RichiType.None && !yaku.IsYakuman) hora.ResultRecord.RichiHoraCnt++;
        if (!tsumo && hoju != null) hoju.ResultRecord.HojuPoint += tenPts[hoju.Order] + paoPts[hoju.Order] + warPts[hoju.Order];
    }

    private void ProcessEndKyoku(bool hora, bool renchan)
    {
        KyokuInfo.EndKyokuWithHora = hora;
        KyokuInfo.Renchan          = renchan;
        _bipai.OpenAll();
        foreach (var p in Player)
        {
            p.ResultRecord.KyokuCnt++;
            if (!p.IsMenzen) p.ResultRecord.FuroCnt++;
            if (p.RichiType != RichiType.None) p.ResultRecord.RichiCnt++;
            p.Mode = PlayerMode.Kyo;
        }
        GameStatus = GameStatus.EndKyoku;
    }

    private void ProcessEndHanchan()
    {
        int[] rnkOdr = new int[4];
        int[] odrRnk = new int[4];
        int[] setTen = new int[4];
        int[] setUma = new int[4];
        int[] setTor = new int[4];
        int[] setTip = new int[4];
        int torCnt   = 0;

        for (int i = 0; i < 4; i++) rnkOdr[i] = (HanchanInfo.Chicha + i) % 4;
        for (int i = 0; i < 4; i++)
        {
            setTen[i] = Player[i].GamePoint - MajakConst.KaeshiPoint;
            if (_rule.Yakitori && Player[i].IsYakitori) { setTor[i] = 1; torCnt++; }
            setTip[i] = (Player[i].Tip - MajakConst.DefaultTip) * 2;
        }
        // Sort by setTen descending
        for (int i = 0; i < 3; i++)
            for (int j = 2; j >= i; j--)
                if (setTen[rnkOdr[j]] < setTen[rnkOdr[j + 1]])
                    (rnkOdr[j], rnkOdr[j + 1]) = (rnkOdr[j + 1], rnkOdr[j]);

        int[] torTbl  = { 0, 30, 15, 10, 0 };
        for (int i = 0; i < 4; i++)
        {
            int odr = rnkOdr[i];
            odrRnk[odr] = i;
            setTor[odr] = setTor[odr] == 1 ? -torTbl[torCnt] : torTbl[4 - torCnt];
        }

        int[][] umaTbl =
        {
            new[] { +10, +5,  -5, -10 },
            new[] { +20, +10, -10, -20 },
            new[] { +30, +10, -10, -30 },
            new[] {   0,   0,   0,   0 },
        };
        int top = rnkOdr[0];
        setUma[top]   = umaTbl[_rule.Uma][0];
        setTen[top]   = 0;
        for (int i = 1; i < 4; i++)
        {
            int odr = rnkOdr[i];
            setTen[odr]   = (setTen[odr] + 1000400) / 1000 - 1000;
            setTen[top]  -= setTen[odr];
            setUma[odr]   = umaTbl[_rule.Uma][i];
        }

        for (int i = 0; i < 4; i++)
        {
            Player[i].ResultRecord.Grade[odrRnk[i]]++;
            Player[i].ResultRecord.PointSum += setTen[i] + setUma[i];
            if (_rule.Tip) { Player[i].ResultRecord.TipMatchCnt++; Player[i].ResultRecord.TipPoint += setTip[i]; }
            Player[i].SetPoint  = setTen[i];
            Player[i].SetUma    = setUma[i];
            Player[i].SetTor    = setTor[i];
            Player[i].SetTip    = setTip[i];
            Player[i].SetTotal  = setTen[i] + setUma[i] + setTor[i] + setTip[i];
            Player[i].SetRank   = odrRnk[i];
        }
        GameStatus = GameStatus.NotPlaying;
    }

    private void ProcessRyuukyoku()
    {
        bool renchan      = true;
        bool nagashiMangan = false;
        LastKyoResult.Clear(MapKyoResultPin(KyokuEnd));

        if (_rule.Nagashi)
            for (int i = 0; i < 4; i++)
                if (Player[i].IsNagashiMangan)
                {
                    int n = KyokuInfo.OyaOrder == i ? 4000 : 2000;
                    for (int j = 0; j < 4; j++)
                        if (i != j)
                        {
                            int pay = j == KyokuInfo.OyaOrder ? n * 2 : n;
                            Player[i].GamePoint += pay; Player[j].GamePoint -= pay;
                            LastKyoResult.TenBal[i] += pay; LastKyoResult.TenBal[j] -= pay;
                        }
                    KyokuEnd     = KyokuEnd.Nagashimangan;
                    LastKyoResult.Pin = KyoResultPin.Nagashimangan;
                    nagashiMangan = true;
                }

        if (!nagashiMangan)
        {
            int tempai = 0;
            for (int i = 0; i < 4; i++)
                if (Player[i].CheckTempai()) { Player[i].IsTempai = true; tempai++; }
            int[] bappu = { 0, 1000, 1500, 3000, 0 };
            for (int i = 0; i < 4; i++)
            {
                int tenBal = Player[i].IsTempai ? bappu[4 - tempai] : -bappu[tempai];
                Player[i].GamePoint += tenBal;
                LastKyoResult.TenBal[i] += tenBal;
            }
            if (!Player[KyokuInfo.OyaOrder].IsTempai) renchan = false;
        }

        if (_rule.Contest != 1)
        {
            // 飛び記録 — 原典: ProcessRyuukyoku 内の tobiチェック
            // TobashiCnt: 流局時はテンパイ (またはナガシマンガン) プレイヤーに加算
            bool anyTobi = false;
            for (int i = 0; i < 4; i++)
                if (Player[i].GamePoint < 0) { Player[i].ResultRecord.TobiCnt++; anyTobi = true; }
            if (anyTobi)
                for (int i = 0; i < 4; i++)
                    if (nagashiMangan ? Player[i].IsNagashiMangan : Player[i].IsTempai)
                        Player[i].ResultRecord.TobashiCnt++;
        }
        ProcessEndKyoku(false, renchan);
    }

    private void ProcessPinchui()
    {
        LastKyoResult.Clear(MapKyoResultPin(KyokuEnd));
        ProcessEndKyoku(false, true);
    }

    private static KyoResultPin MapKyoResultPin(KyokuEnd kyokuEnd) => kyokuEnd switch
    {
        KyokuEnd.Taopai => KyoResultPin.Taopai,
        KyokuEnd.Sanchaho => KyoResultPin.Sanchaho,
        KyokuEnd.Hoanpai => KyoResultPin.Hoanpai,
        KyokuEnd.Sukaikan => KyoResultPin.Sukaikan,
        KyokuEnd.Sucharichi => KyoResultPin.Sucharichi,
        KyokuEnd.Sufontsurenta => KyoResultPin.Sufontsurenta,
        KyokuEnd.Nagashimangan => KyoResultPin.Nagashimangan,
        _ => KyoResultPin.None,
    };

    private void CheckTobiAndRecord()
    {
        // 和了時の飛び記録 — 原典: ProcessHora 内の tobiチェック
        // TobashiCnt: ツモは常に、ロンは放銃者がマイナスになった場合 (または包 ケース) のみ加算
        bool anyTobi = false;
        for (int i = 0; i < 4; i++)
            if (Player[i].GamePoint < 0) { Player[i].ResultRecord.TobiCnt++; anyTobi = true; }
        if (anyTobi)
            for (int i = 0; i < 4; i++)
                if (Player[i].CurAct == Act.Tsu
                    || (Player[i].CurAct == Act.Ron
                        && (Player[_currOrder].GamePoint < 0
                            || (Player[i].IsPao && Player[Player[i].PaoOrder].GamePoint < 0))))
                    Player[i].ResultRecord.TobashiCnt++;
    }

    private int CountDora(Yaku yaku, PaiCode pai, bool richi)
    {
        int n = 0;
        if (_rule.Contest == 1)
        {
            if (pai == KyokuInfo.Dora[0].GetNextNumberPai()) { n++; yaku.DoraCnt[0]++; }
            return n;
        }
        for (int i = 0; i <= KyokuInfo.KanCount; i++)
            if (pai == KyokuInfo.Dora[i].GetNextNumberPai()) { n++; yaku.DoraCnt[0]++; }
        if (richi)
            for (int i = 0; i <= KyokuInfo.KanCount; i++)
                if (pai == KyokuInfo.UraDora[i].GetNextNumberPai()) { n++; yaku.DoraCnt[1]++; yaku.Chip++; }
        if (pai.IsRed) { n++; yaku.DoraCnt[2]++; yaku.Chip++; }
        return n;
    }
}
