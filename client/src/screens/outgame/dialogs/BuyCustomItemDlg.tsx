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
 * 「キャッシュで購入」ボタン (4フレーム 123×20):
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
 *   キャッシュ残高  CRect(167,328,351,339) DT_RIGHT
 *   クーポン残高    CRect(167,351,351,362) DT_RIGHT
 *
 * OnOK():
 *   ProcessCommandBuyCustomItem(shopNo) → mjkc41e (commandBuyCustomItem) 送信
 *   Key.CustomId = "mjkk138e", Key.ShopNo = "mjkk139e"
 * ────────────────────────────────────────────────────────────────────────
 */
import { useRef, useEffect, useState } from 'react'
import * as SignalR from '../../../api/signalr'
import { showError, showMessage } from '../../../utils/msgbox'
import ResponsiveShopTransactionDlg from './ResponsiveShopTransactionDlg'

const IMG_ITEM = '/assets/images/game/items/custom'

/** エラーコード (CUSTOM_ERROR_CODE_*) */
const ERROR_CODE: Record<number, string> = {
  1:  'MPが足りません',
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
  /** 価格 (MP) */
  price:    number
  shopNo:   number
  gameMoney: number
}

interface Props {
  item:       CustomShopItem
  pix:        string
  memberName?: string
  hanCoin:    number
  onClose:    () => void
  onBuyOK?:   (cashCount: number) => void
}

/** ====================================================================
 * CMJBuyCustomItemDlg 本体
 * ==================================================================== */
export default function BuyCustomItemDlg({
  item, pix, hanCoin, onClose, onBuyOK,
}: Props) {
  const [yesDis, setYesDis] = useState(false)
  const [coinBalance] = useState(hanCoin)
  const [receipt, setReceipt] = useState<{
    coinBefore: number
    coinAfter: number
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
          const coinAfter = Number(data.cashCount ?? Math.max(0, coinBalance - item.price))
          setReceipt({
            coinBefore,
            coinAfter,
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

  /** IDC_BTN_HANCOINBUY — MP追加購入 (決済接続前は準備中表示) */
  const handleBuy = () => {
    void showMessage('MP購入機能は準備中です。')
  }

  const yen = (value: number) => value >= 0 ? `${Math.trunc(value).toLocaleString('ja-JP')} MP` : '---'

  if (receipt) {
    return <ResponsiveShopTransactionDlg
      title="購入完了"
      itemName={item.itemName}
      itemKind={item.itemType}
      imageUrl={`${IMG_ITEM}/mj_custom_${String(item.itemId).padStart(2, '0')}.png`}
      costs={[{ label: '購入価格', value: yen(item.price) }]}
      balances={[{ label: '購入後のMP', value: yen(receipt.coinAfter) }]}
      complete
      onCancel={() => { onBuyOK?.(receipt.coinAfter); onClose() }}
    />
  }
  return <ResponsiveShopTransactionDlg
    title="購入しますか？"
    itemName={item.itemName}
    itemKind={item.itemType}
    description={[item.itemDesc]}
    imageUrl={`${IMG_ITEM}/mj_custom_${String(item.itemId).padStart(2, '0')}.png`}
    costs={[{ label: '価格', value: yen(item.price) }]}
    balances={[{ label: '所持MP', value: yen(coinBalance) }, { label: '購入後のMP', value: yen(Math.max(0, coinBalance - item.price)) }]}
    confirmDisabled={yesDis}
    onConfirm={handleYes}
    onCancel={onClose}
  />
}
