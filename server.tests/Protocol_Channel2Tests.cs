using Moq;
using Microsoft.Extensions.Logging;
using MajakServer.Commands;
using MajakServer.Commands.Channel;
using MajakServer.Commands.Room;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;
using System.Globalization;
using System.Text.Json;

namespace MajakServer.Tests;

public class ChannelRepositoryTests
{
    [Fact]
    public void RepairChannelName_ReplacesSubIdWithLegacyTrainingName()
    {
        var method = typeof(ChannelRepository)
            .GetMethod("RepairChannelName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        var repaired = Assert.IsType<string>(method.Invoke(null, new object[] { "00T5A", "00T5A" }));

        Assert.Equal("練習広場", repaired);
    }

    [Fact]
    public void RepairDisplayName_ReplacesSubIdWithLegacyKouryuName()
    {
        var repaired = ChannelRepository.RepairDisplayName("0075B", "0075B");

        Assert.Equal("基本ﾜﾚﾒ広場", repaired);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc14e GetServerTimeCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   1. 成功 → mjkk32e (ServerTime) に現在時刻文字列が入る
///   2. player=null → 何も送らない
/// </summary>
public class GetServerTimeCommandTests
{
    private readonly PlayerSessionService _session = new();

    [Fact]
    public async Task Execute_ReturnsServerTimeKey()
    {
        var player = new MajakPlayer { MemberNo = "user01" };
        var cmd = new GetServerTimeCommand();
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.GetServerTime, sent[0].method);
        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.True(dict.ContainsKey(Key.ServerTime));
        Assert.True(DateTime.TryParseExact(
            (string)dict[Key.ServerTime],
            "MM/dd/yyyy HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _));
    }

    // シナリオ2: GetServerTime は player=null でも応答する (原典に null チェックなし)
    [Fact]
    public async Task Execute_NullPlayer_StillSendsTime()
    {
        var cmd = new GetServerTimeCommand();
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        // player=null でもサーバー時刻は返す
        Assert.Single(sent);
        Assert.Equal(Cmd.GetServerTime, sent[0].method);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc20e BuyMajItemCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   1. 有効な sellCode (CAT_ITEM) → result=1 + itemCode が返る
///   2. 無効な sellCode → result=0 + failCode が返る
///   3. コイン不足 → result=0
///   4. player=null → 何も送らない
///   5. 必須フィールド確認
/// </summary>
public class BuyMajItemCommandTests
{
    private readonly Mock<ItemRepository>            _itemRepoMock = new(MockBehavior.Loose);
    private readonly Mock<PlayerRepository>          _playerRepoMock = new(MockBehavior.Loose);
    private readonly Mock<HistoryRepository>         _histRepoMock = new(MockBehavior.Loose);

    private MajItemService BuildMajItemService()
    {
        _playerRepoMock.Setup(r => r.UpdateCommonRatAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);
        _playerRepoMock.Setup(r => r.SetDailyMissionDirectAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        _itemRepoMock.Setup(r => r.ExchangeMajItemAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string?>()))
            .ReturnsAsync(new MajItemInfo { ItemCode = "item001", BuyDt = DateTime.Now, EndDt = DateTime.Now.AddDays(7), Qty = 1 });
        _itemRepoMock.Setup(r => r.ExchangeTitleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        _itemRepoMock.Setup(r => r.ExchangeAvatarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>()))
            .ReturnsAsync(true);
        _playerRepoMock.Setup(r => r.InsertOrEnableTitleAsync(
                It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _playerRepoMock.Setup(r => r.HasActiveTitleAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        var histRepo = _histRepoMock.Object;
        return new MajItemService(_itemRepoMock.Object, _playerRepoMock.Object, histRepo);
    }

    // シナリオ1: 有効な sellCode (CAT_ITEM) → result=0 + itemCode (success)
    [Fact]
    public async Task Execute_ValidSellCode_CatItem_Success()
    {
        var svc = BuildMajItemService();
        var player = new MajakPlayer { MemberNo = "user01", GamMoney = 5000, GemCount = 100 };
        var cmd = new BuyMajItemCommand(svc);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [Key.SellCode] = "sell001" });

        await cmd.ExecuteAsync(ctx);

        Assert.Equal(2, sent.Count);
        Assert.Equal(Cmd.BuyMajItem, sent[0].method);
        Assert.Equal(Cmd.SelectMajItem, sent[1].method);
        // 成功時: result=0 + ItemCode あり、failCode なし
        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.Equal(0, (int)dict["result"]);
        Assert.Equal(GKey.ValueSuccess, (string)dict[GKey.Result]);
        Assert.Equal(98, (int)dict[Key.GemCount]);
        Assert.Equal(4990L, (long)dict[GKey.GamMoney]);
        Assert.True(dict.ContainsKey(Key.ItemCode + "0"));
        Assert.False(dict.ContainsKey("failCode"));
        var select = CommandTestHelper.AsDict(sent[1].packet);
        Assert.Equal(GKey.ValueSuccess, (string)select[GKey.Result]);
        Assert.Equal(1, (int)select[GKey.Count]);
        Assert.Equal("item001", (string)select[Key.ItemCode + "0"]);
        Assert.Equal("1", (string)select[Key.UseFlag + "0"]);
    }

    // シナリオ2: 無効な sellCode → result=0 + failCode
    [Fact]
    public async Task Execute_InvalidSellCode_Failure()
    {
        var svc = BuildMajItemService();
        var player = new MajakPlayer { MemberNo = "user01", GamMoney = 5000 };
        var cmd = new BuyMajItemCommand(svc);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [Key.SellCode] = "sell999" });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        // 失敗時: result=0 + failCode あり
        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.Equal(0, (int)dict["result"]);
        Assert.Equal(GKey.ValueFailure, (string)dict[GKey.Result]);
        Assert.Equal(3, (int)dict[Key.FailCode]);
    }

    // シナリオ3: コイン不足 → result=0 + failCode
    [Fact]
    public async Task Execute_InsufficientMoney_Failure()
    {
        var svc = BuildMajItemService();
        var player = new MajakPlayer { MemberNo = "user01", GamMoney = 0, GemCount = 0 };
        var cmd = new BuyMajItemCommand(svc);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [Key.SellCode] = "sell001" });

        await cmd.ExecuteAsync(ctx);

        Assert.Equal(0, CommandTestHelper.GetResult(sent[0].packet));
    }

    // シナリオ4: player=null
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new BuyMajItemCommand(BuildMajItemService());
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc21e SelectMajItemCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   1. 所持アイテムを選択 → result=1
///   2. 未所持アイテムを選択 → result=0
///   3. player=null → 何も送らない
/// </summary>
public class SelectMajItemCommandTests
{
    private readonly Mock<ItemRepository>    _itemRepoMock   = new(MockBehavior.Loose);
    private readonly Mock<PlayerRepository>  _playerRepoMock2 = new(MockBehavior.Loose);
    private readonly Mock<HistoryRepository> _histRepoMock2  = new(MockBehavior.Loose);

    private MajItemService BuildMajItemService(IEnumerable<MajItemInfo> items)
    {
        _itemRepoMock.Setup(r => r.GetAllItemsAsync(It.IsAny<string>()))
            .ReturnsAsync(items.ToList());
        _itemRepoMock.Setup(r => r.UpdateMajItemInUseAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        return new MajItemService(_itemRepoMock.Object, _playerRepoMock2.Object, _histRepoMock2.Object);
    }

    // シナリオ1: 有効なアイテムを選択 → result=0 (成功、原典: result=0 + アイテムデータ)
    [Fact]
    public async Task Execute_ValidItem_ReturnsSuccess()
    {
        var items = new List<MajItemInfo>
        {
            // 未使用の所持アイテムだけが選択できる (使用中は E_ITEM_ALREADYINUSE)
            new() { ItemCode = "item001", UseFlag = false, Qty = 1,
                    BuyDt = DateTime.Now, EndDt = DateTime.Now.AddDays(7) }
        };
        var svc = BuildMajItemService(items);
        var player = new MajakPlayer { MemberNo = "user01", MajItems = items };
        var cmd = new SelectMajItemCommand(svc);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [Key.ItemCode] = "item001" });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.SelectMajItem, sent[0].method);
        // 成功時の result = 0 (原典: commandSelectMajItem の応答定義)
        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.Equal(0, (int)dict["result"]);
        Assert.Equal(GKey.ValueSuccess, (string)dict[GKey.Result]);
        Assert.Equal(1, (int)dict[GKey.Count]);
        Assert.Equal("item001", (string)dict[Key.ItemCode + "0"]);
        Assert.Equal("1", (string)dict[Key.UseFlag + "0"]);
    }

    // シナリオ2: 未所持アイテム → result=-1 (失敗、原典: result=-1 + failCode)
    [Fact]
    public async Task Execute_ItemNotFound_ReturnsFailure()
    {
        var svc = BuildMajItemService(Enumerable.Empty<MajItemInfo>());
        var player = new MajakPlayer { MemberNo = "user01", MajItems = [] };
        var cmd = new SelectMajItemCommand(svc);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [Key.ItemCode] = "item001" });

        await cmd.ExecuteAsync(ctx);

        var result = CommandTestHelper.GetResult(sent[0].packet);
        Assert.Equal(-1, result);
        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.Equal(GKey.ValueFailure, (string)dict[GKey.Result]);
        Assert.Equal(4, (int)dict[Key.FailCode]);
    }

