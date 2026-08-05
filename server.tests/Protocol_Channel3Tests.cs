using Moq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MajakServer.Commands;
using MajakServer.Commands.Channel;
using MajakServer.Infrastructure;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;
using System.Text.Json;

namespace MajakServer.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// CreateRoomCommand テスト
// 原典: HMajRoomServer::CreateRoom — circle room は keyCircleIdCnt/keyCircleIdN を読む
// ═══════════════════════════════════════════════════════════════════════════
public class CreateRoomCommandTests
{
    [Fact]
    public async Task Execute_WithLiveContinueRoom_DeniesCreatingOtherRoom()
    {
        const string channelId = "MAJAK200000001";
        var session = new PlayerSessionService();
        var player = new MajakPlayer
        {
            ConnectionId = "c1",
            MemberNo = "u1",
            ChannelId = channelId,
        };
        session.Register(player);

        var registry = new RoomRegistryService(TestMasterCacheFactory.CreateRedisService());
        var continueRoom = new GameRoom
        {
            RoomId = 101,
            ChannelId = channelId,
            RoomTitle = "continue room",
            RoomOption = "120000001000000",
            State = GameRoomState.Playing,
        };
        await registry.RegisterRoomAsync(continueRoom.RoomId, channelId, continueRoom.RoomTitle,
            isPrivate: false, memberCnt: 0, memberMax: 4,
            serverUrl: "http://test", roomOption: continueRoom.RoomOption);
        await registry.SetContinueRoomAsync(player.MemberNo, continueRoom);

        var repoMock = new Mock<PlayerRepository>(MockBehavior.Loose);
        repoMock.Setup(r => r.GetCupConfigsAsync()).ReturnsAsync(new List<CupConfig>());
        var cmd = new CreateRoomCommand(
            session,
            repoMock.Object,
            registry,
            TestMasterCacheFactory.Create(playerRepo: repoMock.Object),
            Microsoft.Extensions.Options.Options.Create(new ChannelServerSettings { ServerUrl = "http://test" }),
            new Mock<ILogger<CreateRoomCommand>>().Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(player, new Dictionary<string, object?>
        {
            [GKey.ChannelId] = player.ChannelId,
            [GKey.RoomOption] = "120000001000000",
            [GKey.RoomId] = 102,
        });

        await cmd.ExecuteAsync(ctx);

        Assert.Null(session.GetRoom(102));
        Assert.Contains(sent, x => x.method == Cmd.ConnectTypeError);
        Assert.DoesNotContain(sent, x => x.method == Cmd.RoomCreated);
    }

    [Fact]
    public async Task Execute_RequestedRoomIdWithNoActivePlayingRoom_ReturnsNotEmptyRoom()
    {
        const string channelId = "MAJAK200000001";
        var session = new PlayerSessionService();
        var staleOwner = new MajakPlayer
        {
            ConnectionId = "old",
            MemberNo = "oldHost",
            ChannelId = channelId,
            IsOutPlayer = true,
        };
        var staleRoom = session.CreateRoom(channelId, staleOwner, "", 1, 0, 0, false, roomId: 101);
        staleRoom.State = GameRoomState.Playing;

        var player = new MajakPlayer
        {
            ConnectionId = "c1",
            MemberNo = "host01",
            ChannelId = channelId,
        };
        session.Register(player);

        var repoMock = new Mock<PlayerRepository>(MockBehavior.Loose);
        repoMock.Setup(r => r.GetCupConfigsAsync()).ReturnsAsync(new List<CupConfig>());
        var cmd = new CreateRoomCommand(
            session,
            repoMock.Object,
            new RoomRegistryService(TestMasterCacheFactory.CreateRedisService()),
            TestMasterCacheFactory.Create(playerRepo: repoMock.Object),
            Microsoft.Extensions.Options.Options.Create(new ChannelServerSettings { ServerUrl = "http://test" }),
            new Mock<ILogger<CreateRoomCommand>>().Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(player, new Dictionary<string, object?>
        {
            [GKey.ChannelId] = player.ChannelId,
            [GKey.RoomOption] = "120000001000000",
            [GKey.RoomId] = 101,
        });

        await cmd.ExecuteAsync(ctx);

        var room = session.GetRoom(101)!;
        Assert.Same(staleRoom, room);
        Assert.Equal("oldHost", room.Seats[0]!.MemberNo);
        Assert.Contains(sent, x => x.method == Cmd.ConnectTypeError);
        Assert.DoesNotContain(sent, x => x.method == Cmd.RoomCreated);
    }

    [Fact]
    public async Task Execute_CircleRoomWithLegacyCircleKeys_StoresRequiredCircles()
    {
        var session = new PlayerSessionService();
        var player = new MajakPlayer
        {
            ConnectionId = "c1",
            MemberNo = "host01",
            ChannelId = "MAJAK200000001",
        };
        session.Register(player);

        var repoMock = new Mock<PlayerRepository>(MockBehavior.Loose);
        repoMock.Setup(r => r.GetCupConfigsAsync()).ReturnsAsync(new List<CupConfig>());
        repoMock.Setup(r => r.GetCircleInfoAsync("host01")).ReturnsAsync(new Dictionary<string, string>
        {
            ["circle01"] = "Circle One",
        });

        var cmd = new CreateRoomCommand(
            session,
            repoMock.Object,
            new RoomRegistryService(TestMasterCacheFactory.CreateRedisService()),
            TestMasterCacheFactory.Create(playerRepo: repoMock.Object),
            Microsoft.Extensions.Options.Options.Create(new ChannelServerSettings { ServerUrl = "http://test" }),
            new Mock<ILogger<CreateRoomCommand>>().Object);
        var (ctx, sent) = CommandTestHelper.MakeContext(player, new Dictionary<string, object?>
        {
            [GKey.ChannelId] = player.ChannelId,
            [GKey.SubId] = "00000",
            [GKey.RoomOption] = "120000001000000",
            [GKey.RoomId] = 101,
            [Key.CircleIdCnt] = 1,
            [$"{Key.CircleId}0"] = "circle01",
        });

        await cmd.ExecuteAsync(ctx);

        var room = session.GetRoom(101)!;
        Assert.Single(room.RequiredCircles);
        Assert.Equal("Circle One", room.RequiredCircles["circle01"]);
        Assert.Contains(sent, x => x.method == Cmd.RoomCreated);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc25e RatingRankInfoCommand テスト
// 原典: ProcessCommand_RatingRankInfo → AddToParser_GradeRankInfoResponse
//   → result=valueSuccess(1) + gradeRankList + gradeRankSelf + gradeSelectList + serverTime
//   → keyGradeRankDate=0 の場合は当月を使用
// ═══════════════════════════════════════════════════════════════════════════
public class RatingRankInfoCommandTests
{
    private readonly Mock<PlayerRepository> _playerRepoMock
        = new(MockBehavior.Loose);

    private static GradeRankService CreateGradeRankService() => new(
        new Mock<IServiceScopeFactory>().Object,
        new Mock<ILogger<GradeRankService>>().Object);

    private RatingRankInfoCommand CreateCommand(MajakPlayer? player = null)
    {
        var session = new PlayerSessionService();
        if (player != null) session.Register(player);
        return new RatingRankInfoCommand(_playerRepoMock.Object, CreateGradeRankService(), session);
    }

    private void SetupMocks(
        List<GradeRankItem>?   rankList   = null,
        GradeRankItem?         selfItem   = null,
        List<GradeSelectItem>? selectList = null)
    {
        _playerRepoMock
            .Setup(r => r.GetGradeRankListAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(rankList ?? new());

        _playerRepoMock
            .Setup(r => r.GetGradeRankSelfAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(selfItem);

        _playerRepoMock
            .Setup(r => r.GetGradeManageListAsync())
            .ReturnsAsync(selectList ?? new());
    }

    // シナリオ1: ランキングデータあり → result=1 + 各リスト返却
    [Fact]
    public async Task Execute_WithRankData_Returns1WithLists()
    {
        SetupMocks(
            rankList: new()
            {
                new GradeRankItem { MemberNo = "user01", Rating = 2000, Grade = 5, Rank = 1 },
                new GradeRankItem { MemberNo = "user02", Rating = 1800, Grade = 4, Rank = 2 },
            },
            selfItem: new GradeRankItem { MemberNo = "me01", Rating = 1500, Rank = 5 },
            selectList: new()
            {
                new GradeSelectItem { DispOrder = 1, YearMonth = 202504, YearMonthStr = "2025年4月" },
            }
        );

        var player = new MajakPlayer { MemberNo = "me01" };
        var cmd    = CreateCommand(player);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [Key.GradeRankId]      = 1,
                [Key.GradeRankDate]    = 202504,
                [Key.GradeRankRefresh] = 1,
                [GKey.Pix]         = "me01",
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.RatingRankInfo, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(1, ((JsonElement)pkt["result"]!).GetInt32());
        Assert.Equal(2, ((JsonElement)pkt["gradeRankCnt"]!).GetInt32());
        Assert.Equal(1, ((JsonElement)pkt["gradeSelectCnt"]!).GetInt32());
        Assert.True(pkt.ContainsKey("serverTime"));
    }

    // シナリオ2: データなし → result=1 + カウント0
    [Fact]
    public async Task Execute_NoData_Returns1WithZeroCounts()
    {
        SetupMocks();
        var player = new MajakPlayer { MemberNo = "me01" };
        var cmd    = CreateCommand(player);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [Key.GradeRankId]      = 0,
                [Key.GradeRankDate]    = 202504,
                [Key.GradeRankRefresh] = 1,
                [GKey.Pix]         = "me01",
            });

        await cmd.ExecuteAsync(ctx);

        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(1, ((JsonElement)pkt["result"]!).GetInt32());
        Assert.Equal(0, ((JsonElement)pkt["gradeRankCnt"]!).GetInt32());
    }

    // シナリオ3: rankDate=0 → 原典は invalid parameter で応答なし
    [Fact]
    public async Task Execute_RankDateZero_NothingSent()
    {
        SetupMocks();
        var player = new MajakPlayer { MemberNo = "me01" };
        var cmd    = CreateCommand(player);
        string? abortReason = null;
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [Key.GradeRankId]      = 1,
                [Key.GradeRankDate]    = 0,
                [Key.GradeRankRefresh] = 1,
                [GKey.Pix]         = "me01",
            }, reason => abortReason = reason);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
        Assert.Contains("RatingRankInfo invalid parameter", abortReason);
        _playerRepoMock.Verify(r =>
            r.GetGradeRankListAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    // シナリオ4: 自分のエントリに isSelf=1 フラグが設定されること
    [Fact]
    public async Task Execute_SelfFlag_MarkedCorrectly()
    {
        SetupMocks(
            rankList: new()
            {
                new GradeRankItem { MemberNo = "me01",  Rating = 2000, Grade = 5, Rank = 1 },
                new GradeRankItem { MemberNo = "other", Rating = 1800, Grade = 4, Rank = 2 },
            }
        );
        var player = new MajakPlayer { MemberNo = "me01" };
        var cmd    = CreateCommand(player);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [Key.GradeRankId]      = 1,
                [Key.GradeRankDate]    = 202504,
                [Key.GradeRankRefresh] = 1,
                [GKey.Pix]         = "me01",
            });

        await cmd.ExecuteAsync(ctx);

        var pkt      = CommandTestHelper.ToDict(sent[0].packet);
        var listJson = ((JsonElement)pkt["gradeRankList"]!).GetRawText();
        var list     = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(listJson)!;
        Assert.Equal(1, list[0]["isSelf"].GetInt32());
        Assert.Equal(0, list[1]["isSelf"].GetInt32());
    }

    [Theory]
    [InlineData("", 202504, 1)]
    [InlineData("other", 202504, 1)]
    [InlineData("me01", 202504, 0)]
    public async Task Execute_InvalidLegacyParameters_NothingSent(
        string requestPix,
        int rankDate,
        int rankRefresh)
    {
        SetupMocks();
        var player = new MajakPlayer { MemberNo = "me01" };
        var cmd = CreateCommand(player);
        string? abortReason = null;
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [Key.GradeRankId] = 1,
                [Key.GradeRankDate] = rankDate,
                [Key.GradeRankRefresh] = rankRefresh,
                [GKey.Pix] = requestPix,
            }, reason => abortReason = reason);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
        Assert.Contains("RatingRankInfo invalid parameter", abortReason);
        _playerRepoMock.Verify(r =>
            r.GetGradeRankListAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    // シナリオ5: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        SetupMocks();
        var cmd = CreateCommand();
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// GetRoomListCommand テスト
// 原典: ProcessCommand_GetRoomList
// ═══════════════════════════════════════════════════════════════════════════
public class GetRoomListCommandTests
{
    // シナリオ1: チャンネル内ルームなし → result=1 + count=0
    [Fact]
    public async Task Execute_NoRooms_ReturnsEmptyList()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);

        var cmd = new GetRoomListCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.GetRoomList, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(1, ((JsonElement)pkt["result"]!).GetInt32());
        Assert.Equal(0, ((JsonElement)pkt["count"]!).GetInt32());
    }

    // シナリオ2: ルームあり → count=1
    [Fact]
    public async Task Execute_WithRoom_ReturnsCount1()
    {
        var session = new PlayerSessionService();
        var owner   = new MajakPlayer { ConnectionId = "c2", MemberNo = "owner", ChannelId = "ch1" };
        session.Register(owner);
        session.CreateRoom("ch1", owner, "", 1, 0, 0, false);

        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);

        var cmd = new GetRoomListCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(1, ((JsonElement)pkt["count"]!).GetInt32());
    }

