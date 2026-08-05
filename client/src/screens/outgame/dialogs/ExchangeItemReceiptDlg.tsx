/**
 * ExchangeItemReceiptDlg — CMJReceiptDlg2 相当の龍宝石/麻雀コイン交換レシート (AP-09 §3-2-9)
 * レガシー: legacy/client/HgMajak2/MJReceiptDlg2.h/cpp
 *
 * ウィンドウ: MoveWindow(0,0,389,500) → 389×500px, CenterWindow(GetParent())
 * OnNcHitTest: pt.y < 41 → HTCAPTION (ドラッグ移動可)
 *
 * ── 画像配置 (OnCreate / OnPaint より — 座標すべてレガシー準拠) ─────────────
 *
 * 背景 (1フレーム 389×500):
 *   mj_shp_window_exchange_04.png  at (0, 0)
 *   ラベル群 ("アイテム名" / "必要龍珠" / "保有龍珠" 等) は背景に焼き込み済み
 *
 * 確認 OK ボタン (4フレーム 85×29):
 *   mj_shp_btn_confirmation.png  at (152, 455)  IDOK
 *
 * アイテム画像:
 *   m_pItemImage->Draw(&dc, 162, 77, 0)
 *
 * ── テキスト (OnPaint — 12px bold MS Pゴシック, 透過背景) ─────────────────
 *   m_strMessage[0]  CRect(56,53,334,64)   DT_CENTER  購入者名
 *   itemName         CRect(144,155,351,166) DT_RIGHT   アイテム名
 *   itemKind         CRect(144,178,351,189) DT_RIGHT   アイテム種類
 *   itemGuid1        CRect(48,221,351,232)  DT_RIGHT   説明1
 *   itemGuid2        CRect(48,233,351,244)  DT_RIGHT   説明2
 *   m_strMessage[3]  CRect(260,261,346,272) DT_RIGHT   必要龍珠数
 *   m_strMessage[4]  CRect(260,291,346,302) DT_RIGHT   必要麻雀コイン
 *   m_strMessage[5]  CRect(260,325,346,336) DT_RIGHT   保有龍珠数
 *   m_strMessage[6]  CRect(260,355,346,366) DT_RIGHT   保有麻雀コイン
 *   m_strMessage[7]  CRect(144,383,351,394) DT_RIGHT   利用可能期間
 *   m_strMessage[8]  CRect(45,406,354,417)  DT_LEFT    注意書き1
 *   m_strMessage[9]  CRect(45,418,354,429)  DT_LEFT    注意書き2
 *   m_strMessage[10] CRect(45,430,354,441)  DT_LEFT    注意書き3
 * ────────────────────────────────────────────────────────────────────────
 */
import { useRef, useEffect, useState } from 'react'
import { useOutgameLayoutMode } from '../../../hooks/useOutgameLayoutMode'

const IMG = '/assets/images/game'
const DIALOG_W = 389
const DIALOG_H = 500

/** アイテム種類 (SJIS デコード済み) */
export type ExchangeReceiptItemKind = 'アバター' | '麻雀称号' | 'リーチ棒' | 'other'

interface Props {
  pix: string
  memberName?: string
  itemName: string
  itemKind: string       // m_strItemKind
  itemGuid1: string
  itemGuid2: string
  costGem: number        // m_nCostGem
  costMoney: number      // m_llGameMoney
  userGem: number        // 保有龍珠
  userMoney: number      // 保有麻雀コイン
  limitDays: number      // m_nLimitDays (-1=永久)
  quantity: number       // m_nQuantity
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
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-fi * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        border: 'none', padding: 0,
        cursor: 'pointer', outline: 'none',
        imageRendering: 'pixelated',
      }}
    />
  )
}

/** ====================================================================
 * ExchangeItemReceiptDlg 本体
 * ==================================================================== */
