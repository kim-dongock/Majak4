/**
 * HanCoinReceiptDlg — CMJReceiptDlg 相当のキャッシュ購入レシート (AP-09 §3-2-8)
 * レガシー: legacy/client/HgMajak2/MJReceiptDlg.h/cpp
 *
 * ウィンドウ: MoveWindow(0,0,390,470) → 390×470px, CenterWindow(GetParent())
 *
 * ── 画像配置 (OnCreate / OnPaint より — 座標すべてレガシー準拠) ─────────────
 *
 * 背景 (1フレーム 390×470):
 *   コインアイテム (m_strSellCode == ""):
 *     _ShopReceiptMain2.png   at (0, 0)
 *   便利アイテム (m_strSellCode != ""):
 *     _ShopReceiptMain2b.png  at (0, 0)
 *
 * Exit ✕ ボタン (4フレーム 18×18):
 *   _ShopReceiptExitBtn.png  at (358, 12)  IDOK
 *
 * OK 閉じるボタン (4フレーム 112×20):
 *   _ShopReceiptOkBtn.png   at (139, 427)  IDOK
 *
 * アイテム画像 (1フレーム):
 *   m_pItemImage->Draw(&dc, 162, 74, 0)
 *
 * ── テキスト (OnPaint — 13px bold MS Pゴシック, 透過背景) ─────────────────
 *   m_strMessage[0]  TextOut(15, 50)       購入者名
 *   itemName         CRect(198,165,372,182) DT_CENTER  アイテム名
 *   m_strMessage[1]  CRect(198,185,372,202) DT_RIGHT   購入前キャッシュ
 *   m_strMessage[2]  CRect(198,205,372,222) DT_RIGHT   購入前商品券
 *   m_strMessage[3]  CRect(198,225,372,242) DT_RIGHT   価格×数量
 *   m_strMessage[4]  CRect(198,245,372,262) DT_RIGHT   購入後キャッシュ
 *   m_strMessage[5]  CRect(198,265,372,282) DT_RIGHT   購入後商品券
 *   m_strMessage[6]  TextOut(17, 288)       購入完了メッセージ
 *   m_strMessage[7]  TextOut(17, 302)       コイン補充メッセージ
 *   m_strMessage[8]  TextOut(17, 330)       緑文字 RGB(40,160,100)
 *   m_strMessage[9]  TextOut(17, 344)       緑文字 RGB(40,160,100)
 * ────────────────────────────────────────────────────────────────────────
 */
import { useRef, useEffect, useState } from 'react'
import { useOutgameLayoutMode } from '../../../hooks/useOutgameLayoutMode'

const IMG = '/assets/images/game'
const FONT = 'var(--majak-font-family-ui)'
const DIALOG_W = 390
const DIALOG_H = 470

