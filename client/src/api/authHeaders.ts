import { useAuthStore } from '../store/authStore'
import { getRememberedGameAccessToken, rememberGameAccessToken } from './gameAuthToken'

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