export default function ExchangeItemReceiptDlg({
  pix, memberName, itemName, itemKind,
  itemGuid1, itemGuid2,
  costGem, costMoney, userGem, userMoney,
  limitDays, quantity, imageUrl, onClose,
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

  /* m_strMessage[] 生成 (コンストラクタ相当) */
  const msg0 = `"${memberName || pix}"さんが交換するアイテム`
  const msg3 = `${Math.trunc(costGem).toLocaleString('ja-JP')}個`
  const msg4 = makeMoneyString(costMoney)
  const msg5 = `${Math.trunc(userGem).toLocaleString('ja-JP')}個`
  const msg6 = makeMoneyString(userMoney)
  const msg7 = limitDays < 0
    ? (quantity <= 0 ? '永久' : `${quantity}回`)
    : `${limitDays}日間`

  function makeMoneyString(value: number): string {
    return `${Math.trunc(value).toLocaleString('ja-JP')} GP`
  }

  /* 注意書き (m_strMessage[8/9/10]) — アイテム種類別 */
  let msg8: string, msg9: string, msg10: string
  if (itemKind === 'アバター') {
    msg8  = '交換したアバターアイテムはハンゲームのマイページで'
    msg9  = '確認してください。'
    msg10 = ''
  } else if (itemKind === '麻雀称号') {
    msg8  = '交換した麻雀称号は麻雀４ページのコレクションで確認'
    msg9  = 'してください。'
    msg10 = ''
  } else if (itemKind === 'リーチ棒') {
    msg8  = '交換したリーチ棒はすぐ反映されます。'
    msg9  = '有効期間はショップの有効期間確認で確認してくださ'
    msg10 = 'い。'
  } else {
    msg8  = '交換したアナウンスはすぐ反映されます。'
    msg9  = '有効期間はショップの有効期間確認で確認してくださ'
    msg10 = 'い。'
  }

  /* テキストスタイル */
  const tBase = {
    position: 'absolute' as const,
    fontFamily: 'var(--majak-font-family-ui)' as const,
    fontSize: 'calc(12px * var(--majak-type-scale))', fontWeight: 'bold' as const,
    color: '#000' as const,
    pointerEvents: 'none' as const,
    overflow: 'hidden' as const, whiteSpace: 'nowrap' as const,
  }
  const right = (l: number, t: number, w: number, h: number) => ({
    ...tBase, left: l, top: t, width: w, height: h, textAlign: 'right' as const,
  })
  const left = (l: number, t: number, w: number, h: number) => ({
    ...tBase, left: l, top: t, width: w, height: h, textAlign: 'left' as const,
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
      {/* ExchangeItemReceiptDlg クライアント領域: 389×500px */}
      <div style={{
        position: 'relative',
        width: DIALOG_W, height: DIALOG_H,
        left: isMobile ? 0 : pos.x, top: isMobile ? 0 : pos.y,
        transform: `scale(${dialogScale})`,
        transformOrigin: 'top left',
      }}>

        {/* ================================================================
            背景: mj_shp_window_exchange_04.png (389×500) at (0,0)
            Create(..., 1, ...) = 1フレーム単一画像
            ================================================================ */}
        <img
          src={`${IMG}/mj_shp_window_exchange_04.png`}
          alt=""
          draggable={false}
          onMouseDown={onDragStart}
          style={{
            position: 'absolute', left: 0, top: 0,
            width: 389, height: 500,
            cursor: 'move', userSelect: 'none',
          }}
        />

        {/* アイテム画像 m_pItemImage->Draw(&dc, 162, 77, 0) */}
        {imageUrl && (
          <img
            src={imageUrl}
            alt={itemName}
            draggable={false}
            style={{
              position: 'absolute', left: 162, top: 77,
              pointerEvents: 'none',
            }}
            onError={e => { (e.currentTarget as HTMLImageElement).style.visibility = 'hidden' }}
          />
        )}

        {/* ================================================================
            テキスト (OnPaint — 12px bold MS Pゴシック, 透過背景)
            ================================================================ */}

        {/* m_strMessage[0] CRect(56,53,334,64) DT_CENTER */}
        <div style={center(56, 53, 278, 11)}>{msg0}</div>
        <div style={{ ...center(56, 67, 278, 11), color: 'rgb(40,160,100)' }}>交換が完了しました。</div>

        {/* itemName CRect(144,155,351,166) DT_RIGHT */}
        <div style={right(144, 155, 207, 11)}>{itemName}</div>

        {/* itemKind CRect(144,178,351,189) DT_RIGHT */}
        <div style={right(144, 178, 207, 11)}>{itemKind}</div>

        {/* itemGuid1 CRect(48,221,351,232) DT_RIGHT */}
        <div style={right(48, 221, 303, 11)}>{itemGuid1}</div>

        {/* itemGuid2 CRect(48,233,351,244) DT_RIGHT */}
        <div style={right(48, 233, 303, 11)}>{itemGuid2}</div>

        {/* msg3 必要龍珠数 CRect(260,261,346,272) DT_RIGHT */}
        <div style={right(260, 261, 86, 11)}>{msg3}</div>

        {/* msg4 必要麻雀コイン CRect(260,291,346,302) DT_RIGHT */}
        <div style={right(260, 291, 86, 11)}>{msg4}</div>

        {/* msg5 保有龍珠数 CRect(260,325,346,336) DT_RIGHT */}
        <div style={right(260, 325, 86, 11)}>{msg5}</div>

        {/* msg6 保有麻雀コイン CRect(260,355,346,366) DT_RIGHT */}
        <div style={right(260, 355, 86, 11)}>{msg6}</div>

        {/* msg7 利用可能期間 CRect(144,383,351,394) DT_RIGHT */}
        <div style={right(144, 383, 207, 11)}>{msg7}</div>

        {/* msg8 注意1 CRect(45,406,354,417) DT_LEFT */}
        <div style={left(45, 406, 309, 11)}>{msg8}</div>

        {/* msg9 注意2 CRect(45,418,354,429) DT_LEFT */}
        <div style={left(45, 418, 309, 11)}>{msg9}</div>

        {/* msg10 注意3 CRect(45,430,354,441) DT_LEFT */}
        {msg10 && <div style={left(45, 430, 309, 11)}>{msg10}</div>}

        {/* ================================================================
            確認OKボタン: mj_shp_btn_confirmation.png (340×29, 4フレーム 85×29)
            m_btnClose.Create(0, ..., 152, 455, ..., IDOK)
            ================================================================ */}
        <SpriteButton
          src={`${IMG}/mj_shp_btn_confirmation.png`}
          frameW={85} frameH={29}
          x={152} y={455}
          onClick={onClose}
          title="確認"
        />
      </div>
      </div>
    </div>
  )
}
