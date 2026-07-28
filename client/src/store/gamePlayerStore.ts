/**
 * ゲームプレイヤーデータストア
 * チャンネル未入室状態でのコイン・称号表示 (DrawMemberInfo 相当) に使用。
 * channel:entered 受信時にも同データを更新して各画面から参照できるようにする。
 */
import { create } from 'zustand'
import { gameAuthHeaders } from '../api/authHeaders'

export interface GamePlayerData {
  gamMoney:   number
  slevel:     string
  nlevel:     number
  rating:     number
  trickTitle: string
  majakTitle: string
  gemCount:   number
}

interface GamePlayerState {
  data:       GamePlayerData | null
  loading:    boolean
  /** /api/player/profile からデータを取得してストアに保存する */
  fetchProfile: (pix: string) => Promise<void>
  /** channel:entered などサーバーイベントで更新する */
  setData: (data: Partial<GamePlayerData>) => void
  /** ログアウト時に前のアカウントの表示データを破棄する */
  clearData: () => void
}

export const useGamePlayerStore = create<GamePlayerState>((set, get) => ({
  data:    null,
  loading: false,

  fetchProfile: async (pix: string) => {
    if (get().loading) return
    set({ loading: true })
    try {
      const res = await fetch(`/api/player/profile?pix=${encodeURIComponent(pix)}`, {
        headers: gameAuthHeaders(),
      })
      if (!res.ok) return
      const json = await res.json() as GamePlayerData
      set({ data: json })
    } catch {
      // ネットワークエラーは無視 (コイン未表示のまま)
    } finally {
      set({ loading: false })
    }
  },

  setData: (partial) =>
    set(s => ({ data: s.data ? { ...s.data, ...partial } : (partial as GamePlayerData) })),
  clearData: () => set({ data: null, loading: false }),
}))
