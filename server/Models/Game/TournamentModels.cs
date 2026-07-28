namespace MajakServer.Models.Game;

using System.Globalization;
using MajakServer.Models.Protocol;

// ─────────────────────────────────────────────────────────────────────────────
// 定数 — 原典: HMajDef.h TRNMNT_* defines
// ─────────────────────────────────────────────────────────────────────────────
public static class TournamentConst
{
    public const int    PhaseFull           = 10;   // TRNMNT_PLAYPHASE_FULL
    public const int    PhaseHalf           = 5;    // TRNMNT_PLAYPHASE_HALF
    public const int    ReserveMaxDays      = 8;    // 8日先まで予約可
    public const int    ReserveMinHours     = 1;    // 最短1時間後から予約可
    public const int    JoinOpenHours       = 1;    // 試合開始1時間前から参加受付開始
    public const int    JoinOpenMinutes     = 10;   // +10分の余裕
    public const int    PlayStartBefore     = 5;    // マッチング開始の5分前にルーム予約
    public const int    PlayEndAfter        = 60;   // 試合終了後60分間観戦可
    public const double MaxRoomDivision     = 0.8;  // チャンネル最大ルーム数の80%まで使用可
    public const long   JoinMoneyMin        = 0;
    public const long   JoinMoneyMax        = 10_000;
    public const long   GetMoneyMin         = 0;
    public const long   GetMoneyMax         = 100_000;
    public const int    NameLenMin          = 8;
    public const int    NameLenMax          = 30;
    public const int    PwLenMax            = 8;
    public const int    MaxRoomNumGlobal    = 200;  // 同時登録可能ルーム上限
    public const int    CheckFlagRegTemp    = 0;    // バリデーションのみ
    public const int    CheckFlagReg        = 1;    // 本登録
    public const string NpcMemberNo        = "*AI*";
    public const double RegMoneyMargin      = 0.10; // 登録費 = 賞金合計 × 1.10
    public const double JoinMoneyGetProb    = 0.90; // 主催者取り分 = 参加費合計 × 0.90
    public const int    ExtraManageValue    = 16;   // 16人以上の大会を主催回数カウント対象にする
}

// ─────────────────────────────────────────────────────────────────────────────
// 状態定数 — 原典: TRNMNT_PLAN_STATUS enum
// ─────────────────────────────────────────────────────────────────────────────
public static class TournamentPlanStatus
{
    public const int Init   = 0;
    public const int End    = 1;  // 終了
    public const int Join   = 2;  // 参加受付中
    public const int Wait   = 3;  // 対局待ち (マッチング済み)
    public const int Play   = 4;  // 対局中
    public const int Reject = 5;  // 破棄 (参加者不足)
    public const int Stop   = 9;  // 中止 (メンテナンス等)
}

// ─────────────────────────────────────────────────────────────────────────────
// 参加状態定数 — 原典: TRNMNT_JOIN_STATUS enum
// ─────────────────────────────────────────────────────────────────────────────
public static class TournamentJoinStatus
{
    public const int Init       = 0;
    public const int End        = 1;  // 終了
    public const int Join       = 2;  // 参加中
    public const int Cancel     = 3;  // キャンセル
    public const int Exit       = 4;  // 離脱・中止
    public const int JoinInRoom = 5;  // 対局ルーム入室中
}

// ─────────────────────────────────────────────────────────────────────────────
// 対局形式定数 — 原典: TRNMNT_PLAY_MODE, TRNMNT_PLAY_NUM
// ─────────────────────────────────────────────────────────────────────────────
public static class TournamentPlayMode
{
    public const int OneWin = 1;  // 1人勝ち上がり
    public const int TwoWin = 2;  // 2人勝ち上がり
}
public static class TournamentPlayNum
{
    public const int OnePlay = 1;  // 1半荘
    public const int TwoPlay = 2;  // 2半荘
}

