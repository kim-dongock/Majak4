using Moq;
using Microsoft.Extensions.Logging;
using MajakServer.Commands.Channel;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;

namespace MajakServer.Tests;

/// <summary>
/// トーナメント系コマンドテスト (mjkc26e〜mjkc30e)
///
/// TournamentService はインメモリキャッシュを持つので、
/// テスト用に plans を直接注入してテストする。
/// </summary>

// ═══════════════════════════════════════════════════════════════════════════
// テストヘルパー — TournamentService インメモリ注入
// ═══════════════════════════════════════════════════════════════════════════
file static class TournamentTestHelper
{
    /// <summary>テスト用 TournamentService をプランなしで作成</summary>
    public static TournamentService BuildEmpty(
        Mock<TournamentRepository>? repoMock = null)
    {
        var mock   = repoMock ?? new(MockBehavior.Loose);
        var logger = new Mock<ILogger<TournamentService>>();
        return TestTournamentServiceFactory.Create(mock.Object, logger.Object);
    }

    /// <summary>テスト用プランを直接 _plans に注入する</summary>
    public static TournamentService BuildWithPlan(TournamentPlan plan,
        Mock<TournamentRepository>? repoMock = null)
    {
        var svc = BuildEmpty(repoMock);
        var plans = (System.Collections.Concurrent.ConcurrentDictionary<long, TournamentPlan>)
            typeof(TournamentService)
                .GetField("_plans",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)!
                .GetValue(svc)!;
        plans[plan.SeqNo] = plan;
        return svc;
    }

    public static TournamentPlan MakePlan(long seqNo = 1, bool isActive = false) => new()
    {
        SeqNo          = seqNo,
        PlayName       = "TestTournament",
        PlayStatus     = isActive ? TournamentPlanStatus.Join : TournamentPlanStatus.Init,
        PlayerNum      = 0,
        MaxPlayerNum   = 4,
        Password       = "",
        PlayMode       = 1,
        PlayNum        = 1,
        PlayTime       = 60,
        JoinMoney      = 0,
        GradeMoney     = new long[] { 1000, 500, 200, 0 },
        // 원전: IsTournamentJoinDayTime — JoinStartDt <= now < MatchStartDt
        JoinStartDt    = DateTime.Now.AddHours(-1),
        MatchStartDt   = DateTime.Now.AddHours(2),
        PlayStartDt    = DateTime.Now.AddHours(2),
        PlayEndDt      = DateTime.Now.AddHours(4),
        ViewEndDt      = DateTime.Now.AddHours(4),
        PlaySchedule   = "",
        RoomOption     = "",
        MaxViewer      = 4,
        PlanMemberNo   = "plan01",
        ResultMemberNo = new string[] { "", "", "", "" },
        MaxRoomNum     = 1,
    };
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc26e TournamentListCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   1. プランなし → result=0 + tournamentCnt=0
///   2. プラン1件あり → result=1 + tournamentCnt=1
///   3. player=null → 何も送らない
///   4. 必須フィールド確認
/// </summary>
public class TournamentListCommandTests
{
    private readonly Mock<TournamentRepository> _tournRepoMock
        = new(MockBehavior.Loose);

    public TournamentListCommandTests()
    {
        _tournRepoMock.Setup(r => r.SelectJoinAsync(It.IsAny<string>()))
            .ReturnsAsync((TournamentJoin?)null);
    }

    // シナリオ1: プランなし → result=0
    [Fact]
    public async Task Execute_NoPlans_Returns0()
    {
        var svc = TournamentTestHelper.BuildEmpty(_tournRepoMock);
        var player = new MajakPlayer { MemberNo = "user01" };
        var cmd = new TournamentListCommand(svc, _tournRepoMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.Pix] = "user01" });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.TournamentList, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(0, ((System.Text.Json.JsonElement)pkt["result"]!).GetInt32());
        Assert.Equal(0, ((System.Text.Json.JsonElement)pkt["tournamentCnt"]!).GetInt32());
    }

    // シナリオ2: プラン1件 → result=1 + cnt=1
    [Fact]
    public async Task Execute_OnePlan_Returns1()
    {
        var plan = TournamentTestHelper.MakePlan(seqNo: 1);
        var svc  = TournamentTestHelper.BuildWithPlan(plan, _tournRepoMock);
        var player = new MajakPlayer { MemberNo = "user01" };
        var cmd = new TournamentListCommand(svc, _tournRepoMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.Pix] = "user01" });

        await cmd.ExecuteAsync(ctx);

        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(1, ((System.Text.Json.JsonElement)pkt["result"]!).GetInt32());
        Assert.Equal(1, ((System.Text.Json.JsonElement)pkt["tournamentCnt"]!).GetInt32());
    }

    [Theory]
    [InlineData(TournamentJoinStatus.End)]
    [InlineData(TournamentJoinStatus.Cancel)]
    [InlineData(TournamentJoinStatus.Exit)]
    public async Task Execute_InactiveJoinStatus_ReturnsNoCurrentTournament(int joinStatus)
    {
        var plan = TournamentTestHelper.MakePlan(seqNo: 10);
        var svc = TournamentTestHelper.BuildWithPlan(plan, _tournRepoMock);
        _tournRepoMock.Setup(r => r.SelectJoinAsync("user01"))
            .ReturnsAsync(new TournamentJoin { JoinSeqNo = 10, JoinStatus = joinStatus });
        var player = new MajakPlayer { MemberNo = "user01" };
        var cmd = new TournamentListCommand(svc, _tournRepoMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.Pix] = "user01" });

        await cmd.ExecuteAsync(ctx);

        var packet = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(0, ((System.Text.Json.JsonElement)packet["tournamentJoinChk"]!).GetInt64());
    }

    // シナリオ3: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var svc = TournamentTestHelper.BuildEmpty(_tournRepoMock);
        var cmd = new TournamentListCommand(svc, _tournRepoMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    // シナリオ4: 必須フィールド確認
    [Fact]
    public async Task Execute_ResponseHasRequiredFields()
    {
        var svc = TournamentTestHelper.BuildEmpty(_tournRepoMock);
        var player = new MajakPlayer { MemberNo = "user01" };
        var cmd = new TournamentListCommand(svc, _tournRepoMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.Pix] = "user01" });

        await cmd.ExecuteAsync(ctx);

        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.True(pkt.ContainsKey("result"));
        Assert.True(pkt.ContainsKey("tournamentCnt"));
        Assert.True(pkt.ContainsKey("tournamentList"));
        Assert.True(pkt.ContainsKey("serverTime"));
        Assert.True(pkt.ContainsKey("tournamentRegistDayTime"));
    }

    [Fact]
    public async Task Execute_MissingMemberNo_NothingSent()
    {
        var svc = TournamentTestHelper.BuildEmpty(_tournRepoMock);
        var player = new MajakPlayer { MemberNo = "user01" };
        var cmd = new TournamentListCommand(svc, _tournRepoMock.Object);
        string? abortReason = null;
        var (ctx, sent) = CommandTestHelper.MakeContext(player, onAbort: reason => abortReason = reason);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
        Assert.Contains("TournamentListCommand invalid memberNo", abortReason);
        _tournRepoMock.Verify(r => r.SelectJoinAsync(It.IsAny<string>()), Times.Never);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc28e TournamentJoinCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   1. 有効な参加 → result=1 + gamMoney が返る
///   2. バリデーション失敗 → result=0 + failCode
///   3. player=null → 何も送らない
/// </summary>
public class TournamentJoinCommandTests
{
    private readonly Mock<TournamentRepository>  _tournRepoMock = new(MockBehavior.Loose);
    private readonly Mock<PlayerRepository>      _playerRepoMock = new(MockBehavior.Loose);
    private readonly Mock<HistoryRepository> _historyRepoMock = new(MockBehavior.Loose);

    private GameMoneyService BuildMoneyService()
    {
        _playerRepoMock.Setup(r => r.UpdateCommonRatAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);
        _historyRepoMock.Setup(r => r.InsertGameMoneyHistAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        return new GameMoneyService(_playerRepoMock.Object, new RatingService(), _historyRepoMock.Object);
    }

    // シナリオ1: 有効な参加 → result=1
    [Fact]
    public async Task Execute_ValidJoin_ReturnsResult1()
    {
        var plan = TournamentTestHelper.MakePlan(seqNo: 100, isActive: true);
        var svc  = TournamentTestHelper.BuildWithPlan(plan, _tournRepoMock);

        _tournRepoMock.Setup(r => r.SelectJoinAsync(It.IsAny<string>()))
            .ReturnsAsync((TournamentJoin?)null);
        _tournRepoMock.Setup(r => r.MergeJoinAsync(
                It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((true, 1));

        var player = new MajakPlayer { MemberNo = "user01", GamMoney = 50000 };
        var cmd = new TournamentJoinCommand(svc, _tournRepoMock.Object, BuildMoneyService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.Pix] = "user01", [Key.TournamentNo] = 100L });

        await cmd.ExecuteAsync(ctx);

        Assert.Equal(2, sent.Count);
        Assert.Equal(Cmd.TournamentJoin, sent[0].method);
        Assert.Equal(1, CommandTestHelper.GetResult(sent[0].packet));
        Assert.Equal("tournament:list_changed", sent[1].method);
        var changed = CommandTestHelper.ToDict(sent[1].packet);
        Assert.Equal(100, ((System.Text.Json.JsonElement)changed["seqNo"]!).GetInt64());
        Assert.Equal("joined", ((System.Text.Json.JsonElement)changed["changeType"]!).GetString());
        _tournRepoMock.Verify(r => r.MergeJoinAsync(
            "user01", 100, TournamentJoinStatus.Join, "00"), Times.Once);
    }

    [Fact]
    public async Task Execute_JoinRepositoryFailure_ReturnsDatabaseError()
    {
        var plan = TournamentTestHelper.MakePlan(seqNo: 100, isActive: true);
        var svc = TournamentTestHelper.BuildWithPlan(plan, _tournRepoMock);
        _tournRepoMock.Setup(r => r.SelectJoinAsync("user01"))
            .ReturnsAsync((TournamentJoin?)null);
        _tournRepoMock.Setup(r => r.MergeJoinAsync(
                "user01", 100, TournamentJoinStatus.Join, "00"))
            .ReturnsAsync((false, 0));

        var player = new MajakPlayer { MemberNo = "user01", GamMoney = 50000 };
        var cmd = new TournamentJoinCommand(svc, _tournRepoMock.Object, BuildMoneyService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.Pix] = "user01", [Key.TournamentNo] = 100L });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        var packet = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(0, CommandTestHelper.GetResult(sent[0].packet));
        Assert.Equal(9999, ((System.Text.Json.JsonElement)packet["failCode"]!).GetInt32());
    }

    [Fact]
    public async Task Execute_ValidJoin_WritesLegacyTournamentJoinMoneyHistory()
    {
        var plan = TournamentTestHelper.MakePlan(seqNo: 100, isActive: true);
        plan.JoinMoney = 500;
        var svc = TournamentTestHelper.BuildWithPlan(plan, _tournRepoMock);
        _tournRepoMock.Setup(r => r.SelectJoinAsync(It.IsAny<string>()))
            .ReturnsAsync((TournamentJoin?)null);
        _tournRepoMock.Setup(r => r.MergeJoinAsync(
                It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((true, 1));

        var player = new MajakPlayer { MemberNo = "user01", GamMoney = 50000, IpAddress = "1.2.3.4" };
        var cmd = new TournamentJoinCommand(svc, _tournRepoMock.Object, BuildMoneyService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.Pix] = "user01", [Key.TournamentNo] = 100L });

        await cmd.ExecuteAsync(ctx);

        Assert.Contains(sent, packet => packet.method == "tournament:list_changed");
        _historyRepoMock.Verify(r => r.InsertGameMoneyHistAsync(
            "user01", GameConst.EvtCodeTournamentJoin, -500, 50000, 49500, "1.2.3.4"), Times.Once);
    }

    // シナリオ2: 存在しないプラン → バリデーション失敗 → result=0
    [Fact]
    public async Task Execute_PlanNotFound_Returns0()
    {
        var svc = TournamentTestHelper.BuildEmpty(_tournRepoMock);
        _tournRepoMock.Setup(r => r.SelectJoinAsync(It.IsAny<string>()))
            .ReturnsAsync((TournamentJoin?)null);

        var player = new MajakPlayer { MemberNo = "user01", GamMoney = 50000 };
        var cmd = new TournamentJoinCommand(svc, _tournRepoMock.Object, BuildMoneyService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.Pix] = "user01", [Key.TournamentNo] = 999L });

        await cmd.ExecuteAsync(ctx);

        Assert.Equal(0, CommandTestHelper.GetResult(sent[0].packet));
    }

    // シナリオ3: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var svc = TournamentTestHelper.BuildEmpty(_tournRepoMock);
        var cmd = new TournamentJoinCommand(svc, _tournRepoMock.Object, BuildMoneyService());
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc29e TournamentJoinCancelCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   1. 未参加キャンセル → バリデーション失敗 → result=0
///   2. player=null → 何も送らない
/// </summary>
public class TournamentJoinCancelCommandTests
{
    private readonly Mock<TournamentRepository>      _tournRepoMock = new(MockBehavior.Loose);
    private readonly Mock<PlayerRepository>          _playerRepoMock = new(MockBehavior.Loose);
    private readonly Mock<HistoryRepository> _historyRepoMock = new(MockBehavior.Loose);

