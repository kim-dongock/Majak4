using Microsoft.AspNetCore.SignalR;
using MajakServer.Models.Protocol;
using MajakServer.Services;
using MajakServer.Repositories.MySQL;
using MajakServer.Infrastructure;

namespace MajakServer.Commands.Channel;

/// <summary>
/// mjkc35e カスタムショップ一覧要求 → mjkc36e 応答
/// 原典: HMajChnlServer::ProcessCommand_GetShopItem
///        (server/legacy/server/HMajChnlServer.cpp:7549)
///
/// レガシー処理:
///   1. HMajCustomShopMast::m_vecShop (DB: MJK_CUSTOMSHOPMAST, VALID=1 AND LIMITDT>SYSDATE) を取得
///   2. 販売期間 SALESDT ≤ now ≤ LIMITDT のみカウント (二重ループ)
///   3. binary で 1件あたり {shopNo, customId, customType, customPrice, Name, description, gameMoney, Purchased}
///   4. kind==100 (セット) の場合 MJK_CUSTOMSETMAST から子アイテム所持確認
///   5. それ以外は m_mapCustomItem.count(customId) で所持判定
///
/// .NET 実装: JSON 形式に変換するが、フィールド名・順序・フィルタは原典厳守
/// </summary>
public class ShopItemRequestCommand : ICommand
{
    private readonly MasterCacheService _masterCache;

    public ShopItemRequestCommand(MasterCacheService masterCache)
    {
        _masterCache = masterCache;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        // 原典: HMajCustomShopMast::m_vecShop (MJK_CUSTOMSHOPMAST)
        var shopMast = await _masterCache.GetCustomShopMastAsync();

        // 原典: MJK_CUSTOMSETMAST (kind==100 のセット商品用)
        var setMast = await _masterCache.GetCustomSetMastAsync();

        // 原典: HMajCustomItemMast::GetCustomKind — customId → kind マッピング
        var itemMast = await _masterCache.GetCustomItemMastAsync();
        var kindMap  = itemMast.ToDictionary(x => x.CustomId, x => x.Kind);

        // 原典 7585~7613: 販売期間フィルタ + Purchased 判定
        var now = DateTime.Now;
        var catalog = new List<Dictionary<string, object>>();
        foreach (var s in shopMast)
        {
            // 原典 7589-7592: if(ctNow < ctStart) continue; if(ctNow > ctEnd) continue;
            if (now < s.SalesDt) continue;
            if (now > s.LimitDt) continue;

            int kind = kindMap.TryGetValue(s.CustomId, out var k) ? k : 0;

            // 原典 7625-7641: Purchased 判定
            int purchased;
            if (kind == 100)
            {
                // セット商品: 子アイテムが1つでも所持済みなら Purchased=1
                purchased = 0;
                if (setMast.TryGetValue(s.CustomId, out var children))
                {
                    foreach (var childId in children)
                    {
                        if (player.CustomItems.ContainsKey(childId)) { purchased = 1; break; }
                    }
                }
            }
            else
            {
                // 通常商品: m_mapCustomItem.count(customId) > 0 で所持判定
                purchased = player.CustomItems.ContainsKey(s.CustomId) ? 1 : 0;
            }

            catalog.Add(new Dictionary<string, object>
            {
                // 原典 shopInfo: shopNo/customId/customType/customPrice/Name/description/gameMoney/Purchased
                ["shopNo"]      = s.ShopNo,
                ["customId"]    = s.CustomId,
                ["customType"]  = kind,
                ["customPrice"] = s.Price,
                ["Name"]        = s.ShopName,
                ["description"] = s.Description,
                ["gameMoney"]   = s.GameMoney,
                ["Purchased"]   = purchased,

                // JSON 移植クライアント互換フィールド
                ["kind"]        = kind,
                ["price"]       = s.Price,
                ["name"]        = s.ShopName,
                ["purchased"]   = purchased,
            });
        }

        await ctx.Caller.SendAsync(Cmd.ShopItemResponse, new
        {
            shopCnt  = catalog.Count,
            shopList = catalog,
        });
    }
}

/// <summary>mjkc39e カスタムアイテムリスト要求 → mjkc40e 応答</summary>
public class CustomItemCommand : ICommand
{
    private readonly ItemService _itemService;

    public CustomItemCommand(ItemService itemService)
    {
        _itemService = itemService;
    }

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        var items = await _itemService.GetCustomItemListAsync(player);

        await ctx.Caller.SendAsync(Cmd.CustomItemResponse, new
        {
            customCnt = items.Count,
            items,
        });
    }
}

/// <summary>mjkc41e カスタムアイテム購入 → mjkc42e 応答</summary>
public class BuyCustomItemCommand : ICommand
{
    private readonly ItemService _itemService;

    public BuyCustomItemCommand(ItemService itemService) => _itemService = itemService;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null)
        {
            await SendResultAsync(ctx, Val.CustomDbError);
            return;
        }

        int shopNo = ctx.GetInt(Key.ShopNo, 0);
        if (shopNo <= 0 && int.TryParse(ctx.GetString(Key.ShopNo), out int parsedShopNo))
            shopNo = parsedShopNo;

        int resultCode;
        int cashCount = player.CashCount;
        try
        {
            var result = await _itemService.BuyCustomItemAsync(player, 0, shopNo);
            resultCode = result.ResultCode;
            cashCount = player.CashCount;
        }
        catch
        {
            resultCode = Val.CustomDbError;
        }

        await SendResultAsync(ctx, resultCode, cashCount);
    }

    private static Task SendResultAsync(CommandContext ctx, int resultCode, int? cashCount = null)
    {
        string message = resultCode == Val.CustomSuccess ? "" : CustomItemErrorMessage(resultCode);
        var packet = new Dictionary<string, object>
        {
            [GKey.Result] = resultCode,
            [GKey.Message] = message,
        };
        if (cashCount.HasValue)
            packet["cashCount"] = cashCount.Value;
        return ctx.Caller.SendAsync(Cmd.BuyCustomItemResponse, packet);
    }

    private static string CustomItemErrorMessage(int resultCode) => resultCode switch
    {
        Val.CustomCoinless => "GEMが足りません",
        Val.CustomOwned    => "既に所持しているアイテムです",
        Val.CustomIdError  => "IDが不正です",
        Val.CustomDbError  => "接続エラー",
        Val.CustomError    => "不明なエラー",
        _                  => "不明なエラー",
    };
}

/// <summary>mjkc38e カスタムアイテム装備 (装備/解除)</summary>
public class EquipCustomItemCommand : ICommand
{
    private readonly ItemService _itemService;

    public EquipCustomItemCommand(ItemService itemService) => _itemService = itemService;

    public async Task ExecuteAsync(CommandContext ctx)
    {
        var player = ctx.Player;
        if (player == null) return;

        int customId = ctx.GetInt(Key.CustomId);
        bool ok = await _itemService.EquipCustomItemAsync(player, customId);

        await ctx.Caller.SendAsync(Cmd.EquipCustomItem, new
        {
            result   = ok ? 1 : 0,
            customId = customId,
        });
    }
}