public static class TournamentPresentKind
{
    public const int ResultPlan  = 1;
    public const int ResultGrade = 2;
    public const int RejectPlan  = 3;
    public const int RejectJoin  = 4;
    public const int StopPlan    = 5;
    public const int StopJoin    = 6;
    public const int Title       = 7;
}

public static class TournamentPresentItemKind
{
    public const int Money      = 1;
    public const int MajakTitle = 2;
}

// ─────────────────────────────────────────────────────────────────────────────
// プレイ時間テーブル — 原典: s_stTournamentPlayTime[] (HMajCommon.h)
// ─────────────────────────────────────────────────────────────────────────────
public record TournamentPlayTimeInfo(int PlayTimeNo, int PlayTimeMin, int PlayCutTime, int PlayTimeMax);

public static class TournamentTables
{
    /// <summary>原典: s_stTournamentPlayTime[]</summary>
    public static readonly TournamentPlayTimeInfo[] PlayTimes =
    {
        new(1, 30, 15, 25),  // 30分
        new(2, 40, 25, 35),  // 40分
        new(3, 50, 35, 45),  // 50分
        new(4, 60, 45, 55),  // 60分
        new(5, 10,  1,  7),  // 10分 (テスト)
        new(6, 20,  5, 15),  // 20分 (テスト)
    };

    /// <summary>
    /// プレイ情報テーブル — 原典: s_stTournamentPlayInfo[]
    /// (maxPlayerNum, playMode, maxPhase, maxRoomCount)
    /// </summary>
    public static readonly (int Players, int Mode, int MaxPhase, int MaxRoom)[] PlayInfos =
    {
        ( 4, 1, 10,  1),  // 4人  1勝ち上がり  1ラウンド  1ルーム
        (16, 1, 20,  4),  // 16人 1勝ち上がり  2ラウンド  4ルーム
        (64, 1, 30, 16),  // 64人 1勝ち上がり  3ラウンド 16ルーム
        ( 8, 2, 20,  2),  //  8人 2勝ち上がり  2ラウンド  2ルーム
        (16, 2, 30,  4),  // 16人 2勝ち上がり  3ラウンド  4ルーム
        (32, 2, 40,  8),  // 32人 2勝ち上がり  4ラウンド  8ルーム
    };

    /// <summary>
    /// ルーム割り当てテーブル — 原典: s_stTournamentRoomInfo[]
    /// (phase, maxPlayers, playMode, roomCount, subIdStart, subIdEnd)
    /// </summary>
    public static readonly (int Phase, int Players, int Mode, int Rooms, int SubStart, int SubEnd)[] RoomInfos =
    {
        // フェーズ1
        (10,  4, 1,  1,  1,  1),
        (10, 16, 1,  4,  1,  4),
        (10, 64, 1, 16,  1, 16),
        (10,  8, 2,  2,  1,  2),
        (10, 16, 2,  4,  1,  4),
        (10, 32, 2,  8,  1,  8),
        // フェーズ2
        (20, 16, 1,  1,  5,  5),
        (20, 64, 1,  4, 17, 20),
        (20,  8, 2,  1,  3,  3),
        (20, 16, 2,  2,  5,  6),
        (20, 32, 2,  4,  9, 12),
        // フェーズ3
        (30, 64, 1,  1, 21, 21),
        (30, 16, 2,  1,  7,  7),
        (30, 32, 2,  2, 13, 14),
        // フェーズ4
        (40, 32, 2,  1, 15, 15),
    };

    /// <summary>PROCODE テーブル — 原典: s_stTournamentProCodeForMoneyLog[]</summary>
    public static readonly (int Kind, string ProCode)[] ProCodesForMoneyLog =
    {
        (TournamentPresentKind.ResultPlan,  GameConst.EvtCodeTournamentResultPlan),
        (TournamentPresentKind.ResultGrade, GameConst.EvtCodeTournamentResultGrade),
        (TournamentPresentKind.RejectPlan,  GameConst.EvtCodeTournamentRejectPlan),
        (TournamentPresentKind.RejectJoin,  GameConst.EvtCodeTournamentRejectJoin),
        (TournamentPresentKind.StopPlan,    GameConst.EvtCodeTournamentStopPlan),
        (TournamentPresentKind.StopJoin,    GameConst.EvtCodeTournamentStopJoin),
    };

