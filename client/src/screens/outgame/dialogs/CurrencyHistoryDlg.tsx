import { useEffect, useRef, useState } from 'react'
import {
  getCurrencyHistory,
  type CurrencyHistoryCurrency,
  type CurrencyHistoryItem,
} from '../../../api/currencyHistory'

interface CurrencyHistoryDlgProps {
  onClose: () => void
}

const CURRENCY_OPTIONS: Array<{ value: CurrencyHistoryCurrency; label: string }> = [
  { value: 'all', label: 'すべて' },
  { value: 'gp', label: 'GP' },
  { value: 'mp', label: 'MP' },
  { value: 'dragon_orb', label: '龍珠' },
]

function toDateInputValue(value: Date): string {
  const offsetValue = new Date(value.getTime() - value.getTimezoneOffset() * 60_000)
  return offsetValue.toISOString().slice(0, 10)
}

function defaultFromDate(): string {
  const value = new Date()
  value.setDate(value.getDate() - 6)
  return toDateInputValue(value)
}

function currencyLabel(currency: CurrencyHistoryItem['currency']): string {
  return currency === 'dragon_orb' ? '龍珠' : currency.toUpperCase()
}

function formatAmount(item: CurrencyHistoryItem): string {
  return `${item.amount >= 0 ? '+' : ''}${item.amount.toLocaleString('ja-JP')} ${currencyLabel(item.currency)}`
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat('ja-JP', {
    year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit',
  }).format(new Date(value))
}

