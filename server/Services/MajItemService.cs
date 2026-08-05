using MajakServer.Models.Player;
using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;

namespace MajakServer.Services;

/// <summary>
/// マジャクアイテム購入/選択サービス
/// 原典: HMajChnlServer::ProcessCommand_BuyMajItem (mjkc20e)
///       HMajChnlServer::ProcessCommand_SelectMajItem (mjkc21e)
///
/// MJSELLMAST データ: レガシー HMajItem.cpp のハードコード配列に対応。
/// カテゴリ:
///   CAT_ITEM    (3) — MJK_ITEMLIST MERGE
///   CAT_TITLE   (2) — InsertMajakTitle
///   CAT_AVATAR  (1) — AVATAR_DISTRIBUTE_ONLY_PC
///   CAT_BILLING (4) — PC_GMBSYS_MAJAKBILLINGBUY ストアドプロシージャ
/// </summary>
public class MajItemService
{
    private readonly ItemRepository            _itemRepo;
    private readonly PlayerRepository          _playerRepo;
    private readonly HistoryRepository         _histRepo;

    // ─── MJSELLMAST カテゴリ定数 ─────────────────────────────────────────────
    public const int CatAvatar  = 1;
    public const int CatTitle   = 2;
    public const int CatItem    = 3;
    public const int CatBilling = 4;

    private const int MissionConditionExchangeGem = 4;
    private const int MissionConditionBuyBillingItem = 6;

    private const int EItemGemShort    = 0;
    private const int EItemMoneyShort  = 1;
    private const int EItemDbError     = 2;
    private const int EItemSellCode    = 3;
    private const int EItemNotOwn      = 4;
    private const int EItemItemCode    = 5;
    private const int EItemAlreadyUse  = 6;
    private const int EItemInnerError  = 7;
    private const int EItemAlreadyOwn  = 8;
    private const int EItemExpired     = 9;
    private const int EItemMustTitle   = 10;
    private const int EItemExhausted   = 11;

