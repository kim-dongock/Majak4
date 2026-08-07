using Moq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MajakServer.Hubs;
using MajakServer.Infrastructure;
using MajakServer.Models.Game;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;

namespace MajakServer.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// TitleService 単体テスト
// 原典: HMajDBObject 称号関連 / ProcessCommand_GetTitle
// ═══════════════════════════════════════════════════════════════════════════
public class TitleServiceTests
{
    private readonly Mock<PlayerRepository> _playerRepoMock
        = new(MockBehavior.Loose);

    private TitleService BuildService(
        Dictionary<string, string>? titleCache = null)
    {
        var svc = new TitleService(_playerRepoMock.Object, TestMasterCacheFactory.Create(playerRepo: _playerRepoMock.Object));

        // _titleCache をリフレクションで直接注入
        typeof(TitleService)
            .GetField("_titleCache",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)!
            .SetValue(svc, titleCache ?? new Dictionary<string, string>
            {
                ["mjkt100"] = "初心者",
                ["mjks013"] = "雀士",
                ["mjkt103"] = "玄人",
            });

        return svc;
    }

    // シナリオ1: 有効な titleCode → result=true + TrickTitle 更新
    [Fact]
    public async Task GetTitleAsync_ValidTrickTitle_ReturnsOkAndUpdatesPlayer()
    {
        _playerRepoMock.Setup(r => r.InsertOrEnableTitleAsync(
                It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _playerRepoMock.Setup(r => r.UpdateCommonRatAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);

        var player = new MajakPlayer { MemberNo = "u1" };
        var svc    = BuildService();

        var (ok, trickTitle, majakTitle, name) =
            await svc.GetTitleAsync(player, 1, "mjks013");

        Assert.True(ok);
        Assert.Equal("mjks013", trickTitle);
        Assert.Equal("雀士", name);
        _playerRepoMock.Verify(r =>
            r.InsertOrEnableTitleAsync("u1", "mjks013"), Times.Once);
    }

    // シナリオ2: titleType=2 → MajakTitle 更新
    [Fact]
    public async Task GetTitleAsync_MajakTitle_UpdatesMajakTitle()
    {
        _playerRepoMock.Setup(r => r.InsertOrEnableTitleAsync(
                It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _playerRepoMock.Setup(r => r.UpdateCommonRatAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);

        var player = new MajakPlayer { MemberNo = "u1" };
        var svc    = BuildService();

        var (ok, _, majakTitle, name) =
            await svc.GetTitleAsync(player, 2, "mjkt100");

        Assert.True(ok);
        Assert.Equal("mjkt100", majakTitle);
        Assert.Equal("初心者", name);
    }

    // シナリオ3: 未登録の titleCode → false を返す (原典: _titleCache にない場合は無効)
    [Fact]
    public async Task GetTitleAsync_UnknownTitle_ReturnsFalse()
    {
        var player = new MajakPlayer { MemberNo = "u1" };
        var svc    = BuildService();

        var (ok, _, _, _) = await svc.GetTitleAsync(player, 1, "mjks999");

        Assert.False(ok);
        _playerRepoMock.Verify(r =>
            r.InsertOrEnableTitleAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    // シナリオ4: GetTitleName — キャッシュ内存在 → 名前返却
    [Fact]
    public void GetTitleName_Cached_ReturnsName()
    {
        var svc = BuildService();
        Assert.Equal("玄人", svc.GetTitleName("mjkt103"));
    }

    // シナリオ5: GetTitleName — 未登録 → null 返却
    [Fact]
    public void GetTitleName_NotFound_ReturnsNull()
    {
        var svc = BuildService();
        Assert.Null(svc.GetTitleName("NOTEXIST"));
    }

    [Fact]
    public void GetTitleName_TypeAndCode_BuildsLegacyTitleIds()
    {
        var svc = BuildService(new Dictionary<string, string>
        {
            ["mjks013"] = "雀士",
            ["mjkt103"] = "玄人",
            ["mjkc001"] = "大会王者",
        });

        Assert.Equal("雀士", svc.GetTitleName(0, 13));
        Assert.Equal("玄人", svc.GetTitleName(1, 103));
        Assert.Equal("大会王者", svc.GetTitleName(1, 1001));
        Assert.Equal("", svc.GetTitleName(1, 999));
    }

    // シナリオ6: EnsureInitialGradeTitleAsync — 10級称号を DB 登録
    [Fact]
    public async Task EnsureInitialGradeTitleAsync_CallsInsertOrEnable()
    {
        _playerRepoMock.Setup(r => r.InsertOrEnableTitleAsync(
                It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var player = new MajakPlayer { MemberNo = "u1" };
        var svc    = BuildService();

        await svc.EnsureInitialGradeTitleAsync(player);

        _playerRepoMock.Verify(r =>
            r.InsertOrEnableTitleAsync("u1", GameConst.RatingTitle10Kyu),
            Times.Once);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// ItemService MajItem ロードテスト
// 原典: HMajDBObject::GetItemInfo + ネットカフェ特典 item002
// ═══════════════════════════════════════════════════════════════════════════
public class ItemServiceMajItemLoadTests
{
    private readonly Mock<ItemRepository> _itemRepoMock = new(MockBehavior.Loose);

    private ItemService BuildService()
        => new(_itemRepoMock.Object, TestMasterCacheFactory.Create());

    [Fact]
    public async Task LoadMajItemsAsync_LoadsRepositoryItems()
    {
        var items = new List<MajItemInfo>
        {
            new() { ItemCode = "item001", UseFlag = true, EndDt = DateTime.Now.AddDays(1), Qty = 1 },
        };
        _itemRepoMock.Setup(r => r.GetAllItemsAsync("u1")).ReturnsAsync(items);
        var player = new MajakPlayer { MemberNo = "u1" };
        var svc = BuildService();

        await svc.LoadMajItemsAsync(player);

        Assert.Single(player.MajItems);
        Assert.Equal("item001", player.MajItems[0].ItemCode);
    }

    [Fact]
    public async Task LoadMajItemsAsync_NetCafeAddsPrivilegeRichiItem()
    {
        _itemRepoMock.Setup(r => r.GetAllItemsAsync("u1")).ReturnsAsync(new List<MajItemInfo>());
        var player = new MajakPlayer { MemberNo = "u1", IsNetCafeIp = true };
        var svc = BuildService();

        await svc.LoadMajItemsAsync(player);

        var item = Assert.Single(player.MajItems);
        Assert.Equal("item002", item.ItemCode);
        Assert.True(item.UseFlag);
        Assert.Equal(2, player.GetRichiEffect());
    }

    [Fact]
    public async Task LoadMajItemsAsync_NetCafeDoesNotOverrideActiveRichiItem()
    {
        _itemRepoMock.Setup(r => r.GetAllItemsAsync("u1")).ReturnsAsync(new List<MajItemInfo>
        {
            new() { ItemCode = "item001", UseFlag = true, EndDt = DateTime.Now.AddDays(1), Qty = 1 },
        });
        var player = new MajakPlayer { MemberNo = "u1", IsNetCafeIp = true };
        var svc = BuildService();

        await svc.LoadMajItemsAsync(player);

        Assert.Contains(player.MajItems, item => item.ItemCode == "item002" && !item.UseFlag);
        Assert.Equal(1, player.GetRichiEffect());
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// MajItemService 単体テスト
// 原典: HMajItem.cpp ProcessCommand_BuyMajItem / ProcessCommand_SelectMajItem
// ═══════════════════════════════════════════════════════════════════════════
public class MajItemServiceTests
{
    private readonly Mock<ItemRepository>            _itemRepoMock  = new(MockBehavior.Loose);
    private readonly Mock<PlayerRepository>          _playerRepoMock = new(MockBehavior.Loose);
    private readonly Mock<HistoryRepository>         _histMock       = new(MockBehavior.Loose);

    private MajItemService BuildService()
    {
        _playerRepoMock.Setup(r => r.UpdateCommonRatAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);
        _playerRepoMock.Setup(r => r.HasActiveTitleAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        _playerRepoMock.Setup(r => r.SetDailyMissionDirectAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        return new MajItemService(
            _itemRepoMock.Object, _playerRepoMock.Object,
            _histMock.Object);
    }

    // ─── BuyMajItemAsync ─────────────────────────────────────────────────

    // シナリオ1: 存在しない sellCode → Fail
    [Fact]
    public async Task BuyMajItemAsync_UnknownSellCode_ReturnsFail()
    {
        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 10000, GemCount = 100 };
        var svc    = BuildService();

        var result = await svc.BuyMajItemAsync(player, "NOTEXIST");

        Assert.False(result.Ok);
        Assert.Equal("SELL_CODE_NOT_FOUND", result.Error);
    }

    // シナリオ2: 宝石不足 → Fail (原典: E_ITEM_GEMSHORT)
    [Fact]
    public async Task BuyMajItemAsync_NotEnoughGem_ReturnsFail()
    {
        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 10000, GemCount = 0 };
        var svc    = BuildService();

        // sell076 は CostGem=50, CostMoney=0
        var result = await svc.BuyMajItemAsync(player, "sell076");

        Assert.False(result.Ok);
        Assert.Equal("GEM_NOT_ENOUGH", result.Error);
    }

    // シナリオ3: コイン不足 → Fail (原典: E_ITEM_MONEYSHORT)
    [Fact]
    public async Task BuyMajItemAsync_NotEnoughMoney_ReturnsFail()
    {
        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 0, GemCount = 100 };
        var svc    = BuildService();

        // sell007 は CostMoney=150
        var result = await svc.BuyMajItemAsync(player, "sell007");

        Assert.False(result.Ok);
        Assert.Equal("MONEY_NOT_ENOUGH", result.Error);
    }

    // シナリオ4: 必要称号未所持 → Fail (原典: E_ITEM_MUSTTITLE)
    [Fact]
    public async Task BuyMajItemAsync_RequiredTitleNotMet_ReturnsFail()
    {
        // sell025 は RequiredTitle="mjkt103"
        _playerRepoMock.Setup(r => r.HasActiveTitleAsync("u1", "mjkt103"))
            .ReturnsAsync(false); // 称号なし

        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 100000, GemCount = 100 };
        var svc    = BuildService();

        var result = await svc.BuyMajItemAsync(player, "sell025");

        Assert.False(result.Ok);
        Assert.Equal("REQUIRED_TITLE_NOT_MET", result.Error);
    }

    [Fact]
    public async Task BuyMajItemAsync_MoneyShortPrecedesRequiredTitleCheck()
    {
        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 0, GemCount = 100 };
        var svc    = BuildService();

        var result = await svc.BuyMajItemAsync(player, "sell025");

        Assert.False(result.Ok);
        Assert.Equal("MONEY_NOT_ENOUGH", result.Error);
        _playerRepoMock.Verify(r => r.HasActiveTitleAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // シナリオ5: CAT_ITEM 正常購入 (sell001: item001, CostGem=0, CostMoney=500)
    [Fact]
    public async Task BuyMajItemAsync_CatItem_Success()
    {
        _itemRepoMock.Setup(r => r.ExchangeMajItemAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<string?>()))
            .ReturnsAsync(new MajItemInfo
            {
                ItemCode = "item001",
                UseFlag  = false,
                BuyDt    = DateTime.Now,
                EndDt    = DateTime.Now.AddDays(7),
                Qty      = 5,
            });

        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 10000, GemCount = 100 };
        var svc    = BuildService();

        var result = await svc.BuyMajItemAsync(player, "sell001");

        Assert.True(result.Ok);
        Assert.Equal("item001", result.ItemCode);
        Assert.Equal(5, result.Qty);
    }

    // シナリオ6: CAT_TITLE 正常購入 (sell007: mjkt100, CostGem=5, CostMoney=150)
    [Fact]
    public async Task BuyMajItemAsync_CatTitle_CallsInsertTitle()
    {
        _itemRepoMock.Setup(r => r.ExchangeTitleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync(true);
        _itemRepoMock.Setup(r => r.ExchangeAvatarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>()))
            .ReturnsAsync(true);

        var player = new MajakPlayer
        {
            MemberNo   = "u1",
            GamMoney   = 10000,
            GemCount   = 100,
            TitleClear = new int[110],
        };
        var svc = BuildService();

        var result = await svc.BuyMajItemAsync(player, "sell007");

        Assert.True(result.Ok);
        Assert.Equal("mjkt100", result.ItemCode);
        _itemRepoMock.Verify(r =>
            r.ExchangeTitleAsync("u1", "mjkt100", 150L, 0, false), Times.Once);
    }

    // シナリオ7: CAT_AVATAR ExchangeAvatar → Success
    [Fact]
    public async Task BuyMajItemAsync_CatAvatar_CallsStoredProc_Success()
    {
        _itemRepoMock.Setup(r => r.ExchangeAvatarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>()))
            .ReturnsAsync(true);

        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 10000, GemCount = 100 };
        var svc    = BuildService();

        var result = await svc.BuyMajItemAsync(player, "sell011"); // CatAvatar

        Assert.True(result.Ok);
        _itemRepoMock.Verify(r => r.ExchangeAvatarAsync("u1", "AA11S", 500L, 0), Times.Once);
    }

    [Fact]
    public async Task BuyMajItemAsync_CatAvatar_FemaleUsesLegacyFemaleCode()
    {
        _itemRepoMock.Setup(r => r.ExchangeAvatarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>()))
            .ReturnsAsync(true);

        var player = new MajakPlayer { MemberNo = "u1", Sex = "F", GamMoney = 10000, GemCount = 100 };
        var svc    = BuildService();

        var result = await svc.BuyMajItemAsync(player, "sell014");

        Assert.True(result.Ok);
        Assert.Equal("LC2CVP", result.ItemCode);
        _itemRepoMock.Verify(r => r.ExchangeAvatarAsync("u1", "LC2CVP", 750L, 0), Times.Once);
    }

    // シナリオ8: CAT_AVATAR ストアドプロシージャ → rtnVal!=1 → Fail
    [Fact]
    public async Task BuyMajItemAsync_CatAvatar_StoredProcFails_ReturnsFail()
    {
        _itemRepoMock.Setup(r => r.ExchangeAvatarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>()))
            .ReturnsAsync(false);

        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 10000, GemCount = 100 };
        var svc    = BuildService();

        var result = await svc.BuyMajItemAsync(player, "sell011");

        Assert.False(result.Ok);
        Assert.StartsWith("AVATAR_BUY_ERROR", result.Error);
    }

    [Fact]
    public async Task BuyMajItemAsync_CatBilling_CallsLegacyBillingSpAndReloadsItem()
    {
        var buyDt = DateTime.Now.AddHours(-1);
        var endDt = DateTime.Now.AddDays(1);
        _itemRepoMock.Setup(s => s.BuyBillingItemAsync(
            "u1", "", "MJ21", "MJ2101", 1, "127.0.0.1", 2, 0))
            .ReturnsAsync((1, "OK"));
        _itemRepoMock.Setup(r => r.GetAllItemsAsync("u1"))
            .ReturnsAsync(new List<MajItemInfo>
            {
                new() { ItemCode = "MJ21", BuyDt = buyDt, EndDt = endDt, Qty = 1 },
            });

        var player = new MajakPlayer { MemberNo = "u1", IpAddress = "127.0.0.1", GamMoney = 10000, GemCount = 100 };
        var svc    = BuildService();

        var result = await svc.BuyMajItemAsync(player, "sell044");

        Assert.True(result.Ok);
        Assert.Equal("MJ21", result.ItemCode);
        Assert.Equal(1, result.Qty);
        Assert.Contains(player.MajItems, item => item.ItemCode == "MJ21" && item.Qty == 1);
    }

    [Fact]
    public async Task BuyMajItemAsync_CatBilling_PostSpMissingItem_ReturnsFail()
    {
        _itemRepoMock.Setup(s => s.BuyBillingItemAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((1, "OK"));
        _itemRepoMock.Setup(r => r.GetAllItemsAsync("u1"))
            .ReturnsAsync(new List<MajItemInfo>());

        var player = new MajakPlayer { MemberNo = "u1", GamMoney = 10000, GemCount = 100 };
        var svc    = BuildService();

        var result = await svc.BuyMajItemAsync(player, "sell044");

        Assert.False(result.Ok);
        Assert.Equal("BILLING_ITEM_NOT_FOUND", result.Error);
    }

    // ─── SelectMajItemAsync ──────────────────────────────────────────────

    // シナリオ9: 所持なし → Fail (原典: E_ITEM_NOTOWN)
    [Fact]
    public async Task SelectMajItemAsync_ItemNotOwned_ReturnsFail()
    {
        var player = new MajakPlayer { MemberNo = "u1", MajItems = [] };
        var svc    = BuildService();

        var result = await svc.SelectMajItemAsync(player, "item001");

        Assert.False(result.Ok);
        Assert.Equal("ITEM_NOT_FOUND", result.Error);
        Assert.Equal(4, result.ErrorCode);
    }

    // シナリオ10: 有効期限切れ → Fail (原典: E_ITEM_EXPIRED)
    [Fact]
    public async Task SelectMajItemAsync_ExpiredItem_ReturnsFail()
    {
        var player = new MajakPlayer
        {
            MemberNo = "u1",
            MajItems =
            [
                new MajItemInfo { ItemCode = "item001", UseFlag = false, EndDt = DateTime.Now.AddDays(-1), Qty = 1 },
            ],
        };
        var svc    = BuildService();

        var result = await svc.SelectMajItemAsync(player, "item001");

        Assert.False(result.Ok);
        Assert.Equal("ITEM_EXPIRED", result.Error);
        Assert.Equal(9, result.ErrorCode);
    }

    // シナリオ11: 正常選択 → 旧アイテム OFF, 新アイテム ON (原典: UpdateItemInUse)
    [Fact]
    public async Task SelectMajItemAsync_ValidItem_UpdatesFlags()
    {
        var items = new List<MajItemInfo>
        {
            new() { ItemCode = "item001", UseFlag = false, EndDt = DateTime.Now.AddDays(1), Qty = 1 },
            new() { ItemCode = "item002", UseFlag = true,  EndDt = DateTime.Now.AddDays(1), Qty = 1 },
        };
        _itemRepoMock.Setup(r => r.UpdateMajItemInUseAsync("u1", "item002", "item001"))
            .ReturnsAsync(true);

        var player = new MajakPlayer { MemberNo = "u1", MajItems = items };
        var svc    = BuildService();

        var result = await svc.SelectMajItemAsync(player, "item001");

        Assert.True(result.Ok);
        Assert.Equal("item001", result.NewItemCode);
        _itemRepoMock.Verify(r =>
            r.UpdateMajItemInUseAsync("u1", "item002", "item001"), Times.Once);
        Assert.False(player.MajItems.First(i => i.ItemCode == "item002").UseFlag);
        Assert.True(player.MajItems.First(i => i.ItemCode == "item001").UseFlag);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// PlayerSessionService 単体テスト
// ─── セッション管理の基本動作を検証 ───────────────────────────────────────
// ═══════════════════════════════════════════════════════════════════════════
public class PlayerSessionServiceTests
{
    // ─── Register / Remove ───────────────────────────────────────────────

    [Fact]
    public void Register_ThenGetByConn_ReturnsPlayer()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1" };

        session.Register(player);

        Assert.Same(player, session.GetByConn("c1"));
        Assert.Same(player, session.GetByMember("u1"));
    }

    [Fact]
    public void Remove_ThenGetByConn_ReturnsNull()
    {
        var session = new PlayerSessionService();
        var player  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1" };
        session.Register(player);

        session.Remove("c1");

        Assert.Null(session.GetByConn("c1"));
        Assert.Null(session.GetByMember("u1"));
    }

    // ─── GetChannelMembers ───────────────────────────────────────────────

    [Fact]
    public void GetChannelMembers_ExcludesRoomPlayers()
    {
        var session = new PlayerSessionService();
        var lobby   = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        var inRoom  = new MajakPlayer { ConnectionId = "c2", MemberNo = "u2", ChannelId = "ch1" };
        session.Register(lobby);
        session.Register(inRoom);
        session.CreateRoom("ch1", inRoom, "", 1, 0, 0, false); // inRoom は RoomId 持つ

        var members = session.GetChannelMembers("ch1").ToList();

        Assert.Single(members);
        Assert.Equal("u1", members[0].MemberNo);
    }

    [Fact]
    public void FindTournamentRecoveryRoom_ReturnsReservedPlayerRoom()
    {
        var session = new PlayerSessionService();
        var room = session.CreateReservedRoom("ch1", "", 1, 0, long.MaxValue, false, subId: "00H8A");
        room.TournamentSeqNo = 10;
        session.RegisterPendingMatch(new PendingAutoMatch
        {
            RoomId = room.RoomId,
            ChannelId = "ch1",
            ExpectedMembers = ["u1"],
        });

        var recovery = session.FindTournamentRecoveryRoom("ch1", "u1");

        Assert.NotNull(recovery);
        Assert.Same(room, recovery.Value.Room);
        Assert.Equal(-1, recovery.Value.SeatOrder);
    }

    [Fact]
    public void FindTournamentRecoveryRoom_ReturnsDisconnectedPlayerSeat()
    {
        var session = new PlayerSessionService();
        var player = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(player);
        var room = session.CreateReservedRoom("ch1", "", 1, 0, long.MaxValue, false, subId: "00H8A");
        room.TournamentSeqNo = 10;
        Assert.True(session.JoinRoom(room.RoomId, player));
        room.State = GameRoomState.Playing;
        player.IsOutPlayer = true;

        var recovery = session.FindTournamentRecoveryRoom("ch1", "u1");

        Assert.NotNull(recovery);
        Assert.Same(room, recovery.Value.Room);
        Assert.Equal(0, recovery.Value.SeatOrder);
    }

    // ─── CreateRoom / JoinRoom / LeaveRoom ───────────────────────────────

    [Fact]
    public void CreateRoom_AddsOwnerToSeat0()
    {
        var session = new PlayerSessionService();
        var owner   = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(owner);

        var room = session.CreateRoom("ch1", owner, "", 1, 0, 0, false);

        Assert.NotNull(session.GetRoom(room.RoomId));
        Assert.Equal("u1", room.Seats[0]?.MemberNo);
        Assert.Equal(room.RoomId, owner.RoomId);
        Assert.Equal(1, room.UnitMoney);
    }

    [Fact]
    public void CreateRoom_UsesExplicitUnitMoney()
    {
        var session = new PlayerSessionService();
        var owner   = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(owner);

        var room = session.CreateRoom("ch1", owner, "", 500, 0, 0, false, unitMoney: 20);

        Assert.Equal(20, room.UnitMoney);
    }

    [Fact]
    public void CreateRoom_FirstRoomIdIsOne()
    {
        var session = new PlayerSessionService();
        var owner   = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(owner);

        var room = session.CreateRoom("ch1", owner, "", 1, 0, 0, false);

        Assert.Equal(1, room.RoomId);
    }

    [Fact]
    public void CreateRoom_UsesRequestedRoomId()
    {
        var session = new PlayerSessionService();
        var owner   = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(owner);

        var room = session.CreateRoom("ch1", owner, "", 1, 0, 0, false, roomId: 3);

        Assert.Equal(3, room.RoomId);
        Assert.Same(room, session.GetRoom(3));
    }

    [Fact]
    public void CreateRoom_RequestedOccupiedRoomIdThrows()
    {
        var session = new PlayerSessionService();
        var owner1  = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        var owner2  = new MajakPlayer { ConnectionId = "c2", MemberNo = "u2", ChannelId = "ch1" };
        session.Register(owner1);
        session.Register(owner2);

        session.CreateRoom("ch1", owner1, "", 1, 0, 0, false, roomId: 3);

        Assert.Throws<InvalidOperationException>(() =>
            session.CreateRoom("ch1", owner2, "", 1, 0, 0, false, roomId: 3));
        Assert.Null(owner2.RoomId);
    }

    [Fact]
    public void CreateRoom_StoresMaxViewer()
    {
        var session = new PlayerSessionService();
        var owner   = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(owner);

        var room = session.CreateRoom("ch1", owner, "", 1, 0, 0, false, maxViewer: 0);

        Assert.Equal(0, room.MaxViewer);
    }

    [Fact]
    public void AddViewer_RespectsMaxViewer()
    {
        var session = new PlayerSessionService();
        var owner   = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        var viewer1 = new MajakPlayer { ConnectionId = "c2", MemberNo = "u2", ChannelId = "ch1" };
        var viewer2 = new MajakPlayer { ConnectionId = "c3", MemberNo = "u3", ChannelId = "ch1" };
        session.Register(owner);
        var room = session.CreateRoom("ch1", owner, "", 1, 0, 0, false, maxViewer: 1);

        Assert.True(room.AddViewer(viewer1));
        Assert.False(room.AddViewer(viewer2));
        Assert.Single(room.Viewers);
    }

    [Fact]
    public void AddViewer_DisabledWhenMaxViewerIsZero()
    {
        var session = new PlayerSessionService();
        var owner   = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        var viewer  = new MajakPlayer { ConnectionId = "c2", MemberNo = "u2", ChannelId = "ch1" };
        session.Register(owner);
        var room = session.CreateRoom("ch1", owner, "", 1, 0, 0, false, maxViewer: 0);

        Assert.False(room.AddViewer(viewer));
        Assert.Empty(room.Viewers);
    }

    [Fact]
    public void JoinRoom_PlayerGetsRoomId()
    {
        var session = new PlayerSessionService();
        var owner   = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        var guest   = new MajakPlayer
        {
            ConnectionId = "c2",
            MemberNo = "u2",
            ChannelId = "ch1",
            IsOutPlayer = true,
        };
        session.Register(owner);
        session.Register(guest);
        var room = session.CreateRoom("ch1", owner, "", 1, 0, 0, false);

        bool ok = session.JoinRoom(room.RoomId, guest);

        Assert.True(ok);
        Assert.Equal(room.RoomId, guest.RoomId);
        Assert.False(guest.IsOutPlayer);
        Assert.Equal(2, room.ActivePlayerCount);
        var memberList = MajakServer.Commands.Room.RoomGetMembersCommand.BuildMemberListPayload(room);
        Assert.Equal(2, Convert.ToInt32(memberList[GKey.Count]));
    }

    [Fact]
    public void LeaveRoom_ClearsPlayerRoomId()
    {
        var session = new PlayerSessionService();
        var owner   = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(owner);
        var room = session.CreateRoom("ch1", owner, "", 1, 0, 0, false);

        session.LeaveRoom(owner);

        Assert.Null(owner.RoomId);
    }

    [Fact]
    public void LeaveRoom_LastPlayer_RemovesRoom()
    {
        var session = new PlayerSessionService();
        var owner   = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(owner);
        var room = session.CreateRoom("ch1", owner, "", 1, 0, 0, false);

        session.LeaveRoom(owner);

        Assert.Null(session.GetRoom(room.RoomId));
    }

    // ─── AutoMatching ───────────────────────────────────────────────────

    [Fact]
    public void TryMatch_FourInQueue_ReturnsArray()
    {
        var session = new PlayerSessionService();

        session.EnqueueMatching("ch1", "u1");
        session.EnqueueMatching("ch1", "u2");
        session.EnqueueMatching("ch1", "u3");
        session.EnqueueMatching("ch1", "u4");

        var matched = session.TryMatch("ch1", _ => 1500);

        Assert.NotNull(matched);
        Assert.Equal(4, matched!.Length);
    }

    [Fact]
    public void TryMatch_LessThanFour_ReturnsNull()
    {
        var session = new PlayerSessionService();
        session.EnqueueMatching("ch1", "u1");
        session.EnqueueMatching("ch1", "u2");
        session.EnqueueMatching("ch1", "u3");

        Assert.Null(session.TryMatch("ch1", _ => 1500));
    }

    [Fact]
    public void DequeueMatching_RemovesMember()
    {
        var session = new PlayerSessionService();
        session.EnqueueMatching("ch1", "u1");
        session.EnqueueMatching("ch1", "u2");

        session.DequeueMatching("ch1", "u1");

        var matched = session.TryMatch("ch1", _ => 1500); // 1人しかいない
        Assert.Null(matched);
    }

    // ─── PendingAutoMatch ──────────────────────────────────────────────

    [Fact]
    public void ConfirmAutoEntry_AllEntered_ReturnsTrue()
    {
        var session = new PlayerSessionService();
        var session2 = new PlayerSessionService();

        var owner = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(owner);
        var room = session.CreateRoom("ch1", owner, "", 1, 0, 0, false);

        session.RegisterPendingMatch(new PendingAutoMatch
        {
            RoomId          = room.RoomId,
            ChannelId       = "ch1",
            ExpectedMembers = new[] { "u1", "u2", "u3", "u4" },
            Players         = new[] { owner },
        });

        // 4人分確定
        session.ConfirmAutoEntry(room.RoomId, "u1");
        session.ConfirmAutoEntry(room.RoomId, "u2");
        session.ConfirmAutoEntry(room.RoomId, "u3");
        var (allEntered, match) = session.ConfirmAutoEntry(room.RoomId, "u4");

        Assert.True(allEntered);
        Assert.NotNull(match);
    }

    [Fact]
    public void ConfirmAutoEntry_PartialEntry_ReturnsFalse()
    {
        var session = new PlayerSessionService();
        var owner   = new MajakPlayer { ConnectionId = "c1", MemberNo = "u1", ChannelId = "ch1" };
        session.Register(owner);
        var room    = session.CreateRoom("ch1", owner, "", 1, 0, 0, false);

        session.RegisterPendingMatch(new PendingAutoMatch
        {
            RoomId          = room.RoomId,
            ChannelId       = "ch1",
            ExpectedMembers = new[] { "u1", "u2", "u3", "u4" },
            Players         = new[] { owner },
        });

        session.ConfirmAutoEntry(room.RoomId, "u1");
        var (allEntered, _) = session.ConfirmAutoEntry(room.RoomId, "u2");

        Assert.False(allEntered);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// AutoMatchingBackgroundService 単体テスト
// 原典: HMajChnlServer::OnTimer TIMERID_MAJANG_AUTOMATCHING
//   4人揃った時点でルーム作成 → 全員に mjkc2e 送信
// ═══════════════════════════════════════════════════════════════════════════
public class AutoMatchingBackgroundServiceTests
{
    private readonly PlayerSessionService      _session = new();
    private readonly List<(string method, object packet)> _hubSent = new();

    private Mock<IHubContext<MajakGameHub>> BuildHubMock()
    {
        var singleProxy = new Mock<ISingleClientProxy>();
        singleProxy.Setup(c => c.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Callback<string, object?[], CancellationToken>((m, a, _) =>
                _hubSent.Add((m, a[0]!)))
            .Returns(Task.CompletedTask);

        var groupProxy = new Mock<IClientProxy>();
        groupProxy.Setup(c => c.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        var clientsMock = new Mock<IHubClients>();
        clientsMock.Setup(c => c.Client(It.IsAny<string>())).Returns(singleProxy.Object);
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(groupProxy.Object);

        var hubMock = new Mock<IHubContext<MajakGameHub>>();
        hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);
        hubMock.Setup(h => h.Groups).Returns(new Mock<IGroupManager>().Object);

        return hubMock;
    }

    private AutoMatchingBackgroundService BuildService(IHubContext<MajakGameHub> hub)
    {
        var playerRepo = new Mock<PlayerRepository>(MockBehavior.Loose);
        playerRepo.Setup(r => r.GetCupConfigsAsync()).ReturnsAsync(new List<CupConfig>());
        var channelRepo = new Mock<ChannelRepository>(MockBehavior.Loose,
            (GameDataContextFactory)null!, TestMasterCacheFactory.CreateRedisService());
        channelRepo.Setup(r => r.GetChannelListAsync(It.IsAny<string>())).ReturnsAsync(new List<ChannelInfo>
        {
            new() { ChanelId = "ch1", UnitMoney = 20 },
        });
        var services = new ServiceCollection();
        services.AddSingleton(TestMasterCacheFactory.Create(
            playerRepo: playerRepo.Object,
            channelRepo: channelRepo.Object));
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var logger       = new Mock<ILogger<AutoMatchingBackgroundService>>();
        var settings = Microsoft.Extensions.Options.Options.Create(new MajakServer.Infrastructure.ChannelServerSettings());
        return new AutoMatchingBackgroundService(
            scopeFactory, _session, hub, settings, logger.Object);
    }

    // シナリオ1: 4人揃った → ルーム作成 + 全員に AutoMatching 通知
    [Fact]
    public async Task TickAsync_FourPlayersQueued_CreatesRoomAndNotifiesAll()
    {
        var hub    = BuildHubMock();
        var svc    = BuildService(hub.Object);
        var tickFn = typeof(AutoMatchingBackgroundService)
            .GetMethod("TickAsync",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)!;

        // 4人をチャンネルに登録してマッチングキューへ
        for (int i = 1; i <= 4; i++)
        {
            var p = new MajakPlayer
            {
                ConnectionId = $"conn{i}",
                MemberNo     = $"user{i:00}",
                ChannelId    = "ch1",
            };
            _session.Register(p);
            _session.EnqueueMatching("ch1", p.MemberNo);
        }

        await (Task)tickFn.Invoke(svc, new object[] { CancellationToken.None })!;

        // 4人に AutoMatching パケットが送信されること
        var autoMatchSent = _hubSent.Where(s => s.method == Cmd.AutoMatching).ToList();
        Assert.Equal(4, autoMatchSent.Count);
    }

    // シナリオ2: 3人以下 → マッチング発生しない
    [Fact]
    public async Task TickAsync_ThreePlayersQueued_NoMatchCreated()
    {
        var hub = BuildHubMock();
        var svc = BuildService(hub.Object);
        var tickFn = typeof(AutoMatchingBackgroundService)
            .GetMethod("TickAsync",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)!;

        for (int i = 1; i <= 3; i++)
        {
            var p = new MajakPlayer
            {
                ConnectionId = $"conn{i}",
                MemberNo     = $"user{i:00}",
                ChannelId    = "ch1",
            };
            _session.Register(p);
            _session.EnqueueMatching("ch1", p.MemberNo);
        }

        await (Task)tickFn.Invoke(svc, new object[] { CancellationToken.None })!;

        Assert.Empty(_hubSent.Where(s => s.method == Cmd.AutoMatching));
    }

    // シナリオ3: マッチング後 PendingAutoMatch が登録されること
    [Fact]
    public async Task TickAsync_AfterMatch_PendingMatchRegistered()
    {
        var hub = BuildHubMock();
        var svc = BuildService(hub.Object);
        var tickFn = typeof(AutoMatchingBackgroundService)
            .GetMethod("TickAsync",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)!;

        for (int i = 1; i <= 4; i++)
        {
            var p = new MajakPlayer
            {
                ConnectionId = $"conn{i}",
                MemberNo     = $"user{i:00}",
                ChannelId    = "ch1",
            };
            _session.Register(p);
            _session.EnqueueMatching("ch1", p.MemberNo);
        }

        await (Task)tickFn.Invoke(svc, new object[] { CancellationToken.None })!;

        // ルームが作成され PendingAutoMatch が存在すること
        var rooms = _session.GetChannelRooms("ch1").ToList();
        Assert.Single(rooms);
        var pending = _session.GetPendingMatch(rooms[0].RoomId);
        Assert.NotNull(pending);
        Assert.Equal(4, pending!.ExpectedMembers.Length);
        Assert.Equal(20, rooms[0].UnitMoney);
    }
}
