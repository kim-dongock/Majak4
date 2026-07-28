/**
 * CMJCustomReceiptDlg 相当 — カスタムアイテム購入レシート (AP-09 §3-2-10)
 * レガシー: legacy/client/HgMajak2/MJCustomReceiptDlg.h/cpp
 *
 * ウィンドウ: MoveWindow(0,0,390,470) → 390×470px, CenterWindow(GetParent())
 * OnNcHitTest: pt.y < 41 → HTCAPTION (ドラッグ移動可)
 *
 * ── 画像配置 (OnCreate / OnPaint より — 座標すべてレガシー準拠) ─────────────
 *
 * 背景 (1フレーム 390×470):
 *   _ShopReceiptMainCustom.png  at (0, 0)
 *   ラベル群は背景に焼き込み済み
 *
 * Exit ✕ ボタン (4フレーム 18×18):
 *   _ShopReceiptExitBtn.png  at (358, 12)  IDOK
 *
 * OK 閉じるボタン (4フレーム 112×20):
 *   _ShopReceiptOkBtn.png   at (139, 427)  IDOK
 *
 * アイテム画像 (1フレーム):
 *   items/custom/mj_custom_{ItemID:02d}.png  at (162, 74)
 *   MAJAK4_ITEM_LIST_CUSTOM_ITEM + ItemID + MAJAK4_EXTENSION_HIM 相当
 *
 * ── テキスト (OnPaint — 13px bold MS Pゴシック, 透過背景) ─────────────────
 *   m_strMessage[0]  TextOut(15, 50)        購入者名
 *   itemName         CRect(198,170,372,207) DT_CENTER  アイテム名
 *   m_strMessage[1]  CRect(198,205,372,222) DT_RIGHT   購入前GEM
 *   m_strMessage[2]  CRect(198,225,372,242) DT_RIGHT   購入前商品券
 *   m_strMessage[3]  CRect(198,245,372,262) DT_RIGHT   購入価格
 *   m_strMessage[4]  CRect(198,265,372,282) DT_RIGHT   購入後GEM
 *   m_strMessage[5]  CRect(198,285,372,302) DT_RIGHT   購入後商品券
 *   m_strMessage[6]  TextOut(17, 308)       購入完了
 *   m_strMessage[7]  TextOut(17, 322)       コイン補充
 *   m_strMessage[8]  TextOut(17, 350)       緑文字 RGB(40,160,100) — 常に Empty()
 *   m_strMessage[9]  TextOut(17, 364)       緑文字 RGB(40,160,100) — 常に Empty()
 * ────────────────────────────────────────────────────────────────────────
 */
import { useRef, useEffect, useState } from 'react'
import { useOutgameLayoutMode } from '../../../hooks/useOutgameLayoutMode'

const IMG      = '/assets/images/game'
const IMG_ITEM = '/assets/images/game/items/custom'
const DIALOG_W = 390
const DIALOG_H = 470

interface Props {
  pix: string
  memberName?: string
  itemId: number           // CMajakShopCustomItem.Item.ItemID
  itemName: string         // CMajakShopCustomItem.Item.ItemName
  price: number            // CMajakShopCustomItem.Price
  gameMoney: number        // CMajakShopCustomItem.GameMoney (コイン補充額)
  coinBefore: number       // nHanCoinBefore
  couponBefore: number     // nHanCoinCouponBefore
  coinAfter: number        // nHanCoinAfter
  couponAfter: number      // nHanCoinCouponAfter
  onClose: () => void
}

/** ====================================================================
 * CMJBmpButton 相当 — AP-06 §2 4フレームスプライトボタン
 * ==================================================================== */
function SpriteButton({
  src, frameW, frameH, x, y, onClick, title,
}: {
  src: string; frameW: number; frameH: number
  x: number; y: number; onClick: () => void; title?: string
}) {
  const [fi, setFi] = useState(0)
  return (
    <button
      title={title}
      onClick={onClick}
      onMouseEnter={() => setFi(2)}
      onMouseLeave={() => setFi(0)}
      onMouseDown={() => setFi(3)}
      onMouseUp={() => setFi(2)}
      style={{
        position: 'absolute', left: x, top: y,
        width: frameW, height: frameH,
        appearance: 'none', WebkitAppearance: 'none',
        backgroundColor: 'transparent',
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-fi * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        border: 'none', padding: 0,
        cursor: 'pointer', outline: 'none',
        overflow: 'hidden', zIndex: 2,
        imageRendering: 'pixelated',
      }}
    />
  )
}