    // ─── MJSELLMAST マスターデータ ────────────────────────────────────────────
    // 原典: static const MJSELLMAST sell[] in HMajItem.cpp
    // SellCode / EvtCodeBuy / EvtCodeUse / Category / ItemCode / ValidDays / CostGem / Quantity / CostMoney / RequiredTitle
    private static readonly MajSellMast[] SellMasters =
    {
        new("sell001", CatItem,   "item001", null,      3,  2,   0,     10,    null),
        new("sell003", CatItem,   "item002", null,      3,  10,  0,     500,   null),
        new("sell005", CatItem,   "item003", null,      3,  5,   0,     100,   null),
        new("sell007", CatTitle,  "mjkt100", null,      0,  0,   5,     150,   null),
        new("sell008", CatTitle,  "mjkt101", null,      0,  0,   50,    1500,  null),
        new("sell009", CatTitle,  "mjkt102", null,      0,  0,   100,   7500,  null),
        new("sell011", CatAvatar, "AA11S",   "AA11S",   0,  0,   10,    500,   null),
        new("sell012", CatAvatar, "A45ZG",   "A45ZG",   0,  0,   15,    750,   null),
        new("sell014", CatAvatar, "LC1CUS",  "LC2CVP",  0,  0,   15,    750,   null),
        new("sell016", CatAvatar, "P111JU",  "P121LK",  0,  0,   15,    750,   null),
        new("sell017", CatAvatar, "A2376",   "A2376",   0,  0,   15,    750,   null),
        new("sell019", CatItem,   "item004", null,      3,  5,   0,     250,   null),
        new("sell020", CatAvatar, "A239Y",   "A239Y",   15, 0,   2,     10,    null),
        new("sell021", CatAvatar, "A23DW",   "A23DW",   0,  0,   10,    500,   null),
        new("sell022", CatAvatar, "AA18V",   "AA18V",   0,  0,   15,    750,   null),
        new("sell023", CatTitle,  "mjks013", null,      0,  0,   50,    2500,  null),
        new("sell024", CatTitle,  "mjkt103", null,      0,  0,   1,     100,   null),
        new("sell025", CatTitle,  "mjkt104", null,      0,  0,   20,    2000,  "mjkt103"),
        new("sell026", CatTitle,  "mjkt105", null,      0,  0,   50,    5000,  "mjkt104"),
        new("sell027", CatTitle,  "mjkt106", null,      0,  0,   100,   10000, "mjkt105"),
        new("sell028", CatTitle,  "mjkt107", null,      0,  0,   200,   20000, "mjkt106"),
        new("sell029", CatTitle,  "mjkt108", null,      0,  0,   500,   50000, "mjkt107"),
        new("sell030", CatTitle,  "mjkt109", null,      0,  0,   1000,  100000,"mjkt108"),
        new("sell031", CatTitle,  "mjkt110", null,      0,  0,   2000,  200000,"mjkt109"),
        new("sell032", CatTitle,  "mjkt111", null,      0,  0,   5000,  500000,"mjkt110"),
        new("sell033", CatTitle,  "mjkt112", null,      0,  0,   10000, 1000000,"mjkt111"),
        new("sell034", CatTitle,  "mjkt113", null,      0,  0,   20000, 2000000,"mjkt112"),
        new("sell035", CatTitle,  "mjkt114", null,      0,  0,   50000, 5000000,"mjkt113"),
        new("sell036", CatTitle,  "mjkt115", null,      0,  0,   0,     0,      null),
        new("sell037", CatTitle,  "mjkt116", null,      0,  0,   0,     0,      null),
        new("sell038", CatTitle,  "mjkt117", null,      0,  0,   0,     0,      null),
        new("sell039", CatTitle,  "mjkt118", null,      0,  0,   0,     0,      null),
        new("sell040", CatBilling,"MJ20",    "MJ2001",  0,  0,   2,     0,     null),
        new("sell041", CatBilling,"MJ20",    "MJ2002",  0,  0,   6,     -3000, null),
        new("sell042", CatBilling,"MJ20",    "MJ2003",  0,  0,   12,    -5000, null),
        new("sell043", CatBilling,"MJ20",    "MJ2004",  0,  0,   40,    -15000,null),
        new("sell044", CatBilling,"MJ21",    "MJ2101",  1,  0,   0,     -2000, null),
        new("sell045", CatBilling,"MJ21",    "MJ2102",  3,  0,   0,     -3000, null),
        new("sell046", CatBilling,"MJ21",    "MJ2103",  7,  0,   0,     -5000, null),
        new("sell047", CatBilling,"MJ21",    "MJ2104",  30, 0,   0,     -15000,null),
        new("sell048", CatBilling,"MJ22",    "MJ2201",  1,  0,   0,     -4000, null),
        new("sell049", CatBilling,"MJ22",    "MJ2202",  3,  0,   0,     -6000, null),
        new("sell050", CatBilling,"MJ22",    "MJ2203",  7,  0,   0,     -10000,null),
        new("sell051", CatBilling,"MJ22",    "MJ2204",  30, 0,   0,     -30000,null),
        new("sell052", CatAvatar, "A24Z5",   "A24Z5",   0,  0,   20,    2000,  null),
        new("sell053", CatAvatar, "A49WK",   "A49WK",   0,  0,   50,    5000,  null),
        new("sell054", CatAvatar, "A14D8",   "A14D8",   0,  0,   100,   10000, null),
        new("sell055", CatAvatar, "LC1EDW",  "LC2EG3",  0,  0,   200,   20000, null),
        new("sell056", CatAvatar, "A24ZR",   "A24ZR",   0,  0,   20,    2000,  null),
        new("sell057", CatAvatar, "A49XX",   "A49XX",   0,  0,   50,    5000,  null),
        new("sell058", CatAvatar, "A14DV",   "A14DV",   0,  0,   100,   10000, null),
        new("sell059", CatAvatar, "LC1EDY",  "LC2EG5",  0,  0,   200,   20000, null),
        new("sell060", CatAvatar, "A2507",   "A2507",   0,  0,   20,    2000,  null),
        new("sell061", CatAvatar, "A49ZN",   "A49ZN",   0,  0,   50,    5000,  null),
        new("sell062", CatAvatar, "A14EI",   "A14EI",   0,  0,   100,   10000, null),
        new("sell063", CatAvatar, "LC1EE4",  "LC2EGB",  0,  0,   200,   20000, null),
        new("sell064", CatAvatar, "A14EZ",   "A14EZ",   0,  0,   20,    2000,  null),
        new("sell065", CatAvatar, "A4A26",   "A4A26",   0,  0,   50,    5000,  null),
        new("sell066", CatAvatar, "A2512",   "A2512",   0,  0,   100,   10000, null),
        new("sell067", CatAvatar, "LD148O",  "LD249M",  0,  0,   200,   20000, null),
        new("sell068", CatAvatar, "A251U",   "A251U",   0,  0,   20,    2000,  null),
        new("sell069", CatAvatar, "A4A43",   "A4A43",   0,  0,   50,    5000,  null),
        new("sell070", CatAvatar, "A14FI",   "A14FI",   0,  0,   100,   10000, null),
        new("sell071", CatAvatar, "LC1EEJ",  "LC2EGQ",  0,  0,   200,   20000, null),
        new("sell072", CatAvatar, "E31N4",   "E31N4",   0,  0,   20,    2000,  null),
        new("sell073", CatAvatar, "AE2ZT",   "AE2ZT",   0,  0,   100,   10000, null),
        new("sell074", CatAvatar, "LC1EEO",  "LC2EGV",  0,  0,   200,   20000, null),
        new("sell075", CatItem,   "MJ23",    "MJ2301",  0,  20,  1,     0,     null),
        new("sell076", CatItem,   "MJ23",    "MJ2302",  0,  10,  50,    0,     null),
    };

