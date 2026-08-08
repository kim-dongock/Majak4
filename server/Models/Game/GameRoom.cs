using MajakServer.Engine;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;

namespace MajakServer.Models.Game;

public sealed record TrainingNpcProfile(string Name, string AvatarId, string Sex);

/// <summary>
/// ゲームルーム状態 (メモリ専用) — 原典: HMajRoomServer
/// </summary>
public class GameRoom
{
    public int    RoomId      { get; set; }
    public string ChannelId   { get; set; } = "";
    public string RoomTitle   { get; set; } = "";    // 部屋名
    public string CreatorNo   { get; set; } = "";   // 作成者 MemberNo (原典: m_pRoomInfo->m_szCreatorId)
    public bool   IsPrivate   { get; set; }
    public string RoomType    { get; set; } = "";    // 原典: m_szRoomType
    public string Password    { get; set; } = "";    // 部屋パスワード
    public string RoomOption  { get; set; } = "";   // MAJAKRULEMAST.ROOMOPTION 形式
    public string ServerUrl   { get; set; } = "";   // このルームを保持するゲームサーバー URL
    public long   MoneyRate   { get; set; }
    public long   UnitMoney   { get; set; }          // 原典: m_nUnitMoney
    public long   MinMoney    { get; set; }
    public long   MaxMoney    { get; set; }
    public int    MinCnt      { get; set; }           // 原典: m_nMinCnt
    public int    LimitCnt    { get; set; } = 4;    // 参加上限人数 (原典: m_pRoomInfo->m_nLimitCnt)
    public int    MaxViewer   { get; set; } = 12;   // 観戦者上限 (原典: m_pRoomInfo->m_nMaxViewer)
    public string SubId       { get; set; } = "";   // チャンネル SubId (例: "00R5A") チャンネル種別判定用
    public int    GameRate    { get; set; } = 1;    // ゲームレート倍率 (原典: GetRoomInfo()->GetGameRate())
    public int    RatingRuleType { get; set; } = 1; // m_stFactor.m_nRatingRuleType
    public float  RatingK     { get; set; } = 20f;  // m_stFactor.K
    public float  RatingRs    { get; set; } = 400f; // m_stFactor.R_s
    public int    RatingNoviceThreshold { get; set; } = 0; // m_stFactor.N_bonus
    public int    RatingBonusThreshold  { get; set; } = 0; // m_stFactor.R_bonus
    public int    RatingExpertThreshold { get; set; } = int.MaxValue; // m_stFactor.R_expert
    public int    RatingBonus           { get; set; } = 0; // m_stFactor.Bonus
    public int    RatingNoviceRate      { get; set; } = 1; // m_stFactor.r_novice
    public int    RatingExpertRate      { get; set; } = 1; // m_stFactor.r_expert
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTimeOffset? NoActiveMembersSince { get; set; }

    // ─── チャンネル種別判定 (原典: HMajChnlInfo::IsXxx()) ───────────────────────
    // SubId 例: "0090A" → [0]='0', [1]='9', [2]='0', [3]='9', [4]='A'
    //          "1090A" → [0]='1' (ビギナー)
    //          "00G5A" → [2]='G' (グレード)
    //          "00R5A" → [2]='R' (競技)
    //          "00T5A" → [2]='T' (練習)
    //          "00C5A" → [2]='C' (カップ)
    //          "00H5A" → [2]='H' (トーナメント)
    //          "00000" → (サークル)
    private bool SubIdAt(int i, char c) => SubId.Length > i && SubId[i] == c;
    private char SubIdChar(int i)       => SubId.Length > i ? SubId[i] : '\0';

