/**
 * 麻雀ショップ 静的アイテムデータ
 * 原典: legacy/client/HgMajak2/MJItemManager.cpp
 *
 * ── BUY_ITEM タブ (CMJItemManager::m_ShopItemData2[]) — 11 件 ───────────────
 *   原典: MJItemManager.cpp:132-157
 *   CMajakShopItemData(itemIndex, AvCodeM, AvCodeF, ProcID, NameM, NameF,
 *                      NameSub, GemPrice, ImgM, ImgF,
 *                      LotteryCount, GameMoney, NameSub2, SellCode)
 *
 * ── EXC_ITEM タブ (CMJItemManager::m_ShopItemData3[]) ────────────────────────
 *   原典: MJItemManager.cpp:171-237
 *   CMajakShopItemData2(Category, ItemKind, NameM, NameF, ItemCode, SellCode,
 *                       GameMoney, CostGem, LimitDays, Quantity,
 *                       Guid1, Guid2, ImgM, ImgF)
 *
 * 画像パス: legacy では .him 形式, 新規では .png に置換 (AP-06)
 *   legacy/items/mj_ryu_item_01.him → /assets/images/game/items/mj_ryu_item_01.png
 */

const IMG = '/assets/images/game/items'

/** ============================================================
 * BUY_ITEM タブ用 (CMajakShopItemData)
 * MP購入アイテム (11件)
 * ============================================================ */
export interface BuyItemData {
  itemIndex:    number
  avCode:       string         // m_strAvCode[0] (性別共通 — レガシーは M/F 別だが現状同一)
  procId:       string         // gmbsysmajakgma
  name:         string         // m_strItemName[0]
  nameSub:      string
  hancoinPrice: number          // 旧互換名: ハンコイン価格。現行表示はMP
  gameMoney:    number
  nameSub2:     string         // 表示用 (例: "2倍", "3倍")
  sellCode:     string
  imagePath:    string         // PNG 相対パス
  priceLabel:   string         // 表示用文字列 ("3000円" など, レガシー名称)
}

/** 原典: MJItemManager.cpp m_ShopItemData2[] (11件) */
export const SHOP_ITEM_DATA_BUY: BuyItemData[] = [
  // 1ページ目
  { itemIndex: 0, avCode: 'MJ2002', procId: 'gmbsysmajakgma', name: '場代無料(6回)',   nameSub: '3000円',   hancoinPrice: 300,  gameMoney: 3000,  nameSub2: '',    sellCode: 'sell041', imagePath: `${IMG}/mj_shop_item_sell_coin_01.png`, priceLabel: '3000円' },
  { itemIndex: 1, avCode: 'MJ2003', procId: 'gmbsysmajakgma', name: '場代無料(12回)',  nameSub: '5000円',   hancoinPrice: 500,  gameMoney: 5000,  nameSub2: '',    sellCode: 'sell042', imagePath: `${IMG}/mj_shop_item_sell_coin_02.png`, priceLabel: '5000円' },
  { itemIndex: 2, avCode: 'MJ2004', procId: 'gmbsysmajakgma', name: '場代無料(40回)',  nameSub: '15000円',  hancoinPrice: 1500, gameMoney: 15000, nameSub2: '',    sellCode: 'sell043', imagePath: `${IMG}/mj_shop_item_sell_coin_03.png`, priceLabel: '15000円' },
  { itemIndex: 3, avCode: 'MJ2101', procId: 'gmbsysmajakgma', name: '龍珠2倍(1日)',   nameSub: '2000円',   hancoinPrice: 200,  gameMoney: 2000,  nameSub2: '2倍', sellCode: 'sell044', imagePath: `${IMG}/mj_shop_item_sell_ryu_01.png`,  priceLabel: '2000円' },
  { itemIndex: 4, avCode: 'MJ2102', procId: 'gmbsysmajakgma', name: '龍珠2倍(3日)',   nameSub: '3000円',   hancoinPrice: 300,  gameMoney: 3000,  nameSub2: '2倍', sellCode: 'sell045', imagePath: `${IMG}/mj_shop_item_sell_ryu_03.png`,  priceLabel: '3000円' },
  { itemIndex: 5, avCode: 'MJ2103', procId: 'gmbsysmajakgma', name: '龍珠2倍(7日)',   nameSub: '5000円',   hancoinPrice: 500,  gameMoney: 5000,  nameSub2: '2倍', sellCode: 'sell046', imagePath: `${IMG}/mj_shop_item_sell_ryu_05.png`,  priceLabel: '5000円' },
  { itemIndex: 6, avCode: 'MJ2104', procId: 'gmbsysmajakgma', name: '龍珠2倍(30日)',  nameSub: '15000円',  hancoinPrice: 1500, gameMoney: 15000, nameSub2: '2倍', sellCode: 'sell047', imagePath: `${IMG}/mj_shop_item_sell_ryu_07.png`,  priceLabel: '15000円' },
  // 2ページ目
  { itemIndex: 7,  avCode: 'MJ2201', procId: 'gmbsysmajakgma', name: '龍珠3倍(1日)',  nameSub: '4000円',   hancoinPrice: 400,  gameMoney: 4000,  nameSub2: '3倍', sellCode: 'sell048', imagePath: `${IMG}/mj_shop_item_sell_ryu_02.png`, priceLabel: '4000円' },
  { itemIndex: 8,  avCode: 'MJ2202', procId: 'gmbsysmajakgma', name: '龍珠3倍(3日)',  nameSub: '6000円',   hancoinPrice: 600,  gameMoney: 6000,  nameSub2: '3倍', sellCode: 'sell049', imagePath: `${IMG}/mj_shop_item_sell_ryu_04.png`, priceLabel: '6000円' },
  { itemIndex: 9,  avCode: 'MJ2203', procId: 'gmbsysmajakgma', name: '龍珠3倍(7日)',  nameSub: '10000円',  hancoinPrice: 1000, gameMoney: 10000, nameSub2: '3倍', sellCode: 'sell050', imagePath: `${IMG}/mj_shop_item_sell_ryu_06.png`, priceLabel: '10000円' },
  { itemIndex: 10, avCode: 'MJ2204', procId: 'gmbsysmajakgma', name: '龍珠3倍(30日)', nameSub: '30000円',  hancoinPrice: 3000, gameMoney: 30000, nameSub2: '3倍', sellCode: 'sell051', imagePath: `${IMG}/mj_shop_item_sell_ryu_08.png`, priceLabel: '30000円' },
]

