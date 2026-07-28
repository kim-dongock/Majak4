using Moq;
using Microsoft.Extensions.Logging;
using MajakServer.Commands.Channel;
using MajakServer.Infrastructure;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;
using System.Text.Json;

namespace MajakServer.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// mjkc1e GetDetailRecCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   1. targetId 未指定 → 自分の戦績を返す
///   2. targetId 指定 (チャンネル内) → 対象プレイヤーの戦績を返す
///   3. targetId 指定 (未発見) → 何も送らない
///   4. player=null → 何も送らない
///   5. 応答に必須フィールドが揃っているか
/// </summary>
public class GetDetailRecCommandTests
{
    private readonly PlayerSessionService _session   = new();
    private readonly Mock<PlayerRepository> _repoMock = new(MockBehavior.Loose);
    private TitleService BuildTitleService()
    {
        // 称号マスターを手動注入
        var svc = new TitleService(_repoMock.Object, TestMasterCacheFactory.Create(playerRepo: _repoMock.Object));
        typeof(TitleService)
            .GetField("_titleCache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(svc, new Dictionary<string, string>
            {
                ["mjkt001"] = "見習い",
                ["mjks001"] = "トリック初心者",
            });
        return svc;
    }

    private MajakPlayer MakePlayer(string id) => new MajakPlayer
    {
        MemberNo      = id,
        AvatarId      = "av01",
        TrickTitle    = "mjks001",
        MajakTitle    = "mjkt001",
        RegularRecord = new RatingRecord(),
        HiClassRecord = new RatingRecord(),
        GradeRecord   = new RatingRecord(),
    };

