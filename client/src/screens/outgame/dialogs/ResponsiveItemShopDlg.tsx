import { useEffect, useState } from 'react'
import * as SignalR from '../../../api/signalr'
import { getCashProducts, getConvenienceItems } from '../../../api/shop'
import type { CashProduct, ConvenienceShopItem } from '../../../api/shop'
import { useAuthStore } from '../../../store/authStore'
import { useOutgameLayoutMode } from '../../../hooks/useOutgameLayoutMode'
import { SHOP_ITEM_DATA_BUY, SHOP_ITEM_DATA_EXC } from './shopItemData'
import type { BuyItemData, ExcItemData } from './shopItemData'
import BuyCustomItemDlg from './BuyCustomItemDlg'
import BuyExchangeItemDlg from './BuyExchangeItemDlg'
import BuyHanCoinItemDlg from './BuyHanCoinItemDlg'

const IMG_CUSTOM_ITEM = '/assets/images/game/items/custom'

type ShopTab = 'cash' | 'custom' | 'item' | 'exchange'

interface CustomShopItem {
  shopNo: number
  customId: number
  kind: number
  price: number
  name: string
  description: string
  gameMoney: number
  purchased: number
}

interface Props {
  onClose: () => void
  onConfirmItem?: () => void
  onBalanceUpdate?: (balance: { cashCount?: number; gemCount: number; gamMoney: number }) => void
  cashCount?: number
  gemCount?: number
  gamMoney?: number
}

function format(value: number, unit: string) {
  return `${Math.max(0, Math.trunc(value)).toLocaleString('ja-JP')} ${unit}`
}

function customKindName(kind: number) {
  if (kind >= 10 && kind < 20) return '背景'
  if (kind >= 20 && kind < 30) return '牌'
  if (kind >= 30 && kind < 40) return 'コスチューム'
  if (kind >= 50 && kind < 60) return 'BGM'
  return 'その他'
}

function cashItemDescription(item: BuyItemData) {
  return item.nameSub2
    ? [
        `獲得できる龍珠が${item.nameSub2}になります。`,
        '龍珠2倍と龍珠3倍が同時に有効な場合は龍珠4倍になります。',
        '対局終了時にアイテムの効果が有効である必要があります。',
        `ボーナスとして${format(item.gameMoney, 'GP')}が付きます。`,
      ]
    : [
        '残っている回数に応じて交流広場及び段位戦の場代が無料になります。',
        'ハイ卓は対象外です。',
        '対局終了時にアイテムの効果が有効である必要があります。',
        `ボーナスとして${format(item.gameMoney, 'GP')}が付きます。`,
      ]
}

function convenienceCardDescription(item: ConvenienceShopItem, legacyItem?: BuyItemData) {
  if (legacyItem?.nameSub2) return `期間中、獲得龍珠が${legacyItem.nameSub2}になります。`
  if (legacyItem) return '交流広場・段位戦の場代が無料になります。'
  return item.description || '便利アイテム'
}