export default function CurrencyHistoryDlg({ onClose }: CurrencyHistoryDlgProps) {
  const [currency, setCurrency] = useState<CurrencyHistoryCurrency>('all')
  const [from, setFrom] = useState(defaultFromDate)
  const [to, setTo] = useState(() => toDateInputValue(new Date()))
  const [items, setItems] = useState<CurrencyHistoryItem[]>([])
  const [nextCursor, setNextCursor] = useState<string | null>(null)
  const [hasMore, setHasMore] = useState(true)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const sentinelRef = useRef<HTMLDivElement | null>(null)
  const loadingRef = useRef(false)
  const requestVersionRef = useRef(0)

  const load = async (reset: boolean, selectedCurrency = currency, selectedFrom = from, selectedTo = to) => {
    if (loadingRef.current || (!reset && !hasMore)) return
    loadingRef.current = true
    setLoading(true)
    setError(null)
    const requestVersion = requestVersionRef.current
    try {
      const page = await getCurrencyHistory(selectedCurrency, selectedFrom, selectedTo, reset ? null : nextCursor)
      if (requestVersion !== requestVersionRef.current) return
      setItems(current => reset ? page.items : [...current, ...page.items])
      setNextCursor(page.nextCursor)
      setHasMore(page.hasMore)
    } catch (requestError) {
      if (requestVersion === requestVersionRef.current) setError((requestError as Error).message)
    } finally {
      if (requestVersion === requestVersionRef.current) {
        loadingRef.current = false
        setLoading(false)
      }
    }
  }

  const reload = (selectedCurrency = currency, selectedFrom = from, selectedTo = to) => {
    requestVersionRef.current += 1
    loadingRef.current = false
    setItems([])
    setNextCursor(null)
    setHasMore(true)
    void load(true, selectedCurrency, selectedFrom, selectedTo)
  }

  useEffect(() => { reload() }, [])

  useEffect(() => {
    const sentinel = sentinelRef.current
    if (!sentinel) return
    const observer = new IntersectionObserver(entries => {
      if (entries[0]?.isIntersecting) void load(false)
    }, { rootMargin: '240px' })
    observer.observe(sentinel)
    return () => observer.disconnect()
  }, [items.length, hasMore, loading])

  return <main className="majak-currency-history-overlay" aria-label="通貨履歴">
    <section className="majak-currency-history">
      <header>
        <div>
          <h1>通貨履歴</h1>
          <p>GP・MP・龍珠の獲得・使用履歴</p>
        </div>
        <button type="button" onClick={onClose}>ロビーに戻る</button>
      </header>
      <div className="majak-currency-history__filters">
        <fieldset>
          <legend>通貨</legend>
          {CURRENCY_OPTIONS.map(option => <label key={option.value}>
            <input
              type="radio"
              name="currency-history-currency"
              checked={currency === option.value}
              onChange={() => {
                setCurrency(option.value)
                reload(option.value)
              }}
            />
            <span>{option.label}</span>
          </label>)}
        </fieldset>
        <div className="majak-currency-history__dates">
          <label>開始日<input type="date" value={from} max={to} onChange={event => setFrom(event.target.value)} /></label>
          <span>〜</span>
          <label>終了日<input type="date" value={to} min={from} onChange={event => setTo(event.target.value)} /></label>
          <button type="button" onClick={() => reload()}>検索</button>
        </div>
      </div>
      <div className="majak-currency-history__list" aria-live="polite">
        {items.map((item, index) => <article key={`${item.occurredAt}-${item.currency}-${index}`}>
          <time>{formatDate(item.occurredAt)}</time>
          <span className={`majak-currency-history__currency is-${item.currency}`}>{currencyLabel(item.currency)}</span>
          <strong>{item.title}</strong>
          <b className={item.amount >= 0 ? 'is-earned' : 'is-spent'}>{formatAmount(item)}</b>
          <small>残高 {item.balanceAfter.toLocaleString('ja-JP')} {currencyLabel(item.currency)}</small>
        </article>)}
        {!loading && !error && items.length === 0 && <p className="majak-currency-history__message">この期間の履歴はありません。</p>}
        {error && <div className="majak-currency-history__error"><p>履歴を取得できませんでした。</p><button type="button" onClick={() => reload()}>再試行</button></div>}
        <div ref={sentinelRef} aria-hidden="true" />
        {loading && <p className="majak-currency-history__message">履歴を読み込んでいます...</p>}
        {!hasMore && items.length > 0 && <p className="majak-currency-history__message">すべての履歴を表示しました。</p>}
      </div>
    </section>
    <style>{`
      .majak-currency-history-overlay { --majak-popup-font-emphasis: var(--majak-text-md); --majak-popup-font-body: calc(13px * var(--majak-type-scale)); position: absolute; inset: 0; z-index: 500; display: block; box-sizing: border-box; overflow: hidden; background: #eef1e9; font-family: var(--majak-font-family-ui); }
      .majak-currency-history { height: 100%; display: flex; flex-direction: column; color: #203632; background: #fffdf8; }
      .majak-currency-history header { display: flex; align-items: center; justify-content: space-between; gap: 18px; padding: 16px max(24px, calc((100% - 1120px) / 2)); color: #fff; background: #174b43; }.majak-currency-history h1 { margin: 0; font-size: var(--majak-font-25); letter-spacing: 0; }.majak-currency-history header p { margin: 4px 0 0; color: rgba(255,255,255,.8); font-size: var(--majak-font-13); }.majak-currency-history header button { height: 34px; border: 1px solid rgba(255,255,255,.75); padding: 0 12px; color: #fff; background: transparent; font: 700 var(--majak-popup-font-body)/1 var(--majak-font-family-ui); cursor: pointer; white-space: nowrap; }
      .majak-currency-history__filters { display: flex; justify-content: space-between; gap: 14px; padding: 12px 16px; border-bottom: 1px solid #c8d0c2; background: #e7ede4; }.majak-currency-history fieldset { display: flex; align-items: center; gap: 12px; min-width: 0; margin: 0; padding: 0; border: 0; }.majak-currency-history legend { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); }.majak-currency-history fieldset label { display: inline-flex; align-items: center; gap: 4px; font-size: var(--majak-popup-font-body); white-space: nowrap; cursor: pointer; }.majak-currency-history__dates { display: flex; align-items: center; gap: 7px; }.majak-currency-history__dates label { display: flex; align-items: center; gap: 4px; color: #536660; font-size: var(--majak-font-12); white-space: nowrap; }.majak-currency-history__dates input { min-width: 120px; border: 1px solid #9eaba1; padding: 5px; color: #253d37; background: #fff; font: inherit; }.majak-currency-history__dates button, .majak-currency-history__error button { height: 30px; border: 0; padding: 0 12px; color: #fff; background: #1b5b4d; font: 700 var(--majak-popup-font-body)/1 var(--majak-font-family-ui); cursor: pointer; }
      .majak-currency-history__list { width: min(1120px, 100%); min-height: 0; flex: 1 1 auto; align-self: center; overflow: auto; border-inline: 1px solid #d9e0d7; }.majak-currency-history__list article { display: grid; grid-template-columns: 140px 58px minmax(0, 1fr) minmax(125px, auto); gap: 8px 12px; align-items: center; padding: 12px 16px; border-bottom: 1px solid #d9e0d7; }.majak-currency-history__list time, .majak-currency-history__list small { color: #687a72; font-size: var(--majak-font-12); }.majak-currency-history__currency { padding: 3px 5px; color: #195b4f; background: #dcebe1; font-size: var(--majak-font-12); font-weight: 700; text-align: center; }.majak-currency-history__currency.is-mp { color: #72512a; background: #f6e8bd; }.majak-currency-history__currency.is-dragon_orb { color: #735041; background: #f3ddd1; }.majak-currency-history__list strong { overflow: hidden; font-size: var(--majak-popup-font-body); text-overflow: ellipsis; white-space: nowrap; }.majak-currency-history__list b { text-align: right; font-size: var(--majak-popup-font-emphasis); white-space: nowrap; }.majak-currency-history__list b.is-earned { color: #19714f; }.majak-currency-history__list b.is-spent { color: #a13f2e; }.majak-currency-history__list small { grid-column: 4; text-align: right; }.majak-currency-history__message { margin: 18px; color: #68776f; text-align: center; font-size: var(--majak-font-13); }.majak-currency-history__error { display: flex; justify-content: center; align-items: center; gap: 12px; padding: 18px; color: #a33b2c; }.majak-currency-history__error p { margin: 0; }
      @media (max-width: 680px) { .majak-currency-history-overlay { position: fixed; }.majak-currency-history { height: 100dvh; }.majak-currency-history header { padding: 13px 12px; }.majak-currency-history h1 { font-size: var(--majak-font-17); }.majak-currency-history header p { font-size: var(--majak-font-10); }.majak-currency-history header button { height: 30px; padding-inline: 8px; font-size: var(--majak-font-11); }.majak-currency-history__filters { display: grid; }.majak-currency-history fieldset { justify-content: space-between; gap: 5px; }.majak-currency-history fieldset label { font-size: var(--majak-font-11); }.majak-currency-history__dates { display: grid; grid-template-columns: 1fr auto 1fr; gap: 6px; }.majak-currency-history__dates label, .majak-currency-history__dates input { font-size: var(--majak-font-10); }.majak-currency-history__dates label { display: grid; gap: 2px; }.majak-currency-history__dates input { min-width: 0; width: 100%; box-sizing: border-box; }.majak-currency-history__dates button, .majak-currency-history__error button { font-size: var(--majak-font-11); }.majak-currency-history__dates button { grid-column: 1 / -1; }.majak-currency-history__list { border-inline: 0; }.majak-currency-history__list article { grid-template-columns: 1fr auto; gap: 5px 8px; padding: 11px 12px; }.majak-currency-history__list time, .majak-currency-history__list small, .majak-currency-history__currency { font-size: var(--majak-font-10); }.majak-currency-history__list time { grid-column: 1; }.majak-currency-history__currency { grid-column: 2; grid-row: 1; }.majak-currency-history__list strong { grid-column: 1 / -1; font-size: var(--majak-font-12); }.majak-currency-history__list b { grid-column: 1; font-size: var(--majak-font-13); text-align: left; }.majak-currency-history__list small { grid-column: 2; text-align: right; }.majak-currency-history__message, .majak-currency-history__error { font-size: var(--majak-font-11); } }
    `}</style>
  </main>
}