    /// 通常チャンネル (グレード/競技でない)  原典: IsRegular()  SubId[2]!='G'
    public bool IsRegularChannel    => SubIdChar(2) != 'G';
    /// グレードモードチャンネル              原典: IsGradeMode() SubId[2]=='G'
    public bool IsGradeChannel      => SubIdAt(2, 'G');
    /// 競技チャンネル                        原典: IsCompete()   SubId[2]=='R'
    public bool IsCompeteChannel    => SubIdAt(2, 'R');
    /// 上級チャンネル (グレード/競技でない)  原典: IsHiClass()   SubId[2]!='G'
    public bool IsHiClassChannel    => SubIdChar(2) != 'G';
    /// 練習チャンネル                        原典: SubId[2]=='T'
    public bool IsTrainingChannel   => SubIdAt(2, 'T');
    /// カップチャンネル                      原典: SubId[2]=='C'
    public bool IsCupChannel        => SubIdAt(2, 'C');
    /// フェスティブカップ                    原典: IsFestive()   SubId[2]=='C'&&SubId[4]=='A'
    public bool IsFestiveChannel    => SubIdAt(2, 'C') && SubIdAt(4, 'A');
    /// ハイイベントカップ                    原典: IsHiEvent()   SubId[2]=='C'&&SubId[4]>='F'&&!='Z'
    public bool IsHiEventChannel    => SubIdAt(2, 'C') && SubIdChar(4) >= 'F' && SubIdChar(4) != 'Z';
    /// トーナメントチャンネル                原典: IsTournament() SubId[2]=='H'
    public bool IsTournamentChannel => SubIdAt(2, 'H');
    /// ビギナーチャンネル                    原典: IsBeginnerChannel() SubId[0]=='1'
    public bool IsBeginnerChannel   => SubIdAt(0, '1');
    /// サークルチャンネル                    原典: IsCircle()    SubId=="00000"
    public bool IsCircleChannel     => SubId == "00000";
    /// オートマッチングチャンネル            原典: IsAutoMatching() SubId[1]=='Z'
    public bool IsAutoMatchChannel  => SubIdChar(1) == 'Z';

    // ─── カップチャンネル設定 (MAJAKCUPMAST から取得) ──────────────────────────
    /// <summary>カップ ID — MJK_EVTRAT.CUPID 参照用 (CUP_JTID_GAME_SUM 時に使用)</summary>
    public int CupId             { get; set; }
    /// <summary>カップシーケンス番号 — MJK_EVTRAT.SEQ 参照用</summary>
    public int CupSeq            { get; set; }
    /// <summary>採点方式 — 原典: m_nJudgementType (CUP_JTID_NONE=-1 / CUP_JTID_GAME_SUM=8)</summary>
    public int CupJudgementType  { get; set; } = -1;
    /// <summary>ポイント集計方式 — 原典: m_nCupPointSumType (SUM_MAX=1 / SUM_MIX=2 / SUM_SUC=3)</summary>
    public int CupPointSumType   { get; set; }
    /// <summary>カップ最大対局数 — 原典: m_nMaxMatchCntLimit / CUP_MATCHCNT_NOLIMIT=-1</summary>
    public int CupMaxMatchCntLimit { get; set; } = -1;
    /// <summary>通常参加条件 — 原典: m_nConditionRegular / CUP_ONLYREGULAR=1</summary>
    public int CupConditionRegular { get; set; }
    /// <summary>課金条件 — 原典: m_nConditionBilling / EVT_BILLING_*</summary>
    public int CupConditionBilling { get; set; }
    /// <summary>本戦参加制限 — 原典: m_bCupEntryLimited</summary>
    public bool CupEntryLimited { get; set; }
    /// <summary>通常役条件ビットマップ — 原典: m_szNormalYakuCondition</summary>
    public string CupNormalYakuCondition { get; set; } = "";
    /// <summary>役満条件ビットマップ — 原典: m_szYakumanCondition</summary>
    public string CupYakumanCondition { get; set; } = "";

    // ─── サークルチャンネル設定 ────────────────────────────────────────────────
    /// <summary>
    /// このルームへの入室を許可するサークル一覧 — 原典: HMajRoomServer::m_mapCircleInfo
    /// キー = CircleId, 値 = CircleName。空の場合はサークル制限なし。
    /// ルーム作成者が keyCircleId* で指定したサークルのみ入室可。
    /// </summary>
    public Dictionary<string, string> RequiredCircles { get; set; } = new();

    public GameRoomState State { get; set; } = GameRoomState.Waiting;