export default function ResponsiveItemShopDlg({
  onClose,
  onConfirmItem,
  onBalanceUpdate,
  cashCount = 0,
  gemCount = 0,
  gamMoney = 0,
}: Props) {
  const player = useAuthStore(state => state.player)
  const layoutMode = useOutgameLayoutMode()
  const mobileLayoutClass = layoutMode === 'desktop' ? '' : ` responsive-shop--${layoutMode}`
  const [tab, setTab] = useState<ShopTab>('cash')
  const [cashProducts, setCashProducts] = useState<CashProduct[]>([])
  const [cashLoadFailed, setCashLoadFailed] = useState(false)
  const [convenienceItems, setConvenienceItems] = useState<ConvenienceShopItem[]>([])
  const [customItems, setCustomItems] = useState<CustomShopItem[]>([])
  const [buyCustomTarget, setBuyCustomTarget] = useState<CustomShopItem | null>(null)
  const [buyItemTarget, setBuyItemTarget] = useState<BuyItemData | null>(null)
  const [exchangeTarget, setExchangeTarget] = useState<ExcItemData | null>(null)
  const [currentCash, setCurrentCash] = useState(cashCount)
  const [currentGem, setCurrentGem] = useState(gemCount)
  const [currentMoney, setCurrentMoney] = useState(gamMoney)

  useEffect(() => {
    setCurrentCash(cashCount)
    setCurrentGem(gemCount)
    setCurrentMoney(gamMoney)
  }, [cashCount, gemCount, gamMoney])

  useEffect(() => {
    let isCurrent = true
    getCashProducts()
      .then(products => { if (isCurrent) setCashProducts(products) })
      .catch(() => { if (isCurrent) setCashLoadFailed(true) })
    return () => { isCurrent = false }
  }, [])

  useEffect(() => {
    let isCurrent = true
    getConvenienceItems()
      .then(items => { if (isCurrent) setConvenienceItems(items) })
      .catch(() => { if (isCurrent) setConvenienceItems([]) })
    return () => { isCurrent = false }
  }, [])

  useEffect(() => {
    const handler = (data: Record<string, unknown>) => {
      const raw = Array.isArray(data.shopList) ? data.shopList as Record<string, unknown>[] : []
      setCustomItems(raw.map(item => ({
        shopNo: Number(item.shopNo ?? item['mjkk139e'] ?? 0),
        customId: Number(item.customId ?? item['mjkk138e'] ?? 0),
        kind: Number(item.customType ?? item.kind ?? item.itemType ?? 0),
        price: Number(item.customPrice ?? item.price ?? 0),
        name: String(item.Name ?? item.name ?? item.itemName ?? ''),
        description: String(item.description ?? item.itemDescription ?? ''),
        gameMoney: Number(item.gameMoney ?? 0),
        purchased: Number(item.Purchased ?? item.purchased ?? 0),
      })))
    }
    SignalR.on('mjkc36e', handler)
    SignalR.send('mjkc35e', { k3e: player?.pix ?? '' }).catch(() => {})
    return () => SignalR.off('mjkc36e', handler)
  }, [player?.pix])

  const pix = player?.pix ?? ''
  const memberName = player?.name || pix
  const sexIndex = player?.sex === 'F' ? 1 : 0
  const getExchangeImage = (item: ExcItemData) => (
    sexIndex === 1 && item.imagePathFemale ? item.imagePathFemale : item.imagePath
  )

  const cashPrice = (value: number) => format(value, 'MP')
  const moneyPrice = (value: number) => format(value, 'GP')

  return (
    <>
      <div className={`responsive-shop-overlay${mobileLayoutClass}`} role="dialog" aria-modal="true" aria-label="麻雀ショップ">
        <section className={`responsive-shop${mobileLayoutClass}`}>
          <header className="responsive-shop__header">
            <div>
              <p className="responsive-shop__eyebrow">MAJAK4 STORE</p>
              <h2>麻雀ショップ</h2>
            </div>
            <button className="responsive-shop__close" type="button" onClick={onClose} aria-label="閉じる">x</button>
          </header>

          <nav className="responsive-shop__tabs" aria-label="ショップ分類">
            {([
              ['cash', 'キャッシュ購入'],
              ['custom', 'カスタム'],
              ['item', '便利アイテム'],
              ['exchange', '龍珠交換'],
            ] as const).map(([id, label]) => (
              <button key={id} type="button" className={tab === id ? 'is-active' : ''} onClick={() => setTab(id)}>
                {label}
              </button>
            ))}
          </nav>

          <main className="responsive-shop__content">
            {tab === 'cash' && (
              <div className="responsive-shop__grid responsive-shop__grid--cash">
                {cashProducts.map(product => {
                  const bonus = Math.max(0, product.cashAmount - product.priceJpy)
                  return (
                    <article className="shop-card shop-card--cash" key={product.productId}>
                      <span className="shop-card__tag">キャッシュ</span>
                      <h3>{format(product.cashAmount, 'MP')}</h3>
                      {bonus > 0 && <p className="shop-card__bonus">+{format(bonus, 'MP ボーナス')}</p>}
                      <div className="shop-card__footer">
                        <strong>¥{product.priceJpy.toLocaleString('ja-JP')}</strong>
                        <button type="button" disabled>購入</button>
                      </div>
                    </article>
                  )
                })}
                {cashProducts.length === 0 && !cashLoadFailed && <p className="responsive-shop__empty">キャッシュ商品を読み込んでいます。</p>}
                {cashLoadFailed && <p className="responsive-shop__empty">キャッシュ商品を読み込めませんでした。</p>}
              </div>
            )}

            {tab === 'custom' && (
              <div className="responsive-shop__grid">
                {customItems.map(item => {
                  const isPurchased = item.purchased !== 0
                  const canBuy = !isPurchased && currentCash >= item.price
                  return (
                    <article className="shop-card" key={item.shopNo}>
                      <div className="shop-card__image"><img src={`${IMG_CUSTOM_ITEM}/mj_custom_${item.customId}.png`} alt="" /></div>
                      <span className="shop-card__tag">{customKindName(item.kind)}</span>
                      <h3>{item.name}</h3>
                      <p>{item.description}</p>
                      <div className="shop-card__footer">
                        <strong>{cashPrice(item.price)}</strong>
                        <button type="button" disabled={!canBuy} onClick={() => setBuyCustomTarget(item)}>{isPurchased ? '購入済み' : '購入'}</button>
                      </div>
                    </article>
                  )
                })}
                {customItems.length === 0 && <p className="responsive-shop__empty">販売中のカスタムアイテムはありません。</p>}
              </div>
            )}

            {tab === 'item' && (
              <div className="responsive-shop__grid">
                {convenienceItems.filter(item => SHOP_ITEM_DATA_BUY.some(candidate => candidate.avCode === item.sellCode)).map(item => {
                  const legacyItem = SHOP_ITEM_DATA_BUY.find(candidate => candidate.avCode === item.sellCode)
                  return <article className="shop-card" key={`${item.itemCode}-${item.sellCode}`}>
                    <div className="shop-card__image">{legacyItem && <img src={legacyItem.imagePath} alt="" />}</div>
                    <span className="shop-card__tag">便利アイテム</span>
                    <h3>{item.itemName}</h3>
                    <p>{convenienceCardDescription(item, legacyItem)}</p>
                    <div className="shop-card__footer">
                      <strong>{cashPrice(item.cashPrice)}</strong>
                      <button type="button" disabled={!legacyItem || currentCash < item.cashPrice} onClick={() => legacyItem && setBuyItemTarget({ ...legacyItem, name: item.itemName, hancoinPrice: item.cashPrice })}>購入</button>
                    </div>
                  </article>
                })}
                {convenienceItems.length === 0 && <p className="responsive-shop__empty">販売中の便利アイテムはありません。</p>}
              </div>
            )}

            {tab === 'exchange' && (
              <div className="responsive-shop__grid">
                {SHOP_ITEM_DATA_EXC.filter(item => item.costGem > 0 || item.gameMoney > 0).map((item, index) => {
                  const canExchange = currentGem >= item.costGem && currentMoney >= item.gameMoney
                  const period = item.limitDays >= 0 ? `${item.limitDays}日` : item.quantity > 0 ? `${item.quantity}回` : '永久'
                  return (
                    <article className="shop-card" key={`${item.sellCode}-${index}`}>
                      <div className="shop-card__image"><img src={getExchangeImage(item)} alt="" /></div>
                      <span className="shop-card__tag">{item.itemKind} / {period}</span>
                      <h3>{item.name}</h3>
                      <p>{item.guid1 || item.guid2}</p>
                      <div className="shop-card__footer">
                        <span className="shop-card__cost"><b>{format(item.costGem, '龍珠')}</b><b>{moneyPrice(item.gameMoney)}</b></span>
                        <button type="button" disabled={!canExchange} onClick={() => setExchangeTarget(item)}>交換</button>
                      </div>
                    </article>
                  )
                })}
              </div>
            )}
          </main>

          <footer className="responsive-shop__footer">
            <div className="responsive-shop__balances" aria-label="所持残高">
              <span className="responsive-shop__balance-title">所持残高</span>
              <span className="responsive-shop__balance responsive-shop__balance--cash"><i aria-hidden="true" /><span>キャッシュ</span><strong>{cashPrice(currentCash)}</strong></span>
              <span className="responsive-shop__balance responsive-shop__balance--gem"><i aria-hidden="true" /><span>龍珠</span><strong>{format(currentGem, '個')}</strong></span>
              <span className="responsive-shop__balance responsive-shop__balance--money"><i aria-hidden="true" /><span>GP</span><strong>{moneyPrice(currentMoney)}</strong></span>
            </div>
            <div className="responsive-shop__actions">
              <button type="button" onClick={onConfirmItem}>所持アイテム</button>
              <button type="button" onClick={onClose}>閉じる</button>
            </div>
          </footer>
        </section>
      </div>

      {buyCustomTarget && <BuyCustomItemDlg
        item={{ itemId: buyCustomTarget.customId, itemName: buyCustomTarget.name, itemType: customKindName(buyCustomTarget.kind), itemDesc: buyCustomTarget.description, price: buyCustomTarget.price, shopNo: buyCustomTarget.shopNo, gameMoney: buyCustomTarget.gameMoney }}
        pix={pix} memberName={memberName} hanCoin={currentCash}
        onClose={() => setBuyCustomTarget(null)}
        onBuyOK={(nextCash) => {
          setCurrentCash(nextCash)
          onBalanceUpdate?.({ cashCount: nextCash, gemCount: currentGem, gamMoney: currentMoney })
          setCustomItems(items => items.map(item => item.shopNo === buyCustomTarget.shopNo ? { ...item, purchased: 1 } : item))
          SignalR.send('mjkc35e', { k3e: player?.pix ?? '' }).catch(() => {})
        }}
      />}
      {buyItemTarget && <BuyHanCoinItemDlg
        item={{ itemCode: buyItemTarget.avCode, sellCode: buyItemTarget.sellCode, itemName: buyItemTarget.name, price: buyItemTarget.hancoinPrice, gameMoney: buyItemTarget.gameMoney, description: cashItemDescription(buyItemTarget), imageUrl: buyItemTarget.imagePath, isLottery: false }}
        pix={pix} memberName={memberName} hanCoin={currentCash}
        onClose={() => setBuyItemTarget(null)}
        onBuyOK={(nextCash) => {
          setCurrentCash(nextCash)
          onBalanceUpdate?.({ cashCount: nextCash, gemCount: currentGem, gamMoney: currentMoney })
        }}
      />}
      {exchangeTarget && <BuyExchangeItemDlg
        item={{ sellCode: exchangeTarget.sellCode, itemName: exchangeTarget.name, itemKind: exchangeTarget.itemKind, itemGuid1: exchangeTarget.guid1, itemGuid2: exchangeTarget.guid2, costGem: exchangeTarget.costGem, costMoney: exchangeTarget.gameMoney, limitDays: exchangeTarget.limitDays, quantity: exchangeTarget.quantity, imageUrl: getExchangeImage(exchangeTarget) }}
        pix={pix} memberName={memberName} userGem={currentGem} userMoney={currentMoney}
        onClose={() => setExchangeTarget(null)}
        onBuyOK={({ userGem, userMoney }) => {
          setCurrentGem(userGem)
          setCurrentMoney(userMoney)
          onBalanceUpdate?.({ cashCount: currentCash, gemCount: userGem, gamMoney: userMoney })
        }}
      />}

      <style>{`
        .responsive-shop-overlay { position: absolute; inset: 0; z-index: 300; display: grid; place-items: center; padding: 20px; overflow: hidden; background: rgba(8, 16, 20, .7); font-family: var(--majak-font-family-ui); box-sizing: border-box; }
        .responsive-shop { width: min(1120px, 100%); height: min(620px, 100%); max-height: 100%; min-height: 0; display: flex; flex-direction: column; overflow: hidden; color: #172323; background: #f5f2e9; border: 1px solid #7d8e80; box-shadow: 0 24px 72px rgba(0, 0, 0, .42); }
        .responsive-shop__header { display: flex; gap: 18px; align-items: center; justify-content: space-between; padding: 16px 24px; color: #fff; background: #174b43; }
        .responsive-shop__header h2 { margin: 2px 0 0; font-size: calc(25px * var(--majak-type-scale)); font-weight: 700; letter-spacing: 0; }
        .responsive-shop__eyebrow { margin: 0; font: 700 calc(10px * var(--majak-type-scale))/1 var(--majak-font-family-ui); letter-spacing: 1px; color: #d7b95d; }
        .responsive-shop__close { width: 36px; height: 36px; border: 1px solid rgba(255,255,255,.7); border-radius: 0; color: #fff; background: transparent; font-size: calc(22px * var(--majak-type-scale)); cursor: pointer; }
        .responsive-shop__balances { flex: 1 1 auto; display: grid; min-width: 0; grid-template-columns: 74px repeat(3, minmax(0, 1fr)); align-items: stretch; overflow: hidden; border: 1px solid #c1cbc0; background: #f7faf4; color: #607069; white-space: nowrap; }
        .responsive-shop__balance-title { display: grid; place-items: center; padding: 0 8px; color: #f7f3e7; background: #315c50; font: 700 calc(11px * var(--majak-type-scale))/1 var(--majak-font-family-ui); letter-spacing: 0; }
        .responsive-shop__balance { display: flex; min-width: 0; gap: 6px; align-items: center; justify-content: center; padding: 7px 8px; border-left: 1px solid #d7dfd4; font: 700 calc(11px * var(--majak-type-scale))/1.2 var(--majak-font-family-ui); }
        .responsive-shop__balance i { width: 6px; height: 6px; flex: none; border-radius: 50%; background: #1c5a4d; box-shadow: 0 0 0 2px rgba(28,90,77,.12); }
        .responsive-shop__balance--gem i { background: #b84228; box-shadow: 0 0 0 2px rgba(184,66,40,.12); }
        .responsive-shop__balance--money i { background: #b88923; box-shadow: 0 0 0 2px rgba(184,137,35,.14); }
        .responsive-shop__balance > span { min-width: 0; overflow: hidden; text-overflow: ellipsis; }
        .responsive-shop__balance strong { color: #1f4d42; font: 700 calc(17px * var(--majak-type-scale))/1.1 var(--majak-font-family-ui); }
        .responsive-shop__tabs { display: grid; grid-template-columns: repeat(4, 1fr); border-bottom: 1px solid #a5afa5; background: #dbe0d7; }
        .responsive-shop__tabs button { min-height: 51px; border: 0; border-right: 1px solid #b7c0b6; color: #31473f; background: transparent; font: 700 calc(14px * var(--majak-type-scale))/1 var(--majak-font-family-ui); cursor: pointer; }
        .responsive-shop__tabs button.is-active { color: #fff; background: #b84228; }
        .responsive-shop__content { min-height: 0; flex: 1; padding: 24px; overflow: auto; overscroll-behavior: contain; }
        .responsive-shop__grid { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 14px; }
        .responsive-shop__grid--cash { grid-template-columns: repeat(5, minmax(0, 1fr)); }
        .shop-card { display: flex; min-width: 0; min-height: 242px; flex-direction: column; padding: 15px; border: 1px solid #c8d0c2; border-radius: 4px; background: #fffdf8; box-shadow: 0 2px 0 rgba(47, 79, 64, .08); }
        .shop-card--cash { min-height: 184px; color: #123d36; background: #ecf0e4; border-top: 4px solid #d19f35; }
        .shop-card__image { height: 94px; display: grid; place-items: center; margin-bottom: 8px; overflow: hidden; background: #f1eee4; }
        .shop-card__image img { max-width: 100%; max-height: 94px; object-fit: contain; image-rendering: auto; }
        .shop-card__tag { min-height: 18px; color: #9a6322; font: 700 calc(11px * var(--majak-type-scale))/1.2 var(--majak-font-family-ui); }
        .shop-card h3 { margin: 5px 0; overflow: hidden; color: #1f302b; font-size: calc(16px * var(--majak-type-scale)); line-height: 1.35; text-overflow: ellipsis; white-space: nowrap; }
        .shop-card p { min-height: 32px; margin: 0; overflow: hidden; color: #607069; font: calc(12px * var(--majak-type-scale))/1.35 var(--majak-font-family-ui); }
        .shop-card__bonus { color: #b84228 !important; font-weight: 700 !important; }
        .shop-card__cost { display: flex; min-width: 0; flex: 1 1 auto; flex-wrap: nowrap; gap: 5px; align-items: center; white-space: nowrap; }
        .shop-card__cost b { flex: none; color: #195346; font: 700 calc(10px * var(--majak-type-scale))/1 var(--majak-font-family-ui); white-space: nowrap; }
        .shop-card__footer { display: flex; gap: 10px; align-items: center; justify-content: space-between; margin-top: auto; padding-top: 12px; }
        .shop-card__footer strong { min-width: 0; color: #b84228; font: 700 calc(14px * var(--majak-type-scale))/1.1 var(--majak-font-family-ui); white-space: nowrap; }
        .shop-card__footer span { color: #65736d; font: calc(11px * var(--majak-type-scale))/1.2 var(--majak-font-family-ui); }
        .shop-card button, .responsive-shop__footer button { border: 0; border-radius: 3px; padding: 9px 13px; color: #fff; background: #1c5a4d; font: 700 calc(12px * var(--majak-type-scale))/1 var(--majak-font-family-ui); cursor: pointer; white-space: nowrap; }
        .shop-card__footer button { flex: none; }
        .shop-card button:hover, .responsive-shop__footer button:hover { background: #123f36; }
        .shop-card button:disabled { color: #87918c; background: #d7ddd5; cursor: not-allowed; }
        .responsive-shop__empty { grid-column: 1 / -1; padding: 42px; text-align: center; color: #647069; font: calc(14px * var(--majak-type-scale)) var(--majak-font-family-ui); }
        .responsive-shop__footer { display: flex; gap: 16px; align-items: center; justify-content: space-between; padding: 12px 24px; border-top: 1px solid #c8d0c2; background: #e8ede4; }
        .responsive-shop__actions { display: flex; gap: 10px; justify-content: flex-end; }
        .responsive-shop__footer button:last-child { color: #32453e; background: transparent; border: 1px solid #839087; }
        .responsive-shop--mobileLandscape, .responsive-shop--mobilePortrait { width: 100%; height: 100%; max-height: 100%; }
        .responsive-shop-overlay--mobileLandscape, .responsive-shop-overlay--mobilePortrait { padding: 0; align-items: stretch; }
        .responsive-shop--mobileLandscape .responsive-shop__header, .responsive-shop--mobilePortrait .responsive-shop__header { gap: 8px; padding: 8px 10px; }
        .responsive-shop--mobileLandscape .responsive-shop__header h2, .responsive-shop--mobilePortrait .responsive-shop__header h2 { margin: 0; font-size: calc(17px * var(--majak-type-scale)); }
        .responsive-shop--mobileLandscape .responsive-shop__eyebrow, .responsive-shop--mobilePortrait .responsive-shop__eyebrow { display: none; }
        .responsive-shop--mobileLandscape .responsive-shop__close, .responsive-shop--mobilePortrait .responsive-shop__close { width: 28px; height: 28px; font-size: calc(18px * var(--majak-type-scale)); }
        .responsive-shop--mobileLandscape .responsive-shop__balances, .responsive-shop--mobilePortrait .responsive-shop__balances { min-width: 0; flex: 1; }
        .responsive-shop--mobileLandscape .responsive-shop__balances, .responsive-shop--mobilePortrait .responsive-shop__balances { grid-template-columns: 54px repeat(3, minmax(0, 1fr)); }
        .responsive-shop--mobileLandscape .responsive-shop__balance-title, .responsive-shop--mobilePortrait .responsive-shop__balance-title { padding: 0 4px; font-size: calc(10px * var(--majak-type-scale)); }
        .responsive-shop--mobileLandscape .responsive-shop__balance, .responsive-shop--mobilePortrait .responsive-shop__balance { gap: 4px; padding: 5px 4px; font-size: calc(10px * var(--majak-type-scale)); }
        .responsive-shop--mobileLandscape .responsive-shop__balance i, .responsive-shop--mobilePortrait .responsive-shop__balance i { width: 4px; height: 4px; }
        .responsive-shop--mobileLandscape .responsive-shop__balance strong, .responsive-shop--mobilePortrait .responsive-shop__balance strong { font-size: calc(16px * var(--majak-type-scale)); }
        .responsive-shop--mobileLandscape .responsive-shop__tabs, .responsive-shop--mobilePortrait .responsive-shop__tabs { overflow-x: auto; grid-template-columns: repeat(4, minmax(94px, 1fr)); }
        .responsive-shop--mobileLandscape .responsive-shop__tabs button, .responsive-shop--mobilePortrait .responsive-shop__tabs button { min-height: 38px; font-size: calc(11px * var(--majak-type-scale)); }
        .responsive-shop--mobileLandscape .responsive-shop__content, .responsive-shop--mobilePortrait .responsive-shop__content { padding: 9px; }
        .responsive-shop--mobileLandscape .responsive-shop__grid, .responsive-shop--mobilePortrait .responsive-shop__grid { grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 8px; }
        .responsive-shop--mobileLandscape .responsive-shop__grid--cash, .responsive-shop--mobilePortrait .responsive-shop__grid--cash { grid-template-columns: repeat(5, minmax(0, 1fr)); }
        .responsive-shop--mobileLandscape .shop-card, .responsive-shop--mobilePortrait .shop-card { min-height: 176px; padding: 9px; }
        .responsive-shop--mobileLandscape .shop-card { min-height: 152px; }
        .responsive-shop--mobileLandscape .shop-card--cash, .responsive-shop--mobilePortrait .shop-card--cash { min-height: 126px; }
        .responsive-shop--mobileLandscape .shop-card__image, .responsive-shop--mobilePortrait .shop-card__image { height: 58px; margin-bottom: 5px; }
        .responsive-shop--mobileLandscape .shop-card__image img, .responsive-shop--mobilePortrait .shop-card__image img { max-height: 58px; }
        .responsive-shop--mobileLandscape .shop-card h3, .responsive-shop--mobilePortrait .shop-card h3 { margin: 3px 0; font-size: calc(13px * var(--majak-type-scale)); }
        .responsive-shop--mobileLandscape .shop-card p, .responsive-shop--mobilePortrait .shop-card p { min-height: 28px; font-size: calc(10px * var(--majak-type-scale)); }
        .responsive-shop--mobileLandscape .shop-card__footer, .responsive-shop--mobilePortrait .shop-card__footer { gap: 5px; padding-top: 7px; }
        .responsive-shop--mobileLandscape .shop-card__footer { align-items: center; flex-direction: row; }
        .responsive-shop--mobilePortrait .shop-card__footer { align-items: flex-end; flex-direction: column; }
        .responsive-shop--mobileLandscape .shop-card button, .responsive-shop--mobilePortrait .shop-card button { padding: 7px 9px; font-size: calc(10px * var(--majak-type-scale)); }
        .responsive-shop--mobileLandscape .responsive-shop__footer, .responsive-shop--mobilePortrait .responsive-shop__footer { gap: 8px; padding: 9px; }
        .responsive-shop--mobileLandscape .responsive-shop__actions, .responsive-shop--mobilePortrait .responsive-shop__actions { gap: 5px; }
      `}</style>
    </>
  )
}