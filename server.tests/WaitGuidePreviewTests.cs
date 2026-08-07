using MajakServer.Engine;
using MajakServer.Hubs;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Services;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace MajakServer.Tests;

public class WaitGuidePreviewTests
{
    private static RuleInfo DefaultRule() => new()
    {
        Hanchan = true,
        Kuitan = true,
        Contest = 0,
        AkaDora = 1,
        Uma = 0,
    };

    private static PaiCode Tile(int serial, int bipaiIndex)
    {
        PaiCode tile = PaiCode.MakeSerial(serial);
        tile.BipaiIndex = bipaiIndex;
        return tile;
    }

    private static MajakGameLogic CreateLogic(params int[] serials)
    {
        var logic = new MajakGameLogic();
        logic.InitHanchan(DefaultRule());
        EnginePlayer player = logic.Player[0];
        player.Tehai.Clear();
        for (int index = 0; index < serials.Length; index++)
            player.Tehai.Add(Tile(serials[index], index + 1));
        player.Mode = PlayerMode.Turn;
        return logic;
    }

    [Fact]
    public void EvaluateWaitGuide_TanyaoWait_ReturnsHanWithoutMutatingHand()
    {
        MajakGameLogic logic = CreateLogic(1, 2, 3, 4, 5, 6, 10, 11, 12, 13, 14, 15, 25, 18);
        int[] before = logic.Player[0].Tehai.Select(tile => tile.BipaiIndex).ToArray();

        WaitGuidePreview? preview = logic.EvaluateWaitGuide(0, discardBipaiIndex: 14);

        Assert.NotNull(preview);
        WaitGuideYakuEntry wait = Assert.Single(preview.Waits, entry => entry.Serial == 25);
        Assert.True(wait.Han >= 1);
        Assert.False(wait.NoYaku);
        Assert.False(wait.IsYakuman);
        Assert.Equal(before, logic.Player[0].Tehai.Select(tile => tile.BipaiIndex));
    }

    [Fact]
    public void EvaluateWaitGuide_OpenHandWithoutYaku_ReturnsNoYaku()
    {
        MajakGameLogic logic = CreateLogic(1, 2);
        EnginePlayer player = logic.Player[0];
        PaiCode claimed = Tile(0, 120);
        Assert.Equal(ActionResult.Ok, player.Chi(1, claimed, new[] { 1, 2 }));
        player.Tehai.Clear();
        int[] concealed = { 3, 4, 5, 10, 11, 12, 24, 25, 26, 27, 18 };
        for (int index = 0; index < concealed.Length; index++)
            player.Tehai.Add(Tile(concealed[index], index + 20));
        player.Mode = PlayerMode.Turn;

        WaitGuidePreview? preview = logic.EvaluateWaitGuide(0, discardBipaiIndex: 30);

        Assert.NotNull(preview);
        WaitGuideYakuEntry wait = Assert.Single(preview.Waits, entry => entry.Serial == 27);
        Assert.Equal(0, wait.Han);
        Assert.True(wait.NoYaku);
        Assert.False(wait.IsYakuman);
    }

    [Fact]
    public void EvaluateWaitGuide_KokushiWait_ReturnsYakuman()
    {
        MajakGameLogic logic = CreateLogic(0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33, 1);

        WaitGuidePreview? preview = logic.EvaluateWaitGuide(0, discardBipaiIndex: 14);

        Assert.NotNull(preview);
        WaitGuideYakuEntry wait = Assert.Single(preview.Waits, entry => entry.Serial == 0);
        Assert.True(wait.IsYakuman);
        Assert.False(wait.NoYaku);
    }

    [Fact]
    public void EvaluateWaitGuide_RejectsNonTurnAndUnknownDiscard()
    {
        MajakGameLogic logic = CreateLogic(1, 2, 3, 4, 5, 6, 10, 11, 12, 13, 14, 15, 25, 18);

        Assert.Null(logic.EvaluateWaitGuide(0, discardBipaiIndex: 99));
        logic.Player[0].Mode = PlayerMode.Furo;
        Assert.Null(logic.EvaluateWaitGuide(0, discardBipaiIndex: 14));
    }
}

