using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using MajakServer.Commands;
using MajakServer.Commands.Channel;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;

namespace MajakServer.Tests;

/// <summary>
/// プロトコルコマンドテスト共通ヘルパー
/// </summary>
public static class CommandTestHelper
{
    /// <summary>
    /// CommandContext を簡単に作れるファクトリー。
    /// 送信されたパケットを captured に記録する。
    /// </summary>
    public static (CommandContext ctx, List<(string method, object packet)> sent)
        MakeContext(MajakPlayer player, Dictionary<string, object?>? payload = null, Action<string>? onAbort = null)
    {
        var sent     = new List<(string, object)>();
        var callerMock  = new Mock<IClientProxy>();
        var clientsMock = new Mock<IHubCallerClients>();
        var groupsMock  = new Mock<IGroupManager>();
        var groupProxy  = new Mock<IClientProxy>();

        callerMock.Setup(c => c.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((m, args, _) =>
                sent.Add((m, args[0]!)))
            .Returns(Task.CompletedTask);

        groupProxy.Setup(c => c.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((m, args, _) =>
                sent.Add((m, args[0]!)))
            .Returns(Task.CompletedTask);

        var singleClientProxy = new Mock<ISingleClientProxy>();
        singleClientProxy.Setup(c => c.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((m, args, _) =>
                sent.Add((m, args[0]!)))
            .Returns(Task.CompletedTask);

        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(groupProxy.Object);
        clientsMock.Setup(c => c.Client(It.IsAny<string>())).Returns(singleClientProxy.Object);
        clientsMock.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>())).Returns(groupProxy.Object);
        clientsMock.Setup(c => c.GroupExcept(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>())).Returns(groupProxy.Object);
        clientsMock.Setup(c => c.OthersInGroup(It.IsAny<string>())).Returns(groupProxy.Object);
        groupsMock.Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        var ctx = new CommandContext
        {
            ConnectionId = player?.ConnectionId ?? "",
            Player       = player,
            Caller       = callerMock.Object,
            Clients      = clientsMock.Object,
            Groups       = groupsMock.Object,
            AbortConnectionWithReason = reason => onAbort?.Invoke(reason),
            Payload      = payload ?? new(),
        };

        return (ctx, sent);
    }

    /// <summary>送信されたパケットを Dictionary として取得</summary>
    public static Dictionary<string, object> AsDict(object packet)
        => (Dictionary<string, object>)packet;

    /// <summary>
    /// anonymous type / Dictionary を JSON 経由で Dictionary に変換する。
    /// クロスアセンブリ anonymous type の dynamic アクセス回避用。
    /// </summary>
    public static Dictionary<string, object?> ToDict(object packet)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(packet);
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(json)!;
    }

