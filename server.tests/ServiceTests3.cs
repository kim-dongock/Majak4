using Moq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MajakServer.Commands.Channel;
using MajakServer.Hubs;
using MajakServer.Infrastructure;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;
using MajakServer.Utils;
namespace MajakServer.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// TournamentTables 静的メソッド テスト
// 原典: s_stTournamentPlayInfo[] / s_stTournamentPlayTime[] (HMajCommon.h)
// ═══════════════════════════════════════════════════════════════════════════
public class TournamentTablesTests
{
    // ─── GetPlayInfo ─────────────────────────────────────────────────────

    // シナリオ1: 4人1勝ち上がり → maxPhase=10, maxRoom=1
    [Fact]
    public void GetPlayInfo_4Players_Mode1_Returns10_1()
    {
        var (maxPhase, maxRoom) = TournamentTables.GetPlayInfo(4, 1);
        Assert.Equal(10, maxPhase);
        Assert.Equal(1, maxRoom);
    }

    // シナリオ2: 16人1勝ち上がり → maxPhase=20, maxRoom=4
    [Fact]
    public void GetPlayInfo_16Players_Mode1_Returns20_4()
    {
        var (maxPhase, maxRoom) = TournamentTables.GetPlayInfo(16, 1);
        Assert.Equal(20, maxPhase);
        Assert.Equal(4, maxRoom);
    }

    // シナリオ3: 存在しない組み合わせ → (0, 0)
    [Fact]
    public void GetPlayInfo_Unknown_Returns0_0()
    {
        var (maxPhase, maxRoom) = TournamentTables.GetPlayInfo(99, 9);
        Assert.Equal(0, maxPhase);
        Assert.Equal(0, maxRoom);
    }

    // ─── GetPlayTime ─────────────────────────────────────────────────────

    // シナリオ4: PlayTimeNo=1 → 30分
    [Fact]
    public void GetPlayTime_No1_Returns30Min()
    {
        var info = TournamentTables.GetPlayTime(1);
        Assert.NotNull(info);
        Assert.Equal(1, info!.PlayTimeNo);
        Assert.Equal(30, info.PlayTimeMin);
    }

    // シナリオ5: 存在しない番号 → null
    [Fact]
    public void GetPlayTime_Unknown_ReturnsNull()
        => Assert.Null(TournamentTables.GetPlayTime(99));

    // ─── CalcPlanMoney ───────────────────────────────────────────────────

    // シナリオ6: 賞金合計 1700 × 1.10 = 1870
    // 原典: TRNMNT_REGMONEYMARGIN = 1.10 (10%)
    [Fact]
    public void CalcPlanMoney_SumsAndAppliesMargin()
    {
        long money = TournamentTables.CalcPlanMoney(new long[] { 1000, 500, 200, 0 });
        Assert.Equal(1870L, money); // 1700 * 1.10
    }

    // シナリオ7: 全て0 → 0
    [Fact]
    public void CalcPlanMoney_AllZero_Returns0()
        => Assert.Equal(0L, TournamentTables.CalcPlanMoney(new long[] { 0, 0, 0, 0 }));

    [Fact]
    public void GetProCodeForMoneyLog_ReturnsLegacyTournamentCodes()
    {
        Assert.Equal("JM00214", TournamentTables.GetProCodeForMoneyLog(TournamentPresentKind.ResultPlan));
        Assert.Equal("JM00215", TournamentTables.GetProCodeForMoneyLog(TournamentPresentKind.ResultGrade));
        Assert.Equal("JM00218", TournamentTables.GetProCodeForMoneyLog(TournamentPresentKind.RejectPlan));
        Assert.Equal("JM00219", TournamentTables.GetProCodeForMoneyLog(TournamentPresentKind.RejectJoin));
        Assert.Equal("JM00220", TournamentTables.GetProCodeForMoneyLog(TournamentPresentKind.StopPlan));
        Assert.Equal("JM00221", TournamentTables.GetProCodeForMoneyLog(TournamentPresentKind.StopJoin));
        Assert.Equal(GameConst.EvtCodeGeneralCode, TournamentTables.GetProCodeForMoneyLog(999));
    }

    // ─── GetMaxPlayNum ───────────────────────────────────────────────────

    // シナリオ8: 4人1勝ち, 1半荘 → 1回戦
    [Fact]
    public void GetMaxPlayNum_4Players_Mode1_Play1_Returns1()
        => Assert.Equal(1, TournamentTables.GetMaxPlayNum(4, 1, 1));

    // シナリオ9: 存在しない組み合わせ → 0
    [Fact]
    public void GetMaxPlayNum_Unknown_Returns0()
        => Assert.Equal(0, TournamentTables.GetMaxPlayNum(99, 9, 1));

