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
    <div className="majak-popup-overlay" role="dialog" aria-modal="true" aria-label={title}>
      <section className="majak-popup-panel shop-transaction">
        <header className="majak-popup-titlebar">
          <div>
            <h2>{title}</h2>
          </div>
          <button className="majak-popup-titlebar__close" type="button" onClick={onCancel} aria-label="閉じる">×</button>
        </header>
        <div className="majak-popup-body shop-transaction__body">
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
        <footer className="majak-popup-actions">
          {complete ? (
            <button type="button" className="shop-transaction__primary" onClick={onCancel}>閉じる</button>
          ) : <>
            <button type="button" className="shop-transaction__cancel" onClick={onCancel}>キャンセル</button>
            <button type="button" className="shop-transaction__primary" disabled={confirmDisabled} onClick={onConfirm}>{confirmLabel}</button>
          </>}
        </footer>
      </section>
    </div>
  )
}