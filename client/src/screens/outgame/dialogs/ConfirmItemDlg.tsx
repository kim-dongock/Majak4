import { useEffect, useState } from 'react'
import * as SignalR from '../../../api/signalr'
import { showError, selectMajItemErrorMessage } from '../../../utils/msgbox'
import { useAuthStore } from '../../../store/authStore'
import { SHOP_ITEM_DATA_BUY, SHOP_ITEM_DATA_EXC, type BuyItemData, type ExcItemData } from './shopItemData'

const IMG = '/assets/images/game'
const IMG_ITEM = `${IMG}/items`
const TM_QUANTITY_LIMIT = new Date(2037, 0, 1).getTime() / 1000

export interface RawMajItem {
  itemCode: string
  buyDt: number
  endDt: number
  qty: number
  useFlag: number
}

interface SlotInfo {
  raw: RawMajItem
  buy?: BuyItemData
  exchange?: ExcItemData
  useFlag: boolean
}

interface Props {
  onClose: () => void
  majItems: RawMajItem[]
  onMajItemsChange?: (items: RawMajItem[]) => void
}

function packetValue(data: Record<string, unknown>, key: string): unknown {
  if (key in data) return data[key]
  const found = Object.keys(data).find(candidate => candidate.toLowerCase() === key.toLowerCase())
  return found ? data[found] : undefined
}

function readAny(data: Record<string, unknown>, keys: string[]) {
  for (const key of keys) {
    const value = packetValue(data, key)
    if (value !== undefined) return value
  }
}

function parseUseFlag(value: unknown) {
  if (typeof value === 'boolean') return value ? 1 : 0
  if (typeof value === 'number') return value === 0 ? 0 : 1
  return ['1', 'Y', 'TRUE', 'V1E'].includes(String(value ?? '').trim().toUpperCase()) ? 1 : 0
}

export function normalizeRawMajItem(item: Record<string, unknown>): RawMajItem {
  return {
    itemCode: String(readAny(item, ['itemCode', 'ItemCode', 'mjkk58e']) ?? ''),
    buyDt: Number(readAny(item, ['buyDt', 'buyDate', 'BuyDt', 'BuyDate', 'mjkk59e']) ?? 0),
    endDt: Number(readAny(item, ['endDt', 'endDate', 'EndDt', 'EndDate', 'mjkk60e']) ?? 0),
    qty: Number(readAny(item, ['qty', 'quantity', 'Qty', 'Quantity', 'mjkk140e']) ?? 0),
    useFlag: parseUseFlag(readAny(item, ['useFlag', 'UseFlag', 'mjkk61e'])),
  }
}

function classifyItems(items: RawMajItem[]): SlotInfo[] {
  const slots: SlotInfo[] = []
  for (const raw of items) {
    const buy = SHOP_ITEM_DATA_BUY.find(item => item.avCode === raw.itemCode)
    if (buy) {
      slots.push({ raw, buy, useFlag: raw.useFlag !== 0 })
      continue
    }
    const exchange = SHOP_ITEM_DATA_EXC.find(item => item.itemCode === raw.itemCode)
    if (exchange) slots.push({ raw, exchange, useFlag: raw.useFlag !== 0 })
  }
  return slots
}

function indexed(data: Record<string, unknown>, key: string, index: number): unknown {
  const direct = packetValue(data, `${key}${index}`) ?? packetValue(data, `${key}_${index}`)
  if (direct !== undefined) return direct
  const collection = packetValue(data, key)
  return Array.isArray(collection) ? collection[index] : collection
}

function itemName(slot: SlotInfo) {
  return (slot.buy?.name ?? slot.exchange?.name ?? '').replace(/\([16]個\)/g, '')
}

function itemImage(slot: SlotInfo, sex: string) {
  if (slot.buy) {
    const useCodes: Record<string, string> = { MJ20: 'mj_shop_item_sell_coin_b01.png', MJ21: 'mj_shop_item_sell_ryu_b01.png', MJ22: 'mj_shop_item_sell_ryu_b02.png' }
    return useCodes[slot.raw.itemCode] ? `${IMG_ITEM}/${useCodes[slot.raw.itemCode]}` : null
  }
  if (!slot.exchange) return null
  if (slot.exchange.itemCode === 'MJ23') return `${IMG_ITEM}/mj_shop_item_sell_coin_b02.png`
  return sex === 'F' && slot.exchange.imagePathFemale ? slot.exchange.imagePathFemale : slot.exchange.imagePath
}

function itemTerm(item: RawMajItem) {
  if (item.endDt >= TM_QUANTITY_LIMIT) return `残り：${item.qty}個`
  const format = (seconds: number) => new Date(seconds * 1000).toLocaleString('ja-JP', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })
  return `${format(item.buyDt)} から ${format(item.endDt)} まで`
}

