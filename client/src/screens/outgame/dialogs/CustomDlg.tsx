import { useEffect, useState } from 'react'
import * as SignalR from '../../../api/signalr'
import { showError } from '../../../utils/msgbox'
import { useAuthStore } from '../../../store/authStore'
import { normalizeRawMajItem, type RawMajItem } from './ConfirmItemDlg'
import { SHOP_ITEM_DATA_BUY, SHOP_ITEM_DATA_EXC } from './shopItemData'

const IMG_ITEM = '/assets/images/game/items/custom'
const TAB_CHARA = 0
const TAB_HAI = 1
const TAB_OTHER = 3
const TAB_GENERAL = 4
const ITEMS_PER_PAGE = 10

interface CustomItem {
  itemId: number
  itemType: number
  itemName: string
  imageFile: string
  equipped: boolean
}

interface GeneralItem {
  raw: RawMajItem
  name: string
  imagePath: string
  canActivate: boolean
}

interface Props {
  initialTab?: number
  hanCoin?: number
  hanCoupon?: number
  currentCharaId?: number
  currentHaiId?: number
  majItems?: RawMajItem[]
  onMajItemsChange?: (items: RawMajItem[]) => void
  onEquipChange?: (item: { itemId: number; itemType: number }) => void
  onRequestShop?: () => void
  onClose: () => void
}

function formatTerm(item: RawMajItem) {
  if (item.endDt >= new Date(2037, 0, 1).getTime() / 1000) return `残り：${item.qty}個`
  const date = (seconds: number) => new Date(seconds * 1000).toLocaleString('ja-JP', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })
  return `${date(item.buyDt)} から ${date(item.endDt)} まで`
}

