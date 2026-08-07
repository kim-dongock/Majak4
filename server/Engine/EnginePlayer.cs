namespace MajakServer.Engine;

/// <summary>
/// Per-seat game state — port of HMajakPlayer.
/// This is the pure engine player, independent of network/session state.
/// </summary>
public class EnginePlayer
{
    // ─── Identity ────────────────────────────────────────────────────────────
    public int Order { get; set; }   // seat order (0-3)

    // ─── Hand State ──────────────────────────────────────────────────────────
    public List<PaiCode> Tehai   = new();
    public List<PaiCode> Sutehai = new();
    public List<FuroBlock> Furo  = new();
    public List<PaiCode> NukiDora = new();  // for flower/nuki modes
    private readonly HashSet<int> _kuikaeForbiddenSerials = new();

    // ─── Mode / Action ───────────────────────────────────────────────────────
    public PlayerMode Mode    { get; set; } = PlayerMode.None;
    public Act        CurAct  { get; set; } = Act.Inv;
    public int[]      CurBipaiIndex = new int[4];

    // ─── Points ──────────────────────────────────────────────────────────────
    public int GamePoint  { get; set; }
    public int SetPoint   { get; set; }
    public int SetUma     { get; set; }
    public int SetTor     { get; set; }
    public int SetTip     { get; set; }
    public int SetTotal   { get; set; }
    public int SetRank    { get; set; }
    public int KyokuPoint { get; set; }
    public int Tip        { get; set; }
    public int KanCnt     { get; set; }

    // ─── Flags ───────────────────────────────────────────────────────────────
    public RichiType RichiType       { get; set; } = RichiType.None;
    public bool      IsMenzen        { get; private set; } = true;
    public bool      IsIppatsu       { get; set; }         // also "pending riichi" marker
    public bool      IsTempai        { get; set; }
    public bool      IsHoraForm      { get; set; }         // set by game logic for furo judgment
    public bool      IsFuriten       { get; private set; } // temp furiten (from others' discard)
    public bool      IsNagashiMangan { get; private set; } = true;
    public bool      IsYakitori      { get; private set; }

    // ─── PAO ─────────────────────────────────────────────────────────────────
    public int PaoOrder { get; private set; } = MajakConst.InvalidOrder;
    public bool IsPao   => PaoOrder != MajakConst.InvalidOrder;

    // ─── Yaku Result ─────────────────────────────────────────────────────────
    public Yaku Yaku = new();

    // ─── Statistics ──────────────────────────────────────────────────────────
    public RatingRecord ResultRecord     = RatingRecord.CreateEmpty();
    public RatingRecord ResultRecordSave = RatingRecord.CreateEmpty();

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    public void InitHanchan(int order, in RuleInfo rule)
    {
        Order      = order;
        GamePoint  = MajakConst.DefaultGamePoint;
        SetPoint   = 0;
        SetUma     = 0;
        SetTor     = 0;
        SetTip     = 0;
        SetTotal   = 0;
        SetRank    = 0;
        Tip        = MajakConst.DefaultTip;
        ResultRecord     = RatingRecord.CreateEmpty();
        ResultRecordSave = RatingRecord.CreateEmpty();
        IsYakitori = rule.Yakitori;
    }

    public void InitKyoku()
    {
        Tehai.Clear();
        Sutehai.Clear();
        Furo.Clear();
        NukiDora.Clear();
        Mode           = PlayerMode.None;
        CurAct         = Act.Inv;
        RichiType      = RichiType.None;
        IsMenzen       = true;
        IsIppatsu      = false;
        IsTempai       = false;
        IsHoraForm     = false;
        IsFuriten      = false;
        IsNagashiMangan = true;
        KyokuPoint     = 0;
        KanCnt         = 0;
        PaoOrder       = MajakConst.InvalidOrder;
        _kuikaeForbiddenSerials.Clear();
        Yaku.Clear();
    }

    // ─── Tile Operations ─────────────────────────────────────────────────────

    public void Tsumo(PaiCode pai) => Tehai.Add(pai);

    /// <summary>Discard a tile from hand (TAP / RIC discard step).</summary>
    public ActionResult Tapai(PaiCode tapai)
    {
        if (IsKuikaeForbidden(tapai)) return ActionResult.ErrKuikae;
        if (!TryRemoveTehai(new[] { tapai.BipaiIndex }, 1, Sutehai)) return ActionResult.ErrPaiNotFoundInHand;
        _kuikaeForbiddenSerials.Clear();
        ClearIppatsu();
        if (RichiType == RichiType.None) IsFuriten = false;
        if (!tapai.IsYaochupai) IsNagashiMangan = false;
        return ActionResult.Ok;
    }

