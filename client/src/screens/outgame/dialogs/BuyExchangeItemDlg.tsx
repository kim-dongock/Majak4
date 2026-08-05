/**
 * BuyExchangeItemDlg — CMJBuyItemDlg2 相当の龍宝石/麻雀コイン交換確認 (AP-09 §3-2-5)
 * レガシー: legacy/client/HgMajak2/MJBuyItemDlg2.h/cpp
 *
 * ウィンドウ: MoveWindow(0,0,389,500) → 389×500px, CenterWindow(GetParent())
 * OnNcHitTest: pt.y < 41 → HTCAPTION (ドラッグ移動可)
 *
 * ── 画像配置 (OnCreate / OnPaint より — 座標すべてレガシー準拠) ─────────────
 *
 * 背景 (1フレーム 389×500):
 *   mj_shp_window_exchange_03.png  at (0, 0)
 *   Draw(&dc, 0, 0, 0) ← 1フレーム単一画像
 *   ※ラベル ("アイテム名" / "アイテム種類" / "必要龍珠" 等) は背景に焼き込み済み
 *
 * はい (Yes) ボタン (4フレーム 85×29):
 *   mj_shp_btn_yes.png  at (99, 455)  IDOK
 *
 * いいえ (No) ボタン (4フレーム 85×29):
 *   mj_shp_btn_no.png   at (206, 455)  IDCANCEL
 *
 * アイテム画像 (1フレーム, 約130×73):
 *   m_pItemImage->Draw(&dc, 162, 77, 0)
 *
 * ── テキスト (OnPaint — 12px bold MS Pゴシック, 透過背景) ─────────────────
 *   m_strMessage[0]        CRect(56,53,334,64)   DT_CENTER  購入者名
 *   m_strItemName          CRect(144,155,351,166) DT_RIGHT   アイテム名
 *   m_strItemKind          CRect(144,178,351,189) DT_RIGHT   アイテム種類
 *   m_strItemGuid1         CRect(48,221,351,232)  DT_RIGHT   説明1
 *   m_strItemGuid2         CRect(48,233,351,244)  DT_RIGHT   説明2
 *   m_strMessage[1] (gem)  CRect(225,256,351,267) DT_RIGHT   必要龍珠数
 *   m_strCostMoney         CRect(225,279,351,290) DT_RIGHT   必要麻雀コイン
 *   m_strMessage[2] (期間) CRect(144,303,351,314) DT_RIGHT   利用可能期間
 *   m_strMessage[3]        CRect(56,338,334,349)  DT_CENTER  (背景に焼き込み済)
 *   m_strMessage[4] (gem)  CRect(225,361,351,372) DT_RIGHT   保有龍珠数
 *   m_strUserMoney         CRect(225,384,351,395) DT_RIGHT   保有麻雀コイン
 *
 * OnOK(): 残高チェック → SendBuyItem(sellCode) 送信 → Yes ボタン無効化
 * OnCancel(): 閉じる
 * ────────────────────────────────────────────────────────────────────────
 */
import { useRef, useEffect, useState } from 'react'
import * as SignalR from '../../../api/signalr'
import { showError, buyMajItemErrorMessage } from '../../../utils/msgbox'
import ExchangeItemReceiptDlg from './ExchangeItemReceiptDlg'
import { useOutgameLayoutMode } from '../../../hooks/useOutgameLayoutMode'
import ResponsiveShopTransactionDlg from './ResponsiveShopTransactionDlg'

const IMG = '/assets/images/game'
const DIALOG_W = 389
const DIALOG_H = 500

/** 龍宝石/麻雀コイン交換アイテム情報 (CMajakShopItemData2 相当) */
export interface ExchangeShopItemData {
  sellCode: string
  itemName: string
  itemKind: string
  itemGuid1: string    // 説明1行目
  itemGuid2: string    // 説明2行目
  costGem: number      // 必要龍珠 m_nCostGem
  costMoney: number    // 必要麻雀コイン m_llGameMoney (円)
  limitDays: number    // 利用可能期間 (日), -1=永久
  quantity: number     // 個数
  imageUrl?: string
}

