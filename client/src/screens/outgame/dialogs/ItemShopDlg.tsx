/**
 * CItemShopDlg 相当 — 麻雀ショップ (AP-09 §3-2-1)
 * 原典: legacy/client/HgMajak2/ItemShopDlg.h/cpp
 *
 * ── レガシー座標・寸法 (ItemShopDlg.cpp 行番号は参考) ──────────────────────
 *  ウィンドウ:   MoveWindow(0,0,662,514)                                L255
 *  タイトル:     "麻雀ショップ" CRect(218,7,445,22) DT_CENTER 15px Bold 白  L407
 *  タブ (145×33×4f, COwnerCheckBox):                                    L221-230
 *    Tab1 BUY_CUSTOM mj_shp_tab_05 (14, 36)
 *    Tab3 BUY_ITEM   mj_shp_tab_04 (159, 36)
 *    Tab4 EXC_ITEM   mj_shp_tab_02 (159+145=304, 36)
 *  プレート 148×163 1f:
 *    上段 (24+155*i, 91), 下段 (24+155*i, 266)                          L281-285
 *    BUY_CUSTOM=plate01, BUY_ITEM=plate03, EXC_ITEM=plate04             L70-95
 *  アイテム画像:
 *    BUY_CUSTOM (33+ox, 138+oy)                                         L481
 *    BUY_ITEM/EXC (66+ox, 142+oy)                                        L495,510
 *  名前:        CRect(33+ox, 103+oy, 33+130+ox, 103+11+oy) DT_CENTER 12px Bold 黒
 *  サブ説明:    CRect(33+ox, 115+oy, ...) RGB(40,160,100)
 *  価格表示:
 *    BUY_*: CRect(39+ox, 228+oy, 39+62+ox, 228+11+oy) DT_RIGHT 12px Bold 黒
 *    EXC:   Gem  CRect(66+ox, 220+oy, ...)
 *           Money CRect(66+ox, 234+oy, ...)
 *  Buy ボタン (mj_shp_btn_buy 51×28×4f):    (112+ox, 219+oy)            L242
 *  Exc ボタン (mj_shp_btn_exchange 34×28×4f):(132+ox, 219+oy)            L247
 *  ページ番号 (mj_shp_num_01 22×28×10f):    (298, 457)cur / (344, 457)max L391-392
 *  ◀ pagedown (26×42×4f): (257, 451)                                   L259
 *  ▶ pageup   (26×42×4f): (382, 451)                                   L265
 *  limit ボタン (88×32×4f): (467, 469)                                  L251
 *  閉じる      (88×32×4f): (560, 469)                                  L215
 *  EXC タブのみ mj_shp_money_01 (228×58, 1f) at (15, 443)               L367
 *  残高テキスト:
 *    text1: CRect(150,453,235,464) DT_RIGHT
 *    text2: CRect(150,480,235,491) DT_RIGHT
 *    BUY:  text1=GEM残, text2=ゲーム内コイン残
 *    EXC:  text1=龍珠個数,    text2=ゲームマネー
 *  OnNcHitTest pt.y<31 → HTCAPTION (ドラッグ可能)                       L561
 *
 * ── タブ別データソース ─────────────────────────────────────────────────────
 *  BUY_CUSTOM (Tab0): サーバ mjkc36e (MJK_CUSTOMSHOPMAST 販売中商品)
 *                     → サーバ送信: shopNo/customId/customType/customPrice/Name/description/gameMoney/Purchased
 *  BUY_ITEM   (Tab1): クライアント静的配列 SHOP_ITEM_DATA_BUY (11件)
 *  EXC_ITEM   (Tab2): クライアント静的配列 SHOP_ITEM_DATA_EXC
 */
import { useRef, useEffect, useState } from 'react'
import * as SignalR from '../../../api/signalr'
import { useAuthStore } from '../../../store/authStore'
import { SHOP_ITEM_DATA_BUY, SHOP_ITEM_DATA_EXC } from './shopItemData'
import type { BuyItemData, ExcItemData } from './shopItemData'
import BuyCustomItemDlg from './BuyCustomItemDlg'
import BuyHanCoinItemDlg from './BuyHanCoinItemDlg'
import BuyExchangeItemDlg from './BuyExchangeItemDlg'

const IMG = '/assets/images/game'
const IMG_CUSTOM_ITEM = `${IMG}/items/custom`  // mj_custom_{customId}.png
const SHOP_W = 662
const SHOP_H = 514