    // 席 (0-3), null = 空席
    public MajakPlayer?[] Seats { get; } = new MajakPlayer?[4];

    // 練習場の空席 NPC 表示情報。対局開始ごとに再生成し、終局結果まで同じ値を使う。
    public TrainingNpcProfile?[] TrainingNpcProfiles { get; } = new TrainingNpcProfile?[4];

    // OKボタン状態配列 (原典: m_bReadyToPlay[PLAYER_MAX_COUNT])
    public bool[] OkButtonStates { get; } = new bool[4];

    // ─── Mahjong Game Engine ─────────────────────────────────────────────────
    /// <summary>
    /// C++ MajakGameLogic の C# 移植エンジン。
    /// 牌配布・アクション処理・役判定・精算を担当する。
    /// </summary>
    public MajakGameLogic Engine { get; } = new();

    /// <summary>
    /// エンジン操作の排他制御 (PerformanceAnalysis §1-2)
    /// 複数プレイヤーが同時にアクションパケットを送信した場合の
    /// 並行アクセスを防ぐ。GamePlayProcessAsync で使用する。
    /// </summary>
    public SemaphoreSlim EngineLock { get; } = new SemaphoreSlim(1, 1);

    // 座席→エンジン順のマッピング (StartGameLogic で設定)
    public int[] SeatToEngineOrder { get; } = new[] { -1, -1, -1, -1 };

    // Web クライアント入力要求の deadline / sequence (AP-14)
    private long _actionSeq;
    private int _gameActionsStarted;
    public PendingActionPrompt?[] PendingActions { get; } = new PendingActionPrompt?[4];
    public int[] KyokuTimeBankMs { get; } = new int[4];

    // Result settlement idempotency guard. 0=not started, 1=processing, 2=completed.
    private int _gameReportProcessState;

    public long IssueActionSeq() => Interlocked.Increment(ref _actionSeq);

    public bool TryStartGameActions()
        => Interlocked.CompareExchange(ref _gameActionsStarted, 1, 0) == 0;

    public void ResetGameActions()
        => Volatile.Write(ref _gameActionsStarted, 0);

    public bool TryBeginGameReportProcess()
        => Interlocked.CompareExchange(ref _gameReportProcessState, 1, 0) == 0;

    public void CompleteGameReportProcess()
        => Volatile.Write(ref _gameReportProcessState, 2);

    public void ResetGameReportProcess()
        => Volatile.Write(ref _gameReportProcessState, 0);

    public void ClearPendingActions()
    {
        Array.Clear(PendingActions, 0, PendingActions.Length);
    }

    // Web クライアントのゲーム画面受信準備完了待ち (mjkc4e → MJPID_INIHAN の間)
    public object GameClientReadyLock { get; } = new();
    public HashSet<string> GameClientReadyConnectionIds { get; } = new();
    public TaskCompletionSource<bool>? GameClientReadyTcs { get; set; }

    // プレイ履歴 (原典: m_vecPlayHist — 牌譜データ)
    public List<object> PlayHistory { get; } = new();

    // 最終結果 payload。c32e を取り逃したクライアントの resync で再送する。
    public IReadOnlyDictionary<string, object?>? LastGameReportPayload { get; set; }

    // 観戦者リスト (原典: m_vecRoomMember の Viewer スロット)
    // 原典: AutoViewRoom で AddViewer() → m_vecRoomMember.m_stViewerPos に追加
    public List<MajakPlayer> Viewers { get; } = new();

    // ─── バニシュ情報 (原典: m_stBanishInfo in HMajRoomServer) ─────────────────
    /// <summary>
    /// バニシュ (Banish = 退場予約) 状態。
    /// 原典: struct BANISHINFO { m_bPreBanishing, m_bReserveBanishing, m_szReserveBanishingMember }
    ///   InitBanish()       で全フィールドをリセット
    ///   AddToParser 系で G::keyPreBanishing / keyReserveBanishing / keyMemberNo へ送出
    /// </summary>
    public BanishInfo BanishInfo { get; set; } = new();