/** ====================================================================
 * CMJCustomReceiptDlg 本体
 * ==================================================================== */
export default function CustomReceiptDlg({
  pix, memberName, itemId, itemName, price, gameMoney,
  coinBefore, couponBefore, coinAfter, couponAfter,
  onClose,
}: Props) {
  /* ドラッグ移動 (OnNcHitTest: pt.y < 41 → HTCAPTION) */
  const [pos, setPos]   = useState({ x: 0, y: 0 })
  const layoutMode = useOutgameLayoutMode()
  const isMobile = layoutMode !== 'desktop'
  const [dialogScale, setDialogScale] = useState(1)
  const dragging        = useRef(false)
  const dragOffset      = useRef({ dx: 0, dy: 0 })

  const onDragStart = (e: React.MouseEvent) => {
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect()
    if (e.clientY - rect.top >= 41) return
    dragging.current   = true
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
    return () => {
      window.removeEventListener('mousemove', onMove)
      window.removeEventListener('mouseup',   onUp)
    }
  }, [])

  useEffect(() => {
    if (!isMobile) {
      setDialogScale(1)
      return
    }
    const updateScale = () => {
      const margin = 16
      setDialogScale(Math.min(1, (window.innerWidth - margin) / DIALOG_W, (window.innerHeight - margin) / DIALOG_H))
    }
    updateScale()
    window.addEventListener('resize', updateScale)
    return () => window.removeEventListener('resize', updateScale)
  }, [isMobile])

  /* アイテム画像パス: MAJAK4_ITEM_LIST_CUSTOM_ITEM + ItemID:02d + MAJAK4_EXTENSION_HIM 相当 */
  const itemImageSrc = `${IMG_ITEM}/mj_custom_${String(itemId).padStart(2, '0')}.png`

  /* メッセージ生成 (コンストラクタ相当) */
  const yen  = (n: number) => `${n}円`
  const moneyString = (value: number) => {
    const sign = value < 0 ? '-' : ''
    const digits = String(Math.abs(Math.trunc(value)))
    const units = ['', '万', '億', '兆', '京']
    const parts: string[] = []
    for (let end = digits.length, unit = 0; end > 0; end -= 4, unit++) {
      const start = Math.max(0, end - 4)
      const part = Number(digits.slice(start, end))
      if (part > 0) parts.unshift(`${part}${units[unit] ?? ''}`)
    }
    return `${sign}${parts.length > 0 ? parts.join('') : '0'}円`
  }
  const msg0 = `"${memberName || pix}"さんが購入したアイテム`
  const msg1 = yen(coinBefore)
  const msg2 = yen(couponBefore)
  const msg3 = yen(price)
  const msg4 = yen(coinAfter)
  const msg5 = yen(couponAfter)
  const msg6 = `${itemName}を購入しました`
  const msg7 = `麻雀コイン${moneyString(gameMoney)}が補充されました。`

  /* テキストスタイル */
  const tBase = {
    position: 'absolute' as const,
    fontFamily: "'MS PGothic', 'Noto Sans JP', 'Noto Sans JP', 'MS UI Gothic', sans-serif" as const,
    fontSize: 13,
    fontWeight: 'bold' as const,
    color: '#000' as const,
    pointerEvents: 'none' as const,
    overflow: 'hidden' as const,
    whiteSpace: 'nowrap' as const,
  }
  const right = (l: number, t: number, w: number, h: number) => ({
    ...tBase, left: l, top: t, width: w, height: h, textAlign: 'right' as const,
  })
  const center = (l: number, t: number, w: number, h: number) => ({
    ...tBase, left: l, top: t, width: w, height: h, textAlign: 'center' as const,
  })

  return (
    /* モーダルオーバーレイ */
    <div style={{
      position: isMobile ? 'fixed' : 'absolute', inset: 0,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'transparent', zIndex: 300,
    }}>
      <div style={{ width: DIALOG_W * dialogScale, height: DIALOG_H * dialogScale }}>
      {/* CMJCustomReceiptDlg クライアント領域: 390×470px */}
      <div style={{
        position: 'relative',
        width: DIALOG_W, height: DIALOG_H,
        left: isMobile ? 0 : pos.x, top: isMobile ? 0 : pos.y,
        transform: `scale(${dialogScale})`,
        transformOrigin: 'top left',
      }}>

        {/* ================================================================
            背景: _ShopReceiptMainCustom.png (390×470) at (0,0)
            Create(..., 1, ...) = 1フレーム単一画像
            ================================================================ */}
        <img
          src={`${IMG}/_ShopReceiptMainCustom.png`}
          alt=""
          draggable={false}
          onMouseDown={onDragStart}
          style={{
            position: 'absolute', left: 0, top: 0,
            width: 390, height: 470,
            cursor: 'move', userSelect: 'none',
          }}
        />

        {/* ================================================================
            アイテム画像: items/custom/mj_custom_{id:02d}.png at (162, 74)
            m_pItemImage->Draw(&dc, 162, 74, 0)
            ================================================================ */}
        <img
          src={itemImageSrc}
          alt={itemName}
          draggable={false}
          style={{
            position: 'absolute', left: 162, top: 74,
            pointerEvents: 'none',
          }}
          onError={e => { (e.currentTarget as HTMLImageElement).style.visibility = 'hidden' }}
        />

        {/* ================================================================
            テキスト (OnPaint — 13px bold MS Pゴシック, 透過背景)
            ================================================================ */}
        {/* msg0 TextOut(15, 50) */}
        <span style={{ ...tBase, left: 15, top: 50 }}>{msg0}</span>

        {/* itemName CRect(198,170,372,207) DT_CENTER */}
        <div style={center(198, 170, 174, 37)}>{itemName}</div>

          {/* msg1 CRect(198,205,372,222) DT_RIGHT — 購入前GEM */}
        <div style={right(198, 205, 174, 17)}>{msg1}</div>

        {/* msg2 CRect(198,225,372,242) DT_RIGHT — 購入前商品券 */}
        <div style={right(198, 225, 174, 17)}>{msg2}</div>

        {/* msg3 CRect(198,245,372,262) DT_RIGHT — 購入価格 */}
        <div style={right(198, 245, 174, 17)}>{msg3}</div>

          {/* msg4 CRect(198,265,372,282) DT_RIGHT — 購入後GEM */}
        <div style={right(198, 265, 174, 17)}>{msg4}</div>

        {/* msg5 CRect(198,285,372,302) DT_RIGHT — 購入後商品券 */}
        <div style={right(198, 285, 174, 17)}>{msg5}</div>

        {/* msg6 TextOut(17, 308) */}
        <span style={{ ...tBase, left: 17, top: 308 }}>{msg6}</span>

        {/* msg7 TextOut(17, 322) */}
        <span style={{ ...tBase, left: 17, top: 322 }}>{msg7}</span>

        {/* msg8/9 TextOut(17, 350/364) RGB(40,160,100) — 現在は常に Empty() */}

        {/* ================================================================
            Exit ✕: _ShopReceiptExitBtn.png (72×18, 4フレーム 18×18) at (358,12)
            m_btnExit.Create(0, ..., 358, 12, ..., IDOK)
            ================================================================ */}
        <SpriteButton
          src={`${IMG}/_ShopReceiptExitBtn.png`}
          frameW={18} frameH={18}
          x={358} y={12}
          onClick={onClose}
          title="閉じる"
        />

        {/* ================================================================
            OK閉じる: _ShopReceiptOkBtn.png (448×20, 4フレーム 112×20) at (139,427)
            m_btnClose.Create(0, ..., 139, 427, ..., IDOK)
            ================================================================ */}
        <SpriteButton
          src={`${IMG}/_ShopReceiptOkBtn.png`}
          frameW={112} frameH={20}
          x={139} y={427}
          onClick={onClose}
          title="OK"
        />
      </div>
      </div>
    </div>
  )
}