    private GameMoneyService BuildMoneyService()
    {
        _playerRepoMock.Setup(r => r.UpdateCommonRatAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);
        _historyRepoMock.Setup(r => r.InsertGameMoneyHistAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        return new GameMoneyService(_playerRepoMock.Object, new RatingService(), _historyRepoMock.Object);
    }

    // シナリオ1: 未参加状態でキャンセル → result=0
    [Fact]
    public async Task Execute_NotJoined_Returns0()
    {
        var svc = TournamentTestHelper.BuildEmpty(_tournRepoMock);
        _tournRepoMock.Setup(r => r.SelectJoinAsync(It.IsAny<string>()))
            .ReturnsAsync((TournamentJoin?)null);  // 参加記録なし

        var player = new MajakPlayer { MemberNo = "user01" };
        var cmd = new TournamentJoinCancelCommand(svc, _tournRepoMock.Object, BuildMoneyService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.Pix] = "user01", [Key.TournamentNo] = 100L });

        await cmd.ExecuteAsync(ctx);

        Assert.Equal(0, CommandTestHelper.GetResult(sent[0].packet));
    }

    // シナリオ2: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var svc = TournamentTestHelper.BuildEmpty(_tournRepoMock);
        var cmd = new TournamentJoinCancelCommand(svc, _tournRepoMock.Object, BuildMoneyService());
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    [Fact]
    public async Task Execute_ValidCancel_WritesLegacyTournamentJoinCancelMoneyHistory()
    {
        var plan = TournamentTestHelper.MakePlan(seqNo: 100, isActive: true);
        plan.JoinMoney = 500;
        var svc = TournamentTestHelper.BuildWithPlan(plan, _tournRepoMock);
        _tournRepoMock.Setup(r => r.SelectJoinAsync("user01"))
            .ReturnsAsync(new TournamentJoin { JoinSeqNo = 100, JoinStatus = TournamentJoinStatus.Join });
        _tournRepoMock.Setup(r => r.MergeJoinAsync("user01", 100, TournamentJoinStatus.Cancel, "00"))
            .ReturnsAsync((true, 1));

        var player = new MajakPlayer { MemberNo = "user01", GamMoney = 49500, IpAddress = "1.2.3.4" };
        var cmd = new TournamentJoinCancelCommand(svc, _tournRepoMock.Object, BuildMoneyService());
        var (ctx, _) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.Pix] = "user01", [Key.TournamentNo] = 100L });

        await cmd.ExecuteAsync(ctx);

        _historyRepoMock.Verify(r => r.InsertGameMoneyHistAsync(
            "user01", GameConst.EvtCodeTournamentJoinCancel, 500, 49500, 50000, "1.2.3.4"), Times.Once);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc30e TournamentDetailCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   1. 存在しない seqNo → result=0