    [Fact]
    public async Task Execute_PendingAutoMatchRoom_NotIncluded()
    {
        var session = new PlayerSessionService();
        var owner   = new MajakPlayer { ConnectionId = "c2", MemberNo = "owner", ChannelId = "ch1" };
        session.Register(owner);
        var room = session.CreateRoom("ch1", owner, "", 1, 0, 0, false);
        session.RegisterPendingMatch(new PendingAutoMatch
        {
            RoomId = room.RoomId,
            ChannelId = "ch1",
            ExpectedMembers = new[] { "owner" },
        });

        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);

        var cmd = new GetRoomListCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(0, ((JsonElement)pkt["count"]!).GetInt32());
    }

    [Fact]
    public async Task Execute_RegistryOnlyPlayingRoom_Included()
    {
        var session = new PlayerSessionService();
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);

        var registry = new RoomRegistryService(TestMasterCacheFactory.CreateRedisService());
        await registry.RegisterRoomAsync(
            roomId: 7,
            chanelId: "ch1",
            title: "grade match",
            isPrivate: false,
            memberCnt: 4,
            memberMax: 4,
            serverUrl: "http://game",
            roomOption: "120000001000000",
            maxViewer: 12,
            roomState: RoomStatePayload.LegacyRoomGameView,
            roomPlaying: 1);

        var cmd = new GetRoomListCommand(session, roomRegistry: registry);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(1, ((JsonElement)pkt["count"]!).GetInt32());

        var room = ((JsonElement)pkt["rooms"]!).EnumerateArray().Single();
        Assert.Equal(7, room.GetProperty("roomId").GetInt32());
        Assert.Equal(RoomStatePayload.LegacyRoomGameView, room.GetProperty("state").GetInt32());
        Assert.Equal(1, room.GetProperty("roomPlaying").GetInt32());
        Assert.Equal("http://game", room.GetProperty("serverUrl").GetString());
    }

    // シナリオ3: 別チャンネルのルームは含まない
    [Fact]
    public async Task Execute_OtherChannelRoom_NotIncluded()
    {
        var session = new PlayerSessionService();
        var owner2  = new MajakPlayer { ConnectionId = "c3", MemberNo = "owner2", ChannelId = "ch2" };
        session.Register(owner2);
        session.CreateRoom("ch2", owner2, "", 1, 0, 0, false);

        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);

        var cmd = new GetRoomListCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(0, ((JsonElement)pkt["count"]!).GetInt32());
    }

    // シナリオ4: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var session = new PlayerSessionService();
        var cmd = new GetRoomListCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// GetMemberListCommand テスト