interface Props {
  item: ExchangeShopItemData
  pix: string
  memberName?: string
  userGem: number      // 保有龍珠数
  userMoney: number    // 保有麻雀コイン (円)
  onClose: () => void
  onBuyOK?: (balances: { userGem: number; userMoney: number }) => void
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
 * BuyExchangeItemDlg 本体
 * ==================================================================== */
export default function BuyExchangeItemDlg({
  item, pix, memberName, userGem, userMoney, onClose, onBuyOK,
}: Props) {
  const [yesDis, setYesDis] = useState(false)
  const [receipt, setReceipt] = useState<{ userGem: number; userMoney: number } | null>(null)
  const layoutMode = useOutgameLayoutMode()
  const isMobile = layoutMode !== 'desktop'
  const useResponsiveDialog = isMobile || layoutMode === 'desktop'
  const [dialogScale, setDialogScale] = useState(1)

  /** mjkc20e レスポンスハンドラを useEffect で登録 */
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
   * OnOK() 相当:
   *   残高チェック → NG なら警告
   *   OK なら commandBuyMajItem (mjkc20e) 送信 → 応答で結果確認
   */
  const handleYes = async () => {
    if (userGem < item.costGem || userMoney < item.costMoney) {
      showError('龍珠またはGPが足りないため交換できません。')
      return
    }
    setYesDis(true)

    let closeAfterResponse = true
    await new Promise<void>(resolve => {
      pendingRef.current = (data) => {
        const failCode = String(data.failCode ?? data['mjkk95e'] ?? '')
        const result = data.k1e ?? data.result
        const isFailure = result === 'v2e' || failCode !== ''
        if (isFailure) {
          const message = String(data.k2e ?? data.message ?? '')
          showError(message || buyMajItemErrorMessage(failCode))
        } else {
          const nextGem = Number(data['mjkk55e'] ?? data.gemcount ?? userGem - item.costGem)
          const nextMoney = Number(data.k34e ?? data.gammoney ?? userMoney - item.costMoney)
          onBuyOK?.({ userGem: nextGem, userMoney: nextMoney })
          closeAfterResponse = false
          setReceipt({
            userGem: nextGem,
            userMoney: nextMoney,
          })
        }
        resolve()
      }
      // Key.SellCode = "mjkk57e"
      SignalR.send('mjkc20e', { 'mjkk57e': item.sellCode }).catch(() => {
        pendingRef.current = null
        showError('サーバーへの送信に失敗しました')
        setYesDis(false)
        resolve()
      })
    })

    if (closeAfterResponse) onClose()
  }

  const makeMoneyStr = (n: number) => `${Math.trunc(n).toLocaleString('ja-JP')} GP`
  const gemStr       = (n: number) => `${Math.trunc(n).toLocaleString('ja-JP')}個`

  /* 利用可能期間テキスト (setItemInfo 相当) */
  const periodStr = item.limitDays < 0
    ? (item.quantity <= 0 ? '永久' : `${item.quantity}回`)
    : `${item.limitDays}日間`

  /* テキストスタイル (OnPaint: 12px bold MS Pゴシック, 透過背景) — 将来の拡張用 */
  // const t = (color = '#000') => ({...})

  const right = (left: number, top: number, width: number, height: number) => ({
    position: 'absolute' as const,
    left, top, width, height,
    fontFamily: 'var(--majak-font-family-ui)' as const,
    fontSize: 'calc(12px * var(--majak-type-scale))', fontWeight: 'bold' as const,
    color: '#000', textAlign: 'right' as const,
    pointerEvents: 'none' as const,
    overflow: 'hidden' as const, whiteSpace: 'nowrap' as const,
  })
  const center = (left: number, top: number, width: number, height: number) => ({
    position: 'absolute' as const,
    left, top, width, height,
    fontFamily: 'var(--majak-font-family-ui)' as const,
    fontSize: 'calc(12px * var(--majak-type-scale))', fontWeight: 'bold' as const,
    color: '#000', textAlign: 'center' as const,
    pointerEvents: 'none' as const,
    overflow: 'hidden' as const, whiteSpace: 'nowrap' as const,
  })

  if (useResponsiveDialog) {
    const period = item.limitDays < 0 ? (item.quantity <= 0 ? '永久' : `${item.quantity}回`) : `${item.limitDays}日間`
    if (receipt) {
      return <ResponsiveShopTransactionDlg
        title="交換完了"
        itemName={item.itemName}
        itemKind={item.itemKind}
        description={[item.itemGuid1, item.itemGuid2].filter(Boolean)}
        imageUrl={item.imageUrl}
        costs={[{ label: '使用した龍珠', value: gemStr(item.costGem) }, { label: '使用したGP', value: makeMoneyStr(item.costMoney) }, { label: '利用期間', value: period }]}
        balances={[{ label: '残り龍珠', value: gemStr(receipt.userGem) }, { label: '残りGP', value: makeMoneyStr(receipt.userMoney) }]}
        complete
        onCancel={onClose}
      />
    }
    return <ResponsiveShopTransactionDlg
      title="交換しますか？"
      itemName={item.itemName}
      itemKind={item.itemKind}
      description={[item.itemGuid1, item.itemGuid2].filter(Boolean)}
      imageUrl={item.imageUrl}
      costs={[{ label: '必要な龍珠', value: gemStr(item.costGem) }, { label: '必要なGP', value: makeMoneyStr(item.costMoney) }, { label: '利用期間', value: period }]}
      balances={[{ label: '所持龍珠', value: gemStr(userGem) }, { label: '所持GP', value: makeMoneyStr(userMoney) }]}
      confirmLabel="交換する"
      confirmDisabled={yesDis}
      onConfirm={handleYes}
      onCancel={onClose}
    />
  }

  return (
    <>
      {!receipt && (
        /* モーダルオーバーレイ */
        <div style={{
          position: isMobile ? 'fixed' : 'absolute', inset: 0,
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          background: 'transparent', zIndex: 400,
        }}>
      <div style={{ width: DIALOG_W * dialogScale, height: DIALOG_H * dialogScale }}>
      {/* BuyExchangeItemDlg クライアント領域: 389×500px */}
      <div style={{
        position: 'relative',
        width: DIALOG_W, height: DIALOG_H,
        left: isMobile ? 0 : pos.x, top: isMobile ? 0 : pos.y,
        transform: `scale(${dialogScale})`,
        transformOrigin: 'top left',
      }}>

        {/* ================================================================
            背景: mj_shp_window_exchange_03.png (389×500) at (0,0)
            Create(..., 1, ...) = 1フレーム単一画像
            タイトル "交換内容確認" / ラベル群は背景に焼き込み済み
            ================================================================ */}
        <img
          src={`${IMG}/mj_shp_window_exchange_03.png`}
          alt=""
          draggable={false}
          onMouseDown={onDragStart}
          style={{
            position: 'absolute', left: 0, top: 0,
            width: 389, height: 500,
            userSelect: 'none',
          }}
        />

        {/* ================================================================
            アイテム画像 m_pItemImage->Draw(&dc, 162, 77, 0)
            1フレーム単一画像
            ================================================================ */}
        {item.imageUrl && (
          <img
            src={item.imageUrl}
            alt={item.itemName}
            draggable={false}
            style={{
              position: 'absolute', left: 162, top: 77,
              pointerEvents: 'none',
            }}
            onError={e => { (e.currentTarget as HTMLImageElement).style.visibility = 'hidden' }}
          />
        )}

        {/* ================================================================
            テキスト (OnPaint)
            ================================================================ */}

        {/* m_strMessage[0]: 購入者名 CRect(56,53,334,64) DT_CENTER */}
        <div style={center(56, 53, 278, 11)}>
          &quot;{memberName || pix}&quot;さんが交換するアイテム
        </div>

        {/* アイテム名 CRect(144,155,351,166) DT_RIGHT */}
        <div style={right(144, 155, 207, 11)}>{item.itemName}</div>

        {/* アイテム種類 CRect(144,178,351,189) DT_RIGHT */}
        <div style={right(144, 178, 207, 11)}>{item.itemKind}</div>

        {/* m_strItemGuid1: 説明1 CRect(48,221,351,232) DT_RIGHT */}
        <div style={right(48, 221, 303, 11)}>{item.itemGuid1}</div>

        {/* m_strItemGuid2: 説明2 CRect(48,233,351,244) DT_RIGHT */}
        <div style={right(48, 233, 303, 11)}>{item.itemGuid2}</div>

        {/* m_strMessage[1]: 必要龍珠数 CRect(225,256,351,267) DT_RIGHT */}
        <div style={right(225, 256, 126, 11)}>{gemStr(item.costGem)}</div>

        {/* m_strCostMoney: 必要麻雀コイン CRect(225,279,351,290) DT_RIGHT */}
        <div style={right(225, 279, 126, 11)}>{makeMoneyStr(item.costMoney)}</div>

        {/* m_strMessage[2]: 利用可能期間 CRect(144,303,351,314) DT_RIGHT */}
        <div style={right(144, 303, 207, 11)}>{periodStr}</div>

        {/* m_strMessage[4]: 保有龍珠数 CRect(225,361,351,372) DT_RIGHT */}
        <div style={right(225, 361, 126, 11)}>{gemStr(userGem)}</div>

        {/* m_strMessage[3]: 保有状況 CRect(56,338,334,349) DT_CENTER */}
        <div style={center(56, 338, 278, 11)}>
          &quot;{memberName || pix}&quot;さんの保有状況
        </div>

        {/* m_strUserMoney: 保有麻雀コイン CRect(225,384,351,395) DT_RIGHT */}
        <div style={right(225, 384, 126, 11)}>{makeMoneyStr(userMoney)}</div>

        {/* ================================================================
            はい (Yes) ボタン: mj_shp_btn_yes.png (340×29, 4フレーム 85×29)
            m_btnReceiptYes.Create(0, ..., 99, 455, ..., IDOK)
            ================================================================ */}
        <SpriteButton
          src={`${IMG}/mj_shp_btn_yes.png`}
          frameW={85} frameH={29}
          x={99} y={455}
          onClick={handleYes}
          disabled={yesDis}
          title="はい"
        />

        {/* ================================================================
            いいえ (No) ボタン: mj_shp_btn_no.png (340×29, 4フレーム 85×29)
            m_btnReceiptNo.Create(0, ..., 206, 455, ..., IDCANCEL)
            ================================================================ */}
        <SpriteButton
          src={`${IMG}/mj_shp_btn_no.png`}
          frameW={85} frameH={29}
          x={206} y={455}
          onClick={onClose}
          title="いいえ"
        />
      </div>
        </div>
        </div>
      )}
      {receipt && (
        <ExchangeItemReceiptDlg
          pix={pix}
          memberName={memberName}
          itemName={item.itemName}
          itemKind={item.itemKind}
          itemGuid1={item.itemGuid1}
          itemGuid2={item.itemGuid2}
          costGem={item.costGem}
          costMoney={item.costMoney}
          userGem={receipt.userGem}
          userMoney={receipt.userMoney}
          limitDays={item.limitDays}
          quantity={item.quantity}
          imageUrl={item.imageUrl}
          onClose={onClose}
        />
      )}
    </>
  )
}