// レガシー: CreateFont(..., "ＭＳ Ｐゴシック")
const FONT = "'MS PGothic', 'MS UI Gothic', 'Meiryo', 'Noto Sans JP', sans-serif"
const ITEM_TEXT_FONT_SIZE = 11

// レガシータブ定数
const BUY_CUSTOM = 0
const BUY_ITEM   = 1
const EXC_ITEM   = 2

/** ============================================================
 * カスタムショップアイテム (サーバから受信)
 * 原典: HMajChnlServer.cpp ProcessCommand_GetShopItem 7615-7622
 * ============================================================ */
interface CustomShopItem {
  shopNo:      number
  customId:    number
  kind:        number
  price:       number
  name:        string
  description: string
  gameMoney:   number
  purchased:   number
}

interface Props {
  onClose:        () => void
  initialTab?:    number
  /** OnBtnItem[n]BuyClicked 相当 (BUY_CUSTOM → CMJBuyCustomItemDlg, BUY_ITEM → CMJBuyItemDlg) */
  onBuyCustom?:   (item: CustomShopItem) => void
  onBuyItem?:     (item: BuyItemData) => void
  /** OnBtnItem[n]ExcClicked 相当 (EXC_ITEM → BuyExchangeItemDlg) */
  onExcItem?:     (item: ExcItemData) => void
  /** OnBtnConfirmItem 相当 → CMJConfirmItemDlg */
  onConfirmItem?: () => void
  /** 交換購入後の所持龍珠/麻雀コイン更新 */
  onBalanceUpdate?: (balance: { gemCount: number; gamMoney: number }) => void
  /** プレイヤー所持データ */
  hanCoin?:       number    // 旧互換: GEM残
  hanCoinCoupon?: number    // 旧互換: 未使用
  gemCount?:      number    // 龍珠個数
  gamMoney?:      number    // ゲームマネー
}

/** ============================================================
 * CMJBmpButton 相当 — 4フレームスプライト
 * normal=0, disabled=1, hover=2, pressed=3
 * ============================================================ */
function SpriteButton({
  src, frameW, frameH, x, y, onClick, disabled = false, title,
}: {
  src: string; frameW: number; frameH: number
  x: number; y: number; onClick: () => void
  disabled?: boolean; title?: string
}) {
  const [fi, setFi] = useState(disabled ? 1 : 0)
  useEffect(() => { setFi(disabled ? 1 : 0) }, [disabled])
  return (
    <button
      title={title}
      disabled={disabled}
      onClick={disabled ? undefined : onClick}
      onMouseEnter={() => !disabled && setFi(2)}
      onMouseLeave={() => !disabled && setFi(disabled ? 1 : 0)}
      onMouseDown={() => !disabled && setFi(3)}
      onMouseUp={() => !disabled && setFi(2)}
      style={{
        position: 'absolute', left: x, top: y,
        width: frameW, height: frameH,
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-fi * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        border: 'none', padding: 0,
        cursor: disabled ? 'not-allowed' : 'pointer',
        outline: 'none', imageRendering: 'pixelated',
      }}
    />
  )
}

/** ============================================================
 * COwnerCheckBox 相当 — タブ
 * SetCheck(true) + EnableWindow(false) → frame3 表示
 * ============================================================ */
function TabButton({
  src, frameW, frameH, x, y, active, onClick,
}: {
  src: string; frameW: number; frameH: number
  x: number; y: number; active: boolean; onClick: () => void
}) {
  const [hover, setHover] = useState(false)
  const fi = active ? 3 : hover ? 2 : 0
  return (
    <button
      aria-disabled={active}
      onClick={active ? undefined : onClick}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        position: 'absolute', left: x, top: y,
        width: frameW, height: frameH,
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-fi * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        border: 'none', padding: 0,
        cursor: active ? 'default' : 'pointer',
        outline: 'none', imageRendering: 'pixelated',
      }}
    />
  )
}

/** ============================================================
 * mj_shp_num_01.png (220×28, 10フレーム 22×28) — 数字表示
 * frame index = 数字 (0-9)
 * ============================================================ */
function NumImg({ x, y, num }: { x: number; y: number; num: number }) {
  // 1〜10ページ想定 → 1桁のみ表示 (レガシー DrawTransparent 1コール)
  const n = Math.max(0, Math.min(9, num))
  return (
    <div style={{
      position: 'absolute', left: x, top: y,
      width: 22, height: 28,
      backgroundImage: `url(${IMG}/mj_shp_num_01.png)`,
      backgroundPosition: `${-n * 22}px 0`,
      backgroundRepeat: 'no-repeat',
      imageRendering: 'pixelated',
      pointerEvents: 'none',
    }} />
  )
}

