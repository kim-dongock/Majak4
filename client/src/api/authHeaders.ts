import { useAuthStore } from '../store/authStore'
import { getRememberedGameAccessToken, rememberGameAccessToken } from './gameAuthToken'
import { refreshLogin } from './auth'

export function gameAuthHeaders(extra?: HeadersInit): HeadersInit {
  const token = getGameAccessToken()
  return token
    ? { ...extra, Authorization: `Bearer ${token}` }
    : { ...extra }
}

export function getGameAccessToken(): string {
  const player = useAuthStore.getState().player
  const accessToken = player?.accessToken
  rememberGameAccessToken(player?.pix, accessToken)
  return accessToken || getRememberedGameAccessToken(player?.pix)
}

function isAccessTokenExpiringSoon(token: string): boolean {
  try {
    const payload = token.split('.')[1]
    if (!payload) return true
    const normalized = payload.replace(/-/g, '+').replace(/_/g, '/')
    const decoded = atob(normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '='))
    const exp = JSON.parse(decoded).exp
    return typeof exp !== 'number' || exp * 1000 <= Date.now() + 60_000
  } catch {
    return true
  }
}

/** Returns Authorization headers after refreshing a missing or expiring game JWT. */
export async function refreshedGameAuthHeaders(extra?: HeadersInit): Promise<HeadersInit> {
  let token = getGameAccessToken()
  if (token && !isAccessTokenExpiringSoon(token)) return { ...extra, Authorization: `Bearer ${token}` }

  const refreshedPlayer = await refreshLogin()
  if (refreshedPlayer?.accessToken) {
    useAuthStore.getState().setPlayer(refreshedPlayer)
    token = refreshedPlayer.accessToken
  }

  return token ? { ...extra, Authorization: `Bearer ${token}` } : { ...extra }
}
