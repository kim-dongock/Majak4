using System.Reflection;
using Moq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MajakServer.Engine;
using MajakServer.Hubs;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;

namespace MajakServer.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// GameLogicService.GetRoomChargeCommon テスト
// 原典: HMajChnlStrategy::GetRoomChargeCommon (HMajChnlInfo.cpp)
//   _MODIFY_BADAI: SubId が m_baDaiMap にあればその値、なければ 0
// ═══════════════════════════════════════════════════════════════════════════
public class GetRoomChargeCommonTests
{
    private static GameLogicService BuildService(PlayerSessionService? session = null)
    {
        session ??= new PlayerSessionService();
        var histMock   = new Mock<HistoryRepository>(MockBehavior.Loose);
        var logMock    = new Mock<LogRepository>(MockBehavior.Loose, (MySqlDbContext)null!);
        var playerMock = new Mock<PlayerRepository>(MockBehavior.Loose);
        var titleMock  = new Mock<TitleService>(MockBehavior.Loose, (PlayerRepository)null!, TestMasterCacheFactory.Create());
        var moneyMock  = new Mock<GameMoneyService>(MockBehavior.Loose,
            (PlayerRepository)null!, (RatingService)null!, (HistoryRepository?)null);
        return new GameLogicService(session, histMock.Object, logMock.Object,
            new RatingService(), playerMock.Object, moneyMock.Object, titleMock.Object, null!, null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
    }

    private static long InvokeGetRoomChargeCommon(GameLogicService svc, GameRoom room)
    {
        var method = typeof(GameLogicService)
            .GetMethod("GetRoomChargeCommon",
                BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (long)method.Invoke(svc, new object[] { room })!;
    }

    private static long InvokeGetStartLimitMoney(GameRoom room)
    {
        var method = typeof(GameLogicService)
            .GetMethod("GetStartLimitMoney",
                BindingFlags.NonPublic | BindingFlags.Static)!;
        return (long)method.Invoke(null, new object[] { room })!;
    }

    private static long InvokeGetFeeWinner(GameRoom room, long winMoney)
    {
        var method = typeof(GameLogicService)
            .GetMethod("GetFeeWinner",
                BindingFlags.NonPublic | BindingFlags.Static)!;
        return (long)method.Invoke(null, new object[] { room, winMoney })!;
    }

    // シナリオ1: 未登録 SubId → 0
    [Fact]
    public void GetRoomChargeCommon_Training_Returns0()
    {
        var svc  = BuildService();
        var room = new GameRoom { SubId = "00T5A", GameRate = 1 };
        Assert.Equal(0L, InvokeGetRoomChargeCommon(svc, room));
    }

    // シナリオ2: 登録済み段位 SubId → map 値 500
    [Fact]
    public void GetRoomChargeCommon_Grade_Returns500()
    {
        var svc  = BuildService();
        var room = new GameRoom { SubId = "0ZG6A", GameRate = 1 };
        Assert.Equal(500L, InvokeGetRoomChargeCommon(svc, room));
    }

    // シナリオ3: SubId[2]='V' (交流戦) → 0
    // 原典: case 'V': return 0
    [Fact]
    public void GetRoomChargeCommon_CrossPlay_Returns0()
    {
        var svc  = BuildService();
        var room = new GameRoom { SubId = "00V5A", GameRate = 1 };
        Assert.Equal(0L, InvokeGetRoomChargeCommon(svc, room));
    }

    // シナリオ4: SubId[2]='H' (トーナメント) → 0
    [Fact]
    public void GetRoomChargeCommon_Tournament_Returns0()
    {
        var svc  = BuildService();
        var room = new GameRoom { SubId = "00H5A", GameRate = 1 };
        Assert.Equal(0L, InvokeGetRoomChargeCommon(svc, room));
    }

    // シナリオ5: 未登録通常 SubId → 0
    [Fact]
    public void GetRoomChargeCommon_Normal_Returns0()
    {
        var svc  = BuildService();
        var room = new GameRoom { SubId = "00N5A", GameRate = 1 };
        Assert.Equal(0L, InvokeGetRoomChargeCommon(svc, room));
    }

    // シナリオ6: 未登録レート SubId → 0
    [Fact]
    public void GetRoomChargeCommon_Rated_Returns0()
    {
        var svc  = BuildService();
        var room = new GameRoom { SubId = "00R5A", GameRate = 1 };
        Assert.Equal(0L, InvokeGetRoomChargeCommon(svc, room));
    }

    // シナリオ7: _MODIFY_BADAI では GameRate 乗算なし
    [Fact]
    public void GetRoomChargeCommon_GameRate2_DoesNotMultiplyMapValue()
    {
        var svc  = BuildService();
        var room = new GameRoom { SubId = "0ZG6A", GameRate = 2 };
        Assert.Equal(500L, InvokeGetRoomChargeCommon(svc, room));
    }

    [Fact]
    public void GetStartLimitMoney_UsesUnitMoneyAndGameRate()
    {
        Assert.Equal(1L, InvokeGetStartLimitMoney(new GameRoom { UnitMoney = 9, GameRate = 100 }));
        Assert.Equal(700L, InvokeGetStartLimitMoney(new GameRoom { UnitMoney = 10, GameRate = 2 }));
    }

    [Fact]
    public void GetStartLimitMoney_HiEventCup_DoesNotApplyUnitMoneyStartLimit()
    {
        Assert.Equal(31500L, InvokeGetStartLimitMoney(new GameRoom { SubId = "00C5F", UnitMoney = 9, GameRate = 100 }));
    }

    [Fact]
    public void GetFeeWinner_GradeNormalTable_UsesLegacyTenPercentCeiling()
    {
        var room = new GameRoom { SubId = "0ZG6A" };
        Assert.Equal(124L, InvokeGetFeeWinner(room, 1234));
    }

    [Fact]
    public void GetFeeWinner_OtherTables_UsesLegacyTwoPercentCeiling()
    {
        var room = new GameRoom { SubId = "0ZG6B" };
        Assert.Equal(25L, InvokeGetFeeWinner(room, 1234));
    }

    // シナリオ8: 空 SubId → 0
    [Fact]
    public void GetRoomChargeCommon_EmptySubId_Returns0()
    {
        var svc  = BuildService();
        var room = new GameRoom { SubId = "", GameRate = 1 };
        Assert.Equal(0L, InvokeGetRoomChargeCommon(svc, room));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// GameLogicService.CalcMoney テスト
// 原典: HMajRoomServer::CalcMoney_GambleType
//   MoneyChange -= RoomCharge (場代控除)
//   コイン不足時は場代を減額
// ═══════════════════════════════════════════════════════════════════════════
public class CalcMoneyTests
{
    private static GameLogicService BuildService(PlayerSessionService session)
    {
        var histMock   = new Mock<HistoryRepository>(MockBehavior.Loose);
        var logMock    = new Mock<LogRepository>(MockBehavior.Loose, (MySqlDbContext)null!);
        var playerMock = new Mock<PlayerRepository>(MockBehavior.Loose);
        var titleMock  = new Mock<TitleService>(MockBehavior.Loose, (PlayerRepository)null!, TestMasterCacheFactory.Create());
        var moneyMock  = new Mock<GameMoneyService>(MockBehavior.Loose,
            (PlayerRepository)null!, (RatingService)null!, (HistoryRepository?)null);
        return new GameLogicService(session, histMock.Object, logMock.Object,
            new RatingService(), playerMock.Object, moneyMock.Object, titleMock.Object, null!, null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
    }

    private static void InvokeCalcMoney(GameLogicService svc, GameRoom room, GameReport report)
    {
        typeof(GameLogicService)
            .GetMethod("CalcMoney", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(svc, new object[] { room, report });
    }

    private static bool InvokeValidateMoneyReport(GameLogicService svc, GameReport report, GameRoom room)
        => (bool)typeof(GameLogicService)
            .GetMethod("ValidateMoneyReport", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(svc, new object[] { report, room })!;

    // シナリオ1: 場代控除 → MoneyChange が減る
    // 原典: u.MoneyChange -= charge
    [Fact]
    public void CalcMoney_DeductsRoomCharge()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", GamMoney = 10000 };
        session.Register(player);

        var svc  = BuildService(session);
        var room = new GameRoom { SubId = "0ZG6A", GameRate = 1 }; // map=500場代
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult { MemberNo = "u1", MoneyChange = 1000 };

        InvokeCalcMoney(svc, room, report);

        Assert.Equal(1000 - 500, report.Users[0]!.MoneyChange);
    Assert.Equal(10500L, report.Users[0]!.CurrMoney);
        Assert.Equal(500L, report.Users[0]!.DealerFee);
    }

    // シナリオ2: 練習チャンネル (場代=100) → MoneyChange から 100 控除
    [Fact]
    public void CalcMoney_Training_100Charge()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", GamMoney = 10000 };
        session.Register(player);

        var svc  = BuildService(session);
        var room = new GameRoom { SubId = "00T5A", GameRate = 1 }; // 未登録=0場代
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult { MemberNo = "u1", MoneyChange = 500 };

        InvokeCalcMoney(svc, room, report);

        Assert.Equal(500L, report.Users[0]!.MoneyChange);
    }

    // シナリオ3: 負の生スコアは直接コイン減算せず、場代のみ制限控除
    // 原典: llFinalMoneyChange は正の MoneyChange のみ反映し、そこから roomCharge を引く
    [Fact]
    public void CalcMoney_NegativeRawMoneyChange_ChargesOnlyRoomFee()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", GamMoney = 50 };
        session.Register(player);

        var svc  = BuildService(session);
        var room = new GameRoom { SubId = "0ZG6A", GameRate = 1 }; // map=500場代
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult { MemberNo = "u1", MoneyChange = -300 };

        InvokeCalcMoney(svc, room, report);

        Assert.Equal(50L, report.Users[0]!.DealerFee);
        Assert.Equal(50L, player.RoomCharge);
        Assert.Equal(-50L, report.Users[0]!.MoneyChange);
        Assert.Equal(0L, report.Users[0]!.CurrMoney);
    }

    [Fact]
    public void CalcMoney_PositiveRawMoneyChange_AppliesMoneyChangeRatio()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", GamMoney = 10000 };
        session.Register(player);

        var svc  = BuildService(session);
        var room = new GameRoom { SubId = "0ZG6A", GameRate = 1 };
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult
        {
            MemberNo = "u1",
            MoneyChange = 1000,
            MoneyChangeRatio = 2,
        };

        InvokeCalcMoney(svc, room, report);

        Assert.Equal(1500L, report.Users[0]!.MoneyChange);
        Assert.Equal(500L, report.Users[0]!.DealerFee);
        Assert.Equal(11500L, report.Users[0]!.CurrMoney);
    }

    [Fact]
    public void CalcMoney_ChargeFreeItem_ExemptsRoomCharge()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", GamMoney = 10000 };
        player.MajItems.Add(new MajItemInfo
        {
            ItemCode = "MJ20",
            Qty = 1,
            UseFlag = true,
            EndDt = DateTime.Now.AddDays(1),
        });
        session.Register(player);

        var svc  = BuildService(session);
        var room = new GameRoom { SubId = "0ZG6A", GameRate = 1 };
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult { MemberNo = "u1", MoneyChange = 1000 };

        InvokeCalcMoney(svc, room, report);

        Assert.Equal(1000L, report.Users[0]!.MoneyChange);
        Assert.Equal(0L, report.Users[0]!.DealerFee);
        Assert.Equal(0L, player.RoomCharge);
        Assert.Equal("MJ20", player.UsedBadaiFreeItem);
    }

    [Fact]
    public void ValidateMoneyReport_UsesLegacyUnitMoneyNotMoneyRate()
    {
        var svc = BuildService(new PlayerSessionService());
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult { MemberNo = "u1", MoneyChange = 15 };

        Assert.False(InvokeValidateMoneyReport(svc, report, new GameRoom { UnitMoney = 20, MoneyRate = 1 }));
        Assert.True(InvokeValidateMoneyReport(svc, report, new GameRoom { UnitMoney = 5, MoneyRate = 1 }));
    }

    [Fact]
    public void ValidateMoneyReport_UnitMoneyZeroRejectsAnyMoneyChange()
    {
        var svc = BuildService(new PlayerSessionService());
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult { MemberNo = "u1", MoneyChange = 1 };

        Assert.False(InvokeValidateMoneyReport(svc, report, new GameRoom { UnitMoney = 0, MoneyRate = 1 }));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// GameLogicService.CalcExperience テスト
// 原典: HMajRoomServer::CalcExperience_MajakType
// ═══════════════════════════════════════════════════════════════════════════
public class CalcExperienceTests
{
    private static GameLogicService BuildService(PlayerSessionService session)
    {
        var histMock   = new Mock<HistoryRepository>(MockBehavior.Loose);
        var logMock    = new Mock<LogRepository>(MockBehavior.Loose, (MySqlDbContext)null!);
        var playerMock = new Mock<PlayerRepository>(MockBehavior.Loose);
        var titleMock  = new Mock<TitleService>(MockBehavior.Loose, (PlayerRepository)null!, TestMasterCacheFactory.Create());
        var moneyMock  = new Mock<GameMoneyService>(MockBehavior.Loose,
            (PlayerRepository)null!, (RatingService)null!, (HistoryRepository?)null);
        return new GameLogicService(session, histMock.Object, logMock.Object,
            new RatingService(), playerMock.Object, moneyMock.Object, titleMock.Object, null!, null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
    }

    private static void InvokeCalcExperience(GameLogicService svc, GameReport report)
    {
        typeof(GameLogicService)
            .GetMethod("CalcExperience", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(svc, new object[] { report });
    }

    // シナリオ1: 経験値が計算される (プレイヤーがセッションにいる場合)
    [Fact]
    public void CalcExperience_WithPlayer_SetsExperienceGain()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", Experience = 100 };
        session.Register(player);

        var svc    = BuildService(session);
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult
        {
            MemberNo   = "u1",
            HoraPoint  = 5000, // 和了点
            HojuPoint  = 0,
        };

        InvokeCalcExperience(svc, report);

        Assert.Equal(150, report.Users[0]!.ExperienceGain);
        Assert.Equal(250, report.Users[0]!.Experience); // 基礎経験値に加算
    }

    // シナリオ2: 経験値 = 少なくとも1
    // 原典: (nHoraSoten * 3 + nHojuSoten) / 100
    [Fact]
    public void CalcExperience_HojuOnly_UsesLegacyFormula()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", Experience = 0 };
        session.Register(player);

        var svc    = BuildService(session);
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult
        {
            MemberNo  = "u1",
            HoraPoint = 0,
            HojuPoint = 10000, // 放銃点のみ
        };

        InvokeCalcExperience(svc, report);

        Assert.Equal(100, report.Users[0]!.ExperienceGain);
        Assert.Equal(100, report.Users[0]!.Experience);
    }

    [Fact]
    public void CalcExperience_ZeroScore_ReturnsZero()
    {
        var session = new PlayerSessionService();
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", Experience = 0 };
        session.Register(player);

        var svc = BuildService(session);
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult
        {
            MemberNo = "u1",
            HoraPoint = 0,
            HojuPoint = 0,
        };

        InvokeCalcExperience(svc, report);

        Assert.Equal(0, report.Users[0]!.ExperienceGain);
        Assert.Equal(0, report.Users[0]!.Experience);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// GameLogicService.CalcGradeModeLeveUp テスト
// 原典: HMajRoomServer::CalcGradeModeLeveUp
// ═══════════════════════════════════════════════════════════════════════════
public class CalcGradeModeLeveUpTests
{
    private static void InvokeCalcGradeModeLeveUp(GameReport report, GameRoom room)
    {
        typeof(GameLogicService)
            .GetMethod("CalcGradeModeLeveUp", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { report, room });
    }

    [Fact]
    public void CalcGradeModeLeveUp_Promotion_SetsNextPointZero()
    {
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult
        {
            MemberNo = "u1",
            PrevGradeLevel = 9,
            PrevGradePoint = 80,
            Ranking = 1,
        };

        InvokeCalcGradeModeLeveUp(report, new GameRoom { SubId = "0007A" });

        Assert.Equal(10, report.Users[0]!.GradeLevel);
        Assert.Equal(0, report.Users[0]!.GradePoint);
        Assert.Equal(110, report.Users[0]!.GradePointTmp);
        Assert.Equal(30, report.Users[0]!.GradeAddPoint);
        Assert.Equal(0, report.Users[0]!.GradeNextPoint);
        Assert.Equal(1, report.Users[0]!.GradeUpDown);
        Assert.True(report.Users[0]!.UpdateBeginner);
    }

    [Fact]
    public void CalcGradeModeLeveUp_Demotion_SetsCurrentLevelMaxAsNextPoint()
    {
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult
        {
            MemberNo = "u1",
            PrevGradeLevel = 13,
            PrevGradePoint = 20,
            Ranking = 4,
        };

        InvokeCalcGradeModeLeveUp(report, new GameRoom { SubId = "0007B" });

        Assert.Equal(12, report.Users[0]!.GradeLevel);
        Assert.Equal(600, report.Users[0]!.GradePoint);
        Assert.Equal(0, report.Users[0]!.GradePointTmp);
        Assert.Equal(-66, report.Users[0]!.GradeAddPoint);
        Assert.Equal(2400, report.Users[0]!.GradeNextPoint);
        Assert.Equal(2, report.Users[0]!.GradeUpDown);
    }

    [Fact]
    public void CalcGradeModeLeveUp_TenDanAchievement_SetsUpdateExtra()
    {
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult
        {
            MemberNo = "u1",
            PrevGradeLevel = 18,
            PrevGradePoint = 4750,
            Ranking = 1,
        };

        InvokeCalcGradeModeLeveUp(report, new GameRoom { SubId = "0007B" });

        Assert.Equal(18, report.Users[0]!.GradeLevel);
        Assert.Equal(2400, report.Users[0]!.GradePoint);
        Assert.Equal(4810, report.Users[0]!.GradePointTmp);
        Assert.Equal(60, report.Users[0]!.GradeAddPoint);
        Assert.Equal(0, report.Users[0]!.GradeNextPoint);
        Assert.Equal(1, report.Users[0]!.GradeUpDown);
        Assert.False(report.Users[0]!.UpdateBeginner);
        Assert.True(report.Users[0]!.UpdateExtra);
    }

    [Theory]
    [InlineData("0007A", 11, 3, -11)]
    [InlineData("0007A", 12, 4, -36)]
    [InlineData("0006A", 11, 4, -16)]
    [InlineData("0007C", 13, 3, -27)]
    [InlineData("0006C", 13, 4, -40)]
    [InlineData("0007D", 16, 4, -120)]
    [InlineData("0006D", 16, 4, -60)]
    [InlineData("0007A", 13, 1, 0)]
    [InlineData("0007B", 9, 1, 0)]
    [InlineData("0007C", 12, 1, 0)]
    [InlineData("0007D", 15, 1, 0)]
    public void CalcGradeModeLeveUp_UsesOfficialPointTable(
        string subId, int gradeLevel, int ranking, int expectedPoint)
    {
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult
        {
            MemberNo = "u1",
            PrevGradeLevel = gradeLevel,
            PrevGradePoint = 3_000,
            Ranking = ranking,
        };

        InvokeCalcGradeModeLeveUp(report, new GameRoom { SubId = subId });

        Assert.Equal(expectedPoint, report.Users[0]!.GradeAddPoint);
    }
}

public class GameResultPayloadTests
{
    private static GameLogicService BuildService(PlayerSessionService session)
    {
        var histMock   = new Mock<HistoryRepository>(MockBehavior.Loose);
        var logMock    = new Mock<LogRepository>(MockBehavior.Loose, (MySqlDbContext)null!);
        var playerMock = new Mock<PlayerRepository>(MockBehavior.Loose);
        var titleMock  = new Mock<TitleService>(MockBehavior.Loose, (PlayerRepository)null!, TestMasterCacheFactory.Create());
        var moneyMock  = new Mock<GameMoneyService>(MockBehavior.Loose,
            (PlayerRepository)null!, (RatingService)null!, (HistoryRepository?)null);
        return new GameLogicService(session, histMock.Object, logMock.Object,
            new RatingService(), playerMock.Object, moneyMock.Object, titleMock.Object, null!, null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
    }

    private static Dictionary<string, object?> InvokeBuildGameResultPayload(GameLogicService svc, GameRoom room, GameReport report)
    {
        return (Dictionary<string, object?>)typeof(GameLogicService)
            .GetMethod("BuildGameResultPayload", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(svc, new object[] { room, report })!;
    }

    [Fact]
    public void BuildGameResultPayload_GradeCurrPoint_UsesLegacyDisplayPoint()
    {
        var session = new PlayerSessionService();
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", GamMoney = 1000 };
        player.ActiveRecord.Rating = 1510;
        session.Register(player);

        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult
        {
            MemberNo = "u1",
            GradePoint = 0,
            GradePointTmp = 110,
            GradeNextPoint = 0,
            GradeLevel = 10,
            PrevGradeLevel = 9,
            UpdateExtra = true,
        };

        var payload = InvokeBuildGameResultPayload(
            BuildService(session),
            new GameRoom { RoomId = 1, ChannelId = "1", SubId = "00G7A" },
            report);

        Assert.Equal(110, payload[$"{Key.GradeCurrPoint}0"]);
        Assert.Equal(1, payload[$"{Key.GradeExtraStage}0"]);
        Assert.Equal(GKey.ValueReportingGamble, payload[GKey.ReportingType]);
    }
}

public class ResultRecordUpdateTests
{
    private static GameLogicService BuildService(PlayerSessionService session, PlayerRepository playerRepo)
    {
        var histMock   = new Mock<HistoryRepository>(MockBehavior.Loose);
        var logMock    = new Mock<LogRepository>(MockBehavior.Loose, (MySqlDbContext)null!);
        var titleMock  = new Mock<TitleService>(MockBehavior.Loose, (PlayerRepository)null!, TestMasterCacheFactory.Create());
        var moneyMock  = new Mock<GameMoneyService>(MockBehavior.Loose,
            (PlayerRepository)null!, (RatingService)null!, (HistoryRepository?)null);
        return new GameLogicService(session, histMock.Object, logMock.Object,
            new RatingService(), playerRepo, moneyMock.Object, titleMock.Object, null!, null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
    }

    private static MajakPlayer InvokeBuildResultUpdatePlayer(MajakPlayer player, GameReport.UserResult user, GameRoom room)
    {
        return (MajakPlayer)typeof(GameLogicService)
            .GetMethod("BuildResultUpdatePlayer", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { player, user, room })!;
    }

    private static (string MemberNo, int Point)[] InvokeBuildTrainingHistoryPlayers(GameReport report)
    {
        return ((IEnumerable<(string MemberNo, int Point)>)typeof(GameLogicService)
            .GetMethod("BuildTrainingHistoryPlayers", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { report })!).ToArray();
    }

    private static string? InvokeBuildGradeTitleId(GameReport.UserResult user)
    {
        return (string?)typeof(GameLogicService)
            .GetMethod("BuildGradeTitleId", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { user });
    }

    private static IReadOnlyList<GradeRankUpdateItem> InvokeBuildGradeRankUpdates(MajakPlayer player, GameReport.UserResult user)
    {
        return (IReadOnlyList<GradeRankUpdateItem>)typeof(GameLogicService)
            .GetMethod("BuildGradeRankUpdates", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { player, user })!;
    }

    private static async Task InvokeAwardGradeBeginnerMoneyAsync(GameLogicService svc, MajakPlayer player, GameReport.UserResult user)
    {
        await (Task)typeof(GameLogicService)
            .GetMethod("AwardGradeBeginnerMoneyAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(svc, new object[] { player, user })!;
    }

    private static void InvokeUpdateHiClassStreaks(MajakPlayer player, GameReport.UserResult user)
    {
        typeof(GameLogicService)
            .GetMethod("UpdateHiClassStreaks", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { player, user });
    }

    [Fact]
    public void BuildResultUpdatePlayer_GradeMode_CarriesDeltaStatsAndFinalGradeValues()
    {
        var player = new MajakPlayer { MemberNo = "u1", ChannelId = "old" };
        player.GradeRecord.Rating = 1500;
        player.GradeRecord.MatchCnt = 30;
        player.GradeRecord.HoraCnt = 40;
        player.ActiveRecord = player.GradeRecord;

        var user = new GameReport.UserResult
        {
            MemberNo = "u1",
            Ranking = 1,
            Rating = 1510,
            MatchCnt = 1,
            WinCnt = 1,
            TurnCnt = 12,
            PointSum = 340,
            HoraCnt = 2,
            HojuCnt = 1,
            DoraCnt = 3,
            TipPoint = 6,
            TipMatchCnt = 1,
            GradeLevel = 10,
            GradePoint = 0,
            UpdateExtra = true,
        };

        var update = InvokeBuildResultUpdatePlayer(
            player,
            user,
            new GameRoom { ChannelId = "grade-ch", SubId = "00G7A" });

        Assert.Equal("grade-ch", update.ChannelId);
        Assert.Same(update.GradeRecord, update.ActiveRecord);
        Assert.Equal(1510, update.GradeRecord.Rating);
        Assert.Equal(1, update.GradeRecord.MatchCnt);
        Assert.Equal(1, update.GradeRecord.WinCnt);
        Assert.Equal(1, update.GradeRecord.Grade1);
        Assert.Equal(12, update.GradeRecord.TurnCnt);
        Assert.Equal(340, update.GradeRecord.PointSum);
        Assert.Equal(2, update.GradeRecord.HoraCnt);
        Assert.Equal(1, update.GradeRecord.HojuCnt);
        Assert.Equal(3, update.GradeRecord.DoraCnt);
        Assert.Equal(6, update.GradeRecord.TipPoint);
        Assert.Equal(1, update.GradeRecord.TipMatchCnt);
        Assert.Equal(10, update.GradeRecord.Grade);
        Assert.Equal(0, update.GradeRecord.GradePoint);
        Assert.Equal(1, update.GradeRecord.TotExtraCount);
    }

    [Theory]
    [InlineData("00N7A", "hi")]
    [InlineData("00R7A", "compete")]
    [InlineData("00X7B", "hi")]
    public void BuildResultUpdatePlayer_NonGrade_CarriesRatingDeltaToChannelRecord(string subId, string recordKind)
    {
        var player = new MajakPlayer { MemberNo = "u1" };
        player.RegularRecord.Rating = 1500;
        player.CompeteRecord.Rating = 1500;
        player.HiClassRecord.Rating = 1500;
        player.ActiveRecord = recordKind switch
        {
            "compete" => player.CompeteRecord,
            "hi" => player.HiClassRecord,
            _ => player.RegularRecord,
        };

        var user = new GameReport.UserResult
        {
            MemberNo = "u1",
            Ranking = 4,
            Rating = 1485,
            RatingChange = -15,
            MatchCnt = 1,
            DefeatCnt = 1,
            PointSum = -120,
            KyokuCnt = 8,
            TipPoint = -4,
            TipMatchCnt = 1,
        };

        var update = InvokeBuildResultUpdatePlayer(
            player,
            user,
            new GameRoom { ChannelId = "ch", SubId = subId });

        var record = recordKind switch
        {
            "compete" => update.CompeteRecord,
            "hi" => update.HiClassRecord,
            _ => update.RegularRecord,
        };

        Assert.Same(record, update.ActiveRecord);
        Assert.Equal(-15, record.Rating);
        Assert.Equal(1, record.MatchCnt);
        Assert.Equal(1, record.DefeatCnt);
        Assert.Equal(1, record.Grade4);
        Assert.Equal(-120, record.PointSum);
        Assert.Equal(8, record.KyokuCnt);
        Assert.Equal(-4, record.TipPoint);
        Assert.Equal(1, record.TipMatchCnt);
    }

    [Fact]
    public void BuildTrainingHistoryPlayers_OrdersByRankingAndUsesSetTotalPoint()
    {
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult
        {
            MemberNo = "third",
            Ranking = 3,
            SetPoint = -10,
            SetUma = -20,
        };
        report.Users[1] = new GameReport.UserResult
        {
            MemberNo = "first",
            Ranking = 1,
            SetPoint = 100,
            SetUma = 20,
            SetTor = 5,
            SetTip = 2,
        };

        var players = InvokeBuildTrainingHistoryPlayers(report);

        Assert.Equal("first", players[0].MemberNo);
        Assert.Equal(127, players[0].Point);
        Assert.Equal("third", players[1].MemberNo);
        Assert.Equal(-30, players[1].Point);
    }

    [Fact]
    public void BuildGradeTitleId_GradeChangeAndExtraStage_UseLegacyTitleNumbers()
    {
        Assert.Equal("mjkt510", InvokeBuildGradeTitleId(new GameReport.UserResult
        {
            GradeLevel = 10,
            GradeUpDown = 1,
        }));
        Assert.Equal("mjkt519", InvokeBuildGradeTitleId(new GameReport.UserResult
        {
            GradeLevel = 18,
            GradeUpDown = 1,
            UpdateExtra = true,
        }));
        Assert.Null(InvokeBuildGradeTitleId(new GameReport.UserResult
        {
            GradeLevel = 10,
            GradeUpDown = 0,
        }));
    }

    [Fact]
    public void BuildGradeRankUpdates_CreatesAllGradeAndExtraRows()
    {
        var player = new MajakPlayer
        {
            MemberNo = "u1",
            AvatarId = "av1",
            DispRange = 7,
        };
        var user = new GameReport.UserResult
        {
            Rating = 1800,
            GradeLevel = 18,
            UpdateExtra = true,
        };

        var rows = InvokeBuildGradeRankUpdates(player, user);

        Assert.Equal(3, rows.Count);
        Assert.Contains(rows, row => row.RankKind == GameConst.RatingRankAll && row.ExtraCount == 0);
        Assert.Contains(rows, row => row.RankKind == 18 && row.ExtraCount == 0);
        Assert.Contains(rows, row => row.RankKind == 19 && row.ExtraCount == 1);
        Assert.All(rows, row =>
        {
            Assert.Equal("u1", row.MemberNo);
            Assert.Equal("av1", row.AvatarId);
            Assert.Equal(7, row.DispFlag);
            Assert.Equal(1800, row.Rating);
            Assert.Equal(18, row.Grade);
        });
    }

    [Fact]
    public async Task AwardGradeBeginnerMoney_AddsEarnedMoneyWithLegacyEventCode()
    {
        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        repo.Setup(r => r.AddEarnedMoneyAsync("u1", 5000, GameConst.EvtCodeGradeBeginnerPresent, 1000))
            .ReturnsAsync(true);
        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 1000, EarnedMoney = 200 };
        var svc = BuildService(new PlayerSessionService(), repo.Object);

        await InvokeAwardGradeBeginnerMoneyAsync(svc, player, new GameReport.UserResult { UpdateBeginner = true });

        Assert.Equal(5200, player.EarnedMoney);
        repo.Verify(r => r.AddEarnedMoneyAsync("u1", 5000, GameConst.EvtCodeGradeBeginnerPresent, 1000), Times.Once);
    }

    [Fact]
    public void UpdateHiClassStreaks_UpdatesConsecutiveTopMax()
    {
        var player = new MajakPlayer { H_ContTopNow = 6, H_ContTopMax = 6 };

        InvokeUpdateHiClassStreaks(player, new GameReport.UserResult { Ranking = 1 });

        Assert.Equal(7, player.H_ContTopNow);
        Assert.Equal(7, player.H_ContTopMax);

        InvokeUpdateHiClassStreaks(player, new GameReport.UserResult { Ranking = 2 });

        Assert.Equal(0, player.H_ContTopNow);
        Assert.Equal(7, player.H_ContTopMax);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// GameLogicService.CalcRating テスト
// 原典: HMajRoomServer::CalcRating_MajakType (TYPE1)
//   R_acquired = Σ K * P_ij * Regulation
// ═══════════════════════════════════════════════════════════════════════════
public class CalcRatingTests
{
    private static GameLogicService BuildService(PlayerSessionService session)
    {
        var histMock   = new Mock<HistoryRepository>(MockBehavior.Loose);
        var logMock    = new Mock<LogRepository>(MockBehavior.Loose, (MySqlDbContext)null!);
        var playerMock = new Mock<PlayerRepository>(MockBehavior.Loose);
        var titleMock  = new Mock<TitleService>(MockBehavior.Loose, (PlayerRepository)null!, TestMasterCacheFactory.Create());
        var moneyMock  = new Mock<GameMoneyService>(MockBehavior.Loose,
            (PlayerRepository)null!, (RatingService)null!, (HistoryRepository?)null);
        return new GameLogicService(session, histMock.Object, logMock.Object,
            new RatingService(), playerMock.Object, moneyMock.Object, titleMock.Object, null!, null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
    }

    private static void InvokeCalcRating(GameLogicService svc, GameReport report, GameRoom? room = null)
    {
        typeof(GameLogicService)
            .GetMethod("CalcRating", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(svc, new object[] { report, room ?? new MajakServer.Models.Game.GameRoom() });
    }

    // シナリオ1: 1位プレイヤーのレーティングが上がる
    [Fact]
    public void CalcRating_Winner_RatingIncreases()
    {
        var session = new PlayerSessionService();
        for (int i = 1; i <= 4; i++)
        {
            var p = new MajakPlayer { ConnectionId = $"c{i}", MemberNo = $"u{i}" };
            p.ActiveRecord.Rating = 1500;
            session.Register(p);
        }

        var svc    = BuildService(session);
        var report = new GameReport();
        for (int i = 0; i < 4; i++)
            report.Users[i] = new GameReport.UserResult
            {
                MemberNo = $"u{i + 1}",
                Ranking  = i + 1,
            };

        InvokeCalcRating(svc, report);

        // 1位はレーティング上昇
        Assert.True(report.Users[0]!.RatingChange > 0, "1位はレーティングアップ");
        // 4位はレーティング下降
        Assert.True(report.Users[3]!.RatingChange < 0, "4位はレーティングダウン");
    }

    [Fact]
    public void CalcRating_EqualRatingFourPlayers_MatchesLegacyType1Regulation()
    {
        var session = new PlayerSessionService();
        for (int i = 1; i <= 4; i++)
        {
            var p = new MajakPlayer { ConnectionId = $"c{i}", MemberNo = $"u{i}" };
            p.ActiveRecord.Rating = 1500;
            session.Register(p);
        }

        var svc    = BuildService(session);
        var report = new GameReport();
        for (int i = 0; i < 4; i++)
            report.Users[i] = new GameReport.UserResult
            {
                MemberNo = $"u{i + 1}",
                Ranking  = i + 1,
            };

        InvokeCalcRating(svc, report);

        Assert.Equal(15, report.Users[0]!.RatingChange);
        Assert.Equal(5, report.Users[1]!.RatingChange);
        Assert.Equal(-5, report.Users[2]!.RatingChange);
        Assert.Equal(-15, report.Users[3]!.RatingChange);
    }

    [Fact]
    public void CalcRating_Type1_UsesRoomRatingFactors()
    {
        var session = new PlayerSessionService();
        for (int i = 1; i <= 4; i++)
        {
            var p = new MajakPlayer { ConnectionId = $"c{i}", MemberNo = $"u{i}" };
            p.ActiveRecord.Rating = 1500;
            session.Register(p);
        }

        var svc    = BuildService(session);
        var report = new GameReport();
        for (int i = 0; i < 4; i++)
            report.Users[i] = new GameReport.UserResult
            {
                MemberNo = $"u{i + 1}",
                Ranking  = i + 1,
            };

        InvokeCalcRating(svc, report, new GameRoom { RatingK = 40f, RatingRs = 400f });

        Assert.Equal(30, report.Users[0]!.RatingChange);
        Assert.Equal(10, report.Users[1]!.RatingChange);
        Assert.Equal(-10, report.Users[2]!.RatingChange);
        Assert.Equal(-30, report.Users[3]!.RatingChange);
    }

    [Fact]
    public void CalcRating_Type1_AppliesLegacyMemberFactors()
    {
        var session = new PlayerSessionService();
        for (int i = 1; i <= 4; i++)
        {
            var p = new MajakPlayer { ConnectionId = $"c{i}", MemberNo = $"u{i}" };
            p.ActiveRecord.Rating = 1500;
            p.ActiveRecord.MatchCnt = 0;
            session.Register(p);
        }

        var svc    = BuildService(session);
        var report = new GameReport();
        for (int i = 0; i < 4; i++)
            report.Users[i] = new GameReport.UserResult
            {
                MemberNo = $"u{i + 1}",
                Ranking  = i + 1,
            };

        InvokeCalcRating(svc, report, new GameRoom
        {
            RatingNoviceThreshold = 1600,
            RatingNoviceRate = 2,
            RatingBonusThreshold = 1600,
            RatingBonus = 3,
        });

        Assert.Equal(33, report.Users[0]!.RatingChange);
        Assert.Equal(13, report.Users[1]!.RatingChange);
        Assert.Equal(-7, report.Users[2]!.RatingChange);
        Assert.Equal(-27, report.Users[3]!.RatingChange);
    }

    [Fact]
    public void CalcRating_Type2_EqualRatingFourPlayers_UsesWinnerVsOthersFormula()
    {
        var session = new PlayerSessionService();
        for (int i = 1; i <= 4; i++)
        {
            var p = new MajakPlayer { ConnectionId = $"c{i}", MemberNo = $"u{i}" };
            p.ActiveRecord.Rating = 1500;
            session.Register(p);
        }

        var svc    = BuildService(session);
        var report = new GameReport();
        for (int i = 0; i < 4; i++)
            report.Users[i] = new GameReport.UserResult
            {
                MemberNo = $"u{i + 1}",
                Ranking  = i + 1,
            };

        InvokeCalcRating(svc, report, new GameRoom { RatingRuleType = 2 });

        Assert.Equal(15, report.Users[0]!.RatingChange);
        Assert.Equal(-5, report.Users[1]!.RatingChange);
        Assert.Equal(-5, report.Users[2]!.RatingChange);
        Assert.Equal(-5, report.Users[3]!.RatingChange);
    }

    [Fact]
    public void CalcRating_Type1_TeamPlay_DistributesTeamRatingChange()
    {
        var session = new PlayerSessionService();
        for (int i = 1; i <= 4; i++)
        {
            var p = new MajakPlayer { ConnectionId = $"c{i}", MemberNo = $"u{i}" };
            p.ActiveRecord.Rating = 1500;
            session.Register(p);
        }

        var svc    = BuildService(session);
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult { MemberNo = "u1", Ranking = 1, TeamId = 10 };
        report.Users[1] = new GameReport.UserResult { MemberNo = "u2", Ranking = 2, TeamId = 10 };
        report.Users[2] = new GameReport.UserResult { MemberNo = "u3", Ranking = 3, TeamId = 20 };
        report.Users[3] = new GameReport.UserResult { MemberNo = "u4", Ranking = 4, TeamId = 20 };

        InvokeCalcRating(svc, report);

        Assert.Equal(10, report.Users[0]!.RatingChange);
        Assert.Equal(10, report.Users[1]!.RatingChange);
        Assert.Equal(-10, report.Users[2]!.RatingChange);
        Assert.Equal(-10, report.Users[3]!.RatingChange);
    }

    [Fact]
    public void CalcRating_Type2_TeamPlay_DistributesTeamRatingChange()
    {
        var session = new PlayerSessionService();
        for (int i = 1; i <= 4; i++)
        {
            var p = new MajakPlayer { ConnectionId = $"c{i}", MemberNo = $"u{i}" };
            p.ActiveRecord.Rating = 1500;
            session.Register(p);
        }

        var svc    = BuildService(session);
        var report = new GameReport();
        report.Users[0] = new GameReport.UserResult { MemberNo = "u1", Ranking = 1, TeamId = 10 };
        report.Users[1] = new GameReport.UserResult { MemberNo = "u2", Ranking = 2, TeamId = 10 };
        report.Users[2] = new GameReport.UserResult { MemberNo = "u3", Ranking = 3, TeamId = 20 };
        report.Users[3] = new GameReport.UserResult { MemberNo = "u4", Ranking = 4, TeamId = 20 };

        InvokeCalcRating(svc, report, new GameRoom { RatingRuleType = 2 });

        Assert.Equal(5, report.Users[0]!.RatingChange);
        Assert.Equal(5, report.Users[1]!.RatingChange);
        Assert.Equal(-5, report.Users[2]!.RatingChange);
        Assert.Equal(-5, report.Users[3]!.RatingChange);
    }

    [Fact]
    public void CalcRating_GradeMode_UsesPointSumForLegacyFormula()
    {
        var session = new PlayerSessionService();
        for (int i = 1; i <= 4; i++)
        {
            var p = new MajakPlayer { ConnectionId = $"c{i}", MemberNo = $"u{i}" };
            p.GradeRecord.Rating = 1500;
            p.ActiveRecord = p.GradeRecord;
            session.Register(p);
        }

        var svc    = BuildService(session);
        var report = new GameReport();
        for (int i = 0; i < 4; i++)
            report.Users[i] = new GameReport.UserResult
            {
                MemberNo = $"u{i + 1}",
                Score = 0,
                PointSum = 20,
                MatchCnt = 0,
            };

        InvokeCalcRating(svc, report, new GameRoom { SubId = "00G7A" });

        Assert.All(report.Users.Where(u => u != null), u =>
        {
            Assert.Equal(10, u!.RatingChange);
            Assert.Equal(1510, u.Rating);
        });
    }

    [Fact]
    public void CalcRating_GradeMode_UsesAccumulatedGradeMatchCount()
    {
        var session = new PlayerSessionService();
        for (int i = 1; i <= 4; i++)
        {
            var player = new MajakPlayer { ConnectionId = $"c{i}", MemberNo = $"u{i}" };
            player.GradeRecord.Rating = 1500;
            player.GradeRecord.MatchCnt = 400;
            player.ActiveRecord = player.GradeRecord;
            session.Register(player);
        }

        var service = BuildService(session);
        var report = new GameReport();
        for (int i = 0; i < 4; i++)
            report.Users[i] = new GameReport.UserResult
            {
                MemberNo = $"u{i + 1}",
                PointSum = 20,
                MatchCnt = 1,
            };

        InvokeCalcRating(service, report, new GameRoom { SubId = "00G7A" });

        Assert.All(report.Users.Where(user => user != null), user =>
        {
            Assert.Equal(2, user!.RatingChange);
            Assert.Equal(1502, user.Rating);
        });
    }

    // シナリオ2: 等しいレーティングなら 1位>0, 4位<0 のゼロサム
    [Fact]
    public void CalcRating_EqualRating_ZeroSum()
    {
        var session = new PlayerSessionService();
        for (int i = 1; i <= 4; i++)
        {
            var p = new MajakPlayer { ConnectionId = $"c{i}", MemberNo = $"u{i}" };
            p.ActiveRecord.Rating = 1500; // 全員同じレーティング
            session.Register(p);
        }

        var svc    = BuildService(session);
        var report = new GameReport();
        for (int i = 0; i < 4; i++)
            report.Users[i] = new GameReport.UserResult
            {
                MemberNo = $"u{i + 1}",
                Ranking  = i + 1,
            };

        InvokeCalcRating(svc, report);

        int total = report.Users.Where(u => u != null).Sum(u => u!.RatingChange);
        // ゼロサムに近い (丸め誤差あり、±10以内)
        Assert.InRange(total, -10, 10);
    }

    // シナリオ3: プレイヤー0人では何も変わらない
    [Fact]
    public void CalcRating_EmptyReport_NoChanges()
    {
        var session = new PlayerSessionService();
        var svc     = BuildService(session);
        var report  = new GameReport();

        // 例外なく実行されること
        var ex = Record.Exception(() => InvokeCalcRating(svc, report));
        Assert.Null(ex);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// RuleInfo テスト
// 原典: RULEINFO struct (MajakDef.h)
// ═══════════════════════════════════════════════════════════════════════════
public class RuleInfoTests
{
    // シナリオ1: デフォルト RuleInfo のフィールド確認
    [Fact]
    public void RuleInfo_Default_Values()
    {
        var rule = new RuleInfo();
        Assert.False(rule.Hanchan);
        Assert.False(rule.Kuitan);
        Assert.Equal(0, rule.AkaDora);
        Assert.Equal(0, rule.Uma);
        Assert.Equal(0, rule.Contest);
    }

    // シナリオ2: with 式による複製
    [Fact]
    public void RuleInfo_WithExpression_Works()
    {
        var rule = new RuleInfo { Hanchan = true, Kuitan = true, AkaDora = 1 };
        var copy = rule with { AkaDora = 2 };

        Assert.Equal(1, rule.AkaDora);  // 元は変わらない
        Assert.Equal(2, copy.AkaDora);  // コピーで変更
        Assert.True(copy.Hanchan);       // Hanchan は引き継がれる
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// EnginePlayer ResultRecord テスト
// 原典: HMajakPlayer m_stRecResult — TurnCnt / HoraCnt / RichiCnt など
// ═══════════════════════════════════════════════════════════════════════════
public class EnginePlayerResultRecordTests
{
    private static RuleInfo DefaultRule() => new()
    {
        Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 1, Uma = 0,
    };

    // シナリオ1: InitHanchan → ResultRecord が 0 リセット
    [Fact]
    public void InitHanchan_ResultRecord_IsZero()
    {
        var p = new EnginePlayer();
        p.ResultRecord.TurnCnt = 10;
        p.InitHanchan(0, DefaultRule());

        Assert.Equal(0, p.ResultRecord.TurnCnt);
        Assert.Equal(0, p.ResultRecord.HoraCnt);
    }

    // シナリオ2: HMajakPlayer::InitKyoku itself does not back up ResultRecord.
    [Fact]
    public void InitKyoku_DoesNotBackUpResultRecordByItself()
    {
        var p = new EnginePlayer();
        p.InitHanchan(0, DefaultRule());
        p.ResultRecordSave.TurnCnt = 2;
        p.ResultRecord.TurnCnt = 5;

        p.InitKyoku();

        Assert.Equal(2, p.ResultRecordSave.TurnCnt);
    }

    [Fact]
    public void GameLogicInitKyoku_BacksUpResultRecordLikeLegacy()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        logic.Player[0].ResultRecord.TurnCnt = 5;

        typeof(MajakGameLogic)
            .GetMethod("InitKyoku", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(logic, Array.Empty<object>());

        Assert.Equal(5, logic.Player[0].ResultRecordSave.TurnCnt);
    }

    // シナリオ3: Tap処理 → TurnCnt++ (ProcessTurn で増加)
    [Fact]
    public void ProcessTurn_Tap_IncrementsTurnCnt()
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());

        int parent = logic.KyokuInfo.OyaOrder;
        var tapTile = logic.Player[parent].Tehai.Last();
        int prevTurnCnt = logic.Player[parent].ResultRecord.TurnCnt;

        logic.ProcessAction(parent, MajakServer.Engine.Act.Tap, new[] { tapTile.BipaiIndex }, 1);

        Assert.Equal(prevTurnCnt + 1, logic.Player[parent].ResultRecord.TurnCnt);
    }
}