    // シナリオ1: targetId 未指定 → 原典は lpData なしで何も送らない
    [Fact]
    public async Task Execute_NoTargetId_ReturnsSelfRecord()
    {
        var player = MakePlayer("user01");
        var cmd = new GetDetailRecCommand(_session, BuildTitleService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }

    // シナリオ2: チャンネル内の他プレイヤーを指定
    [Fact]
    public async Task Execute_ValidTargetId_ReturnsTargetRecord()
    {
        var me     = MakePlayer("user01");
        var target = MakePlayer("user02");
        me.ChannelId     = "ch1";
        target.ChannelId = "ch1";
        target.AvatarId  = "av02";
        target.RegularRecord.Rating = 1500;
        target.RegularRecord.MatchCnt = 12;
        target.HiClassRecord.HoraCnt = 7;
        target.GradeRecord.Grade = 9;
        target.GradeRecord.GradePoint = 345;
        me.ConnectionId     = "conn1";
        target.ConnectionId = "conn2";
        _session.Register(me);
        _session.Register(target);

        var cmd = new GetDetailRecCommand(_session, BuildTitleService());
        var (ctx, sent) = CommandTestHelper.MakeContext(me,
            new Dictionary<string, object?> { ["pix"] = "user02" });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(target.Pix, ((System.Text.Json.JsonElement)pkt["memberNo"]!).GetString());
        Assert.NotEqual(target.MemberNo, ((System.Text.Json.JsonElement)pkt["memberNo"]!).GetString());
        var regular = (JsonElement)pkt["regular"]!;
        var hiClass = (JsonElement)pkt["hiClass"]!;
        var gradeMode = (JsonElement)pkt["gradeMode"]!;
        Assert.Equal(1500, regular.GetProperty("rating").GetInt32());
        Assert.Equal(12, regular.GetProperty("matchCnt").GetInt32());
        Assert.Equal(7, hiClass.GetProperty("horaCnt").GetInt32());
        Assert.Equal(9, gradeMode.GetProperty("grade").GetInt32());
        Assert.Equal(345, ((JsonElement)pkt["gradePoint"]!).GetInt32());
    }

    [Fact]
    public async Task Execute_LegacyMemberNoKey_ReturnsTargetRecord()
    {
        var me     = MakePlayer("user01");
        var target = MakePlayer("user02");
        me.ChannelId     = "ch1";
        target.ChannelId = "ch1";
        me.ConnectionId     = "conn1";
        target.ConnectionId = "conn2";
        _session.Register(me);
        _session.Register(target);

        var cmd = new GetDetailRecCommand(_session, BuildTitleService());
        var (ctx, sent) = CommandTestHelper.MakeContext(me,
            new Dictionary<string, object?> { [GKey.Pix] = "user02" });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(target.Pix, ((JsonElement)pkt["memberNo"]!).GetString());
        Assert.NotEqual(target.MemberNo, ((JsonElement)pkt["memberNo"]!).GetString());
    }

    // シナリオ3: 存在しない targetId → 何も送らない
    [Fact]
    public async Task Execute_UnknownTargetId_NothingSent()
    {
        var player = MakePlayer("user01");
        player.ChannelId    = "ch1";
        player.ConnectionId = "conn1";
        _session.Register(player);

        var cmd = new GetDetailRecCommand(_session, BuildTitleService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { ["pix"] = "ghost99" });

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }

    // シナリオ4: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new GetDetailRecCommand(_session, BuildTitleService());
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    // シナリオ5: 応答に regular / hiClass / gradeMode フィールドが存在する
    [Fact]
    public async Task Execute_ResponseContainsRecordFields()
    {
        var player = MakePlayer("user01");
        player.ChannelId = "ch1";
        player.ConnectionId = "conn1";
        _session.Register(player);
        var cmd = new GetDetailRecCommand(_session, BuildTitleService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { ["pix"] = "user01" });

        await cmd.ExecuteAsync(ctx);

        var pktType = sent[0].packet.GetType();
        Assert.NotNull(pktType.GetProperty("regular"));
        Assert.NotNull(pktType.GetProperty("hiClass"));
        Assert.NotNull(pktType.GetProperty("gradeMode"));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc2e AutoMatchingCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   1. コインあり → キューに追加 → result=1
///   2. コイン=0 → 拒否 → result=0 + failCode
///   3. player=null → 何も送らない
/// </summary>
public class AutoMatchingCommandTests
{
    private readonly PlayerSessionService _session = new();

    // シナリオ1: コインあり → マッチング受理
    [Fact]
    public async Task Execute_AutoMatchingChannel_HasMoney_EnqueuesWithoutResponse()
    {
        const string channelId = "AZ";
        var cmd = new AutoMatchingCommand(_session, new MajakServer.Services.RatingService());
        var sentAll = new List<(string method, object packet)>();

        for (int i = 1; i <= 4; i++)
        {
            var player = new MajakPlayer { MemberNo = $"user0{i}", ChannelId = channelId, GamMoney = 1000 };
            var (ctx, sent) = CommandTestHelper.MakeContext(player);
            await cmd.ExecuteAsync(ctx);
            sentAll.AddRange(sent);
        }

        Assert.Empty(sentAll);
        Assert.Equal(new[] { "user01", "user02", "user03", "user04" },
            _session.TryMatch(channelId, _ => 1500));
    }

    [Fact]
    public async Task Execute_NonAutoMatchingChannel_DoesNotEnqueue()
    {
        const string channelId = "AA";
        var cmd = new AutoMatchingCommand(_session, new MajakServer.Services.RatingService());

        for (int i = 1; i <= 4; i++)
        {
            var player = new MajakPlayer { MemberNo = $"user0{i}", ChannelId = channelId, GamMoney = 1000 };
            var (ctx, _) = CommandTestHelper.MakeContext(player);
            await cmd.ExecuteAsync(ctx);
        }

        Assert.Null(_session.TryMatch(channelId, _ => 1500));
    }

    // シナリオ2: コイン不足 → 拒否
    [Fact]
    public async Task Execute_NoMoney_RejectsWithFailCode()
    {
        var player = new MajakPlayer { MemberNo = "user01", ChannelId = "AZ", GamMoney = 0 };
        var cmd = new AutoMatchingCommand(_session, new MajakServer.Services.RatingService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(0, CommandTestHelper.GetResult(sent[0].packet));
    }

    [Fact]
    public async Task Execute_BeginnerAutoMatchingWithTooManyMatches_RejectsLikeLegacyLimit()
    {
        var player = new MajakPlayer
        {
            MemberNo = "user01",
            ChannelId = "MAJAK21ZG6A001",
            GamMoney = 1000,
            RegularRecord = { MatchCnt = 11 },
        };
        var cmd = new AutoMatchingCommand(_session, new MajakServer.Services.RatingService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.AutoMatching, sent[0].method);
        Assert.Equal(0, CommandTestHelper.GetResult(sent[0].packet));
        Assert.Null(_session.TryMatch(player.ChannelId, _ => 1500));
    }

    [Fact]
    public async Task Execute_CupPlayLicenseFailure_RejectsWithLegacyFailCode()
    {
        const string channelId = "MAJAK20ZC5F001";
        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        repo.Setup(r => r.GetCupConfigsAsync()).ReturnsAsync(new List<CupConfig>
        {
            new(
                ChannelId: channelId,
                ChannelName: "Cup",
                DateFrom: DateTime.Now.AddDays(-1),
                DateTo: DateTime.Now.AddDays(1),
                IsFestive: false,
                ConditionBilling: 1),
        });
        var cache = TestMasterCacheFactory.Create(playerRepo: repo.Object);
        var player = new MajakPlayer { MemberNo = "user01", ChannelId = channelId, GamMoney = 1000 };
        var cmd = new AutoMatchingCommand(_session, new MajakServer.Services.RatingService(), cache);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.AutoMatching, sent[0].method);
        var packet = CommandTestHelper.AsDict(sent[0].packet);
        Assert.Equal(CupPlayLicense.LicenseBuyItem, Convert.ToInt32(packet[GKey.FailCode]));
        Assert.Null(_session.TryMatch(channelId, _ => 1500));
    }

    [Fact]
    public async Task Execute_CupStatusStopped_RejectsWithLegacyPauseMessageOnly()
    {
        const string channelId = "MAJAK20ZC5F001";
        var repo = new Mock<PlayerRepository>(MockBehavior.Loose);
        repo.Setup(r => r.GetCupStatusAsync(channelId)).ReturnsAsync(2);
        var cache = TestMasterCacheFactory.Create(playerRepo: repo.Object);
        var player = new MajakPlayer { MemberNo = "user01", ChannelId = channelId, GamMoney = 1000 };
        var cmd = new AutoMatchingCommand(_session, new MajakServer.Services.RatingService(), cache, repo.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.AutoMatching, sent[0].method);
        var packet = CommandTestHelper.AsDict(sent[0].packet);
        Assert.Equal(GKey.ValueFailure, packet[GKey.Result]);
        Assert.True(packet.ContainsKey(GKey.Message));
        Assert.False(packet.ContainsKey(GKey.FailCode));
        Assert.Null(_session.TryMatch(channelId, _ => 1500));
    }

    // シナリオ3: player=null
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new AutoMatchingCommand(_session, new MajakServer.Services.RatingService());
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc3e CancelAutoMatchingCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   1. キャンセル成功 → result=1
///   2. player=null → 何も送らない
/// </summary>
public class CancelAutoMatchingCommandTests
{
    private readonly PlayerSessionService _session = new();

    [Fact]
    public async Task Execute_CancelsAndResponds()
    {
        const string channelId = "MAJAK20ZG6A001";
        var player = new MajakPlayer { MemberNo = "user01", ChannelId = channelId };
        for (int i = 1; i <= 4; i++)
        {
            _session.EnqueueMatching(channelId, $"user0{i}");
        }
        var cmd = new CancelAutoMatchingCommand(_session);
        var payload = new Dictionary<string, object?> { [GKey.Pix] = "user01" };
        var (ctx, sent) = CommandTestHelper.MakeContext(player, payload);

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.CancelAutoMatching, sent[0].method);
        Assert.Same(payload, sent[0].packet);
        Assert.Null(_session.TryMatch(channelId, _ => 1500));
    }

    [Fact]
    public async Task Execute_NonAutoMatchingChannel_DoesNotCancelOrRespond()
    {
        const string channelId = "MAJAK20AA6A001";
        var player = new MajakPlayer { MemberNo = "user01", ChannelId = channelId };
        for (int i = 1; i <= 4; i++)
        {
            _session.EnqueueMatching(channelId, $"user0{i}");
        }
        var cmd = new CancelAutoMatchingCommand(_session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
        Assert.Equal(new[] { "user01", "user02", "user03", "user04" },
            _session.TryMatch(channelId, _ => 1500));
    }

    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new CancelAutoMatchingCommand(_session);
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc16e AvatarGearCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   commandAvatarGear は HMajChnlServer::SendAvatarGear からの S→C Push 専用。
///   C→S ハンドラは存在しないため、受信しても何も送らず状態も変更しない。
/// </summary>
public class AvatarGearCommandTests
{
    private readonly Mock<PlayerRepository> _repoMock = new(MockBehavior.Loose);

    [Fact]
    public async Task Execute_WithPlayer_NothingSent()
    {
        var player = new MajakPlayer { MemberNo = "user01", MemorialShop = 0, AvatarId = "av01" };
        var cmd = new AvatarGearCommand(_repoMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }

    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new AvatarGearCommand(_repoMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc19e GetTitleCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   commandGetTitle は HMajPlayer::CheckTitleClear() からの S→C Push 専用。
///   C→S ハンドラは存在しないため、受信しても何も送らず状態も変更しない。
/// </summary>
public class GetTitleCommandTests
{
    private readonly Mock<PlayerRepository> _repoMock = new(MockBehavior.Loose);

    private TitleService BuildTitleService()
    {
        _repoMock.Setup(r => r.InsertOrEnableTitleAsync(
                It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.UpdateCommonRatAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);

        var svc = new TitleService(_repoMock.Object, TestMasterCacheFactory.Create(playerRepo: _repoMock.Object));
        typeof(TitleService)
            .GetField("_titleCache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(svc, new Dictionary<string, string>
            {
                ["mjkt001"] = "初段",
                ["mjks001"] = "トリック初心者",
            });
        return svc;
    }

    // シナリオ1: 有効な称号コードを受けても何も送らない
    [Fact]
    public async Task Execute_ValidTitle_NothingSent()
    {
        var player = new MajakPlayer
        {
            MemberNo   = "user01",
            TrickTitle = "mjks001",
            MajakTitle = "mjkt001",
        };
        var cmd = new GetTitleCommand(BuildTitleService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [Key.TitleType] = 2,
                [Key.TitleCode] = "mjkt001",
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }

    // シナリオ2: 無効な称号コードでも何も送らない
    [Fact]
    public async Task Execute_InvalidTitle_NothingSent()
    {
        var player = new MajakPlayer { MemberNo = "user01" };
        var cmd = new GetTitleCommand(BuildTitleService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [Key.TitleType] = 2,
                [Key.TitleCode] = "mjkt999",   // 存在しない
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }

    // シナリオ3: titleType=1 でも TrickTitle は更新しない
    [Fact]
    public async Task Execute_TitleType1_DoesNotUpdateTrickTitle()
    {
        var player = new MajakPlayer { MemberNo = "user01", TrickTitle = "" };
        var cmd = new GetTitleCommand(BuildTitleService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [Key.TitleType] = 1,
                [Key.TitleCode] = "mjks001",
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Equal("", player.TrickTitle);
        Assert.Empty(sent);
    }

    // シナリオ4: titleType=2 でも MajakTitle は更新しない
    [Fact]
    public async Task Execute_TitleType2_DoesNotUpdateMajakTitle()
    {
        var player = new MajakPlayer { MemberNo = "user01", MajakTitle = "" };
        var cmd = new GetTitleCommand(BuildTitleService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [Key.TitleType] = 2,
                [Key.TitleCode] = "mjkt001",
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Equal("", player.MajakTitle);
        Assert.Empty(sent);
    }

    // シナリオ5: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new GetTitleCommand(BuildTitleService());
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    // シナリオ6: 応答フィールドは作らない
    [Fact]
    public async Task Execute_DoesNotBuildResponseFields()
    {
        var player = new MajakPlayer { MemberNo = "user01" };
        var cmd = new GetTitleCommand(BuildTitleService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [Key.TitleType] = 2,
                [Key.TitleCode] = "mjkt999",
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }
}
