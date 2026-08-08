/**
 * BuyHanCoinItemDlg — CMJBuyItemDlg 相当のMP購入確認ダイアログ (AP-09 §3-2-4)
 * レガシー: legacy/client/HgMajak2/MJBuyItemDlg.h/cpp
 *
 * ウィンドウ: MoveWindow(0,0,390,470) → 390×470px, CenterWindow(GetParent())
 * OnNcHitTest → HTCAPTION : ドラッグ移動可能
 *
 * 画像 (OnCreate / OnPaint より — 座標すべてレガシー準拠):
 * ┌────────────────────────────────────────────┐
 * │ 背景    _ShopReceiptMain1.png  390×470  at (0,0)  ← 1フレーム単一画像 │
 * │ Exit  _ShopReceiptExitBtn.png   18×18   at (358,12) 4フレーム       │
 * │ Yes   _ShopReceiptYesBtn.png   112×20   at (73,427) 4フレーム 初期無効│
 * │ No    _ShopReceiptNoBtn.png    112×20   at (204,427) 4フレーム      │
 * │ Buy   _ShopReceiptBuyBtn.png   123×20   at (260,370) 4フレーム      │
 * └────────────────────────────────────────────┘
 *
 * テキスト (OnPaint — 13px MS Pゴシック, 透過背景):
 *   (15, 50)   m_strMessage[0]  : 購入者名
 *   (162,74)   アイテム画像      : m_pShopItemData->m_strImagePath
 *   (160,153)  アイテム名
 *   (160,174)  購入単価
 *   (25, 210〜280) m_strMessage[1〜6]: 商品説明
 *   (25, 320)  "購入アイテム数" (非抽選時のみ)
 *   (220,320)  "個"             (非抽選時のみ)
 *   (25, 334)  "購入合計 : {price}"
 *   DrawText right-align CRect(10,366,245,381): キャッシュ残高
 *   DrawText right-align CRect(10,380,245,395): 商品券残高
 *
 * 数量コンボ IDC_CMB_COUNT: MoveWindow(179,317,40,180) 抽選以外のみ表示
 *
 * OnOK  → commandBuyMajItem (mjkc20e) 相当
 * OnCancel → 閉じる (IDCANCEL)
 */
import { useRef, useEffect, useState } from 'react'
import * as SignalR from '../../../api/signalr'
import { showError, showMessage, buyMajItemErrorMessage } from '../../../utils/msgbox'
import HanCoinReceiptDlg from './HanCoinReceiptDlg'
import LotSlotDlg from './LotSlotDlg'
import { useOutgameLayoutMode } from '../../../hooks/useOutgameLayoutMode'
import ResponsiveShopTransactionDlg from './ResponsiveShopTransactionDlg'

const IMG = '/assets/images/game'
const SHOP_RECEIPT_BUY_BTN = `${IMG}/_ShopReceiptBuyBtn.png?v=opaque`
const DIALOG_W = 390
const DIALOG_H = 470

/** MP購入アイテム情報 (CMajakShopItemData 相当) */
export interface HanCoinShopItemData {
  itemCode: string
  sellCode: string
  itemName: string
  price: number
  gameMoney: number
  description: string[]  // m_strMessage[1..6]
  imageUrl?: string
  isLottery?: boolean
  lotteryCount?: number
}

interface Props {
  item: HanCoinShopItemData
  pix: string
  memberName?: string
  hanCoin: number
  onClose: () => void
  /** OnOK 後に呼ばれる購入完了コールバック */
  onBuyOK?: (cashCount: number) => void
}

/** ====================================================================
 * CMJBmpButton 相当 — AP-06 §2 4フレームスプライトボタン
 * ==================================================================== */
function SpriteButton({
  src, frameW, frameH, x, y, onClick, disabled = false, title,
}: {
  src: string
  frameW: number
  frameH: number
  x: number
  y: number
  onClick: () => void
  disabled?: boolean
  title?: string
}) {
  const [fi, setFi] = useState(disabled ? 1 : 0)

  /* disabled 状態変化を追跡 */
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
        position: 'absolute',
        left: x,
        top: y,
        width: frameW,
        height: frameH,
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-fi * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        border: 'none',
        padding: 0,
        cursor: disabled ? 'not-allowed' : 'pointer',
        outline: 'none',
        imageRendering: 'pixelated',
      }}
    />
  )
}

/** ====================================================================
 * BuyHanCoinItemDlg 本体
 * ==================================================================== */
