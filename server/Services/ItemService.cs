using MajakServer.Infrastructure;
using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;

namespace MajakServer.Services;

/// <summary>
/// カスタムアイテム購入/装備サービス
/// — ProcessCommand_BuyCustomItem / SetCustomItem 移植
/// </summary>
public class ItemService
{
    private readonly ItemRepository             _itemRepo;
    private readonly MasterCacheService         _masterCache;

    // 起動時インプロセスキャッシュ (Redis 未接続時のフォールバック)
    private Dictionary<int, (int Kind, string Name, long Price)> _itemMast = new();

    // デフォルトアイテムリスト (HMajCommon.h s_nDefaultCustom 配列)
    private static readonly int[] DefaultCustomItems =
    {
        GameConst.CustomBoardDefault,    // 100000 背景板
        100001,
        100002,
        GameConst.CustomHaiDefault,      // 100003 牌デザイン
        100004,
        100005,
        GameConst.CustomCostumeDefault,  // 100011 コスチューム
    };

    // カスタムアイテム KIND 定義 (MajakDef.h CUSTOMITEM_KIND)
    public const int KindBoard   = 10;
    public const int KindHai     = 20;
    public const int KindCostume = 30;

    public ItemService(
        ItemRepository             itemRepo,
        MasterCacheService         masterCache)
    {
        _itemRepo    = itemRepo;
        _masterCache = masterCache;
    }

    public async Task InitAsync()
    {
        // Redis または DB から取得し、インプロセスキャッシュに保持
        var list = await _masterCache.GetCustomItemMastAsync();
        _itemMast = list.ToDictionary(x => x.CustomId, x => (x.Kind, x.Name, x.Price));
    }

    /// <summary>
    /// カスタムアイテム所持確認 + デフォルトアイテム付与 — GetUserCustomItem
    /// </summary>
    public async Task EnsureDefaultItemsAsync(MajakPlayer player)
    {
        await _itemRepo.LoadCustomItemsAsync(player);

        if (_itemMast.Count == 0)
        {
            var customMast = await _masterCache.GetCustomItemMastAsync();
            _itemMast = customMast.ToDictionary(x => x.CustomId, x => (x.Kind, x.Name, x.Price));
        }

        var missingDefaultItems = DefaultCustomItems
            .Where(defaultId => !player.CustomItems.ContainsKey(defaultId))
            .ToArray();
        if (missingDefaultItems.Length == 0) return;

        await _itemRepo.EnsureDefaultCustomItemsAsync(
            player.MemberNo,
            missingDefaultItems
                .Select(defaultId => (CustomId: defaultId, Equip: GetDefaultEquip(defaultId) ? 1 : 0))
                .ToArray());

        foreach (int defaultId in missingDefaultItems)
        {
            int kind  = _itemMast.TryGetValue(defaultId, out var m) ? m.Kind : 0;
            int equip = GetDefaultEquip(defaultId) ? 1 : 0;
            player.CustomItems[defaultId] = new UserCustomItem { Kind = kind, Equip = equip };
        }
    }

    /// <summary>
    /// マジャクアイテム所持情報ロード — HMajDBObject::GetItemInfo 相当。
    /// ネットカフェ特典 item002 はレガシー同様メモリ上で有効化する。
    /// </summary>
    public async Task LoadMajItemsAsync(MajakPlayer player)
    {
        player.MajItems = await _itemRepo.GetAllItemsAsync(player.MemberNo) ?? [];

        if (!player.IsNetCafeIp || player.IsGuestId) return;

        DateTime now = DateTime.Now;
        bool hasActiveRichi = player.MajItems.Any(item => item.UseFlag
            && item.EndDt > now
            && item.ItemCode is "item001" or "item002" or "item004");

        var current = player.MajItems.FirstOrDefault(item => item.ItemCode == "item002");
        var privilege = current == null
            ? new MajItemInfo
            {
                ItemCode = "item002",
                BuyDt = now,
                EndDt = now.AddDays(1),
                Qty = 1,
                UseFlag = !hasActiveRichi,
            }
            : current.EndDt <= now
                ? current with { EndDt = now.AddDays(1), Qty = Math.Max(1, current.Qty), UseFlag = !hasActiveRichi }
                : current;

        ReplaceMajItem(player.MajItems, privilege);
    }

    private static void ReplaceMajItem(List<MajItemInfo> items, MajItemInfo item)
    {
        int index = items.FindIndex(x => x.ItemCode == item.ItemCode);
        if (index >= 0) items[index] = item;
        else items.Add(item);
    }

    private static bool GetDefaultEquip(int customId) => customId is
        GameConst.CustomBoardDefault or
        GameConst.CustomHaiDefault or
        GameConst.CustomCostumeDefault;