    /// <summary>Riichi discard (validates tenpai before committing).</summary>
    public ActionResult Richi(PaiCode tapai)
    {
        if (RichiType != RichiType.None) return ActionResult.ErrAfterRichi;
        if (!IsMenzen) return ActionResult.ErrNotMenzen;

        int idx = FindInTehai(tapai.BipaiIndex);
        if (idx < 0) return ActionResult.ErrPaiNotFoundInHand;

        // Temporarily remove and verify tenpai
        var saved = Tehai[idx];
        Tehai.RemoveAt(idx);
        bool tenpai = new Hand(this).CheckTempai();
        Tehai.Insert(idx, saved);

        if (!tenpai) return ActionResult.ErrNotTempai;

    Tehai.RemoveAt(idx);
    Sutehai.Add(tapai);
    SortTehaiByCode();
        IsFuriten = false;
        if (!tapai.IsYaochupai) IsNagashiMangan = false;
        IsIppatsu = true;   // marks riichi pending; also ippatsu after SetRichi
        return ActionResult.Ok;
    }

    /// <summary>Confirm riichi — called after all players pass on the riichi discard.</summary>
    public void SetRichi(bool isFirstTurn)
    {
        RichiType  = isFirstTurn ? RichiType.Wrichi : RichiType.Richi;
        GamePoint -= 1000;
        // IsIppatsu stays true for ippatsu yaku
    }

    public void ClearIppatsu() => IsIppatsu = false;
    public void ClearNagashiMangan() => IsNagashiMangan = false;
    public void ClearYakitori() => IsYakitori = false;

    /// <summary>Set temp furiten (failed to ron another's discard).</summary>
    public void SetTempFuriten() => IsFuriten = true;

    /// <summary>Ryuukyoku declaration (TAO).</summary>
    public ActionResult Taopai()
    {
        var counts = new int[34];
        foreach (var tile in Tehai)
            counts[tile.GetSerial()]++;

        int kindCount = 0;
        for (int suit = 0; suit < 3; suit++)
        {
            if (counts[suit * 9] != 0) kindCount++;
            if (counts[suit * 9 + 8] != 0) kindCount++;
        }
        for (int honor = 27; honor < 34; honor++)
            if (counts[honor] != 0) kindCount++;

        return kindCount >= 9 ? ActionResult.Ok : ActionResult.ErrCannotHora;
    }

    // ─── Furo Operations ─────────────────────────────────────────────────────

    public ActionResult Chi(int tapaiOrder, PaiCode curTapai, int[] bipaiIndex)
    {
        var furo = new FuroBlock { Act = Act.Chi, TapaiOrder = tapaiOrder };
        furo.Tiles.Add(curTapai);
        if (!TryRemoveTehai(bipaiIndex, 2, furo.Tiles)) return ActionResult.ErrPaiNotFoundInHand;
        furo.Tiles.Sort((a, b) => a.GetSerial().CompareTo(b.GetSerial()));
        Furo.Add(furo);
        IsMenzen = false;
        SetChiKuikae(curTapai, furo);
        return ActionResult.Ok;
    }

    public ActionResult Pon(int tapaiOrder, PaiCode curTapai, int[] bipaiIndex)
    {
        ProcessPao(curTapai, tapaiOrder);
        var furo = new FuroBlock { Act = Act.Pon, TapaiOrder = tapaiOrder };
        furo.Tiles.Add(curTapai);
        if (!TryRemoveTehai(bipaiIndex, 2, furo.Tiles)) return ActionResult.ErrPaiNotFoundInHand;
        Furo.Add(furo);
        IsMenzen = false;
        SetPonKuikae(curTapai);
        return ActionResult.Ok;
    }

    public bool IsKuikaeForbidden(PaiCode tile)
        => _kuikaeForbiddenSerials.Contains(tile.GetSerial());

    private void SetChiKuikae(PaiCode calledTile, FuroBlock furo)
    {
        _kuikaeForbiddenSerials.Clear();
        int calledSerial = calledTile.GetSerial();
        int firstSerial = furo.Tiles[0].GetSerial();
        _kuikaeForbiddenSerials.Add(calledSerial);

        int alternativeSerial = calledSerial == firstSerial
            ? firstSerial + 3
            : calledSerial == firstSerial + 2
                ? firstSerial - 1
                : -1;
        if (alternativeSerial >= 0 && alternativeSerial / 9 == firstSerial / 9)
            _kuikaeForbiddenSerials.Add(alternativeSerial);
    }

    private void SetPonKuikae(PaiCode calledTile)
    {
        _kuikaeForbiddenSerials.Clear();
        _kuikaeForbiddenSerials.Add(calledTile.GetSerial());
    }