    // OKボタン収集 (後方互換用 — SendOkButtonCommand から利用)
    private readonly HashSet<string> _okSet = new();

    public int PlayerCount => Seats.Count(s => s != null);
    public int ActivePlayerCount => Seats.Count(s => s != null && !s.IsOutPlayer);
    public int ViewerCount => Viewers.Count;

    public void AddPlayer(MajakPlayer player, int seat)
    {
        Seats[seat] = player;
        player.IsOutPlayer = false;
        player.SeatPos = (uint)seat;
        player.EngineOrder = GameConst.PlayerMaxCount;
        player.RoomId  = RoomId;
        NoActiveMembersSince = null;
    }

    public bool RefreshPlayerConnection(MajakPlayer player)
    {
        for (int i = 0; i < Seats.Length; i++)
        {
            var seat = Seats[i];
            if (seat?.MemberNo != player.MemberNo) continue;
            seat.ConnectionId = player.ConnectionId;
            seat.NickName = player.NickName;
            seat.AvatarId = player.AvatarId;
            seat.Password = player.Password;
            seat.IpAddress = player.IpAddress;
            seat.ChannelId = player.ChannelId;
            seat.IsOutPlayer = false;
            player.RoomId = RoomId;
            player.SeatPos = (uint)i;
            player.EngineOrder = seat.EngineOrder;
            Seats[i] = seat;
            NoActiveMembersSince = null;
            return true;
        }
        return false;
    }

    public void RemovePlayer(string memberNo)
    {
        for (int i = 0; i < Seats.Length; i++)
        {
            if (Seats[i]?.MemberNo == memberNo)
            {
                Seats[i] = null;
                break;
            }
        }
        _okSet.Remove(memberNo);
    }

    public bool AddViewer(MajakPlayer player)
    {
        if (MaxViewer <= 0 || Viewers.Count >= MaxViewer)
            return false;

        if (Seats.Any(s => s?.MemberNo == player.MemberNo) || Viewers.Any(v => v.MemberNo == player.MemberNo))
            return false;

        player.RoomId   = RoomId;
        player.IsViewer = true;
        player.SeatPos  = (uint)Viewers.Count;
        player.EngineOrder = GameConst.PlayerMaxCount;
        Viewers.Add(player);
        if (ActivePlayerCount > 0)
            NoActiveMembersSince = null;
        return true;
    }

    public bool RemoveViewer(string memberNo)
    {
        int index = Viewers.FindIndex(v => v.MemberNo == memberNo);
        if (index < 0) return false;

        Viewers.RemoveAt(index);
        for (int i = 0; i < Viewers.Count; i++)
            Viewers[i].SeatPos = (uint)i;
        return true;
    }

    public MajakPlayer? FindPlayer(string memberNo)
        => Seats.FirstOrDefault(s => s?.MemberNo == memberNo);

    /// <summary>OKボタン登録。全員同意済みなら true を返す</summary>
    public bool RegisterOk(string memberNo)
    {
        _okSet.Add(memberNo);
        return _okSet.Count >= PlayerCount && PlayerCount >= 2;
    }

    public void ClearOk() => _okSet.Clear();

    public bool IsEmpty => Seats.All(s => s == null) && Viewers.Count == 0;
    public bool HasNoActiveMembers => ActivePlayerCount == 0 && Viewers.Count == 0;
    public bool HasNoActivePlayers => ActivePlayerCount == 0;

    // トーナメント管理 (原典: HMajRoomServer::m_stTournamentPlan / m_stTournamentDetail)
    public long TournamentSeqNo { get; set; }  // 0 = トーナメントルームでない
    public int  TournamentSubId { get; set; }

    // ゲーム進行状態 (原典: majak.m_stHanchanInfo 相当)
    public int  CurrentKyoku { get; set; }  // 現在の局番号
}

public class PendingActionPrompt
{
    public long ActionSeq { get; init; }
    public int SeatOrder { get; init; }
    public PlayerMode PlayerMode { get; init; }
    public DateTimeOffset IssuedAt { get; init; }
    public DateTimeOffset DeadlineAt { get; init; }
    public int BaseTimeMs { get; init; }
    public int KeepTimeMs { get; init; }
    public int TimeBankMsAtIssue { get; init; }
    public bool UsesTimeBank { get; init; }
}

