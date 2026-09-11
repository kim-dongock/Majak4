import { useEffect, useRef, useState } from 'react'
import * as SignalR from '../../../api/signalr'
import { buyMajItemErrorMessage, showError } from '../../../utils/msgbox'
import LotSlotDlg from './LotSlotDlg'
import ResponsiveShopTransactionDlg from './ResponsiveShopTransactionDlg'

export interface HanCoinShopItemData {
  itemCode: string
  sellCode: string
  itemName: string
  price: number
  gameMoney: number
  description: string[]
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
  onBuyOK?: (cashCount: number) => void
}

function yen(value: number) {
  return value < 0 ? '---' : `${Math.trunc(value).toLocaleString('ja-JP')} MP`
}

export default function BuyHanCoinItemDlg({ item, hanCoin, onClose, onBuyOK }: Props) {
  const [confirmEnabled, setConfirmEnabled] = useState(false)
  const [count, setCount] = useState(1)
  const [receipt, setReceipt] = useState<{ coinAfter: number } | null>(null)
  const [showLotSlot, setShowLotSlot] = useState(false)
  const [lotteryTotal, setLotteryTotal] = useState(0)
  const pendingRef = useRef<((data: Record<string, unknown>) => void) | null>(null)
  const totalPrice = item.price * count

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
    setConfirmEnabled(true)
  }, [])

  const handleConfirm = async () => {
    if (!confirmEnabled) return
    setConfirmEnabled(false)

    await new Promise<void>(resolve => {
      pendingRef.current = data => {
        const failCode = String(data.failCode ?? data['mjkk95e'] ?? '')
        if (failCode !== '') {
          const message = String(data.k2e ?? data.message ?? '')
          showError(message || buyMajItemErrorMessage(failCode))
          onClose()
        } else {
          const coinAfter = Number(data.cashCount ?? (hanCoin >= 0 ? Math.max(0, hanCoin - totalPrice) : hanCoin))
          if (item.isLottery && typeof item.lotteryCount === 'number' && item.lotteryCount > 0) {
            const moneyChange = Number(data.moneyChange ?? 0)
            if (!Number.isSafeInteger(moneyChange) || moneyChange <= 0) {
              showError('くじの獲得GPを確認できませんでした')
              onClose()
            } else {
              setLotteryTotal(moneyChange)
              setShowLotSlot(true)
            }
          } else {
            setReceipt({ coinAfter })
          }
        }
        resolve()
      }
      SignalR.send('mjkc20e', { 'mjkk57e': item.sellCode, count: String(count) }).catch(() => {
        pendingRef.current = null
        showError('サーバーへの送信に失敗しました')
        setConfirmEnabled(true)
        resolve()
      })
    })
  }

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
    balances={[{ label: '所持MP', value: yen(hanCoin) }, { label: '購入後のMP', value: yen(Math.max(0, hanCoin - totalPrice)) }]}
    quantity={item.isLottery ? undefined : count}
    onQuantityChange={setCount}
    confirmDisabled={!confirmEnabled}
    onConfirm={handleConfirm}
    onCancel={onClose}
  >
    {showLotSlot && item.isLottery && typeof item.lotteryCount === 'number' && item.lotteryCount > 0 && <LotSlotDlg
      itemName={item.itemName}
      lotteryCount={item.lotteryCount}
      totalAmount={lotteryTotal}
      imageUrl={item.imageUrl}
      onResult={() => { onBuyOK?.(hanCoin); onClose() }}
      onClose={() => { setShowLotSlot(false); setConfirmEnabled(true) }}
    />}
  </ResponsiveShopTransactionDlg>
}
