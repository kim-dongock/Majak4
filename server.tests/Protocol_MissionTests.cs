using Moq;
using Microsoft.Extensions.Logging;
using MajakServer.Commands.Channel;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;

namespace MajakServer.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// mjkc32e GetMissionListCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   1. 正常 → デイリーミッション11件 + 週間報酬8件が含まれる
///   2. ポイント不足の週間報酬 → 受取不可 (値=2)
///   3. player=null → 何も送らない
///   4. 必須フィールド確認
/// </summary>
public class GetMissionListCommandTests
{
    private readonly Mock<LogRepository>     _logMock    = new(MockBehavior.Loose, (MySqlDbContext)null!);
    private readonly Mock<PlayerRepository>  _repoMock   = new(MockBehavior.Loose);

    private MissionService BuildMissionService(
        int weeklyPoint = 0,
        Dictionary<int, int>? dailyMap = null,
        Dictionary<int, int>? weeklyMap = null,
        Dictionary<int, WeeklyRewardMastInfo>? weeklyMast = null)
    {
        _repoMock.Setup(r => r.GetDailyMissionListForTodayAsync(It.IsAny<string>()))
            .ReturnsAsync(dailyMap ?? new Dictionary<int, int>());
        _repoMock.Setup(r => r.GetDailyMissionMastAsync())
            .ReturnsAsync(Enumerable.Range(1, 11).ToDictionary(i => i, i => new DailyMissionMastInfo
            {
                MissionId = i,
                ConditionType = i,
                ConditionCnt = 1,
                Point = 1,
            }));
        _repoMock.Setup(r => r.GetWeeklyPointForWeekAsync(It.IsAny<string>()))
            .ReturnsAsync(weeklyPoint);
        _repoMock.Setup(r => r.GetWeeklyRewardListForWeekAsync(It.IsAny<string>()))
            .ReturnsAsync(weeklyMap ?? new Dictionary<int, int>());
        _repoMock.Setup(r => r.GetWeeklyRewardMastAsync())
            .ReturnsAsync(weeklyMast ?? Enumerable.Range(1, 8)
                .ToDictionary(i => i, i => new WeeklyRewardMastInfo
                {
                    RewardId   = i,
                    MustPoint  = i * 10,
                    RewardType = 1,
                    RewardCnt  = 100,
                }));

        return new MissionService(_logMock.Object, _repoMock.Object, TestMasterCacheFactory.Create(playerRepo: _repoMock.Object));
    }

    // シナリオ1: 正常レスポンス — 11件デイリー + 8件週間報酬
    [Fact]
    public async Task Execute_Normal_Returns11DailyAnd8WeeklyFields()
    {
        var svc    = BuildMissionService();
        var player = new MajakPlayer { MemberNo = "user01" };
        var cmd    = new GetMissionListCommand(svc);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.GetMissionList, sent[0].method);
        var dict = CommandTestHelper.AsDict(sent[0].packet);

        // 必須フィールド
        Assert.True(dict.ContainsKey(Key.PointDayOwn));
        Assert.True(dict.ContainsKey(Key.PointDayMax));
        Assert.True(dict.ContainsKey(Key.PointWeekOwn));
        Assert.True(dict.ContainsKey(Key.PointWeekMax));
        Assert.Equal(GKey.ValueSuccess, dict[GKey.Result]);

        // デイリーミッション 11 件
        Assert.True(dict.ContainsKey(Key.DailyMission1));
        Assert.True(dict.ContainsKey(Key.DailyMission11));

        // 週間報酬 8 件
        Assert.True(dict.ContainsKey(Key.WeeklyReward1));
        Assert.True(dict.ContainsKey(Key.WeeklyReward8));
    }

    // シナリオ2: ポイント不足 → 週間報酬=1 (MSN_RS_RCV)
    [Fact]
    public async Task Execute_LowWeeklyPoint_WeeklyRewardIs1()
    {
        var svc    = BuildMissionService(weeklyPoint: 0);  // ポイント=0 → 全て受取不可
        var player = new MajakPlayer { MemberNo = "user01" };
        var cmd    = new GetMissionListCommand(svc);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        var dict = CommandTestHelper.AsDict(sent[0].packet);
        // MustPoint=10 に対してポイント=0 → 受取不可 (MSN_RS_RCV=1)
        Assert.Equal(1, (int)dict[Key.WeeklyReward1]);
    }

    // シナリオ3: ポイント十分 + 未受取 → 週間報酬=0
    [Fact]
    public async Task Execute_SufficientPoint_NotReceived_WeeklyRewardIs0()
    {
        var svc    = BuildMissionService(weeklyPoint: 100);  // ポイント=100 → 全て受取可能
        var player = new MajakPlayer { MemberNo = "user01" };
        var cmd    = new GetMissionListCommand(svc);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        var dict = CommandTestHelper.AsDict(sent[0].packet);
        // MustPoint=10 に対してポイント=100 → 受取可能・未受取 (値=0)
        Assert.Equal(0, (int)dict[Key.WeeklyReward1]);
        Assert.Equal(100, (int)dict[Key.PointWeekOwn]);
        Assert.Equal(77, (int)dict[Key.PointWeekMax]);
    }

    // シナリオ4: player=null
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new GetMissionListCommand(BuildMissionService());
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc33e RcvWeeklyRewardCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   1. 成功 → result=1 + 新コイン/ジェムが返る
///   2. ポイント不足 → result=0
///   3. 受取済み → result=0
///   4. player=null → 何も送らない
/// </summary>
public class RcvWeeklyRewardCommandTests
{
    private readonly Mock<PlayerRepository>          _repoMock = new(MockBehavior.Loose);
    private readonly Mock<LogRepository>             _logMock  = new(MockBehavior.Loose, (MySqlDbContext)null!);