    private static readonly Dictionary<string, (string Money, string Gem)> ProCodes = new(StringComparer.Ordinal)
    {
        ["sell001"] = ("JM00123", "JM00128"), ["sell003"] = ("JM00123", "JM00128"),
        ["sell005"] = ("JM00124", "JM00129"), ["sell007"] = ("JM00122", "JM00127"),
        ["sell008"] = ("JM00122", "JM00127"), ["sell009"] = ("JM00122", "JM00127"),
        ["sell011"] = ("JM00121", "JM00126"), ["sell012"] = ("JM00132", "JM00138"),
        ["sell014"] = ("JM00134", "JM00140"), ["sell016"] = ("JM00136", "JM00142"),
        ["sell017"] = ("JM00137", "JM00143"), ["sell019"] = ("JM00123", "JM00128"),
        ["sell020"] = ("JM00144", "JM00145"), ["sell021"] = ("JM00167", "JM00168"),
        ["sell022"] = ("JM00169", "JM00170"), ["sell023"] = ("JM00146", "JM00147"),
        ["sell024"] = ("JM00122", "JM00127"), ["sell025"] = ("JM00122", "JM00127"),
        ["sell026"] = ("JM00122", "JM00127"), ["sell027"] = ("JM00122", "JM00127"),
        ["sell028"] = ("JM00122", "JM00127"), ["sell029"] = ("JM00122", "JM00127"),
        ["sell030"] = ("JM00122", "JM00127"), ["sell031"] = ("JM00122", "JM00127"),
        ["sell032"] = ("JM00122", "JM00127"), ["sell033"] = ("JM00122", "JM00127"),
        ["sell034"] = ("JM00122", "JM00127"), ["sell035"] = ("JM00122", "JM00127"),
        ["sell036"] = ("JM00122", "JM00127"), ["sell037"] = ("JM00122", "JM00127"),
        ["sell038"] = ("JM00122", "JM00127"), ["sell039"] = ("JM00122", "JM00127"),
        ["sell052"] = ("JM00236", "JM00237"), ["sell053"] = ("JM00238", "JM00239"),
        ["sell054"] = ("JM00240", "JM00241"), ["sell055"] = ("JM00242", "JM00243"),
        ["sell056"] = ("JM00252", "JM00253"), ["sell057"] = ("JM00254", "JM00255"),
        ["sell058"] = ("JM00256", "JM00257"), ["sell059"] = ("JM00258", "JM00259"),
        ["sell060"] = ("JM00264", "JM00265"), ["sell061"] = ("JM00266", "JM00267"),
        ["sell062"] = ("JM00268", "JM00269"), ["sell063"] = ("JM00270", "JM00271"),
        ["sell064"] = ("JM00278", "JM00279"), ["sell065"] = ("JM00280", "JM00281"),
        ["sell066"] = ("JM00282", "JM00283"), ["sell067"] = ("JM00284", "JM00285"),
        ["sell068"] = ("JM00290", "JM00291"), ["sell069"] = ("JM00292", "JM00293"),
        ["sell070"] = ("JM00294", "JM00295"), ["sell071"] = ("JM00296", "JM00297"),
        ["sell072"] = ("JM00302", "JM00303"), ["sell073"] = ("JM00304", "JM00305"),
        ["sell074"] = ("JM00306", "JM00307"),
    };

