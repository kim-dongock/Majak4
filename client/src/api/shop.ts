import { refreshedGameAuthHeaders } from './authHeaders'

declare const __API_BASE__: string | undefined
const API_BASE: string = (typeof __API_BASE__ !== 'undefined' ? __API_BASE__ : '') ?? ''

export interface CashProduct {
  productId: string
  displayName: string
  cashAmount: number
  priceJpy: number
}

export interface ConvenienceShopItem {
  itemCode: string
  sellCode: string
  itemName: string
  cashPrice: number
  description: string
}

export async function getCashProducts(): Promise<CashProduct[]> {
  const response = await fetch(`${API_BASE}/api/shop/cash-products`, {
    headers: await refreshedGameAuthHeaders(),
  })
  if (!response.ok) throw new Error(`GET /api/shop/cash-products failed: ${response.status}`)
  return response.json() as Promise<CashProduct[]>
}

export async function getConvenienceItems(): Promise<ConvenienceShopItem[]> {
  const response = await fetch(`${API_BASE}/api/shop/convenience-items`, {
    headers: await refreshedGameAuthHeaders(),
  })
  if (!response.ok) throw new Error(`GET /api/shop/convenience-items failed: ${response.status}`)
  return response.json() as Promise<ConvenienceShopItem[]>
}