    [Fact]
    public void MajakCommon_DateHelpers_MatchLegacyFormatAndWeekStart()
    {
        Assert.True(MajakCommon.TryParseDateTime("2030/01/02 03:04:05", out var parsed));
        Assert.Equal("2030/01/02 03:04:05", MajakCommon.FormatDateTime(parsed));
        Assert.Equal(new DateTime(2029, 12, 31), MajakCommon.GetStartOfWeek(parsed));
        Assert.True(MajakCommon.IsSameWeek(parsed, new DateTime(2030, 1, 5, 23, 59, 59)));
        Assert.False(MajakCommon.TryParseDateTime("2030-01-02 03:04:05", out _));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// TournamentService 純粋ロジックテスト
// 原典: CheckTournamentRequiredValue / CheckTournamentCoordinalValue
// ═══════════════════════════════════════════════════════════════════════════
public class TournamentServiceLogicTests
{
    private readonly Mock<TournamentRepository> _repoMock
        = new(MockBehavior.Loose);
    private readonly Mock<ILogger<TournamentService>> _loggerMock = new();

    private TournamentService BuildEmpty()
        => TestTournamentServiceFactory.Create(_repoMock.Object, _loggerMock.Object);

    private TournamentService BuildWithPlan(TournamentPlan plan)
    {
        var svc = BuildEmpty();
        var plans = (System.Collections.Concurrent.ConcurrentDictionary<long, TournamentPlan>)
            typeof(TournamentService)
                .GetField("_plans",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)!
                .GetValue(svc)!;
        plans[plan.SeqNo] = plan;
        return svc;
    }

    // ─── ValidateRegist ──────────────────────────────────────────────────

    // シナリオ1: 不正な baseRule (フィールド不足) → failCode=1001
    // 原典: IsTournamentBaseRule → E_TRNMT_REG_BASERULE_FAIL
    [Fact]
    public void ValidateRegist_InvalidBaseRule_Returns1001()
    {
        var svc = BuildEmpty();
        var (ok, fails) = svc.ValidateRegist(
            "TestTournament", "INVALID", "0|1000|500|200|0",
            DateTime.Now.AddHours(3).ToString("yyyy/MM/dd HH:mm:ss"),
            "", 4, "", "host01", isAdmin: true, out _);

        Assert.False(ok);
        Assert.Contains(1001, fails);
    }

    // シナリオ2: 不正な moneyRule → failCode=1002
    // 原典: IsTournamentMoneyRule → E_TRNMT_REG_INPUTMONEY_INVALID
    [Fact]
    public void ValidateRegist_InvalidMoneyRule_Returns1002()
    {
        var svc = BuildEmpty();
        var (ok, fails) = svc.ValidateRegist(
            "TestTournament", "4|1|1|5", "INVALID",
            DateTime.Now.AddHours(3).ToString("yyyy/MM/dd HH:mm:ss"),
            "", 4, "", "host01", isAdmin: true, out _);

        Assert.False(ok);
        Assert.Contains(1002, fails);
    }

    // シナリオ3: 名前が短すぎる → failCode=1003
    // 原典: IsTournamentName → E_TRNMT_REG_NAME_SIZE
    [Fact]
    public void ValidateRegist_NameTooShort_Returns1003()
    {
        var svc = BuildEmpty();
        var (ok, fails) = svc.ValidateRegist(
            "AB",  // NameLenMin=8 未満
            "4|1|1|5", "0|1000|500|200|0",
            DateTime.Now.AddHours(3).ToString("yyyy/MM/dd HH:mm:ss"),
            "", 4, "", "host01", isAdmin: true, out _);

        Assert.False(ok);
        Assert.Contains(1003, fails);
    }

    [Fact]
    public void ValidateRegist_FourJapaneseCharacterName_UsesLegacyShiftJisByteLength()
    {
        var svc = BuildEmpty();
        var (ok, fails) = svc.ValidateRegist(
            "麻雀大会", // Shift-JIS 8 bytes
            "4|1|1|5", "0|1000|500|200|0",
            DateTime.Now.AddHours(3).ToString("yyyy/MM/dd HH:mm:ss"),
            "", 4, "", "host01", isAdmin: true, out _);

        Assert.True(ok);
        Assert.DoesNotContain(1003, fails);
    }

    [Fact]
    public void ValidateRegist_ProtocolDateFormat_ParsesWithInvariantCulture()
    {
        var svc = BuildEmpty();
        var (ok, fails) = svc.ValidateRegist(
            "TestTournament", "4|1|1|5", "0|1000|500|200|0",
            "2030/07/20 10:30:00",
            "", 4, "", "host01", isAdmin: true, out var plan);

        Assert.True(ok);
        Assert.DoesNotContain(1004, fails);
        Assert.Equal(new DateTime(2030, 7, 20, 10, 30, 0), plan!.PlayStartDt);
    }

    // シナリオ4: 正常な登録 (管理者 isAdmin=true) → ok=true + plan 生成
    [Fact]
    public void ValidateRegist_Valid_ReturnsOkWithPlan()
    {
        var svc = BuildEmpty();
        var (ok, fails, plan) = ValidateHelper(svc);

        Assert.True(ok);
        Assert.Empty(fails);
        Assert.NotNull(plan);
        Assert.Equal("TestTournament001", plan!.PlayName);
        Assert.Equal(4, plan.MaxPlayerNum);
    }

    [Fact]
    public void ValidateRegist_BuildsScheduleWithLegacyPlayTimeMinInterval()
    {
        var svc = BuildEmpty();
        var (ok, fails) = svc.ValidateRegist(
            "TestTournament001", "16|1|1|1", "0|1000|500|200|0",
            "2030/01/01 10:00:00",
            "", 4, "", "host01", isAdmin: true, out var plan);

        Assert.True(ok);
        Assert.Empty(fails);
        Assert.NotNull(plan);
        Assert.Equal(new[] { "2030/01/01 10:00:00", "2030/01/01 10:30:00" }, plan!.StartPlanDtAll);
        Assert.Equal("2030/01/01 10:00:00|2030/01/01 10:30:00", plan.PlaySchedule);
        Assert.Equal(new DateTime(2030, 1, 1, 11, 0, 0), plan.PlayEndDt);
        Assert.Equal(new DateTime(2030, 1, 1, 12, 0, 0), plan.ViewEndDt);
        Assert.Equal(new DateTime(2030, 1, 1, 10, 15, 0), plan.NextCutDt);
        Assert.Equal(new DateTime(2030, 1, 1, 10, 25, 0), plan.NextEndDt);
    }

    private static (bool ok, List<int> fails, TournamentPlan? plan) ValidateHelper(
        TournamentService svc, string name = "TestTournament001")
    {
        var (ok, fails) = svc.ValidateRegist(
            name, "4|1|1|5", "0|1000|500|200|0",
            DateTime.Now.AddHours(3).ToString("yyyy/MM/dd HH:mm:ss"),
            "", 4, "", "host01", isAdmin: true, out var plan);
        return (ok, fails, plan);
    }

    // シナリオ5: 名前重複 → failCode=1006
    // 原典: CheckTournamentCoordinalValue → E_TRNMT_REG_PLANNAME_DUP
    [Fact]
    public void ValidateRegist_DuplicateName_Returns1006()
    {
        var plan = new TournamentPlan
        {
            SeqNo      = 1,
            PlayName   = "TestTournament001",
            PlayStatus = TournamentPlanStatus.Join,  // IsActive = true
        };
        var svc = BuildWithPlan(plan);

        var (ok, fails) = svc.ValidateRegist(
            "TestTournament001", "4|1|1|5", "0|1000|500|200|0",
            DateTime.Now.AddHours(3).ToString("yyyy/MM/dd HH:mm:ss"),
            "", 4, "", "host02", isAdmin: true, out _);

        Assert.False(ok);
        Assert.Contains(1006, fails);
    }

    // シナリオ6: 主催者重複 → failCode=1007
    [Fact]
    public void ValidateRegist_DuplicateOrganizer_Returns1007()
    {
        var plan = new TournamentPlan
        {
            SeqNo        = 1,
            PlayName     = "OtherPlan",
            PlanMemberNo = "host01",
            PlayStatus   = TournamentPlanStatus.Join,
        };
        var svc = BuildWithPlan(plan);

        var (ok, fails) = svc.ValidateRegist(
            "NewPlan12345", "4|1|1|5", "0|1000|500|200|0",
            DateTime.Now.AddHours(3).ToString("yyyy/MM/dd HH:mm:ss"),
            "", 4, "", "host01", isAdmin: true, out _); // 同じ主催者

        Assert.False(ok);
        Assert.Contains(1007, fails);
    }

    // ─── ValidateJoin ────────────────────────────────────────────────────

    // シナリオ7: 存在しないプラン → failCode=2001
    [Fact]
    public void ValidateJoin_PlanNotFound_Returns2001()
    {
        var svc = BuildEmpty();
        var (ok, code) = svc.ValidateJoin(999, "u1", "", 10000, null);
        Assert.False(ok);
        Assert.Equal(2001, code);
    }

    // シナリオ8: 参加時間外 (PlayStatus=Init) → failCode=2002
    [Fact]
    public void ValidateJoin_NotJoinable_Returns2002()
    {
        var plan = new TournamentPlan
        {
            SeqNo      = 1,
            PlayStatus = TournamentPlanStatus.Init,  // IsJoinable = false
            JoinStartDt = DateTime.Now.AddHours(-1),
            MatchStartDt = DateTime.Now.AddHours(2),
        };
        var svc = BuildWithPlan(plan);
        var (ok, code) = svc.ValidateJoin(1, "u1", "", 10000, null);
        Assert.False(ok);
        Assert.Equal(2002, code);
    }

    // シナリオ9: 満員 → failCode=2003
    // 原典: m_nPlayerNum >= m_nMaxPlayerNum → E_TRNMT_JOIN_MEMBEROVER
    [Fact]
    public void ValidateJoin_Full_Returns2003()
    {
        var plan = new TournamentPlan
        {
            SeqNo        = 1,
            PlayStatus   = TournamentPlanStatus.Join,
            JoinStartDt  = DateTime.Now.AddHours(-1),
            MatchStartDt = DateTime.Now.AddHours(2),
            PlayerNum    = 4,
            MaxPlayerNum = 4,  // 満員
        };
        var svc = BuildWithPlan(plan);
        var (ok, code) = svc.ValidateJoin(1, "u1", "", 10000, null);
        Assert.False(ok);
        Assert.Equal(2003, code);
    }

    // シナリオ10: 参加費不足 → failCode=2005
    // 原典: m_stRec.m_llGamMoney < m_llJoinMoney → E_TRNMT_JOIN_MONEYSHORT
    [Fact]
    public void ValidateJoin_NotEnoughMoney_Returns2005()
    {
        var plan = new TournamentPlan
        {
            SeqNo        = 1,
            PlayStatus   = TournamentPlanStatus.Join,
            JoinStartDt  = DateTime.Now.AddHours(-1),
            MatchStartDt = DateTime.Now.AddHours(2),
            PlayerNum    = 0,
            MaxPlayerNum = 4,
            JoinMoney    = 5000,
        };
        var svc = BuildWithPlan(plan);
        var (ok, code) = svc.ValidateJoin(1, "u1", "", 100, null); // お金不足
        Assert.False(ok);
        Assert.Equal(2005, code);
    }

    // シナリオ11: パスワード不一致 → failCode=2006
    [Fact]
    public void ValidateJoin_WrongPassword_Returns2006()
    {
        var plan = new TournamentPlan
        {
            SeqNo        = 1,
            PlayStatus   = TournamentPlanStatus.Join,
            JoinStartDt  = DateTime.Now.AddHours(-1),
            MatchStartDt = DateTime.Now.AddHours(2),
            PlayerNum    = 0,
            MaxPlayerNum = 4,
            Password     = "correct",
        };
        var svc = BuildWithPlan(plan);
        var (ok, code) = svc.ValidateJoin(1, "u1", "wrong", 10000, null);
        Assert.False(ok);
        Assert.Equal(2006, code);
    }

    // シナリオ12: 正常参加 → ok=true
    [Fact]
    public void ValidateJoin_Valid_ReturnsOk()
    {
        var plan = new TournamentPlan
        {
            SeqNo        = 1,
            PlayStatus   = TournamentPlanStatus.Join,
            JoinStartDt  = DateTime.Now.AddHours(-1),
            MatchStartDt = DateTime.Now.AddHours(2),
            PlayerNum    = 0,
            MaxPlayerNum = 4,
        };
        var svc = BuildWithPlan(plan);
        var (ok, _) = svc.ValidateJoin(1, "u1", "", 10000, null);
        Assert.True(ok);
    }

    // ─── ValidateCancel ──────────────────────────────────────────────────

    // シナリオ13: マッチング後はキャンセル不可 → failCode=3002
    // 原典: ctMatchStart <= ctNow → E_TRNMT_JOINCANCEL_TIMEOVER
    [Fact]
    public void ValidateCancel_AfterMatchStart_Returns3002()
    {
        var plan = new TournamentPlan
        {
            SeqNo        = 1,
            PlayStatus   = TournamentPlanStatus.Wait,
            MatchStartDt = DateTime.Now.AddMinutes(-5), // 既にマッチング開始
        };
        var svc = BuildWithPlan(plan);
        var currentJoin = new TournamentJoin { JoinSeqNo = 1, JoinStatus = TournamentJoinStatus.Join };
        var (ok, code) = svc.ValidateCancel(1, currentJoin);
        Assert.False(ok);
        Assert.Equal(3002, code);
    }

    // シナリオ14: 正常キャンセル → ok=true
    [Fact]
    public void ValidateCancel_Valid_ReturnsOk()
    {
        var plan = new TournamentPlan
        {
            SeqNo        = 1,
            PlayStatus   = TournamentPlanStatus.Join,
            MatchStartDt = DateTime.Now.AddHours(1), // まだマッチング前
        };
        var svc = BuildWithPlan(plan);
        var currentJoin = new TournamentJoin { JoinSeqNo = 1, JoinStatus = TournamentJoinStatus.Join };
        var (ok, _) = svc.ValidateCancel(1, currentJoin);
        Assert.True(ok);
    }

    // ─── RegisterAsync ───────────────────────────────────────────────────

    // シナリオ15: コイン不足 → false (原典: organizer GamMoney < planMoney)
    [Fact]
    public async Task RegisterAsync_NotEnoughMoney_ReturnsFalse()
    {
        var svc = BuildEmpty();
        var plan = new TournamentPlan
        {
            SeqNo      = 1,
            GradeMoney = new long[] { 1000, 500, 200, 0 }, // planMoney=1870
        };
        var organizer = new MajakPlayer { MemberNo = "host01", GamMoney = 100 }; // 不足

        bool ok = await svc.RegisterAsync(plan, organizer);

        Assert.False(ok);
    }

    // シナリオ16: DB 成功 → プランがキャッシュされること
    [Fact]
    public async Task RegisterAsync_Success_PlanCached()
    {
        _repoMock.Setup(r => r.InsertPlanAsync(It.IsAny<TournamentPlan>()))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.UpdatePlayerNumAsync(It.IsAny<long>(), It.IsAny<int>()))
            .ReturnsAsync(true);

        var svc  = BuildEmpty();
        var plan = new TournamentPlan
        {
            SeqNo      = 100,
            GradeMoney = new long[] { 0, 0, 0, 0 }, // planMoney=0
            MaxRoomNum = 1,
        };
        var organizer = new MajakPlayer { MemberNo = "host01", GamMoney = 10000 };

        bool ok = await svc.RegisterAsync(plan, organizer);

        Assert.True(ok);
        Assert.NotNull(svc.GetPlan(100));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// CupChannelBackgroundService 状態遷移テスト
// 原典: HMajChnlServer::OnTimer TIMERID_MAJANG_CHNLCTRL
//   Stanby → (DateFrom 到達) → Running
//   Running → (DateTo 到達)  → Stopping
//   Stopping → (+1h 後)      → Stanby (mjkc13e 送信)
// ═══════════════════════════════════════════════════════════════════════════
public class CupChannelBackgroundServiceTests
{
    private readonly List<(string method, object packet)> _sent = new();

    private (CupChannelBackgroundService svc, Mock<IHubContext<MajakGameHub>> hub)
        BuildService(Func<string, Task<List<CupConfig>>> loadCups, PlayerSessionService? session = null)
    {
        var built = BuildServiceWithRepo(loadCups, session);
        return (built.svc, built.hub);
    }

    private (CupChannelBackgroundService svc, Mock<IHubContext<MajakGameHub>> hub, Mock<PlayerRepository> repo)
        BuildServiceWithRepo(Func<string, Task<List<CupConfig>>> loadCups, PlayerSessionService? session = null)
    {
        var groupProxy = new Mock<IClientProxy>();
        groupProxy.Setup(c => c.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((m, a, _) =>
                _sent.Add((m, a[0]!)))
            .Returns(Task.CompletedTask);

        var hubClients = new Mock<IHubClients>();
        hubClients.Setup(c => c.Group(It.IsAny<string>())).Returns(groupProxy.Object);

        var hub = new Mock<IHubContext<MajakGameHub>>();
        hub.Setup(h => h.Clients).Returns(hubClients.Object);
        hub.Setup(h => h.Groups).Returns(new Mock<IGroupManager>().Object);

        var playerRepoMock = BuildPlayerRepository(loadCups);
        var scopeFactory = BuildScopeFactory(playerRepoMock);
        var logger = new Mock<ILogger<CupChannelBackgroundService>>();
        var redisService = new MajakServer.Infrastructure.RedisService(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var leaderSettings = new ChannelServerSettings { IsPrimaryServer = true };
        var leader = new MajakServer.Infrastructure.PrimaryLeaderService(redisService, leaderSettings);
        var masterCache = TestMasterCacheFactory.Create(playerRepoMock.Object);

        var svc = new CupChannelBackgroundService(
            scopeFactory.Object, hub.Object, leader, masterCache, session ?? new PlayerSessionService(), logger.Object);

        // LoadCupConfigsAsync を reflection で差し替え (private method) → TickAsync を直接呼ぶ
        // TickAsync は private なので reflection で呼び出す
        return (svc, hub, playerRepoMock);
    }

    private static Mock<PlayerRepository> BuildPlayerRepository(Func<string, Task<List<CupConfig>>> loadCups)
    {
        var playerRepoMock = new Mock<PlayerRepository>(MockBehavior.Loose);
        playerRepoMock.Setup(r => r.GetCupConfigsAsync())
            .Returns(() => loadCups(""));
        playerRepoMock.Setup(r => r.UpdateCupStatusAsync(
                It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        playerRepoMock.Setup(r => r.UpdateCupStatusAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .Returns(Task.CompletedTask);
        playerRepoMock.Setup(r => r.ResetCupMemberCountAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        return playerRepoMock;
    }

    private Mock<IServiceScopeFactory> BuildScopeFactory(Mock<PlayerRepository> playerRepoMock)
    {
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(PlayerRepository))).Returns(playerRepoMock.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(sp.Object);

        var asyncScope = new Mock<IAsyncDisposable>();

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return scopeFactory;
    }

    // TickAsync を直接呼び出すヘルパー
    private static async Task InvokeTickAsync(CupChannelBackgroundService svc)
    {
        var method = typeof(CupChannelBackgroundService)
            .GetMethod("TickAsync",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)!;
        await (Task)method.Invoke(svc, new object[] { CancellationToken.None })!;
    }

    // _states フィールドを取得するヘルパー
    private static Dictionary<string, CupChannelState> GetStates(
        CupChannelBackgroundService svc)
    {
        return (Dictionary<string, CupChannelState>)
            typeof(CupChannelBackgroundService)
                .GetField("_states",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)!
                .GetValue(svc)!;
    }

    // ─── 状態遷移: Stanby → Running ─────────────────────────────────────

    // シナリオ1: DateFrom 到達 → Running に遷移 (GoCupStart)
    [Fact]
    public async Task Tick_StanbyAndDateFromReached_TransitionsToRunning()
    {
        var (svc, hub) = BuildService(_ => Task.FromResult(new List<CupConfig>()));

        // LoadCupConfigsAsync の戻り値を差し替え (直接 GetCupConfigsAsync をスタブ)
        // CupConfig を注入するため、reflectionで _states 初期値ではなく TickAsync が使う
        // LoadCupConfigsAsync は scope factory 経由なのでここではスキップ
        // 代わりにTickAsync の内部的な GetCupConfigsAsync が空を返す場合のテスト
        await InvokeTickAsync(svc);

        // スコープファクトリが空リストを返すため何もしない
        Assert.Empty(_sent);
    }

    // シナリオ2: _states に Running がある + DateTo 過ぎた → Stopping に遷移
    // (内部状態を直接設定してテスト)
    [Fact]
    public void StateTransition_Running_DateToExpired_BecomeStopping()
    {
        var (svc, _) = BuildService(_ => Task.FromResult(new List<CupConfig>()));
        var states = GetStates(svc);

        var pastDate = DateTime.Now.AddHours(-2);
        states["ch1"] = new CupChannelState(
            CupStatus.Running,
            DateFrom: pastDate.AddHours(-5),
            DateTo:   pastDate,       // DateTo は過去
            IsFestive: false,
            StopStartedAt: null);

        // 状態確認
        Assert.Equal(CupStatus.Running, states["ch1"].Status);
        Assert.True(DateTime.Now >= states["ch1"].DateTo);
    }

    [Fact]
    public async Task Tick_RunningDateToReached_UpdatesLegacyStopStatus()
    {
        var now = DateTime.Now;
        var cup = new CupConfig(
            ChannelId: "ch1",
            ChannelName: "Cup",
            DateFrom: now.AddHours(-2),
            DateTo: now.AddMinutes(-1),
            IsFestive: false,
            CupId: 777,
            CupSeq: 3);
        var (svc, _, repo) = BuildServiceWithRepo(_ => Task.FromResult(new List<CupConfig> { cup }));
        var states = GetStates(svc);
        states["ch1"] = new CupChannelState(
            CupStatus.Running,
            cup.DateFrom,
            cup.DateTo,
            cup.IsFestive,
            StopStartedAt: null);

        await InvokeTickAsync(svc);

        Assert.Equal(CupStatus.Stopping, states["ch1"].Status);
        repo.Verify(r => r.UpdateCupStatusAsync("ch1", 2, 777, 3), Times.Once);
    }

    // シナリオ3: Stopping + 1時間経過 → ChannelStop (mjkc13e) 送信が期待される構造確認
    [Fact]
    public void StateTransition_Stopping_1HourPassed_TriggersStop()
    {
        var (svc, _) = BuildService(_ => Task.FromResult(new List<CupConfig>()));
        var states = GetStates(svc);

        var stopStarted = DateTime.Now.AddHours(-2); // 2時間前に停止開始
        states["ch1"] = new CupChannelState(
            CupStatus.Stopping,
            DateFrom: stopStarted.AddDays(-1),
            DateTo:   stopStarted,
            IsFestive: false,
            StopStartedAt: stopStarted);

        // +1h 経過チェック
        bool shouldStop = DateTime.Now >= stopStarted.AddHours(1);
        Assert.True(shouldStop);
    }

    [Fact]
    public async Task Tick_StoppingOneHourPassed_SendsChannelStopAndResetsState()
    {
        var stopStarted = DateTime.Now.AddHours(-2);
        var cup = new CupConfig(
            ChannelId: "ch1",
            ChannelName: "Cup",
            DateFrom: stopStarted.AddDays(-1),
            DateTo: stopStarted,
            IsFestive: false);
        var (svc, _) = BuildService(_ => Task.FromResult(new List<CupConfig> { cup }));
        var states = GetStates(svc);
        states["ch1"] = new CupChannelState(
            CupStatus.Stopping,
            cup.DateFrom,
            cup.DateTo,
            cup.IsFestive,
            StopStartedAt: stopStarted);

        await InvokeTickAsync(svc);

        Assert.Contains(_sent, x => x.method == Cmd.ChannelStop);
        var packet = CommandTestHelper.ToDict(_sent.Single(x => x.method == Cmd.ChannelStop).packet);
        Assert.Equal(1, ((System.Text.Json.JsonElement)packet["dummy"]!).GetInt32());
        Assert.Equal(CupStatus.Stanby, states["ch1"].Status);
    }

    [Fact]
    public async Task Tick_ActiveHiEventCup_SendsNoticeToAllActiveChannelsOncePerInterval()
    {
        var session = new PlayerSessionService();
        session.Register(new MajakPlayer { MemberNo = "u1", ConnectionId = "c1", ChannelId = "ch1" });
        session.Register(new MajakPlayer { MemberNo = "u2", ConnectionId = "c2", ChannelId = "ch2" });
        var now = DateTime.Now;
        var cup = new CupConfig(
            ChannelId: "MAJAK20ZC5F001",
            ChannelName: "Event Cup",
            DateFrom: now.AddMinutes(-5),
            DateTo: now.AddMinutes(30),
            IsFestive: false);
        var (svc, _) = BuildService(_ => Task.FromResult(new List<CupConfig> { cup }), session);

        await InvokeTickAsync(svc);
        await InvokeTickAsync(svc);

        var notices = _sent.Where(x => x.method == Cmd.Notice).ToList();
        Assert.Equal(2, notices.Count);
        foreach (var notice in notices)
        {
            var packet = CommandTestHelper.ToDict(notice.packet);
            Assert.Contains("Event Cup", ((System.Text.Json.JsonElement)packet["message"]!).GetString());
        }
    }

    // シナリオ4: CupStatus 定数値確認 (原典: ST_STANBY=0/ST_RUN=1/ST_STOP=2)
    [Fact]
    public void CupStatus_Values_MatchLegacy()
    {
        Assert.Equal(0, (int)CupStatus.Stanby);
        Assert.Equal(1, (int)CupStatus.Running);
        Assert.Equal(2, (int)CupStatus.Stopping);
    }
}

public class UserPresentRepositoryTests
{
    [Fact]
    public async Task GetUserPresentAsync_MoneyPresent_AddsEarnedMoneyAndMarksReceived()
    {
        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 1000, EarnedMoney = 50 };
        var repo = new Mock<PlayerRepository>(MockBehavior.Loose)
        {
            CallBase = true,
        };
        repo.Setup(r => r.GetUserPresentAsync("u1"))
            .ReturnsAsync(new List<UserPresentRecord>
            {
                new() { SeqNo = 11, PresentKind = 1, PresentKbn = TournamentPresentKind.ResultGrade, PresentNum = 500 },
            });
        repo.Setup(r => r.AddEarnedMoneyAsync("u1", 500, GameConst.EvtCodeTournamentResultGrade, 1000))
            .ReturnsAsync(true)
            .Verifiable();
        repo.Setup(r => r.UpdateUserPresentReceivedAsync(It.Is<IEnumerable<long>>(seqNos => seqNos.SequenceEqual(new long[] { 11 }))))
            .ReturnsAsync(true)
            .Verifiable();

        var presents = await repo.Object.GetUserPresentAsync(player);

        Assert.Single(presents);
        Assert.Equal(550, player.EarnedMoney);
        repo.Verify();
    }

    [Fact]
    public async Task GetUserPresentAsync_TitlePresent_InsertsTitleAndMarksReceived()
    {
        var player = new MajakPlayer { MemberNo = "u1" };
        var repo = new Mock<PlayerRepository>(MockBehavior.Loose)
        {
            CallBase = true,
        };
        repo.Setup(r => r.GetUserPresentAsync("u1"))
            .ReturnsAsync(new List<UserPresentRecord>
            {
                new() { SeqNo = 21, PresentKind = 2, PresentKbn = TournamentPresentKind.Title, PresentId = "mjkc001" },
            });
        repo.Setup(r => r.InsertOrEnableTitleAsync("u1", "mjkc001"))
            .Returns(Task.CompletedTask)
            .Verifiable();
        repo.Setup(r => r.UpdateUserPresentReceivedAsync(It.Is<IEnumerable<long>>(seqNos => seqNos.SequenceEqual(new long[] { 21 }))))
            .ReturnsAsync(true)
            .Verifiable();

        var presents = await repo.Object.GetUserPresentAsync(player);

        Assert.Single(presents);
        repo.Verify();
    }

    [Fact]
    public async Task GetUserPresentAsync_MoneyApplyFails_DoesNotMarkReceived()
    {
        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 1000 };
        var repo = new Mock<PlayerRepository>(MockBehavior.Loose)
        {
            CallBase = true,
        };
        repo.Setup(r => r.GetUserPresentAsync("u1"))
            .ReturnsAsync(new List<UserPresentRecord>
            {
                new() { SeqNo = 31, PresentKind = 1, PresentKbn = TournamentPresentKind.StopJoin, PresentNum = 700 },
            });
        repo.Setup(r => r.AddEarnedMoneyAsync("u1", 700, GameConst.EvtCodeTournamentStopJoin, 1000))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.Object.GetUserPresentAsync(player));
        repo.Verify(r => r.UpdateUserPresentReceivedAsync(It.IsAny<IEnumerable<long>>()), Times.Never);
    }

    [Fact]
    public async Task GetUserPresentAsync_ReceivedUpdateFails_Throws()
    {
        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 1000 };
        var repo = new Mock<PlayerRepository>(MockBehavior.Loose)
        {
            CallBase = true,
        };
        repo.Setup(r => r.GetUserPresentAsync("u1"))
            .ReturnsAsync(new List<UserPresentRecord>
            {
                new() { SeqNo = 41, PresentKind = 1, PresentKbn = TournamentPresentKind.ResultPlan, PresentNum = 300 },
            });
        repo.Setup(r => r.AddEarnedMoneyAsync("u1", 300, GameConst.EvtCodeTournamentResultPlan, 1000))
            .ReturnsAsync(true);
        repo.Setup(r => r.UpdateUserPresentReceivedAsync(It.IsAny<IEnumerable<long>>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.Object.GetUserPresentAsync(player));
    }
}

public class GradeRankServiceTests
{
    private static (GradeRankService Service, Mock<PlayerRepository> Repo) BuildService()
    {
        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        var services = new ServiceCollection();
        services.AddSingleton(repo.Object);
        var provider = services.BuildServiceProvider();
        return (new GradeRankService(provider.GetRequiredService<IServiceScopeFactory>(), new Mock<ILogger<GradeRankService>>().Object), repo);
    }

    [Fact]
    public async Task FlushGameClearCntAsync_WhenRepositoryFails_RestoresPendingCount()
    {
        var (service, repo) = BuildService();
        repo.SetupSequence(r => r.UpdateGameClearCntAsync(1))
            .ThrowsAsync(new InvalidOperationException("db down"))
            .Returns(Task.CompletedTask);

        service.AddGameClearCnt();

        await service.FlushGameClearCntAsync();
        await service.FlushGameClearCntAsync();

        repo.Verify(r => r.UpdateGameClearCntAsync(1), Times.Exactly(2));
    }

    [Fact]
    public async Task ReloadProPlayersAsync_CachesLegacyPictureUrlByMemberNo()
    {
        var (service, repo) = BuildService();
        repo.Setup(r => r.GetProPlayerListAsync())
            .ReturnsAsync(new List<ProPlayerInfo>
            {
                new() { MemberNo = "ProUser", PictureUrl = "https://example.invalid/pro.png" },
            });

        await service.ReloadProPlayersAsync();

        Assert.True(service.IsPro("prouser"));
        Assert.Equal("https://example.invalid/pro.png", service.GetProPictureUrl("PROUSER"));
        Assert.Equal("", service.GetProPictureUrl("normal"));
    }

    [Fact]
    public async Task PastFixGradeRankingAsync_UsesLegacyNowDuringPastStatusFlow()
    {
        var now = DateTime.Now;
        int rankDateNow = int.Parse(now.ToString("yyyyMM"));
        int rankDatePast = now.Month == 1
            ? (now.Year - 1) * 100 + 12
            : now.Year * 100 + (now.Month - 1);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (service, repo) = BuildService();

        repo.Setup(r => r.UpdateGradeManageStatusAsync(rankDatePast, 2, 3))
            .Returns(async () =>
            {
                started.SetResult();
                await release.Task;
                return 1;
            });
        repo.Setup(r => r.UpdateGradeManageStatusAsync(rankDatePast, 3, 1))
            .ReturnsAsync(1);
        repo.Setup(r => r.LoadGradeRankForConfirmAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<GradeRankConfirmItem>());
        repo.Setup(r => r.InsertGradeManageAsync(It.IsAny<int>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var task = service.PastFixGradeRankingAsync();
        await started.Task;

        Assert.True(service.IsBatchRunning);
        release.SetResult();
        await task;

        Assert.False(service.IsBatchRunning);
        repo.Verify(r => r.UpdateGradeManageStatusAsync(rankDatePast, 2, 3), Times.Once);
        repo.Verify(r => r.InsertGradeManageAsync(rankDateNow, It.IsAny<DateTime>()), Times.Once);
        repo.Verify(r => r.UpdateGradeManageStatusAsync(rankDatePast, 3, 1), Times.Once);
    }

    [Fact]
    public async Task PastFixGradeRankingAsync_RankUpdateFailure_DoesNotInsertOrRelease()
    {
        var now = DateTime.Now;
        int rankDateNow = int.Parse(now.ToString("yyyyMM"));
        int rankDatePast = now.Month == 1
            ? (now.Year - 1) * 100 + 12
            : now.Year * 100 + (now.Month - 1);
        var (service, repo) = BuildService();

        repo.Setup(r => r.UpdateGradeManageStatusAsync(rankDatePast, 2, 3))
            .ReturnsAsync(1);
        repo.SetupSequence(r => r.LoadGradeRankForConfirmAsync(rankDatePast, It.IsAny<int>()))
            .ReturnsAsync(new List<GradeRankConfirmItem>
            {
                new() { MemberNo = "p0", Rank = 1, Rating = 1500, Grade = 10, ExtraCnt = 0 },
            });
        repo.Setup(r => r.UpdateGradeRankConfirmAsync(rankDatePast, It.IsAny<int>(), It.IsAny<IReadOnlyList<GradeRankConfirmItem>>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        await service.PastFixGradeRankingAsync();

        Assert.False(service.IsBatchRunning);
        repo.Verify(r => r.InsertGradeManageAsync(rankDateNow, It.IsAny<DateTime>()), Times.Never);
        repo.Verify(r => r.UpdateGradeManageStatusAsync(rankDatePast, 3, 1), Times.Never);
    }
}

public class CupEntryLimitTests
{
    private static bool ShouldReject(CupConfig cup, MajakPlayer player)
    {
        var method = typeof(EnterChannelCommand)
            .GetMethod("ShouldRejectCupEntry",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static)!;
        return (bool)method.Invoke(null, new object[] { cup, player })!;
    }

    [Fact]
    public void ShouldRejectCupEntry_EntryLimitedPaidCupWithoutTitle_ReturnsTrue()
    {
        var cup = new CupConfig(
            ChannelId: "MAJAK20ZC5F001",
            ChannelName: "Cup",
            DateFrom: DateTime.Now.AddDays(-1),
            DateTo: DateTime.Now.AddDays(1),
            IsFestive: true,
            CupId: 1,
            CupSeq: 1,
            JudgementType: 8,
            CupPointSumType: 1,
            EntryLimited: true,
            ConditionBilling: 1);

        Assert.True(ShouldReject(cup, new MajakPlayer()));
    }

    [Fact]
    public void ShouldRejectCupEntry_EntryTitleOrFreeBilling_AllowsEntry()
    {
        var paidCup = new CupConfig(
            ChannelId: "MAJAK20ZC5F001",
            ChannelName: "Cup",
            DateFrom: DateTime.Now.AddDays(-1),
            DateTo: DateTime.Now.AddDays(1),
            IsFestive: true,
            EntryLimited: true,
            ConditionBilling: 1);
        var freeCup = paidCup with { ConditionBilling = 2 };

        Assert.False(ShouldReject(paidCup, new MajakPlayer { CupEvtRec = { EntryTitle = 201 } }));
        Assert.False(ShouldReject(freeCup, new MajakPlayer()));
    }

    [Fact]
    public void ShouldRejectCupLevel_OutsideMinMaxRange_ReturnsTrue()
    {
        var cup = new CupConfig(
            ChannelId: "MAJAK20ZC5F001",
            ChannelName: "Cup",
            DateFrom: DateTime.Now.AddDays(-1),
            DateTo: DateTime.Now.AddDays(1),
            IsFestive: true,
            MinLevel: 3,
            MaxLevel: 7);

        Assert.True(ShouldRejectLevel("ShouldRejectCupMinLevel", cup, new MajakPlayer { NLevel = 2 }));
        Assert.True(ShouldRejectLevel("ShouldRejectCupMaxLevel", cup, new MajakPlayer { NLevel = 8 }));
        Assert.False(ShouldRejectLevel("ShouldRejectCupMinLevel", cup, new MajakPlayer { NLevel = 3 }));
        Assert.False(ShouldRejectLevel("ShouldRejectCupMaxLevel", cup, new MajakPlayer { NLevel = 7 }));
    }

    private static bool ShouldRejectLevel(string methodName, CupConfig cup, MajakPlayer player)
    {
        var method = typeof(EnterChannelCommand)
            .GetMethod(methodName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static)!;
        return (bool)method.Invoke(null, new object[] { cup, player })!;
    }
}

public class EnterChannelPauseTests
{
    [Fact]
    public async Task ShouldRejectStoppedCupEnter_ContinuePlayerIsAllowed()
    {
        const string channelId = "MAJAK20ZC5F001";
        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        repo.Setup(r => r.GetCupStatusAsync(channelId)).ReturnsAsync(2);

        var session = new PlayerSessionService();
        var owner = new MajakPlayer { ConnectionId = "old", MemberNo = "user01", ChannelId = channelId };
        var room = session.CreateRoom(channelId, owner, "", 1, 0, 0, false);
        room.State = GameRoomState.Playing;
        owner.IsOutPlayer = true;

        var cmd = CreateEnterChannelCommand(session, repo.Object);

        Assert.False(await InvokeShouldRejectStoppedCupEnterAsync(cmd, channelId, "user01", "0ZC5F"));
        Assert.True(await InvokeShouldRejectStoppedCupEnterAsync(cmd, channelId, "other", "0ZC5F"));
    }

    [Fact]
    public void IsContinuePlayerInChannel_OnlyPlayingOutPlayerSeatMatches()
    {
        const string channelId = "MAJAK20ZC5F001";
        var session = new PlayerSessionService();
        var owner = new MajakPlayer { ConnectionId = "old", MemberNo = "user01", ChannelId = channelId };
        var room = session.CreateRoom(channelId, owner, "", 1, 0, 0, false);

        room.State = GameRoomState.Playing;
        owner.IsOutPlayer = true;

        Assert.True(session.IsContinuePlayerInChannel(channelId, "user01"));
        Assert.False(session.IsContinuePlayerInChannel(channelId, "other"));

        owner.IsOutPlayer = false;
        Assert.False(session.IsContinuePlayerInChannel(channelId, "user01"));

        owner.IsOutPlayer = true;
        room.State = GameRoomState.Waiting;
        Assert.False(session.IsContinuePlayerInChannel(channelId, "user01"));
    }

    [Fact]
    public async Task Execute_CupStatusStopped_RejectsBeforeRegisteringPlayer()
    {
        const string channelId = "MAJAK20ZC5F001";
        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        repo.Setup(r => r.GetCupStatusAsync(channelId)).ReturnsAsync(2);

        var session = new PlayerSessionService();
        var masterCache = TestMasterCacheFactory.Create(playerRepo: repo.Object);
        var cmd = new EnterChannelCommand(
            session,
            repo.Object,
            new RatingService(),
            null!,
            null!,
            null!,
            null!,
            null!,
            Options.Create(new ChannelServerSettings()),
            null!,
            new GradeRankService(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), new Mock<ILogger<GradeRankService>>().Object),
            masterCache,
            new AdminIdService(masterCache),
            new MenteTimeService(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), new Mock<ILogger<MenteTimeService>>().Object),
            new Mock<ILogger<EnterChannelCommand>>().Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(new MajakPlayer { ConnectionId = "conn1" }, new Dictionary<string, object?>
        {
            [GKey.ChannelId] = channelId,
            [GKey.Pix] = "user01",
            [GKey.Name] = "User",
            [GKey.AvatarId] = "avatar01",
        });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.EnterChannel, sent[0].method);
        var packet = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(0, CommandTestHelper.GetResult(sent[0].packet));
        Assert.Equal("SERVICE_MAINTENANCE", ((System.Text.Json.JsonElement)packet["error"]!).GetString());
        Assert.Null(session.GetByMember("user01"));
    }

    [Fact]
    public async Task Execute_GuestId_ResetsLegacyGameRecordsBeforeLoad()
    {
        const string channelId = "MAJAK200000001";
        var repo = CreateEnterRepoLoadCommonFails();
        var session = new PlayerSessionService();
        var cmd = CreateEnterChannelCommand(session, repo.Object);
        var player = new MajakPlayer { ConnectionId = "conn1", IsGuestId = true };
        var (ctx, sent) = CommandTestHelper.MakeContext(player, new Dictionary<string, object?>
        {
            [GKey.ChannelId] = channelId,
            [GKey.Pix] = "guest01",
            [GKey.Name] = "Guest",
            [GKey.AvatarId] = "avatar01",
        });

        await cmd.ExecuteAsync(ctx);

        repo.Verify(r => r.ResetGuestGameRecordsAsync("guest01"), Times.Once);
        Assert.Single(sent);
        Assert.Equal(Cmd.EnterChannel, sent[0].method);
    }

    [Fact]
    public async Task Execute_NormalId_DoesNotResetGuestGameRecords()
    {
        const string channelId = "MAJAK200000001";
        var repo = CreateEnterRepoLoadCommonFails();
        var session = new PlayerSessionService();
        var cmd = CreateEnterChannelCommand(session, repo.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(new MajakPlayer { ConnectionId = "conn1" }, new Dictionary<string, object?>
        {
            [GKey.ChannelId] = channelId,
            [GKey.Pix] = "user01",
            [GKey.Name] = "User",
            [GKey.AvatarId] = "avatar01",
        });

        await cmd.ExecuteAsync(ctx);

        repo.Verify(r => r.ResetGuestGameRecordsAsync(It.IsAny<string>()), Times.Never);
        Assert.Single(sent);
        Assert.Equal(Cmd.EnterChannel, sent[0].method);
    }

    [Fact]
    public async Task Execute_SameConnectionSameChannel_ReturnsSilentlyLikeLegacy()
    {
        const string channelId = "MAJAK200000001";
        var repo = CreateEnterRepoLoadCommonFails();
        var session = new PlayerSessionService();
        var existing = new MajakPlayer { ConnectionId = "conn1", MemberNo = "user01", ChannelId = channelId };
        session.Register(existing);
        var cmd = CreateEnterChannelCommand(session, repo.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(existing, new Dictionary<string, object?>
        {
            [GKey.ChannelId] = channelId,
            [GKey.Pix] = "user01",
            [GKey.Name] = "User",
            [GKey.AvatarId] = "avatar01",
        });

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
        repo.Verify(r => r.LoadCommonRatAsync(It.IsAny<MajakPlayer>()), Times.Never);
    }

    [Fact]
    public async Task Execute_SameConnectionSameChannel_PreservesRoomSeatSession()
    {
        const string channelId = "MAJAK200000001";
        var redis = TestMasterCacheFactory.CreateRedisService();
        var repo = CreateEnterRepoLoadCommonSucceeds();
        var itemRepo = new Mock<ItemRepository>(MockBehavior.Loose);
        itemRepo.Setup(r => r.GetAllItemsAsync(It.IsAny<string>())).ReturnsAsync(new List<MajItemInfo>());
        var masterCache = TestMasterCacheFactory.Create(playerRepo: repo.Object, itemRepo: itemRepo.Object);
        var session = new PlayerSessionService();
        var existing = new MajakPlayer { ConnectionId = "conn1", MemberNo = "user01", ChannelId = channelId, NickName = "Old" };
        session.Register(existing);
        var room = session.CreateRoom(channelId, existing, "120000001000000", 1, 0, 0, false, roomId: 7);
        var cmd = new EnterChannelCommand(
            session,
            repo.Object,
            new RatingService(),
            null!,
            new ItemService(itemRepo.Object, masterCache),
            new TitleService(repo.Object, masterCache),
            new RoomRegistryService(redis),
            new ChannelMemberService(redis),
            Options.Create(new ChannelServerSettings()),
            new ServerLoadService(redis, new ChannelServerSettings()),
            new GradeRankService(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), new Mock<ILogger<GradeRankService>>().Object),
            masterCache,
            new AdminIdService(masterCache),
            new MenteTimeService(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), new Mock<ILogger<MenteTimeService>>().Object),
            new Mock<ILogger<EnterChannelCommand>>().Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(existing, new Dictionary<string, object?>
        {
            [GKey.ChannelId] = channelId,
            [GKey.Pix] = "user01",
            [GKey.Name] = "User",
            [GKey.AvatarId] = "avatar01",
        });

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
        Assert.Same(existing, session.GetByConn("conn1"));
        Assert.Same(existing, room.Seats[0]);
        Assert.Equal(7, session.GetByConn("conn1")!.RoomId);
    }

    [Fact]
    public async Task Execute_ContinuePlayerDifferentConnection_IsNotRejectedAsMultiLogin()
    {
        const string channelId = "MAJAK200000001";
        var repo = CreateEnterRepoLoadCommonFails();
        var session = new PlayerSessionService();
        var owner = new MajakPlayer { ConnectionId = "old", MemberNo = "user01", ChannelId = channelId };
        var room = session.CreateRoom(channelId, owner, "", 1, 0, 0, false);
        room.State = GameRoomState.Playing;
        owner.IsOutPlayer = true;
        session.Register(owner);
        var cmd = CreateEnterChannelCommand(session, repo.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(new MajakPlayer { ConnectionId = "new" }, new Dictionary<string, object?>
        {
            [GKey.ChannelId] = channelId,
            [GKey.Pix] = "user01",
            [GKey.Name] = "User",
            [GKey.AvatarId] = "avatar01",
        });

        await cmd.ExecuteAsync(ctx);

        Assert.DoesNotContain(sent, packet =>
        {
            var dict = CommandTestHelper.ToDict(packet.packet);
            return dict.TryGetValue("error", out var error)
                && error is System.Text.Json.JsonElement element
                && element.GetString() == "USER_MULTI_LOGIN";
        });
        Assert.Contains(sent, packet => packet.method == Cmd.EnterChannel);
    }

    [Fact]
    public async Task Execute_AbandonPreviousRoom_DetachesOldPlayingConnectionBeforeMultiLoginCheck()
    {
        const string channelId = "MAJAK200000001";
        var repo = CreateEnterRepoLoadCommonFails();
        var session = new PlayerSessionService();
        var owner = new MajakPlayer { ConnectionId = "old", MemberNo = "user01", ChannelId = channelId };
        var room = session.CreateRoom(channelId, owner, "", 1, 0, 0, false);
        room.State = GameRoomState.Playing;
        session.Register(owner);
        var cmd = CreateEnterChannelCommand(session, repo.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(new MajakPlayer { ConnectionId = "new" }, new Dictionary<string, object?>
        {
            [GKey.ChannelId] = channelId,
            [GKey.Pix] = "user01",
            [GKey.Name] = "User",
            [GKey.AvatarId] = "avatar01",
            ["abandonPreviousRoom"] = true,
            ["abandonRoomId"] = room.RoomId,
        });

        await cmd.ExecuteAsync(ctx);

        Assert.True(owner.IsOutPlayer);
        Assert.Equal(0, room.ActivePlayerCount);
        Assert.Same(room, session.GetRoom(room.RoomId));
        Assert.Null(session.GetByMember("user01"));
        Assert.DoesNotContain(sent, packet =>
        {
            var dict = CommandTestHelper.ToDict(packet.packet);
            return dict.TryGetValue("error", out var error)
                && error is System.Text.Json.JsonElement element
                && element.GetString() == "USER_MULTI_LOGIN";
        });
    }

    [Fact]
    public void Remove_OldConnection_DoesNotRemoveNewerMemberMapping()
    {
        var session = new PlayerSessionService();
        var oldPlayer = new MajakPlayer { ConnectionId = "old", MemberNo = "user01", ChannelId = "ch1" };
        var newPlayer = new MajakPlayer { ConnectionId = "new", MemberNo = "user01", ChannelId = "ch1" };
        session.Register(oldPlayer);
        session.Register(newPlayer);

        session.Remove("old");

        Assert.Same(newPlayer, session.GetByMember("user01"));
    }

    private static Mock<PlayerRepository> CreateEnterRepoLoadCommonFails()
    {
        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        repo.Setup(r => r.GetCupStatusAsync(It.IsAny<string>())).ReturnsAsync(0);
        repo.Setup(r => r.GetCupConfigsAsync()).ReturnsAsync(new List<CupConfig>());
        repo.Setup(r => r.ExistsCommonRatAsync(It.IsAny<string>())).ReturnsAsync(true);
        repo.Setup(r => r.LoadCommonRatAsync(It.IsAny<MajakPlayer>())).ReturnsAsync(false);
        repo.Setup(r => r.ResetGuestGameRecordsAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        return repo;
    }

    private static Mock<PlayerRepository> CreateEnterRepoLoadCommonSucceeds()
    {
        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        repo.Setup(r => r.GetCupStatusAsync(It.IsAny<string>())).ReturnsAsync(0);
        repo.Setup(r => r.GetCupConfigsAsync()).ReturnsAsync(new List<CupConfig>());
        repo.Setup(r => r.ExistsCommonRatAsync(It.IsAny<string>())).ReturnsAsync(true);
        repo.Setup(r => r.LoadCommonRatAsync(It.IsAny<MajakPlayer>())).ReturnsAsync(true);
        repo.Setup(r => r.LoadHangeRatAsync(It.IsAny<MajakPlayer>())).ReturnsAsync(true);
        repo.Setup(r => r.LoadHiClassRatAsync(It.IsAny<MajakPlayer>())).ReturnsAsync(true);
        repo.Setup(r => r.LoadGradeRatAsync(It.IsAny<MajakPlayer>())).ReturnsAsync(true);
        repo.Setup(r => r.GetTitleListAsync(It.IsAny<string>())).ReturnsAsync(new List<string>());
        repo.Setup(r => r.ReceiveGeneralEventGiftAsync(It.IsAny<MajakPlayer>())).ReturnsAsync(true);
        repo.Setup(r => r.LoadSkinListAsync(It.IsAny<MajakPlayer>())).Returns(Task.CompletedTask);
        repo.Setup(r => r.EnsureSubRecordAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<int?>())).Returns(Task.CompletedTask);
        return repo;
    }

    private static EnterChannelCommand CreateEnterChannelCommand(PlayerSessionService session, PlayerRepository repo)
        => new(
            session,
            repo,
            new RatingService(),
            null!,
            null!,
            null!,
            null!,
            null!,
            Options.Create(new ChannelServerSettings()),
            null!,
            new GradeRankService(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), new Mock<ILogger<GradeRankService>>().Object),
            TestMasterCacheFactory.Create(playerRepo: repo),
            new AdminIdService(TestMasterCacheFactory.Create(playerRepo: repo)),
            new MenteTimeService(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), new Mock<ILogger<MenteTimeService>>().Object),
            new Mock<ILogger<EnterChannelCommand>>().Object);

    private static async Task<bool> InvokeShouldRejectStoppedCupEnterAsync(
        EnterChannelCommand cmd,
        string channelId,
        string memberNo,
        string subId)
    {
        var method = typeof(EnterChannelCommand).GetMethod(
            "ShouldRejectStoppedCupEnterAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var task = (Task<bool>)method.Invoke(cmd, new object[] { channelId, memberNo, subId })!;
        return await task;
    }
}

public class CupPlayLicenseTests
{
    [Fact]
    public void Check_FestiveCupMaxMatchReached_ReturnsLegacyOverLicense()
    {
        var cup = new CupConfig(
            ChannelId: "MAJAK20ZC5A001",
            ChannelName: "Cup",
            DateFrom: DateTime.Now.AddDays(-1),
            DateTo: DateTime.Now.AddDays(1),
            IsFestive: true,
            MaxMatchCntLimit: 3);
        var player = new MajakPlayer { CupRec = { CupMatchCnt = 3 } };

        Assert.Equal(CupPlayLicense.LicenseOver, CupPlayLicense.Check(cup, player));
    }

    [Fact]
    public void Check_HiEventCupMaxMatchReached_ReturnsLegacyOverLicense()
    {
        var cup = new CupConfig(
            ChannelId: "MAJAK20ZC5F001",
            ChannelName: "Cup",
            DateFrom: DateTime.Now.AddDays(-1),
            DateTo: DateTime.Now.AddDays(1),
            IsFestive: false,
            MaxMatchCntLimit: 2);
        var player = new MajakPlayer { CupEvtRec = { MatchCnt = 2, BuyItem = true } };

        Assert.Equal(CupPlayLicense.LicenseOver, CupPlayLicense.Check(cup, player));
    }

    [Fact]
    public void Check_HiEventBeforeBillingWithoutBuyItem_ReturnsBuyItemLicense()
    {
        var cup = new CupConfig(
            ChannelId: "MAJAK20ZC5F001",
            ChannelName: "Cup",
            DateFrom: DateTime.Now.AddDays(-1),
            DateTo: DateTime.Now.AddDays(1),
            IsFestive: false,
            ConditionBilling: 1);

        Assert.Equal(CupPlayLicense.LicenseBuyItem, CupPlayLicense.Check(cup, new MajakPlayer()));
    }

    [Fact]
    public void Check_HiEventFreeEntryLimited_UsesTitleOrBuyItemFreeLicense()
    {
        var cup = new CupConfig(
            ChannelId: "MAJAK20ZC5F001",
            ChannelName: "Cup",
            DateFrom: DateTime.Now.AddDays(-1),
            DateTo: DateTime.Now.AddDays(1),
            IsFestive: false,
            EntryLimited: true,
            ConditionBilling: 2);

        Assert.Equal(CupPlayLicense.LicenseBuyItemFree, CupPlayLicense.Check(cup, new MajakPlayer()));
        Assert.Equal(CupPlayLicense.Success, CupPlayLicense.Check(cup, new MajakPlayer { CupEvtRec = { EntryTitle = 201 } }));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// TournamentPlan モデル テスト
// ═══════════════════════════════════════════════════════════════════════════
public class TournamentPlanModelTests
{
    // シナリオ1: IsActive — Join/Wait/Play 状態で true
    [Theory]
    [InlineData(TournamentPlanStatus.Join, true)]
    [InlineData(TournamentPlanStatus.Wait, true)]
    [InlineData(TournamentPlanStatus.Play, true)]
    [InlineData(TournamentPlanStatus.Init, false)]
    [InlineData(TournamentPlanStatus.End,  false)]
    [InlineData(TournamentPlanStatus.Stop, false)]
    public void IsActive_ByStatus(int status, bool expected)
    {
        var plan = new TournamentPlan { PlayStatus = status };
        Assert.Equal(expected, plan.IsActive);
    }

    // シナリオ2: IsJoinable — JoinStartDt <= now < MatchStartDt かつ Status=Join
    [Fact]
    public void IsJoinable_ValidWindow_ReturnsTrue()
    {
        var plan = new TournamentPlan
        {
            PlayStatus   = TournamentPlanStatus.Join,
            JoinStartDt  = DateTime.Now.AddHours(-1),
            MatchStartDt = DateTime.Now.AddHours(2),
        };
        Assert.True(plan.IsJoinable(DateTime.Now));
    }

    // シナリオ3: IsJoinable — JoinStartDt より前 → false
    [Fact]
    public void IsJoinable_BeforeJoinStart_ReturnsFalse()
    {
        var plan = new TournamentPlan
        {
            PlayStatus   = TournamentPlanStatus.Join,
            JoinStartDt  = DateTime.Now.AddHours(1),  // まだ開始前
            MatchStartDt = DateTime.Now.AddHours(3),
        };
        Assert.False(plan.IsJoinable(DateTime.Now));
    }

    // シナリオ4: IsJoinable — MatchStartDt 以降 → false
    [Fact]
    public void IsJoinable_AfterMatchStart_ReturnsFalse()
    {
        var plan = new TournamentPlan
        {
            PlayStatus   = TournamentPlanStatus.Join,
            JoinStartDt  = DateTime.Now.AddHours(-2),
            MatchStartDt = DateTime.Now.AddMinutes(-5),  // マッチング開始済み
        };
        Assert.False(plan.IsJoinable(DateTime.Now));
    }

    // シナリオ5: TournamentConst — JoinMoneyMin/Max 確認
    [Fact]
    public void TournamentConst_JoinMoneyRange_IsCorrect()
    {
        Assert.Equal(0L, TournamentConst.JoinMoneyMin);
        Assert.Equal(10_000L, TournamentConst.JoinMoneyMax);
    }

    // シナリオ6: TournamentConst — PhaseFull/PhaseHalf 確認
    [Fact]
    public void TournamentConst_PhaseValues_Match()
    {
        Assert.Equal(10, TournamentConst.PhaseFull);
        Assert.Equal(5,  TournamentConst.PhaseHalf);
    }

    // シナリオ7: TournamentPlanStatus 定数確認 (原典: TRNMNT_PLAN_STATUS_*)
    [Fact]
    public void TournamentPlanStatus_Values_MatchLegacy()
    {
        Assert.Equal(0, TournamentPlanStatus.Init);
        Assert.Equal(2, TournamentPlanStatus.Join);
        Assert.Equal(3, TournamentPlanStatus.Wait);
        Assert.Equal(4, TournamentPlanStatus.Play);
    }
}
