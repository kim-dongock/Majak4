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
  const listRef = useRef<HTMLDivElement | null>(null)
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
    }, { root: listRef.current, rootMargin: '240px' })
    observer.observe(sentinel)
    return () => observer.disconnect()
  }, [items.length, hasMore, loading])

  return <div className="majak-popup-overlay" role="presentation">
    <section className="majak-popup-panel majak-currency-history" role="dialog" aria-modal="true" aria-labelledby="currency-history-title">
      <header className="majak-popup-titlebar">
        <span id="currency-history-title">通貨履歴</span>
        <button className="majak-popup-titlebar__close" type="button" onClick={onClose} aria-label="閉じる">×</button>
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
      <div ref={listRef} className="majak-currency-history__list" aria-live="polite">
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
  </div>
}