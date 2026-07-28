using Moq;
using Microsoft.Extensions.Logging;
using MajakServer.Commands.Channel;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;

namespace MajakServer.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// mjkc22e GetGemCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   commandGetGem は HMajRoomServer::GetGemCountToGet() からの S→C Push 専用。
///   C→S ハンドラは存在しないため、受信しても何も送らない。
/// </summary>
public class GetGemCommandTests
{
    // シナリオ1: 正常プレイヤーでも何も送らない
    [Fact]
    public async Task Execute_Normal_NothingSent()
    {
        var player = new MajakPlayer { MemberNo = "user01", GemCount = 5 };
        var cmd = new GetGemCommand();
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }

    // シナリオ2: GemCount=0 でも何も送らない
    [Fact]
    public async Task Execute_ZeroGem_StillReturnsResult1()
    {
        var player = new MajakPlayer { MemberNo = "user01", GemCount = 0 };
        var cmd = new GetGemCommand();
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }

    // シナリオ3: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new GetGemCommand();
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    // シナリオ4: 互換フィールドも作らない
    [Fact]
    public async Task Execute_CountAndGemCountAreEqual()
    {
        var player = new MajakPlayer { MemberNo = "user01", GemCount = 12 };
        var cmd = new GetGemCommand();
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// room:invite InviteCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   1. チャンネル内メンバーへ招待 → InviteGame S→C 送信
///   2. 招待拒否フラグが立っている相手 → 何も送らない
///   3. 対象メンバーが別チャンネル → 何も送らない
///   4. RoomId=null → 何も送らない (ルーム未入室)
///   5. targetPix が空 → 何も送らない
///   6. player=null → 何も送らない
///   7. 自分のチャンネル外メンバー → 何も送らない
///   8. 送信パケットに必須フィールドが揃っているか
/// </summary>
public class InviteCommandTests
{
    private readonly PlayerSessionService _session = new();

    private (MajakPlayer me, MajakPlayer target) SetupPlayers(
        string channelId   = "ch1",
        bool   rejectInvite = false,
        string targetChannel = "ch1")
    {
        var me = new MajakPlayer
        {
            ConnectionId = "conn1",
            MemberNo     = "me001",
            NickName     = "Me",
            AvatarId     = "avMe",
            ChannelId    = channelId,
            RoomId       = 42,
        };
        var target = new MajakPlayer
        {
            ConnectionId  = "conn2",
            MemberNo      = "tgt001",
            ChannelId     = targetChannel,
            RejectInvite  = rejectInvite,
        };
        _session.Register(me);
        _session.Register(target);
        return (me, target);
    }

    // シナリオ1: 正常招待 → InviteGame 送信
    [Fact]
    public async Task Execute_ValidTarget_SendsInviteGame()
    {
        var (me, target) = SetupPlayers();
        var cmd = new InviteCommand(_session);
        var (ctx, sent) = CommandTestHelper.MakeContext(me,
            new Dictionary<string, object?> { ["targetPix"] = "tgt001" });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.InviteGame, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.True(pkt.ContainsKey(GKey.Pix));
        Assert.True(pkt.ContainsKey(GKey.RoomId));
        Assert.True(pkt.ContainsKey(GKey.InviteGameString));
        Assert.Equal(me.Pix, ((System.Text.Json.JsonElement)pkt[GKey.Pix]!).GetString());
        Assert.NotEqual(me.MemberNo, ((System.Text.Json.JsonElement)pkt[GKey.Pix]!).GetString());
        Assert.Equal(42,      ((System.Text.Json.JsonElement)pkt[GKey.RoomId]!).GetInt32());
    }

    // シナリオ2: 招待拒否フラグ → 何も送らない
    [Fact]
    public async Task Execute_TargetRejectsInvite_NothingSent()
    {
        var (me, _) = SetupPlayers(rejectInvite: true);
        var cmd = new InviteCommand(_session);
        var (ctx, sent) = CommandTestHelper.MakeContext(me,
            new Dictionary<string, object?> { ["targetPix"] = "tgt001" });

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }

    // シナリオ3: 対象が別チャンネル → 何も送らない
    [Fact]
    public async Task Execute_TargetDifferentChannel_NothingSent()
    {
        var (me, _) = SetupPlayers(channelId: "ch1", targetChannel: "ch2");
        var cmd = new InviteCommand(_session);
        var (ctx, sent) = CommandTestHelper.MakeContext(me,
            new Dictionary<string, object?> { ["targetPix"] = "tgt001" });

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }

    // シナリオ4: ルーム未入室 (RoomId=null) → 何も送らない
    [Fact]
    public async Task Execute_NotInRoom_NothingSent()
    {
        var (me, _) = SetupPlayers();
        me.RoomId = null;
        var cmd = new InviteCommand(_session);
        var (ctx, sent) = CommandTestHelper.MakeContext(me,
            new Dictionary<string, object?> { ["targetPix"] = "tgt001" });

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }

    // シナリオ5: targetPix が空 → 何も送らない
    [Fact]
    public async Task Execute_EmptyTargetId_NothingSent()
    {
        var (me, _) = SetupPlayers();
        var cmd = new InviteCommand(_session);
        var (ctx, sent) = CommandTestHelper.MakeContext(me,
            new Dictionary<string, object?> { ["targetPix"] = "" });

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }

    // シナリオ6: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new InviteCommand(_session);
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    // シナリオ7: 存在しない targetPix → 何も送らない
    [Fact]
    public async Task Execute_TargetNotFound_NothingSent()
    {
        var (me, _) = SetupPlayers();
        var cmd = new InviteCommand(_session);
        var (ctx, sent) = CommandTestHelper.MakeContext(me,
            new Dictionary<string, object?> { ["targetPix"] = "ghost999" });

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }

    // シナリオ8: 送信パケットに必須フィールドが全部含まれる
    [Fact]
    public async Task Execute_PacketContainsRequiredFields()
    {
        var (me, _) = SetupPlayers();
        var cmd = new InviteCommand(_session);
        var (ctx, sent) = CommandTestHelper.MakeContext(me,
            new Dictionary<string, object?> { ["targetPix"] = "tgt001" });

        await cmd.ExecuteAsync(ctx);

        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.True(pkt.ContainsKey(GKey.Pix));
        Assert.True(pkt.ContainsKey(GKey.RoomId));
        Assert.True(pkt.ContainsKey(GKey.RoomPwd));
        Assert.True(pkt.ContainsKey(GKey.InviteGameString));
        Assert.True(pkt.ContainsKey(GKey.YesNo));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc37e SetCustomItemCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   1. 所持アイテムを装備設定 → 応答なし
///   2. 未所持アイテムでも原典どおり DB 更新を試み、メモリ装備状態を更新する
///   3. player=null → 何も送らない
/// </summary>
public class SetCustomItemCommandTests
{
    private readonly Mock<ItemRepository>            _itemRepoMock = new(MockBehavior.Loose);

    private ItemService BuildItemService()
    {
        _itemRepoMock.Setup(r => r.SetEquipAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        var svc = new ItemService(_itemRepoMock.Object, TestMasterCacheFactory.Create(itemRepo: _itemRepoMock.Object));
        typeof(ItemService)
            .GetField("_itemMast",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(svc, new Dictionary<int, (int Kind, string Name, long Price)>
            {
                [100001] = (10, "背景板A", 500),
            });
        return svc;
    }

    // シナリオ1: 所持アイテムを装備設定 → 原典はクライアント応答なし
    [Fact]
    public async Task Execute_OwnedItem_NoResponse()
    {
        var svc    = BuildItemService();
        var player = new MajakPlayer { MemberNo = "user01" };
        player.CustomItems[100001] = new UserCustomItem { Kind = 10, Equip = 0 };

        var cmd = new SetCustomItemCommand(svc);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [Key.CustomId] = 100001 });

        await cmd.ExecuteAsync(ctx);

        // 原典 ProcessCommand_SetCustomItem: クライアントへ返答なし (return TRUE のみ)
        Assert.Empty(sent);
        // DB 更新が呼ばれたことを確認
        _itemRepoMock.Verify(r => r.SetEquipAsync("user01", 0, 100001), Times.Once);
        Assert.Equal(1, player.CustomItems[100001].Equip);
    }

    // シナリオ2: 未所持 → 原典は所持チェックなしで SetUserCustomItem を実行し、応答なし
    [Fact]
    public async Task Execute_NotOwned_SilentSet()
    {
        var svc    = BuildItemService();
        var player = new MajakPlayer { MemberNo = "user01" };
        // アイテムを持っていない

        var cmd = new SetCustomItemCommand(svc);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [Key.CustomId] = 100001 });

        await cmd.ExecuteAsync(ctx);

        // 原典 ProcessCommand_SetCustomItem: クライアントへ返答なし (return TRUE のみ)
        Assert.Empty(sent);
        _itemRepoMock.Verify(r => r.SetEquipAsync("user01", 0, 100001), Times.Once);
        Assert.Equal(10, player.CustomItems[100001].Kind);
        Assert.Equal(1, player.CustomItems[100001].Equip);
    }

    // シナリオ3: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new SetCustomItemCommand(BuildItemService());
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}