    /// <summary>
    /// カスタムアイテム購入 — ProcessCommand_BuyCustomItem (mjkc41e) 移植
    /// 現行: MySQL トランザクション内で GEM 残高確認/差引と購入付与を実行する。
    /// 戻り値: (ResultCode, Quantity)
    ///   ResultCode: Val.CustomSuccess=成功 / Val.Custom* エラーコード
    /// </summary>
    public async Task<(int ResultCode, int Quantity)>
        BuyCustomItemAsync(MajakPlayer player, int customId, int shopNo)
    {
        var shopMast = await _masterCache.GetCustomShopMastAsync();
        var shop = shopMast.FirstOrDefault(s => s.ShopNo == shopNo);
        if (shop == null)
            return (Val.CustomIdError, 0);

        customId = shop.CustomId;
        if (_itemMast.Count == 0)
        {
            var customMast = await _masterCache.GetCustomItemMastAsync();
            _itemMast = customMast.ToDictionary(x => x.CustomId, x => (x.Kind, x.Name, x.Price));
        }
        int kind = _itemMast.TryGetValue(customId, out var mast) ? mast.Kind : 0;

        if (kind == 100)
        {
            var setMast = await _masterCache.GetCustomSetMastAsync();
            if (setMast.TryGetValue(customId, out var children)
                && children.Any(childId => player.CustomItems.ContainsKey(childId)))
                return (Val.CustomOwned, 0);
        }
        else if (player.CustomItems.ContainsKey(customId))
        {
            return (Val.CustomOwned, 0);
        }

        var (rtnVal, rtnMsg) = await _itemRepo.BuyCustomItemAsync(
            memberNo: player.MemberNo,
            password: player.Password,
            shopNo:   shopNo.ToString(),
            userIp:   player.IpAddress);

        if (rtnVal != 1)
        {
            // リポジトリエラーコードを Val.Custom* にマッピング
            return rtnVal switch
            {
                -1101 => (Val.CustomCoinless, 0),   // GEM 不足
                -1102 => (Val.CustomIdError,  0),   // ユーザー/アイテム情報エラー
                -1104 => (Val.CustomError,    0),   // 配布失敗
                -1105 => (Val.CustomIdError,  0),   // アイテム情報エラー
                -1106 => (Val.CustomIdError,  0),   // セットアイテムエラー
                -2110 => (Val.CustomError,    0),   // 所持情報 INSERT 失敗
                -2104 => (Val.CustomError,    0),   // ゲームマネー配布失敗
                -1    => (Val.CustomDbError,  0),   // プロシージャ呼び出し失敗
                _     => (Val.CustomError,    0),
            };
        }

        // 原典: 購入成功時は GetUserCustomItem で所持リストを再ロードする。
        await EnsureDefaultItemsAsync(player);
        player.CashCount = await _itemRepo.GetCashCountAsync(player.MemberNo);
        return (Val.CustomSuccess, 1);
    }

    /// <summary>
    /// カスタムアイテム装備 — ProcessCommand_EquipCustomItem (mjkc38e) 移植
    /// </summary>
    public async Task<bool> EquipCustomItemAsync(MajakPlayer player, int customId)
    {
        if (!player.CustomItems.TryGetValue(customId, out var item)) return false;

        int prevId = player.GetCustomEquip(item.Kind);
        await _itemRepo.SetEquipAsync(player.MemberNo, prevId, customId);

        if (prevId != 0 && player.CustomItems.ContainsKey(prevId))
            player.CustomItems[prevId].Equip = 0;
        player.CustomItems[customId].Equip = 1;
        return true;
    }

    /// <summary>
    /// カスタムアイテム設定 — ProcessCommand_SetCustomItem (mjkc37e) 移植
    /// 原典: HMajDBObject::SetUserCustomItem。応答なしで DB 更新とメモリ装備状態を更新する。
    /// </summary>
    public async Task SetCustomItemAsync(MajakPlayer player, int customId)
    {
        if (_itemMast.Count == 0)
        {
            var customMast = await _masterCache.GetCustomItemMastAsync();
            _itemMast = customMast.ToDictionary(x => x.CustomId, x => (x.Kind, x.Name, x.Price));
        }

        int kind = _itemMast.TryGetValue(customId, out var mast) ? mast.Kind : 0;
        int prevId = player.GetCustomEquip(kind);
        await _itemRepo.SetEquipAsync(player.MemberNo, prevId, customId);

        if (prevId != 0 && player.CustomItems.ContainsKey(prevId))
            player.CustomItems[prevId].Equip = 0;

        if (player.CustomItems.TryGetValue(customId, out var item))
        {
            item.Kind = kind;
            item.Equip = 1;
        }
        else
        {
            player.CustomItems[customId] = new UserCustomItem { Kind = kind, Equip = 1 };
        }
    }

    /// <summary>
    /// 所持カスタムアイテム一覧 — ProcessCommand_GetCustomItem (mjkc39e → mjkc40e) 移植
    /// 原典: m_mapCustomItem を走査し、HMajCustomItemMast::GetCustomItem で customId/nKind/Name を送る。
    /// </summary>
    public async Task<List<Dictionary<string, object>>> GetCustomItemListAsync(MajakPlayer player)
    {
        if (_itemMast.Count == 0)
        {
            var customMast = await _masterCache.GetCustomItemMastAsync();
            _itemMast = customMast.ToDictionary(x => x.CustomId, x => (x.Kind, x.Name, x.Price));
        }

        var items = new List<Dictionary<string, object>>();
        foreach (var customId in player.CustomItems.Keys.OrderBy(x => x))
        {
            if (!_itemMast.TryGetValue(customId, out var mast))
                break;

            items.Add(new Dictionary<string, object>
            {
                ["customId"] = customId,
                ["nKind"]    = mast.Kind,
                ["Name"]     = mast.Name,
            });
        }

        return items;
    }

    /// <summary>
    /// ショップカタログ返却 (mjkc35e → mjkc36e 応答用)
    /// 原典: ProcessCommand_GetShopItem — HMajCustomShopMast 全件 + m_mapCustomItem 所持フラグ
    /// </summary>
    public List<object> GetShopCatalog(MajakPlayer player)
        => _itemMast
            .Select(kv => (object)new
            {
                customId  = kv.Key,
                kind      = kv.Value.Kind,
                name      = kv.Value.Name,
                price     = kv.Value.Price,
                purchased = player.CustomItems.ContainsKey(kv.Key) ? 1 : 0,
            })
            .ToList();
}
