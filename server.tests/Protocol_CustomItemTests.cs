using Moq;
using Microsoft.Extensions.Logging;
using MajakServer.Commands.Channel;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;
using System.Text.Json;

namespace MajakServer.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// mjkc35e ShopItemRequestCommand テスト
// 原典: ProcessCommand_GetShopItem
//   → HMajCustomShopMast の全カタログ + 所持フラグ
//   → binary: count + shopInfo(shopNo/customId/customType/customPrice/Name/description/gameMoney/Purchased)
// ═══════════════════════════════════════════════════════════════════════════
public class ShopItemRequestCommandTests
{
    private readonly Mock<ItemRepository>            _itemRepoMock = new(MockBehavior.Loose);

    private void SetupShopCatalog(bool empty = false)
    {
        _itemRepoMock.Setup(r => r.GetCustomShopMastAsync())
            .ReturnsAsync(empty ? new List<CustomShopItemInfo>() : new List<CustomShopItemInfo>
            {
                new() { ShopNo = 3, CustomId = 100001, ShopName = "背景板A", Price = 500, GameMoney = 1200, SalesDt = DateTime.Now.AddDays(-1), LimitDt = DateTime.Now.AddDays(1) },
                new() { ShopNo = 2, CustomId = 100002, ShopName = "背景板B", Price = 800, SalesDt = DateTime.Now.AddDays(-1), LimitDt = DateTime.Now.AddDays(1) },
                new() { ShopNo = 1, CustomId = 200001, ShopName = "パタパタ君", Price = 300, SalesDt = DateTime.Now.AddDays(-1), LimitDt = DateTime.Now.AddDays(1) },
            });
        _itemRepoMock.Setup(r => r.GetCustomItemMastAsync())
            .ReturnsAsync(empty ? new List<(int CustomId, int Kind, string Name, long Price)>() : new List<(int, int, string, long)>
            {
                (100001, 10, "背景板A", 500),
                (100002, 10, "背景板B", 800),
                (200001, 20, "パタパタ君", 300),
            });
        _itemRepoMock.Setup(r => r.GetCustomSetMastAsync())
            .ReturnsAsync(new Dictionary<int, List<int>>());
    }