///   2. 存在する seqNo → result=1 + プラン情報
///   3. player=null → 何も送らない
///   4. 必須フィールド確認
/// </summary>
public class TournamentDetailCommandTests
{
    // シナリオ1: プランなし → result=0
    [Fact]
    public async Task Execute_NoPlan_Returns0()
    {
        var svc = TournamentTestHelper.BuildEmpty();
        var player = new MajakPlayer { MemberNo = "user01" };
        var cmd = new TournamentDetailCommand(svc);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.Pix] = "user01", [Key.TournamentNo] = 999L });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.TournamentDetail, sent[0].method);
        Assert.Equal(0, CommandTestHelper.GetResult(sent[0].packet));
    }

    // シナリオ2: プランあり → result=1 + tournamentList に内容
    [Fact]
    public async Task Execute_PlanExists_Returns1()
    {
        var plan = TournamentTestHelper.MakePlan(seqNo: 200);
        var svc  = TournamentTestHelper.BuildWithPlan(plan);
        var player = new MajakPlayer { MemberNo = "user01" };
        var cmd = new TournamentDetailCommand(svc);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.Pix] = "user01", [Key.TournamentNo] = 200L });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(1, CommandTestHelper.GetResult(sent[0].packet));
    }

    // シナリオ3: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var svc = TournamentTestHelper.BuildEmpty();
        var cmd = new TournamentDetailCommand(svc);
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    // シナリオ4: 必須フィールド確認
    [Fact]
    public async Task Execute_ResponseHasRequiredFields()
    {
        var plan = TournamentTestHelper.MakePlan(seqNo: 300);
        var svc  = TournamentTestHelper.BuildWithPlan(plan);
        var player = new MajakPlayer { MemberNo = "user01" };
        var cmd = new TournamentDetailCommand(svc);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.Pix] = "user01", [Key.TournamentNo] = 300L });

        await cmd.ExecuteAsync(ctx);

        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.True(pkt.ContainsKey("result"));
        Assert.True(pkt.ContainsKey("tournamentDetailCnt"));
        Assert.True(pkt.ContainsKey("serverTime"));
    }
}
