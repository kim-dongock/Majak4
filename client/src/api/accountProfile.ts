import { authApiUrl } from './auth'
import { refreshedGameAuthHeaders } from './authHeaders'

export interface AccountProfileUpdate {
  birthYear: number
  avatarId: string
  userColor: string
}

export async function updateAccountProfile(profile: AccountProfileUpdate): Promise<AccountProfileUpdate> {
  const request = async (forceRefresh = false) => fetch(authApiUrl('/api/player/account-profile'), {
    method: 'PATCH',
    headers: await refreshedGameAuthHeaders({ 'Content-Type': 'application/json' }, forceRefresh),
    body: JSON.stringify(profile),
  })

  let response = await request()
  if (response.status === 401) response = await request(true)
  if (!response.ok) {
    const error = await response.json().catch(() => null) as { error?: string } | null
    throw new Error(error?.error ?? `HTTP ${response.status}`)
  }

  return await response.json() as AccountProfileUpdate
}