// 原典: ProcessCommand_GetMemberList
// ═══════════════════════════════════════════════════════════════════════════
public class GetMemberListCommandTests
{
    // シナリオ1: ロビーメンバー2人 → count=2
    [Fact]
    public async Task Execute_TwoLobbyMembers_ReturnsBoth()
    {
        var session = new PlayerSessionService();
        var p1 = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", NickName = "Alice", ChannelId = "ch1", Rating = 1500, NLevel = 3 };
        var p2 = new MajakPlayer { ConnectionId = "c2", MemberNo = "u2", NickName = "Bob",   ChannelId = "ch1", Rating = 1200, NLevel = 2 };
        session.Register(p1);
        session.Register(p2);

        var cmd = new GetMemberListCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(p1);

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.GetMemberList, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(1, ((JsonElement)pkt["result"]!).GetInt32());
        Assert.Equal(2, ((JsonElement)pkt["count"]!).GetInt32());
    }

    // シナリオ2: ルーム在室メンバーも接続者リストに含め、位置に部屋番号を表示する
    // 原典: HgMemberListWnd は full member list で IDS_CHANNEL_MEMLIST_ROOMNO を表示する
    [Fact]
    public async Task Execute_RoomMemberIncludedWithRoomLocation()
    {
        var session = new PlayerSessionService();
        var lobby = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        var owner = new MajakPlayer { ConnectionId = "c2", MemberNo = "u2", ChannelId = "ch1" };
        session.Register(lobby);
        session.Register(owner);
        session.CreateRoom("ch1", owner, "", 1, 0, 0, false); // owner はルーム入室

        var cmd = new GetMemberListCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(lobby);

        await cmd.ExecuteAsync(ctx);

        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(2, ((JsonElement)pkt["count"]!).GetInt32());
        var members = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(
            ((JsonElement)pkt["members"]!).GetRawText())!;
        var ownerMember = Assert.Single(members, member => member["memberNo"].GetString() == owner.Pix);
        Assert.NotEqual(owner.MemberNo, ownerMember["memberNo"].GetString());
        Assert.Equal(1, ownerMember["roomId"].GetInt32());
        Assert.Equal("1番部屋", ownerMember["location"].GetString());
    }