interface Props {
  pix: string
  memberName?: string
  itemName: string
  sellCode: string       // "" = コインアイテム, その他 = 便利アイテム
  price: number          // m_nHancoinPrice
  count: number
  coinBefore: number     // nHanCoinBefore
  coinAfter: number      // nHanCoinAfter
  gameMoney: number      // m_llGameMoney (コイン補充額)
  imageUrl?: string
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
 * HanCoinReceiptDlg 本体
 * ==================================================================== */
export default function HanCoinReceiptDlg({
  pix, memberName, itemName, sellCode, price, count,
  coinBefore, coinAfter,
  gameMoney, imageUrl, onClose,
}: Props) {
  /* ドラッグ移動 */
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

  /* 背景画像選択 */
  const bgSrc = sellCode === ''
    ? `${IMG}/_ShopReceiptMain2.png`    /* コインアイテム */
    : `${IMG}/_ShopReceiptMain2b.png`   /* 便利アイテム */

  const yen = (n: number) => `${Math.trunc(n).toLocaleString('ja-JP')} MP`
  const moneyString = (value: number) => `${Math.trunc(value).toLocaleString('ja-JP')} GP`
  const isConvenience = sellCode !== ''

  /* メッセージ生成 (コンストラクタの m_strMessage[] 相当) */
  const msg0 = `"${memberName || pix}"さんが購入したアイテム`
  const msg1 = yen(coinBefore)
  const msg3 = `(${yen(price)}×${count})${Math.trunc(price * count).toLocaleString('ja-JP')} MP`
  const msg4 = yen(coinAfter)
  const msg6 = isConvenience
    ? `${itemName}を${count}個購入しました`
    : `${itemName}を${count}個購入して`
  const msg7 = `${moneyString(gameMoney * count)}が補充されました。`

  /* テキストスタイル */
  const textBase = {
    position: 'absolute' as const,
    fontFamily: FONT,
    fontSize: 'calc(13px * var(--majak-type-scale))',
    fontWeight: 'bold' as const,
    pointerEvents: 'none' as const,
    whiteSpace: 'nowrap' as const,
  }
  const rightBox = (left: number, top: number, width: number, height: number) => ({
    ...textBase,
    left, top, width, height,
    color: '#000',
    textAlign: 'right' as const,
    overflow: 'hidden' as const,
  })
  const centerBox = (left: number, top: number, width: number, height: number) => ({
    ...textBase,
    left, top, width, height,
    color: '#000',
    textAlign: 'center' as const,
    overflow: 'hidden' as const,
  })

  return (
    /* モーダルオーバーレイ */
    <div style={{
      position: isMobile ? 'fixed' : 'absolute', inset: 0,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'transparent', zIndex: 300,
    }}>
      <div style={{ width: DIALOG_W * dialogScale, height: DIALOG_H * dialogScale }}>
      {/* CMJReceiptDlg クライアント領域: 390×470px */}
      <div style={{
        position: 'relative',
        width: DIALOG_W, height: DIALOG_H,
        left: isMobile ? 0 : pos.x, top: isMobile ? 0 : pos.y,
        transform: `scale(${dialogScale})`,
        transformOrigin: 'top left',
      }}
        onMouseDown={isMobile ? undefined : onDragStart}
      >

        {/* ================================================================
            背景
            コインアイテム: _ShopReceiptMain2.png  (390×470, 1フレーム)
            便利アイテム:   _ShopReceiptMain2b.png (390×470, 1フレーム)
            ================================================================ */}
        <img
          src={bgSrc}
          alt=""
          draggable={false}
          style={{
            position: 'absolute', left: 0, top: 0,
            width: 390, height: 470,
            userSelect: 'none',
          }}
        />

        {/* ================================================================
            アイテム画像 m_pItemImage->Draw(&dc, 162, 74, 0)
            ================================================================ */}
        {imageUrl && (
          <img
            src={imageUrl}
            alt={itemName}
            draggable={false}
            style={{
              position: 'absolute', left: 162, top: 74,
              pointerEvents: 'none',
            }}
            onError={e => { (e.currentTarget as HTMLImageElement).style.visibility = 'hidden' }}
          />
        )}

        {/* ================================================================
            テキスト (OnPaint)
            ================================================================ */}

        {/* m_strMessage[0] TextOut(15, 50) — 購入者名 */}
        <span style={{ ...textBase, left: 15, top: 50, color: '#000' }}>{msg0}</span>

        {/* アイテム名 CRect(198,165,372,182) DT_CENTER */}
        <div style={centerBox(198, 165, 174, 17)}>{itemName}</div>

          {/* m_strMessage[1] CRect(198,185,372,202) DT_RIGHT — 購入前GEM */}
        <div style={rightBox(198, 185, 174, 17)}>{msg1}</div>

        {/* m_strMessage[3] CRect(198,225,372,242) DT_RIGHT — 価格×数量 */}
        <div style={rightBox(198, 225, 174, 17)}>{msg3}</div>

          {/* m_strMessage[4] CRect(198,245,372,262) DT_RIGHT — 購入後GEM */}
        <div style={rightBox(198, 245, 174, 17)}>{msg4}</div>

        {/* m_strMessage[6] TextOut(17, 288) — 購入完了 */}
        <span style={{ ...textBase, left: 17, top: 288, color: '#000' }}>{msg6}</span>

        {/* m_strMessage[7] TextOut(17, 302) — コイン補充 */}
        <span style={{ ...textBase, left: 17, top: 302, color: '#000' }}>{msg7}</span>

        {/* m_strMessage[8] TextOut(17, 330) — 緑文字 RGB(40,160,100) */}
        {/* m_strMessage[9] TextOut(17, 344) — 緑文字 RGB(40,160,100) */}
        {/* (レガシーコメントアウト済み — 現在は常に Empty()) */}

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
