using Moq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MajakServer.Commands;
using MajakServer.Commands.Room;
using MajakServer.Commands.Channel;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;
using System.Text.Json;

namespace MajakServer.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// RoomEmoticonCommand テスト (room:emoticon)
// 原典: HMajRoomServer::ProcessCommand_EmoticonCommand
//   → AddValue(keyMemberNo, memberNo) + AddValue(keyEmoticonAvatarId, avatarId)
//   → SendDataToAll (ルーム全員へ)
// ═══════════════════════════════════════════════════════════════════════════
public class RoomEmoticonCommandTests
{
    private PlayerSessionService BuildSessionWithRoom(out MajakPlayer player, int roomId = 10)
    {
        var session = new PlayerSessionService();
        player = new MajakPlayer
        {
            ConnectionId = "conn1",
            MemberNo     = "user01",
            AvatarId     = "avt001",
            ChannelId    = "ch1",
        };
        session.Register(player);
        session.CreateRoom("ch1", player, "", 1, 0, 0, false);
        return session;
    }

    // シナリオ1: ルーム内 → 全員にエモートブロードキャスト
    [Fact]
    public async Task Execute_InRoom_BroadcastsEmoticon()
    {
        var session = BuildSessionWithRoom(out var player);
        var room = session.GetRoom(player.RoomId!.Value)!;
        var other = new MajakPlayer { ConnectionId = "conn2", MemberNo = "user02", AvatarId = "avt002", ChannelId = "ch1" };
        var viewer = new MajakPlayer { ConnectionId = "conn3", MemberNo = "viewer01", AvatarId = "avt003", ChannelId = "ch1", IsViewer = true };
        session.Register(other);
        session.Register(viewer);
        room.AddPlayer(other, 1);
        room.Viewers.Add(viewer);
        var cmd     = new RoomEmoticonCommand(session);
        IReadOnlyList<string>? recipients = null;
        var sent = new List<(string method, object packet)>();
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((method, args, _) => sent.Add((method, args[0]!)))
            .Returns(Task.CompletedTask);
        var clientsMock = new Mock<IHubCallerClients>();
        clientsMock.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>()))
            .Callback<IReadOnlyList<string>>(list => recipients = list)
            .Returns(proxy.Object);
        clientsMock.Setup(c => c.Group(It.IsAny<string>()))
            .Throws(new InvalidOperationException("emoticon should use live room member connections"));
        var ctx = new CommandContext
        {
            ConnectionId = player.ConnectionId,
            Player = player,
            Clients = clientsMock.Object,
            Payload = new Dictionary<string, object?> { [Key.EmoticonId] = 3 },
        };

        await cmd.ExecuteAsync(ctx);

        Assert.Equal(new[] { "conn1", "conn2", "conn3" }, recipients?.OrderBy(x => x).ToArray());
        Assert.Single(sent);
        Assert.Equal(Cmd.UseEmoticon, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(player.Pix, ((JsonElement)pkt[GKey.Pix]!).GetString());
        Assert.NotEqual(player.MemberNo, ((JsonElement)pkt[GKey.Pix]!).GetString());
        Assert.Equal(3,        ((JsonElement)pkt[Key.EmoticonId]!).GetInt32());
        // 原典: rParser.AddValue(MAJ::keyEmoticonAvatarId, pPlayer->m_szAvatarId) — サーバー値
        Assert.Equal("avt001", ((JsonElement)pkt[Key.EmoticonAvatarId]!).GetString());
        Assert.DoesNotContain("memberNo", pkt.Keys);
    }

    // シナリオ2: ルーム未入室 → 何も送らない
    [Fact]
    public async Task Execute_NotInRoom_NothingSent()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", RoomId = null };
        session.Register(player);

        var cmd = new RoomEmoticonCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [Key.EmoticonId] = 1 });

        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    // シナリオ3: ルームが存在しない → 何も送らない
    [Fact]
    public async Task Execute_RoomNotFound_NothingSent()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", RoomId = 999 };
        session.Register(player);

        var cmd = new RoomEmoticonCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [Key.EmoticonId] = 1 });

        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    // シナリオ4: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var session = new PlayerSessionService();
        var cmd = new RoomEmoticonCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// RoomEnterRoomCommand テスト (room:enter)
