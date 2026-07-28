using Moq;
using Microsoft.Extensions.Logging;
using MajakServer.Commands.Channel;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using MajakServer.Services;

namespace MajakServer.Tests;

/// <summary>
/// ItemService テスト
///
/// 検証シナリオ:
///   - BuyCustomItemAsync: 成功 / アイテム不存在 / 既所持 / GEM 不足 (プロシージャエラー)
///   - EquipCustomItemAsync: 成功 / 未所持アイテム
///   - GetShopCatalog / GetCustomItemList: 構造確認
///   - BuyCustomItemCommand (mjkc41e): レスポンスパケット検証
/// </summary>
public class ItemServiceTests
{
    private readonly Mock<ItemRepository>              _itemRepoMock  = new(MockBehavior.Loose);

    private ItemService BuildItemService()
    {
        // デフォルト: カスタムアイテム購入成功
        _itemRepoMock.Setup(s => s.BuyCustomItemAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((1, "success"));

        _itemRepoMock.Setup(r => r.EnsureDefaultCustomItemAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        _itemRepoMock.Setup(r => r.SetEquipAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        _itemRepoMock.Setup(r => r.GetCustomShopMastAsync())
            .ReturnsAsync(new List<CustomShopItemInfo>
            {
                new()
                {
                    ShopNo = 5, CustomId = 100001, ShopName = "背景板A", Price = 500,
                    SalesDt = DateTime.Now.AddDays(-1), LimitDt = DateTime.Now.AddDays(1),
                },
            });
        _itemRepoMock.Setup(r => r.GetCustomItemMastAsync())
            .ReturnsAsync(new List<(int CustomId, int Kind, string Name, long Price)>
            {
                (100000, 10, "背景板デフォルト", 0),
                (100001, 10, "背景板A", 500),
                (100003, 20, "牌デフォルト", 0),
                (100011, 30, "コスチュームデフォルト", 0),
            });
        _itemRepoMock.Setup(r => r.GetCustomSetMastAsync())
            .ReturnsAsync(new Dictionary<int, List<int>>());
        _itemRepoMock.Setup(r => r.LoadCustomItemsAsync(It.IsAny<MajakPlayer>()))
            .Callback<MajakPlayer>(p => p.CustomItems[100001] = new UserCustomItem { Kind = 10, Equip = 0 })
            .Returns(Task.CompletedTask);

        var svc = new ItemService(_itemRepoMock.Object, TestMasterCacheFactory.Create(itemRepo: _itemRepoMock.Object));
        // マスターデータを手動注入 (InitAsync の代替)
        typeof(ItemService)
            .GetField("_itemMast", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(svc, new Dictionary<int, (int Kind, string Name, long Price)>
            {
                [100000] = (10, "背景板デフォルト", 0),
                [100001] = (10, "背景板A",         500),
                [100003] = (20, "牌デフォルト",      0),
                [100011] = (30, "コスチュームデフォルト", 0),
            });
        return svc;
    }

    // ─── BuyCustomItemAsync ────────────────────────────────────────────────

    // シナリオ1: 正常購入 → ResultCode=0 (G::valueSuccess)
    [Fact]
    public async Task BuyCustomItemAsync_Success_Returns1()
    {
        var svc    = BuildItemService();
        var player = new MajakPlayer { MemberNo = "user01", GamMoney = 1000 };

        var (resultCode, qty) = await svc.BuyCustomItemAsync(player, 100001, shopNo: 5);

        Assert.Equal(Val.CustomSuccess, resultCode);
        Assert.Equal(1, qty);
        // メモリ上にアイテムが追加されること
        Assert.True(player.CustomItems.ContainsKey(100001));
    }

    // シナリオ2: アイテムID が存在しない → CustomIdError
    [Fact]
    public async Task BuyCustomItemAsync_UnknownItemId_ReturnsCustomIdError()
    {
        var svc    = BuildItemService();
        var player = new MajakPlayer { MemberNo = "user01" };

        var (resultCode, _) = await svc.BuyCustomItemAsync(player, 999999, shopNo: 1);

        Assert.Equal(Val.CustomIdError, resultCode);
    }

    // シナリオ3: プロシージャが -1101 (GEM 不足) → CustomCoinless
    [Fact]
    public async Task BuyCustomItemAsync_GemShortage_ReturnsCustomCoinless()
    {
        _itemRepoMock.Setup(s => s.BuyCustomItemAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((-1101, "GEM Not Enough"));
        _itemRepoMock.Setup(r => r.GetCustomShopMastAsync())
            .ReturnsAsync(new List<CustomShopItemInfo>
            {
                new() { ShopNo = 5, CustomId = 100001, ShopName = "背景板A", Price = 500, SalesDt = DateTime.Now.AddDays(-1), LimitDt = DateTime.Now.AddDays(1) },
            });
        _itemRepoMock.Setup(r => r.GetCustomItemMastAsync())
            .ReturnsAsync(new List<(int CustomId, int Kind, string Name, long Price)>
            {
                (100001, 10, "背景板A", 500),
            });
        _itemRepoMock.Setup(r => r.GetCustomSetMastAsync())
            .ReturnsAsync(new Dictionary<int, List<int>>());
        _itemRepoMock.Setup(r => r.LoadCustomItemsAsync(It.IsAny<MajakPlayer>()))
            .Callback<MajakPlayer>(p => p.CustomItems[100001] = new UserCustomItem { Kind = 10, Equip = 0 })
            .Returns(Task.CompletedTask);

        var svc    = new ItemService(_itemRepoMock.Object, TestMasterCacheFactory.Create(itemRepo: _itemRepoMock.Object));
        typeof(ItemService)
            .GetField("_itemMast", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(svc, new Dictionary<int, (int Kind, string Name, long Price)>
            {
                [100001] = (10, "背景板A", 500),
            });

        var player = new MajakPlayer { MemberNo = "user01" };
        var (resultCode, _) = await svc.BuyCustomItemAsync(player, 100001, shopNo: 5);

        Assert.Equal(Val.CustomCoinless, resultCode);
    }

    // シナリオ4: プロシージャが -1102 (ユーザーエラー) → CustomIdError
    [Fact]
    public async Task BuyCustomItemAsync_UserError_ReturnsCustomIdError()
    {
        _itemRepoMock.Setup(s => s.BuyCustomItemAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((-1102, "user info error-1"));
        _itemRepoMock.Setup(r => r.GetCustomShopMastAsync())
            .ReturnsAsync(new List<CustomShopItemInfo>
            {
                new() { ShopNo = 5, CustomId = 100001, ShopName = "背景板A", Price = 500, SalesDt = DateTime.Now.AddDays(-1), LimitDt = DateTime.Now.AddDays(1) },
            });
        _itemRepoMock.Setup(r => r.GetCustomItemMastAsync())
            .ReturnsAsync(new List<(int CustomId, int Kind, string Name, long Price)>
            {
                (100001, 10, "背景板A", 500),
            });
        _itemRepoMock.Setup(r => r.GetCustomSetMastAsync())
            .ReturnsAsync(new Dictionary<int, List<int>>());
        _itemRepoMock.Setup(r => r.LoadCustomItemsAsync(It.IsAny<MajakPlayer>()))
            .Callback<MajakPlayer>(p => p.CustomItems[100001] = new UserCustomItem { Kind = 10, Equip = 0 })
            .Returns(Task.CompletedTask);

        var svc    = new ItemService(_itemRepoMock.Object, TestMasterCacheFactory.Create(itemRepo: _itemRepoMock.Object));
        typeof(ItemService)
            .GetField("_itemMast", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(svc, new Dictionary<int, (int Kind, string Name, long Price)>
            {
                [100001] = (10, "背景板A", 500),
            });

        var player = new MajakPlayer { MemberNo = "user01" };
        var (resultCode, _) = await svc.BuyCustomItemAsync(player, 100001, shopNo: 5);

        Assert.Equal(Val.CustomIdError, resultCode);
    }

    // ─── EquipCustomItemAsync ──────────────────────────────────────────────

    // シナリオ5: 所持アイテムを装備 → 成功、以前の装備が外れる
    [Fact]
    public async Task EquipCustomItemAsync_Success_SwitchesEquip()
    {
        var svc    = BuildItemService();
        var player = new MajakPlayer { MemberNo = "user01" };
        // 同種アイテム2つ所持: 現在は 100000 が装備済み
        player.CustomItems[100000] = new UserCustomItem { Kind = 10, Equip = 1 };
        player.CustomItems[100001] = new UserCustomItem { Kind = 10, Equip = 0 };

        var ok = await svc.EquipCustomItemAsync(player, 100001);

        Assert.True(ok);
        Assert.Equal(0, player.CustomItems[100000].Equip);  // 外れた
        Assert.Equal(1, player.CustomItems[100001].Equip);  // 装備
    }

    // シナリオ6: 未所持アイテムを装備しようとする → false
    [Fact]
    public async Task EquipCustomItemAsync_NotOwned_ReturnsFalse()
    {
        var svc    = BuildItemService();
        var player = new MajakPlayer { MemberNo = "user01" };
        // アイテムを持っていない

        var ok = await svc.EquipCustomItemAsync(player, 100001);

        Assert.False(ok);
    }

    // ─── GetShopCatalog ────────────────────────────────────────────────────

    [Fact]
    public void DefaultCustomItems_MatchesLegacyHMajCommonTable()
    {
        var defaultItems = (int[])typeof(ItemService)
            .GetField("DefaultCustomItems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;
        var getDefaultEquip = typeof(ItemService)
            .GetMethod("GetDefaultEquip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        int[] expectedIds = [100000, 100001, 100002, 100003, 100004, 100005, 100011];

        Assert.Equal(expectedIds, defaultItems);
        foreach (int customId in expectedIds)
        {
            bool expectedEquip = customId == 100000 || customId == 100003 || customId == 100011;
            Assert.Equal(expectedEquip, (bool)getDefaultEquip.Invoke(null, [customId])!);
        }
    }

    [Fact]
    public async Task EnsureDefaultItemsAsync_MissingMasterKind_UsesLegacyZeroKind()
    {
        _itemRepoMock.Setup(r => r.LoadCustomItemsAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);
        _itemRepoMock.Setup(r => r.GetCustomItemMastAsync())
            .ReturnsAsync(new List<(int CustomId, int Kind, string Name, long Price)>
            {
                (100000, 10, "背景板デフォルト", 0),
            });
        _itemRepoMock.Setup(r => r.EnsureDefaultCustomItemAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var player = new MajakPlayer { MemberNo = "user01" };
        var svc = new ItemService(_itemRepoMock.Object, TestMasterCacheFactory.Create(itemRepo: _itemRepoMock.Object));

        await svc.EnsureDefaultItemsAsync(player);

        Assert.Equal(10, player.CustomItems[100000].Kind);
        Assert.Equal(0, player.CustomItems[100001].Kind);
        Assert.Equal(0, player.CustomItems[100002].Kind);
    }

    [Fact]
    public async Task MasterCache_CustomMasts_ReturnRepositoryDataWhenRedisMisses()
    {
        var shop = new CustomShopItemInfo { ShopNo = 7, CustomId = 100001, ShopName = "背景板A", Price = 500 };
        _itemRepoMock.Setup(r => r.GetCustomItemMastAsync())
            .ReturnsAsync(new List<(int CustomId, int Kind, string Name, long Price)>
            {
                (100001, 10, "背景板A", 500),
            });
        _itemRepoMock.Setup(r => r.GetCustomShopMastAsync())
            .ReturnsAsync(new List<CustomShopItemInfo> { shop });

        var cache = TestMasterCacheFactory.Create(itemRepo: _itemRepoMock.Object);

        var itemMast = await cache.GetCustomItemMastAsync();
        var shopMast = await cache.GetCustomShopMastAsync();

        var item = Assert.Single(itemMast);
        Assert.Equal((100001, 10, "背景板A", 500L), item);
        Assert.Same(shop, Assert.Single(shopMast));
    }

    // シナリオ7: ショップカタログには所持フラグが含まれること
    [Fact]
    public void GetShopCatalog_ContainsPurchasedFlag()
    {
        var svc    = BuildItemService();
        var player = new MajakPlayer { MemberNo = "user01" };
        player.CustomItems[100000] = new UserCustomItem { Kind = 10, Equip = 1 };

        var catalog = svc.GetShopCatalog(player);

        Assert.NotEmpty(catalog);
        // 全カタログエントリに purchased フィールドがあること
        // (dynamic なのでリフレクションで確認)
        foreach (var item in catalog)
        {
            var type = item.GetType();
            Assert.NotNull(type.GetProperty("purchased"));
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// mjkc41e BuyCustomItemCommand テスト (プロトコル層)
// ═══════════════════════════════════════════════════════════════════════════
public class BuyCustomItemCommandTests
{
    private readonly Mock<ItemRepository>            _itemRepoMock = new(MockBehavior.Loose);

    private (ItemService svc, BuyCustomItemCommand cmd) BuildCommand(int spReturnVal = 1)
    {
        _itemRepoMock.Setup(s => s.BuyCustomItemAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((spReturnVal, spReturnVal == 1 ? "success" : "error"));
        _itemRepoMock.Setup(r => r.EnsureDefaultCustomItemAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        _itemRepoMock.Setup(r => r.GetCustomShopMastAsync())
            .ReturnsAsync(new List<CustomShopItemInfo>
            {
                new()
                {
                    ShopNo = 5, CustomId = 100001, ShopName = "背景板A", Price = 500,
                    SalesDt = DateTime.Now.AddDays(-1), LimitDt = DateTime.Now.AddDays(1),
                },
            });
        _itemRepoMock.Setup(r => r.GetCustomItemMastAsync())
            .ReturnsAsync(new List<(int CustomId, int Kind, string Name, long Price)>
            {
                (100001, 10, "背景板A", 500),
            });
        _itemRepoMock.Setup(r => r.GetCustomSetMastAsync())
            .ReturnsAsync(new Dictionary<int, List<int>>());
        _itemRepoMock.Setup(r => r.LoadCustomItemsAsync(It.IsAny<MajakPlayer>()))
            .Returns(Task.CompletedTask);

        var svc = new ItemService(_itemRepoMock.Object, TestMasterCacheFactory.Create(itemRepo: _itemRepoMock.Object));
        typeof(ItemService)
            .GetField("_itemMast", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(svc, new Dictionary<int, (int Kind, string Name, long Price)>
            {
                [100001] = (10, "背景板A", 500),
            });

        return (svc, new BuyCustomItemCommand(svc));
    }

    // シナリオ1: 購入成功 → mjkc42e 応答 result=0 (G::valueSuccess)
    [Fact]
    public async Task Execute_Success_SendsBuyCustomItemResponse()
    {
        var (_, cmd) = BuildCommand(spReturnVal: 1);
        var player   = new MajakPlayer { MemberNo = "user01" };
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [Key.ShopNo]   = 5,
            });

        await cmd.ExecuteAsync(ctx);

        Assert.Single(sent);
        var (method, _) = sent[0];
        Assert.Equal(Cmd.BuyCustomItemResponse, method);

        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.Equal(Val.CustomSuccess, (int)dict[GKey.Result]);
        Assert.Equal("", (string)dict[GKey.Message]);
        Assert.False(dict.ContainsKey("result"));
        Assert.False(dict.ContainsKey("message"));
    }

    // シナリオ2: 購入失敗 (GEM 不足) → mjkc42e に失敗コード
    [Fact]
    public async Task Execute_GemShortage_SendsFailureResponse()
    {
        var (_, cmd) = BuildCommand(spReturnVal: -1101);
        var player   = new MajakPlayer { MemberNo = "user01" };
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [Key.ShopNo]   = 5,
            });

        await cmd.ExecuteAsync(ctx);

        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.Equal(Val.CustomCoinless, (int)dict[GKey.Result]);
        Assert.Equal("GEMが足りません", (string)dict[GKey.Message]);
    }

    [Fact]
    public async Task Execute_MissingShopNo_SendsIdErrorWithoutCallingProcedure()
    {
        var (_, cmd) = BuildCommand();
        var player = new MajakPlayer { MemberNo = "user01" };
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [Key.ShopNo] = 999,
            });

        await cmd.ExecuteAsync(ctx);

        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.Equal(Val.CustomIdError, (int)dict[GKey.Result]);
        Assert.Equal("IDが不正です", (string)dict[GKey.Message]);
        _itemRepoMock.Verify(s => s.BuyCustomItemAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_AlreadyOwnedItem_SendsOwnedWithoutCallingProcedure()
    {
        var (_, cmd) = BuildCommand();
        var player = new MajakPlayer { MemberNo = "user01" };
        player.CustomItems[100001] = new UserCustomItem { Kind = 10, Equip = 0 };
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [Key.ShopNo] = 5,
            });

        await cmd.ExecuteAsync(ctx);

        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.Equal(Val.CustomOwned, (int)dict[GKey.Result]);
        Assert.Equal("既に所持しているアイテムです", (string)dict[GKey.Message]);
        _itemRepoMock.Verify(s => s.BuyCustomItemAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_SetItemOwnedChild_SendsOwnedWithoutCallingProcedure()
    {
        var (svc, cmd) = BuildCommand();
        _itemRepoMock.Setup(r => r.GetCustomShopMastAsync())
            .ReturnsAsync(new List<CustomShopItemInfo>
            {
                new()
                {
                    ShopNo = 6, CustomId = 300001, ShopName = "セットA", Price = 1000,
                    SalesDt = DateTime.Now.AddDays(-1), LimitDt = DateTime.Now.AddDays(1),
                },
            });
        _itemRepoMock.Setup(r => r.GetCustomItemMastAsync())
            .ReturnsAsync(new List<(int CustomId, int Kind, string Name, long Price)>
            {
                (300001, 100, "セットA", 1000),
            });
        _itemRepoMock.Setup(r => r.GetCustomSetMastAsync())
            .ReturnsAsync(new Dictionary<int, List<int>>
            {
                [300001] = new() { 100001, 100002 },
            });
        typeof(ItemService)
            .GetField("_itemMast", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(svc, new Dictionary<int, (int Kind, string Name, long Price)>
            {
                [300001] = (100, "セットA", 1000),
            });
        var player = new MajakPlayer { MemberNo = "user01" };
        player.CustomItems[100002] = new UserCustomItem { Kind = 10, Equip = 0 };
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [Key.ShopNo] = 6,
            });

        await cmd.ExecuteAsync(ctx);

        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.Equal(Val.CustomOwned, (int)dict[GKey.Result]);
        Assert.Equal("既に所持しているアイテムです", (string)dict[GKey.Message]);
        _itemRepoMock.Verify(s => s.BuyCustomItemAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(-1102, Val.CustomIdError, "IDが不正です")]
    [InlineData(-1103, Val.CustomError, "不明なエラー")]
    [InlineData(-1, Val.CustomDbError, "接続エラー")]
    public async Task Execute_ProcedureReturnCodes_MapToLegacyResultAndMessage(
        int procedureReturnCode,
        int expectedResult,
        string expectedMessage)
    {
        var (_, cmd) = BuildCommand(spReturnVal: procedureReturnCode);
        var player = new MajakPlayer { MemberNo = "user01" };
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [Key.ShopNo] = 5,
            });

        await cmd.ExecuteAsync(ctx);

        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.Equal(expectedResult, (int)dict[GKey.Result]);
        Assert.Equal(expectedMessage, (string)dict[GKey.Message]);
    }

    // シナリオ3: player=null → 何も送らない
    [Fact]
    public async Task Execute_NullPlayer_NothingSent()
    {
        var (_, cmd) = BuildCommand();
        var (ctx, sent) = CommandTestHelper.MakeContext(null!);
        await cmd.ExecuteAsync(ctx);
        Assert.Empty(sent);
    }

    // シナリオ4: レスポンスに必須フィールドが揃っているか
    [Fact]
    public async Task Execute_ResponseHasAllRequiredFields()
    {
        var (_, cmd) = BuildCommand(spReturnVal: 1);
        var player   = new MajakPlayer { MemberNo = "user01" };
        var (ctx, sent) = CommandTestHelper.MakeContext(player,
            new Dictionary<string, object?>
            {
                [Key.CustomId] = 100001,
                [Key.ShopNo]   = 5,
            });

        await cmd.ExecuteAsync(ctx);

        var dict = CommandTestHelper.AsDict(sent[0].packet);
        Assert.True(dict.ContainsKey(GKey.Result));
        Assert.True(dict.ContainsKey(GKey.Message));
    }
}
