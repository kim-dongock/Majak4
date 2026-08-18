import { authApiUrl } from './auth'
import { refreshedGameAuthHeaders } from './authHeaders'

export type CurrencyHistoryCurrency = 'all' | 'gp' | 'mp' | 'dragon_orb'

export interface CurrencyHistoryItem {
  occurredAt: string
  currency: Exclude<CurrencyHistoryCurrency, 'all'>
  title: string
  amount: number
  balanceBefore: number
  balanceAfter: number
}

export interface CurrencyHistoryPage {
  items: CurrencyHistoryItem[]
  nextCursor: string | null
  hasMore: boolean
}

export async function getCurrencyHistory(
  currency: CurrencyHistoryCurrency,
  from: string,
  to: string,
  cursor: string | null,
): Promise<CurrencyHistoryPage> {
  const params = new URLSearchParams({ currency, from, to, limit: '30' })
  if (cursor) params.set('cursor', cursor)
  const response = await fetch(authApiUrl(`/api/player/currency-history?${params}`), {
    headers: await refreshedGameAuthHeaders(),
    cache: 'no-store',
  })
  if (!response.ok) throw new Error(`Unable to load currency history: ${response.status}`)
  return response.json() as Promise<CurrencyHistoryPage>
}