import { useEffect, useRef, useState } from 'react'
import * as SignalR from '../../../api/signalr'
import { buyMajItemErrorMessage, showError } from '../../../utils/msgbox'
import ResponsiveShopTransactionDlg from './ResponsiveShopTransactionDlg'

export interface ExchangeShopItemData {
  sellCode: string
  itemName: string
  itemKind: string
  itemGuid1: string
  itemGuid2: string
  costGem: number
  costMoney: number
  limitDays: number
  quantity: number
  imageUrl?: string
}

interface Props {
  item: ExchangeShopItemData
  pix: string
  memberName?: string
  userGem: number
  userMoney: number
  onClose: () => void
  onBuyOK?: (balances: { userGem: number; userMoney: number }) => void
}

function money(value: number) {
  return `${Math.trunc(value).toLocaleString('ja-JP')} GP`
}

function gems(value: number) {
  return `${Math.trunc(value).toLocaleString('ja-JP')}個`
}

export default function BuyExchangeItemDlg({ item, userGem, userMoney, onClose, onBuyOK }: Props) {
  const [confirmDisabled, setConfirmDisabled] = useState(false)
  const [receipt, setReceipt] = useState<{ userGem: number; userMoney: number } | null>(null)
  const pendingRef = useRef<((data: Record<string, unknown>) => void) | null>(null)
  const period = item.limitDays < 0
    ? (item.quantity <= 0 ? '永久' : `${item.quantity}回`)
    : `${item.limitDays}日間`

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

  const handleConfirm = async () => {
    if (userGem < item.costGem || userMoney < item.costMoney) {
      showError('龍珠またはGPが足りないため交換できません。')
      return
    }
    setConfirmDisabled(true)

    await new Promise<void>(resolve => {
      pendingRef.current = data => {
        const failCode = String(data.failCode ?? data['mjkk95e'] ?? '')
        const result = data.k1e ?? data.result
        if (result === 'v2e' || failCode !== '') {
          const message = String(data.k2e ?? data.message ?? '')
          showError(message || buyMajItemErrorMessage(failCode))
          setConfirmDisabled(false)
        } else {
          const nextGem = Number(data['mjkk55e'] ?? data.gemcount ?? userGem - item.costGem)
          const nextMoney = Number(data.k34e ?? data.gammoney ?? userMoney - item.costMoney)
          onBuyOK?.({ userGem: nextGem, userMoney: nextMoney })
          setReceipt({ userGem: nextGem, userMoney: nextMoney })
        }
        resolve()
      }
      SignalR.send('mjkc20e', { 'mjkk57e': item.sellCode }).catch(() => {
        pendingRef.current = null
        showError('サーバーへの送信に失敗しました')
        setConfirmDisabled(false)
        resolve()
      })
    })
  }

  if (receipt) {
    return <ResponsiveShopTransactionDlg
      title="交換完了"
      itemName={item.itemName}
      itemKind={item.itemKind}
      description={[item.itemGuid1, item.itemGuid2].filter(Boolean)}
      imageUrl={item.imageUrl}
      costs={[{ label: '使用した龍珠', value: gems(item.costGem) }, { label: '使用したGP', value: money(item.costMoney) }, { label: '利用期間', value: period }]}
      balances={[{ label: '残り龍珠', value: gems(receipt.userGem) }, { label: '残りGP', value: money(receipt.userMoney) }]}
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
    costs={[{ label: '必要な龍珠', value: gems(item.costGem) }, { label: '必要なGP', value: money(item.costMoney) }, { label: '利用期間', value: period }]}
    balances={[{ label: '所持龍珠', value: gems(userGem) }, { label: '所持GP', value: money(userMoney) }]}
    confirmLabel="交換する"
    confirmDisabled={confirmDisabled}
    onConfirm={handleConfirm}
    onCancel={onClose}
  />
}