    private (MissionService missionSvc, GameMoneyService moneySvc) BuildServices(
        bool rewardOk = true, int weeklyPoint = 100)
    {
        _repoMock.Setup(r => r.GetWeeklyRewardMastAsync())
            .ReturnsAsync(new Dictionary<int, WeeklyRewardMastInfo>
            {
                [1] = new() { RewardId = 1, MustPoint = 10, RewardType = 1, RewardCnt = 200 },
            });
        _repoMock.Setup(r => r.GetWeeklyPointForWeekAsync(It.IsAny<string>()))
            .ReturnsAsync(weeklyPoint);
        _repoMock.Setup(r => r.GetWeeklyRewardStatusForWeekAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(rewardOk ? (int?)0 : 1);
        _repoMock.Setup(r => r.ReflectWeeklyRewardAsync(It.IsAny<MajakPlayer>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()))
            .ReturnsAsync(rewardOk);
        _repoMock.Setup(r => r.UpdateCommonRatAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.AddEarnedGameMoneyAsync(
            It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(0);
        _logMock.Setup(r => r.InsertWeeklyRewardHistAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var moneySvc    = new GameMoneyService(_repoMock.Object, new RatingService());
        var missionSvc  = new MissionService(_logMock.Object, _repoMock.Object, TestMasterCacheFactory.Create(playerRepo: _repoMock.Object));
        return (missionSvc, moneySvc);
    }

    // シナリオ1: 報酬受取成功 → result=1 + コイン付与
    [Fact]
    public async Task Execute_Success_ReturnsResult1WithNewMoney()
    {
        var (mission, money) = BuildServices(rewardOk: true, weeklyPoint: 100);
        var player = new MajakPlayer { MemberNo = "user01", GamMoney = 1000 };
        var cmd = new RcvWeeklyRewardCommand(mission, money);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [Key.WeeklyRewardId] = 1 });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.RcvWeeklyReward, sent[0].method);
        Assert.Equal(1, CommandTestHelper.GetResult(sent[0].packet));
        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.Equal(GKey.ValueSuccess, dict[GKey.Result]);
        Assert.Equal(1200L, (long)dict[GKey.GamMoney]);
        Assert.True(dict.ContainsKey(Key.GemCount));
        Assert.True(dict.ContainsKey(GKey.SLevel));
        Assert.True(dict.ContainsKey(GKey.NLevel));
        // コインが付与されていること (200加算)
        Assert.Equal(1200, player.GamMoney);
    }

    // シナリオ2: ポイント不足 → result=0
    [Fact]
    public async Task Execute_LowWeeklyPoint_Failure()
    {
        var (mission, money) = BuildServices(weeklyPoint: 0);
        var player = new MajakPlayer { MemberNo = "user01", GamMoney = 1000 };
        var cmd = new RcvWeeklyRewardCommand(mission, money);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [Key.WeeklyRewardId] = 1 });

        await cmd.ExecuteAsync(ctx);

        Assert.Equal(0, CommandTestHelper.GetResult(sent[0].packet));
        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.Equal(GKey.ValueFailure, dict[GKey.Result]);
        Assert.Equal("ポイントが足りません。", dict[GKey.Message]);
        Assert.False(dict.ContainsKey(GKey.GamMoney));
        Assert.False(dict.ContainsKey(Key.GemCount));
        Assert.False(dict.ContainsKey(GKey.SLevel));
        Assert.False(dict.ContainsKey(GKey.NLevel));
        Assert.Equal(1000, player.GamMoney);  // コイン変化なし
    }

    // シナリオ3: 受取済み → result=0
    [Fact]
    public async Task Execute_AlreadyReceived_Failure()
    {
        var (mission, money) = BuildServices(rewardOk: false, weeklyPoint: 100);
        var player = new MajakPlayer { MemberNo = "user01", GamMoney = 1000 };
        var cmd = new RcvWeeklyRewardCommand(mission, money);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [Key.WeeklyRewardId] = 1 });

        await cmd.ExecuteAsync(ctx);

        Assert.Equal(0, CommandTestHelper.GetResult(sent[0].packet));
    }

    // シナリオ4: player=null
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var (mission, money) = BuildServices();
        var cmd = new RcvWeeklyRewardCommand(mission, money);
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc34e RcvSerialBonusCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   1. 有効なシリアルコード → result=1 + コイン付与
///   2. 無効なシリアルコード → result=0
///   3. 空のシリアルコード → result=0
///   4. player=null → 何も送らない
/// </summary>
public class RcvSerialBonusCommandTests
{
    private readonly Mock<PlayerRepository>          _repoMock = new(MockBehavior.Loose);
    private readonly Mock<LogRepository>             _logMock  = new(MockBehavior.Loose, (MySqlDbContext)null!);