// 原典: ProcessCommand_RoomEnterRoom
//   待機中: SendOkButtonState → smmc1e
//   進行中: SendPlayHist → history
//   常に: EnterRoomCmd → result=1
// ═══════════════════════════════════════════════════════════════════════════
public class RoomEnterRoomCommandTests
{
    [Fact]
    public async Task Execute_WithLiveContinueRoom_DeniesEnteringOtherRoom()
    {
        var session = new PlayerSessionService();
        var host = new MajakPlayer { ConnectionId = "host", MemberNo = "host", ChannelId = "ch1" };
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(host);
        session.Register(player);
        var targetRoom = session.CreateRoom("ch1", host, "", 1, 0, 0, false, roomId: 102);
        targetRoom.State = GameRoomState.Waiting;

        var registry = new RoomRegistryService(TestMasterCacheFactory.CreateRedisService());
        var continueRoom = new GameRoom
        {
            RoomId = 101,
            ChannelId = "ch1",
            RoomTitle = "continue room",
            RoomOption = "120000001000000",
            State = GameRoomState.Playing,
        };
        await registry.RegisterRoomAsync(continueRoom.RoomId, continueRoom.ChannelId, continueRoom.RoomTitle,
            isPrivate: false, memberCnt: 0, memberMax: 4,
            serverUrl: "http://test", roomOption: continueRoom.RoomOption);
        await registry.SetContinueRoomAsync(player.MemberNo, continueRoom);

        var repoMock = new Mock<PlayerRepository>(MockBehavior.Loose);
        var cmd = new RoomEnterRoomCommand(session, new FakeGameLogicService(), repoMock.Object, registry);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.RoomId] = targetRoom.RoomId });

        await cmd.ExecuteAsync(ctx);

        Assert.DoesNotContain(targetRoom.Seats, seat => seat?.MemberNo == player.MemberNo);
        Assert.Contains(sent, s => s.method == Cmd.ConnectTypeError);
        Assert.DoesNotContain(sent, s => s.method == Cmd.EnterRoomCmd);
    }

    // シナリオ1: 待機状態ルーム → OKボタン状態 + EnterRoomCmd
    [Fact]
    public async Task Execute_WaitingRoom_SendsOkButtonAndEnterRoom()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);
        var room = session.CreateRoom("ch1", player, "", 1, 0, 0, false);
        room.State = GameRoomState.Waiting;

        var cmd = new RoomEnterRoomCommand(session, new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.RoomId] = room.RoomId });

        await cmd.ExecuteAsync(ctx);

        // smmc1e (SendOkButton) + EnterRoomCmd
        Assert.Contains(sent, s => s.method == Cmd.SendOkButton);
        Assert.Contains(sent, s => s.method == Cmd.EnterRoomCmd);
        var enterPkt = CommandTestHelper.ToDict(sent.First(s => s.method == Cmd.EnterRoomCmd).packet);
        Assert.Equal(1, ((JsonElement)enterPkt["result"]!).GetInt32());
    }

    [Fact]
    public async Task Execute_PrivateRoomAlreadyJoined_DoesNotRequirePasswordAgain()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);
        var room = session.CreateRoom("ch1", player, "", 1, 0, 0, true, roomPassword: "secret");
        room.State = GameRoomState.Waiting;

        var cmd = new RoomEnterRoomCommand(session, new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.RoomId] = room.RoomId });

        await cmd.ExecuteAsync(ctx);

        Assert.Contains(sent, s => s.method == Cmd.EnterRoomCmd);
        Assert.DoesNotContain(sent, s => s.method == Cmd.ConnectTypeError);
    }

    [Fact]
    public async Task Execute_WaitingRoom_DoesNotShrinkRoomLimitOnJoin()
    {
        var session = new PlayerSessionService();
        var host    = new MajakPlayer { ConnectionId = "c1", MemberNo = "host", ChannelId = "ch1" };
        var player  = new MajakPlayer { ConnectionId = "c2", MemberNo = "u2", ChannelId = "ch1" };
        session.Register(host);
        session.Register(player);
        var room = session.CreateRoom("ch1", host, "", 1, 0, 0, false);
        room.LimitCnt = 4;
        room.State = GameRoomState.Waiting;

        var cmd = new RoomEnterRoomCommand(session, new FakeGameLogicService());
        var (ctx, _) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.RoomId] = room.RoomId });

        await cmd.ExecuteAsync(ctx);

        Assert.Equal(2, room.PlayerCount);
        Assert.Equal(4, room.LimitCnt);
    }

    // シナリオ2: 進行中ルーム (履歴あり) → history + EnterRoomCmd
    [Fact]
    public async Task Execute_PlayingRoomWithHistory_SendsHistoryAndEnterRoom()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);
        var room = session.CreateRoom("ch1", player, "", 1, 0, 0, false);
        room.State = GameRoomState.Playing;
        room.PlayHistory.Add(new { test = 1 });

        var cmd = new RoomEnterRoomCommand(session, new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.RoomId] = room.RoomId });

        await cmd.ExecuteAsync(ctx);

        Assert.Contains(sent, s => s.method == Cmd.History);
        Assert.Contains(sent, s => s.method == Cmd.EnterRoomCmd);
    }

    [Fact]
    public async Task Execute_PlayingRoomWithHistory_SendsPaiInfoBeforeHistory()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1", EngineOrder = 0 };
        session.Register(player);
        var room = session.CreateRoom("ch1", player, "", 1, 0, 0, false);
        room.Engine.InitHanchan(new MajakServer.Engine.RuleInfo { Kuitan = true, Contest = 0 });
        room.State = GameRoomState.Playing;
        room.PlayHistory.Add(new { test = 1 });

        var cmd = new RoomEnterRoomCommand(session, new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.RoomId] = room.RoomId });

        await cmd.ExecuteAsync(ctx);

        int paiInfoIndex = sent.FindIndex(s => s.method == Cmd.PaiInfoList);
        int historyIndex = sent.FindIndex(s => s.method == Cmd.History);
        Assert.InRange(paiInfoIndex, 0, int.MaxValue);
        Assert.InRange(historyIndex, 0, int.MaxValue);
        Assert.True(paiInfoIndex < historyIndex, $"Expected PaiInfo before History. order={string.Join(',', sent.Select(s => s.method))}");
    }

    [Fact]
    public async Task Execute_PlayingRoomReconnectsOutPlayerAndSendsHistory()
    {
        var session = new PlayerSessionService();
        var oldPlayer = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1", EngineOrder = 0 };
        session.Register(oldPlayer);
        var room = session.CreateRoom("ch1", oldPlayer, "", 1, 0, 0, false);
        room.Engine.InitHanchan(new MajakServer.Engine.RuleInfo { Kuitan = true, Contest = 0 });
        room.State = GameRoomState.Playing;
        room.PlayHistory.Add(new { test = 1 });
        oldPlayer.ConnectionId = "";
        oldPlayer.IsOutPlayer = true;

        var reconnectPlayer = new MajakPlayer { ConnectionId = "c2", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(reconnectPlayer);
        var cmd = new RoomEnterRoomCommand(session, new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(reconnectPlayer,
            new Dictionary<string, object?> { [GKey.RoomId] = room.RoomId });

        await cmd.ExecuteAsync(ctx);

        Assert.False(room.Seats[0]!.IsOutPlayer);
        Assert.Equal("c2", room.Seats[0]!.ConnectionId);
        Assert.Equal(room.RoomId, reconnectPlayer.RoomId);
        Assert.Contains(sent, s => s.method == Cmd.EnterRoomCmd);
        Assert.Contains(sent, s => s.method == Cmd.AddMember);
        Assert.Contains(sent, s => s.method == Cmd.History);
        Assert.DoesNotContain(sent, s => s.method == Cmd.ConnectTypeError);
    }

    [Fact]
    public async Task Execute_PlayingRoomRefreshesOutPlayerAndBroadcastsAddMember()
    {
        var session = new PlayerSessionService();
        var player = new MajakPlayer { ConnectionId = "c2", MemberNo = "u1", ChannelId = "ch1", EngineOrder = 0 };
        session.Register(player);
        var room = session.CreateRoom("ch1", player, "", 1, 0, 0, false);
        room.Engine.InitHanchan(new MajakServer.Engine.RuleInfo { Kuitan = true, Contest = 0 });
        room.State = GameRoomState.Playing;
        room.PlayHistory.Add(new { test = 1 });
        player.IsOutPlayer = true;
        room.Seats[0]!.IsOutPlayer = true;

        var cmd = new RoomEnterRoomCommand(session, new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.RoomId] = room.RoomId });

        await cmd.ExecuteAsync(ctx);

        Assert.False(room.Seats[0]!.IsOutPlayer);
        Assert.Contains(sent, s => s.method == Cmd.AddMember);
        Assert.Contains(sent, s => s.method == Cmd.EnterRoomCmd);
        Assert.Contains(sent, s => s.method == Cmd.History);
        Assert.DoesNotContain(sent, s => s.method == Cmd.ConnectTypeError);
    }

    // シナリオ3: 進行中ルーム (履歴なし) → history なし + EnterRoomCmd のみ
    [Fact]
    public async Task Execute_PlayingRoomNoHistory_SendsOnlyEnterRoom()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);
        var room = session.CreateRoom("ch1", player, "", 1, 0, 0, false);
        room.State = GameRoomState.Playing;
        // PlayHistory は空

        var cmd = new RoomEnterRoomCommand(session, new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.RoomId] = room.RoomId });

        await cmd.ExecuteAsync(ctx);

        Assert.DoesNotContain(sent, s => s.method == Cmd.History);
        Assert.Contains(sent, s => s.method == Cmd.EnterRoomCmd);
    }

    [Fact]
    public async Task Execute_CircleLimitedRoom_AllowsMemberOfAnyRequiredCircle()
    {
        var session = new PlayerSessionService();
        var host = new MajakPlayer { ConnectionId = "c1", MemberNo = "host", ChannelId = "ch1" };
        var player = new MajakPlayer { ConnectionId = "c2", MemberNo = "u2", ChannelId = "ch1" };
        session.Register(host);
        session.Register(player);
        var room = session.CreateRoom("ch1", host, "", 1, 0, 0, false);
        room.RequiredCircles["circle01"] = "Circle One";
        room.RequiredCircles["circle02"] = "Circle Two";

        var repoMock = new Mock<PlayerRepository>(MockBehavior.Loose);
        repoMock.Setup(r => r.GetCircleInfoAsync("u2")).ReturnsAsync(new Dictionary<string, string>
        {
            ["circle02"] = "Circle Two",
        });

        var cmd = new RoomEnterRoomCommand(
            session,
            new FakeGameLogicService(),
            repoMock.Object,
            new RoomRegistryService(TestMasterCacheFactory.CreateRedisService()));
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.RoomId] = room.RoomId });

        await cmd.ExecuteAsync(ctx);

        Assert.Contains(room.Seats, s => s?.MemberNo == "u2");
        Assert.Contains(sent, s => s.method == Cmd.EnterRoomCmd);
        Assert.DoesNotContain(sent, s => s.method == Cmd.ConnectTypeError);
    }

    [Fact]
    public async Task Execute_CircleLimitedRoom_RejectsNonMemberAndRestoresCachedCircles()
    {
        var session = new PlayerSessionService();
        var host = new MajakPlayer { ConnectionId = "c1", MemberNo = "host", ChannelId = "ch1" };
        var player = new MajakPlayer
        {
            ConnectionId = "c2",
            MemberNo = "u2",
            ChannelId = "ch1",
            CircleInfo = new Dictionary<string, string> { ["cached"] = "Cached Circle" },
        };
        session.Register(host);
        session.Register(player);
        var room = session.CreateRoom("ch1", host, "", 1, 0, 0, false);
        room.RequiredCircles["circle01"] = "Circle One";
        room.RequiredCircles["circle02"] = "Circle Two";

        var repoMock = new Mock<PlayerRepository>(MockBehavior.Loose);
        repoMock.Setup(r => r.GetCircleInfoAsync("u2")).ReturnsAsync(new Dictionary<string, string>
        {
            ["other"] = "Other Circle",
        });

        var cmd = new RoomEnterRoomCommand(
            session,
            new FakeGameLogicService(),
            repoMock.Object,
            new RoomRegistryService(TestMasterCacheFactory.CreateRedisService()));
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.RoomId] = room.RoomId });

        await cmd.ExecuteAsync(ctx);

        Assert.DoesNotContain(room.Seats, s => s?.MemberNo == "u2");
        var error = Assert.Single(sent, s => s.method == Cmd.ConnectTypeError);
        var packet = CommandTestHelper.ToDict(error.packet);
        Assert.Equal(LegacyErrorCode.MajNotEntryCircle, ((JsonElement)packet[GKey.FailCode]!).GetInt32());
        Assert.Contains("Circle One", ((JsonElement)packet[GKey.Message]!).GetString());
        Assert.Contains("Circle Two", ((JsonElement)packet[GKey.Message]!).GetString());
        Assert.Equal("Cached Circle", Assert.Single(player.CircleInfo).Value);
    }

    [Fact]
    public async Task Execute_MemberNoMismatch_SendsNotMatchSocketError()
    {
        var session = new PlayerSessionService();
        var host = new MajakPlayer { ConnectionId = "c1", MemberNo = "host", ChannelId = "ch1" };
        var player = new MajakPlayer { ConnectionId = "c2", MemberNo = "u2", ChannelId = "ch1" };
        session.Register(host);
        session.Register(player);
        var room = session.CreateRoom("ch1", host, "", 1, 0, 0, false);

        var cmd = new RoomEnterRoomCommand(session, new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [GKey.RoomId] = room.RoomId,
                [GKey.Pix] = "other",
            });

        await cmd.ExecuteAsync(ctx);

        Assert.DoesNotContain(room.Seats, s => s?.MemberNo == "u2");
        var error = Assert.Single(sent, s => s.method == Cmd.ConnectTypeError);
        var packet = CommandTestHelper.ToDict(error.packet);
        Assert.Equal(LegacyErrorCode.NotMatchSocketId, ((JsonElement)packet[GKey.FailCode]!).GetInt32());
    }

    // シナリオ4: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var session = new PlayerSessionService();
        var cmd = new RoomEnterRoomCommand(session, new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// RoomAlterRoomCommand テスト (room:alter)
// 原典: ProcessCommand_RoomAlterRoom
//   ホストのみが limitCnt 変更可。全員に AlterRoom 通知。
// ═══════════════════════════════════════════════════════════════════════════
public class RoomAlterRoomCommandTests
{
    // シナリオ1: ホストが limitCnt 変更 → AlterRoom ブロードキャスト
    [Fact]
    public async Task Execute_HostChangesLimit_BroadcastsAlterRoom()
    {
        var session = new PlayerSessionService();
        var host    = new MajakPlayer { ConnectionId = "c1", MemberNo = "host", ChannelId = "ch1" };
        session.Register(host);
        session.CreateRoom("ch1", host, "", 1, 0, 0, false);

        var cmd = new RoomAlterRoomCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(host,
            new Dictionary<string, object?> { ["roomLimitCnt"] = 2 });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.AlterRoom, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(1, ((JsonElement)pkt["result"]!).GetInt32());
        Assert.Equal(2, ((JsonElement)pkt["limitCnt"]!).GetInt32());
    }

    [Fact]
    public async Task Execute_LimitBelowCurrentPlayerCount_ClampsToPlayerCount()
    {
        var session = new PlayerSessionService();
        var host = new MajakPlayer { ConnectionId = "c1", MemberNo = "host", ChannelId = "ch1" };
        var guest = new MajakPlayer { ConnectionId = "c2", MemberNo = "guest", ChannelId = "ch1" };
        session.Register(host);
        session.Register(guest);
        var room = session.CreateRoom("ch1", host, "", 1, 0, 0, false);
        session.JoinRoom(room.RoomId, guest);

        var cmd = new RoomAlterRoomCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(host,
            new Dictionary<string, object?> { [GKey.RoomLimitCnt] = 1 });

        await cmd.ExecuteAsync(ctx);

        Assert.Equal(2, room.LimitCnt);
        Assert.Single(sent);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(2, ((JsonElement)pkt["limitCnt"]!).GetInt32());
    }

    // シナリオ2: 非ホストが変更しようとしても無視
    [Fact]
    public async Task Execute_NonHost_NothingSent()
    {
        var session = new PlayerSessionService();
        var host    = new MajakPlayer { ConnectionId = "c1", MemberNo = "host", ChannelId = "ch1" };
        var guest   = new MajakPlayer { ConnectionId = "c2", MemberNo = "guest", ChannelId = "ch1" };
        session.Register(host);
        session.Register(guest);
        var room = session.CreateRoom("ch1", host, "", 1, 0, 0, false);
        session.JoinRoom(room.RoomId, guest);

        var cmd = new RoomAlterRoomCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(guest,
            new Dictionary<string, object?> { ["roomLimitCnt"] = 3 });

        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    // シナリオ3: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var session = new PlayerSessionService();
        var cmd = new RoomAlterRoomCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

public class RoomExitRoomCommandTests
{
    [Fact]
    public async Task Execute_PlayingPlayerExit_KeepsOutPlayerSeatAndSendsLegacyDeleteMember()
    {
        var session = new PlayerSessionService();
        var owner = new MajakPlayer { ConnectionId = "c1", MemberNo = "owner", ChannelId = "ch1", EngineOrder = 0 };
        var player = new MajakPlayer { ConnectionId = "c2", MemberNo = "u2", ChannelId = "ch1", EngineOrder = 1 };
        session.Register(owner);
        session.Register(player);
        var room = session.CreateRoom("ch1", owner, "", 1, 0, long.MaxValue, false);
        Assert.True(session.JoinRoom(room.RoomId, player));
        room.State = GameRoomState.Playing;

        var cmd = new RoomExitRoomCommand(session,
            new RoomRegistryService(TestMasterCacheFactory.CreateRedisService()),
            new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Null(player.RoomId);
        Assert.True(player.IsOutPlayer);
        Assert.Same(player, room.Seats[(int)player.SeatPos]);
        Assert.Equal(1, room.LimitCnt);
        var deleteMember = sent.First(s => s.method == Cmd.DeleteMember);
        var packet = CommandTestHelper.ToDict(deleteMember.packet);
        Assert.Equal("owner", ((JsonElement)packet[GKey.RoomHost]!).GetString());
        Assert.Equal(GKey.ValuePlayer, ((JsonElement)packet[GKey.PlayerType]!).GetString());
        Assert.Equal((int)player.SeatPos, ((JsonElement)packet[GKey.PlayerPos]!).GetInt32());
        Assert.Equal("u2", ((JsonElement)packet[GKey.Pix]!).GetString());
    }

    [Fact]
    public async Task Execute_PlayingViewerExit_SendsViewerDeleteMember()
    {
        var session = new PlayerSessionService();
        var owner = new MajakPlayer { ConnectionId = "c1", MemberNo = "owner", ChannelId = "ch1" };
        var viewer = new MajakPlayer { ConnectionId = "cv", MemberNo = "viewer1", ChannelId = "ch1" };
        session.Register(owner);
        session.Register(viewer);
        var room = session.CreateRoom("ch1", owner, "", 1, 0, long.MaxValue, false);
        room.State = GameRoomState.Playing;
        Assert.True(room.AddViewer(viewer));

        var cmd = new RoomExitRoomCommand(session,
            new RoomRegistryService(TestMasterCacheFactory.CreateRedisService()),
            new FakeGameLogicService());
        var (ctx, sent) = CommandTestHelper.MakeContext(viewer);

        await cmd.ExecuteAsync(ctx);

        var deleteMember = sent.First(s => s.method == Cmd.DeleteMember);
        var packet = CommandTestHelper.ToDict(deleteMember.packet);
        Assert.Equal(GKey.ValueViewer, ((JsonElement)packet[GKey.PlayerType]!).GetString());
        Assert.DoesNotContain(room.Viewers, v => v.MemberNo == "viewer1");
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// TsumikomiCommand テスト (smmc5e)
// 原典: ProcessCommand_Tsumikomi
//   TestEnvironment=true の場合のみ有効
//   TestEnvironment=false → return FALSE (何も送らない)
// ═══════════════════════════════════════════════════════════════════════════
public class TsumikomiCommandTests
{
    private static IConfiguration BuildConfig(bool testEnv)
    {
        var dict = new Dictionary<string, string?> { ["GameSettings:TestEnvironment"] = testEnv.ToString() };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    // シナリオ1: TestEnvironment=true + 有効な配牌 → "tsumi" チャットをブロードキャスト
    [Fact]
    public async Task Execute_TestEnvTrue_SetsBipaiAndSendsTsumiChat()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);
        session.CreateRoom("ch1", player, "", 1, 0, 0, false);

        var cmd = new TsumikomiCommand(session, BuildConfig(true));
        var payload = new Dictionary<string, object?>
        {
            ["pai"] = Enumerable.Range(0, 136).Select(i => i % 34).ToArray(),
        };
        var (ctx, sent) = CommandTestHelper.MakeContext(player, payload);

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.HanChatRelay, sent[0].method);
        var packet = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(sent[0].packet);
        Assert.Equal("u1", packet[GKey.Pix]);
        Assert.Equal(GKey.ValuePlayer, packet[GKey.PlayerType]);
        Assert.Equal(GKey.ValueAll, packet[GKey.Target]);
        Assert.Equal(0, packet[GKey.Color]);
        Assert.Equal("tsumi", packet[GKey.String]);
        Assert.Equal("u1", packet["memberNo"]);
        Assert.Equal("tsumi", packet["string"]);
    }

    // シナリオ1b: TestEnvironment=true でも配牌データが不正なら GetFromParser_KyokuInfo 失敗相当
    [Fact]
    public async Task Execute_TestEnvTrue_InvalidPayload_NothingSent()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);
        session.CreateRoom("ch1", player, "", 1, 0, 0, false);

        var cmd = new TsumikomiCommand(session, BuildConfig(true));
        var (ctx, sent) = CommandTestHelper.MakeContext(player, new Dictionary<string, object?>
        {
            ["pai"] = new[] { 1, 2, 3 },
        });

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }

    // シナリオ2: TestEnvironment=false → 何も送らない (原典: return FALSE)
    [Fact]
    public async Task Execute_TestEnvFalse_NothingSent()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);
        session.CreateRoom("ch1", player, "", 1, 0, 0, false);

        var cmd = new TsumikomiCommand(session, BuildConfig(false));
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    // シナリオ3: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new TsumikomiCommand(new PlayerSessionService(), BuildConfig(true));
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// IpAdapterInfoCommand テスト (smmc6e)
// 原典: ProcessCommandIPAdapterInfo
//   keyGateway + keyMACAddr をプレイヤーに保存するのみ。
// ═══════════════════════════════════════════════════════════════════════════
public class IpAdapterInfoCommandTests
{
    // シナリオ1: player あり → legacy keys を保存し、応答は送らない
    [Fact]
    public async Task Execute_WithLegacyKeys_StoresGatewayAndMacWithoutResponse()
    {
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1" };
        var cmd    = new IpAdapterInfoCommand();
        var payload = new Dictionary<string, object?> { [Key.Gateway] = "192.168.1.1", [Key.MacAddr] = "AA:BB:CC:DD:EE:FF" };
        var (ctx, sent) = CommandTestHelper.MakeContext(player, payload);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
        Assert.Equal("192.168.1.1", player.Gateway);
        Assert.Equal("AA:BB:CC:DD:EE:FF", player.MacAddr);
    }

    [Fact]
    public async Task Execute_AliasKeys_StoresGatewayAndMac()
    {
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1" };
        var cmd    = new IpAdapterInfoCommand();
        var payload = new Dictionary<string, object?> { ["gateway"] = "10.0.0.1", ["mac"] = "AA:BB:CC:DD" };
        var (ctx, sent) = CommandTestHelper.MakeContext(player, payload);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
        Assert.Equal("10.0.0.1", player.Gateway);
        Assert.Equal("AA:BB:CC:DD", player.MacAddr);
    }

    [Fact]
    public async Task Execute_TooLongValues_PreservesExistingValues()
    {
        var player = new MajakPlayer
        {
            ConnectionId = "c1",
            MemberNo = "u1",
            Gateway = "1.1.1.1",
            MacAddr = "AA:BB",
        };
        var cmd = new IpAdapterInfoCommand();
        var payload = new Dictionary<string, object?>
        {
            [Key.Gateway] = "123.123.123.1234",
            [Key.MacAddr] = "AA:BB:CC:DD:EE:FF:00",
        };
        var (ctx, sent) = CommandTestHelper.MakeContext(player, payload);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
        Assert.Equal("1.1.1.1", player.Gateway);
        Assert.Equal("AA:BB", player.MacAddr);
    }

    // シナリオ2: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new IpAdapterInfoCommand();
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// EventInfoCommand テスト (smmc3e)
// 原典: HMajDBObject::GetMemberEventInfo → EVTUSERMAST 照会
//   result=1 + eventCnt + events[]
// ═══════════════════════════════════════════════════════════════════════════
public class EventInfoCommandTests
{
    private readonly Mock<PlayerRepository> _playerRepoMock
        = new(MockBehavior.Loose);

    // シナリオ1: イベント情報あり → result=1 + eventCnt
    [Fact]
    public async Task Execute_WithEvents_Returns1WithCount()
    {
        _playerRepoMock
            .Setup(r => r.GetMemberEventInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<EventInfo>
            {
                new EventInfo { EvtCode = "EVT001", EvtNo = 1, ExtraVal1 = 10 },
                new EventInfo { EvtCode = "EVT002", EvtNo = 2, ExtraVal1 = 20 },
            });

        var player = new MajakPlayer { MemberNo = "u1" };
        var cmd    = new EventInfoCommand(_playerRepoMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.EventInfo, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(1, ((JsonElement)pkt["result"]!).GetInt32());
        Assert.Equal(2, ((JsonElement)pkt["eventCnt"]!).GetInt32());
    }

    // シナリオ2: イベント情報なし → result=1 + eventCnt=0
    [Fact]
    public async Task Execute_NoEvents_Returns1WithZero()
    {
        _playerRepoMock
            .Setup(r => r.GetMemberEventInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<EventInfo>());

        var player = new MajakPlayer { MemberNo = "u1" };
        var cmd    = new EventInfoCommand(_playerRepoMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(0, ((JsonElement)pkt["eventCnt"]!).GetInt32());
    }

    // シナリオ3: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        _playerRepoMock.Setup(r => r.GetMemberEventInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<EventInfo>());
        var cmd = new EventInfoCommand(_playerRepoMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc27e TournamentRegistCommand テスト
// 原典: ProcessCommand_TournamentRegist
//   CheckTournamentRequiredValue + CheckTournamentCoordinalValue チェック
//   成功 → result=1 + failCodeCnt=0 + gamMoney
//   失敗 → result=0 + failCode
// ═══════════════════════════════════════════════════════════════════════════
public class TournamentRegistCommandTests
{
    private readonly Mock<TournamentRepository> _tournRepoMock
        = new(MockBehavior.Loose);
    private readonly Mock<PlayerRepository> _playerRepoMock
        = new(MockBehavior.Loose);
    private readonly Mock<HistoryRepository> _historyRepoMock
        = new(MockBehavior.Loose);

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

    private TournamentService BuildTournamentService()
    {
        _tournRepoMock.Setup(r => r.InsertPlanAsync(It.IsAny<TournamentPlan>()))
            .ReturnsAsync(true);
        _tournRepoMock.Setup(r => r.UpdatePlayerNumAsync(It.IsAny<long>(), It.IsAny<int>()))
            .ReturnsAsync(true);
        var logger = new Mock<ILogger<TournamentService>>();
        return TestTournamentServiceFactory.Create(_tournRepoMock.Object, logger.Object);
    }

    // 有効な baseRule / moneyRule を返すヘルパー
    private static Dictionary<string, object?> ValidPayload(int registFlag = 1) => new()
    {
        [GKey.Pix]          = "host01",
        ["tournamentRegistFlag"] = registFlag,
        ["tournamentBaseRule"]   = "4|1|1|5",   // maxPlayers=4, mode=1, playNum=1, playTime=5
        ["tournamentMoneyRule"]  = "0|1000|500|200|0",
        ["tournamentName"]       = "TestTournament",
        ["tournamentDate"]       = DateTime.Now.AddHours(3).ToString("yyyy/MM/dd HH:mm:ss"),
        [GKey.Password]          = "",
        [GKey.MaxViewer]         = 4,
        [GKey.RoomOption]        = "",
    };

    // シナリオ1: 仮登録 (registFlag=0) → バリデーションのみ → result=1
    [Fact]
    public async Task Execute_CheckOnly_Returns1()
    {
        var svc    = BuildTournamentService();
        var money  = BuildMoneyService();
        var player = new MajakPlayer { MemberNo = "host01", GamMoney = 100000, IsAdminId = true };
        var cmd    = new TournamentRegistCommand(svc, money);
        var (ctx, sent) = CommandTestHelper.MakeContext(player, ValidPayload(registFlag: 0));

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.TournamentRegist, sent[0].method);
        Assert.Equal(1, CommandTestHelper.GetResult(sent[0].packet));
    }

    // シナリオ2: 本登録 (registFlag=1) → DB登録 → result=1
    [Fact]
    public async Task Execute_Register_Returns1()
    {
        var svc    = BuildTournamentService();
        var money  = BuildMoneyService();
        var player = new MajakPlayer { MemberNo = "host01", GamMoney = 100000, IsAdminId = true };
        var cmd    = new TournamentRegistCommand(svc, money);
        var (ctx, sent) = CommandTestHelper.MakeContext(player, ValidPayload(registFlag: 1));

        await cmd.ExecuteAsync(ctx);

        Assert.Equal(2, sent.Count);
        Assert.Equal(1, CommandTestHelper.GetResult(sent[0].packet));
        Assert.Equal("tournament:list_changed", sent[1].method);
        var changed = CommandTestHelper.ToDict(sent[1].packet);
        Assert.Equal("registered", ((System.Text.Json.JsonElement)changed["changeType"]!).GetString());
    }

    [Fact]
    public async Task Execute_RegisterWithInsufficientPlanMoney_Returns1010WithoutInsert()
    {
        var svc    = BuildTournamentService();
        var money  = BuildMoneyService();
        var player = new MajakPlayer { MemberNo = "host01", GamMoney = 1000, IsAdminId = true };
        var cmd    = new TournamentRegistCommand(svc, money);
        var (ctx, sent) = CommandTestHelper.MakeContext(player, ValidPayload(registFlag: 1));

        await cmd.ExecuteAsync(ctx);

        var packet = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(0, CommandTestHelper.GetResult(sent[0].packet));
        Assert.Equal("1010", Convert.ToString(packet["failCode"]));
        _tournRepoMock.Verify(r => r.InsertPlanAsync(It.IsAny<TournamentPlan>()), Times.Never);
    }

    [Fact]
    public async Task Execute_Register_WritesLegacyTournamentPlanMoneyHistory()
    {
        var svc = BuildTournamentService();
        var money = BuildMoneyService();
        var player = new MajakPlayer { MemberNo = "host01", GamMoney = 100000, IsAdminId = true, IpAddress = "1.2.3.4" };
        var cmd = new TournamentRegistCommand(svc, money);
        var (ctx, _) = CommandTestHelper.MakeContext(player, ValidPayload(registFlag: 1));

        await cmd.ExecuteAsync(ctx);

        _historyRepoMock.Verify(r => r.InsertGameMoneyHistAsync(
            "host01", GameConst.EvtCodeTournamentPlan, -1870, 100000, 98130, "1.2.3.4"), Times.Once);
    }

    // シナリオ3: baseRule が不正 → バリデーション失敗 → result=0
    [Fact]
    public async Task Execute_InvalidBaseRule_Returns0()
    {
        var svc    = BuildTournamentService();
        var money  = BuildMoneyService();
        var player = new MajakPlayer { MemberNo = "host01", GamMoney = 100000, IsAdminId = true };
        var cmd    = new TournamentRegistCommand(svc, money);
        var payload = ValidPayload();
        payload["tournamentBaseRule"] = "INVALID";  // 不正な値

        var (ctx, sent) = CommandTestHelper.MakeContext(player, payload);

        await cmd.ExecuteAsync(ctx);

        Assert.Equal(0, CommandTestHelper.GetResult(sent[0].packet));
    }

    // シナリオ4: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var svc   = BuildTournamentService();
        var money = BuildMoneyService();
        var cmd   = new TournamentRegistCommand(svc, money);
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    // シナリオ5: 応答フィールド確認 (failCodeCnt + gamMoney)
    [Fact]
    public async Task Execute_ResponseHasRequiredFields()
    {
        var svc    = BuildTournamentService();
        var money  = BuildMoneyService();
        var player = new MajakPlayer { MemberNo = "host01", GamMoney = 50000, IsAdminId = true };
        var cmd    = new TournamentRegistCommand(svc, money);
        var (ctx, sent) = CommandTestHelper.MakeContext(player, ValidPayload(registFlag: 0));

        await cmd.ExecuteAsync(ctx);

        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.True(pkt.ContainsKey("result"));
        Assert.True(pkt.ContainsKey("failCodeCnt"));
        Assert.True(pkt.ContainsKey("failCode"));
        Assert.True(pkt.ContainsKey("gamMoney"));
    }

    [Fact]
    public async Task Execute_MissingRequiredLegacyKey_NothingSent()
    {
        var svc = BuildTournamentService();
        var money = BuildMoneyService();
        var player = new MajakPlayer { MemberNo = "host01", GamMoney = 100000, IsAdminId = true };
        var cmd = new TournamentRegistCommand(svc, money);
        var payload = ValidPayload();
        payload.Remove(GKey.RoomOption);
        string? abortReason = null;
        var (ctx, sent) = CommandTestHelper.MakeContext(player, payload, reason => abortReason = reason);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
        Assert.Contains("TournamentRegistCommand missing required key", abortReason);
        _tournRepoMock.Verify(r => r.InsertPlanAsync(It.IsAny<TournamentPlan>()), Times.Never);
    }
}