/// <summary>
/// ゲーム結果レポート — 原典: HMajGameReport.h
/// </summary>
public class GameReport
{
    public int    RoomId     { get; set; }
    public string ChannelId  { get; set; } = "";
    public bool   PrivateYn  { get; set; }
    public string RoomOption { get; set; } = "";
    public long   MoneyRate  { get; set; }
    public long   MinMoney   { get; set; }
    public long   MaxMoney   { get; set; }

    public UserResult[] Users { get; } = new UserResult[4];

    public class UserResult
    {
        public string MemberNo      { get; set; } = "";
        public bool   IsConnect     { get; set; } = true;
        public bool   Connected     { get; set; } = true;
        public int    TeamId        { get; set; } = -1; // NO_TEAM_ID
        public int    Ranking       { get; set; }
        public int    Score         { get; set; }  // ゲームポイント
        public int    GameScore     { get; set; }  // ゲームスコア (m_nGamePoint)
        public int    SetPoint      { get; set; }  // セット点数
        public int    SetUma        { get; set; }  // ウマ
        public int    SetTor        { get; set; }  // 焼き鳥
        public int    SetTip        { get; set; }  // チップ
        public int    TipPoint      { get; set; }
        public int    TipMatchCnt   { get; set; }
        public int    PointSum      { get; set; }  // 総得点
        public bool   Yakitori      { get; set; }
        public int    Chip          { get; set; }
        public long   PrevMoney     { get; set; }
        public long   PrevLent      { get; set; }
        public long   DealerFee     { get; set; }
        public long   MoneyChange   { get; set; }
        public uint   MoneyChangeRatio { get; set; } = 1;
        public long   CurrMoney     { get; set; }
        public long   CurrLent      { get; set; }
        public string IpAddress     { get; set; } = "";
        public string Gateway       { get; set; } = "";
        public string MacAddr       { get; set; } = "";
        // 戦績統計
        public int    MatchCnt      { get; set; }
        public int    WinCnt        { get; set; }
        public int    DefeatCnt     { get; set; }
        public int    DrawCnt       { get; set; }
        public int    TurnCnt       { get; set; }
        public int    DaidaCnt      { get; set; }
        public int    KyokuCnt      { get; set; }
        public int    HoraCnt       { get; set; }
        public int    HoraPoint     { get; set; }
        public int    HojuCnt       { get; set; }
        public int    HojuPoint     { get; set; }
        public int    RichiCnt      { get; set; }
        public int    FuroCnt       { get; set; }
        public int    TobiCnt       { get; set; }
        public int    TobashiCnt    { get; set; }
        public int    DoraCnt       { get; set; }
        public int    UraDoraCnt    { get; set; }
        public int    RichiHoraCnt  { get; set; }
        // レーティング
        public int    Rating        { get; set; }
        public int    RatingChange  { get; set; }
        public int    PrevNLevel    { get; set; }
        public int    GemCount      { get; set; }
        // 経験値
        public int    Experience    { get; set; }
        public int    ExperienceGain { get; set; }
        // 段位モード専用 (原典: HMajMemberScore グレード関連フィールド)
        public int    PrevGradeLevel { get; set; }  // 対局前の段位
        public int    GradeLevel    { get; set; }  // 対局後の段位
        public int    PrevGradePoint { get; set; } // 対局前のポイント
        public int    GradePoint    { get; set; }  // 対局後のポイント
        public int    GradePointTmp { get; set; }  // 結果表示用ポイント
        public int    GradeAddPoint { get; set; }  // 今回の加減ポイント
        public int    GradeNextPoint { get; set; } // 次の段位までの展望ポイント
        public int    GradeUpDown   { get; set; }  // 0=STAY 1=UP 2=DOWN
        public bool   UpdateBeginner { get; set; } // 1段に進級したフラグ
        public bool   UpdateExtra   { get; set; }  // 10段位達成フラグ
    }
}
