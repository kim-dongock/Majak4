import { refreshedGameAuthHeaders } from './authHeaders'

declare const __API_BASE__: string | undefined
const API_BASE = ((typeof __API_BASE__ !== 'undefined' ? __API_BASE__ : '') ?? '').replace(/\/$/, '')

export interface PaifuArchiveMember {
  name: string
  title: string
  rating: number
  result: string
}

export interface PaifuArchiveSummary {
  archiveId: number
  playedAt: string
  roomName: string
  roomOption: string
  result: string
  members: PaifuArchiveMember[]
  packetCount: number
}

export interface PaifuArchiveFilter {
  from?: string
  to?: string
  room?: string
  member?: string
  result?: string
  matchKind?: 'normal' | 'tournament'
}

function queryString(filter: PaifuArchiveFilter): string {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(filter)) {
    if (value) params.set(key, value)
  }
  const query = params.toString()
  return query ? `?${query}` : ''
}

export async function getPaifuArchives(filter: PaifuArchiveFilter = {}): Promise<PaifuArchiveSummary[]> {
  const response = await fetch(`${API_BASE}/api/player/paifu${queryString(filter)}`, {
    headers: await refreshedGameAuthHeaders(),
  })
  if (!response.ok) throw new Error(`Paifu archive request failed: ${response.status}`)
  return response.json() as Promise<PaifuArchiveSummary[]>
}

export async function getPaifuReplayPayload(archiveId: number): Promise<unknown> {
  const response = await fetch(`${API_BASE}/api/player/paifu/${archiveId}/replay`, {
    method: 'POST',
    headers: await refreshedGameAuthHeaders(),
  })
  if (!response.ok) throw new Error(`Paifu replay request failed: ${response.status}`)
  return response.json()
}