    /// <summary>result フィールドを int で取得するショートカット</summary>
    public static int GetResult(object packet)
    {
        var d = ToDict(packet);
        return ((System.Text.Json.JsonElement)d["result"]!).GetInt32();
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc17e MoneyReplenishmentCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
public class MoneyReplenishmentCommandTests
{
    private readonly Mock<PlayerRepository>          _playerRepoMock = new(MockBehavior.Loose);

    private GameMoneyService BuildMoneyService()
    {
        _playerRepoMock.Setup(r => r.UpdateCommonRatAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);
        _playerRepoMock.Setup(r => r.UpdateChargeFreeMoneyAsync(It.IsAny<MajakPlayer>()))
            .ReturnsAsync(true);
        return new GameMoneyService(_playerRepoMock.Object, new RatingService());
    }

    // シナリオ1: コインが不足している → 補充成功 → チャンネル全員へブロードキャスト
    [Fact]
    public async Task Execute_LowMoney_SuccessPacketBroadcastToChannel()
    {
        var cmd    = new MoneyReplenishmentCommand(BuildMoneyService());
        var player = new MajakPlayer
        {
            MemberNo  = "user01",
            ChannelId = "MAJAK20ZG6A001",
            GamMoney  = 100,
            AllinCnt  = 0
        };
        var (ctx, sent) = CommandTestHelper.MakeContext(player);
        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        var (method, packet) = sent[0];
        var dict = CommandTestHelper.AsDict(packet);

        Assert.Equal(Cmd.MoneyReplenishment, method);
        Assert.Equal("user01", dict["memberNo"]);
        Assert.Equal("success", dict["result"]);
        Assert.Equal(GameConst.AllinMoney, (long)dict["gammoney"]);
        Assert.Equal(2, (int)dict[Key.ReplenishmentType]);  // FREE=2
    }

    // シナリオ2: コインが十分 → 補充不要 → 要求者のみにレスポンス
    [Fact]
    public async Task Execute_SufficientMoney_FailurePacketToCallerOnly()
    {
        var cmd    = new MoneyReplenishmentCommand(BuildMoneyService());
        var player = new MajakPlayer
        {
            MemberNo  = "user01",
            ChannelId = "MAJAK20ZG6A001",
            GamMoney  = 1000,
            AllinCnt  = 0
        };
        var (ctx, sent) = CommandTestHelper.MakeContext(player);
        await cmd.ExecuteAsync(ctx);

        // 失敗のとき Caller (個人) のみへ送信
        Assert.Single(sent);
        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.Equal("failure", dict["result"]);
        Assert.Equal(0, (int)dict[Key.ReplenishmentType]);
    }

    // シナリオ3: 未ログイン (player=null) → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new MoneyReplenishmentCommand(BuildMoneyService());
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    // シナリオ4: レスポンスパケットに必須フィールドが全部揃っているか確認
    [Fact]
    public async Task Execute_ResponseContainsAllRequiredFields()
    {
        var cmd    = new MoneyReplenishmentCommand(BuildMoneyService());
        var player = new MajakPlayer
        {
            MemberNo  = "user01",
            ChannelId = "MAJAK20ZG6A001",
            GamMoney  = 0,
            AllinCnt  = 0,
            Rating    = 1400
        };
        var (ctx, sent) = CommandTestHelper.MakeContext(player);
        await cmd.ExecuteAsync(ctx);

        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.True(dict.ContainsKey("memberNo"));
        Assert.True(dict.ContainsKey("result"));
        Assert.True(dict.ContainsKey("rating"));
        Assert.True(dict.ContainsKey("slevel"));
        Assert.True(dict.ContainsKey("nlevel"));
        Assert.True(dict.ContainsKey("gammoney"));
        Assert.True(dict.ContainsKey(Key.ReplenishmentType));
        Assert.True(dict.ContainsKey(Key.RestAllInCnt));
        Assert.True(dict.ContainsKey(Key.AllInCnt));
        Assert.True(dict.ContainsKey(Key.UseLentMoney));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc18e ApplyEarnedMoneyCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
public class ApplyEarnedMoneyCommandTests
{
    private readonly Mock<PlayerRepository>          _playerRepoMock = new(MockBehavior.Loose);

    private GameMoneyService BuildMoneyService()
    {
        _playerRepoMock.Setup(r => r.UpdateCommonRatAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);
        _playerRepoMock.Setup(r => r.GetEarnedMoneyAsync(It.IsAny<string>()))
            .ReturnsAsync(((long, int)?)(0, 0));
        return new GameMoneyService(_playerRepoMock.Object, new RatingService());
    }

    // シナリオ1: EarnedMoney あり → 適用成功 → チャンネル全員ブロードキャスト
    [Fact]
    public async Task Execute_HasEarned_SuccessAndBroadcast()
    {
        var cmd    = new ApplyEarnedMoneyCommand(BuildMoneyService());
        var player = new MajakPlayer
        {
            MemberNo    = "user01",
            ChannelId   = "ch001",
            GamMoney    = 500,
            EarnedMoney = 200,
            GamMoneyU   = 0
        };
        _playerRepoMock.Setup(r => r.GetEarnedMoneyAsync("user01"))
            .ReturnsAsync((200, 10));

        var (ctx, sent) = CommandTestHelper.MakeContext(player);
        await cmd.ExecuteAsync(ctx);

        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.Equal("success", dict["result"]);
        Assert.Equal(700L, (long)dict["gammoney"]);
    }

    // シナリオ2: GamMoneyU != 0 → 適用失敗 → Caller のみ返却
    [Fact]
    public async Task Execute_GamMoneyUNotZero_FailureToCallerOnly()
    {
        var cmd    = new ApplyEarnedMoneyCommand(BuildMoneyService());
        var player = new MajakPlayer
        {
            MemberNo    = "user01",
            ChannelId   = "ch001",
            GamMoney    = 500,
            EarnedMoney = 200,
            GamMoneyU   = 50    // 未確定コインあり
        };
        _playerRepoMock.Setup(r => r.GetEarnedMoneyAsync("user01"))
            .ReturnsAsync((200, 10));

        var (ctx, sent) = CommandTestHelper.MakeContext(player);
        await cmd.ExecuteAsync(ctx);

        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.Equal("failure", dict["result"]);
        Assert.Equal(500L, (long)dict["gammoney"]);
    }

    // シナリオ3: EarnedMoney=0 → 適用失敗
    [Fact]
    public async Task Execute_ZeroEarned_Failure()
    {
        var cmd    = new ApplyEarnedMoneyCommand(BuildMoneyService());
        var player = new MajakPlayer
        {
            MemberNo    = "user01",
            ChannelId   = "ch001",
            GamMoney    = 500,
            EarnedMoney = 0
        };
        var (ctx, sent) = CommandTestHelper.MakeContext(player);
        await cmd.ExecuteAsync(ctx);

        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.Equal("failure", dict["result"]);
    }

    // シナリオ4: レスポンス必須フィールド
    [Fact]
    public async Task Execute_ResponseHasAllRequiredFields()
    {
        var cmd    = new ApplyEarnedMoneyCommand(BuildMoneyService());
        var player = new MajakPlayer
        {
            MemberNo    = "user01",
            ChannelId   = "ch001",
            GamMoney    = 500,
            EarnedMoney = 0
        };
        var (ctx, sent) = CommandTestHelper.MakeContext(player);
        await cmd.ExecuteAsync(ctx);

        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.True(dict.ContainsKey("memberNo"));
        Assert.True(dict.ContainsKey("result"));
        Assert.True(dict.ContainsKey("rating"));
        Assert.True(dict.ContainsKey("slevel"));
        Assert.True(dict.ContainsKey("nlevel"));
        Assert.True(dict.ContainsKey("gammoney"));
    }
}

