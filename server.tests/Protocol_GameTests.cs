using Moq;
using Microsoft.Extensions.Logging;
using MajakServer.Commands;
using MajakServer.Commands.Game;
using MajakServer.Commands.Channel;
using MajakServer.Commands.Room;
using MajakServer.Engine;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;
using System.Text.Json;

namespace MajakServer.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// GamePlayCommand テスト (playing)
// 原典: ProcessCommand_GamePlay
//   ゲーム進行中: エンジン経由でアクション処理
//   非進行中 / viewer / seatOrder 不一致: invalid command → CloseSocket 相当
// ═══════════════════════════════════════════════════════════════════════════
public class GamePlayCommandTests
{
    private readonly Mock<GameLogicService> _gameLogicMock;
    private readonly PlayerSessionService   _session;

    public GamePlayCommandTests()
    {
        _session      = new PlayerSessionService();

        // GameLogicService mock — null 依存でインスタンス化 (非進行中テストでは呼ばれない)
        _gameLogicMock = new Mock<GameLogicService>(
            MockBehavior.Loose,
            _session,
            (HistoryRepository)null!,
            (LogRepository)null!,
            (RatingService)null!,
            (PlayerRepository)null!,
            (GameMoneyService)null!,
            (TitleService)null!,
            (TournamentService)null!,
            (GradeRankService)null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            (ILogger<GameLogicService>)null!,
            (RoomRegistryService)null!);
    }