    public ActionResult MinKan(int tapaiOrder, PaiCode curTapai, int[] bipaiIndex)
    {
        ProcessPao(curTapai, tapaiOrder);
        var furo = new FuroBlock { Act = Act.Kan, TapaiOrder = tapaiOrder };
        furo.Tiles.Add(curTapai);
        if (!TryRemoveTehai(bipaiIndex, 3, furo.Tiles)) return ActionResult.ErrPaiNotFoundInHand;
        Furo.Add(furo);
        IsMenzen = false;
        KanCnt++;
        return ActionResult.Ok;
    }

    public ActionResult AnKan(int[] bipaiIndex)
    {
        if (RichiType != RichiType.None)
        {
            int serial = Tehai.Last().GetSerial();
            if (serial < 27 && !new Hand(this).CheckAnkan(serial))
                return ActionResult.ErrAnkanAfterRichi;
        }

        var furo = new FuroBlock { Act = Act.Ank, TapaiOrder = Order };
        if (!TryRemoveTehai(bipaiIndex, 4, furo.Tiles)) return ActionResult.ErrPaiNotFoundInHand;
        Furo.Add(furo);
        KanCnt++;
        return ActionResult.Ok;
    }

    public ActionResult ChaKan(PaiCode tehaiTile)
    {
        // Find the existing pon block with matching tile
        int idx = FindInTehai(tehaiTile.BipaiIndex);
        if (idx < 0) return ActionResult.ErrPaiNotFoundInHand;

        int serial = tehaiTile.GetSerial();
        var pon = Furo.FirstOrDefault(f => f.Act == Act.Pon && f.Tiles[0].GetSerial() == serial);
        if (pon == null) return ActionResult.ErrInvalidMode;

        pon.Act = Act.Cha;
        if (!TryRemoveTehai(new[] { tehaiTile.BipaiIndex }, 1, pon.Tiles)) return ActionResult.ErrPaiNotFoundInHand;
        KanCnt++;
        return ActionResult.Ok;
    }

    public ActionResult Hua(PaiCode tehaiTile)
    {
        if (!tehaiTile.IsHuapai) return ActionResult.ErrHuapai;

    if (!TryRemoveTehai(new[] { tehaiTile.BipaiIndex }, 1, NukiDora)) return ActionResult.ErrPaiNotFoundInHand;
        return ActionResult.Ok;
    }

    // ─── Queries ─────────────────────────────────────────────────────────────

    public bool CheckTempai(PaiCode pai) => new Hand(this).CheckTempai(pai.GetSerial());
    public bool CheckTempai()            => new Hand(this).CheckTempai();
    public bool CheckHoraForm()          => new Hand(this).CheckHoraForm();
    public bool CheckHoraForm(PaiCode p) => new Hand(this).CheckHoraForm(p.GetSerial());

    public bool CheckFuriten()
    {
        if (IsFuriten) return true;
        var hand = new Hand(this);
        foreach (var t in Sutehai)
            if (hand.CheckHoraForm(t.GetSerial())) return true;
        return false;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private int FindInTehai(int bipaiIndex)
    {
        for (int i = 0; i < Tehai.Count; i++)
            if (Tehai[i].BipaiIndex == bipaiIndex) return i;
        return -1;
    }

    private bool TryRemoveTehai(int[] bipaiIndex, int count, List<PaiCode> destination)
    {
        if (count > 4) return false;

        var indexes = new int[count];
        var used = new bool[Tehai.Count];
        for (int i = 0; i < count; i++)
        {
            int found = -1;
            for (int j = 0; j < Tehai.Count; j++)
            {
                if (!used[j] && Tehai[j].BipaiIndex == bipaiIndex[i])
                {
                    found = j;
                    used[j] = true;
                    break;
                }
            }
            if (found < 0) return false;
            indexes[i] = found;
        }

        for (int i = 0; i < count; i++) destination.Add(Tehai[indexes[i]]);
        Array.Sort(indexes, (a, b) => b.CompareTo(a));
        for (int i = 0; i < count; i++) Tehai.RemoveAt(indexes[i]);
        SortTehaiByCode();
        return true;
    }

    private void SortTehaiByCode() => Tehai.Sort((a, b) => a.Code.CompareTo(b.Code));

    private void ProcessPao(PaiCode tapai, int tapaiOrder)
    {
        if (tapai.IsSangenpai)
        {
            int cnt = Furo.Count(f => f.Tiles[0].IsSangenpai);
            if (cnt == 2) PaoOrder = tapaiOrder;
        }
        else if (tapai.IsFonpai)
        {
            int cnt = Furo.Count(f => f.Tiles[0].IsFonpai);
            if (cnt == 3) PaoOrder = tapaiOrder;
        }
    }
}