    // シナリオ3: メンバーフィールド確認
    [Fact]
    public async Task Execute_FieldsIncluded()
    {
        var session = new PlayerSessionService();
        var p1 = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", NickName = "Alice", ChannelId = "ch1", RoomId = null };
        session.Register(p1);

        var cmd = new GetMemberListCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(p1);
        await cmd.ExecuteAsync(ctx);

        var pkt     = CommandTestHelper.ToDict(sent[0].packet);
        var members = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(
            ((JsonElement)pkt["members"]!).GetRawText())!;
        Assert.Equal(p1.Pix, members[0]["memberNo"].GetString());
        Assert.NotEqual(p1.MemberNo, members[0]["memberNo"].GetString());
        Assert.Equal("Alice", members[0]["nickname"].GetString());
        Assert.Equal("ロビー", members[0]["location"].GetString());
    }

    [Fact]
    public async Task Execute_AutoMatchingChannel_IncludesLegacyMemberInfoString()
    {
        var session = new PlayerSessionService();
        var player = new MajakPlayer
        {
            ConnectionId = "c1",
            MemberNo = "u1",
            NickName = "Alice",
            ChannelId = "MAJAK20ZG6A001",
            Sex = "F",
            NLevel = 4,
            GamMoney = 12000,
            DispRange = 2,
        };
        player.ActiveRecord.MatchCnt = 10;
        player.ActiveRecord.WinCnt = 4;
        player.ActiveRecord.DefeatCnt = 5;
        player.ActiveRecord.DrawCnt = 1;
        player.ActiveRecord.Rating = 1530;
        player.GradeRecord.Grade = 8;
        session.Register(player);

        var cmd = new GetMemberListCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        string legacyInfo = ((JsonElement)pkt[$"{GKey.Pix}0"]!).GetString()!;
        string[] fields = legacyInfo.Split('\t');
        Assert.Equal("u1", fields[0]);
        Assert.Equal("F", fields[1]);
        Assert.Equal("ロビー", fields[2]);
        Assert.Equal("10", fields[3]);
        Assert.Equal("1530", fields[7]);
        Assert.Equal("12000", fields[9]);
        Assert.Equal("Alice", fields[17]);
        Assert.Equal("8", fields[18]);
    }