    private (MissionService missionSvc, GameMoneyService moneySvc) BuildServices(
        bool serialValid = true, long bonusMoney = 500, int missionNo = 1, string giftMessage = "bonus")
    {
        _repoMock.Setup(r => r.GetSerialMastsAsync())
            .ReturnsAsync(serialValid
                ? new List<SerialMastInfo> { new() { GiftCode = "CODE01", EvtCode = "EVT01", EvtNo = 1, MissionNo = missionNo, GiftValue = (int)bonusMoney, GiftMessage = giftMessage } }
                : new List<SerialMastInfo>());
        _repoMock.Setup(r => r.SerialExchangeItemExistsAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        _repoMock.Setup(r => r.InsertSerialExchangeItemAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(serialValid);
        _repoMock.Setup(r => r.UpdateCommonRatSerialResourceAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.AddEarnedGameMoneyAsync(
            It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(0);

        var moneySvc   = new GameMoneyService(_repoMock.Object, new RatingService());
        var missionSvc = new MissionService(_logMock.Object, _repoMock.Object, TestMasterCacheFactory.Create(playerRepo: _repoMock.Object));
        return (missionSvc, moneySvc);
    }

    // シナリオ1: 有効コード → result=1 + コイン付与
    [Fact]
    public async Task Execute_ValidSerial_ReturnsSuccess()
    {
        var (mission, money) = BuildServices(serialValid: true, bonusMoney: 500);
        var player = new MajakPlayer { MemberNo = "user01", GamMoney = 1000 };
        var cmd = new RcvSerialBonusCommand(mission, money);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [Key.SerialCode] = "CODE01" });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.RcvSerialBonus, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(GKey.ValueSuccess, ((System.Text.Json.JsonElement)pkt[GKey.Result]!).GetString());
        Assert.Equal("bonus", ((System.Text.Json.JsonElement)pkt[GKey.Message]!).GetString());
        Assert.Equal(1500, ((System.Text.Json.JsonElement)pkt[GKey.GamMoney]!).GetInt64());
        Assert.Equal(0, ((System.Text.Json.JsonElement)pkt[Key.GemCount]!).GetInt32());
        Assert.Equal(player.SLevel, ((System.Text.Json.JsonElement)pkt[GKey.SLevel]!).GetString());
        Assert.Equal(player.NLevel, ((System.Text.Json.JsonElement)pkt[GKey.NLevel]!).GetInt32());
        Assert.Equal(1500, player.GamMoney);  // 500 加算
    }

    [Fact]
    public async Task Execute_ValidGemSerial_ReturnsGemCountAndKeepsMoney()
    {
        var (mission, money) = BuildServices(serialValid: true, bonusMoney: 5, missionNo: 2, giftMessage: "gem bonus");
        var player = new MajakPlayer { MemberNo = "user01", GamMoney = 1000, GemCount = 10 };
        var cmd = new RcvSerialBonusCommand(mission, money);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [Key.SerialCode] = "CODE01" });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(GKey.ValueSuccess, ((System.Text.Json.JsonElement)pkt[GKey.Result]!).GetString());
        Assert.Equal("gem bonus", ((System.Text.Json.JsonElement)pkt[GKey.Message]!).GetString());
        Assert.Equal(1000, ((System.Text.Json.JsonElement)pkt[GKey.GamMoney]!).GetInt64());
        Assert.Equal(15, ((System.Text.Json.JsonElement)pkt[Key.GemCount]!).GetInt32());
        Assert.Equal(15, player.GemCount);
    }

    // シナリオ2: 無効コード → result=0
    [Fact]
    public async Task Execute_InvalidSerial_Failure()
    {
        var (mission, money) = BuildServices(serialValid: false);
        var player = new MajakPlayer { MemberNo = "user01", GamMoney = 1000 };
        var cmd = new RcvSerialBonusCommand(mission, money);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [Key.SerialCode] = "INVALID" });

        await cmd.ExecuteAsync(ctx);

        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(GKey.ValueFailure, ((System.Text.Json.JsonElement)pkt[GKey.Result]!).GetString());
        Assert.Equal(1000, player.GamMoney);
    }

    // シナリオ3: 空のシリアルコード → result=0
    [Fact]
    public async Task Execute_EmptySerial_Failure()
    {
        var (mission, money) = BuildServices();
        var player = new MajakPlayer { MemberNo = "user01", GamMoney = 1000 };
        var cmd = new RcvSerialBonusCommand(mission, money);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [Key.SerialCode] = "" });

        await cmd.ExecuteAsync(ctx);

        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(GKey.ValueFailure, ((System.Text.Json.JsonElement)pkt[GKey.Result]!).GetString());
    }

    // シナリオ4: player=null
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var (mission, money) = BuildServices();
        var cmd = new RcvSerialBonusCommand(mission, money);
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}