    private static readonly Dictionary<string, MajSellMast> SellMasterDict =
        SellMasters.ToDictionary(m => m.SellCode);

    // 原典: static const MJITEMMAST item[] in HMajItem.cpp
    private static readonly Dictionary<string, int> ItemCategories = new(StringComparer.Ordinal)
    {
        ["item001"] = 1, // CAT_RICHI
        ["item002"] = 1,
        ["item004"] = 1,
        ["item003"] = 2, // CAT_NOTICE
        ["MJ20"] = 3,    // CAT_CHARGEFREE
        ["MJ21"] = 4,    // CAT_GEMDOUBLE
        ["MJ22"] = 5,    // CAT_GEMTRIPLE
        ["MJ23"] = 6,    // CAT_CHARGEFREEHIGH
    };

    public MajItemService(
        ItemRepository            itemRepo,
        PlayerRepository          playerRepo,
        HistoryRepository         histRepo)
    {
        _itemRepo     = itemRepo;
        _playerRepo   = playerRepo;
        _histRepo     = histRepo;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BuyMajItemAsync — 原典: ProcessCommand_BuyMajItem (mjkc20e)
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// アイテム購入処理。
    /// 返り値: (ok, newGamMoney, newGemCount, boughtItemCode, buyDt, endDt, qty)
    /// </summary>
    public async Task<BuyMajItemResult> BuyMajItemAsync(MajakPlayer player, string sellCode)
    {
        // 1. SELLCODE からマスターデータ取得
        if (!SellMasterDict.TryGetValue(sellCode, out var mast))
            return BuyMajItemResult.Fail("SELL_CODE_NOT_FOUND", EItemSellCode, "未登録のSELLCODEです");

        // 2. コスト確認
        if (mast.CostGem > 0 && player.GemCount < mast.CostGem)
            return BuyMajItemResult.Fail("GEM_NOT_ENOUGH", EItemGemShort, "龍珠が足りません");
        if (mast.CostMoney > 0 && player.GamMoney < mast.CostMoney)
            return BuyMajItemResult.Fail("MONEY_NOT_ENOUGH", EItemMoneyShort, "マネーが足りません");

        // 3. 必要称号チェック (前提条件)
        if (!string.IsNullOrEmpty(mast.RequiredTitle))
        {
            bool hasTitle;
            try
            {
                hasTitle = await _playerRepo.HasActiveTitleAsync(player.MemberNo, mast.RequiredTitle);
            }
            catch
            {
                return BuyMajItemResult.Fail("REQUIRED_TITLE_DB_ERROR", EItemDbError, "DBエラー");
            }

            if (!hasTitle)
                return BuyMajItemResult.Fail("REQUIRED_TITLE_NOT_MET", EItemMustTitle, "必要な称号を持っていません");
        }

        string? oldUseItemCode = null;
        bool hasMajItem = ItemCategories.TryGetValue(mast.ItemCode, out int itemCategory);
        if (hasMajItem)
        {
            if (player.MajItems.Count == 0)
                player.MajItems = await _itemRepo.GetAllItemsAsync(player.MemberNo) ?? [];

            oldUseItemCode = player.MajItems.FirstOrDefault(item => item.UseFlag
                && ItemCategories.TryGetValue(item.ItemCode, out int oldCategory)
                && oldCategory == itemCategory)?.ItemCode;
        }

        // 4. カテゴリ別処理 (ExchangeItem)
        BuyMajItemResult result = mast.Category switch
        {
            CatItem => await BuyItemCategoryAsync(player, mast, oldUseItemCode),
            CatTitle => await BuyTitleCategoryAsync(player, mast),
            CatAvatar => await BuyAvatarCategoryAsync(player, mast),
            CatBilling => await BuyBillingCategoryAsync(player, mast),
            _ => BuyMajItemResult.Fail("UNKNOWN_CATEGORY", EItemDbError, "DBエラー"),
        };

        if (result.Ok)
        {
            await WritePurchaseHistoryAsync(player, mast);

            player.GemCount -= mast.CostGem;
            player.GamMoney = Math.Max(0, player.GamMoney - mast.CostMoney);
            if (mast.Category == CatBilling)
                player.CashCount = await _itemRepo.GetCashCountAsync(player.MemberNo);

            result = result with { CashCount = player.CashCount };

            int conditionType = mast.Category == CatBilling
                ? MissionConditionBuyBillingItem
                : (mast.CostMoney > 0 || mast.CostGem > 0 ? MissionConditionExchangeGem : 0);
            if (conditionType != 0)
                await _playerRepo.SetDailyMissionDirectAsync(player.MemberNo, conditionType, 1);
        }

        return result.Ok && hasMajItem
            ? result with
            {
                OldUseItemCode = oldUseItemCode != result.ItemCode ? oldUseItemCode : null,
                NewUseItemCode = result.ItemCode,
            }
            : result;
    }

    // CAT_ITEM 処理 ─────────────────────────────────────────────────────────
    private async Task<BuyMajItemResult> BuyItemCategoryAsync(MajakPlayer player, MajSellMast mast, string? oldUseItemCode)
    {
        var item = await _itemRepo.ExchangeMajItemAsync(
            player.MemberNo, mast.ItemCode, mast.ValidDays, mast.Quantity, mast.CostMoney, mast.CostGem, oldUseItemCode);

        if (item == null)
            return BuyMajItemResult.Fail("ITEM_EXCHANGE_ERROR", EItemDbError, "DBエラー");

        UpdatePlayerMajItem(player, item, oldUseItemCode);

        return new BuyMajItemResult
        {
            Ok         = true,
            ItemCode   = item.ItemCode,
            BuyDt      = item.BuyDt,
            EndDt      = item.EndDt,
            Qty        = item.Qty,
            GamMoney   = Math.Max(0, player.GamMoney - mast.CostMoney),
            GemCount   = player.GemCount - mast.CostGem,
        };
    }

    // CAT_TITLE 処理 ────────────────────────────────────────────────────────
    private async Task<BuyMajItemResult> BuyTitleCategoryAsync(MajakPlayer player, MajSellMast mast)
    {
        bool ok = await _itemRepo.ExchangeTitleAsync(player.MemberNo, mast.ItemCode, mast.CostMoney, mast.CostGem, mast.SellCode == "sell024");
        if (!ok)
            return BuyMajItemResult.Fail("TITLE_ALREADY_OWN", mast.SellCode == "sell024" ? EItemDbError : EItemAlreadyOwn, "すでに持っています");

        // TitleClear フラグ更新
        if (mast.ItemCode.StartsWith("mjks") && int.TryParse(mast.ItemCode[4..], out int titleNum)
            && titleNum < player.TitleClear.Length)
            player.TitleClear[titleNum] = 1;

        return new BuyMajItemResult
        {
            Ok       = true,
            ItemCode = mast.ItemCode,
            GamMoney = Math.Max(0, player.GamMoney - mast.CostMoney),
            GemCount = player.GemCount - mast.CostGem,
        };
    }

    // CAT_AVATAR 処理 ───────────────────────────────────────────────────────
    /// <summary>
    /// アバターアイテム購入 — AVATAR_DISTRIBUTE_ONLY_PC 呼び出し
    /// </summary>
    private async Task<BuyMajItemResult> BuyAvatarCategoryAsync(MajakPlayer player, MajSellMast mast)
    {
        string avatarCode = player.Sex.StartsWith("M", StringComparison.OrdinalIgnoreCase)
            ? mast.ItemCode
            : (mast.SubCode ?? mast.ItemCode);
        bool ok = await _itemRepo.ExchangeAvatarAsync(player.MemberNo, avatarCode, mast.CostMoney, mast.CostGem);
        if (!ok)
            return BuyMajItemResult.Fail("AVATAR_BUY_ERROR", EItemDbError, "DBエラー");

        return new BuyMajItemResult
        {
            Ok       = true,
            ItemCode = avatarCode,
            GamMoney = Math.Max(0, player.GamMoney - mast.CostMoney),
            GemCount = player.GemCount - mast.CostGem,
        };
    }

    // CAT_BILLING 処理 ──────────────────────────────────────────────────────
    /// <summary>
    /// 課金アイテム購入 — PC_GMBSYS_MAJAKBILLINGBUY 呼び出し
    /// 原典: HMajItem.cpp ProcessCommand_BuyMajItem → PC_GMBSYS_MAJAKBILLINGBUY
    ///   IN_SUBCODE  = mast.SubCode  (ITEMMAST.SUBCODE)
    ///   IN_BUYCATE  = 2 (CLIENT)
    /// </summary>
    private async Task<BuyMajItemResult> BuyBillingCategoryAsync(MajakPlayer player, MajSellMast mast)
    {
        var (rtnVal, rtnMsg) = await _itemRepo.BuyBillingItemAsync(
            memberNo: player.MemberNo,
            password: player.Password,
            itemCode: mast.ItemCode,
            subCode:  mast.SubCode ?? mast.ItemCode,
            validDays: mast.ValidDays,
            userIp:   player.IpAddress,
            buyCate:  2,
            quantity: mast.Quantity);

        if (rtnVal != 1)
            return BuyMajItemResult.Fail($"BILLING_BUY_ERROR:{rtnVal}:{rtnMsg}", EItemDbError, string.IsNullOrEmpty(rtnMsg) ? "DBエラー" : rtnMsg);

        var item = (await _itemRepo.GetAllItemsAsync(player.MemberNo))
            .FirstOrDefault(x => x.ItemCode == mast.ItemCode);
        if (item == null)
            return BuyMajItemResult.Fail("BILLING_ITEM_NOT_FOUND", EItemDbError, "DBエラー");

        UpdatePlayerMajItem(player, item, null);

        return new BuyMajItemResult
        {
            Ok       = true,
            ItemCode = mast.ItemCode,
            BuyDt    = item.BuyDt,
            EndDt    = item.EndDt,
            Qty      = item.Qty,
            GamMoney = Math.Max(0, player.GamMoney - mast.CostMoney),
            GemCount = player.GemCount - mast.CostGem,
        };
    }

    private async Task WritePurchaseHistoryAsync(MajakPlayer player, MajSellMast mast)
    {
        if (!ProCodes.TryGetValue(mast.SellCode, out var proCodes))
            return;

        long afterMoney = Math.Max(0, player.GamMoney - mast.CostMoney);
        int afterGem = player.GemCount - mast.CostGem;

        if (!string.IsNullOrEmpty(proCodes.Money))
            await _histRepo.InsertGameMoneyHistAsync(player.MemberNo, proCodes.Money, -mast.CostMoney, player.GamMoney, afterMoney, player.IpAddress);
        if (!string.IsNullOrEmpty(proCodes.Gem))
            await _histRepo.InsertGameMoneyHistAsync(player.MemberNo, proCodes.Gem, -mast.CostGem, player.GemCount, afterGem, player.IpAddress);
    }

    private static void UpdatePlayerMajItem(MajakPlayer player, MajItemInfo item, string? oldUseItemCode)
    {
        if (!string.IsNullOrEmpty(oldUseItemCode) && oldUseItemCode != item.ItemCode)
        {
            var oldItem = player.MajItems.FirstOrDefault(x => x.ItemCode == oldUseItemCode);
            if (oldItem != null)
                ReplacePlayerMajItem(player, oldItem with { UseFlag = false });
        }

        var current = player.MajItems.FirstOrDefault(x => x.ItemCode == item.ItemCode);
        if (current == null)
        {
            player.MajItems.Add(item);
            return;
        }

        ReplacePlayerMajItem(player, current with
        {
            BuyDt = item.BuyDt,
            EndDt = item.EndDt,
            Qty = item.Qty,
            UseFlag = true,
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SelectMajItemAsync — 原典: ProcessCommand_SelectMajItem (mjkc21e)
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// アイテム選択 (USE フラグ切り替え)。
    /// 返り値: (ok, oldItemCode, newItemCode, itemCount)
    /// </summary>
    public async Task<SelectMajItemResult> SelectMajItemAsync(MajakPlayer player, string itemCode)
    {
        if (!ItemCategories.TryGetValue(itemCode, out int category))
            return SelectMajItemResult.Fail("ITEM_CODE_NOT_FOUND", EItemItemCode, "未登録のITEMCODEです");

        var newItem = player.MajItems.FirstOrDefault(i => i.ItemCode == itemCode);
        if (newItem == null)
            return SelectMajItemResult.Fail("ITEM_NOT_FOUND", EItemNotOwn, "持っていないアイテムです");

        if (newItem.UseFlag)
            return SelectMajItemResult.Fail("ITEM_ALREADY_IN_USE", EItemAlreadyUse, "すでに使用中です");

        bool isExpired   = newItem.EndDt != default && newItem.EndDt <= DateTime.Now;
        bool isExhausted = newItem.Qty <= 0;
        if (isExpired)
            return SelectMajItemResult.Fail("ITEM_EXPIRED", EItemExpired, "有効期限切れです");
        if (isExhausted)
            return SelectMajItemResult.Fail("ITEM_EXHAUSTED", EItemExhausted, "残り数量がありません");

        var oldItem = player.MajItems.FirstOrDefault(i => i.UseFlag
                                               && ItemCategories.TryGetValue(i.ItemCode, out int oldCategory)
                                               && oldCategory == category);

        if (!await _itemRepo.UpdateMajItemInUseAsync(player.MemberNo, oldItem?.ItemCode, itemCode))
            return SelectMajItemResult.Fail("DB_ERROR", EItemDbError, "DBエラー");

        if (oldItem != null)
            ReplacePlayerMajItem(player, oldItem with { UseFlag = false });
        ReplacePlayerMajItem(player, newItem with { UseFlag = true });

        return new SelectMajItemResult
        {
            Ok          = true,
            OldItemCode = oldItem?.ItemCode,
            NewItemCode = itemCode,
            ItemCount   = oldItem == null ? 1 : 2,
        };
    }

    private static void ReplacePlayerMajItem(MajakPlayer player, MajItemInfo item)
    {
        int index = player.MajItems.FindIndex(x => x.ItemCode == item.ItemCode);
        if (index >= 0)
            player.MajItems[index] = item;
    }
}

// ─── 戻り値型 ─────────────────────────────────────────────────────────────────

public record BuyMajItemResult
{
    public bool     Ok       { get; init; }
    public string   Error    { get; init; } = "";
    public int      ErrorCode { get; init; } = -1;
    public string   ErrorMessage { get; init; } = "";
    public string   ItemCode { get; init; } = "";
    public string?  OldUseItemCode { get; init; }
    public string?  NewUseItemCode { get; init; }
    public DateTime BuyDt    { get; init; }
    public DateTime EndDt    { get; init; }
    public int      Qty      { get; init; }
    public long     GamMoney { get; init; }
    public int      GemCount { get; init; }
    public int      CashCount { get; init; }

    public static BuyMajItemResult Fail(string err, int errorCode = -1, string errorMessage = "") => new()
    {
        Ok = false,
        Error = err,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

public record SelectMajItemResult
{
    public bool    Ok          { get; init; }
    public string  Error       { get; init; } = "";
    public int     ErrorCode   { get; init; } = -1;
    public string  ErrorMessage { get; init; } = "";
    public string? OldItemCode { get; init; }
    public string  NewItemCode { get; init; } = "";
    public int     ItemCount   { get; init; }

    public static SelectMajItemResult Fail(string err, int errorCode = -1, string errorMessage = "") => new()
    {
        Ok = false,
        Error = err,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
    };
}

// ─── MJSELLMAST マスターデータ型 ─────────────────────────────────────────────
public record MajSellMast(
    string  SellCode,
    int     Category,
    string  ItemCode,
    string? SubCode,      // ITEMMAST.SUBCODE (BillingBuy 用) / アバター女性コード
    int     ValidDays,
    int     CostGem,
    int     Quantity,
    long    CostMoney,
    string? RequiredTitle);