    // シナリオ4: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var session = new PlayerSessionService();
        var cmd = new GetMemberListCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// ExitChannelCommand テスト
// 原典: ProcessCommand_ExitChannel
//   → チャンネルグループから削除 → 残メンバーへ channel:member_left 通知 → result=1
// ═══════════════════════════════════════════════════════════════════════════
public class ExitChannelCommandTests
{
    // シナリオ1: ロビー状態で退場 → result=1 + channel:member_left
    [Fact]
    public async Task Execute_InLobby_SendsResult1AndNotify()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1", RoomId = null };
        session.Register(player);

        var cmd = new ExitChannelCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Equal(2, sent.Count);
        Assert.Contains(sent, s => s.method == Cmd.ExitChannel);
        Assert.Contains(sent, s => s.method == Cmd.DeleteMember);
        var exitPkt = CommandTestHelper.ToDict(sent.First(s => s.method == Cmd.ExitChannel).packet);
        Assert.Equal(1, ((JsonElement)exitPkt["result"]!).GetInt32());
    }

    [Fact]
    public async Task Execute_AutoMatchingLobby_RemovesWaitingPlayer()
    {
        const string channelId = "MAJAK20ZG6A001";
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "user01", ChannelId = channelId, RoomId = null };
        session.Register(player);
        for (int i = 1; i <= 4; i++)
        {
            session.EnqueueMatching(channelId, $"user0{i}");
        }

