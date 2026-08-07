using Moq;
using Microsoft.AspNetCore.SignalR;
using MajakServer.Commands.Channel;
using MajakServer.Hubs;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Services;
using System.Text.Json;

namespace MajakServer.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// YakumanBonusCommand テスト (mjkc23e)
// 原典: commandYakumanBonus はサーバー→クライアント送信のみ
//   AddYakumanBonus() から S→C でブロードキャスト — C→S ハンドラは存在しない
// .NET 実装: スタブ (Task.CompletedTask のみ) — 受信しても何もしない
// ═══════════════════════════════════════════════════════════════════════════
public class YakumanBonusCommandTests
{
    // シナリオ1: 通常プレイヤー → 何も送らない (スタブ)
    [Fact]
    public async Task Execute_AnyPlayer_NothingSent()
    {
        var player = new MajakPlayer { MemberNo = "u1" };
        var cmd    = new YakumanBonusCommand();
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }

    // シナリオ2: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new YakumanBonusCommand();
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }

    // シナリオ3: 正常に完了すること (例外を投げない)
    [Fact]
    public async Task Execute_CompletesWithoutException()
    {
        var cmd = new YakumanBonusCommand();
        var (ctx, _) = CommandTestHelper.MakeContext(new MajakPlayer { MemberNo = "u1" });

        var ex = await Record.ExceptionAsync(() => cmd.ExecuteAsync(ctx));
        Assert.Null(ex);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// AutoEnterRoomCommand テスト (mjkc6e)
// 原典: ProcessCommand_RoomAutoEnterRoom (HMajRoomServer)
//   → FindReservePlayer で予約チェック → 入室 → AutoStart (全員揃った場合)
// ═══════════════════════════════════════════════════════════════════════════
public class AutoEnterRoomCommandTests
{
    private readonly PlayerSessionService _session = new();

    // IHubContext mock ヘルパー
    private static (Mock<IHubContext<MajakGameHub>> hubCtx,
                    List<(string method, object packet)> hubSent)
        BuildHubMock()
    {
        var sent        = new List<(string, object)>();
        var proxyMock   = new Mock<IClientProxy>();
        proxyMock.Setup(c => c.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((m, args, _) =>
                sent.Add((m, args[0]!)))
            .Returns(Task.CompletedTask);

        var clientsMock = new Mock<IHubClients>();
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(proxyMock.Object);
        clientsMock.Setup(c => c.GroupExcept(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
            .Returns(proxyMock.Object);

        var hubCtxMock = new Mock<IHubContext<MajakGameHub>>();
        hubCtxMock.Setup(h => h.Clients).Returns(clientsMock.Object);
        hubCtxMock.Setup(h => h.Groups).Returns(new Mock<IGroupManager>().Object);

        return (hubCtxMock, sent);
    }

    // ─── 異常系 ───────────────────────────────────────────────────────────

    // シナリオ1: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var (hub, _)    = BuildHubMock();
        var cmd         = new AutoEnterRoomCommand(_session, hub.Object,
            new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);

        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    // シナリオ2: roomId 未指定 → ConnectTypeError
    [Fact]
    public async Task Execute_MissingRoomId_SendsConnectTypeError()
    {
        var (hub, _)    = BuildHubMock();
        var player      = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        _session.Register(player);

        var cmd         = new AutoEnterRoomCommand(_session, hub.Object,
            new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player); // roomId なし

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.ConnectTypeError, sent[0].method);
    }

    // シナリオ3: memberNo が自分と一致しない → ConnectTypeError (セキュリティチェック)
    [Fact]
    public async Task Execute_MemberNoMismatch_SendsConnectTypeError()
    {
        var (hub, _) = BuildHubMock();
        var player   = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        var owner    = new MajakPlayer { ConnectionId = "c2", MemberNo = "owner", ChannelId = "ch1" };
        _session.Register(player);
        _session.Register(owner);
        var room = _session.CreateRoom("ch1", owner, "", 1, 0, 0, false);

        var cmd         = new AutoEnterRoomCommand(_session, hub.Object,
            new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [GKey.RoomId]   = room.RoomId,
                ["memberNo"]    = "DIFFERENT_ID",  // 一致しない
                ["connectFor"]  = "GameJoin",
                ["playerType"]  = "v4e",
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.ConnectTypeError, sent[0].method);
    }

    // シナリオ4: 存在しないルーム → ConnectTypeError
    [Fact]
    public async Task Execute_RoomNotFound_SendsConnectTypeError()
    {
        var (hub, _) = BuildHubMock();
        var player   = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        _session.Register(player);

        var cmd         = new AutoEnterRoomCommand(_session, hub.Object,
            new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [GKey.RoomId] = 9999,
                ["connectFor"] = "GameJoin",
                ["playerType"] = "v4e",
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.ConnectTypeError, sent[0].method);
    }

    // シナリオ5: PendingMatch なし + 非ビューアー → ConnectTypeError
    // 原典: FindReservePlayer(pPlayer) が FALSE → E_MAJ_AUTOENTEROOM_FAILED
    [Fact]
    public async Task Execute_NotInPendingList_SendsConnectTypeError()
    {
        var (hub, _) = BuildHubMock();
        var owner    = new MajakPlayer { ConnectionId = "c2", MemberNo = "owner", ChannelId = "ch1" };
        _session.Register(owner);
        var room = _session.CreateRoom("ch1", owner, "", 1, 0, 0, false);

        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        _session.Register(player);

        // PendingMatch は登録するが ExpectedMembers に u1 は入れない
        _session.RegisterPendingMatch(new PendingAutoMatch
        {
            RoomId          = room.RoomId,
            ChannelId       = "ch1",
            ExpectedMembers = new[] { "other1", "other2", "other3", "other4" },
            Players         = new[] { owner },
        });

        var cmd         = new AutoEnterRoomCommand(_session, hub.Object,
            new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [GKey.RoomId]  = room.RoomId,
                ["memberNo"]   = "u1",
                ["connectFor"] = "GameJoin",
                ["playerType"] = "v4e",
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.ConnectTypeError, sent[0].method);
    }

    // ─── 正常系 ───────────────────────────────────────────────────────────

    // シナリオ6: 予約あり + 正常入室 → result=1 + AutoEnterRoom
    [Fact]
    public async Task Execute_ValidEntry_SendsAutoEnterRoom()
    {
        var (hub, hubSent) = BuildHubMock();
        var owner    = new MajakPlayer { ConnectionId = "c2", MemberNo = "owner", ChannelId = "ch1" };
        _session.Register(owner);
        var room = _session.CreateRoom("ch1", owner, "", 1, 0, 0, false);

        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        _session.Register(player);

        _session.RegisterPendingMatch(new PendingAutoMatch
        {
            RoomId          = room.RoomId,
            ChannelId       = "ch1",
            ExpectedMembers = new[] { "u1", "u2", "u3", "u4" },
            Players         = new[] { owner, player },
        });

        var cmd         = new AutoEnterRoomCommand(_session, hub.Object,
            new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [GKey.RoomId]  = room.RoomId,
                ["memberNo"]   = "u1",
                ["connectFor"] = "GameJoin",
                ["playerType"] = "v4e",
            });

        await cmd.ExecuteAsync(ctx);

        // Caller: AutoEnterRoom result=1
        Assert.Contains(sent, s => s.method == Cmd.AutoEnterRoom);
        var pkt = CommandTestHelper.ToDict(sent.First(s => s.method == Cmd.AutoEnterRoom).packet);
        Assert.Equal(1, ((JsonElement)pkt["result"]!).GetInt32());
        Assert.Equal(room.RoomId, ((JsonElement)pkt["roomId"]!).GetInt32());
        Assert.Contains(hubSent, s => s.method == Cmd.AddMember);
        Mock.Get(hub.Object.Clients).Verify(
            clients => clients.Group($"room_{room.RoomId}"),
            Times.Once);
    }

    [Fact]
    public async Task Execute_LegacyKeyedGameJoin_SendsAutoEnterRoom()
    {
        var (hub, _) = BuildHubMock();
        var owner = new MajakPlayer { ConnectionId = "c2", MemberNo = "owner", ChannelId = "ch1" };
        _session.Register(owner);
        var room = _session.CreateRoom("ch1", owner, "", 1, 0, 0, false);

        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        _session.Register(player);

        _session.RegisterPendingMatch(new PendingAutoMatch
        {
            RoomId = room.RoomId,
            ChannelId = "ch1",
            ExpectedMembers = new[] { "u1", "u2", "u3", "u4" },
            Players = new[] { owner, player },
        });

        var cmd = new AutoEnterRoomCommand(_session, hub.Object,
            new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [GKey.RoomId] = room.RoomId,
                [GKey.Pix] = "u1",
                [GKey.ConnectFor] = GKey.ValueConnectForGameJoin,
                [GKey.PlayerType] = GKey.ValuePlayer,
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Contains(sent, s => s.method == Cmd.AutoEnterRoom);
    }

    [Fact]
    public async Task Execute_InvalidConnectFor_SendsConnectTypeError()
    {
        var (hub, _) = BuildHubMock();
        var owner = new MajakPlayer { ConnectionId = "c2", MemberNo = "owner", ChannelId = "ch1" };
        _session.Register(owner);
        var room = _session.CreateRoom("ch1", owner, "", 1, 0, 0, false);

        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        _session.Register(player);

        _session.RegisterPendingMatch(new PendingAutoMatch
        {
            RoomId = room.RoomId,
            ChannelId = "ch1",
            ExpectedMembers = new[] { "u1", "u2", "u3", "u4" },
            Players = new[] { owner, player },
        });

        var cmd = new AutoEnterRoomCommand(_session, hub.Object,
            new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [GKey.RoomId] = room.RoomId,
                [GKey.Pix] = "u1",
                [GKey.ConnectFor] = "invalid",
                [GKey.PlayerType] = GKey.ValuePlayer,
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.ConnectTypeError, sent[0].method);
    }

    [Fact]
    public async Task Execute_FirstReservedPlayer_AppliesAutoCreateRoomPayload()
    {
        var (hub, _) = BuildHubMock();
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        _session.Register(player);
        var room = _session.CreateReservedRoom("ch1", "", 1, 0, long.MaxValue, false);

        _session.RegisterPendingMatch(new PendingAutoMatch
        {
            RoomId = room.RoomId,
            ChannelId = "ch1",
            ExpectedMembers = new[] { "u1", "u2", "u3", "u4" },
            Players = new[] { player },
        });

        var cmd = new AutoEnterRoomCommand(_session, hub.Object,
            new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [GKey.RoomId] = room.RoomId,
                [GKey.Pix] = "u1",
                [GKey.ConnectFor] = GKey.ValueConnectForGameJoin,
                [GKey.PlayerType] = GKey.ValuePlayer,
                [GKey.RoomTitle] = "auto room",
                [GKey.RoomPwd] = "pw",
                [GKey.RoomOption] = "120000001000000",
                [GKey.RoomLimitCnt] = 3,
                [GKey.RoomMinCnt] = 2,
                ["roomType"] = "private",
                ["unitMoney"] = 250L,
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Contains(sent, s => s.method == Cmd.AutoEnterRoom);
        Assert.Equal("u1", room.CreatorNo);
        Assert.Equal("auto room", room.RoomTitle);
        Assert.Equal("pw", room.Password);
        Assert.Equal("120000001000000", room.RoomOption);
        Assert.Equal(3, room.LimitCnt);
        Assert.Equal(2, room.MinCnt);
        Assert.Equal("private", room.RoomType);
        Assert.Equal(250, room.UnitMoney);
        Assert.Same(player, room.Seats[0]);
        Assert.Equal(new[] { "u2", "u3", "u4" }, player.PreMatchMemberNos);
    }

    [Fact]
    public async Task Execute_GameJoinInvalidPassword_SendsInvalidPassword()
    {
        var (hub, _) = BuildHubMock();
        var owner = new MajakPlayer { ConnectionId = "c2", MemberNo = "owner", ChannelId = "ch1" };
        _session.Register(owner);
        var room = _session.CreateRoom("ch1", owner, "", 1, 0, long.MaxValue, false,
            roomPassword: "secret");

        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        _session.Register(player);

        _session.RegisterPendingMatch(new PendingAutoMatch
        {
            RoomId = room.RoomId,
            ChannelId = "ch1",
            ExpectedMembers = new[] { "u1", "u2", "u3", "u4" },
            Players = new[] { owner, player },
        });

        var cmd = new AutoEnterRoomCommand(_session, hub.Object,
            new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [GKey.RoomId] = room.RoomId,
                [GKey.Pix] = "u1",
                [GKey.ConnectFor] = GKey.ValueConnectForGameJoin,
                [GKey.PlayerType] = GKey.ValuePlayer,
                [GKey.RoomPwd] = "wrong",
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.ConnectTypeError, sent[0].method);
        var packet = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(LegacyErrorCode.InvalidPassword, ((JsonElement)packet[GKey.FailCode]!).GetInt32());
        Assert.Contains("u1", _session.GetPendingMatch(room.RoomId)!.RemovedMembers);
    }

    [Fact]
    public async Task Execute_GameJoinerStoresLegacyPreMatchMemberNos()
    {
        var (hub, _) = BuildHubMock();
        var players = Enumerable.Range(1, 4).Select(i =>
        {
            var p = new MajakPlayer { ConnectionId = $"c{i}", MemberNo = $"u{i}", ChannelId = "ch1" };
            _session.Register(p);
            return p;
        }).ToArray();
        var room = _session.CreateReservedRoom("ch1", "120000001000000", 1, 0, long.MaxValue, false);
        room.CreatorNo = "u1";
        _session.JoinRoom(room.RoomId, players[0]);
        _session.RegisterPendingMatch(new PendingAutoMatch
        {
            RoomId = room.RoomId,
            ChannelId = "ch1",
            ExpectedMembers = players.Select(p => p.MemberNo).ToArray(),
            Players = players,
        });

        var cmd = new AutoEnterRoomCommand(_session, hub.Object, new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(players[1],
            new Dictionary<string, object?>
            {
                [GKey.RoomId] = room.RoomId,
                [GKey.Pix] = "u2",
                [GKey.ConnectFor] = GKey.ValueConnectForGameJoin,
                [GKey.PlayerType] = GKey.ValuePlayer,
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Contains(sent, s => s.method == Cmd.AutoEnterRoom);
        Assert.Equal(new[] { "u1", "u3", "u4" }, players[1].PreMatchMemberNos);
    }

    [Fact]
    public async Task Execute_LegacyViewPlayerTypeViewer_AddsViewer()
    {
        var (hub, _) = BuildHubMock();
        var owner = new MajakPlayer { ConnectionId = "c2", MemberNo = "owner", ChannelId = "ch1" };
        _session.Register(owner);
        var room = _session.CreateRoom("ch1", owner, "", 1, 0, long.MaxValue, false);
        room.State = GameRoomState.Playing;

        var viewer = new MajakPlayer { ConnectionId = "c1", MemberNo = "viewer1", ChannelId = "ch1" };
        _session.Register(viewer);

        var cmd = new AutoEnterRoomCommand(_session, hub.Object,
            new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(viewer,
            new Dictionary<string, object?>
            {
                [GKey.RoomId] = room.RoomId,
                [GKey.Pix] = "viewer1",
                [GKey.ConnectFor] = GKey.ValueConnectForView,
                [GKey.PlayerType] = GKey.ValueViewer,
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Contains(sent, s => s.method == Cmd.AutoEnterRoom);
        Assert.Contains(room.Viewers, v => v.MemberNo == "viewer1");
    }

    [Fact]
    public async Task Execute_WithLiveContinueRoom_DeniesViewingOtherRoom()
    {
        var (hub, _) = BuildHubMock();
        var owner = new MajakPlayer { ConnectionId = "c2", MemberNo = "owner", ChannelId = "ch1" };
        _session.Register(owner);
        var targetRoom = _session.CreateRoom("ch1", owner, "", 1, 0, long.MaxValue, false);
        targetRoom.State = GameRoomState.Playing;

        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        _session.Register(player);

        var registry = new RoomRegistryService(TestMasterCacheFactory.CreateRedisService());
        var continueRoom = new GameRoom
        {
            RoomId = targetRoom.RoomId + 1,
            ChannelId = "ch1",
            RoomTitle = "continue room",
            RoomOption = "120000001000000",
            State = GameRoomState.Playing,
        };
        var continuedPlayer = new MajakPlayer { MemberNo = player.MemberNo };
        continueRoom.AddPlayer(continuedPlayer, 0);
        continuedPlayer.IsOutPlayer = true;
        await registry.RegisterRoomAsync(continueRoom.RoomId, continueRoom.ChannelId, continueRoom.RoomTitle,
            isPrivate: false, memberCnt: 0, memberMax: 4,
            serverUrl: "http://test", roomOption: continueRoom.RoomOption);
        await registry.SetContinueRoomAsync(player.MemberNo, continueRoom);

        var cmd = new AutoEnterRoomCommand(_session, hub.Object, new FakeGameLogicService(), registry);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [GKey.RoomId] = targetRoom.RoomId,
                [GKey.Pix] = "u1",
                [GKey.ConnectFor] = GKey.ValueConnectForView,
                [GKey.PlayerType] = GKey.ValueViewer,
            });

        await cmd.ExecuteAsync(ctx);

        Assert.DoesNotContain(targetRoom.Viewers, v => v.MemberNo == "u1");
        Assert.Contains(sent, s => s.method == Cmd.ConnectTypeError);
        Assert.DoesNotContain(sent, s => s.method == Cmd.AutoEnterRoom);
    }

    [Fact]
    public async Task Execute_ViewWithPlayerTypePlayer_SendsCannotEnterRoom()
    {
        var (hub, _) = BuildHubMock();
        var owner = new MajakPlayer { ConnectionId = "c2", MemberNo = "owner", ChannelId = "ch1" };
        _session.Register(owner);
        var room = _session.CreateRoom("ch1", owner, "", 1, 0, long.MaxValue, false);
        room.State = GameRoomState.Playing;

        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        _session.Register(player);

        var cmd = new AutoEnterRoomCommand(_session, hub.Object,
            new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [GKey.RoomId] = room.RoomId,
                [GKey.Pix] = "u1",
                [GKey.ConnectFor] = GKey.ValueConnectForView,
                [GKey.PlayerType] = GKey.ValuePlayer,
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.ConnectTypeError, sent[0].method);
        var packet = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(LegacyErrorCode.CannotEnterRoom, ((JsonElement)packet[GKey.FailCode]!).GetInt32());
    }

    [Fact]
    public async Task Execute_ReconnectPlayingRoomWithoutHistory_StillSendsPaiInfo()
    {
        var (hub, _) = BuildHubMock();
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1", EngineOrder = 0 };
        _session.Register(player);
        var room = _session.CreateRoom("ch1", player, "", 1, 0, 0, false);
        room.Engine.InitHanchan(new MajakServer.Engine.RuleInfo { Kuitan = true, Contest = 0 });
        room.State = GameRoomState.Playing;
        player.EngineOrder = 0;

        var cmd = new AutoEnterRoomCommand(_session, hub.Object, new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [GKey.RoomId] = room.RoomId,
                ["memberNo"] = "u1",
                ["connectFor"] = "GameJoin",
                ["playerType"] = "v4e",
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Contains(sent, s => s.method == Cmd.AutoEnterRoom);
        Assert.Contains(sent, s => s.method == Cmd.PaiInfoList);
        Assert.DoesNotContain(sent, s => s.method == Cmd.History);
    }

    [Fact]
    public async Task Execute_ReconnectsOutPlayerToPlayingAutoMatchRoomAndSendsState()
    {
        var (hub, _) = BuildHubMock();
        var disconnectedPlayer = new MajakPlayer { ConnectionId = "old-connection", MemberNo = "u1", ChannelId = "ch1", EngineOrder = 0 };
        _session.Register(disconnectedPlayer);
        var room = _session.CreateRoom("ch1", disconnectedPlayer, "", 1, 0, 0, false);
        room.Engine.InitHanchan(new MajakServer.Engine.RuleInfo { Kuitan = true, Contest = 0 });
        room.State = GameRoomState.Playing;
        room.PlayHistory.Add(new { test = "resume" });
        disconnectedPlayer.EngineOrder = 0;
        disconnectedPlayer.ConnectionId = "";
        disconnectedPlayer.IsOutPlayer = true;

        var reconnectPlayer = new MajakPlayer { ConnectionId = "new-connection", MemberNo = "u1", ChannelId = "ch1" };
        _session.Register(reconnectPlayer);
        var cmd = new AutoEnterRoomCommand(_session, hub.Object, new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(reconnectPlayer,
            new Dictionary<string, object?>
            {
                [GKey.RoomId] = room.RoomId,
                [GKey.Pix] = "u1",
                [GKey.ConnectFor] = GKey.ValueConnectForGameJoin,
                [GKey.PlayerType] = GKey.ValuePlayer,
            });

        await cmd.ExecuteAsync(ctx);

        Assert.False(room.Seats[0]!.IsOutPlayer);
        Assert.Equal("new-connection", room.Seats[0]!.ConnectionId);
        Assert.Equal(room.RoomId, reconnectPlayer.RoomId);
        Assert.Contains(sent, s => s.method == Cmd.AutoEnterRoom);
        var autoEnterPacket = CommandTestHelper.ToDict(sent.First(s => s.method == Cmd.AutoEnterRoom).packet);
        Assert.Equal((int)GameRoomState.Playing, ((JsonElement)autoEnterPacket["state"]!).GetInt32());
        Assert.Contains(sent, s => s.method == Cmd.PaiInfoList);
        Assert.Contains(sent, s => s.method == Cmd.History);
        Assert.DoesNotContain(sent, s => s.method == Cmd.ConnectTypeError);
    }

    [Fact]
    public async Task Execute_RebindsActivePlayingSeatToNewConnectionAndSendsState()
    {
        var (hub, _) = BuildHubMock();
        var oldPlayer = new MajakPlayer { ConnectionId = "old-connection", MemberNo = "u1", ChannelId = "ch1" };
        _session.Register(oldPlayer);
        var room = _session.CreateRoom("ch1", oldPlayer, "", 1, 0, 0, false);
        room.Engine.InitHanchan(new MajakServer.Engine.RuleInfo { Kuitan = true, Contest = 0 });
        room.State = GameRoomState.Playing;
        oldPlayer.EngineOrder = 0;

        var reconnectPlayer = new MajakPlayer { ConnectionId = "new-connection", MemberNo = "u1", ChannelId = "ch1" };
        _session.Register(reconnectPlayer);
        var cmd = new AutoEnterRoomCommand(_session, hub.Object, new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(reconnectPlayer,
            new Dictionary<string, object?>
            {
                [GKey.RoomId] = room.RoomId,
                [GKey.Pix] = "u1",
                [GKey.ConnectFor] = GKey.ValueConnectForGameJoin,
                [GKey.PlayerType] = GKey.ValuePlayer,
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Equal("new-connection", room.Seats[0]!.ConnectionId);
        Assert.False(room.Seats[0]!.IsOutPlayer);
        Assert.Equal(room.RoomId, reconnectPlayer.RoomId);
        Assert.Contains(sent, packet => packet.method == Cmd.AutoEnterRoom);
        Assert.Contains(sent, packet => packet.method == Cmd.PaiInfoList);
        Assert.DoesNotContain(sent, packet => packet.method == Cmd.ConnectTypeError);
    }

    // シナリオ7: 4人全員揃った → AutoStart ブロードキャスト
    [Fact]
    public async Task Execute_AllPlayersEntered_SendsAutoStart()
    {
        var (hub, hubSent) = BuildHubMock();

        // 4人プレイヤー作成
        var players = Enumerable.Range(1, 4).Select(i =>
        {
            var p = new MajakPlayer { ConnectionId = $"c{i}", MemberNo = $"u{i}", ChannelId = "ch1" };
            _session.Register(p);
            return p;
        }).ToList();

        var room = _session.CreateRoom("ch1", players[0], "00000000000000", 1, 0, 0, false);
        for (int i = 1; i < 4; i++) _session.JoinRoom(room.RoomId, players[i]);

        _session.RegisterPendingMatch(new PendingAutoMatch
        {
            RoomId          = room.RoomId,
            ChannelId       = "ch1",
            ExpectedMembers = players.Select(p => p.MemberNo).ToArray(),
            Players         = players,
            RoomOption      = "00000000000000",
        });

        // 3人先行入室
        for (int i = 0; i < 3; i++)
        {
            var (hub2, _) = BuildHubMock();
            var cmd2 = new AutoEnterRoomCommand(_session, hub2.Object,
                new FakeGameLogicService());
            var (ctx2, _) = CommandTestHelper.MakeContext(players[i],
                new Dictionary<string, object?>
                {
                    [GKey.RoomId]  = room.RoomId,
                    ["memberNo"]   = players[i].MemberNo,
                    ["connectFor"] = "GameJoin",
                    ["playerType"] = "v4e",
                });
            await cmd2.ExecuteAsync(ctx2);
        }

        // 4人目が入室 → AutoStart トリガー
        var cmd4     = new AutoEnterRoomCommand(_session, hub.Object,
            new FakeGameLogicService());
        var (ctx4, sent4) = CommandTestHelper.MakeContext(players[3],
            new Dictionary<string, object?>
            {
                [GKey.RoomId]  = room.RoomId,
                ["memberNo"]   = players[3].MemberNo,
                ["connectFor"] = "GameJoin",
                ["playerType"] = "v4e",
            });

        await cmd4.ExecuteAsync(ctx4);

        // Caller: AutoEnterRoom (result=1) + ctx.Clients.Group: AutoStart (全員へ)
        // FakeGameLogicService は ctx.Clients.Group を経由して送信するため sent4 に入る
        Assert.Contains(sent4, s => s.method == Cmd.AutoEnterRoom);
        Assert.Contains(sent4, s => s.method == Cmd.AutoStart);
        Assert.Null(_session.GetPendingMatch(room.RoomId));
    }
}

/// <summary>
/// テスト用 GameLogicService スタブ。
/// StartGameLogicAsync のみ AutoStart を Clients.Group へ送信して正常終了する。
/// </summary>
internal class FakeGameLogicService : GameLogicService
{
    public FakeGameLogicService() : base(
        new PlayerSessionService(), null!, null!, null!, null!, null!, null!, null!, null!,
        new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()) { }

    public override async Task StartGameLogicAsync(
        MajakServer.Models.Game.GameRoom room,
        MajakServer.Commands.CommandContext ctx)
    {
        room.State = MajakServer.Models.Game.GameRoomState.Playing;
        await ctx.Clients.Group($"room_{room.RoomId}")
            .SendAsync(MajakServer.Models.Protocol.Cmd.AutoStart, new { result = 1 });
    }
}
