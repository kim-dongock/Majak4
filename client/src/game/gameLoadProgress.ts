export const GAME_LOAD_PROGRESS_EVENT = 'majak:game-load-progress'

export const GAME_LOAD_STEPS = [
  { id: 'server', label: 'サーバーに接続中' },
  { id: 'channel', label: 'チャンネル情報を確認中' },
  { id: 'room', label: 'ルームに再入室中' },
  { id: 'resources', label: 'リソースダウンロード中' },
  { id: 'scene', label: 'ゲーム画面を初期化中' },
  { id: 'tiles', label: '牌情報を復元中' },
  { id: 'history', label: '対局履歴を復元中' },
  { id: 'sync', label: '対局状態を同期中' },
  { id: 'ready', label: 'ゲーム再開準備中' },
] as const

export type GameLoadStep = typeof GAME_LOAD_STEPS[number]['id']

export function emitGameLoadProgress(step: GameLoadStep, details: Record<string, unknown> = {}) {
  window.dispatchEvent(new CustomEvent(GAME_LOAD_PROGRESS_EVENT, { detail: { step, ...details } }))
}