export default function BuyHanCoinItemDlg({ item, pix, memberName, hanCoin, onClose, onBuyOK }: Props) {
  /**
   * m_btnReceiptYes は初期 WS_DISABLED — 残高照会が完了してから有効化
   * (InquiryFinished 相当 : コンポーネントマウント後 500ms で有効化)
   */
  const [yesEnabled, setYesEnabled] = useState(false)
  const [count, setCount]           = useState(1)
  const [receipt, setReceipt]       = useState<{
    count: number
    coinBefore: number
    coinAfter: number
  } | null>(null)
  const [showLotSlot, setShowLotSlot] = useState(false)
  const [coinBalance] = useState(hanCoin)
  const layoutMode = useOutgameLayoutMode()
  const isMobile = layoutMode !== 'desktop'
  const useResponsiveDialog = isMobile || layoutMode === 'desktop'
  const [dialogScale, setDialogScale] = useState(1)

  /**
   * mjkc20e レスポンスハンドラを useEffect で登録 — コンポーネント寿命に紐付け
   * (アンマウント時も必ず off される)
   */
  const pendingRef = useRef<((data: Record<string, unknown>) => void) | null>(null)
  useEffect(() => {
    const handler = (data: Record<string, unknown>) => {
      pendingRef.current?.(data)
      pendingRef.current = null
    }
    SignalR.on('mjkc20e', handler)
    return () => {
      SignalR.off('mjkc20e', handler)
      pendingRef.current = null
    }
  }, [])

  useEffect(() => {
    setYesEnabled(true)
  }, [])

  /* ドラッグ移動 (OnNcHitTest → HTCAPTION 相当) */
  const [pos, setPos]   = useState({ x: 0, y: 0 })
  const dragging        = useRef(false)
  const dragOffset      = useRef({ dx: 0, dy: 0 })

  const onDragStart = (e: React.MouseEvent) => {
    if (e.nativeEvent.offsetY >= 41) return
    dragging.current  = true
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

  const totalPrice = item.price * count
  /**
   * OnOK() — 購入実行 (commandBuyMajItem = mjkc20e)
   * レガシー ProcessBuyItemCommand:
   *   failCode != 0 → m_pDlgBuyItem->ErrorBuyItemCommand(message) でエラー表示
   *   success      → onBuyOK コールバック
   *
   * 応答キー:
   *   result    = 0 (成功/失敗ともに 0; 原典: 0=OK)
   *   failCode  = N (N!=0 なら失敗, Key.FailCode = "mjkk95e")
   *   "mjkk55e" = gemCount
   *   "k34e"    = gamMoney
   */
  const handleYes = async () => {
    if (!yesEnabled) return
    setYesEnabled(false)

    await new Promise<void>(resolve => {
      pendingRef.current = async (data) => {
        const failCode = String(data.failCode ?? data['mjkk95e'] ?? '')
        if (failCode !== '') {
          const message = String(data.k2e ?? data.message ?? '')
          showError(message || buyMajItemErrorMessage(failCode))
          onClose()
        } else {
          const coinAfter = Number(data.cashCount ?? (coinBalance >= 0 ? Math.max(0, coinBalance - totalPrice) : coinBalance))
          if (item.isLottery && typeof item.lotteryCount === 'number' && item.lotteryCount > 0) {
            setShowLotSlot(true)
          } else {
            setReceipt({ count, coinBefore: coinBalance, coinAfter })
          }
        }
        resolve()
      }
      // Key.SellCode = "mjkk57e"
      SignalR.send('mjkc20e', { 'mjkk57e': item.sellCode, count: String(count) }).catch(() => {
        pendingRef.current = null
        showError('サーバーへの送信に失敗しました')
        setYesEnabled(true)
        resolve()
      })
    })
  }

  /**
  * OnBtnReceiptBuy() — MP追加購入
  * 決済機能を接続するまでは準備中メッセージを表示する。
   */
  const handleBuy = () => {
    void showMessage('MP購入機能は準備中です。')
  }

  /**
   * OnCancel() — 閉じる
   */
  const handleNo = () => onClose()

  /* テキストスタイル (OnPaint: 13px MS Pゴシック, 透過背景) */
  const txt = (color = '#000') => ({
    position: 'absolute' as const,
    fontFamily: 'var(--majak-font-family-ui)' as const,
    fontSize: 'calc(13px * var(--majak-type-scale))',
    color,
    whiteSpace: 'nowrap' as const,
    pointerEvents: 'none' as const,
  })

  const yen = (n: number) => n < 0 ? '---' : `${Math.trunc(n).toLocaleString('ja-JP')} MP`
  const moneyString = (value: number) => `${Math.trunc(value).toLocaleString('ja-JP')} GP`

  if (useResponsiveDialog) {
    if (receipt) {
      return <ResponsiveShopTransactionDlg
        title="購入完了"
        itemName={item.itemName}
        description={item.description}
        imageUrl={item.imageUrl}
        costs={[{ label: '購入合計', value: yen(totalPrice) }]}
        balances={[{ label: '購入後のMP', value: yen(receipt.coinAfter) }]}
        complete
        onCancel={() => { onBuyOK?.(receipt.coinAfter); onClose() }}
      />
    }
    return <ResponsiveShopTransactionDlg
      title="購入しますか？"
      itemName={item.itemName}
      itemKind="便利アイテム"
      description={item.description}
      imageUrl={item.imageUrl}
      costs={[{ label: '単価', value: yen(item.price) }, { label: '購入合計', value: yen(totalPrice) }]}
      balances={[{ label: '所持MP', value: yen(coinBalance) }, { label: '購入後のMP', value: yen(Math.max(0, coinBalance - totalPrice)) }]}
      quantity={item.isLottery ? undefined : count}
      onQuantityChange={setCount}
      confirmDisabled={!yesEnabled}
      onConfirm={handleYes}
      onCancel={handleNo}
    >
      {showLotSlot && item.isLottery && typeof item.lotteryCount === 'number' && item.lotteryCount > 0 && <LotSlotDlg
        itemName={item.itemName}
        lotteryCount={item.lotteryCount}
        totalAmount={item.gameMoney}
        imageUrl={item.imageUrl}
        onResult={() => { onBuyOK?.(coinBalance); onClose() }}
        onClose={() => { setShowLotSlot(false); setYesEnabled(true) }}
      />}
    </ResponsiveShopTransactionDlg>
  }

  return (
    <>
      {!receipt && (
        /* モーダルオーバーレイ (CenterWindow(GetParent()) 相当: flex center) */
        <div
          style={{
            position: isMobile ? 'fixed' : 'absolute',
            inset: 0,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            background: 'transparent',
            zIndex: 400,
          }}
        >
        <div style={{ width: DIALOG_W * dialogScale, height: DIALOG_H * dialogScale }}>
        {/* CMJBuyItemDlg クライアント領域: 390×470px */}
        <div
          style={{
            position: 'relative',
            width: DIALOG_W,
            height: DIALOG_H,
            left: isMobile ? 0 : pos.x,
            top: isMobile ? 0 : pos.y,
            transform: `scale(${dialogScale})`,
            transformOrigin: 'top left',
          }}
        >
        {/* ================================================================
            背景: _ShopReceiptMain1.png (390×470) at (0,0)
            Create(..., 1, ...) = 1フレーム単一画像 → <img> で全体を表示
            ================================================================ */}
        <img
          src={`${IMG}/_ShopReceiptMain1.png`}
          alt=""
          draggable={false}
          onMouseDown={onDragStart}   /* OnNcHitTest HTCAPTION */
          style={{
            position: 'absolute',
            left: 0,
            top: 0,
            width: 390,
            height: 470,
            userSelect: 'none',
          }}
        />

        {/* ================================================================
            Exit ✕ ボタン: _ShopReceiptExitBtn.png (72×18, 4フレーム 18×18)
            m_btnExit.Create(0, ..., 358, 12, ..., IDCANCEL)
            ================================================================ */}
        <SpriteButton
          src={`${IMG}/_ShopReceiptExitBtn.png`}
          frameW={18} frameH={18}
          x={358} y={12}
          onClick={handleNo}
          title="閉じる"
        />

        {/* ================================================================
            テキスト (OnPaint — 13px MS Pゴシック, 透過背景)
            ================================================================ */}

        {/* (15,50) m_strMessage[0]: 購入者名 */}
        <span style={{ ...txt(), left: 15, top: 50 }}>
          &quot;{memberName || pix}&quot;さんが購入するアイテム
        </span>

        {/* (162,74) アイテム画像 m_pItemImage->Draw(&dc, 162, 74, 0) */}
        {item.imageUrl && (
          <img
            src={item.imageUrl}
            alt={item.itemName}
            draggable={false}
            style={{
              position: 'absolute',
              left: 162,
              top: 74,
              pointerEvents: 'none',
            }}
          />
        )}

        {item.isLottery && typeof item.lotteryCount === 'number' && item.lotteryCount > 0 && (
          <>
            <div
              style={{
                ...txt('#000'),
                left: 191,
                top: 125,
                width: 34,
                height: 12,
                textAlign: 'right',
                fontWeight: 'bold',
              }}
            >
              {item.lotteryCount}回
            </div>
            <div
              style={{
                ...txt('#fff'),
                left: 190,
                top: 124,
                width: 34,
                height: 12,
                textAlign: 'right',
                fontWeight: 'bold',
              }}
            >
              {item.lotteryCount}回
            </div>
          </>
        )}

        {/* (160,153) アイテム名 */}
        <span style={{ ...txt(), left: 160, top: 153 }}>{item.itemName}</span>

        {/* (160,174) 購入単価 */}
        <span style={{ ...txt(), left: 160, top: 174 }}>{moneyString(item.price)}</span>

        {/* (25,210〜280) m_strMessage[1..6]: 商品説明
            [1][2][3] → 黒, [4][5][6] → RGB(40,160,100) 緑 */}
        {item.description.slice(0, 3).map((line, i) => (
          <span key={i} style={{ ...txt('#000'), left: 25, top: 210 + i * 14 }}>{line}</span>
        ))}
        {item.description.slice(3, 6).map((line, i) => (
          <span key={i + 3} style={{ ...txt('rgb(40,160,100)'), left: 25, top: 252 + i * 14 }}>{line}</span>
        ))}

        {/* 数量: "購入アイテム数" (25,320) / "個" (220,320) ← 非抽選のみ
            IDC_CMB_COUNT: MoveWindow(179,317, 40, 180) */}
        {!item.isLottery && (
          <>
            <span style={{ ...txt(), left: 25, top: 320 }}>購入アイテム数</span>
            <select
              value={count}
              onChange={e => setCount(Number(e.target.value))}
              disabled={!yesEnabled}
              style={{
                position: 'absolute',
                left: 179,
                top: 317,
                width: 40,
                height: 22,
                fontFamily: 'var(--majak-font-family-ui)',
                fontSize: 'calc(12px * var(--majak-type-scale))',
                border: '1px solid #888',
              }}
            >
              {[1, 2, 3, 5, 10].map(v => (
                <option key={v} value={v}>{v}</option>
              ))}
            </select>
            <span style={{ ...txt(), left: 220, top: 320 }}>個</span>
          </>
        )}

        {/* (25,334) 購入合計 */}
        <span style={{ ...txt(), left: 25, top: 334 }}>
          購入合計 : {moneyString(totalPrice)}
        </span>

          {/* DrawText right-align CRect(10,366,245,381): キャッシュ残高 */}
        <div
          style={{
            position: 'absolute',
            left: 10, top: 366,
            width: 235, height: 15,
            fontFamily: 'var(--majak-font-family-ui)',
            fontSize: 'calc(13px * var(--majak-type-scale))',
            color: '#000',
            textAlign: 'right',
            pointerEvents: 'none',
          }}
        >
          現在&quot;{memberName || pix}&quot;さんのキャッシュ : {coinBalance.toLocaleString('ja-JP')} MP
        </div>

        {/* ================================================================
            購入ボタン: _ShopReceiptBuyBtn.png (492×20, 4フレーム 123×20)
            m_btnReceiptBuy.Create(0, ..., 260, 370, ..., IDC_BTN_HANCOINBUY)
            ================================================================ */}
        <SpriteButton
          src={SHOP_RECEIPT_BUY_BTN}
          frameW={123} frameH={20}
          x={260} y={370}
          onClick={handleBuy}
            title="MP購入"
        />

        {/* ================================================================
            はい (Yes) ボタン: _ShopReceiptYesBtn.png (448×20, 4フレーム 112×20)
            m_btnReceiptYes.Create(WS_DISABLED, ..., 73, 427, ..., IDOK)
            初期は WS_DISABLED → 残高照会完了後に有効化
            ================================================================ */}
        <SpriteButton
          src={`${IMG}/_ShopReceiptYesBtn.png`}
          frameW={112} frameH={20}
          x={73} y={427}
          onClick={handleYes}
          disabled={!yesEnabled}
          title="はい (購入)"
        />

        {/* ================================================================
            いいえ (No) ボタン: _ShopReceiptNoBtn.png (448×20, 4フレーム 112×20)
            m_btnReceiptNo.Create(0, ..., 204, 427, ..., IDCANCEL)
            ================================================================ */}
        <SpriteButton
          src={`${IMG}/_ShopReceiptNoBtn.png`}
          frameW={112} frameH={20}
          x={204} y={427}
          onClick={handleNo}
          title="いいえ (キャンセル)"
        />

        {showLotSlot && item.isLottery && typeof item.lotteryCount === 'number' && item.lotteryCount > 0 && (
          <LotSlotDlg
            itemName={item.itemName}
            lotteryCount={item.lotteryCount}
            totalAmount={item.gameMoney}
            imageUrl={item.imageUrl}
            onResult={() => {
              onBuyOK?.(coinBalance)
              onClose()
            }}
            onClose={() => {
              setShowLotSlot(false)
              setYesEnabled(true)
            }}
          />
        )}
        </div>
        </div>
        </div>
      )}
      {receipt && (
        <HanCoinReceiptDlg
          pix={pix}
          memberName={memberName}
          itemName={item.itemName}
          sellCode={item.sellCode}
          price={item.price}
          count={receipt.count}
          coinBefore={receipt.coinBefore}
          coinAfter={receipt.coinAfter}
          gameMoney={item.gameMoney}
          imageUrl={item.imageUrl}
          onClose={() => {
            onBuyOK?.(receipt.coinAfter)
            onClose()
          }}
        />
      )}
    </>
  )
}