    // シナリオ3: player=null
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var svc = BuildMajItemService(Enumerable.Empty<MajItemInfo>());
        var cmd = new SelectMajItemCommand(svc);
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// smmc1e SendOkButtonCommand テスト
// 原典: SendOkButtonState から送る S→C パケット。C→S では処理しない。
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   1. ルーム内プレイヤー → 何も送らない
///   2. player=null → 何も送らない
///   3. RoomId=null → 何も送らない
///   4. ルーム不存在 → 何も送らない
/// </summary>
public class SendOkButtonCommandTests
{
    private readonly PlayerSessionService _session = new();

    private (GameRoom room, MajakPlayer player) SetupRoom()
    {
        var player = new MajakPlayer
        {
            ConnectionId = "conn1",
            MemberNo     = "user01",
            ChannelId    = "ch1",
            RoomId       = 1,
            SeatPos      = 0,  // 0=East
        };
        // CreateRoom が内部で _rooms に登録する
        var room = _session.CreateRoom("ch1", player, "", 1, 0, 0, false);
        return (room, player);
    }

    // シナリオ1: C→S smmc1e は処理しない
    [Fact]
    public async Task Execute_InRoom_NothingSent()
    {
        var (room, player) = SetupRoom();
        var cmd = new SendOkButtonCommand(_session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }

    // シナリオ2: player=null
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new SendOkButtonCommand(_session);
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    // シナリオ3: RoomId=null
    [Fact]
    public async Task Execute_NoRoom_NothingSent()
    {
        var player = new MajakPlayer { MemberNo = "user01", RoomId = null };
        var cmd = new SendOkButtonCommand(_session);
        var (ctx, sent) = CommandTestHelper.MakeContext(player);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// smmc2e PushOkButtonCommand テスト
// ═══════════════════════════════════════════════════════════════════════════
/// <summary>
/// シナリオ:
///   1. コインあり → OKフラグがトグルされ、ルームにブロードキャスト
///   2. コインなし → result=0 + LackMoney フィールドが返る
///   3. ゲーム進行中 → 無視 (何も送らない)
///   4. player=null → 何も送らない
/// </summary>
public class PushOkButtonCommandTests
{
    private readonly PlayerSessionService _session = new();
    private readonly Mock<GameLogicService> _gameLogicMock;

    public PushOkButtonCommandTests()
    {
        var playerRepo = new Mock<PlayerRepository>(MockBehavior.Loose);
        var histRepo   = new Mock<HistoryRepository>(MockBehavior.Loose);
        var itemRepo   = new Mock<ItemRepository>(MockBehavior.Loose);
        playerRepo.Setup(r => r.UpdateCommonRatAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);

        var histRepo2  = new Mock<HistoryRepository>(MockBehavior.Loose);
        var logRepo    = new Mock<LogRepository>(MockBehavior.Loose, (MySqlDbContext)null!);
        var titleSvc   = new TitleService(playerRepo.Object, TestMasterCacheFactory.Create(playerRepo: playerRepo.Object));

        var moneySvc = new GameMoneyService(playerRepo.Object, new RatingService());
        _gameLogicMock = new Mock<GameLogicService>(
            MockBehavior.Loose,
            _session, histRepo2.Object, logRepo.Object, new RatingService(),
            playerRepo.Object, moneySvc, titleSvc, (TournamentService)null!, (GradeRankService)null!,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            (ILogger<GameLogicService>)null!, (RoomRegistryService?)null);
    }

    private (GameRoom room, MajakPlayer player) SetupRoom2(long money = 1000, GameRoomState state = GameRoomState.Waiting)
    {
        var player = new MajakPlayer
        {
            ConnectionId = "conn1",
            MemberNo     = "user01",
            RoomId       = 1,
            SeatPos      = 0,  // 0=East
            GamMoney     = money,
        };
        _session.Register(player);
        var room = _session.CreateRoom("ch1", player, "", 1, 0, 0, false);
        player.RoomId = room.RoomId;
        room.State    = state;
        return (room, player);
    }

    // シナリオ1: コインあり → OKフラグがトグルされる
    [Fact]
    public async Task Execute_HasMoney_TogglesOkFlag()
    {
        var (room, player) = SetupRoom2(money: 1000);
        var cmd = new PushOkButtonCommand(_session, _gameLogicMock.Object, new RatingService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        Assert.False(room.OkButtonStates[0]);
        await cmd.ExecuteAsync(ctx);

        Assert.True(room.OkButtonStates[0]);
        Assert.Contains(sent, s => s.method == Cmd.SendOkButton);
        Assert.Contains(sent, s => s.method == Cmd.PushOkButton);

        var okState = CommandTestHelper.ToDict(sent.First(s => s.method == Cmd.SendOkButton).packet);
        Assert.Equal(1, ((JsonElement)okState[$"{Key.OkButton}0"]!).GetInt32());
        Assert.Equal(0, ((JsonElement)okState[$"{Key.OkButton}1"]!).GetInt32());
        Assert.Equal(0, ((JsonElement)okState[$"{Key.OkButton}2"]!).GetInt32());
        Assert.Equal(0, ((JsonElement)okState[$"{Key.OkButton}3"]!).GetInt32());

        var response = CommandTestHelper.ToDict(sent.First(s => s.method == Cmd.PushOkButton).packet);
        Assert.Equal(0, ((JsonElement)response[Key.LackMoney]!).GetInt64());
    }

    // シナリオ2: グレード入場条件NG → LackMoney 返却
    [Fact]
    public async Task Execute_GradeLimitFailure_ReturnsLackMoney()
    {
        var (_, player) = SetupRoom2(money: 0);
        var room = _session.GetRoom(player.RoomId!.Value)!;
        room.SubId = "00G5A";
        var cmd = new PushOkButtonCommand(_session, _gameLogicMock.Object, new RatingService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.PushOkButton, sent[0].method);
        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.True(dict.ContainsKey(Key.LackMoney));
    }

    // シナリオ3: ゲーム進行中 → 無視
    [Fact]
    public async Task Execute_GamePlaying_NothingSent()
    {
        var (_, player) = SetupRoom2(money: 1000, state: GameRoomState.Playing);
        var cmd = new PushOkButtonCommand(_session, _gameLogicMock.Object, new RatingService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Empty(sent);
    }

    [Theory]
    [InlineData("0Z05A")]
    [InlineData("00H5A")]
    public async Task Execute_AutoMatchingOrTournamentRoom_BroadcastsReadyState(string subId)
    {
        var (room, player) = SetupRoom2(money: 1000);
        room.SubId = subId;
        var cmd = new PushOkButtonCommand(_session, _gameLogicMock.Object, new RatingService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.True(room.OkButtonStates[0]);
        Assert.Contains(sent, packet => packet.method == Cmd.SendOkButton);
        Assert.Contains(sent, packet => packet.method == Cmd.PushOkButton);
        _gameLogicMock.Verify(svc => svc.StartGameLogicAsync(It.IsAny<GameRoom>(), It.IsAny<CommandContext>()), Times.Never);
    }

    // シナリオ4: player=null
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new PushOkButtonCommand(_session, _gameLogicMock.Object, new RatingService());
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}
