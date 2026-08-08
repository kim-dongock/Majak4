import { refreshedGameAuthHeaders } from './authHeaders'

declare const __API_BASE__: string | undefined
const API_BASE = ((typeof __API_BASE__ !== 'undefined' ? __API_BASE__ : '') ?? '').replace(/\/$/, '')

export interface CollectionTitle {
  titleId: string
  titleName: string
  isEquipped: boolean
}

export interface PlayerCollection {
  majakTitles: CollectionTitle[]
  trickTitles: CollectionTitle[]
  equippedMajakTitle: string
  equippedTrickTitle: string
}

async function readCollectionResponse(response: Response): Promise<PlayerCollection> {
  if (!response.ok) throw new Error(`Collection request failed: ${response.status}`)
  return response.json() as Promise<PlayerCollection>
}

export async function getPlayerCollection(): Promise<PlayerCollection> {
  return readCollectionResponse(await fetch(`${API_BASE}/api/player/collection`, {
    headers: await refreshedGameAuthHeaders(),
  }))
}

export async function equipCollectionTitle(category: 'majak' | 'trick', titleId: string | null): Promise<PlayerCollection> {
  return readCollectionResponse(await fetch(`${API_BASE}/api/player/collection/equip`, {
    method: 'POST',
    headers: await refreshedGameAuthHeaders({ 'Content-Type': 'application/json' }),
    body: JSON.stringify({ category, titleId }),
  }))
}