    // シナリオ1: ゲーム非進行中 → invalid command 相当で CloseSocket
    [Fact]
    public async Task Execute_NotPlaying_AbortsConnection()
    {
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        _session.Register(player);
        var room = _session.CreateRoom("ch1", player, "", 1, 0, 0, false);
        room.State = GameRoomState.Waiting; // 待機中
        string? abortReason = null;

        var cmd = new GamePlayCommand(_session, _gameLogicMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(player, onAbort: reason => abortReason = reason);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
        Assert.Contains("invalid status", abortReason);
        _gameLogicMock.Verify(g => g.GamePlayProcessAsync(It.IsAny<GameRoom>(), It.IsAny<CommandContext>()), Times.Never);
    }

    // シナリオ2: ルーム未入室 → CloseSocket 相当
    [Fact]
    public async Task Execute_NotInRoom_AbortsConnection()
    {
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", RoomId = null };
        _session.Register(player);
        string? abortReason = null;

        var cmd = new GamePlayCommand(_session, _gameLogicMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(player, onAbort: reason => abortReason = reason);

        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
        Assert.Contains("not in room", abortReason);
    }

    // シナリオ3: player=null → CloseSocket 相当
    [Fact]
    public async Task Execute_NullPlayer_AbortsConnection()
    {
        string? abortReason = null;
        var cmd = new GamePlayCommand(_session, _gameLogicMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(null!, onAbort: reason => abortReason = reason);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
        Assert.Contains("player is null", abortReason);
    }

    // シナリオ4: 観戦者 → invalid command 相当で CloseSocket
    [Fact]
    public async Task Execute_Viewer_AbortsConnection()
    {
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1", IsViewer = true };
        _session.Register(player);
        var room = _session.CreateRoom("ch1", player, "", 1, 0, 0, false);
        room.State = GameRoomState.Playing;
        room.Engine.InitHanchan(new RuleInfo { Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 1, Uma = 0 });
        string? abortReason = null;

        var cmd = new GamePlayCommand(_session, _gameLogicMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { ["playType"] = "MJPID_ACTION", ["seatOrder"] = 0, ["action"] = 1 },
            reason => abortReason = reason);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
        Assert.Contains("invalid status", abortReason);
        Assert.Contains("isViewer=True", abortReason);
        _gameLogicMock.Verify(g => g.GamePlayProcessAsync(It.IsAny<GameRoom>(), It.IsAny<CommandContext>()), Times.Never);
    }

    // シナリオ5: playType 不正 → GetFromParser_ActionInfo error 相当で CloseSocket
    [Fact]
    public async Task Execute_InvalidPlayType_AbortsConnection()
    {
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        _session.Register(player);
        var room = _session.CreateRoom("ch1", player, "", 1, 0, 0, false);
        room.Engine.InitHanchan(new RuleInfo { Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 1, Uma = 0 });
        player.EngineOrder = 0;
        string? abortReason = null;

        var cmd = new GamePlayCommand(_session, _gameLogicMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { ["playType"] = "MJPID_ACTIONS", ["seatOrder"] = 0, ["action"] = 1 },
            reason => abortReason = reason);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
        Assert.Contains("invalid action packet", abortReason);
        _gameLogicMock.Verify(g => g.GamePlayProcessAsync(It.IsAny<GameRoom>(), It.IsAny<CommandContext>()), Times.Never);
    }

    [Fact]
    public async Task Execute_MissingActionHeader_AbortsConnection()
    {
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1", EngineOrder = 0 };
        _session.Register(player);
        var room = _session.CreateRoom("ch1", player, "", 1, 0, 0, false);
        room.Engine.InitHanchan(new RuleInfo { Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 1, Uma = 0 });
        player.EngineOrder = 0;
        string? abortReason = null;

        var cmd = new GamePlayCommand(_session, _gameLogicMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { ["playType"] = "MJPID_ACTION", ["seatOrder"] = 0 },
            reason => abortReason = reason);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
        Assert.Contains("invalid action packet", abortReason);
        _gameLogicMock.Verify(g => g.GamePlayProcessAsync(It.IsAny<GameRoom>(), It.IsAny<CommandContext>()), Times.Never);
    }

    // シナリオ6: nSeatOrder と m_nSeatPos 不一致 → nOrder error で CloseSocket
    [Fact]
    public async Task Execute_SeatOrderMismatch_AbortsConnection()
    {
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        _session.Register(player);
        var room = _session.CreateRoom("ch1", player, "", 1, 0, 0, false);
        room.Engine.InitHanchan(new RuleInfo { Hanchan = true, Kuitan = true, Contest = 0, AkaDora = 1, Uma = 0 });
        room.Engine.HanchanInfo.Player = new[] { 0, 1, 2, 3 };
        player.EngineOrder = 0;
        string? abortReason = null;

        var cmd = new GamePlayCommand(_session, _gameLogicMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { ["playType"] = "MJPID_ACTION", ["seatOrder"] = 1, ["action"] = 1 },
            reason => abortReason = reason);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
        Assert.Contains("nOrder error", abortReason);
        _gameLogicMock.Verify(g => g.GamePlayProcessAsync(It.IsAny<GameRoom>(), It.IsAny<CommandContext>()), Times.Never);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// AgariRecCommand テスト (horarec)
// 原典: ProcessCommand_AgariRec — 非デバッグ時は invalid command / return FALSE
// .NET 実装も本番ではクライアント入力を受け付けない
// ═══════════════════════════════════════════════════════════════════════════
public class AgariRecCommandTests
{
    private readonly Mock<HistoryRepository> _histMock
        = new(MockBehavior.Loose);

    // シナリオ1: ルーム在室でも本番では何もしない
    [Fact]
    public async Task Execute_InRoom_NothingSentNoDbWrite()
    {
        _histMock.Setup(r => r.InsertYakuHistAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", RoomId = 5 };
        var cmd    = new AgariRecCommand(_histMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { ["yaku"] = 1 });

        await cmd.ExecuteAsync(ctx);

        _histMock.Verify(r => r.InsertYakuHistAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        Assert.Empty(sent);
    }

    // シナリオ2: ルーム未入室 → 何もしない
    [Fact]
    public async Task Execute_NotInRoom_NothingSentNoDbWrite()
    {
        _histMock.Setup(r => r.InsertYakuHistAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", RoomId = null };
        var cmd    = new AgariRecCommand(_histMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { ["yaku"] = 1 });

        await cmd.ExecuteAsync(ctx);

        _histMock.Verify(r => r.InsertYakuHistAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        Assert.Empty(sent);
    }

    // シナリオ3: player=null → 何も送らない・DB 呼び出しなし
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new AgariRecCommand(_histMock.Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
        _histMock.Verify(r => r.InsertYakuHistAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// HistoryCommand テスト (history)
// 原典: ProcessCommand_History — 非デバッグ時 invalid command / return FALSE
// .NET 実装も client-made history を拒否する
// ═══════════════════════════════════════════════════════════════════════════
public class HistoryCommandTests
{
    private readonly Mock<HistoryRepository> _historyRepoMock
        = new(MockBehavior.Loose);
    private readonly Mock<LogRepository> _mysqlLogMock
        = new(MockBehavior.Loose, (MySqlDbContext)null!);
    private readonly Mock<PlayerRepository> _playerRepoMock
        = new(MockBehavior.Loose);

    private void SetupMocks()
    {
        _historyRepoMock.Setup(r => r.InsertGameHistAsync(It.IsAny<GameReport>()))
            .ReturnsAsync(1L);
        _mysqlLogMock.Setup(r => r.InsertGameHistAsync(It.IsAny<GameReport>()))
            .Returns(Task.CompletedTask);
        _playerRepoMock.Setup(r => r.UpdateCommonRatAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);
    }

    private HistoryCommand BuildCommand(PlayerSessionService session) =>
        new HistoryCommand(
            session,
            _historyRepoMock.Object,
            _mysqlLogMock.Object,
            new RatingService(),
            _playerRepoMock.Object);

    // シナリオ1: クライアント送信 history は DB 保存せず何も送らない
    [Fact]
    public async Task Execute_GameOver_NothingSentNoDbWrite()
    {
        SetupMocks();
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);
        var room = session.CreateRoom("ch1", player, "", 1, 0, 0, false);
        room.State = GameRoomState.Playing;

        var cmd = new HistoryCommand(
            session, _historyRepoMock.Object, _mysqlLogMock.Object,
            new RatingService(), _playerRepoMock.Object);

        var (ctx, sent) = CommandTestHelper.MakeContext(player);
        await cmd.ExecuteAsync(ctx);

        _historyRepoMock.Verify(r => r.InsertGameHistAsync(It.IsAny<GameReport>()), Times.Never);
        _mysqlLogMock.Verify(r => r.InsertGameHistAsync(It.IsAny<GameReport>()), Times.Never);
        Assert.Empty(sent);
    }

    // シナリオ2: client-made history ではルーム状態を変更しない
    [Fact]
    public async Task Execute_GameOver_RoomStateUnchanged()
    {
        SetupMocks();
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);
        var room = session.CreateRoom("ch1", player, "", 1, 0, 0, false);
        room.State = GameRoomState.Playing;

        var cmd = BuildCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);
        await cmd.ExecuteAsync(ctx);

        Assert.Equal(GameRoomState.Playing, room.State);
        Assert.Empty(sent);
    }

    // シナリオ3: player=null → 何も送らない・DB 呼び出しなし
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        SetupMocks();
        var session = new PlayerSessionService();
        var cmd = BuildCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
        _historyRepoMock.Verify(r => r.InsertGameHistAsync(It.IsAny<GameReport>()), Times.Never);
    }

    // シナリオ4: ルーム未入室 → 何も送らない
    [Fact]
    public async Task Execute_NotInRoom_NothingSent()
    {
        SetupMocks();
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", RoomId = null };
        session.Register(player);

        var cmd = BuildCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// ReplayNaviCommand テスト (repnavi)
// ═══════════════════════════════════════════════════════════════════════════
public class ReplayNaviCommandTests
{
    // シナリオ1: ルーム在室 → ルーム全員にブロードキャスト
    [Fact]
    public async Task Execute_InRoom_BroadcastsToRoom()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);
        session.CreateRoom("ch1", player, "", 1, 0, 0, false);
        var payload = new Dictionary<string, object?>
        {
            ["join"] = true,
            ["paif"] = true,
            ["skip"] = true,
            ["nSkip"] = 0,
            ["data"] = "paifu-bin-or-json",
        };

        var cmd = new ReplayNaviCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player, payload);

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.ReplayNavi, sent[0].method);
        Assert.Same(payload, sent[0].packet);
    }

    // シナリオ2: ルーム未入室 → 何も送らない
    [Fact]
    public async Task Execute_NotInRoom_NothingSent()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", RoomId = null };
        session.Register(player);

        var cmd = new ReplayNaviCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    // シナリオ3: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new ReplayNaviCommand(new PlayerSessionService());
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

public class GameReportCommandTests
{
    [Fact]
    public async Task Execute_ClientMadeGameReport_NothingSent()
    {
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        var cmd = new GameReportCommand();
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { ["fake"] = 1 });

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// RoomStateCommand テスト (mjkroom)
// 原典: ProcessCommand_RoomState — "invalid command" ログのみ, return TRUE
// .NET 実装も client-made room state を受け付けない
// ═══════════════════════════════════════════════════════════════════════════
public class RoomStateCommandTests
{
    // シナリオ1: ルーム在室でも何も送らない
    [Fact]
    public async Task Execute_InRoom_NothingSent()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);
        session.CreateRoom("ch1", player, "", 1, 0, 0, false);

        var cmd = new RoomStateCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }

    // シナリオ2: ルーム未入室 → 何も送らない
    [Fact]
    public async Task Execute_NotInRoom_NothingSent()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", RoomId = null };
        session.Register(player);

        var cmd = new RoomStateCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    // シナリオ3: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new RoomStateCommand(new PlayerSessionService());
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    [Fact]
    public void BuildRoomStatePayload_OutPlayerIsContinueMemberOnly()
    {
        var session = new PlayerSessionService();
        var p0 = new MajakPlayer { ConnectionId = "c0", MemberNo = "u0", ChannelId = "ch1" };
        var p1 = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1", IsOutPlayer = true };
        session.Register(p0);
        session.Register(p1);
        var room = session.CreateRoom("ch1", p0, "", 1, 0, 0, false, roomId: 12);
        room.AddPlayer(p1, 1);
        room.State = GameRoomState.Playing;

        var packet = RoomStatePayload.Build(room);

        Assert.Equal(1, packet[GKey.MemberCnt]);
        Assert.Equal(p0.Pix, packet[$"{GKey.Pix}0"]);
        Assert.NotEqual(p0.MemberNo, packet[$"{GKey.Pix}0"]);
        Assert.False(packet.ContainsKey($"{GKey.Pix}1"));
        Assert.Equal(1, packet[GKey.OpMemberCnt]);
        Assert.Equal(p1.Pix, packet[$"{GKey.OpPix}0"]);
        Assert.NotEqual(p1.MemberNo, packet[$"{GKey.OpPix}0"]);
        Assert.Equal(1, packet[$"{GKey.OpMemberPos}0"]);
    }

    [Fact]
    public void BuildRoomStatePayload_AllOutPlayersRemainVisibleAsContinueMembers()
    {
        var room = new GameRoom { RoomId = 13, ChannelId = "ch1", State = GameRoomState.Playing };
        for (int seat = 0; seat < 4; seat++)
        {
            room.AddPlayer(new MajakPlayer
            {
                MemberNo = $"u{seat}",
                ChannelId = "ch1",
                IsOutPlayer = true,
            }, seat);
        }

        var packet = RoomStatePayload.Build(room);

        Assert.Equal(RoomStatePayload.LegacyRoomGameView, packet[GKey.RoomStateKey]);
        Assert.Equal(0, packet[GKey.MemberCnt]);
        Assert.Equal(4, packet[GKey.OpMemberCnt]);
        for (int seat = 0; seat < 4; seat++)
        {
            Assert.Equal($"u{seat}", packet[$"{GKey.OpPix}{seat}"]);
            Assert.Equal(seat, packet[$"{GKey.OpMemberPos}{seat}"]);
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// PaiInfoListCommand テスト (smmc4e)
// 原典: commandPaiInfoList は S→C (サーバープッシュ) パケット
// .NET 実装もクライアント入力を任意中継しない
// ═══════════════════════════════════════════════════════════════════════════
public class PaiInfoListCommandTests
{
    // シナリオ1: ルーム在室でも何も送らない
    [Fact]
    public async Task Execute_InRoom_NothingSent()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);
        session.CreateRoom("ch1", player, "", 1, 0, 0, false);

        var cmd = new PaiInfoListCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }

    // シナリオ2: ルーム未入室 → 何も送らない
    [Fact]
    public async Task Execute_NotInRoom_NothingSent()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", RoomId = null };
        session.Register(player);

        var cmd = new PaiInfoListCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    // シナリオ3: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new PaiInfoListCommand(new PlayerSessionService());
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// ViewRoomCommand テスト (room:view)
// 原典: ProcessCommand_ViewRoom (HMajChnlServer)
//   ルームなし → room:enter_error + failCode=1
//   パスワード不一致 → room:enter_error + failCode=3
//   ゲーム未進行 → room:enter_error + failCode=2
//   成功 → MemberList + AddMember (ルーム全員へ) + ViewRoom 応答
// ═══════════════════════════════════════════════════════════════════════════
public class ViewRoomCommandTests
{
    // シナリオ1: ルームが存在しない → room:enter_error + failCode=1
    [Fact]
    public async Task Execute_RoomNotFound_SendsEnterError()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);

        var cmd = new ViewRoomCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { ["roomId"] = 999, ["roomPwd"] = "" });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.ConnectTypeError, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(LegacyErrorCode.CannotEnterRoom, ((JsonElement)pkt["failCode"]!).GetInt32());
    }

    // シナリオ2: ゲーム未進行 (Waiting) → room:enter_error + failCode=2
    [Fact]
    public async Task Execute_NotPlaying_SendsEnterError()
    {
        var session = new PlayerSessionService();
        var owner   = new MajakPlayer { ConnectionId = "c2", MemberNo = "owner", ChannelId = "ch1" };
        session.Register(owner);
        var room = session.CreateRoom("ch1", owner, "", 1, 0, 0, false);
        room.State = GameRoomState.Waiting; // ゲーム未開始

        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "viewer", ChannelId = "ch1" };
        session.Register(player);

        var cmd = new ViewRoomCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { ["roomId"] = room.RoomId, ["roomPwd"] = "", ["playerType"] = GKey.ValueViewer });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.ConnectTypeError, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(LegacyErrorCode.CannotEnterRoom, ((JsonElement)pkt["failCode"]!).GetInt32());
    }

    // シナリオ3: ゲーム進行中 → MemberList (Caller) + AddMember (Group) + ViewRoom 応答
    [Fact]
    public async Task Execute_Playing_AddsViewerAndNotifies()
    {
        var session = new PlayerSessionService();
        var owner   = new MajakPlayer { ConnectionId = "c2", MemberNo = "owner", ChannelId = "ch1" };
        session.Register(owner);
        var room = session.CreateRoom("ch1", owner, "", 1, 0, 0, false);
        room.State = GameRoomState.Playing;

        var viewer = new MajakPlayer { ConnectionId = "c1", MemberNo = "viewer", ChannelId = "ch1" };
        session.Register(viewer);

        var cmd = new ViewRoomCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(viewer,
            new Dictionary<string, object?> { ["roomId"] = room.RoomId, ["roomPwd"] = "", ["playerType"] = GKey.ValueViewer });

        await cmd.ExecuteAsync(ctx);

        // Caller: MemberList + ViewRoom / Group: AddMember
        Assert.Contains(sent, s => s.method == Cmd.MemberList);
        Assert.Contains(sent, s => s.method == Cmd.AddMember);
        Assert.Contains(sent, s => s.method == Cmd.ViewRoom);
        Assert.True(viewer.IsViewer);
        Assert.Equal(room.RoomId, viewer.RoomId);
        Assert.Contains(room.Viewers, v => v.MemberNo == viewer.MemberNo);
        var add = CommandTestHelper.ToDict(sent.First(s => s.method == Cmd.AddMember).packet);
        Assert.Equal(GKey.ValueViewer, ((JsonElement)add["playerType"]!).GetString());
        var view = CommandTestHelper.ToDict(sent.First(s => s.method == Cmd.ViewRoom).packet);
        Assert.Equal(1, ((JsonElement)view["result"]!).GetInt32());
        Assert.Equal(GKey.ValueViewer, ((JsonElement)view["playerType"]!).GetString());
    }

    [Fact]
    public async Task Execute_Playing_RequestsFullGameResyncForViewer()
    {
        var session = new PlayerSessionService();
        var owner = new MajakPlayer { ConnectionId = "c2", MemberNo = "owner", ChannelId = "ch1" };
        session.Register(owner);
        var room = session.CreateRoom("ch1", owner, "", 1, 0, 0, false);
        room.State = GameRoomState.Playing;
        room.Engine.InitHanchan(new RuleInfo { Hanchan = true, Kuitan = true });
        room.PlayHistory.Add(new { playType = "MJPID_INIHAN" });
        room.PlayHistory.Add(new { playType = "MJPID_INIKYO" });
        var viewer = new MajakPlayer { ConnectionId = "c1", MemberNo = "viewer", ChannelId = "ch1" };
        session.Register(viewer);
        var gameLogic = new GameLogicService(
            session,
            (HistoryRepository)null!,
            (LogRepository)null!,
            new RatingService(),
            (PlayerRepository)null!,
            (GameMoneyService)null!,
            (TitleService)null!,
            (TournamentService)null!,
            (GradeRankService)null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            (ILogger<GameLogicService>)null!,
            (RoomRegistryService)null!);
        var cmd = new ViewRoomCommand(session, gameLogic);
        var (ctx, sent) = CommandTestHelper.MakeContext(viewer,
            new Dictionary<string, object?> { ["roomId"] = room.RoomId, ["roomPwd"] = "", ["playerType"] = GKey.ValueViewer });

        await cmd.ExecuteAsync(ctx);

        int paiInfoIndex = sent.FindIndex(packet => packet.method == Cmd.PaiInfoList);
        int historyIndex = sent.FindIndex(packet => packet.method == Cmd.History);
        Assert.True(paiInfoIndex >= 0);
        Assert.True(historyIndex > paiInfoIndex);
    }

    [Fact]
    public async Task Execute_TournamentMissingRequiredKeys_AbortsConnection()
    {
        var session = new PlayerSessionService();
        var owner = new MajakPlayer { ConnectionId = "c2", MemberNo = "owner", ChannelId = "ch1" };
        session.Register(owner);
        var room = session.CreateRoom("ch1", owner, "", 1, 0, 0, false, subId: "00H5A");
        room.State = GameRoomState.Playing;
        room.TournamentSeqNo = 100;
        room.TournamentSubId = 2;

        var viewer = new MajakPlayer { ConnectionId = "c1", MemberNo = "viewer", ChannelId = "ch1" };
        session.Register(viewer);

        var cmd = new ViewRoomCommand(session);
        string? abortReason = null;
        var (ctx, sent) = CommandTestHelper.MakeContext(viewer,
            new Dictionary<string, object?> { ["roomId"] = room.RoomId, ["roomPwd"] = "", ["playerType"] = GKey.ValueViewer },
            reason => abortReason = reason);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
        Assert.Contains("missing tournament view key", abortReason);
    }

    [Fact]
    public async Task Execute_TournamentMemberMismatch_SendsTournamentViewError()
    {
        var session = new PlayerSessionService();
        var owner = new MajakPlayer { ConnectionId = "c2", MemberNo = "owner", ChannelId = "ch1" };
        session.Register(owner);
        var room = session.CreateRoom("ch1", owner, "", 1, 0, 0, false, subId: "00H5A");
        room.State = GameRoomState.Playing;
        room.TournamentSeqNo = 100;
        room.TournamentSubId = 2;

        var viewer = new MajakPlayer { ConnectionId = "c1", MemberNo = "viewer", ChannelId = "ch1" };
        session.Register(viewer);

        var cmd = new ViewRoomCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(viewer,
            new Dictionary<string, object?>
            {
                ["roomId"] = room.RoomId,
                ["roomPwd"] = "",
                ["playerType"] = GKey.ValueViewer,
                [Key.TournamentNo] = 100L,
                [Key.TournamentSubId] = 2,
                [Key.TournamentChkRoomMember] = "ghost|other",
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.ConnectTypeError, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(LegacyErrorCode.TournamentViewRoom, ((JsonElement)pkt["failCode"]!).GetInt32());
    }

    [Fact]
    public async Task Execute_TournamentMemberMatch_AllowsView()
    {
        var session = new PlayerSessionService();
        var owner = new MajakPlayer { ConnectionId = "c2", MemberNo = "owner", ChannelId = "ch1" };
        session.Register(owner);
        var room = session.CreateRoom("ch1", owner, "", 1, 0, 0, false, subId: "00H5A");
        room.State = GameRoomState.Playing;
        room.TournamentSeqNo = 100;
        room.TournamentSubId = 2;

        var viewer = new MajakPlayer { ConnectionId = "c1", MemberNo = "viewer", ChannelId = "ch1" };
        session.Register(viewer);

        var cmd = new ViewRoomCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(viewer,
            new Dictionary<string, object?>
            {
                ["roomId"] = room.RoomId,
                ["roomPwd"] = "",
                ["playerType"] = GKey.ValueViewer,
                [Key.TournamentNo] = 100L,
                [Key.TournamentSubId] = 2,
                [Key.TournamentChkRoomMember] = "ghost|owner",
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Contains(sent, s => s.method == Cmd.MemberList);
        Assert.Contains(sent, s => s.method == Cmd.AddMember);
        Assert.Contains(sent, s => s.method == Cmd.ViewRoom);
        Assert.True(viewer.IsViewer);
    }

    // シナリオ4: プライベートルーム + パスワード不一致 → failCode=3
    [Fact]
    public async Task Execute_WrongPassword_SendsEnterError()
    {
        var session = new PlayerSessionService();
        var owner   = new MajakPlayer { ConnectionId = "c2", MemberNo = "owner", ChannelId = "ch1" };
        session.Register(owner);
        var room = session.CreateRoom("ch1", owner, "", 1, 0, 0, true);
        room.Password  = "secret";
        room.State     = GameRoomState.Playing;

        var viewer = new MajakPlayer { ConnectionId = "c1", MemberNo = "viewer", ChannelId = "ch1" };
        session.Register(viewer);

        var cmd = new ViewRoomCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(viewer,
            new Dictionary<string, object?> { ["roomId"] = room.RoomId, ["roomPwd"] = "wrong", ["playerType"] = GKey.ValueViewer });

        await cmd.ExecuteAsync(ctx);

        Assert.Equal(Cmd.ConnectTypeError, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(LegacyErrorCode.InvalidPassword, ((JsonElement)pkt["failCode"]!).GetInt32());
    }

    // シナリオ5: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new ViewRoomCommand(new PlayerSessionService());
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}
