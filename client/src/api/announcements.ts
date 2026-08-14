import { authApiUrl } from './auth'
import { refreshedGameAuthHeaders } from './authHeaders'

export interface GameAnnouncement {
  announcementId: number
  title: string
  body: string
  isPublished: boolean
  isStartup: boolean
  publishedAt: string | null
  createdAt: string
  updatedAt: string
}

export async function getStartupAnnouncement(): Promise<GameAnnouncement | null> {
  const response = await fetch(authApiUrl('/api/announcements/startup'), {
    headers: await refreshedGameAuthHeaders(),
    cache: 'no-store',
  })
  if (response.status === 204 || response.status === 401) return null
  if (!response.ok) throw new Error(`Unable to load announcement: ${response.status}`)
  return response.json() as Promise<GameAnnouncement>
}

export interface AnnouncementPage {
  items: GameAnnouncement[]
  hasMore: boolean
}

export async function getAnnouncements(offset: number, limit = 20): Promise<AnnouncementPage> {
  const response = await fetch(authApiUrl(`/api/announcements?offset=${offset}&limit=${limit}`), {
    headers: await refreshedGameAuthHeaders(),
    cache: 'no-store',
  })
  if (!response.ok) throw new Error(`Unable to load announcements: ${response.status}`)
  return response.json() as Promise<AnnouncementPage>
}