    /// <summary>主催回数称号 — 原典: s_stTournamentTitleInfo[]</summary>
    public static readonly (string TitleId, int PlanNum)[] TitleInfos =
    {
        ("mjkt600", 1),
        ("mjkt601", 5),
        ("mjkt602", 10),
        ("mjkt603", 50),
        ("mjkt604", 100),
    };

    public static TournamentPlayTimeInfo? GetPlayTime(int no)
        => Array.Find(PlayTimes, t => t.PlayTimeNo == no);

    public static string GetProCodeForMoneyLog(int tournamentKind)
        => Array.Find(ProCodesForMoneyLog, p => p.Kind == tournamentKind).ProCode
           ?? GameConst.EvtCodeGeneralCode;

    public static string? GetTitleIdForManageCount(int manageCount)
        => Array.Find(TitleInfos, title => title.PlanNum == manageCount).TitleId;

    public static (int MaxPhase, int MaxRoom) GetPlayInfo(int players, int mode)
    {
        foreach (var (p, m, ph, r) in PlayInfos)
            if (p == players && m == mode) return (ph, r);
        return (0, 0);
    }

    public static (int Rooms, int SubStart, int SubEnd) GetRoomInfo(int phase, int players, int mode)
    {
        // PhaseHalf(5) を FULL(10) の倍数に正規化 — 原典: SetTournamentMatchingInfo
        int chkPhase = phase;
        if (chkPhase % TournamentConst.PhaseFull != 0)
            chkPhase += TournamentConst.PhaseHalf;

        foreach (var (ph, p, m, r, s, e) in RoomInfos)
            if (ph == chkPhase && p == players && m == mode) return (r, s, e);
        return (0, 0, 0);
    }

