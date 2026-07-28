/**
 * 認証ステート管理 (Zustand)
 * AP-02 §1 準拠: Hangame ログインクッキーから取得したプレイヤー情報を保持する。
 */
import { create } from 'zustand'
import type { MajakPlayer } from '../api/auth'
import { clearRememberedGameAccessToken, rememberGameAccessToken } from '../api/gameAuthToken'

type AuthStatus = 'idle' | 'loading' | 'ok' | 'error' | 'login_required'

interface AuthState {
  status:  AuthStatus
  player:  MajakPlayer | null
  error:   string | null
  setLoading: () => void
  setPlayer:  (player: MajakPlayer) => void
  setError:   (msg: string) => void
  requireLogin: () => void
}

export const useAuthStore = create<AuthState>(set => ({
  status:  'idle',
  player:  null,
  error:   null,

  setLoading: () => set({ status: 'loading', error: null }),
  setPlayer:  (player) => set(state => {
    const nextPlayer = !player.accessToken && state.player?.pix === player.pix && state.player.accessToken
      ? { ...player, accessToken: state.player.accessToken }
      : player
    if (nextPlayer.accessToken) rememberGameAccessToken(nextPlayer.pix, nextPlayer.accessToken)
    else if (state.player?.pix !== nextPlayer.pix) clearRememberedGameAccessToken()
    return { status: 'ok', player: nextPlayer, error: null }
  }),
  setError:   (error)  => set({ status: 'error', error }),
  requireLogin: () => {
    clearRememberedGameAccessToken()
    set({ status: 'login_required', player: null, error: null })
  },
}))
