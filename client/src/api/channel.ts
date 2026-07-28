/**
 * AP-04 §8 準拠: チャンネル関連 REST API
 *
 * VITE_API_BASE_URL:
 *   development : "" (空文字) → Vite dev server プロキシ (/api/...) 経由
 *   alpha       : "http://alpha-game.majak2.jp"
 *   production  : "https://game.majak2.jp"
 */
import { gameAuthHeaders } from './authHeaders'

// Vite がビルド時に VITE_API_BASE_URL を文字列リテラルに置換する。
// 開発時は Vite dev server プロキシを使うため空文字でよい。
declare const __API_BASE__: string | undefined
const API_BASE: string = (typeof __API_BASE__ !== 'undefined' ? __API_BASE__ : '') ?? ''

// ── 内部ヘルパー ──────────────────────────────────────────────
async function apiPost(path: string, body: unknown): Promise<void> {
  const res = await fetch(`${API_BASE}${path}`, {
    method: 'POST',
    headers: gameAuthHeaders({ 'Content-Type': 'application/json' }),
    body: JSON.stringify(body),
  })
  if (!res.ok) throw new Error(`POST ${path} failed: ${res.status}`)
}

async function apiGet<T>(path: string): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    headers: gameAuthHeaders(),
  })
  if (!res.ok) throw new Error(`GET ${path} failed: ${res.status}`)
  return res.json() as Promise<T>
}

// ── チャンネルサーバー URL 取得 ───────────────────────────────
export async function getChannelServerUrl(chanelId: string): Promise<string> {
  const data = await apiGet<{ serverUrl: string }>(
    `/api/channel/${encodeURIComponent(chanelId)}/server`)
  return data.serverUrl
}

// ── チャンネル入室 (Redis に登録) ─────────────────────────────
export async function enterChannel(
  chanelId: string,
  pix: string,
  nickname: string,
  rating: number,
  sex: string,
  avatarId: string,
): Promise<void> {
  await apiPost(`/api/channel/${encodeURIComponent(chanelId)}/enter`,
    { pix, nickname, rating, sex, avatarId })
}

// ── チャンネル退室 (Redis から削除) ──────────────────────────
export async function leaveChannel(chanelId: string, pix: string): Promise<void> {
  await apiPost(`/api/channel/${encodeURIComponent(chanelId)}/leave`, { pix })
}

// ── チャンネルメンバーリスト取得 (Redis) ─────────────────────
export interface ChannelMember {
  pix: string
  nickname: string
  rating:   number
  sex:      'male' | 'female'
  avatarId: string
}

export async function getChannelMembers(chanelId: string): Promise<ChannelMember[]> {
  const list = await apiGet<Array<{
    pix: string; nickname: string; rating: number; sex: string; avatarId: string
  }>>(`/api/channel/${encodeURIComponent(chanelId)}/members`)
  return list.map(m => ({
    ...m,
    sex: (m.sex === 'female' ? 'female' : 'male') as 'male' | 'female',
  }))
}

// ── チャンネルルームリスト取得 (ゲームサーバーメモリ直接) ────
export interface RoomEntry {
  roomId:     number
  title:      string
  isPrivate:  boolean
  memberCnt:  number
  memberMax:  number
  maxViewer?: number
  roomOption: string
  serverUrl:  string   // このルームが存在するゲームサーバー URL
}

export async function getChannelRooms(chanelId: string): Promise<RoomEntry[]> {
  return apiGet<RoomEntry[]>(`/api/channel/${encodeURIComponent(chanelId)}/rooms`)
}

export interface ContinueRoomEntry {
  found: boolean
  pix?: string
  roomId?: number
  chanelId?: string
  channelId?: string
  title?: string
  serverUrl?: string
  roomOption?: string
  updatedAt?: string
}

export async function getPlayerContinueRoom(pix: string): Promise<ContinueRoomEntry | null> {
  const data = await apiGet<ContinueRoomEntry>(`/api/player/continue-room?pix=${encodeURIComponent(pix)}`)
  return data.found ? data : null
}

// ── ルーム数最小サーバー URL 取得 (ルーム作成時に使用) ───────
export async function getBestServer(): Promise<string> {
  const data = await apiGet<{ serverUrl: string }>('/api/room/best-server')
  return data.serverUrl
}

// ── チャンネル一覧取得 (GET /api/channels) ──────────────────
// CMJSelLobbyWnd の MSGID_GET_NEXT_USER_CNT 相当
// Oracle CHANELMAST から全チャンネルの情報と在室人数を返す
export interface ChannelInfo {
  chanelId:  string
  subId:     string
  chanelName: string
  maxMember: number
  maxRoom:   number
  chanelType: number
  unitMoney: number
  memberCnt: number
  usedRoom:  number
}

export async function getChannels(): Promise<ChannelInfo[]> {
  return apiGet<ChannelInfo[]>('/api/channels')
}