/** ============================================================
 * makeMoneyString 相当 — 4桁単位 + "円"
 * ============================================================ */
function moneyStr(v: number): string {
  if (v === 0) return '0円'

  const sign = v < 0 ? '-' : ''
  let rest = Math.abs(Math.trunc(v))
  const units = ['', '万', '億', '兆', '京']
  const parts: string[] = []

  for (let unitIndex = 0; rest > 0 && unitIndex < units.length; unitIndex++) {
    const chunk = rest % 10000
    if (chunk > 0) parts.unshift(`${chunk}${units[unitIndex]}`)
    rest = Math.floor(rest / 10000)
  }

  return `${sign}${parts.join('')}円`
}

/** ====================================================================
 * CItemShopDlg 本体
 * ==================================================================== */
export default function ItemShopDlg({
  onClose, initialTab = BUY_CUSTOM,
  onBuyCustom, onBuyItem, onExcItem, onConfirmItem, onBalanceUpdate,
  gemCount: gemCountProp = 0, gamMoney: gamMoneyProp = 0,
}: Props) {
  const player = useAuthStore(state => state.player)
  const [tabNo, setTabNo]      = useState(initialTab)
  const [pageNo, setPageNo]    = useState(1)             // レガシーは 1-based
  const [customItems, setCustomItems] = useState<CustomShopItem[]>([])
  const [buyCustomTarget, setBuyCustomTarget] = useState<CustomShopItem | null>(null)
  const [buyItemTarget, setBuyItemTarget] = useState<BuyItemData | null>(null)
  const [excItemTarget, setExcItemTarget] = useState<ExcItemData | null>(null)
  const [gemCountState, setGemCountState] = useState(gemCountProp)
  const [gamMoneyState, setGamMoneyState] = useState(gamMoneyProp)
  const [dialogScale, setDialogScale] = useState(1)
  useEffect(() => {
    setGemCountState(gemCountProp)
    setGamMoneyState(gamMoneyProp)
  }, [gamMoneyProp, gemCountProp])
  useEffect(() => {
    const updateScale = () => {
      const margin = 16
      setDialogScale(Math.min(1, (window.innerWidth - margin) / SHOP_W, (window.innerHeight - margin) / SHOP_H))
    }
    updateScale()
    window.addEventListener('resize', updateScale)
    return () => window.removeEventListener('resize', updateScale)
  }, [])
  const gemCount = gemCountState
  const gamMoney = gamMoneyState

  const hanCoin = gemCount
  const hanCoinCoupon = 0

  /* ドラッグ移動 — OnNcHitTest pt.y<31 → HTCAPTION (L561) */
  const [pos, setPos]   = useState({ x: 0, y: 0 })
  const dragging        = useRef(false)
  const dragOffset      = useRef({ dx: 0, dy: 0 })
  const onDragStart     = (e: React.MouseEvent) => {
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect()
    if (e.clientY - rect.top >= 31) return
    dragging.current = true
    dragOffset.current = { dx: e.clientX - pos.x, dy: e.clientY - pos.y }
    e.preventDefault()
  }
  useEffect(() => {
    const onMove = (e: MouseEvent) => {
      if (!dragging.current) return
      setPos({ x: e.clientX - dragOffset.current.dx, y: e.clientY - dragOffset.current.dy })
    }
    const onUp = () => { dragging.current = false }
    window.addEventListener('mousemove', onMove)
    window.addEventListener('mouseup',   onUp)
    return () => { window.removeEventListener('mousemove', onMove); window.removeEventListener('mouseup', onUp) }
  }, [])

  /* ── BUY_CUSTOM タブ: サーバから商品リスト取得 (mjkc35e → mjkc36e)
       原典: ItemShopDlg.cpp:256 OnInitDialog → OnBtnTab1 で 1回 取得
       タブ切替時にサーバ通信は走らない (レガシー) */
  useEffect(() => {
    const handler = (data: Record<string, unknown>) => {
      const raw = Array.isArray(data.shopList) ? (data.shopList as Record<string, unknown>[]) : []
      const list: CustomShopItem[] = raw.map(x => ({
        shopNo:      Number(x.shopNo      ?? x['mjkk139e'] ?? 0),
        customId:    Number(x.customId    ?? x['mjkk138e'] ?? 0),
        kind:        Number(x.customType  ?? x.kind ?? x.itemType ?? 0),
        price:       Number(x.customPrice ?? x.price ?? 0),
        name:        String(x.Name        ?? x.name ?? x.itemName ?? ''),
        description: String(x.description ?? x.itemDescription ?? ''),
        gameMoney:   Number(x.gameMoney   ?? 0),
        purchased:   Number(x.Purchased   ?? x.purchased ?? 0),
      }))
      setCustomItems(list)
    }
    SignalR.on('mjkc36e', handler)
    SignalR.send('mjkc35e', { k3e: player?.pix ?? '' }).catch(() => {})
    return () => SignalR.off('mjkc36e', handler)
  }, [player?.pix])

  /* ── 現在タブの全アイテム数 (レガシー CMJItemManager::GetShopItemCount L268-285) */
  const itemNumMax =
    tabNo === BUY_CUSTOM ? customItems.length :
    tabNo === BUY_ITEM   ? SHOP_ITEM_DATA_BUY.length :
                            SHOP_ITEM_DATA_EXC.length

  /* ── 最大ページ数 (レガシー OnBtnTab1 L613: (m_nItemNumMax-1)/8 + 1) */
  const pageMax = Math.max(1, Math.floor(Math.max(0, itemNumMax - 1) / 8) + 1)

  /* ── 現在ページのアイテム数 (レガシー setItemNumOfPage L799) */
  const itemNumOfPage = Math.max(0, Math.min(8, itemNumMax - (pageNo - 1) * 8))

  /* ── タブ変更 — レガシー OnBtnTab1/3/4 (L608-633) */
  const changeTab = (tab: number) => {
    setTabNo(tab)
    setPageNo(1)
  }

  /* ── ページ操作 — OnBtnPageDown/Up (L702-721) */
  const onPageDown = () => {
    let next = pageNo - 1
    if (next <= 0) next = pageMax  // 順環
    setPageNo(next)
  }
  const onPageUp = () => {
    let next = pageNo + 1
    if (next > pageMax) next = 1   // 順環
    setPageNo(next)
  }

  /* ── 残高テキスト (レガシー L410-441) */
  const text1: string =
    tabNo === EXC_ITEM
      ? `${gemCount}個`
      : `${gemCount}個`
  const text2: string =
    tabNo === EXC_ITEM
      ? moneyStr(gamMoney)
      : moneyStr(gamMoney)

  /* ── OFFSET 計算 (L191-192) */
  const ox = (i: number) => 155 * (i % 4)
  const oy = (i: number) => 175 * Math.floor(i / 4)

  /* ── プレート画像 (タブ別) */
  const plateImg =
    tabNo === BUY_CUSTOM ? `${IMG}/mj_shp_window_plate_01.png` :
    tabNo === BUY_ITEM   ? `${IMG}/mj_shp_window_plate_03.png` :
                            `${IMG}/mj_shp_window_plate_04.png`

  /* ── 1ページ分のアイテムスライス */
  const startIdx = (pageNo - 1) * 8
  const customSlice = customItems.slice(startIdx, startIdx + 8)
  const buySlice    = SHOP_ITEM_DATA_BUY.slice(startIdx, startIdx + 8)
  const excSlice    = SHOP_ITEM_DATA_EXC.slice(startIdx, startIdx + 8)

  const pix = player?.pix ?? ''
  const memberName = player?.name || pix
  const sexIndex = player?.sex === 'F' ? 1 : 0
  const getExcImagePath = (item: ExcItemData) => (
    sexIndex === 1 && item.imagePathFemale ? item.imagePathFemale : item.imagePath
  )
  const customKindName = (kind: number): string => {
    if (kind >= 10 && kind < 20) return '背景'
    if (kind >= 20 && kind < 30) return '牌'
    if (kind >= 30 && kind < 40) return 'コスチューム'
    if (kind >= 50 && kind < 60) return 'BGM'
    return 'その他'
  }
  const openBuyCustom = (item: CustomShopItem) => {
    onBuyCustom?.(item)
    setBuyCustomTarget(item)
  }
  const openBuyItem = (item: BuyItemData) => {
    onBuyItem?.(item)
    setBuyItemTarget(item)
  }
  const openExcItem = (item: ExcItemData) => {
    onExcItem?.(item)
    setExcItemTarget(item)
  }
  const buyItemDescription = (item: BuyItemData): string[] => (
    item.nameSub2
      ? [
          `${item.nameSub}の間、獲得できる龍珠が${item.nameSub2}になります。`,
          '※対局終了時にアイテムの効果が有 効である必要があります。',
          '※龍珠2倍と龍珠3倍が同時に有効な 場合は龍珠4倍となります。',
          `※オマケとして麻雀コイン${moneyStr(item.gameMoney)}が付い てきます。`,
        ]
      : [
          '残っている回数量によって交流広 場及び段位戦場代が',
          '無料になります。',
          '※ハイ卓は対象外となります。',
          '※対局終了時に効果が有効である必要があります。',
          `※オマケとして麻雀コイン${moneyStr(item.gameMoney)}が付い てきます。`,
        ]
  )

  return (
    <div style={{
      position: dialogScale < 1 ? 'fixed' : 'absolute', inset: 0, zIndex: 300,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'rgba(0,0,0,0.45)',
    }}>
      <div style={{ width: SHOP_W * dialogScale, height: SHOP_H * dialogScale }}>
      {/* CItemShopDlg ウィンドウ: 662×514 */}
      <div
        style={{
          position: 'relative',
          width: SHOP_W,
          height: SHOP_H,
          left: dialogScale < 1 ? 0 : pos.x,
          top: dialogScale < 1 ? 0 : pos.y,
          transform: `scale(${dialogScale})`,
          transformOrigin: 'top left',
        }}
        onMouseDown={dialogScale < 1 ? undefined : onDragStart}
      >
        {/* 背景 mj_shp_window.png (662×514) */}
        <img
          src={`${IMG}/mj_shp_window.png`}
          alt=""
          draggable={false}
          style={{ position: 'absolute', left: 0, top: 0, width: SHOP_W, height: SHOP_H, userSelect: 'none' }}
        />

        {/* タイトル "麻雀ショップ" CRect(218,7,445,22) 15px Bold 白 — L407 */}
        <div style={{
          position: 'absolute', left: 218, top: 7, width: 227, height: 15,
          fontFamily: FONT, fontSize: 14, fontWeight: 'bold', color: '#fff',
          lineHeight: '15px', textAlign: 'center', overflow: 'hidden', pointerEvents: 'none',
        }}>
          麻雀ショップ
        </div>

        {/* タブ (3個) — L221-230 */}
        <TabButton src={`${IMG}/mj_shp_tab_05.png`} frameW={145} frameH={33}
          x={14}  y={36} active={tabNo === BUY_CUSTOM} onClick={() => changeTab(BUY_CUSTOM)} />
        <TabButton src={`${IMG}/mj_shp_tab_04.png`} frameW={145} frameH={33}
          x={159} y={36} active={tabNo === BUY_ITEM}   onClick={() => changeTab(BUY_ITEM)}   />
        <TabButton src={`${IMG}/mj_shp_tab_02.png`} frameW={145} frameH={33}
          x={304} y={36} active={tabNo === EXC_ITEM}   onClick={() => changeTab(EXC_ITEM)}   />

        {/* プレート: 上段4枚 (24+155*i, 91), 下段4枚 (24+155*i, 266) — L281-285 */}
        {[0, 1, 2, 3].map(i => (
          <img key={`plate-top-${i}`} src={plateImg} alt="" draggable={false}
            style={{ position: 'absolute', left: 24 + 155 * i, top: 91, width: 148, height: 163, pointerEvents: 'none' }} />
        ))}
        {[0, 1, 2, 3].map(i => (i + 4) < itemNumOfPage && (
          <img key={`plate-bot-${i}`} src={plateImg} alt="" draggable={false}
            style={{ position: 'absolute', left: 24 + 155 * i, top: 266, width: 148, height: 163, pointerEvents: 'none' }} />
        ))}

        {/* ── BUY_CUSTOM タブ — L274-301 */}
        {tabNo === BUY_CUSTOM && customSlice.map((item, i) => {
          const x = ox(i), y = oy(i)
          const imgUrl = `${IMG_CUSTOM_ITEM}/mj_custom_${item.customId}.png`
          const canBuy = gemCount >= item.price && item.purchased === 0
          return (
            <div key={`bc-${item.customId}`}>
              {/* アイテム画像 BUY_CUSTOM (33+ox, 138+oy) — L481 */}
              <img src={imgUrl} alt="" draggable={false}
                onError={e => { (e.currentTarget as HTMLImageElement).style.visibility = 'hidden' }}
                style={{
                  position: 'absolute', left: 33 + x, top: 138 + y,
                  imageRendering: 'pixelated', pointerEvents: 'none',
                }}
              />
              {/* 名前 CRect(33+ox, 103+oy, 33+130+ox, 103+11+oy) DT_CENTER */}
              <div style={{
                position: 'absolute', left: 33 + x, top: 103 + y, width: 130, height: 11,
                fontFamily: FONT, fontSize: ITEM_TEXT_FONT_SIZE, fontWeight: 'bold', color: '#000',
                lineHeight: '11px', textAlign: 'center', overflow: 'hidden', whiteSpace: 'nowrap',
                pointerEvents: 'none',
              }}>
                {item.name}
              </div>
              {/* サブ説明 CRect(33+ox, 115+oy, ...) RGB(40,160,100) */}
              <div style={{
                position: 'absolute', left: 33 + x, top: 115 + y, width: 130, height: 11,
                fontFamily: FONT, fontSize: ITEM_TEXT_FONT_SIZE, color: 'rgb(40,160,100)',
                lineHeight: '11px', textAlign: 'center', overflow: 'hidden', whiteSpace: 'nowrap',
                pointerEvents: 'none',
              }}>
              </div>
              {/* 価格 CRect(39+ox, 228+oy, 39+62+ox, 228+11+oy) DT_RIGHT */}
              <div style={{
                position: 'absolute', left: 39 + x, top: 228 + y, width: 62, height: 11,
                fontFamily: FONT, fontSize: ITEM_TEXT_FONT_SIZE, fontWeight: 'bold', color: '#000',
                lineHeight: '11px', textAlign: 'right', overflow: 'hidden', pointerEvents: 'none',
              }}>
                {moneyStr(item.price)}
              </div>
              {/* Buy ボタン (112+ox, 219+oy) 51×28 — L242 */}
              <SpriteButton src={`${IMG}/mj_shp_btn_buy.png`} frameW={51} frameH={28}
                x={112 + x} y={219 + y}
                disabled={!canBuy}
                onClick={() => openBuyCustom(item)}
                title={item.purchased === 0 ? '購入' : '購入済'} />
            </div>
          )
        })}

        {/* ── BUY_ITEM タブ — L332-351 */}
        {tabNo === BUY_ITEM && buySlice.map((item, i) => {
          const x = ox(i), y = oy(i)
          return (
            <div key={`bi-${item.sellCode}`}>
              {/* アイテム画像 BUY_ITEM (66+ox, 142+oy) — L495 */}
              <img src={item.imagePath} alt="" draggable={false}
                onError={e => { (e.currentTarget as HTMLImageElement).style.visibility = 'hidden' }}
                style={{
                  position: 'absolute', left: 66 + x, top: 142 + y,
                  imageRendering: 'pixelated', pointerEvents: 'none',
                }}
              />
              {/* 名前 (33+ox, 103+oy) */}
              <div style={{
                position: 'absolute', left: 33 + x, top: 103 + y, width: 130, height: 11,
                fontFamily: FONT, fontSize: ITEM_TEXT_FONT_SIZE, fontWeight: 'bold', color: '#000',
                lineHeight: '11px', textAlign: 'center', overflow: 'hidden', whiteSpace: 'nowrap',
                pointerEvents: 'none',
              }}>
                {item.name}
              </div>
              {/* サブ "麻雀ｺｲﾝ%s" — レガシー: strItemDesc.Format("麻雀ｺｲﾝ%s", nameSub) */}
              <div style={{
                position: 'absolute', left: 33 + x, top: 115 + y, width: 130, height: 11,
                fontFamily: FONT, fontSize: ITEM_TEXT_FONT_SIZE, color: 'rgb(40,160,100)',
                lineHeight: '11px', textAlign: 'center', overflow: 'hidden', whiteSpace: 'nowrap',
                pointerEvents: 'none',
              }}>
                {`麻雀ｺｲﾝ${item.nameSub}`}
              </div>
              {/* 価格 */}
              <div style={{
                position: 'absolute', left: 39 + x, top: 228 + y, width: 62, height: 11,
                fontFamily: FONT, fontSize: ITEM_TEXT_FONT_SIZE, fontWeight: 'bold', color: '#000',
                lineHeight: '11px', textAlign: 'right', overflow: 'hidden', pointerEvents: 'none',
              }}>
                {moneyStr(item.hancoinPrice)}
              </div>
              {/* Buy (112+ox, 219+oy) 51×28 */}
              <SpriteButton src={`${IMG}/mj_shp_btn_buy.png`} frameW={51} frameH={28}
                x={112 + x} y={219 + y}
                disabled={gemCount < item.hancoinPrice}
                onClick={() => openBuyItem(item)}
                title="購入" />
            </div>
          )
        })}

        {/* ── EXC_ITEM タブ — L353-389, L505-543 */}
        {tabNo === EXC_ITEM && excSlice.map((item, i) => {
          const x = ox(i), y = oy(i)
          // 空き枠 (costGem==0 && gameMoney==0) は描画しない — L369-378
          const isEmpty = item.costGem === 0 && item.gameMoney === 0
          if (isEmpty) return null
          const imagePath = getExcImagePath(item)
          // 残数/期限テキスト — L518-531
          const subText =
            item.limitDays >= 0  ? `${item.limitDays}日` :
            item.quantity  >  0  ? `${item.quantity}回` :
                                    '永久'
          return (
            <div key={`ex-${item.itemCode}-${i}`}>
              {/* アイテム画像 EXC (66+ox, 142+oy) — L510 */}
              <img src={imagePath} alt="" draggable={false}
                onError={e => { (e.currentTarget as HTMLImageElement).style.visibility = 'hidden' }}
                style={{
                  position: 'absolute', left: 66 + x, top: 142 + y,
                  imageRendering: 'pixelated', pointerEvents: 'none',
                }}
              />
              {/* 名前 (33+ox, 103+oy) */}
              <div style={{
                position: 'absolute', left: 33 + x, top: 103 + y, width: 130, height: 11,
                fontFamily: FONT, fontSize: ITEM_TEXT_FONT_SIZE, fontWeight: 'bold', color: '#000',
                lineHeight: '11px', textAlign: 'center', overflow: 'hidden', whiteSpace: 'nowrap',
                pointerEvents: 'none',
              }}>
                {item.name}
              </div>
              {/* 残数/期限 (33+ox, 115+oy) */}
              <div style={{
                position: 'absolute', left: 33 + x, top: 115 + y, width: 130, height: 11,
                fontFamily: FONT, fontSize: ITEM_TEXT_FONT_SIZE, color: 'rgb(40,160,100)',
                lineHeight: '11px', textAlign: 'center', overflow: 'hidden', whiteSpace: 'nowrap',
                pointerEvents: 'none',
              }}>
                {subText}
              </div>
              {/* Gem (66+ox, 220+oy) — L539 */}
              <div style={{
                position: 'absolute', left: 66 + x, top: 220 + y, width: 62, height: 11,
                fontFamily: FONT, fontSize: ITEM_TEXT_FONT_SIZE, fontWeight: 'bold', color: '#000',
                lineHeight: '11px', textAlign: 'right', overflow: 'hidden', pointerEvents: 'none',
              }}>
                {item.costGem}個
              </div>
              {/* Money (66+ox, 234+oy) — L540 */}
              <div style={{
                position: 'absolute', left: 66 + x, top: 234 + y, width: 62, height: 11,
                fontFamily: FONT, fontSize: ITEM_TEXT_FONT_SIZE, fontWeight: 'bold', color: '#000',
                lineHeight: '11px', textAlign: 'right', overflow: 'hidden', pointerEvents: 'none',
              }}>
                {moneyStr(item.gameMoney)}
              </div>
              {/* Exc ボタン (132+ox, 219+oy) 34×28 — L247 */}
              <SpriteButton src={`${IMG}/mj_shp_btn_exchange.png`} frameW={34} frameH={28}
                x={132 + x} y={219 + y}
                onClick={() => openExcItem(item)}
                title="交換" />
            </div>
          )
        })}

        {/* EXC タブのみ コイン画像 mj_shp_money_01 (228×58, 1f) at (15, 443) — L367 */}
        {tabNo === EXC_ITEM && (
          <img src={`${IMG}/mj_shp_money_01.png`} alt="" draggable={false}
            style={{ position: 'absolute', left: 15, top: 443, width: 228, height: 58, pointerEvents: 'none' }}
          />
        )}

        {/* 残高テキスト text1: (150,453)-(235,464) DT_RIGHT 12px Bold 黒 */}
        <div style={{
          position: 'absolute', left: 150, top: 453, width: 85, height: 11,
          fontFamily: FONT, fontSize: 11, fontWeight: 'bold', color: '#000',
          lineHeight: '11px', textAlign: 'right', overflow: 'hidden', pointerEvents: 'none',
        }}>{text1}</div>
        {/* 残高テキスト text2: (150,480)-(235,491) */}
        <div style={{
          position: 'absolute', left: 150, top: 480, width: 85, height: 11,
          fontFamily: FONT, fontSize: 11, fontWeight: 'bold', color: '#000',
          lineHeight: '11px', textAlign: 'right', overflow: 'hidden', pointerEvents: 'none',
        }}>{text2}</div>

        {/* ページ番号: 現在(298, 457), 最大(344, 457) — L391-392 */}
        <NumImg x={298} y={457} num={pageNo} />
        <NumImg x={344} y={457} num={pageMax} />

        {/* ◀ pagedown (257, 451) 26×42 — L259 */}
        <SpriteButton src={`${IMG}/mj_shp_pagedown.png`} frameW={26} frameH={42}
          x={257} y={451} disabled={pageMax <= 1} onClick={onPageDown} title="前のページ" />

        {/* ▶ pageup (382, 451) 26×42 — L265 */}
        <SpriteButton src={`${IMG}/mj_shp_pageup.png`} frameW={26} frameH={42}
          x={382} y={451} disabled={pageMax <= 1} onClick={onPageUp} title="次のページ" />

        {/* limit ボタン (467, 469) 88×32 — L251 */}
        <SpriteButton src={`${IMG}/mj_shp_btn_limit.png`} frameW={88} frameH={32}
          x={467} y={469} onClick={() => onConfirmItem?.()} title="アイテム確認" />

        {/* 閉じる (560, 469) 88×32 — L215 */}
        <SpriteButton src={`${IMG}/mj_shp_btn_close.png`} frameW={88} frameH={32}
          x={560} y={469} onClick={onClose} title="閉じる" />
      </div>
      </div>

      {buyCustomTarget && (
        <BuyCustomItemDlg
          item={{
            itemId: buyCustomTarget.customId,
            itemName: buyCustomTarget.name,
            itemType: customKindName(buyCustomTarget.kind),
            itemDesc: buyCustomTarget.description,
            price: buyCustomTarget.price,
            shopNo: buyCustomTarget.shopNo,
            gameMoney: buyCustomTarget.gameMoney,
          }}
          pix={pix}
          memberName={memberName}
          hanCoin={hanCoin}
          hanCoupon={hanCoinCoupon}
          onClose={() => setBuyCustomTarget(null)}
          onBuyOK={() => {
            setCustomItems(items => items.map(item => (
              item.shopNo === buyCustomTarget.shopNo ? { ...item, purchased: 1 } : item
            )))
            SignalR.send('mjkc35e', { k3e: player?.pix ?? '' }).catch(() => {})
          }}
        />
      )}

      {buyItemTarget && (
        <BuyHanCoinItemDlg
          item={{
            itemCode: buyItemTarget.avCode,
            sellCode: buyItemTarget.sellCode,
            itemName: buyItemTarget.name,
            price: buyItemTarget.hancoinPrice,
            gameMoney: buyItemTarget.gameMoney,
            description: buyItemDescription(buyItemTarget),
            imageUrl: buyItemTarget.imagePath,
            isLottery: false,
          }}
          pix={pix}
          memberName={memberName}
          hanCoin={hanCoin}
          hanCoupon={hanCoinCoupon}
          onClose={() => setBuyItemTarget(null)}
        />
      )}

      {excItemTarget && (
        <BuyExchangeItemDlg
          item={{
            sellCode: excItemTarget.sellCode,
            itemName: excItemTarget.name,
            itemKind: excItemTarget.itemKind,
            itemGuid1: excItemTarget.guid1,
            itemGuid2: excItemTarget.guid2,
            costGem: excItemTarget.costGem,
            costMoney: excItemTarget.gameMoney,
            limitDays: excItemTarget.limitDays,
            quantity: excItemTarget.quantity,
            imageUrl: getExcImagePath(excItemTarget),
          }}
          pix={pix}
          memberName={memberName}
          userGem={gemCount}
          userMoney={gamMoney}
          onClose={() => setExcItemTarget(null)}
          onBuyOK={({ userGem, userMoney }) => {
            setGemCountState(userGem)
            setGamMoneyState(userMoney)
            onBalanceUpdate?.({ gemCount: userGem, gamMoney: userMoney })
          }}
        />
      )}
    </div>
  )
}