/** ============================================================
 * EXC_ITEM タブ用 (CMajakShopItemData2)
 * 龍珠交換アイテム
 * ============================================================ */
export interface ExcItemData {
  category:   number    // 0=アバター, 1=麻雀称号, 2=リーチ棒
  itemKind:   string    // "麻雀称号", "リーチ棒", "アバター" 等
  name:       string
  itemCode:   string
  sellCode:   string
  gameMoney:  number     // m_llGameMoney (ゲームマネー消費)
  costGem:    number     // m_nCostGem (龍珠消費)
  limitDays:  number    // -1=無期限
  quantity:   number    // -1=無制限
  guid1:      string
  guid2:      string
  imagePath:  string
  imagePathFemale?: string
}

/** 原典: MJItemManager.cpp m_ShopItemData3[] */
export const SHOP_ITEM_DATA_EXC: ExcItemData[] = [
  // 1ページ目 — 麻雀称号 (mjkt103〜mjkt110)
  { category: 1, itemKind: '麻雀称号', name: '歩き出した者',     itemCode: 'mjkt103', sellCode: 'sell024', gameMoney:    100, costGem:    1, limitDays: -1, quantity: -1, guid1: '獲得条件：なし',                            guid2: '', imagePath: `${IMG}/mj_shop_item_change_shougou_01.png` },
  { category: 1, itemKind: '麻雀称号', name: '路傍の石',         itemCode: 'mjkt104', sellCode: 'sell025', gameMoney:   2000, costGem:   20, limitDays: -1, quantity: -1, guid1: '獲得条件：称号「歩き出した者」を所持',      guid2: '', imagePath: `${IMG}/mj_shop_item_change_shougou_02.png` },
  { category: 1, itemKind: '麻雀称号', name: '野に咲く花',       itemCode: 'mjkt105', sellCode: 'sell026', gameMoney:   5000, costGem:   50, limitDays: -1, quantity: -1, guid1: '獲得条件：称号「路傍の石」を所持',          guid2: '', imagePath: `${IMG}/mj_shop_item_change_shougou_03.png` },
  { category: 1, itemKind: '麻雀称号', name: '嚢中の錐',         itemCode: 'mjkt106', sellCode: 'sell027', gameMoney:  10000, costGem:  100, limitDays: -1, quantity: -1, guid1: '獲得条件：称号「野に咲く花」を所持',        guid2: '', imagePath: `${IMG}/mj_shop_item_change_shougou_04.png` },
  { category: 1, itemKind: '麻雀称号', name: '輝く星',           itemCode: 'mjkt107', sellCode: 'sell028', gameMoney:  20000, costGem:  200, limitDays: -1, quantity: -1, guid1: '獲得条件：称号「嚢中の錐」を所持',          guid2: '', imagePath: `${IMG}/mj_shop_item_change_shougou_05.png` },
  { category: 1, itemKind: '麻雀称号', name: '月に叢雲、花に風', itemCode: 'mjkt108', sellCode: 'sell029', gameMoney:  50000, costGem:  500, limitDays: -1, quantity: -1, guid1: '獲得条件：称号「輝く星」を所持',            guid2: '', imagePath: `${IMG}/mj_shop_item_change_shougou_06.png` },
  { category: 1, itemKind: '麻雀称号', name: '武陵桃源',         itemCode: 'mjkt109', sellCode: 'sell030', gameMoney: 100000, costGem: 1000, limitDays: -1, quantity: -1, guid1: '獲得条件：称号「月に叢雲、花に風」を所持',  guid2: '', imagePath: `${IMG}/mj_shop_item_change_shougou_07.png` },
  { category: 1, itemKind: '麻雀称号', name: '天衣無縫',         itemCode: 'mjkt110', sellCode: 'sell031', gameMoney: 200000, costGem: 2000, limitDays: -1, quantity: -1, guid1: '獲得条件：称号「武陵桃源」を所持',          guid2: '', imagePath: `${IMG}/mj_shop_item_change_shougou_08.png` },

  // 2ページ目 — 2015/11/06 新アバター追加対応 (MAJAK3_LIMITED_AVATER_20151118_12xx + COCONE)
  { category: 0, itemKind: 'アバター', name: '[龍珠]頭にキツネ', itemCode: 'DUMMY', sellCode: 'sell072', gameMoney:  2000, costGem:  20, limitDays: -1, quantity: -1, guid1: 'ハンゲームのアバターになります。', guid2: '', imagePath: `${IMG}/e3lin02.png` },
  { category: 0, itemKind: 'アバター', name: '[龍珠]狐耳',       itemCode: 'DUMMY', sellCode: 'sell073', gameMoney: 10000, costGem: 100, limitDays: -1, quantity: -1, guid1: 'ハンゲームのアバターになります。', guid2: '', imagePath: `${IMG}/aesun01.png` },
  { category: 0, itemKind: 'アバター', name: '[龍珠]九尾衣装',   itemCode: 'DUMMY', sellCode: 'sell074', gameMoney: 20000, costGem: 200, limitDays: -1, quantity: -1, guid1: 'ハンゲームのアバターになります。', guid2: '', imagePath: `${IMG}/lc1zun03.png`, imagePathFemale: `${IMG}/lc2bly04.png` },
  { category: 0, itemKind: 'アバター', name: '', itemCode: 'DUMMY', sellCode: '', gameMoney: 0, costGem: 0, limitDays: -1, quantity: -1, guid1: '', guid2: '', imagePath: '' },
  { category: 0, itemKind: 'アバター', name: '', itemCode: 'DUMMY', sellCode: '', gameMoney: 0, costGem: 0, limitDays: -1, quantity: -1, guid1: '', guid2: '', imagePath: '' },
  { category: 0, itemKind: 'アバター', name: '', itemCode: 'DUMMY', sellCode: '', gameMoney: 0, costGem: 0, limitDays: -1, quantity: -1, guid1: '', guid2: '', imagePath: '' },
  { category: 0, itemKind: 'アバター', name: '', itemCode: 'DUMMY', sellCode: '', gameMoney: 0, costGem: 0, limitDays: -1, quantity: -1, guid1: '', guid2: '', imagePath: '' },
  { category: 0, itemKind: 'アバター', name: '', itemCode: 'DUMMY', sellCode: '', gameMoney: 0, costGem: 0, limitDays: -1, quantity: -1, guid1: '', guid2: '', imagePath: '' },

  // 4ページ目 — アバター
  { category: 0, itemKind: 'アバター', name: '[龍珠]炎のリーチ',         itemCode: 'DUMMY', sellCode: 'sell011', gameMoney:  500, costGem: 10, limitDays: -1, quantity: -1, guid1: 'ハンゲームのアバターになります。', guid2: '', imagePath: `${IMG}/mj_ryu_item_02.png` },
  { category: 0, itemKind: 'アバター', name: '[龍珠]昇り龍',             itemCode: 'DUMMY', sellCode: 'sell012', gameMoney:  750, costGem: 15, limitDays: -1, quantity: -1, guid1: 'ハンゲームのアバターになります。', guid2: '', imagePath: `${IMG}/mj_ryu_item_03.png` },
  { category: 0, itemKind: 'アバター', name: '[龍珠]黒トントン胴',       itemCode: 'DUMMY', sellCode: 'sell014', gameMoney:  750, costGem: 15, limitDays: -1, quantity: -1, guid1: 'ハンゲームのアバターになります。', guid2: '', imagePath: `${IMG}/mj_ryu_item_05.png` },
  { category: 0, itemKind: 'アバター', name: '[龍珠]黒トントン頭',       itemCode: 'DUMMY', sellCode: 'sell016', gameMoney:  750, costGem: 15, limitDays: -1, quantity: -1, guid1: 'ハンゲームのアバターになります。', guid2: '', imagePath: `${IMG}/mj_ryu_item_07.png` },
  { category: 0, itemKind: 'アバター', name: '[龍珠]ガラスのイーピン',   itemCode: 'DUMMY', sellCode: 'sell017', gameMoney:  750, costGem: 15, limitDays: -1, quantity: -1, guid1: 'ハンゲームのアバターになります。', guid2: '', imagePath: `${IMG}/mj_ryu_item_08.png` },
  { category: 0, itemKind: 'アバター', name: '[龍珠]火の点棒試し',       itemCode: 'DUMMY', sellCode: 'sell020', gameMoney:   10, costGem:  2, limitDays: 15, quantity: -1, guid1: 'ハンゲームのアバターになります。', guid2: '', imagePath: `${IMG}/mj_ryu_item_15.png` },
  { category: 0, itemKind: 'アバター', name: '[龍珠]ガラスのイーソー',   itemCode: 'DUMMY', sellCode: 'sell021', gameMoney:  500, costGem: 10, limitDays: -1, quantity: -1, guid1: 'ハンゲームのアバターになります。', guid2: '', imagePath: `${IMG}/mj_ryu_item_20.png` },
  { category: 0, itemKind: 'アバター', name: '[龍珠]びりびりりーち',     itemCode: 'DUMMY', sellCode: 'sell022', gameMoney:  750, costGem: 15, limitDays: -1, quantity: -1, guid1: 'ハンゲームのアバターになります。', guid2: '', imagePath: `${IMG}/mj_ryu_item_19.png` },

  // 5ページ目 — 麻雀称号 (mjkt100〜102 龍関連)
  { category: 1, itemKind: '麻雀称号', name: '龍のかけら',       itemCode: 'mjkt100', sellCode: 'sell007', gameMoney:    150, costGem:    5, limitDays: -1, quantity: -1, guid1: '龍玉で交換して称号として使えます。',       guid2: '', imagePath: `${IMG}/mj_ryu_item_09.png` },
  { category: 1, itemKind: '麻雀称号', name: '龍の証',           itemCode: 'mjkt101', sellCode: 'sell008', gameMoney:   1500, costGem:   50, limitDays: -1, quantity: -1, guid1: '龍玉で交換して称号として使えます。',       guid2: '', imagePath: `${IMG}/mj_ryu_item_10.png` },
  { category: 1, itemKind: '麻雀称号', name: '暗黒の龍',         itemCode: 'mjkt102', sellCode: 'sell009', gameMoney:   7500, costGem:  100, limitDays: -1, quantity: -1, guid1: '龍玉で交換して称号として使えます。',       guid2: '', imagePath: `${IMG}/mj_ryu_item_11.png` },

  // リーチ棒
  { category: 2, itemKind: 'リーチ棒', name: '隼リーチ',         itemCode: 'item001', sellCode: 'sell001', gameMoney:     10, costGem:    2, limitDays:  3, quantity: -1, guid1: 'リーチの時に特別な演出が出ます。',         guid2: 'ゲーム展開には関係ありません。', imagePath: `${IMG}/mj_ryu_item_12.png` },
  { category: 2, itemKind: 'リーチ棒', name: '電撃リーチ',       itemCode: 'item002', sellCode: 'sell003', gameMoney:    500, costGem:   10, limitDays:  3, quantity: -1, guid1: 'リーチの時に特別な演出が出ます。',         guid2: 'ゲーム展開には関係ありません。', imagePath: `${IMG}/mj_ryu_item_13.png` },
  { category: 2, itemKind: 'リーチ棒', name: '黄金リーチ',       itemCode: 'item004', sellCode: 'sell019', gameMoney:    250, costGem:    5, limitDays:  3, quantity: -1, guid1: 'リーチの時に特別な演出が出ます。',         guid2: 'ゲーム展開には関係ありません。', imagePath: `${IMG}/mj_ryu_item_16.png` },

  // ハイ卓場代無料
  { category: 1, itemKind: 'ハイ卓場代無料', name: 'ハイ卓場代無料(1回)', itemCode: 'MJ23', sellCode: 'sell075', gameMoney: 0, costGem: 20, limitDays: -1, quantity: 1, guid1: 'ハイ卓の場代が無料になります。', guid2: '※対局終了時に効果が有効である必要があります。', imagePath: `${IMG}/mj_shop_item_sell_coin_04.png` },
]
