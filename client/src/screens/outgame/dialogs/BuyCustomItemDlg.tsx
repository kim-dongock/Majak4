/**
 * CMJBuyCustomItemDlg 相当 — カスタムアイテム購入確認 (AP-09 §3-2-6)
 * レガシー: legacy/client/HgMajak2/MJBuyCustomItemDlg.h/cpp
 *
 * ウィンドウ: MoveWindow(0,0,389,500) → 389×500px, CenterWindow(GetParent())
 * OnNcHitTest: pt.y < 41 → HTCAPTION (ドラッグ移動可)
 *
 * ── 画像配置 (OnCreate / OnPaint より — 座標すべてレガシー準拠) ─────────────
 *
 * 背景 (1フレーム 389×500):
 *   mj_shp_window_exchange_05.png  at (0, 0)
 *
 * アイテム画像 (1フレーム 約130×130):
 *   items/custom/mj_custom_{itemId:02d}.png  at (129, 73)
 *
 * Yes ボタン (4フレーム 85×29):
 *   mj_shp_btn_yes.png  at (99, 458)  IDOK
 *
 * No ボタン (4フレーム 85×29):
 *   mj_shp_btn_no.png   at (206, 458)  IDCANCEL
 *
 * 「GEMで購入」ボタン (4フレーム 123×20):
 *   _ShopReceiptBuyBtn.png  at (235, 377)  IDC_BTN_HANCOINBUY
 *   → 追加購入ページを外部ブラウザで開く
 *
 * ── テキスト (OnPaint — 12px bold MS ゴシック DT_*) ──────────────────────
 *   タイトル        CRect(56,53,334,64)    DT_CENTER
 *   アイテム名      CRect(167,154,351,180) DT_RIGHT
 *   アイテムタイプ  CRect(167,193,351,204) DT_RIGHT
 *   アイテム説明    CRect(150,216,351,259) DT_WORDBREAK
 *   価格            CRect(167,271,351,281) DT_RIGHT
 *   保有者名        CRect(57,305,333,316)  DT_RIGHT
 *   GEM残高  CRect(167,328,351,339) DT_RIGHT
 *   クーポン残高    CRect(167,351,351,362) DT_RIGHT
 *
 * OnOK():
 *   ProcessCommandBuyCustomItem(shopNo) → mjkc41e (commandBuyCustomItem) 送信
 *   Key.CustomId = "mjkk138e", Key.ShopNo = "mjkk139e"
 * ────────────────────────────────────────────────────────────────────────
 */
import { useRef, useEffect, useState } from 'react'
import * as SignalR from '../../../api/signalr'
import { showError } from '../../../utils/msgbox'
import CustomReceiptDlg from './CustomReceiptDlg'
import { useOutgameLayoutMode } from '../../../hooks/useOutgameLayoutMode'

const IMG      = '/assets/images/game'
const IMG_ITEM = '/assets/images/game/items/custom'
const SHOP_RECEIPT_BUY_BTN = `${IMG}/_ShopReceiptBuyBtn.png?v=opaque`
const DIALOG_W = 389
const DIALOG_H = 500

/** エラーコード (CUSTOM_ERROR_CODE_*) */
const ERROR_CODE: Record<number, string> = {
  1:  'GEMが足りません',
  2:  '既に所持しているアイテムです',
  11: 'IDが不正です',
  12: '接続エラー',
  13: '不明なエラー',
}

export interface CustomShopItem {
  itemId:   number
  itemName: string
  itemType: string
  itemDesc: string
  /** 価格 (円) */
  price:    number
  shopNo:   number
  gameMoney: number
}

interface Props {
  item:       CustomShopItem
  pix:        string
  memberName?: string
  hanCoin:    number
  hanCoupon:  number
  onClose:    () => void
  onBuyOK?:   () => void
}

/** ====================================================================
 * CMJBmpButton 相当 — AP-06 §2 4フレームスプライトボタン
 * ==================================================================== */
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
      onMouseLeave={() => setFi(disabled ? 1 : 0)}
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

/** ====================================================================
 * CMJBuyCustomItemDlg 本体
 * ==================================================================== */