export default function CustomDlg({
  initialTab = TAB_GENERAL,
  currentCharaId = 0,
  currentHaiId = 0,
  majItems = [],
  onMajItemsChange,
  onEquipChange,
  onRequestShop,
  onClose,
}: Props) {
  const player = useAuthStore(state => state.player)
  const [tab, setTab] = useState(initialTab)
  const [items, setItems] = useState<CustomItem[]>([])
  const [page, setPage] = useState(1)
  const [pendingItemCode, setPendingItemCode] = useState<string | null>(null)
  const currentId = tab === TAB_CHARA ? currentCharaId : tab === TAB_HAI ? currentHaiId : 0

  useEffect(() => {
    setPage(1)
    if (tab === TAB_GENERAL) {
      setItems([])
      return
    }
    const handler = (data: Record<string, unknown>) => {
      const source = Array.isArray(data.items) ? data.items as Array<{ customId: number; nKind?: number; Name?: string }> : []
      const belongsToTab = (kind: number) => tab === TAB_HAI ? kind >= 20 && kind < 30 : tab === TAB_CHARA ? kind >= 30 && kind < 40 : kind < 20 || kind >= 40
      setItems(source.filter(item => {
        const kind = Number(item.nKind ?? 0)
        return (kind < 10 || kind >= 20) && belongsToTab(kind)
      }).map(item => {
        const itemId = Number(item.customId)
        const itemType = Number(item.nKind ?? 0)
        const fileId = itemId < 100 ? String(itemId).padStart(2, '0') : String(itemId)
        return { itemId, itemType, itemName: item.Name ?? `アイテム #${itemId}`, imageFile: tab === TAB_CHARA && itemId === 100011 ? 'icon_cos.png' : `mj_custom_${fileId}.png`, equipped: itemId === currentId }
      }))
    }
    SignalR.on('mjkc40e', handler)
    SignalR.send('mjkc39e', { k3e: player?.pix ?? '' }).catch(() => {})
    return () => SignalR.off('mjkc40e', handler)
  }, [tab, player?.pix, currentId])

  useEffect(() => {
    const handler = (data: Record<string, unknown>) => {
      const result = String(data.k1e ?? data.result ?? '')
      if (result === 'v2e' || result === '-1') {
        showError(String(data.message ?? data.k2e ?? 'アイテムの使用に失敗しました'))
      } else {
        const codes = [data.mjkk58e0, data.mjkk58e1]
        const flags = [data.mjkk61e0, data.mjkk61e1]
        onMajItemsChange?.(majItems.map(item => {
          const index = codes.findIndex(code => String(code ?? '') === item.itemCode)
          return index < 0 ? item : normalizeRawMajItem({ ...item, useFlag: flags[index] })
        }))
      }
      setPendingItemCode(null)
    }
    SignalR.on('mjkc21e', handler)
    return () => SignalR.off('mjkc21e', handler)
  }, [majItems, onMajItemsChange])

  const generalItems = majItems.flatMap(raw => {
    const canActivate = raw.qty > 0 && raw.endDt > Date.now() / 1000
    const buy = SHOP_ITEM_DATA_BUY.find(item => item.avCode === raw.itemCode)
    if (buy) return [{ raw, name: buy.name, imagePath: buy.imagePath, canActivate }]
    const exchange = SHOP_ITEM_DATA_EXC.find(item => item.itemCode === raw.itemCode)
    return exchange ? [{ raw, name: exchange.name, imagePath: exchange.imagePath, canActivate }] : []
  })
  const sourceItems = tab === TAB_GENERAL ? generalItems : items
  const totalPages = Math.max(1, Math.ceil(sourceItems.length / ITEMS_PER_PAGE))
  const pageItems = sourceItems.slice((page - 1) * ITEMS_PER_PAGE, page * ITEMS_PER_PAGE)
  const changePage = (delta: number) => setPage(current => Math.min(totalPages, Math.max(1, current + delta)))

  const equipCustom = async (item: CustomItem) => {
    if (item.itemType < 20) return
    const isCurrent = item.equipped || item.itemId === currentId
    const defaultItemId = item.itemType >= 30 && item.itemType < 40 ? 100011 : 100003
    const targetItemId = isCurrent ? defaultItemId : item.itemId
    try {
      await SignalR.send('mjkc37e', { k3e: player?.pix ?? '', 'mjkk138e': targetItemId })
      setItems(previous => previous.map(candidate => ({ ...candidate, equipped: candidate.itemId === targetItemId })))
      onEquipChange?.({ itemId: targetItemId, itemType: item.itemType })
      SignalR.send('mjkc39e', { k3e: player?.pix ?? '' }).catch(() => {})
    } catch {
      showError('サーバーへの送信に失敗しました')
    }
  }

  const useGeneral = (item: GeneralItem) => {
    setPendingItemCode(item.raw.itemCode)
    SignalR.send('mjkc21e', { 'mjkk58e': item.raw.itemCode }).catch(() => {
      setPendingItemCode(null)
      showError('サーバーへの送信に失敗しました')
    })
  }

  return <div className="majak-popup-overlay custom-inventory-overlay" role="dialog" aria-modal="true" aria-label="所持品">
    <section className="majak-popup-panel custom-inventory">
      <header className="majak-popup-titlebar"><h2>所持品</h2><button className="majak-popup-titlebar__close" type="button" onClick={onClose} aria-label="閉じる">×</button></header>
      <nav className="custom-inventory__tabs" role="tablist" aria-label="所持品の種類">
        {[[TAB_GENERAL, 'ゲーム用'], [TAB_CHARA, 'キャラ'], [TAB_HAI, '牌'], [TAB_OTHER, 'その他']].map(([value, label]) => <button key={value} type="button" role="tab" aria-selected={tab === value} className={tab === value ? 'is-active' : ''} onClick={() => setTab(value as number)}>{label}</button>)}
      </nav>
      <main className="majak-popup-body">
        {pageItems.length === 0 ? <p className="empty">所持しているアイテムはありません。</p> : <div className="grid">
          {pageItems.map(item => {
            const general = tab === TAB_GENERAL
            const custom = item as CustomItem
            const gameItem = item as GeneralItem
            const equipped = !general && (custom.equipped || custom.itemId === currentId)
            return <article className="custom-inventory__item" key={general ? gameItem.raw.itemCode : custom.itemId}>
              <div className="image"><img src={general ? gameItem.imagePath : `${IMG_ITEM}/${custom.imageFile}`} alt="" onError={event => { event.currentTarget.style.visibility = 'hidden' }} /></div>
              <h3>{general ? gameItem.name : custom.itemName}</h3>
              {general && <p>{formatTerm(gameItem.raw)}</p>}
              <button type="button" disabled={general ? !gameItem.canActivate || pendingItemCode === gameItem.raw.itemCode : equipped} onClick={() => general ? useGeneral(gameItem) : equipCustom(custom)}>{general ? (gameItem.raw.useFlag ? '使用中' : '使用する') : equipped ? '装備を外す' : '装備する'}</button>
            </article>
          })}
        </div>}
      </main>
      <footer className="majak-popup-actions custom-inventory__footer"><button type="button" onClick={() => onRequestShop ? onRequestShop() : onClose()}>ショップへ</button><span className="custom-inventory__pager"><button type="button" onClick={() => changePage(-1)} disabled={page === 1}>←</button>{page} / {totalPages}<button type="button" onClick={() => changePage(1)} disabled={page === totalPages}>→</button></span><button className="custom-inventory__close" type="button" onClick={onClose}>閉じる</button></footer>
    </section>
    <style>{`
      .custom-inventory-overlay { position: absolute; inset: 0; z-index: 300; display: grid; place-items: center; padding: 20px; box-sizing: border-box; background: rgba(8,16,20,.72); font-family: var(--majak-font-family-ui); }
      .custom-inventory { width: min(1050px, 100%); height: min(650px, 100%); min-height: 0; display: flex; flex-direction: column; overflow: hidden; color: #1d302b; border: 1px solid #748a7c; background: var(--majak-popup-panel-color); }
      .custom-inventory header { display: flex; justify-content: space-between; align-items: center; padding: 14px 22px; color: #fff; background: #174b43; } .custom-inventory h2 { margin: 0; font-size: var(--majak-popup-font-title); } .custom-inventory header button { color: #fff; background: transparent; border: 1px solid currentColor; font-size: var(--majak-popup-close-font-size); }
      .custom-inventory nav { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 0; padding: 0 18px; border-bottom: 1px solid #c8d0c2; } .custom-inventory nav button, .custom-inventory footer button, .custom-inventory article button { min-height: var(--majak-popup-command-height); border: 0; color: #31473f; background: #dbe0d7; font: var(--majak-weight-regular) var(--majak-popup-font-body)/1 var(--majak-font-family-ui); } .custom-inventory nav button.is-active, .custom-inventory footer button, .custom-inventory article button { color: #fff; background: #1c5a4d; }
      .custom-inventory main { min-height: 0; flex: 1; padding: 18px; overflow: auto; } .grid { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 12px; } article { min-width: 0; min-height: 210px; display: flex; flex-direction: column; padding: 12px; border: 1px solid #c8d0c2; background: #fffdf8; } .image { height: 124px; display: grid; place-items: center; background: #f1eee4; } .image img { max-width: 100%; max-height: 100%; object-fit: contain; } article h3, article p { margin: 8px 0; overflow: hidden; font-size: var(--majak-popup-font-body); text-align: center; text-overflow: ellipsis; white-space: nowrap; } article p { color: #5b6d66; } article button { margin-top: auto; } button:disabled { color: #84908a; background: #d6ddd5; } .empty { padding: 48px; text-align: center; }
      .custom-inventory footer { display: grid; grid-template-columns: max-content minmax(0, 1fr) max-content; gap: 12px; align-items: center; padding: 12px 18px; border-top: 1px solid #c8d0c2; background: #e8ede4; } .custom-inventory footer > button { width: auto; } .custom-inventory footer span { display: flex; justify-content: center; gap: 8px; align-items: center; } .custom-inventory footer span button { width: var(--majak-popup-command-height); padding: 0; } .custom-inventory footer > button:last-child { justify-self: end; }
      @media (max-width: 720px) { .custom-inventory-overlay { padding: 0; } .custom-inventory { width: 100%; height: 100%; } .custom-inventory header { padding: 8px 10px; } .custom-inventory main { padding: 8px; } .grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } article { min-height: 170px; padding: 8px; } .image { height: 90px; } .custom-inventory footer { padding: 8px; gap: 6px; } }
    `}</style>
  </div>
}