    private ItemService BuildItemService(
        Dictionary<int, (int Kind, string Name, long Price)>? catalog = null)
    {
        var svc = new ItemService(_itemRepoMock.Object, TestMasterCacheFactory.Create(itemRepo: _itemRepoMock.Object));
        typeof(ItemService)
            .GetField("_itemMast",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(svc, catalog ?? new Dictionary<int, (int, string, long)>
            {
                [100001] = (10, "背景板A", 500),
                [100002] = (10, "背景板B", 800),
                [200001] = (20, "パタパタ君", 300),
            });
        return svc;
    }

    // シナリオ1: カタログあり → shopCnt=3 + shopList
    [Fact]
    public async Task Execute_WithCatalog_ReturnsShopList()
    {
        var player = new MajakPlayer { MemberNo = "u1" };
        // 1個所持
        player.CustomItems[100001] = new UserCustomItem { Kind = 10, Equip = 0 };
        SetupShopCatalog();

        var cmd = new ShopItemRequestCommand(TestMasterCacheFactory.Create(itemRepo: _itemRepoMock.Object));
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.ShopItemResponse, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(3, ((JsonElement)pkt["shopCnt"]!).GetInt32());
        Assert.False(pkt.ContainsKey("result"));

        var shopJson = ((JsonElement)pkt["shopList"]!).GetRawText();
        var shops    = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(shopJson)!;
        var first    = shops.First(s => s["shopNo"].GetInt32() == 3);
        Assert.Equal(10, first["customType"].GetInt32());
        Assert.Equal(500, first["customPrice"].GetInt32());
        Assert.Equal("背景板A", first["Name"].GetString());
        Assert.Equal(1200, first["gameMoney"].GetInt64());
        Assert.Equal(1, first["Purchased"].GetInt32());
    }

    // シナリオ2: カタログなし → shopCnt=0
    [Fact]
    public async Task Execute_EmptyCatalog_Returns0Count()
    {
        var player = new MajakPlayer { MemberNo = "u1" };
        SetupShopCatalog(empty: true);
        var cmd    = new ShopItemRequestCommand(TestMasterCacheFactory.Create(itemRepo: _itemRepoMock.Object));
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(0, ((JsonElement)pkt["shopCnt"]!).GetInt32());
    }

    // シナリオ3: 所持アイテムに purchased=1 フラグが設定されること
    [Fact]
    public async Task Execute_OwnedItem_HasPurchasedFlag()
    {
        var player = new MajakPlayer { MemberNo = "u1" };
        player.CustomItems[100001] = new UserCustomItem { Kind = 10, Equip = 0 };
        SetupShopCatalog();

        var cmd = new ShopItemRequestCommand(TestMasterCacheFactory.Create(itemRepo: _itemRepoMock.Object));
        var (ctx, sent) = CommandTestHelper.MakeContext(player);
        await cmd.ExecuteAsync(ctx);

        var pkt      = CommandTestHelper.ToDict(sent[0].packet);
        var shopJson = ((JsonElement)pkt["shopList"]!).GetRawText();
        var shops    = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(shopJson)!;

        var owned    = shops.First(s => s["customId"].GetInt32() == 100001);
        var notOwned = shops.First(s => s["customId"].GetInt32() == 100002);
        Assert.Equal(1, owned["purchased"].GetInt32());
        Assert.Equal(0, notOwned["purchased"].GetInt32());
    }

    [Fact]
    public async Task Execute_SetItemOwnedChild_HasPurchasedFlagAndFiltersSalePeriod()
    {
        var now = DateTime.Now;
        _itemRepoMock.Setup(r => r.GetCustomShopMastAsync())
            .ReturnsAsync(new List<CustomShopItemInfo>
            {
                new() { ShopNo = 10, CustomId = 300001, ShopName = "セットA", Price = 1000, SalesDt = now.AddDays(-1), LimitDt = now.AddDays(1) },
                new() { ShopNo = 11, CustomId = 300002, ShopName = "未来商品", Price = 1000, SalesDt = now.AddDays(1), LimitDt = now.AddDays(2) },
                new() { ShopNo = 12, CustomId = 300003, ShopName = "期限切れ", Price = 1000, SalesDt = now.AddDays(-2), LimitDt = now.AddDays(-1) },
            });
        _itemRepoMock.Setup(r => r.GetCustomItemMastAsync())
            .ReturnsAsync(new List<(int CustomId, int Kind, string Name, long Price)>
            {
                (300001, 100, "セットA", 1000),
                (300002, 100, "未来商品", 1000),
                (300003, 100, "期限切れ", 1000),
            });
        _itemRepoMock.Setup(r => r.GetCustomSetMastAsync())
            .ReturnsAsync(new Dictionary<int, List<int>>
            {
                [300001] = new() { 100001, 100002 },
            });
        var player = new MajakPlayer { MemberNo = "u1" };
        player.CustomItems[100002] = new UserCustomItem { Kind = 10, Equip = 0 };

        var cmd = new ShopItemRequestCommand(TestMasterCacheFactory.Create(itemRepo: _itemRepoMock.Object));
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(1, ((JsonElement)pkt["shopCnt"]!).GetInt32());
        var shopJson = ((JsonElement)pkt["shopList"]!).GetRawText();
        var shops = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(shopJson)!;
        Assert.Equal(300001, shops[0]["customId"].GetInt32());
        Assert.Equal(100, shops[0]["customType"].GetInt32());
        Assert.Equal(1, shops[0]["Purchased"].GetInt32());
    }

    // シナリオ4: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new ShopItemRequestCommand(TestMasterCacheFactory.Create(itemRepo: _itemRepoMock.Object));
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc38e EquipCustomItemCommand テスト
// 原典: ProcessCommand_EquipCustomItem (HMajChnlServer)
//   → SetUserCustomItem で装備設定 → result=1/0 応答
// ═══════════════════════════════════════════════════════════════════════════
public class EquipCustomItemCommandTests
{
    private readonly Mock<ItemRepository>            _itemRepoMock = new(MockBehavior.Loose);

    private ItemService BuildItemService()
    {
        _itemRepoMock.Setup(r => r.SetEquipAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        return new ItemService(_itemRepoMock.Object, TestMasterCacheFactory.Create(itemRepo: _itemRepoMock.Object));
    }

    // シナリオ1: 所持アイテム装備 → result=1
    [Fact]
    public async Task Execute_OwnedItem_Returns1()
    {
        var svc    = BuildItemService();
        var player = new MajakPlayer { MemberNo = "u1" };
        player.CustomItems[100001] = new UserCustomItem { Kind = 10, Equip = 0 };

        var cmd = new EquipCustomItemCommand(svc);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [Key.CustomId] = 100001 });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.EquipCustomItem, sent[0].method);
        Assert.Equal(1, CommandTestHelper.GetResult(sent[0].packet));
    }

