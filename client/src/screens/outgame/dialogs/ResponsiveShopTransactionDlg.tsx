import type { ReactNode } from 'react'

interface Detail {
  label: string
  value: string
}

interface Props {
  title: string
  itemName: string
  itemKind?: string
  description?: string[]
  imageUrl?: string
  costs: Detail[]
  balances?: Detail[]
  quantity?: number
  onQuantityChange?: (quantity: number) => void
  confirmLabel?: string
  confirmDisabled?: boolean
  onConfirm?: () => void
  onCancel: () => void
  complete?: boolean
  children?: ReactNode
}

export default function ResponsiveShopTransactionDlg({
  title,
  itemName,
  itemKind,
  description = [],
  imageUrl,
  costs,
  balances = [],
  quantity,
  onQuantityChange,
  confirmLabel = '購入する',
  confirmDisabled = false,
  onConfirm,
  onCancel,
  complete = false,
  children,
}: Props) {
  return (
    <div className="shop-transaction-overlay" role="dialog" aria-modal="true" aria-label={title}>
      <section className="shop-transaction">
        <header>
          <div>
            <p>MAJAK4 STORE</p>
            <h2>{title}</h2>
          </div>
          <button type="button" onClick={onCancel} aria-label="閉じる">x</button>
        </header>
        <div className="shop-transaction__body">
          <div className="shop-transaction__item">
            {imageUrl && <div className="shop-transaction__image"><img src={imageUrl} alt="" /></div>}
            <div>
              {itemKind && <span>{itemKind}</span>}
              <h3>{itemName}</h3>
              {description.map((line, index) => <p key={`${line}-${index}`}>{line}</p>)}
            </div>
          </div>
          <dl className="shop-transaction__details">
            {quantity === undefined
              ? costs.map(detail => <div key={detail.label}><dt>{detail.label}</dt><dd>{detail.value}</dd></div>)
              : <>
                  {costs.slice(0, -1).map(detail => <div key={detail.label}><dt>{detail.label}</dt><dd>{detail.value}</dd></div>)}
                  <div><dt>数量</dt><dd><select value={quantity} onChange={event => onQuantityChange?.(Number(event.target.value))}>{[1, 2, 3, 5, 10].map(value => <option key={value} value={value}>{value}</option>)}</select></dd></div>
                  {costs.slice(-1).map(detail => <div key={detail.label}><dt>{detail.label}</dt><dd>{detail.value}</dd></div>)}
                </>}
          </dl>
          {balances.length > 0 && <dl className="shop-transaction__balances">
            {balances.map(detail => <div key={detail.label}><dt>{detail.label}</dt><dd>{detail.value}</dd></div>)}
          </dl>}
          {children}
        </div>
        <footer>
          {complete ? (
            <button type="button" className="shop-transaction__primary" onClick={onCancel}>閉じる</button>
          ) : <>
            <button type="button" className="shop-transaction__cancel" onClick={onCancel}>キャンセル</button>
            <button type="button" className="shop-transaction__primary" disabled={confirmDisabled} onClick={onConfirm}>{confirmLabel}</button>
          </>}
        </footer>
      </section>
      <style>{`
        .shop-transaction-overlay { position: absolute; inset: 0; z-index: 400; display: grid; place-items: center; padding: 16px; overflow: hidden; background: rgba(8,16,20,.76); font-family: var(--majak-font-family-ui); box-sizing: border-box; }
        .shop-transaction { width: min(560px, 100%); max-height: 100%; display: flex; flex-direction: column; overflow: hidden; color: #18312b; background: #f8f5ec; border: 1px solid #829287; box-shadow: 0 18px 54px rgba(0,0,0,.42); }
        .shop-transaction header { display: flex; align-items: center; justify-content: space-between; padding: 15px 18px; color: #fff; background: #174b43; }
        .shop-transaction header p { margin: 0; color: #d9bc62; font: 700 calc(10px * var(--majak-type-scale))/1 var(--majak-font-family-ui); letter-spacing: 1px; }
        .shop-transaction header h2 { margin: 2px 0 0; font-size: calc(25px * var(--majak-type-scale)); font-weight: 700; line-height: 1; letter-spacing: 0; }
        .shop-transaction header button { width: 36px; height: 36px; border: 1px solid rgba(255,255,255,.75); color: #fff; background: transparent; font-size: calc(22px * var(--majak-type-scale)); cursor: pointer; }
        .shop-transaction__body { padding: 18px; overflow: hidden; }
        .shop-transaction__item { display: grid; grid-template-columns: 104px minmax(0,1fr); gap: 14px; align-items: center; }
        .shop-transaction__image { height: 104px; display: grid; place-items: center; background: #e8ede4; }
        .shop-transaction__image img { max-width: 100%; max-height: 100%; object-fit: contain; }
        .shop-transaction__item span { color: #a06425; font: 700 calc(11px * var(--majak-type-scale))/1 var(--majak-font-family-ui); }
        .shop-transaction__item h3 { margin: 5px 0 8px; font-size: calc(16px * var(--majak-type-scale)); font-weight: 400; line-height: 1.35; }
        .shop-transaction__item p { margin: 3px 0; color: #5b6d66; font: calc(12px * var(--majak-type-scale))/1.35 var(--majak-font-family-ui); }
        .shop-transaction__details, .shop-transaction__balances { margin: 18px 0 0; border-top: 1px solid #c8d0c2; font-family: var(--majak-font-family-ui); }
        .shop-transaction__balances { background: #eaf0e5; }
        .shop-transaction__details div, .shop-transaction__balances div { display: flex; justify-content: space-between; gap: 16px; padding: 10px 2px; border-bottom: 1px solid #d6ddd2; }
        .shop-transaction__balances div { padding-inline: 10px; }
        .shop-transaction dt { color: #5a6e66; font: calc(11px * var(--majak-type-scale))/1.2 var(--majak-font-family-ui); } .shop-transaction dd { margin: 0; color: #173f36; font: 700 calc(15px * var(--majak-type-scale))/1.1 var(--majak-font-family-ui); text-align: right; }
        .shop-transaction select { min-width: 70px; padding: 3px; border: 1px solid #8e9c90; background: #fff; font: 700 calc(13px * var(--majak-type-scale))/1 var(--majak-font-family-ui); }
        .shop-transaction footer { display: flex; justify-content: flex-end; gap: 10px; padding: 14px 18px; border-top: 1px solid #c8d0c2; background: #e7ede4; }
        .shop-transaction footer button { border: 0; border-radius: 3px; padding: 10px 15px; font: 700 calc(12px * var(--majak-type-scale))/1 var(--majak-font-family-ui); cursor: pointer; white-space: nowrap; }
        .shop-transaction__cancel { color: #3e5249; background: transparent; border: 1px solid #87958a !important; }
        .shop-transaction__primary { color: #fff; background: #1b5b4d; } .shop-transaction__primary:disabled { color: #84908a; background: #d6ddd5; cursor: not-allowed; }
        @media (max-width: 700px), (max-height: 560px) {
          .shop-transaction-overlay { padding: 16px; }
          .shop-transaction { width: min(540px, 100%); max-height: calc(100dvh - 32px); }
          .shop-transaction__body { min-height: 0; padding: 12px; }
          .shop-transaction__details, .shop-transaction__balances { margin-top: 10px; }
          .shop-transaction__details { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); }
          .shop-transaction__balances { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); }
          .shop-transaction__details div, .shop-transaction__balances div { min-width: 0; gap: 4px; padding: 7px 5px; }
          .shop-transaction__details div:nth-child(odd), .shop-transaction__balances div:nth-child(odd) { border-right: 1px solid #d6ddd2; }
          .shop-transaction dt, .shop-transaction dd { min-width: 0; white-space: nowrap; }
          .shop-transaction footer { padding: 10px 12px; }
        }
      `}</style>
    </div>
  )
}