    /// <summary>次回開始/打ち切り/終了予定日時設定 — 原典: SetTournamentNextStartAndCutDt</summary>
    public static bool SetNextStartAndCut(TournamentPlan plan)
    {
        int phaseUnit = plan.PlayNum == TournamentPlayNum.OnePlay
            ? TournamentConst.PhaseFull : TournamentConst.PhaseHalf;
        int index = (plan.PlayPhase - phaseUnit) / phaseUnit;
        if (index < 0 || index >= plan.StartPlanDtAll.Count)
            return false;

        if (!DateTime.TryParseExact(
                plan.StartPlanDtAll[index],
                "yyyy/MM/dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var nextStart))
        {
            return false;
        }

        var pt = GetPlayTime(plan.PlayTime);
        if (pt == null) return false;

        plan.NextStartDt = nextStart;
        plan.NextCutDt   = nextStart.AddMinutes(pt.PlayCutTime);
        plan.NextEndDt   = nextStart.AddMinutes(pt.PlayTimeMax);
        return true;
    }

    /// <summary>総対局数 — 原典: GetTournamentMaxPlayNum</summary>
    public static int GetMaxPlayNum(int players, int mode, int playNum)
    {
        var (maxPhase, _) = GetPlayInfo(players, mode);
        if (maxPhase == 0) return 0;
        return maxPhase / (playNum == TournamentPlayNum.OnePlay
            ? TournamentConst.PhaseFull : TournamentConst.PhaseHalf);
    }

    /// <summary>登録費計算 — 原典: GetTournamentPlanMoney</summary>
    public static long CalcPlanMoney(long[] gradeMoney)
    {
        long total = gradeMoney.Sum();
        return (long)(total * (1.0 + TournamentConst.RegMoneyMargin));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// データモデル — 原典: TOURNAMENT_PLAN struct
// ─────────────────────────────────────────────────────────────────────────────
public class TournamentPlan
{
    public long     SeqNo          { get; set; }
    public string   PlayName       { get; set; } = "";
    public int      PlayStatus     { get; set; }
    public int      PlayPhase      { get; set; }
    public int      PlayerNum      { get; set; }
    public int      MaxPlayerNum   { get; set; }
    public int      MaxRoomNum     { get; set; }
    public string   RoomOption     { get; set; } = "";
    public string   Password       { get; set; } = "";
    public int      MaxViewer      { get; set; }
    public int      PlayNum        { get; set; }
    public int      PlayTime       { get; set; }
    public int      PlayMode       { get; set; }
    public long     JoinMoney      { get; set; }
    public long[]   GradeMoney     { get; set; } = new long[4];
    public string   PlanMemberNo   { get; set; } = "";
    public string[] ResultMemberNo { get; set; } = ["", "", "", ""];
    public string   PlaySchedule   { get; set; } = "";
    public List<string> StartPlanDtAll { get; set; } = new();

    public DateTime JoinStartDt    { get; set; }
    public DateTime MatchStartDt   { get; set; }
    public DateTime PlayStartDt    { get; set; }
    public DateTime PlayEndDt      { get; set; }
    public DateTime ViewEndDt      { get; set; }
    public DateTime NextStartDt    { get; set; }
    public DateTime NextEndDt      { get; set; }
    public DateTime NextCutDt      { get; set; }
    public int      PlayEndCount   { get; set; }

    public bool IsActive =>
        PlayStatus is TournamentPlanStatus.Join or
                      TournamentPlanStatus.Wait or
                      TournamentPlanStatus.Play;

    public bool IsJoinable(DateTime now) =>
        PlayStatus == TournamentPlanStatus.Join
        && now >= JoinStartDt && now < MatchStartDt;
}

// ─────────────────────────────────────────────────────────────────────────────
// データモデル — 原典: TOURNAMENT_DETAIL struct
// ─────────────────────────────────────────────────────────────────────────────
public class TournamentDetail
{
    public long     SeqNo          { get; set; }
    public int      SubId          { get; set; }
    public int      RoomId         { get; set; }
    public string[] PlayerMemberNo { get; set; } = ["", "", "", ""];
    public string[] JoinMemberNo   { get; set; } = ["", "", "", ""];
    public int[]    PointTmp       { get; set; } = new int[4];
    public int[]    Point          { get; set; } = new int[4];
    public string[] GradePlayerMemberNo { get; set; } = ["", "", "", ""];
    public string[] GradeMemberNo  { get; set; } = ["", "", "", ""];
    public DateTime StartPlanDt    { get; set; }
    public DateTime EndPlanDt      { get; set; }
    public DateTime StartDt        { get; set; }
    public DateTime EndDt          { get; set; }

    public bool IsFinished => EndDt != default;
}

// ─────────────────────────────────────────────────────────────────────────────
// データモデル — 原典: TOURNAMENT_JOIN struct
// ─────────────────────────────────────────────────────────────────────────────
public class TournamentJoin
{
    public string   MemberNo       { get; set; } = "";
    public long     JoinSeqNo      { get; set; }
    public string   JoinMemberNo   { get; set; } = "00";
    public int      JoinStatus     { get; set; }
    public int      TotManageNum   { get; set; }
    public int      ManageNum      { get; set; }
    public DateTime LastManageDt   { get; set; }

    public bool IsActiveJoiner =>
        JoinStatus == TournamentJoinStatus.Join ||
        JoinStatus == TournamentJoinStatus.JoinInRoom;
}

// ─────────────────────────────────────────────────────────────────────────────
// データモデル — 原典: TOURNAMENT_LIMIT struct
// ─────────────────────────────────────────────────────────────────────────────
public class TournamentLimit
{
    public int      LimitNo        { get; set; }
    public int      LimitValid     { get; set; }
    public DateTime LimitStartDt   { get; set; }
    public DateTime LimitEndDt     { get; set; }
}
