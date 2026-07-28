using MajakServer.Models.Player;
using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL.Entities;
using MajakServer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MajakServer.Repositories.MySQL;

/// <summary>
/// カスタムアイテム照会/購入/装備 — HMajDBObject GetUserCustomItem / SetUserCustomItem 移植
/// </summary>
public class ItemRepository
{
    private readonly GameDataContextFactory? _gameDb;
    private readonly LogRepository? _log;
    private readonly ILogger<ItemRepository>? _logger;

    private static ulong ParseMemberNo(string memberNo)
        => MemberNoIds.Parse(memberNo);

    private async Task<TResult> ExecuteGameTransactionAsync<TResult>(
        Func<GameDataContext, IDbContextTransaction, Task<TResult>> operation)
    {
        await using var strategyDb = await RequireGameDb().CreateAsync();
        var strategy = strategyDb.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var db = await RequireGameDb().CreateAsync();
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                return await operation(db, tx);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        });
    }

    public ItemRepository()
    {
    }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public ItemRepository(
        GameDataContextFactory gameDb,
        LogRepository log,
        ILogger<ItemRepository> logger)
    {
        _gameDb = gameDb;
        _log = log;
        _logger = logger;
    }

    /// <summary>
    /// MJK_USERCUSTOMITEM + MJK_CUSTOMITEMMAST 照会 — GetUserCustomItem
    /// </summary>
    public virtual async Task LoadCustomItemsAsync(MajakPlayer player)
    {
        var memberNoValue = ParseMemberNo(player.MemberNo);
        await using var db = await RequireGameDb().CreateAsync();
        var items = await (
            from owned in db.PlayerCustomItems.AsNoTracking()
            join master in db.CustomItemMasters.AsNoTracking() on owned.CustomId equals master.CustomId
            where owned.MemberNo == memberNoValue && owned.Quantity > 0 && master.IsValid
            select new { owned.CustomId, owned.EquipSlot, master.Kind })
            .ToListAsync();
        player.CustomItems.Clear();
        foreach (var item in items)
        {
            int customId = checked((int)item.CustomId);
            player.CustomItems[customId] = new UserCustomItem
            {
                Equip = item.EquipSlot,
                Kind  = item.Kind,
            };
        }
    }

    /// <summary>
    /// デフォルトカスタムアイテム付与 (初回ログイン時) — InsertDefaultCustomItem
    /// MERGE INTO MJK_USERCUSTOMITEM
    /// </summary>
    public virtual async Task EnsureDefaultCustomItemAsync(string memberNo, int customId, int equip)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        uint id = checked((uint)customId);
        bool masterExists = await db.CustomItemMasters.AnyAsync(item => item.CustomId == id);
        bool owned = await db.PlayerCustomItems.AnyAsync(item => item.MemberNo == memberNoValue && item.CustomId == id);
        if (!masterExists || owned) return;

        var now = DateTime.Now;
        db.PlayerCustomItems.Add(new PlayerCustomItemEntity
        {
            MemberNo = memberNoValue,
            CustomId = id,
            Quantity = 1,
            EquipSlot = checked((byte)equip),
            AcquiredAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// カスタムアイテム装備/解除 — SetUserCustomItem
    /// 同じ KIND の既存装備を解除後、新しいアイテムを装備
    /// </summary>
    public virtual async Task SetEquipAsync(string memberNo, int prevCustomId, int newCustomId)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        var ids = new[] { prevCustomId, newCustomId }.Where(id => id != 0).Select(id => checked((uint)id)).ToArray();
        var items = await db.PlayerCustomItems
            .Where(item => item.MemberNo == memberNoValue && ids.Contains(item.CustomId))
            .ToListAsync();
        foreach (var item in items)
            item.EquipSlot = item.CustomId == checked((uint)newCustomId) ? (byte)1 : (byte)0;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// MJK_CUSTOMITEMMAST 全件ロード — GetCustomItemMast
    /// </summary>
    public virtual async Task<List<(int CustomId, int Kind, string Name, long Price)>> GetCustomItemMastAsync()
    {
        await using var db = await RequireGameDb().CreateAsync();
        return await db.CustomItemMasters.AsNoTracking()
            .Where(item => item.IsValid)
            .Select(item => new ValueTuple<int, int, string, long>(
                checked((int)item.CustomId), item.Kind, item.ItemName, 0L))
            .ToListAsync();
    }

    /// <summary>
    /// MJK_CUSTOMSHOPMAST 照会 — 原典: HMajDBObject::GetCustomShopMast
    /// (server/legacy/server/HMajDBObject.cpp:11398)
    ///
    /// レガシー SQL そのまま:
    ///   SELECT SHOPNO, SHOPNAME, PRICE, CUSTOMID, DESCRIPTION, GAMEMONEY, SALESDT, LIMITDT
    ///   FROM   MJK_CUSTOMSHOPMAST
    ///   WHERE  VALID = 1 AND LIMITDT > sysdate
    ///   ORDER BY SHOPNO DESC
    ///
    /// 販売期間内チェック (SALESDT ≤ now ≤ LIMITDT) は
    /// 呼び出し側 (ShopItemRequestCommand) で再フィルタする
    /// (原典: HMajChnlServer::ProcessCommand_GetShopItem 7588-7593)
    /// </summary>
    public virtual async Task<List<CustomShopItemInfo>> GetCustomShopMastAsync()
    {
        await using var db = await RequireGameDb().CreateAsync();
        var now = DateTime.Now;
        return await db.CustomShopMasters.AsNoTracking()
            .Where(item => item.IsValid && (item.SaleEndAt == null || item.SaleEndAt > now))
            .OrderByDescending(item => item.ShopNo)
            .Select(item => new CustomShopItemInfo
            {
                ShopNo = checked((int)item.ShopNo),
                ShopName = item.ShopName,
                Price = checked((int)item.HcPrice),
                CustomId = checked((int)item.CustomId),
                Description = item.Description ?? string.Empty,
                GameMoney = item.GameMoney,
                SalesDt = item.SaleStartAt ?? DateTime.MinValue,
                LimitDt = item.SaleEndAt ?? DateTime.MaxValue,
            })
            .ToListAsync();
    }

    /// <summary>
    /// MJK_CUSTOMSETMAST 照会 — 原典: HMajDBObject::GetCustomSetMast (HMajDBObject.cpp:11483)
    /// セット商品 (kind=100) の構成アイテム一覧を返す。
    /// 戻り値: SetCustomId → 子 CustomId のリスト
    /// </summary>
    public virtual async Task<Dictionary<int, List<int>>> GetCustomSetMastAsync()
    {
        await using var db = await RequireGameDb().CreateAsync();
        var rows = await (
            from set in db.CustomItemSets.AsNoTracking()
            join master in db.CustomItemMasters.AsNoTracking() on set.SetId equals master.CustomId
            where master.IsValid
            select new { set.SetId, set.CustomId })
            .ToListAsync();
        return rows
            .GroupBy(row => checked((int)row.SetId))
            .ToDictionary(group => group.Key, group => group.Select(row => checked((int)row.CustomId)).ToList());
    }

    private GameDataContextFactory RequireGameDb()
        => _gameDb ?? throw new InvalidOperationException("MySQL GameDataContextFactory is not configured.");

    private LogRepository RequireLog()
        => _log ?? throw new InvalidOperationException("MySQL LogRepository is not configured.");

    private async Task InsertGameMoneyHistFromTransactionCodeAsync(
        string memberNo,
        string eventCode,
        long eventMoney,
        long preMoney,
        long afterMoney,
        string remoteAddress)
    {
        if (_gameDb is null || _log is null) return;

        var metadata = await TransactionCodeMetadataResolver.ResolveAsync(_gameDb, eventCode);
        if (metadata is null) return;

        await _log.InsertGameMoneyHistAsync(
            memberNo, eventCode, eventMoney, preMoney, afterMoney, remoteAddress,
            metadata.EventTitle, gameId: metadata.GameId, isValid: metadata.IsHistoryEnabled);
    }

    /// <summary>PC_GMBSYS_MAJAKCUSTOMITEMBUY equivalent.</summary>
    public virtual async Task<(int RtnVal, string RtnMessage)> BuyCustomItemAsync(
        string memberNo,
        string password,
        string shopNo,
        string userIp)
    {
        if (!uint.TryParse(shopNo, out uint parsedShopNo))
            return (-1105, "item info error-1");

        try
        {
            return await ExecuteGameTransactionAsync(async (db, tx) =>
            {
            var memberNoValue = ParseMemberNo(memberNo);
            var now = DateTime.Now;
            var product = await (
                from shop in db.CustomShopMasters
                join master in db.CustomItemMasters on shop.CustomId equals master.CustomId
                where shop.ShopNo == parsedShopNo
                    && shop.IsValid
                    && master.IsValid
                    && (shop.SaleStartAt == null || shop.SaleStartAt <= now)
                    && (shop.SaleEndAt == null || shop.SaleEndAt > now)
                select new { Shop = shop, Master = master })
                .SingleOrDefaultAsync();
            if (product is null)
                return (-1105, "item info error-1");

            List<uint> grantIds;
            if (product.Master.Kind == 100)
            {
                grantIds = await (
                    from setItem in db.CustomItemSets
                    join master in db.CustomItemMasters on setItem.CustomId equals master.CustomId
                    where setItem.SetId == product.Master.CustomId && master.IsValid
                    select setItem.CustomId)
                    .ToListAsync();
                if (grantIds.Count == 0)
                    return (-1106, "setitem info error-1");
            }
            else
            {
                grantIds = [product.Master.CustomId];
            }

            int gemPrice = checked((int)product.Shop.HcPrice);
            var wallet = await db.PlayerWallets.SingleOrDefaultAsync(item => item.MemberNo == memberNoValue);
            if (wallet is null)
                return (-1102, "item info error-1");
            if (wallet.GemCount < gemPrice)
                return (-1101, "GEM Not Enough");
            wallet.GemCount -= gemPrice;
            wallet.UpdatedAt = now;

            var ownedItems = await db.PlayerCustomItems
                .Where(item => item.MemberNo == memberNoValue && grantIds.Contains(item.CustomId))
                .ToDictionaryAsync(item => item.CustomId);
            foreach (uint customId in grantIds)
            {
                if (ownedItems.TryGetValue(customId, out var owned))
                {
                    owned.Quantity = checked((ushort)(owned.Quantity + 1));
                    owned.UpdatedAt = now;
                }
                else
                {
                    db.PlayerCustomItems.Add(new PlayerCustomItemEntity
                    {
                        MemberNo = memberNoValue,
                        CustomId = customId,
                        Quantity = 1,
                        EquipSlot = 0,
                        AcquiredAt = now,
                        UpdatedAt = now,
                    });
                }
            }

            long moneyBefore = 0;
            long moneyAfter = 0;
            if (product.Shop.GameMoney > 0)
            {
                moneyBefore = checked(wallet.GameMoney + wallet.PendingGameMoney + wallet.EarnedGameMoney);
                wallet.EarnedGameMoney = checked(wallet.EarnedGameMoney + product.Shop.GameMoney);
                wallet.UpdatedAt = now;
                moneyAfter = checked(wallet.GameMoney + wallet.PendingGameMoney + wallet.EarnedGameMoney);
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            try
            {
                await RequireLog().InsertItemPurchaseHistAsync(
                    memberNo,
                    product.Master.CustomId.ToString(),
                    quantity: 1,
                    unitPrice: product.Shop.HcPrice,
                    purchaseChannel: 2);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex,
                    "Custom item purchase log failed after successful purchase: memberNo={MemberNo}, shopNo={ShopNo}",
                    memberNo, shopNo);
            }

            if (product.Shop.GameMoney > 0)
            {
                try
                {
                    await InsertGameMoneyHistFromTransactionCodeAsync(memberNo,
                        product.Shop.AvCode ?? string.Empty,
                        product.Shop.GameMoney, moneyBefore, moneyAfter, userIp);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex,
                        "Custom item game-money log failed after purchase: memberNo={MemberNo}, shopNo={ShopNo}",
                        memberNo, shopNo);
                }
            }

            return (1, "success");
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "MySQL custom item purchase failed: memberNo={MemberNo}, shopNo={ShopNo}", memberNo, shopNo);
            return (-1104, "OTHERS DATA ERR 4");
        }
    }

    /// <summary>PC_GMBSYS_MAJAKBILLINGBUY equivalent.</summary>
    public virtual async Task<(int RtnVal, string RtnMsg)> BuyBillingItemAsync(
        string memberNo,
        string password,
        string itemCode,
        string subCode,
        int validDays,
        string userIp,
        int buyCate)
    {
        try
        {
            return await ExecuteGameTransactionAsync(async (db, tx) =>
            {
            var memberNoValue = ParseMemberNo(memberNo);
            var master = await db.BillingItemMasters.AsNoTracking()
                .SingleOrDefaultAsync(item => item.ItemCode == itemCode && item.SubCode == subCode);
            if (master?.UnitMoney is null)
                return (-1102, "item info error-1");

            int gemPrice = checked((int)master.UnitMoney.Value);
            var wallet = await db.PlayerWallets.SingleOrDefaultAsync(item => item.MemberNo == memberNoValue);
            if (wallet is null)
                return (-1102, "item info error-1");
            if (wallet.GemCount < gemPrice)
                return (-1101, "GEM Not Enough");
            wallet.GemCount -= gemPrice;
            wallet.UpdatedAt = DateTime.Now;

            var now = DateTime.Now;
            var owned = await db.PlayerFunctionItems
                .SingleOrDefaultAsync(item => item.MemberNo == memberNoValue && item.ItemCode == itemCode);
            if (owned is null)
            {
                db.PlayerFunctionItems.Add(new PlayerFunctionItemEntity
                {
                    MemberNo = memberNoValue,
                    ItemCode = itemCode,
                    Quantity = 1,
                    BoughtAt = now,
                    ExpiresAt = now.AddDays(validDays),
                    IsEquipped = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
            else
            {
                DateTime extensionBase = owned.ExpiresAt is not null && owned.ExpiresAt > now
                    ? owned.ExpiresAt.Value
                    : now;
                if (owned.ExpiresAt is null || owned.ExpiresAt <= now)
                    owned.BoughtAt = now;
                owned.ExpiresAt = extensionBase.AddDays(validDays);
                owned.IsEquipped = true;
                owned.UpdatedAt = now;
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            try
            {
                await RequireLog().InsertItemPurchaseHistAsync(
                    memberNo,
                    subCode,
                    quantity: 1,
                    unitPrice: master.UnitMoney.Value,
                    purchaseChannel: checked((byte)buyCate));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex,
                    "Billing item purchase log failed after successful purchase: memberNo={MemberNo}, itemCode={ItemCode}, subCode={SubCode}",
                    memberNo, itemCode, subCode);
            }

            return (1, "success");
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "MySQL billing item purchase failed: memberNo={MemberNo}, itemCode={ItemCode}, subCode={SubCode}, buyCate={BuyCate}",
                memberNo, itemCode, subCode, buyCate);
            return (-2001, "billing item distribute error");
        }
    }

    /// <summary>
    /// MJK_ITEMLIST 照会 — GetItemInfo (アイテム所持情報)
    /// </summary>
    public async Task<(DateTime? BuyDt, DateTime? EndDt, int Qty)> GetItemInfoAsync(
        string memberNo, string itemCode)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        var item = await db.PlayerFunctionItems.AsNoTracking()
            .SingleOrDefaultAsync(owned => owned.MemberNo == memberNoValue && owned.ItemCode == itemCode);
        return item is null
            ? (null, null, 0)
            : (item.BoughtAt, item.ExpiresAt, checked((int)item.Quantity));
    }

    /// <summary>
    /// MJK_ITEMLIST 全件照会 — GetItemInfo (複数アイテム)
    /// 原典: HMajDBObject::GetItemInfo
    /// </summary>
    public virtual async Task<List<MajItemInfo>> GetAllItemsAsync(string memberNo)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        var now = DateTime.Now;
        var items = await db.PlayerFunctionItems.AsNoTracking()
            .Where(item => item.MemberNo == memberNoValue)
            .ToListAsync();
        return items.Select(item => new MajItemInfo
        {
            ItemCode = item.ItemCode,
            BuyDt = item.BoughtAt,
            EndDt = item.ExpiresAt ?? DateTime.MaxValue,
            Qty = checked((int)item.Quantity),
            UseFlag = item.IsEquipped && (item.ExpiresAt == null || item.ExpiresAt > now),
        }).ToList();
    }

    /// <summary>
    /// MJK_ITEMLIST MERGE — ExchangeItem の ITEM カテゴリ部分
    /// 原典: HMajDBObject::ExchangeItem (CAT_ITEM)
    ///   MERGE INTO MJK_ITEMLIST: 既存なら ENDDT 延長 + QTY 加算, なければ INSERT
    /// </summary>
    public virtual async Task<MajItemInfo> UpsertMajItemAsync(
        string memberNo, string itemCode, int validDays, int qty)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        var item = await UpsertFunctionItemAsync(db, memberNoValue, itemCode, validDays, qty, DateTime.Now);
        await db.SaveChangesAsync();
        return ToMajItemInfo(item);
    }

    /// <summary>
    /// HMajDBObject::ExchangeItem CAT_ITEM equivalent.
    /// Cost debit, old item USEFLG off, and new item MERGE run in one transaction.
    /// </summary>
    public virtual async Task<MajItemInfo?> ExchangeMajItemAsync(
        string memberNo, string itemCode, int validDays, int qty, long costMoney, int costGem, string? oldUseItemCode)
    {
        try
        {
            return await ExecuteGameTransactionAsync(async (db, tx) =>
            {
            var memberNoValue = ParseMemberNo(memberNo);
            if (costMoney > 0 || costGem > 0)
            {
                var wallet = await db.PlayerWallets.SingleOrDefaultAsync(item => item.MemberNo == memberNoValue);
                if (wallet is null || wallet.GameMoney < costMoney || wallet.GemCount < costGem)
                {
                    await tx.RollbackAsync();
                    return null;
                }
                wallet.GameMoney -= costMoney;
                wallet.GemCount -= costGem;
                wallet.UpdatedAt = DateTime.Now;
            }

            if (!string.IsNullOrEmpty(oldUseItemCode) && oldUseItemCode != itemCode)
            {
                var oldItem = await db.PlayerFunctionItems.SingleOrDefaultAsync(
                    item => item.MemberNo == memberNoValue && item.ItemCode == oldUseItemCode);
                if (oldItem is not null) oldItem.IsEquipped = false;
            }

            var item = await UpsertFunctionItemAsync(db, memberNoValue, itemCode, validDays, qty, DateTime.Now);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return ToMajItemInfo(item);
            });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>HMajDBObject::ExchangeItem CAT_TITLE equivalent.</summary>
    public virtual async Task<bool> ExchangeTitleAsync(string memberNo, string titleId, long costMoney, int costGem, bool ignoreAlreadyOwned)
    {
        try
        {
            return await ExecuteGameTransactionAsync(async (db, tx) =>
            {
            var memberNoValue = ParseMemberNo(memberNo);
            bool owned = await db.PlayerTitles.AnyAsync(title => title.MemberNo == memberNoValue && title.TitleId == titleId);
            if (owned && !ignoreAlreadyOwned)
            {
                await tx.RollbackAsync();
                return false;
            }

            if (costMoney > 0 || costGem > 0)
            {
                var wallet = await db.PlayerWallets.SingleOrDefaultAsync(item => item.MemberNo == memberNoValue);
                if (wallet is null || wallet.GameMoney < costMoney || wallet.GemCount < costGem)
                {
                    await tx.RollbackAsync();
                    return false;
                }
                wallet.GameMoney -= costMoney;
                wallet.GemCount -= costGem;
                wallet.UpdatedAt = DateTime.Now;
            }

            if (!owned)
                db.PlayerTitles.Add(new PlayerTitleEntity
                {
                    MemberNo = memberNoValue,
                    TitleId = titleId,
                    AcquiredAt = DateTime.Now,
                });

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
            });
        }
        catch
        {
            return false;
        }
    }

    /// <summary>HMajDBObject::ExchangeItem CAT_AVATAR equivalent.</summary>
    public virtual async Task<bool> ExchangeAvatarAsync(string memberNo, string avatarCode, long costMoney, int costGem)
    {
        if (string.IsNullOrWhiteSpace(memberNo) || string.IsNullOrWhiteSpace(avatarCode) ||
            costMoney < 0 || costGem < 0)
            return false;

        try
        {
            return await ExecuteGameTransactionAsync(async (db, tx) =>
            {
            var memberNoValue = ParseMemberNo(memberNo);
            var wallet = await db.PlayerWallets.SingleOrDefaultAsync(item => item.MemberNo == memberNoValue);
            if (wallet is null || wallet.GameMoney < costMoney || wallet.GemCount < costGem)
            {
                await tx.RollbackAsync();
                return false;
            }

            var now = DateTime.Now;
            wallet.GameMoney -= costMoney;
            wallet.GemCount -= costGem;
            wallet.UpdatedAt = now;
            db.PlayerAvatarInventory.Add(new PlayerAvatarInventoryEntity
            {
                MemberNo = memberNoValue,
                AvatarCode = avatarCode,
                CostMoney = costMoney,
                CostGem = costGem,
                AcquiredAt = now,
            });

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
            });
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// MJK_ITEMLIST USEFLG 更新 — UpdateItemUseFlag
    /// 原典: HMajDBObject::UpdateItemUseFlag
    /// </summary>
    public virtual async Task UpdateMajItemUseFlagAsync(string memberNo, string itemCode, bool use)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        await using var db = await RequireGameDb().CreateAsync();
        await db.PlayerFunctionItems
            .Where(item => item.MemberNo == memberNoValue && item.ItemCode == itemCode)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.IsEquipped, use)
                .SetProperty(item => item.UpdatedAt, DateTime.Now));
    }

    /// <summary>
    /// HMajDBObject::UpdateItemInUse equivalent. Old OFF and new ON are committed together.
    /// </summary>
    public virtual async Task<bool> UpdateMajItemInUseAsync(string memberNo, string? oldItemCode, string newItemCode)
    {
        var memberNoValue = ParseMemberNo(memberNo);
        try
        {
            return await ExecuteGameTransactionAsync(async (db, tx) =>
            {
            if (!string.IsNullOrEmpty(oldItemCode))
            {
                await db.PlayerFunctionItems
                    .Where(item => item.MemberNo == memberNoValue && item.ItemCode == oldItemCode)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsEquipped, false));
            }

            int updated = await db.PlayerFunctionItems
                .Where(item => item.MemberNo == memberNoValue && item.ItemCode == newItemCode)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.IsEquipped, true)
                    .SetProperty(item => item.UpdatedAt, DateTime.Now));
            if (updated == 0)
            {
                await tx.RollbackAsync();
                return false;
            }

            await tx.CommitAsync();
            return true;
            });
        }
        catch
        {
            return false;
        }
    }

    private static async Task<PlayerFunctionItemEntity> UpsertFunctionItemAsync(
        GameDataContext db, ulong memberNoValue, string itemCode, int validDays, int quantity, DateTime now)
    {
        var item = await db.PlayerFunctionItems.SingleOrDefaultAsync(
            owned => owned.MemberNo == memberNoValue && owned.ItemCode == itemCode);
        if (item is null)
        {
            item = new PlayerFunctionItemEntity
            {
                MemberNo = memberNoValue,
                ItemCode = itemCode,
                Quantity = checked((uint)(quantity > 0 ? quantity : 1)),
                BoughtAt = now,
                ExpiresAt = validDays > 0 ? now.AddDays(validDays) : null,
                IsEquipped = true,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.PlayerFunctionItems.Add(item);
            return item;
        }

        bool isActive = item.ExpiresAt is null || item.ExpiresAt > now;
        if (!isActive) item.BoughtAt = now;
        item.ExpiresAt = validDays > 0
            ? (isActive && item.ExpiresAt.HasValue ? item.ExpiresAt.Value : now).AddDays(validDays)
            : null;
        item.Quantity = checked(item.Quantity + (uint)quantity);
        item.IsEquipped = true;
        item.UpdatedAt = now;
        return item;
    }

    private static MajItemInfo ToMajItemInfo(PlayerFunctionItemEntity item)
        => new()
        {
            ItemCode = item.ItemCode,
            BuyDt = item.BoughtAt,
            EndDt = item.ExpiresAt ?? DateTime.MaxValue,
            Qty = checked((int)item.Quantity),
            UseFlag = item.IsEquipped,
        };
}

// ─── MJK_ITEMLIST 行データ ──────────────────────────────────────────────────
public record MajItemInfo
{
    public string   ItemCode { get; init; } = "";
    public DateTime BuyDt    { get; init; }
    public DateTime EndDt    { get; init; }
    public int      Qty      { get; init; }
    public bool     UseFlag  { get; init; }
    public bool     IsValid  => UseFlag && EndDt > DateTime.Now;
}