    // シナリオ2: 未所持アイテム → result=0
    [Fact]
    public async Task Execute_NotOwnedItem_Returns0()
    {
        var svc    = BuildItemService();
        var player = new MajakPlayer { MemberNo = "u1" };
        // アイテムを持っていない

        var cmd = new EquipCustomItemCommand(svc);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [Key.CustomId] = 999 });

        await cmd.ExecuteAsync(ctx);

        Assert.Equal(0, CommandTestHelper.GetResult(sent[0].packet));
    }

    // シナリオ3: 装備変更時に DB が呼ばれること
    [Fact]
    public async Task Execute_Equip_CallsSetEquipAsync()
    {
        var svc    = BuildItemService();
        var player = new MajakPlayer { MemberNo = "u1" };
        player.CustomItems[100001] = new UserCustomItem { Kind = 10, Equip = 0 };

        var cmd = new EquipCustomItemCommand(svc);
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?> { [Key.CustomId] = 100001 });

        await cmd.ExecuteAsync(ctx);

        _itemRepoMock.Verify(r => r.SetEquipAsync("u1", 0, 100001), Times.Once);
    }

    // シナリオ4: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new EquipCustomItemCommand(BuildItemService());
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc39e CustomItemCommand テスト (所持アイテムリスト)
// 原典: commandCustomItem → commandCustomItemResponse (mjkc40e)
//   → binary: count + customInfo(customId/nKind/Name)
// ═══════════════════════════════════════════════════════════════════════════
public class CustomItemCommandTests
{
    private readonly Mock<ItemRepository>            _itemRepoMock = new(MockBehavior.Loose);

    private ItemService BuildItemService()
    {
        _itemRepoMock.Setup(r => r.GetCustomItemMastAsync())
            .ReturnsAsync(new List<(int CustomId, int Kind, string Name, long Price)>
            {
                (100001, 10, "アイテムA", 100),
                (200001, 20, "アイテムB", 200),
            });

        return new ItemService(_itemRepoMock.Object, TestMasterCacheFactory.Create(itemRepo: _itemRepoMock.Object));
    }

    // シナリオ1: 所持アイテムあり → customCnt + items
    [Fact]
    public async Task Execute_WithItems_ReturnsItemList()
    {
        var player = new MajakPlayer { MemberNo = "u1" };
        player.CustomItems[100001] = new UserCustomItem { Kind = 10, Equip = 1 };
        player.CustomItems[200001] = new UserCustomItem { Kind = 20, Equip = 0 };

        var cmd = new CustomItemCommand(BuildItemService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        Assert.Equal(Cmd.CustomItemResponse, sent[0].method);
        var pkt = CommandTestHelper.ToDict(sent[0].packet);
        Assert.False(pkt.ContainsKey("result"));
        Assert.Equal(2, ((JsonElement)pkt["customCnt"]!).GetInt32());

        var itemsJson = ((JsonElement)pkt["items"]!).GetRawText();
        var items     = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(itemsJson)!;
        Assert.Equal(2, items.Count);
        Assert.Equal(100001, items[0]["customId"].GetInt32());
        Assert.Equal(10, items[0]["nKind"].GetInt32());
        Assert.Equal("アイテムA", items[0]["Name"].GetString());
        Assert.False(items[0].ContainsKey("kind"));
        Assert.False(items[0].ContainsKey("itemType"));
        Assert.False(items[0].ContainsKey("name"));
    }

    // シナリオ2: 所持アイテムなし → customCnt=0 + items=[]
    [Fact]
    public async Task Execute_NoItems_ReturnsEmptyList()
    {
        var player = new MajakPlayer { MemberNo = "u1" };
        var cmd    = new CustomItemCommand(BuildItemService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player);

        await cmd.ExecuteAsync(ctx);

        var pkt      = CommandTestHelper.ToDict(sent[0].packet);
        Assert.Equal(0, ((JsonElement)pkt["customCnt"]!).GetInt32());
        var itemJson = ((JsonElement)pkt["items"]!).GetRawText();
        var items    = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(itemJson)!;
        Assert.Empty(items);
    }

    // シナリオ3: レガシー応答には equip フラグが含まれないこと
    [Fact]
    public async Task Execute_DoesNotReturnEquipFlag()
    {
        var player = new MajakPlayer { MemberNo = "u1" };
        player.CustomItems[100001] = new UserCustomItem { Kind = 10, Equip = 1 };

        var cmd = new CustomItemCommand(BuildItemService());
        var (ctx, sent) = CommandTestHelper.MakeContext(player);
        await cmd.ExecuteAsync(ctx);

        var pkt      = CommandTestHelper.ToDict(sent[0].packet);
        var itemJson = ((JsonElement)pkt["items"]!).GetRawText();
        var items    = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(itemJson)!;
        Assert.False(items[0].ContainsKey("equip"));
    }

    // シナリオ4: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var cmd = new CustomItemCommand(BuildItemService());
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }
}