        var cmd = new ExitChannelCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Contains(sent, s => s.method == Cmd.ExitChannel);
        Assert.Null(session.TryMatch(channelId, _ => 1500));
    }

    // シナリオ2: ルーム在室で退場 → DeleteMember + channel:member_left + ExitChannel
    [Fact]
    public async Task Execute_InRoom_SendsDeleteMemberAndExit()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);
        session.CreateRoom("ch1", player, "", 1, 0, 0, false); // player は RoomId を持つ

        var cmd = new ExitChannelCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Contains(sent, s => s.method == Cmd.DeleteMember);
        Assert.Contains(sent, s => s.method == Cmd.ExitChannel);
        Assert.Contains(sent, s => s.method == Cmd.DeleteMember);
    }

    // シナリオ3: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var session = new PlayerSessionService();
        var cmd = new ExitChannelCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// HanChatAllRelayCommand テスト
// 原典: ProcessCommand_HanChatAllRelay — チャンネル全員へ中継
// ═══════════════════════════════════════════════════════════════════════════
public class HanChatAllRelayCommandTests
{
    // シナリオ1: メッセージあり → チャンネル全員に中継
    [Fact]
    public async Task Execute_WithMessage_RelaysToChannel()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);

        var cmd = new HanChatAllRelayCommand();
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { ["string"] = "Hello!" });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.HanChatRelay, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(player.Pix, ((JsonElement)pkt["memberNo"]!).GetString());
        Assert.NotEqual(player.MemberNo, ((JsonElement)pkt["memberNo"]!).GetString());
        Assert.Equal("Hello!", ((JsonElement)pkt["string"]!).GetString());
        Assert.Equal(GKey.ValueAll, ((JsonElement)pkt["target"]!).GetString());
    }

    // シナリオ1b: レガシー keyString(k41e) 入力でも中継
    [Fact]
    public async Task Execute_WithLegacyStringKey_RelaysToChannel()
    {
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        var cmd = new HanChatAllRelayCommand();
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.String] = "Legacy hello" });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.HanChatRelay, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal("u1", ((JsonElement)pkt[GKey.Pix]!).GetString());
        Assert.Equal("Legacy hello", ((JsonElement)pkt[GKey.String]!).GetString());
        Assert.Equal("Legacy hello", ((JsonElement)pkt["string"]!).GetString());
    }

    // シナリオ1c: 管理者の ADMIN? は room ProcessService_Chatting と同じく応答文字列へ置換
    [Fact]
    public async Task Execute_AdminProbeFromAdmin_RewritesMessage()
    {
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "admin01", ChannelId = "ch1", IsAdminId = true };
        var cmd = new HanChatAllRelayCommand();
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.String] = "ADMIN?" });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.HanChatRelay, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal("admin user", ((JsonElement)pkt[GKey.String]!).GetString());
        Assert.Equal("admin user", ((JsonElement)pkt["string"]!).GetString());
    }

    // シナリオ1d: 管理者の配牌デバッグコマンドは engine flag と応答文字列に反映
    [Fact]
    public async Task Execute_AdminHaipaiDebugCommand_SetsRoomEngineAndRewritesMessage()
    {
        var session = new PlayerSessionService();
        var player = new MajakPlayer
        {
            ConnectionId = "c1",
            MemberNo = "admin01",
            ChannelId = "ch1",
            IsAdminId = true,
        };
        session.Register(player);
        var room = session.CreateRoom("ch1", player, "", 1, 0, 0, false, roomId: 17);
        var cmd = new HanChatAllRelayCommand(session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.String] = "!HPKOKU" });

        await cmd.ExecuteAsync(ctx);

        Assert.Equal(1008, room.Engine.DebugHaipaiYaku);
        Assert.Single(sent);
        Assert.Equal(Cmd.HanChatRelay, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal("配牌:国士無双(聴牌) To 親.", ((JsonElement)pkt[GKey.String]!).GetString());
        Assert.Equal("配牌:国士無双(聴牌) To 親.", ((JsonElement)pkt["string"]!).GetString());
    }

    [Fact]
    public async Task Execute_AdminRankForceCommand_SendsCallerDebugResponseOnly()
    {
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "admin01", Pix = "pix-admin01", ChannelId = "ch1", IsAdminId = true };
        var cmd = new HanChatAllRelayCommand();
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.String] = "!RANKFORCED" });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.HanChatRelay, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(player.Pix, ((JsonElement)pkt[GKey.Pix]!).GetString());
        Assert.NotEqual(player.MemberNo, ((JsonElement)pkt[GKey.Pix]!).GetString());
        Assert.NotEqual("!RANKFORCED", ((JsonElement)pkt[GKey.String]!).GetString());
    }

    [Fact]
    public async Task Execute_AdminRatingRankDebugCommand_RoutesToRatingRankInfo()
    {
        var repoMock = new Mock<PlayerRepository>(MockBehavior.Loose);
        repoMock.Setup(r => r.GetGradeRankListAsync(202407, 99, 30)).ReturnsAsync(new List<GradeRankItem>());
        repoMock.Setup(r => r.GetGradeManageListAsync()).ReturnsAsync(new List<GradeSelectItem>());
        var session = new PlayerSessionService();
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "admin01", ChannelId = "ch1", IsAdminId = true };
        session.Register(player);
        var ratingCommand = new RatingRankInfoCommand(repoMock.Object, new GradeRankService(
            new Mock<IServiceScopeFactory>().Object,
            new Mock<ILogger<GradeRankService>>().Object), session);
        var cmd = new HanChatAllRelayCommand(session, ratingCommand);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [GKey.String] = "!RQRRI,202407,99,1" });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.RatingRankInfo, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(202407, ((JsonElement)pkt["rankDate"]!).GetInt32());
        Assert.Equal(99, ((JsonElement)pkt["rankId"]!).GetInt32());
    }

    // シナリオ1e: 対局室内チャットは room seats の live connection に中継
    [Fact]
    public async Task Execute_InRoom_RelaysToRoomRecipients()
    {
        var session = new PlayerSessionService();
        var p0 = new MajakPlayer { ConnectionId = "c0", MemberNo = "u0", ChannelId = "ch1" };
        var p1 = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(p0);
        session.Register(p1);
        var room = session.CreateRoom("ch1", p0, "120000001000000", 1, 0, 0, false, roomId: 7);
        room.AddPlayer(p1, 1);

        IReadOnlyList<string>? recipients = null;
        var sent = new List<(string method, object packet)>();
        var roomProxy = new Mock<IClientProxy>();
        roomProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((method, args, _) => sent.Add((method, args[0]!)))
            .Returns(Task.CompletedTask);
        var clientsMock = new Mock<IHubCallerClients>();
        clientsMock.Setup(c => c.Group(It.IsAny<string>()))
            .Throws(new InvalidOperationException("room chat should not depend on SignalR room group membership"));
        clientsMock.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>()))
            .Callback<IReadOnlyList<string>>(list => recipients = list)
            .Returns(roomProxy.Object);
        var ctx = new CommandContext
        {
            ConnectionId = p0.ConnectionId,
            Player = p0,
            Clients = clientsMock.Object,
            Payload = new Dictionary<string, object?> { [GKey.String] = "Room hello" },
        };

        await new HanChatAllRelayCommand(session).ExecuteAsync(ctx);

        Assert.Equal(new[] { "c0", "c1" }, recipients?.OrderBy(x => x).ToArray());
        Assert.Single(sent);
        Assert.Equal(Cmd.HanChatRelay, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(p0.Pix, ((JsonElement)pkt[GKey.Pix]!).GetString());
        Assert.NotEqual(p0.MemberNo, ((JsonElement)pkt[GKey.Pix]!).GetString());
        Assert.Equal("Room hello", ((JsonElement)pkt[GKey.String]!).GetString());
        Assert.Equal("Room hello", ((JsonElement)pkt["string"]!).GetString());
    }

    [Fact]
    public async Task Execute_InRoom_RelaysToLegacyTenRoomGroup()
    {
        var session = new PlayerSessionService();
        var p7 = new MajakPlayer { ConnectionId = "c7", MemberNo = "u7", ChannelId = "ch1" };
        var p9 = new MajakPlayer { ConnectionId = "c9", MemberNo = "u9", ChannelId = "ch1" };
        var p11 = new MajakPlayer { ConnectionId = "c11", MemberNo = "u11", ChannelId = "ch1" };
        session.Register(p7);
        session.Register(p9);
        session.Register(p11);
        session.CreateRoom("ch1", p7, "120000001000000", 1, 0, 0, false, roomId: 7);
        session.CreateRoom("ch1", p9, "120000001000000", 1, 0, 0, false, roomId: 9);
        session.CreateRoom("ch1", p11, "120000001000000", 1, 0, 0, false, roomId: 11);

        IReadOnlyList<string>? recipients = null;
        var sent = new List<(string method, object packet)>();
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((method, args, _) => sent.Add((method, args[0]!)))
            .Returns(Task.CompletedTask);
        var clientsMock = new Mock<IHubCallerClients>();
        clientsMock.Setup(c => c.Groups(It.IsAny<IReadOnlyList<string>>()))
            .Throws(new InvalidOperationException("room chat should not depend on SignalR room group membership"));
        clientsMock.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>()))
            .Callback<IReadOnlyList<string>>(list => recipients = list)
            .Returns(proxy.Object);
        var ctx = new CommandContext
        {
            ConnectionId = p7.ConnectionId,
            Player = p7,
            Clients = clientsMock.Object,
            Payload = new Dictionary<string, object?> { [GKey.String] = "Group hello" },
        };

        await new HanChatAllRelayCommand(session).ExecuteAsync(ctx);

        Assert.Equal(new[] { "c7", "c9" }, recipients?.OrderBy(x => x).ToArray());
        Assert.DoesNotContain("c11", recipients ?? Array.Empty<string>());
        Assert.Single(sent);
        Assert.Equal(Cmd.HanChatRelay, sent[0].method);
    }

    // シナリオ2: メッセージが空 → 何も送らない
    [Fact]
    public async Task Execute_EmptyMessage_NothingSent()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);

        var cmd = new HanChatAllRelayCommand();
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { ["string"] = "" });

        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    // シナリオ3: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new HanChatAllRelayCommand();
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}