export default function BuyCustomItemDlg({
  item, pix, memberName, hanCoin, hanCoupon, onClose, onBuyOK,
}: Props) {
  const [yesDis, setYesDis] = useState(false)
  const [coinBalance] = useState(hanCoin)
  const [couponBalance] = useState(hanCoupon)
  const layoutMode = useOutgameLayoutMode()
  const isMobile = layoutMode !== 'desktop'
  const [dialogScale, setDialogScale] = useState(1)
  const [receipt, setReceipt] = useState<{
    coinBefore: number
    couponBefore: number
    coinAfter: number
    couponAfter: number
  } | null>(null)

  /** mjkc42e レスポンスハンドラを useEffect で登録 */
  const pendingRef = useRef<((data: Record<string, unknown>) => void) | null>(null)
  useEffect(() => {
    const handler = (data: Record<string, unknown>) => {
      pendingRef.current?.(data)
      pendingRef.current = null
    }
    SignalR.on('mjkc42e', handler)
    return () => {
      SignalR.off('mjkc42e', handler)
      pendingRef.current = null
    }
  }, [])

  useEffect(() => {
    setYesDis(false)
  }, [])

  /* ドラッグ移動 (OnNcHitTest: pt.y < 41 → HTCAPTION) */
  const [pos, setPos]   = useState({ x: 0, y: 0 })
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

  /**
   * OnOK() 相当 — ProcessCommandBuyCustomItem(shopNo) → mjkc41e 送信
   * Key.CustomId = "mjkk138e", Key.ShopNo = "mjkk139e"
  * 応答: mjkc42e k1e(0=成功, 1=コイン不足, 2=所持済み, 11-13=エラー)
   */
  const handleYes = async () => {
    setYesDis(true)
    await new Promise<void>(resolve => {
      pendingRef.current = async (data) => {
        const resultCode = Number(data.k1e ?? -1)
        if (resultCode !== 0) {
          if (resultCode === 1) {
            handleBuy()
          } else {
            const message = String(data.k2e ?? '')
            showError(message || ERROR_CODE[resultCode] || `購入に失敗しました (code: ${resultCode})`)
          }
        } else {
          const coinBefore = coinBalance
          const couponBefore = couponBalance
          const coinAfter = coinBalance >= 0 ? Math.max(0, coinBalance - item.price) : coinBalance
          const couponAfter = couponBalance
          setReceipt({
            coinBefore,
            couponBefore,
            coinAfter,
            couponAfter,
          })
        }
        resolve()
      }
      SignalR.send('mjkc41e', {
        k3e: pix,
        'mjkk139e': String(item.shopNo),
      }).catch(() => {
        pendingRef.current = null
        showError('サーバーへの送信に失敗しました')
        resolve()
      })
    })
  }

  /** IDC_BTN_HANCOINBUY — 追加購入ページを外部で開く */
  const handleBuy = () => {
    window.open('https://coin.hangame.co.jp/', '_blank', 'noopener,noreferrer')
  }

  const textStyle = {
    fontFamily: "'MS PGothic', 'Noto Sans JP', 'Noto Sans JP', 'MS UI Gothic', sans-serif" as const,
    fontSize: 12, fontWeight: 'bold' as const,
    color: '#000', pointerEvents: 'none' as const,
    overflow: 'hidden' as const, whiteSpace: 'nowrap' as const,
  }
  const yen = (value: number) => value >= 0 ? `${Math.trunc(value)}円` : '---'

  return (
    <>
      {!receipt && (
        /* モーダルオーバーレイ */
        <div style={{
          position: isMobile ? 'fixed' : 'absolute', inset: 0,
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          background: 'transparent', zIndex: 250,
        }}>
      <div style={{ width: DIALOG_W * dialogScale, height: DIALOG_H * dialogScale }}>
      {/* CMJBuyCustomItemDlg クライアント領域: 389×500px */}
      <div
        style={{
          position: 'relative',
          width: DIALOG_W, height: DIALOG_H,
          left: isMobile ? 0 : pos.x, top: isMobile ? 0 : pos.y,
          transform: `scale(${dialogScale})`,
          transformOrigin: 'top left',
        }}
        onMouseDown={isMobile ? undefined : onDragStart}
      >

        {/* ── 背景 mj_shp_window_exchange_05.png (389×500) ── */}
        <img
          src={`${IMG}/mj_shp_window_exchange_05.png`}
          alt=""
          draggable={false}
          style={{ position: 'absolute', left: 0, top: 0, width: 389, height: 500, userSelect: 'none' }}
        />

        {/* ── アイテム画像 at (129, 73) ── */}
        <img
          src={`${IMG_ITEM}/mj_custom_${String(item.itemId).padStart(2, '0')}.png`}
          alt={item.itemName}
          draggable={false}
          style={{ position: 'absolute', left: 129, top: 73, pointerEvents: 'none' }}
          onError={e => { (e.currentTarget as HTMLImageElement).style.visibility = 'hidden' }}
        />

        {/* ── タイトル CRect(56,53,334,64) DT_CENTER ── */}
        <div style={{ ...textStyle, position: 'absolute', left: 56, top: 53, width: 278, textAlign: 'center' }}>
          &quot;{memberName || pix}&quot;さんが購入するアイテム
        </div>

        {/* ── アイテム名 CRect(167,154,351,180) DT_RIGHT ── */}
        <div style={{ ...textStyle, position: 'absolute', left: 167, top: 154, width: 184, textAlign: 'right', whiteSpace: 'normal' as const }}>
          {item.itemName}
        </div>

        {/* ── アイテムタイプ CRect(167,193,351,204) DT_RIGHT ── */}
        <div style={{ ...textStyle, position: 'absolute', left: 167, top: 193, width: 184, textAlign: 'right' }}>
          {item.itemType}
        </div>

        {/* ── アイテム説明 CRect(150,216,351,259) DT_WORDBREAK ── */}
        <div style={{
          ...textStyle,
          position: 'absolute', left: 150, top: 216, width: 201, height: 43,
          whiteSpace: 'pre-wrap' as const, overflow: 'hidden' as const,
          textAlign: 'left',
        }}>
          {item.itemDesc}
        </div>

        {/* ── 価格 CRect(167,271,351,281) DT_RIGHT ── */}
        <div style={{ ...textStyle, position: 'absolute', left: 167, top: 271, width: 184, textAlign: 'right' }}>
          {yen(item.price)}
        </div>

        {/* ── 保有者名 CRect(57,305,333,316) DT_RIGHT ── */}
        <div style={{ ...textStyle, position: 'absolute', left: 57, top: 305, width: 276, textAlign: 'right' }}>
          &quot;{memberName || pix}&quot;さんの保有状況
        </div>

        {/* ── GEM残高 CRect(167,328,351,339) DT_RIGHT ── */}
        <div style={{ ...textStyle, position: 'absolute', left: 167, top: 328, width: 184, textAlign: 'right' }}>
          {yen(coinBalance)}
        </div>

        {/* ── クーポン残高 CRect(167,351,351,362) DT_RIGHT ── */}
        <div style={{ ...textStyle, position: 'absolute', left: 167, top: 351, width: 184, textAlign: 'right' }}>
          {yen(couponBalance)}
        </div>

        {/* ── GEMで購入ボタン _ShopReceiptBuyBtn.png (492×20, 4フレーム 123×20) at (235, 377) ── */}
        <SpriteButton
          src={SHOP_RECEIPT_BUY_BTN}
          frameW={123} frameH={20}
          x={235} y={377}
          onClick={handleBuy}
          title="GEMで購入"
        />

        {/* ── Yes ボタン mj_shp_btn_yes.png (340×29, 4フレーム 85×29) at (99, 458) ── */}
        <SpriteButton
          src={`${IMG}/mj_shp_btn_yes.png`}
          frameW={85} frameH={29}
          x={99} y={458}
          onClick={handleYes}
          disabled={yesDis}
          title="購入する"
        />

        {/* ── No ボタン mj_shp_btn_no.png (340×29, 4フレーム 85×29) at (206, 458) ── */}
        <SpriteButton
          src={`${IMG}/mj_shp_btn_no.png`}
          frameW={85} frameH={29}
          x={206} y={458}
          onClick={onClose}
          title="キャンセル"
        />
        </div>
        </div>
        </div>
      )}
      {receipt && (
        <CustomReceiptDlg
          pix={pix}
          memberName={memberName}
          itemId={item.itemId}
          itemName={item.itemName}
          price={item.price}
          gameMoney={item.gameMoney}
          coinBefore={receipt.coinBefore}
          couponBefore={receipt.couponBefore}
          coinAfter={receipt.coinAfter}
          couponAfter={receipt.couponAfter}
          onClose={() => {
            onBuyOK?.()
            onClose()
          }}
        />
      )}
    </>
  )
}