public class WaitGuidePreviewHubTests
{
    private static MajakGameHub CreateHub(PlayerSessionService session, string connectionId)
    {
        var context = new Mock<HubCallerContext>();
        context.SetupGet(value => value.ConnectionId).Returns(connectionId);
        return new MajakGameHub(session, null!, null!, null!, null!, null!)
        {
            Context = context.Object,
        };
    }

    [Fact]
    public async Task GetWaitGuidePreview_RejectsInvalidSessions()
    {
        var session = new PlayerSessionService();
        Assert.Null(await CreateHub(session, "missing").GetWaitGuidePreview(7, 14));

        var viewer = new MajakPlayer
        {
            ConnectionId = "viewer",
            MemberNo = "viewer-user",
            RoomId = 7,
            EngineOrder = 0,
            IsViewer = true,
        };
        session.Register(viewer);
        Assert.Null(await CreateHub(session, viewer.ConnectionId).GetWaitGuidePreview(7, 14));

        var wrongRoom = new MajakPlayer
        {
            ConnectionId = "wrong-room",
            MemberNo = "wrong-room-user",
            RoomId = 7,
            EngineOrder = 0,
        };
        session.Register(wrongRoom);
        Assert.Null(await CreateHub(session, wrongRoom.ConnectionId).GetWaitGuidePreview(8, 14));

        var stale = new MajakPlayer
        {
            ConnectionId = "stale",
            MemberNo = "reconnected-user",
            RoomId = 7,
            EngineOrder = 0,
        };
        session.Register(stale);
        session.Register(new MajakPlayer
        {
            ConnectionId = "current",
            MemberNo = stale.MemberNo,
            RoomId = 7,
            EngineOrder = 0,
        });
        Assert.Null(await CreateHub(session, stale.ConnectionId).GetWaitGuidePreview(7, 14));
    }

    [Fact]
    public async Task GetWaitGuidePreview_ValidPlayer_ReturnsPreviewWithoutMutatingHand()
    {
        var session = new PlayerSessionService();
        var player = new MajakPlayer
        {
            ConnectionId = "c1",
            MemberNo = "u1",
            ChannelId = "ch1",
            EngineOrder = 0,
        };
        session.Register(player);
        GameRoom room = session.CreateRoom("ch1", player, "", 1, 0, 0, false);
        player.EngineOrder = 0;
        room.State = GameRoomState.Playing;
        room.Engine.InitHanchan(new RuleInfo
        {
            Hanchan = true,
            Kuitan = true,
            Contest = 0,
            AkaDora = 1,
            Uma = 0,
        });
        EnginePlayer enginePlayer = room.Engine.Player[0];
        enginePlayer.Tehai.Clear();
        int[] serials = { 1, 2, 3, 4, 5, 6, 10, 11, 12, 13, 14, 15, 25, 18 };
        for (int index = 0; index < serials.Length; index++)
        {
            PaiCode tile = PaiCode.MakeSerial(serials[index]);
            tile.BipaiIndex = index + 1;
            enginePlayer.Tehai.Add(tile);
        }
        enginePlayer.Mode = PlayerMode.Turn;
        int[] before = enginePlayer.Tehai.Select(tile => tile.BipaiIndex).ToArray();

        object? response = await CreateHub(session, player.ConnectionId)
            .GetWaitGuidePreview(room.RoomId, discardBipaiIndex: 14);

        WaitGuidePreview preview = Assert.IsType<WaitGuidePreview>(response);
        Assert.Contains(preview.Waits, wait => wait.Serial == 25 && wait.Han >= 1);
        Assert.Equal(before, enginePlayer.Tehai.Select(tile => tile.BipaiIndex));
    }
}