export default function ConfirmItemDlg({ onClose, majItems, onMajItemsChange }: Props) {
  const player = useAuthStore(state => state.player)
  const [slots, setSlots] = useState<SlotInfo[]>(() => classifyItems(majItems))
  const [pendingIndex, setPendingIndex] = useState<number | null>(null)

  useEffect(() => setSlots(classifyItems(majItems)), [majItems])

  useEffect(() => {
    const handler = (data: Record<string, unknown>) => {
      const result = String(packetValue(data, 'k1e') ?? packetValue(data, 'result') ?? '')
      if (result === 'v2e' || result === 'failure' || result === '0') return
      const count = Number(packetValue(data, 'k25e') ?? data.count ?? 0)
      const nextItems = Array.from({ length: count }, (_, index) => normalizeRawMajItem({
        itemCode: indexed(data, 'mjkk58e', index), buyDt: indexed(data, 'mjkk59e', index), endDt: indexed(data, 'mjkk60e', index), qty: indexed(data, 'mjkk140e', index), useFlag: indexed(data, 'mjkk61e', index),
      })).filter(item => item.itemCode)
      onMajItemsChange?.(nextItems)
    }
    SignalR.on('mjkc43e', handler)
    SignalR.send('mjkc43e', {}).catch(() => {})
    return () => SignalR.off('mjkc43e', handler)
  }, [onMajItemsChange])

  useEffect(() => {
    const handler = (data: Record<string, unknown>) => {
      const result = String(packetValue(data, 'k1e') ?? packetValue(data, 'result') ?? '')
      if (result === 'v2e' || result === 'failure' || result === '0') {
        showError(selectMajItemErrorMessage(String(data.failCode ?? data.message ?? data.k2e ?? '')) || 'アイテムの選択に失敗しました')
      } else {
        const count = Number(packetValue(data, 'k25e') ?? data.count ?? 0)
        const flags = new Map(Array.from({ length: count }, (_, index) => [String(indexed(data, 'mjkk58e', index) ?? ''), parseUseFlag(indexed(data, 'mjkk61e', index))]))
        if (flags.size) onMajItemsChange?.(majItems.map(item => flags.has(item.itemCode) ? { ...item, useFlag: flags.get(item.itemCode)! } : item))
      }
      setPendingIndex(null)
    }
    SignalR.on('mjkc21e', handler)
    return () => SignalR.off('mjkc21e', handler)
  }, [majItems, onMajItemsChange])

  const equip = (index: number, slot: SlotInfo) => {
    setPendingIndex(index)
    SignalR.send('mjkc21e', { 'mjkk58e': slot.raw.itemCode }).catch(() => { setPendingIndex(null); showError('サーバーへの送信に失敗しました') })
  }

  return <div className="majak-popup-overlay owned-items-overlay" role="dialog" aria-modal="true" aria-label="所持アイテム">
    <section className="majak-popup-panel owned-items"><header className="majak-popup-titlebar"><h2>所持アイテム</h2><button className="majak-popup-titlebar__close" type="button" onClick={onClose} aria-label="閉じる">×</button></header><main className="majak-popup-body">{slots.length === 0 ? <p>所持アイテムはありません。</p> : slots.map((slot, index) => { const canEquip = !!slot.exchange && !slot.useFlag && slot.raw.qty > 0 && slot.raw.endDt > Date.now() / 1000; const image = itemImage(slot, player?.sex ?? 'M'); return <article key={`${slot.raw.itemCode}-${index}`}><div className="image">{image && <img src={image} alt="" />}</div><div><h3>{itemName(slot)}</h3><span className={slot.useFlag ? 'active' : ''}>{slot.useFlag ? '使用中' : '所持中'}</span><p>{itemTerm(slot.raw)}</p></div>{canEquip && <button type="button" disabled={pendingIndex === index} onClick={() => equip(index, slot)}>装備</button>}</article> })}</main><footer className="majak-popup-actions"><button type="button" onClick={onClose}>閉じる</button></footer></section>
    <style>{`
      .owned-items-overlay { position: absolute; inset: 0; z-index: 400; display: grid; place-items: center; padding: 12px; box-sizing: border-box; background: rgba(8,16,20,.76); font-family: var(--majak-font-family-ui); } .owned-items { width: min(720px,100%); height: min(560px,100%); min-height: 0; display: flex; flex-direction: column; overflow: hidden; color: #18312b; border: 1px solid #829287; background: var(--majak-popup-panel-color); } .owned-items header { display: flex; align-items: center; justify-content: space-between; padding: 10px 14px; color: #fff; background: #174b43; } .owned-items h2 { margin: 0; font-size: var(--majak-popup-font-title); } .owned-items header button { border: 1px solid currentColor; color: #fff; background: transparent; font-size: var(--majak-popup-close-font-size); } .owned-items main { min-height: 0; flex: 1; padding: 10px; overflow: auto; } .owned-items article { display: grid; grid-template-columns: 52px 1fr auto; gap: 10px; align-items: center; min-height: 62px; padding: 7px; border-bottom: 1px solid #d5ddd2; background: #fffdf8; } .owned-items article:nth-child(even) { background: #f0f4ea; } .image { width: 52px; height: 52px; display: grid; place-items: center; background: #e6ece1; } .image img { max-width: 100%; max-height: 100%; object-fit: contain; } .owned-items h3 { margin: 0; font-size: var(--majak-popup-font-emphasis); } .owned-items span { display: inline-block; margin-top: 4px; padding: 3px 5px; color: #607069; background: #e3e9df; font-size: var(--majak-popup-font-body); } .owned-items span.active { color: #fff; background: #b84228; } .owned-items p { margin: 5px 0 0; color: #5b6d66; font-size: var(--majak-popup-font-body); } .owned-items article > button, .owned-items footer button { min-height: var(--majak-popup-command-height); border: 0; padding: 0 11px; color: #fff; background: #1b5b4d; font: var(--majak-weight-regular) var(--majak-popup-font-body)/1 var(--majak-font-family-ui); } .owned-items footer { display: flex; justify-content: flex-end; padding: 9px 14px; background: #e7ede4; } @media (max-width: 420px) { .owned-items-overlay { padding: 0; } .owned-items { width: 100%; height: 100%; } .owned-items article { grid-template-columns: 44px 1fr auto; gap: 7px; } .image { width: 44px; height: 44px; } }
    `}</style>
  </div>
}
