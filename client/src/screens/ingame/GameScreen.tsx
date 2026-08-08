/**
 * GameScreen — React 側の Phaser マウントコンテナ
 * Phaser.Game を <div> に mount / unmount する
 *
 * ── 登録 SignalR イベント ────────────────────────────────────────────
 *   room:game_report  → CMJHanRes 相当の最終結果画面を表示
 *   playing           → CMJKyoRes 相当の1局終了結果を表示
 *   mjkc19e/mjkc22e/mjkc23e → CMJSlideAnnounce 相当のスライド公告を表示
 * ────────────────────────────────────────────
 */
import { useEffect, useRef, useState } from 'react'
import { createGame, destroyGame, GAME_HEIGHT, GAME_WIDTH } from '../../game/GameInstance'
import * as SignalR from '../../api/signalr'
import HanRes, { type HanResPlayer } from './HanRes'
import { LegacyKyoRes, type KyoResData } from './KyoRes.tsx'
import SlideAnnounce, { type SlideAnnounceData } from './SlideAnnounce'
import ViewerListWnd, { type ViewerEntry } from './ViewerListWnd'
import AskEndDlg from '../outgame/dialogs/AskEndDlg'
import GameInviteDialog from './GameInviteDialog'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { useAuthStore } from '../../store/authStore'
import { useCustomSkinStore } from '../../store/customSkinStore'
import { getAvatarUrl, getDefaultAvatarUrl } from '../../utils/resources'
import { getChannelServerUrl } from '../../api/channel'
import { getTabSessionId } from '../../utils/tabSession'
import { GAME_AUTO_CONTROL_EVENT, GAME_KYOKU_STARTED_EVENT } from '../../game/autoControl'
import { playMajakChat, playMajakSfx, playMajakSid, SID_DRAW, SID_EXIT, SID_JOIN, stopMajakBgm } from '../../utils/majakSound'
import { applyTengokuTextColor, getLegacyBoardSoundSkinId, getLegacyRoomPalette, isTengokuBoardSkin } from '../../utils/legacySkinPalette'
import { useDesktopScreenScale } from '../../hooks/useDesktopScreenScale'

const CMD_GAME_PLAY = 'playing'
const CMD_AUTO_START = 'mjkc4e'
const CMD_GET_TITLE = 'mjkc19e'
const CMD_GET_GEM = 'mjkc22e'
const CMD_YAKUMAN_BONUS = 'mjkc23e'
const CMD_USE_EMOTICON = 'mjkc24e'

const KEY_COUNT = 'k25e'
const KEY_MEMBER_ID = 'k3e'
const KEY_TITLE_TYPE = 'mjkk48e'
const KEY_TITLE_CODE = 'mjkk49e'
const KEY_TITLE_NAME = 'mjkk50e'
const KEY_YAKU_NAME = 'mjkk62e'
const KEY_EMOTICON_ID = 'mjkk63e'
const KEY_TOURNAMENT_TOTAL_REPORT = 'mjkk97e'
const KEY_GEM_GAME = 'mjkk56e'
const ASK_END_SET_EVENT = 'majak:ask-end-set'
const KYO_RESULT_ACTION_EVENT = 'majak:kyo-result-action'
const GAME_STATUS_EVENT = 'majak:game-status'
const GAME_SYNC_EVENT = 'majak:game-sync'
const PAIFU_ROTATE_EVENT = 'majak:paifu-rotate'
const PAIFU_HAND_OPEN_EVENT = 'majak:paifu-hand-open'
const MAX_GAME_LOG_MESSAGES = 200
const GAME_CALL_AVATAR_EVENT = 'majak:call-avatar'
const GAME_STATE_STORAGE_KEY = 'majak:last-game-state'
const ACT_PAS = 1
const ACT_CHI = 2
const ACT_PON = 3
const ACT_KAN = 4
const ACT_RON = 5
const ACT_ANK = 7
const ACT_CHA = 8
const ACT_RIC = 9
const ACT_TAO = 10
const ACT_TSU = 11
const ACT_HUA = 12
const PIN_RON = 0
const PIN_TSU = 1
const PIN_NON = 2
const PIN_TAO = 3
const PIN_HOR = 4
const PIN_HOA = 5
const PIN_KAN = 6
const PIN_RIC = 7
const PIN_FON = 8
const PIN_NAG = 9
const IMG = '/assets/images/game'
const EMOTICON_COUNT = 6
const EMOTICON_FRAME_MS = 33
const EMOTICON_LEGACY_IDS = [1, 3, 5, 7, 8, 11]
const EMOTICON_POS = [
  { x: 14, y: 456, dir: 'r' as const },
  { x: 521, y: 456, dir: 'l' as const },
  { x: 521, y: 2, dir: 'l' as const },
  { x: 14, y: 2, dir: 'r' as const },
]

interface ChatMsg {
  id: string
  name: string
  text: string
  color?: string
  bold?: boolean
}

interface ActiveEmoticon {
  id: string
  pix: string
  type: number
  loc: number
  startedAt: number
}

interface EmoticonStep {
  frame: number
  alpha: number
}

const LEGACY_PROXY_GUIDE = [
  '接続が切れた人は以後すべてツモ切りとなります。',
  '再接続すると対局中に復帰できます。',
  '成績は対局終了後の成績がそのまま反映されます。',
]
const LEGACY_TOURNAMENT_LINEOFF_GUIDE = [
  '接続が切れた人は以後すべてツモ切りとなります。',
  'トーナメント戦の落ち戻りはトーナメントロビーへお越しください。',
]
const LEGACY_GEM_GAME_STATUS = [
  '',
  'この対戦は龍珠獲得戦になります。',
  'この対戦は龍珠獲得戦BIGになります。',
]

function asNumber(value: unknown, fallback = 0): number {
  const n = Number(value)
  return Number.isFinite(n) ? n : fallback
}

function gemGameStatusText(value: unknown): string | null {
  const gemGame = asNumber(value, 0)
  return LEGACY_GEM_GAME_STATUS[gemGame] ?? null
}

function extractSubId(channelId?: string) {
  const id = channelId ?? ''
  return id.length >= 11 ? id.substring(6, 11) : id
}

function isDaniChannel(channelId?: string) {
  return extractSubId(channelId)[2] === 'G'
}

function isReplayChannel(channelId?: string) {
  return extractSubId(channelId)[2] === 'V'
}

const OPTION_ICON = {
  set: `${IMG}/mj_opt_0.png`,
  kui: `${IMG}/mj_opt_3.png`,
  uma: `${IMG}/mj_opt_1.png`,
  ron: `${IMG}/mj_optron.png`,
  red: `${IMG}/mj_opt_5.png`,
  spd: `${IMG}/mj_opt_4.png`,
  opn: `${IMG}/mj_opt_6.png`,
  cht: `${IMG}/mj_opt_7.png`,
  ach: `${IMG}/mj_opt_8.png`,
} as const

function readOptionDigit(option: string | undefined, index: number, fallback: number) {
  const char = option?.charAt(index) ?? ''
  return /^\d$/.test(char) ? Number(char) : fallback
}

function optionSprite(src: string, value: number, x: number, y: number, maxFrame = 8) {
  const frame = Math.max(0, Math.min(maxFrame, value))
  return (
    <div
      key={`${src}-${x}-${y}`}
      style={{
        position: 'absolute',
        left: x,
        top: y,
        width: 17,
        height: 17,
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-17 * frame}px 0`,
        backgroundRepeat: 'no-repeat',
        pointerEvents: 'none',
      }}
    />
  )
}

interface GameLocationState {
  channelId?: string
  lobbyId?: string
  roomId?: string
  roomTitle?: string
  roomOption?: string
  serverUrl?: string
  myOdr?: number
  isViewer?: boolean
  customBgId?: number
  customBoardType?: number
  customHaiId?: number
  skipInitialRoomEnter?: boolean
  viewers?: ViewerEntry[]
  players?: GamePlayerEntry[]
}

interface GamePlayerEntry {
  playerId: string
  name: string
  rating: number
  pos: 0 | 1 | 2 | 3
  avatarId?: string
  sex?: string
  slevel?: string
  point?: number
  majakTitle?: number
  trickTitle?: number
  customCostume?: number
  customCostumeType?: number
}

interface ChannelMemberEntry {
  pix: string
  name: string
  rating: number
  slevel?: string
  location?: string
  roomId?: number
}

interface ActiveCallAvatar {
  id: string
  avatarUrl: string
  fallbackAvatarUrl: string
  x: number
  y: number
  w: number
  h: number
}

function readTitleCode(value: unknown): number {
  if (typeof value === 'number') return Number.isFinite(value) ? value : 0
  const text = String(value ?? '')
  if (!text) return 0
  const direct = Number(text)
  if (Number.isFinite(direct)) return direct
  const match = text.match(/mjk([tsc])(\d{3})/i)
  if (!match) return 0
  const code = Number(match[2])
  return match[1].toLowerCase() === 'c' ? code + 1000 : code
}

function mergePlayers(prev: GamePlayerEntry[], next: GamePlayerEntry[]): GamePlayerEntry[] {
  const byId = new Map(prev.map(player => [player.playerId, player]))
  for (const player of next) {
    const old = byId.get(player.playerId)
    if (!old) {
      byId.set(player.playerId, player)
      continue
    }
    byId.set(player.playerId, {
      ...old,
      pos: player.pos,
      name: player.name || old.name,
      rating: player.rating || old.rating,
      point: player.point ?? old.point,
      avatarId: player.avatarId || old.avatarId,
      sex: player.sex || old.sex,
      slevel: player.slevel || old.slevel,
      majakTitle: player.majakTitle || old.majakTitle,
      trickTitle: player.trickTitle || old.trickTitle,
      customCostume: player.customCostume || old.customCostume,
      customCostumeType: player.customCostumeType || old.customCostumeType,
    })
  }
  return [...byId.values()].sort((a, b) => a.pos - b.pos)
}

function readGamePlayer(data: Record<string, unknown>): GamePlayerEntry | null {
  const playerId = String(data.k3e ?? data.pix ?? data.playerId ?? '')
  if (!playerId) return null
  const pos = Number(data.engineOrder ?? data.odr ?? data.k58e ?? data.playerPos ?? data.seatPos ?? data.pos ?? -1)
  if (pos < 0 || pos > 3) return null
  return {
    playerId,
    name: String(data.mjkk34e ?? data.k8e ?? data.nickName ?? data.nickname ?? data.name ?? ''),
    rating: Number(data.k31e ?? data.rating ?? 0),
    pos: pos as 0 | 1 | 2 | 3,
    avatarId: data.k7e != null || data.avatarId != null ? String(data.k7e ?? data.avatarId) : undefined,
    sex: data.k11e != null || data.sex != null ? String(data.k11e ?? data.sex) : undefined,
    slevel: String(data.k32e ?? data.slevel ?? data.dan ?? ''),
    point: data.point != null || data.setTen != null ? Number(data.point ?? data.setTen) : undefined,
    majakTitle: readTitleCode(data.mjkk47e ?? data.majakTitle),
    trickTitle: readTitleCode(data.mjkk46e ?? data.trickTitle),
    customCostume: Number(data.mjkk136e ?? data.customCostume ?? data.charaId ?? 0),
    customCostumeType: Number(data.mjkk137e ?? data.customCostumeType ?? data.charaType ?? 0),
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value != null && typeof value === 'object'
}

function statusMemberName(players: GamePlayerEntry[], odr: number): string {
  const player = players.find(item => item.pos === odr)
  return player?.name || player?.playerId || String(odr)
}

function actionStatusText(action: number, memberName: string): string | null {
  switch (action) {
    case ACT_RON: return `ロン：${memberName}`
    case ACT_TSU: return `ツモ：${memberName}`
    case ACT_PON: return `ポン：${memberName}`
    case ACT_CHI: return `チー：${memberName}`
    case ACT_KAN:
    case ACT_ANK:
    case ACT_CHA:
      return `カン：${memberName}`
    case ACT_RIC: return `リーチ：${memberName}`
    case ACT_TAO: return '流局'
    case ACT_HUA: return `花：${memberName}`
    default: return null
  }
}

function signed(value: number): string {
  return `${value >= 0 ? '+' : ''}${Math.trunc(value)}`
}

function kyoPlayerStatusName(data: KyoResData, odr: number): string {
  const player = data.players[odr]
  return player?.name || player?.pix || String(odr)
}

function kyoEndPinStatus(data: KyoResData): string | null {
  const pin = Number(data.pinType)
  if (pin === PIN_RON) {
    const selected = Number(data.selectedOdr ?? -1)
    const winnerOdr = selected >= 0 && data.players[selected]?.isHora
      ? selected
      : data.players.findIndex(player => player.isHora)
    const hojuOdr = data.players.findIndex(player => player.isHoju)
    if (winnerOdr < 0) return null
    return `ロン：${kyoPlayerStatusName(data, winnerOdr)}\n放銃：${kyoPlayerStatusName(data, hojuOdr)}`
  }
  if (pin === PIN_TSU) {
    const winnerOdr = data.players.findIndex(player => player.isHora)
    if (winnerOdr < 0) return null
    return `ツモ：${kyoPlayerStatusName(data, winnerOdr)}`
  }
  switch (pin) {
    case PIN_NON:
    case PIN_HOA:
    case PIN_NAG:
      return '流局（荒牌）'
    case PIN_TAO:
      return '流局（九種幺九倒牌）'
    case PIN_HOR:
      return '流局（三家和）'
    case PIN_KAN:
      return '流局（四開槓）'
    case PIN_RIC:
      return '流局（四家立直）'
    case PIN_FON:
      return '流局（四風子連打）'
    default:
      return null
  }
}

function kyoEndStatusLines(data: KyoResData): string[] {
  const lines: string[] = []
  const pinStatus = kyoEndPinStatus(data)
  if (pinStatus) lines.push(...pinStatus.split('\n'))

  if (Number(data.pinType) === PIN_NAG) {
    for (let odr = 0; odr < data.players.length; odr++) {
      if (data.players[odr]?.isNagashiMangan) lines.push(`流し満貫：${kyoPlayerStatusName(data, odr)}`)
    }
  }

  for (let odr = 0; odr < data.players.length; odr++) {
    const player = data.players[odr]
    if (!player) continue
    const balance = Number(player.tenBal ?? 0)
    const tip = Number(player.tipBal ?? 0)
    if (balance === 0 && tip === 0) continue
    const base = `${kyoPlayerStatusName(data, odr)}：${signed(balance)}点`
    lines.push(tip !== 0 ? `${base}(ﾁｯﾌﾟ ${signed(tip)}枚)` : base)
  }

  lines.push('----')
  return lines
}

function gameEndStatusLines(data: Record<string, unknown>): string[] {
  const rawEnd = String(data.gameEnd ?? '').toLowerCase()
  const endValue = asNumber(data.gameEndValue, -1)
  let endText: string | null = null
  if (rawEnd === 'set' || endValue === 1) {
    endText = Boolean(data.isHanchanRule) ? '----半荘終了----' : '----東風戦終了----'
  } else if (rawEnd === 'stop' || endValue === 2) {
    endText = '----対局中断----'
  } else if (rawEnd === 'tobi' || endValue === 3) {
    endText = '----ハコテン終了----'
  } else if (rawEnd === 'hora' || endValue === 4) {
    endText = '----アガリ止め終了----'
  }
  return endText ? [endText, '----対局終了----'] : []
}

function keepCurrentPlayerOrder(next: GamePlayerEntry[], current: GamePlayerEntry[]): GamePlayerEntry[] {
  const currentById = new Map(current.map(player => [player.playerId, player]))
  return next.map(player => {
    const old = currentById.get(player.playerId)
    return old ? { ...player, pos: old.pos } : player
  })
}

function buildEnterChannelPayload(channelId: string, player: ReturnType<typeof useAuthStore.getState>['player']) {
  const subId = channelId.length >= 11 ? channelId.substring(6, 11) : channelId
  const pix = player?.pix ?? ''
  const nickname = player?.name ?? ''
  const avatarId = player?.avatarId ?? ''
  return {
    gameId: 'MAJAK4',
    k22e: 'MAJAK4',
    subId,
    k23e: subId,
    channelId,
    k24e: channelId,
    pix,
    k3e: pix,
    nickname,
    name: nickname,
    k8e: nickname,
    avatarId,
    k7e: avatarId,
    password: player?.password ?? '',
    tabId: getTabSessionId(),
  }
}

function buildExitRoomPayload(player: ReturnType<typeof useAuthStore.getState>['player'], seatPos: number | undefined, isViewer = false) {
  const pix = player?.pix ?? ''
  const name = player?.name ?? ''
  const playerType = isViewer ? 'v5e' : 'v4e'
  const pos = isViewer ? -1 : seatPos ?? -1
  return {
    playerType,
    k57e: playerType,
    playerPos: pos,
    k58e: pos,
    pix,
    k3e: pix,
    name,
    k8e: name,
  }
}

function buildGetMemberListPayload(channelId: string) {
  const subId = extractSubId(channelId)
  return {
    gameId: 'MAJAK4',
    k22e: 'MAJAK4',
    subId,
    k23e: subId,
    channelId,
    k24e: channelId,
  }
}

function readStoredGameState(roomId: string | undefined): GameLocationState | null {
  if (!roomId) return null
  try {
    const raw = window.sessionStorage.getItem(GAME_STATE_STORAGE_KEY)
    if (!raw) return null
    const state = JSON.parse(raw) as GameLocationState
    return String(state.roomId ?? '') === roomId ? state : null
  } catch {
    return null
  }
}

function storeGameState(state: GameLocationState | null): void {
  if (!state?.roomId) return
  window.sessionStorage.setItem(GAME_STATE_STORAGE_KEY, JSON.stringify(state))
}

interface TournamentTotalResultItem {
  pix: string
  grade: number
  pointTotal: number
  point1st: number
  point2nd: number
}

function readTournamentTotalResult(data: Record<string, unknown>, count: number): TournamentTotalResultItem[] {
  if (Array.isArray(data.tournamentTotalReport)) {
    return (data.tournamentTotalReport as Array<Record<string, unknown>>).map(item => ({
      pix: String(item.pix ?? item.k3e ?? ''),
      grade: Number(item.grade ?? 0),
      pointTotal: Number(item.pointTotal ?? 0),
      point1st: Number(item.point1st ?? 0),
      point2nd: Number(item.point2nd ?? 0),
    }))
  }
  return Array.from({ length: count }, (_, index) => {
    const raw = String(data[`${KEY_TOURNAMENT_TOTAL_REPORT}${index}`] ?? '')
    const parts = raw.split('\t')
    return {
      pix: parts[0] ?? '',
      grade: Number(parts[1] ?? 0),
      pointTotal: Number(parts[2] ?? 0),
      point1st: Number(parts[3] ?? 0),
      point2nd: Number(parts[4] ?? 0),
    }
  }).filter(item => item.pix)
}

function SpriteDigit({ frame, x, y }: { frame: number; x: number; y: number }) {
  return <span style={{ position: 'absolute', left: x, top: y, width: 9, height: 17, backgroundImage: `url(${IMG}/mj_num_game00.png)`, backgroundPosition: `${-frame * 9}px 0`, backgroundRepeat: 'no-repeat', imageRendering: 'pixelated' }} />
}

function SignedNumber({ value, x, y }: { value: number; x: number; y: number }) {
  const text = value.toLocaleString('en-US', { signDisplay: 'always', useGrouping: false }).padStart(4, ' ')
  return (
    <>
      {text.slice(-4).split('').map((ch, idx) => {
        if (ch === ' ') return null
        const frame = ch === '+' ? 10 : ch === '-' ? 11 : Number(ch)
        return <SpriteDigit key={`${idx}-${ch}`} frame={frame} x={x + idx * 9} y={y} />
      })}
    </>
  )
}

function makeEmoticonSteps(frames: number[], repeat: number, fadeFrame: number): EmoticonStep[] {
  const base = Array.from({ length: repeat }, () => frames).flat().map(frame => ({ frame, alpha: 255 }))
  return [...base, { frame: fadeFrame, alpha: 191 }, { frame: fadeFrame, alpha: 127 }, { frame: fadeFrame, alpha: 63 }, { frame: fadeFrame, alpha: 0 }]
}

const EMOTICON_STEPS: EmoticonStep[][] = [
  makeEmoticonSteps([0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5], 7, 5),
  [...makeEmoticonSteps([0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6], 1, 6).slice(0, 14), ...Array.from({ length: 68 }, () => ({ frame: 6, alpha: 255 })), { frame: 6, alpha: 191 }, { frame: 6, alpha: 127 }, { frame: 6, alpha: 63 }, { frame: 6, alpha: 0 }],
  makeEmoticonSteps([0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 3, 3, 2, 2, 1, 1, 0, 0], 5, 1),
  makeEmoticonSteps([0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 3, 3, 2, 2, 1, 1, 0, 0], 5, 1),
  [...makeEmoticonSteps([0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7], 1, 7).slice(0, 16), ...Array.from({ length: 66 }, () => ({ frame: 7, alpha: 255 })), { frame: 7, alpha: 191 }, { frame: 7, alpha: 127 }, { frame: 7, alpha: 63 }, { frame: 7, alpha: 0 }],
  makeEmoticonSteps([0, 0, 1, 1, 2, 2, 3, 3], 10, 2),
]

function normalizeEmoticonType(value: unknown): number | null {
  const raw = Number(value)
  if (!Number.isFinite(raw)) return null
  if (raw >= 0 && raw < EMOTICON_COUNT) return raw
  const legacyIndex = EMOTICON_LEGACY_IDS.indexOf(raw)
  return legacyIndex >= 0 ? legacyIndex : null
}

function emoticonLocForPix(pix: string, players: GamePlayerEntry[], myOdr: number | undefined): number | null {
  const player = players.find(item => item.playerId === pix)
  if (!player) return null
  const baseOdr = myOdr ?? 0
  return (4 + player.pos - baseOdr) % 4
}

function EmoticonSpriteButton({ index, x, y, disabled, onClick }: { index: number; x: number; y: number; disabled: boolean; onClick: () => void }) {
  const [frameIdx, setFrameIdx] = useState(0)
  const frame = disabled ? 1 : frameIdx
  return (
    <button
      type="button"
      title={`F${index + 1}`}
      disabled={disabled}
      onClick={disabled ? undefined : onClick}
      onMouseEnter={() => !disabled && setFrameIdx(2)}
      onMouseLeave={() => !disabled && setFrameIdx(0)}
      onMouseDown={() => !disabled && setFrameIdx(3)}
      onMouseUp={() => !disabled && setFrameIdx(2)}
      style={{
        position: 'absolute',
        left: x,
        top: y,
        width: 16,
        height: 16,
        border: 'none',
        padding: 0,
        outline: 'none',
        backgroundColor: 'transparent',
        backgroundImage: `url(${IMG}/emobt${String(index).padStart(2, '0')}.png)`,
        backgroundPosition: `${-16 * frame}px 0`,
        backgroundRepeat: 'no-repeat',
        cursor: disabled ? 'default' : 'pointer',
        imageRendering: 'pixelated',
        pointerEvents: 'auto',
      }}
    />
  )
}

function EmoticonAnimation({ item, now }: { item: ActiveEmoticon; now: number }) {
  const steps = EMOTICON_STEPS[item.type] ?? EMOTICON_STEPS[0]
  const frameIndex = Math.max(0, Math.min(steps.length - 1, Math.floor((now - item.startedAt) / EMOTICON_FRAME_MS)))
  const step = steps[frameIndex]
  const pos = EMOTICON_POS[item.loc] ?? EMOTICON_POS[0]
  return (
    <div
      style={{
        position: 'absolute',
        left: pos.x,
        top: pos.y,
        width: 96,
        height: 96,
        opacity: step.alpha / 255,
        backgroundImage: `url(${IMG}/emo_${pos.dir}_${String(item.type).padStart(2, '0')}.png)`,
        backgroundPosition: `${-96 * step.frame}px 0`,
        backgroundRepeat: 'no-repeat',
        imageRendering: 'pixelated',
      }}
    />
  )
}

function CallAvatarPopup({ item }: { item: ActiveCallAvatar }) {
  return (
    <img
      src={item.avatarUrl}
      alt=""
      draggable={false}
      onError={e => { e.currentTarget.src = item.fallbackAvatarUrl || getDefaultAvatarUrl('male') }}
      style={{
        position: 'absolute',
        left: item.x,
        top: item.y,
        width: item.w,
        height: item.h,
        objectFit: 'contain',
        imageRendering: 'auto',
      }}
    />
  )
}

function TournamentTotalResultDialog({ items, players, onClose }: {
  items: TournamentTotalResultItem[]
  players: HanResPlayer[]
  onClose: () => void
}) {
  return (
    <div style={{ position: 'absolute', inset: 0, zIndex: 520, display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'rgba(0,0,0,0.45)' }} onClick={onClose} onContextMenu={e => e.preventDefault()}>
      <div style={{ position: 'relative', width: 451, height: 330, imageRendering: 'pixelated' }} onClick={e => e.stopPropagation()}>
        <img src={`${IMG}/mj_general_result.png`} alt="" draggable={false} style={{ position: 'absolute', left: 0, top: 0 }} />
        {items.slice(0, 4).map((item, idx) => {
          const offX = idx * 108
          const player = players.find(p => p.pix === item.pix)
          const isNpc = item.pix.length === 0
          const displayName = player?.name || item.pix
          return (
            <div key={`${item.pix}-${idx}`}>
              {isNpc ? (
                <img src={`${IMG}/mj_aiAvtrL.png`} alt="" draggable={false} style={{ position: 'absolute', left: 41 + offX, top: 63, width: 45, height: 64, objectFit: 'cover' }} />
              ) : (
                <img src={getAvatarUrl(player?.avatarId ?? null)} alt="" draggable={false} style={{ position: 'absolute', left: 41 + offX, top: 63, width: 45, height: 64, objectFit: 'cover', imageRendering: 'auto' }} onError={e => { e.currentTarget.src = getDefaultAvatarUrl('male') }} />
              )}
              <div style={{ position: 'absolute', left: 13 + offX, top: 127, width: 100, height: 20, color: isNpc ? '#f00' : '#fff', fontFamily: 'var(--majak-font-family-ui)', fontSize: 'calc(14px * var(--majak-type-scale))', fontWeight: 'bold', lineHeight: '20px', textAlign: 'center', overflow: 'hidden', whiteSpace: 'nowrap', textOverflow: 'ellipsis' }}>
                {isNpc ? '<トントン>' : displayName}
              </div>
              <span style={{ position: 'absolute', left: 44 + offX, top: 155, width: 39, height: 25, backgroundImage: `url(${IMG}/mj_ranking_L.png)`, backgroundPosition: `${-idx * 39}px 0`, backgroundRepeat: 'no-repeat', imageRendering: 'pixelated' }} />
              {item.grade > 0 && <img src={`${IMG}/mj_win.png`} alt="" draggable={false} style={{ position: 'absolute', left: 14 + offX, top: 180, imageRendering: 'pixelated' }} />}
              <SignedNumber value={item.pointTotal} x={73 + offX} y={228} />
              <SignedNumber value={item.point1st} x={73 + offX} y={261} />
              <SignedNumber value={item.point2nd} x={73 + offX} y={293} />
            </div>
          )
        })}
      </div>
    </div>
  )
}

function NextTournamentResultButton({ onClick }: { onClick: () => void }) {
  return (
    <div style={{ position: 'absolute', inset: 0, zIndex: 360, display: 'flex', alignItems: 'center', justifyContent: 'center', pointerEvents: 'none' }}>
      <div style={{ position: 'relative', width: 'min(100vw, calc(100vh * 1019 / 735))', aspectRatio: '1019 / 735', pointerEvents: 'none' }}>
        <button aria-label="next result" onClick={onClick} style={{ position: 'absolute', left: '42.69%', top: '88.03%', width: '17.08%', height: '5.44%', border: 0, padding: 0, background: `url(${IMG}/mj_next01.png) 0 0 / 400% 100% no-repeat`, imageRendering: 'pixelated', pointerEvents: 'auto', cursor: 'pointer' }} />
      </div>
    </div>
  )
}

function GameLoadingOverlay({ visible }: { visible: boolean }) {
  if (!visible) return null
  return (
    <div style={{ position: 'absolute', inset: 0, zIndex: 900, display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'rgba(0, 0, 0, 0.58)', pointerEvents: 'auto' }}>
      <div style={{ width: 260, height: 312, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 18, background: 'rgba(255, 255, 255, 0.94)', border: '1px solid rgba(0, 0, 0, 0.24)', boxShadow: '0 8px 24px rgba(0, 0, 0, 0.45)' }} aria-busy="true" aria-label="対局状況を同期中">
        <img src="/assets/images/common/ico_big_majak2.jpg" alt="" draggable={false} style={{ width: 230, height: 230, objectFit: 'cover' }} />
        <div className="majak-sync-spinner" aria-hidden="true" />
      </div>
    </div>
  )
}

function GameSpriteButton({ src, frameW, frameH, x, y, checked = false, disabled = false, hidden = false, onClick, title }: {
  src: string
  frameW: number
  frameH: number
  x: number
  y: number
  checked?: boolean
  disabled?: boolean
  hidden?: boolean
  onClick: () => void
  title: string
}) {
  const [frameIdx, setFrameIdx] = useState(0)
  if (hidden) return null
  const frame = disabled ? 1 : checked ? 3 : frameIdx
  return (
    <button
      type="button"
      title={title}
      disabled={disabled}
      onClick={disabled ? undefined : onClick}
      onMouseEnter={() => !disabled && !checked && setFrameIdx(2)}
      onMouseLeave={() => !disabled && setFrameIdx(0)}
      onMouseDown={() => !disabled && setFrameIdx(3)}
      onMouseUp={() => !disabled && !checked && setFrameIdx(2)}
      style={{
        position: 'absolute', left: x, top: y,
        width: frameW, height: frameH,
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-frame * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        backgroundColor: 'transparent',
        border: 'none', padding: 0, outline: 'none',
        cursor: disabled ? 'default' : 'pointer',
        imageRendering: 'pixelated',
        pointerEvents: 'auto',
      }}
    />
  )
}

export default function GameScreen() {
  const desktopScale = useDesktopScreenScale()
  const containerRef = useRef<HTMLDivElement>(null)
  const navigate     = useNavigate()
  const location     = useLocation()
  const { roomId }   = useParams<{ roomId: string }>()
  const navState     = location.state as GameLocationState | null
  const [gameState]  = useState<GameLocationState | null>(() => navState ?? readStoredGameState(roomId))
  const [signalReady, setSignalReady] = useState(false)
  const [syncLoading, setSyncLoading] = useState(true)

  /** CMJHanRes 表示状態 */
  const [hanResData, setHanResData] = useState<HanResPlayer[] | null>(null)
  const [hanResFlags, setHanResFlags] = useState({ hasTor: false, hasTip: false, isViewer: false, isTournament: false })
  /** CMJKyoRes 表示状態 */
  const [kyoResData, setKyoResData] = useState<KyoResData | null>(null)
  /** CMJSlideAnnounce 表示状態 */
  const [announceData, setAnnounceData] = useState<SlideAnnounceData | null>(null)
  /** CMJAskEndDlg 表示状態 */
  const [askEndSet, setAskEndSet] = useState<{ roomId: string; seatOrder: number; actionSeq?: number; localDeadlineAt?: number } | null>(null)
  /** CMJKyoRes 継続アクション送信用状態 */
  const [kyoResultAction, setKyoResultAction] = useState<{ roomId: string; seatOrder: number; timeLimit: number; actionSeq?: number; localDeadlineAt?: number } | null>(null)
  const [tournamentTotalResult, setTournamentTotalResult] = useState<TournamentTotalResultItem[] | null>(null)
  const [showTournamentTotalResult, setShowTournamentTotalResult] = useState(false)
  const [viewers, setViewers] = useState<ViewerEntry[]>(gameState?.viewers ?? [])
  const [players, setPlayers] = useState<GamePlayerEntry[]>(gameState?.players ?? [])
  const [statusLog, setStatusLog] = useState<ChatMsg[]>([])
  const [chatLog, setChatLog] = useState<ChatMsg[]>([])
  const [chatText, setChatText] = useState('')
  const [activeEmoticons, setActiveEmoticons] = useState<ActiveEmoticon[]>([])
  const [activeCallAvatars, setActiveCallAvatars] = useState<ActiveCallAvatar[]>([])
  const [emoticonNow, setEmoticonNow] = useState(0)
  const [emoticonCooldown, setEmoticonCooldown] = useState(false)
  const [channelMembers, setChannelMembers] = useState<ChannelMemberEntry[]>([])
  const [showInviteDialog, setShowInviteDialog] = useState(false)
  const [inviteTargetPix, setInviteTargetPix] = useState<string | null>(null)
  const [inviteWaiting, setInviteWaiting] = useState(false)
  const [inviteResult, setInviteResult] = useState<'accepted' | 'declined' | 'timeout' | null>(null)
  const initialMyOdr = gameState?.myOdr ?? players.find(p => p.playerId === useAuthStore.getState().player?.pix)?.pos
  const [viewOdr, setViewOdr] = useState<number | undefined>(undefined)
  const effectiveMyOdr = viewOdr ?? initialMyOdr
  const [autoTsumoGiri, setAutoTsumoGiri] = useState(false)
  const [autoPass, setAutoPass] = useState(false)
  const [autoHora, setAutoHora] = useState(false)
  const [proxyPlay, setProxyPlay] = useState(false)
  const [viewerHandHidden, setViewerHandHidden] = useState(false)
  useEffect(() => {
    window.dispatchEvent(new CustomEvent(GAME_AUTO_CONTROL_EVENT, {
      detail: {
        prox: proxyPlay,
        autoTap: autoTsumoGiri,
        autoPass,
        autoHora,
      },
    }))
  }, [proxyPlay, autoTsumoGiri, autoPass, autoHora])
  useEffect(() => {
    const onKyokuStarted = () => {
      if (proxyPlay) return
      setAutoTsumoGiri(false)
      setAutoPass(false)
      setAutoHora(false)
    }
    window.addEventListener(GAME_KYOKU_STARTED_EVENT, onKyokuStarted)
    return () => window.removeEventListener(GAME_KYOKU_STARTED_EVENT, onKyokuStarted)
  }, [proxyPlay])
  const statusLogRef = useRef<HTMLDivElement>(null)
  const chatLogRef = useRef<HTMLDivElement>(null)
  const callAvatarTimersRef = useRef<number[]>([])
  const syncLoadingStartedAtRef = useRef(0)
  const syncLoadingOffTimerRef = useRef<number | null>(null)
  const playersRef = useRef<GamePlayerEntry[]>(players)
  const viewersRef = useRef<ViewerEntry[]>(viewers)
  const gameOrderLockedRef = useRef(false)
  const effectiveMyOdrRef = useRef<number | undefined>(effectiveMyOdr)
  const messageSeqRef = useRef(0)
  const proxyGuideShownRef = useRef(false)
  const gemGameGuideShownRef = useRef(false)
  const pendingInviteTargetRef = useRef<string | null>(null)
  const emoticonCooldownRef = useRef(false)
  const emoticonCooldownTimerRef = useRef<number | null>(null)
  const pendingEmoticonsRef = useRef<Array<{ pix: string; type: number }>>([])
  const channelId = gameState?.channelId ?? navState?.channelId
  const roomTitle = String(gameState?.roomTitle ?? navState?.roomTitle ?? '')
  const roomOption = String(gameState?.roomOption ?? navState?.roomOption ?? '')
  const fallbackSkin = useCustomSkinStore()
  const routeCustomBgId = Number(gameState?.customBgId ?? navState?.customBgId ?? 0)
  const routeCustomBoardType = Number(gameState?.customBoardType ?? navState?.customBoardType ?? 0)
  const routeCustomHaiId = Number(gameState?.customHaiId ?? navState?.customHaiId ?? 0)
  const customBgId = routeCustomBgId > 0 ? routeCustomBgId : fallbackSkin.bgId
  const customBoardType = routeCustomBoardType > 0 ? routeCustomBoardType : fallbackSkin.bgType
  const customHaiId = routeCustomHaiId > 0 ? routeCustomHaiId : fallbackSkin.haiId
  const tengokuBoardSkin = isTengokuBoardSkin(customBgId, customBoardType)
  const legacyPalette = getLegacyRoomPalette(tengokuBoardSkin)
  const boardSoundSkinId = getLegacyBoardSoundSkinId(customBgId, customBoardType)
  const boardSoundOptions = boardSoundSkinId ? { skinId: boardSoundSkinId } : {}
  const subId = extractSubId(channelId)
  const replayChannel = isReplayChannel(channelId)
  const chatEnabled = replayChannel || (!isDaniChannel(channelId) && readOptionDigit(roomOption, 14, 0) !== 0)
  const chatInputDisabled = !chatEnabled
  const viewerChatVisible = readOptionDigit(roomOption, 7, 0) !== 0
  const viewerHandOpenEnabled = readOptionDigit(roomOption, 6, 0) !== 0
  const inviteDisabled = subId[1] === 'Z' || subId[2] === 'H'
  const showGameControlButtons = !gameState?.skipInitialRoomEnter
  const isViewerUser = Boolean(gameState?.isViewer)
  const canUseEmoticon = effectiveMyOdr != null && !hanResData
  const emoticonButtonDisabled = !canUseEmoticon || chatInputDisabled || emoticonCooldown
  const optionIcons = [
    optionSprite(OPTION_ICON.set, readOptionDigit(roomOption, 0, 1), 948, 39, 1),
    optionSprite(OPTION_ICON.kui, readOptionDigit(roomOption, 3, 0), 965, 39, 1),
    optionSprite(OPTION_ICON.uma, readOptionDigit(roomOption, 1, 2), 982, 39, 3),
    optionSprite(OPTION_ICON.ron, readOptionDigit(roomOption, 12, 0), 999, 39, 2),
    optionSprite(OPTION_ICON.red, readOptionDigit(roomOption, 5, 2), 948, 56, 2),
    optionSprite(OPTION_ICON.spd, readOptionDigit(roomOption, 2, 2), 965, 56, 3),
    optionSprite(OPTION_ICON.opn, readOptionDigit(roomOption, 6, 0), 982, 56, 1),
    optionSprite(readOptionDigit(roomOption, 14, 0) ? OPTION_ICON.cht : OPTION_ICON.ach, readOptionDigit(roomOption, 14, 0) ? readOptionDigit(roomOption, 7, 0) : 0, 999, 56, 1),
  ]

  const nextMessageId = () => `${Date.now()}-${messageSeqRef.current++}`

  const putStatus = (text: string, color = '#000', bold = false) => {
    setStatusLog(prev => [...prev, { id: nextMessageId(), name: '', text, color, bold }].slice(-MAX_GAME_LOG_MESSAGES))
  }

  useEffect(() => {
    const onGameStatus = (event: Event) => {
      const detail = (event as CustomEvent<{ text?: string; color?: string; bold?: boolean }>).detail ?? {}
      const text = String(detail.text ?? '')
      if (text) putStatus(text, applyTengokuTextColor(detail.color, tengokuBoardSkin), Boolean(detail.bold))
    }
    window.addEventListener(GAME_STATUS_EVENT, onGameStatus)
    return () => window.removeEventListener(GAME_STATUS_EVENT, onGameStatus)
  }, [])

  useEffect(() => {
    const setSyncLoadingVisible = (visible: boolean) => {
      if (syncLoadingOffTimerRef.current !== null) {
        window.clearTimeout(syncLoadingOffTimerRef.current)
        syncLoadingOffTimerRef.current = null
      }
      if (visible) {
        syncLoadingStartedAtRef.current = performance.now()
        setSyncLoading(true)
        return
      }
      const elapsed = performance.now() - syncLoadingStartedAtRef.current
      const delay = Math.max(0, 600 - elapsed)
      syncLoadingOffTimerRef.current = window.setTimeout(() => {
        setSyncLoading(false)
        syncLoadingOffTimerRef.current = null
      }, delay)
    }
    const onSync = (event: Event) => {
      const detail = (event as CustomEvent<{ active?: boolean }>).detail ?? {}
      setSyncLoadingVisible(Boolean(detail.active))
    }
    const onConnectionLost = () => setSyncLoadingVisible(true)
    window.addEventListener(GAME_SYNC_EVENT, onSync)
    SignalR.onConnectionLost(onConnectionLost)
    return () => {
      window.removeEventListener(GAME_SYNC_EVENT, onSync)
      SignalR.offConnectionLost(onConnectionLost)
      if (syncLoadingOffTimerRef.current !== null) window.clearTimeout(syncLoadingOffTimerRef.current)
    }
  }, [])

  useEffect(() => {
    const onCallAvatar = (event: Event) => {
      const detail = (event as CustomEvent<Partial<ActiveCallAvatar>>).detail ?? {}
      const avatarUrl = String(detail.avatarUrl ?? '')
      if (!avatarUrl) return
      const item: ActiveCallAvatar = {
        id: `${Date.now()}-${Math.random()}`,
        avatarUrl,
        fallbackAvatarUrl: String(detail.fallbackAvatarUrl ?? getDefaultAvatarUrl('male')),
        x: Number(detail.x ?? 0),
        y: Number(detail.y ?? 0),
        w: Number(detail.w ?? 66),
        h: Number(detail.h ?? 94),
      }
      setActiveCallAvatars(prev => [...prev, item])
      const timer = window.setTimeout(() => {
        setActiveCallAvatars(prev => prev.filter(active => active.id !== item.id))
        callAvatarTimersRef.current = callAvatarTimersRef.current.filter(activeTimer => activeTimer !== timer)
      }, 1100)
      callAvatarTimersRef.current.push(timer)
    }
    window.addEventListener(GAME_CALL_AVATAR_EVENT, onCallAvatar)
    return () => {
      window.removeEventListener(GAME_CALL_AVATAR_EVENT, onCallAvatar)
      callAvatarTimersRef.current.forEach(timer => window.clearTimeout(timer))
      callAvatarTimersRef.current = []
    }
  }, [])

  const putChatMessage = (text: string, color: string, bold = false) => {
    setChatLog(prev => [...prev, { id: nextMessageId(), name: '', text, color, bold }].slice(-MAX_GAME_LOG_MESSAGES))
  }

  const displayNameForPix = (pix: string): string => {
    if (!pix) return ''
    const player = playersRef.current.find(item => item.playerId === pix)
    if (player?.name) return player.name
    const viewer = viewersRef.current.find(item => item.pix === pix)
    if (viewer?.name) return viewer.name
    const member = channelMembers.find(item => item.pix === pix)
    return member?.name || pix
  }

  const putProxyGuideStatus = () => {
    if (proxyGuideShownRef.current) return
    proxyGuideShownRef.current = true
    const guide = subId[2] === 'H' ? LEGACY_TOURNAMENT_LINEOFF_GUIDE : LEGACY_PROXY_GUIDE
    guide.forEach(line => putStatus(line, legacyPalette.normal, true))
  }

  const putGemGameStatus = (value: unknown) => {
    if (gemGameGuideShownRef.current) return
    const text = gemGameStatusText(value)
    if (!text) return
    gemGameGuideShownRef.current = true
    putStatus(text, legacyPalette.normal, true)
  }

  const playEmoticon = (pix: string, type: number): boolean => {
    const loc = emoticonLocForPix(pix, playersRef.current, effectiveMyOdrRef.current)
    if (loc == null) return false
    const now = performance.now()
    setEmoticonNow(now)
    setActiveEmoticons(prev => [
      ...prev.filter(item => item.loc !== loc),
      { id: `${pix}-${type}-${now}`, pix, type, loc, startedAt: now },
    ])
    return true
  }

  const queueEmoticon = (pix: string, type: number) => {
    pendingEmoticonsRef.current.push({ pix, type })
    if (pendingEmoticonsRef.current.length > 24) pendingEmoticonsRef.current.shift()
  }

  const flushQueuedEmoticons = () => {
    if (document.visibilityState === 'hidden') return
    const pending = pendingEmoticonsRef.current.splice(0)
    for (const item of pending) {
      if (!playEmoticon(item.pix, item.type)) queueEmoticon(item.pix, item.type)
    }
  }

  const playOrQueueEmoticon = (pix: string, type: number) => {
    if (document.visibilityState === 'hidden') {
      queueEmoticon(pix, type)
      return
    }
    if (!playEmoticon(pix, type)) queueEmoticon(pix, type)
  }

  useEffect(() => {
    document.addEventListener('visibilitychange', flushQueuedEmoticons)
    window.addEventListener('focus', flushQueuedEmoticons)
    return () => {
      document.removeEventListener('visibilitychange', flushQueuedEmoticons)
      window.removeEventListener('focus', flushQueuedEmoticons)
    }
  }, [])

  useEffect(() => {
    playersRef.current = players
    flushQueuedEmoticons()
  }, [players])

  useEffect(() => { viewersRef.current = viewers }, [viewers])

  useEffect(() => {
    effectiveMyOdrRef.current = effectiveMyOdr
    flushQueuedEmoticons()
  }, [effectiveMyOdr])

  useEffect(() => {
    if (activeEmoticons.length === 0) return
    const id = window.setInterval(() => {
      const now = performance.now()
      setEmoticonNow(now)
      setActiveEmoticons(prev => prev.filter(item => {
        const steps = EMOTICON_STEPS[item.type] ?? EMOTICON_STEPS[0]
        return now - item.startedAt < steps.length * EMOTICON_FRAME_MS
      }))
    }, EMOTICON_FRAME_MS)
    return () => window.clearInterval(id)
  }, [activeEmoticons.length])

  useEffect(() => () => {
    if (emoticonCooldownTimerRef.current != null) window.clearTimeout(emoticonCooldownTimerRef.current)
  }, [])

  useEffect(() => {
    storeGameState({ ...(gameState ?? {}), ...(navState ?? {}), roomId: roomId ?? gameState?.roomId })
  }, [gameState, navState, roomId])

  useEffect(() => {
    if (!roomId) return
    if (gameState?.channelId) return
    navigate('/channel', { replace: true })
  }, [gameState?.channelId, navigate, roomId])

  useEffect(() => {
    let cancelled = false

    async function prepareSignalR() {
      const channelId = gameState?.channelId
      if (!roomId || !channelId) {
        setSignalReady(true)
        return
      }

      const serverUrl = gameState.serverUrl || await getChannelServerUrl(channelId).catch(() => '')
      const hubUrl = serverUrl ? `${serverUrl}/hubs/majak` : '/hubs/majak'
      await SignalR.connect(hubUrl)
      if (cancelled) return
      const alreadyInGameFlow = Boolean(gameState?.skipInitialRoomEnter || gameState?.players?.length)
      if (!alreadyInGameFlow) {
        await SignalR.send('c1e', buildEnterChannelPayload(channelId, useAuthStore.getState().player)).catch(() => {})
      }
      if (!cancelled) setSignalReady(true)
    }

    prepareSignalR().catch(() => {
      if (!cancelled) setSignalReady(true)
    })

    return () => { cancelled = true }
  }, [gameState?.channelId, gameState?.serverUrl, roomId])

  useEffect(() => {
    if (!containerRef.current || !signalReady) return
    createGame(containerRef.current, {
      roomId: roomId ?? '',
      myOdr: initialMyOdr,
      players: players as unknown as Array<Record<string, unknown>>,
      roomOption,
      isViewer: Boolean(gameState?.isViewer),
      customBgId,
      customBoardType,
      customHaiId,
      skipInitialRoomEnter: Boolean(gameState?.skipInitialRoomEnter || gameState?.players?.length),
    })
    return () => destroyGame()
  }, [customBgId, customHaiId, initialMyOdr, roomId, signalReady])

  useEffect(() => {
    if (!signalReady) return
    let mounted = true
    const myPix = useAuthStore.getState().player?.pix ?? ''

    const isViewerPacket = (data: Record<string, unknown>) => {
      const playerType = data.k57e ?? data.playerType
      return playerType === 'viewer' || playerType === 'v5e' || playerType === 2 || playerType === '2'
    }

    const toDanName = (gradeLevel: number) => [
      '10級', '9級', '8級', '7級', '6級', '5級', '4級', '3級', '2級', '1級',
      '初段', '二段', '三段', '四段', '五段', '六段', '七段', '八段', '九段', '十段',
    ][gradeLevel] ?? ''

    const toViewerEntry = (data: Record<string, unknown>): ViewerEntry => ({
      pix: String(data.k3e ?? data.pix ?? ''),
      name:     String(data.mjkk34e ?? data.k8e ?? data.nickName ?? data.nickname ?? data.name ?? ''),
      avatarId: data.k7e != null || data.avatarId != null ? String(data.k7e ?? data.avatarId) : undefined,
      sex:      data.k11e != null || data.sex != null ? String(data.k11e ?? data.sex) : undefined,
      slevel:   data.k32e != null || data.slevel != null ? String(data.k32e ?? data.slevel) : undefined,
      dan:      data.dan != null ? String(data.dan) : toDanName(Number(data.gradeCurrLevel ?? -1)),
      rating:   Number(data.k31e ?? data.rating ?? 0),
      playerPos: data.k58e != null || data.playerPos != null ? Number(data.k58e ?? data.playerPos) : undefined,
    })

    const upsertViewer = (data: Record<string, unknown>) => {
      const entry = toViewerEntry(data)
      if (!entry.pix) return
      setViewers(prev => [...prev.filter(v => v.pix !== entry.pix), entry])
    }

    const removeViewer = (data: Record<string, unknown>) => {
      const pix = String(data.k3e ?? data.pix ?? '')
      if (!pix) return
      setViewers(prev => prev.filter(v => v.pix !== pix))
    }

    const onMemberList = (data: Record<string, unknown>) => {
      if (!mounted) return
      const legacyCount = Number(data.k25e ?? data.count ?? 0)
      const legacyMembers = Array.from({ length: legacyCount }, (_, index): Record<string, unknown> => {
        const pix = data[`k3e${index}`]
        const playerType = data[`k57e${index}`]
        return {
          pix,
          nickName: data[`mjkk34e${index}`] ?? data[`k8e${index}`] ?? pix,
          name: data[`k8e${index}`] ?? data[`mjkk34e${index}`] ?? pix,
          avatarId: data[`k7e${index}`],
          sex: data[`k11e${index}`],
          playerType,
          playerPos: data[`k58e${index}`],
          seatPos: data[`k58e${index}`],
          rating: data[`k31e${index}`],
          slevel: data[`k32e${index}`],
          nlevel: data[`k33e${index}`],
          trickTitle: data[`mjkk46e${index}`],
          majakTitle: data[`mjkk47e${index}`],
          customCostume: data[`mjkk136e${index}`],
          customCostumeType: data[`mjkk137e${index}`],
          isViewer: playerType === 'v5e',
        }
      }).filter(member => member.pix != null && String(member.pix) !== '')
      const members = Array.isArray(data.members) && data.members.length > 0
        ? data.members as Array<Record<string, unknown>>
        : legacyMembers
      setViewers(members.filter(isViewerPacket).map(toViewerEntry).filter(v => v.pix))
      const parsedPlayers = [...new Map(members.filter(m => !isViewerPacket(m)).map(readGamePlayer).filter(p => p != null).map(p => [p.playerId, p])).values()]
      const nextPlayers = gameOrderLockedRef.current ? keepCurrentPlayerOrder(parsedPlayers, playersRef.current) : parsedPlayers
      setPlayers(prev => mergePlayers(prev, nextPlayers))
    }
    SignalR.on('c16e', onMemberList)

    const onChannelMemberList = (data: Record<string, unknown>) => {
      if (!mounted) return
      if (Number(data.result) !== 1 && data.k1e !== 'v1e') return
      const count = Number(data.k25e ?? data.count ?? 0)
      const legacyList: Array<Record<string, unknown>> = Array.from({ length: count }, (_, index): Record<string, unknown> | null => {
        const raw = data[`k3e${index}`]
        if (typeof raw !== 'string' || raw === '') return null
        const parts = raw.split('\t')
        if (parts.length >= 22) {
          return {
            pix: parts[0] ?? '',
            name: parts[2] || parts[27] || parts[0] || '',
            rating: Number(parts[12] ?? 0),
            slevel: parts[13] === ' ' ? undefined : parts[13],
            location: parts[5] || undefined,
            roomId: undefined,
          }
        }
        if (parts.length >= 12) {
          return {
            pix: parts[0] ?? '',
            name: parts[17] && parts[17] !== ' ' ? parts[17] : parts[0] ?? '',
            rating: Number(parts[7] ?? 0),
            location: parts[2] || undefined,
            roomId: undefined,
          }
        }
        return null
      }).filter((member): member is Record<string, unknown> => member != null && String(member.pix ?? '') !== '')
      const list: Array<Record<string, unknown>> = legacyList.length > 0
        ? legacyList
        : Array.isArray(data.members) ? data.members as Array<Record<string, unknown>> : []
      setChannelMembers(list
        .map(member => ({
          pix: String(member.k3e ?? member.pix ?? ''),
          name: String(member.k8e ?? member.nickname ?? member.name ?? ''),
          rating: Number(member.k31e ?? member.rating ?? 0),
          slevel: member.k32e != null || member.slevel != null ? String(member.k32e ?? member.slevel) : undefined,
          location: member.k12e != null || member.location != null ? String(member.k12e ?? member.location) : undefined,
          roomId: member.k42e != null || member.roomId != null ? Number(member.k42e ?? member.roomId) : undefined,
        }))
        .filter(member => member.pix && member.pix !== myPix && (member.roomId == null || member.roomId <= 0) && member.location !== 'room'))
    }
    SignalR.on('c7e', onChannelMemberList)

    const onRoomEnter = (data: Record<string, unknown>) => {
      if (!mounted) return
      if (Number(data.result) !== 1) return
      const player = readGamePlayer(data)
      if (player) {
        setPlayers(prev => mergePlayers(prev, gameOrderLockedRef.current ? keepCurrentPlayerOrder([player], prev) : [player]))
        if (player.playerId === myPix && player.pos >= 0) putProxyGuideStatus()
      }
    }
    SignalR.on('c14e', onRoomEnter)

    const onMemberJoined = (data: Record<string, unknown>) => {
      if (!mounted) return
      const pix = String(data.k3e ?? data.pix ?? '')
      const msg = `${displayNameForPix(pix)}様が入室しました。`
      if (isViewerPacket(data)) {
        upsertViewer(data)
        putChatMessage(msg, legacyPalette.roomJoin)
        return
      }
      const player = readGamePlayer(data)
      putChatMessage(msg, legacyPalette.roomJoin, true)
      playMajakSid(SID_JOIN, boardSoundOptions)
      if (player) {
        setPlayers(prev => mergePlayers(prev, gameOrderLockedRef.current ? keepCurrentPlayerOrder([player], prev) : [player]))
        if (pix === myPix && player.pos >= 0) putProxyGuideStatus()
      }
    }
    SignalR.on('c5e', onMemberJoined)

    const onMemberLeft = (data: Record<string, unknown>) => {
      if (!mounted) return
      const pix = String(data.k3e ?? data.pix ?? '')
      const isPlaying = Boolean(data.isPlaying ?? data.playing ?? false)
      const displayName = displayNameForPix(pix)
      const msg = isPlaying
        ? `${displayName}様の接続が切れました。`
        : `${displayName}様が退室しました。`
      if (isViewerPacket(data)) {
        removeViewer(data)
        putChatMessage(msg, legacyPalette.roomExit)
        return
      }
      putChatMessage(msg, isPlaying ? legacyPalette.roomDrop : legacyPalette.roomExit, true)
      playMajakSid(SID_EXIT, boardSoundOptions)
      if (pix) setPlayers(prev => prev.filter(p => p.playerId !== pix))
    }
    SignalR.on('c6e', onMemberLeft)

    const onAutoExitRoom = (data: Record<string, unknown>) => {
      if (!mounted) return
      const pix = String(data.k3e ?? data.pix ?? '')
      const myPix = useAuthStore.getState().player?.pix ?? ''
      if (pix === '' || pix === myPix) {
        navigate(-1)
        return
      }
      removeViewer(data)
    }
    SignalR.on('mjkc5e', onAutoExitRoom)

    void SignalR.send('c16e', {}).catch(() => {})

    /**
     * room:game_report — CMJTblUser::EndSet() → CMJHanRes 生成 相当
     * サーバー: GameLogicService.GameReportProcessAsync()
     *   → BuildGameResultPayload() → SendAsync(Cmd.GameReport, payload)
     *
     * payload.users[]: {
    *   seq, pix, ranking, rank, point, setBal, setTen, setUma, setTor, setTip,
    *   moneyChange, coinGain, coinNeed, gammoney, nlevel, slevel,
     *   horaCnt, hojuCnt, richiCnt, furoCnt, ...
     * }
     */
    const onGameReport = (data: Record<string, unknown>) => {
      const tournamentTotalReportCnt = Number(data.tournamentTotalReportCnt ?? data.mjkk98e ?? 0)
      const users = Array.isArray(data.users)
        ? data.users as Array<Record<string, unknown>>
        : []
      if (tournamentTotalReportCnt > 0) {
        setTournamentTotalResult(readTournamentTotalResult(data, tournamentTotalReportCnt))
        setShowTournamentTotalResult(users.length === 0)
      }
      if (Number(data.result) !== 1) return
      const players: HanResPlayer[] = users.map((u, idx) => ({
        pix:       String(u.pix ?? u.k3e ?? ''),
        name:      String(u.name ?? ''),
        avatarId:  String(u.avatarId ?? ''),
        sex:       String(u.sex ?? ''),
        charaId:   Number(u.charaId ?? u.customCostume ?? 0),
        seatPos:   (Number(u.seatPos ?? idx) % 4) as 0 | 1 | 2 | 3,
        rank:      (Number(u.rank ?? (Number(u.ranking ?? idx + 1) - 1)) % 4) as 0 | 1 | 2 | 3,
        point:     Number(u.point ?? 0),
        setBal:    Number(u.setBal        ?? 0),
        setTen:    Number(u.setTen        ?? 0),
        setUma:    Number(u.setUma        ?? 0),
        setTor:    u.setTor !== undefined ? Number(u.setTor) : undefined,
        setTip:    u.setTip !== undefined ? Number(u.setTip) : undefined,
        coinGain:  Number(u.coinGain      ?? 0),
        coinNeed:  Number(u.coinNeed      ?? 0),
        prevNlevel: u.prevNlevel !== undefined ? Number(u.prevNlevel) : undefined,
        nlevel:    u.nlevel !== undefined ? Number(u.nlevel) : undefined,
        levelName: String(u.slevel ?? ''),
        rating: u.rating !== undefined ? Number(u.rating) : undefined,
        ratingChange: u.ratingChange !== undefined ? Number(u.ratingChange) : undefined,
        matchCnt: u.matchCnt !== undefined ? Number(u.matchCnt) : undefined,
        winCnt: u.winCnt !== undefined ? Number(u.winCnt) : undefined,
        defeatCnt: u.defeatCnt !== undefined ? Number(u.defeatCnt) : undefined,
        drawCnt: u.drawCnt !== undefined ? Number(u.drawCnt) : undefined,
        gameMoney: u.gammoney !== undefined ? Number(u.gammoney) : undefined,
        moneyChange: u.moneyChange !== undefined ? Number(u.moneyChange) : undefined,
        dealerFee: u.dealerFee !== undefined ? Number(u.dealerFee) : undefined,
        gemCount: u.gemCount !== undefined ? Number(u.gemCount) : undefined,
        experience: u.experience !== undefined ? Number(u.experience) : undefined,
        expGain: u.expGain !== undefined ? Number(u.expGain) : undefined,
        horaCnt: u.horaCnt !== undefined ? Number(u.horaCnt) : undefined,
        horaPoint: u.horaPoint !== undefined ? Number(u.horaPoint) : undefined,
        hojuCnt: u.hojuCnt !== undefined ? Number(u.hojuCnt) : undefined,
        richiCnt: u.richiCnt !== undefined ? Number(u.richiCnt) : undefined,
        furoCnt: u.furoCnt !== undefined ? Number(u.furoCnt) : undefined,
        doraCnt: u.doraCnt !== undefined ? Number(u.doraCnt) : undefined,
        richiHoraCnt: u.richiHoraCnt !== undefined ? Number(u.richiHoraCnt) : undefined,
        prevGradeLevel: u.prevGradeLevel !== undefined ? Number(u.prevGradeLevel) : undefined,
        gradeLevel: u.gradeLevel !== undefined ? Number(u.gradeLevel) : undefined,
        prevGradePoint: u.prevGradePoint !== undefined ? Number(u.prevGradePoint) : undefined,
        gradePoint: u.gradePoint !== undefined ? Number(u.gradePoint) : undefined,
        gradeAddPoint: u.gradeAddPoint !== undefined ? Number(u.gradeAddPoint) : undefined,
        gradeNextPoint: u.gradeNextPoint !== undefined ? Number(u.gradeNextPoint) : undefined,
        gradeUpDown: u.gradeUpDown !== undefined ? Number(u.gradeUpDown) : undefined,
        isMe:      String(u.pix ?? u.k3e ?? '') === myPix,
      }))
      const mySetBal = players.find(player => player.isMe)?.setBal ?? 0
      stopMajakBgm()
      if (mySetBal > 0) playMajakSfx('mjkhiendwin', boardSoundOptions)
      else if (mySetBal < 0) playMajakSfx('mjkhiendlost', boardSoundOptions)
      setHanResFlags({
        hasTor: Boolean(data.hasTor),
        hasTip: Boolean(data.hasTip),
        isViewer: !players.some(p => p.isMe),
        isTournament: Boolean(data.isTournament),
      })
      gameEndStatusLines(data).forEach(line => putStatus(line, legacyPalette.normal, true))
      setHanResData(players)
    }
    SignalR.on('c32e', onGameReport)

    /**
     * playing — CMJTblUser::EndKyoSub → EVENT_PUTKYORES 相当。
     * 原典は局結果イベントで CMJKyoRes を生成する。
     * 現行サーバーは Cmd.GamePlay("playing") で playType=MJPID_ENDKYO を通知するため、
     * ここでは同イベント上に局結果ペイロードが乗った場合のみ React オーバーレイを表示する。
     */
    const onGamePlay = (data: Record<string, unknown>) => {
      if (!mounted) return
      if (data.playType === 'MJPID_INIHAN') {
        const memberInfo = Array.isArray(data.memberInfo) ? data.memberInfo : []
        const startedPlayers = memberInfo.map((member, order) => {
          if (!isRecord(member)) return null
          const engineOrder = Number(member.engineOrder ?? order)
          return readGamePlayer({ ...member, engineOrder, odr: engineOrder })
        }).filter(player => player != null)
        gameOrderLockedRef.current = startedPlayers.length > 0
        const localOdr = startedPlayers.find(player => player.playerId === myPix)?.pos
        if (localOdr !== undefined) setViewOdr(localOdr)
        if (startedPlayers.length > 0) setPlayers(prev => mergePlayers(prev, startedPlayers))
        putStatus('----対局開始----')
        if (startedPlayers.some(player => player.playerId === myPix)) playMajakSfx('mjkhistart', boardSoundOptions)
        setHanResData(null)
        setHanResFlags({ hasTor: false, hasTip: false, isViewer: false, isTournament: false })
        setKyoResData(null)
        setAskEndSet(null)
        setKyoResultAction(null)
        setActiveEmoticons([])
        setTournamentTotalResult(null)
        setShowTournamentTotalResult(false)
        return
      }
      if (data.playType === 'MJPID_INIKYO') {
        const oyaOdr = asNumber(data.oyaOrder ?? data.oyaOdr ?? data.dealerOdr, -1)
        const oya = playersRef.current.find(player => player.pos === oyaOdr)
        putStatus(oya ? `親：${oya.name || oya.playerId}` : `親：${oyaOdr}`)
        return
      }
      if (data.playType === 'MJPID_ACTION') {
        const action = asNumber(data.action, -1)
        const seatOrder = asNumber(data.seatOrder ?? data.order, -1)
        const text = actionStatusText(action, statusMemberName(playersRef.current, seatOrder))
        if (text) putStatus(text)
        return
      }
      if (data.playType !== 'MJPID_ENDKYO') return
      if (!Array.isArray(data.players)) return
      const kyoResultData = data as unknown as KyoResData
      kyoEndStatusLines(kyoResultData).forEach(line => putStatus(line))
      const pinType = Number(kyoResultData.pinType)
      if (pinType > PIN_NON && pinType !== PIN_RON && pinType !== PIN_TSU) playMajakSid(SID_DRAW, boardSoundOptions)
      playMajakSfx('mjkhiend1', boardSoundOptions)
      setKyoResData(kyoResultData)
    }
    SignalR.on(CMD_GAME_PLAY, onGamePlay)

    /** mjkc4e — CMJRoomWnd::ProcessRoomStartNewGameCommand keyGemGame status */
    const onAutoStart = (data: Record<string, unknown>) => {
      if (!mounted) return
      putGemGameStatus(data[KEY_GEM_GAME] ?? data.gemGame)
    }
    SignalR.on(CMD_AUTO_START, onAutoStart)

    /** HanChatRelay — legacy CMJRoomWnd::ProcessCommonChatService 相当 */
    const onChat = (data: Record<string, unknown>) => {
      if (!mounted) return
      const pix = String(data.k3e ?? data.pix ?? '')
      const myPix = useAuthStore.getState().player?.pix ?? ''
      const isSelf = pix !== '' && pix === myPix
      const isAbuse = Boolean(data.isAbuse ?? data.abuse ?? false)
      const isPlayer = playersRef.current.some(player => player.playerId === pix)
      const isUserViewer = !playersRef.current.some(player => player.playerId === myPix)
      if (!isSelf && !replayChannel) {
        if (!chatEnabled) return
        if (!viewerChatVisible && isUserViewer === isPlayer) return
      }
      const color = isAbuse ? legacyPalette.chatAbuse : isSelf ? legacyPalette.chatSelf : isPlayer ? legacyPalette.chatOther : legacyPalette.chatViewer
      setChatLog(prev => [
        ...prev,
        {
          id: nextMessageId(),
          name: displayNameForPix(pix),
          text: String(data.k41e ?? data.string ?? ''),
          color,
        },
      ].slice(-MAX_GAME_LOG_MESSAGES))
      if (!isAbuse) playMajakChat()
    }
    SignalR.on('hc1e', onChat)

    /** mjkc24e — HMajRoomServer::ProcessCommand_EmoticonCommand */
    const onUseEmoticon = (data: Record<string, unknown>) => {
      if (!mounted) return
      const pix = String(data[KEY_MEMBER_ID] ?? data.k3e ?? data.pix ?? '')
      const type = normalizeEmoticonType(data[KEY_EMOTICON_ID] ?? data.emoticonId ?? data.emoticonNo ?? data.mjkk19e)
      if (!pix || type == null) return
      playOrQueueEmoticon(pix, type)
    }
    SignalR.on(CMD_USE_EMOTICON, onUseEmoticon)

    const onInviteResponse = (data: Record<string, unknown>) => {
      if (!mounted) return
      const pix = String(data.k3e ?? data.pix ?? '')
      if (!pix || (pendingInviteTargetRef.current && pendingInviteTargetRef.current !== pix)) return
      const isPendingInvite = pendingInviteTargetRef.current === pix
      pendingInviteTargetRef.current = null
      const yesNo = data.k64e
      if (isPendingInvite) {
        setInviteWaiting(false)
        setInviteResult(yesNo === 'v7e' ? 'accepted' : 'declined')
      }
      const displayName = displayNameForPix(pix)
      const text = yesNo === 'v7e'
        ? `${displayName}さんがゲーム申し込みを承諾しました。`
        : yesNo === 'v6e'
          ? `${displayName}さんから応答がありませんでした。`
          : `${displayName}さんから応答がありませんでした。\n『また今 度誘ってね！』`
      const entry = { id: nextMessageId(), name: '', text, color: legacyPalette.notice }
      setChatLog(prev => [...prev, entry].slice(-MAX_GAME_LOG_MESSAGES))
      setStatusLog(prev => [...prev, entry].slice(-MAX_GAME_LOG_MESSAGES))
    }
    SignalR.on('c23e', onInviteResponse)

    /**
     * mjkc19e — CMJRoomWnd::ShowSlideAnnounce(ANNOUNCE_GET_TRICKTITLE/MAJAKTITLE)
     * 原典: MJRoomWnd3.cpp ProcessRoomCommand("mjkc19e")
     *   k25e=count, mjkk48e{N}=titleType, mjkk49e{N}=titleCode, mjkk50e{N}=titleName
     */
    const onGetTitle = (data: Record<string, unknown>) => {
      if (!mounted) return
      const count = asNumber(data[KEY_COUNT], 0)
      for (let i = 0; i < count; i++) {
        const type = asNumber(data[`${KEY_TITLE_TYPE}${i}`], -1)
        const code = asNumber(data[`${KEY_TITLE_CODE}${i}`], 0)
        if (type !== 0 && type !== 1) continue
        setAnnounceData({
          type: type as 0 | 1,
          code,
          name: String(data[`${KEY_TITLE_NAME}${i}`] ?? ''),
        })
      }
    }
    SignalR.on(CMD_GET_TITLE, onGetTitle)

    /**
     * mjkc22e — CMJRoomWnd::ShowSlideAnnounce(ANNOUNCE_GET_RYUTAMA)
     * 原典: parser(G::serviceRoom, MAJ::commandGetGem); G::keyCount = 取得数
     */
    const onGetGem = (data: Record<string, unknown>) => {
      if (!mounted) return
      const count = asNumber(data[KEY_COUNT], 0)
      setAnnounceData({ type: 2, code: count, name: '' })
      putStatus(`龍珠を${count}個獲得しました。`, legacyPalette.normal, true)
    }
    SignalR.on(CMD_GET_GEM, onGetGem)

    /**
     * mjkc23e — CMJRoomWnd::ShowSlideAnnounce(ANNOUNCE_GET_YAKUMAN)
    * 原典: HMajChnlServer::AddYakumanBonus → pix / yakuName
     */
    const onYakumanBonus = (data: Record<string, unknown>) => {
      if (!mounted) return
      setAnnounceData({
        type: 3,
        code: 0,
        name: String(data[KEY_MEMBER_ID] ?? ''),
        name2: String(data[KEY_YAKU_NAME] ?? ''),
      })
    }
    SignalR.on(CMD_YAKUMAN_BONUS, onYakumanBonus)

    return () => {
      mounted = false
      SignalR.off('c16e',                onMemberList)
      SignalR.off('c7e',                 onChannelMemberList)
      SignalR.off('c14e',                onRoomEnter)
      SignalR.off('c5e',                 onMemberJoined)
      SignalR.off('c6e',                 onMemberLeft)
      SignalR.off('mjkc5e',              onAutoExitRoom)
      SignalR.off('c32e', onGameReport)
      SignalR.off(CMD_GAME_PLAY,      onGamePlay)
      SignalR.off(CMD_AUTO_START,     onAutoStart)
      SignalR.off('hc1e',             onChat)
      SignalR.off(CMD_USE_EMOTICON,   onUseEmoticon)
      SignalR.off('c23e',             onInviteResponse)
      SignalR.off(CMD_GET_TITLE,      onGetTitle)
      SignalR.off(CMD_GET_GEM,        onGetGem)
      SignalR.off(CMD_YAKUMAN_BONUS,  onYakumanBonus)
    }
  }, [navigate, roomId, signalReady])

  useEffect(() => {
    if (statusLogRef.current) statusLogRef.current.scrollTop = statusLogRef.current.scrollHeight
    if (chatLogRef.current) chatLogRef.current.scrollTop = chatLogRef.current.scrollHeight
  }, [statusLog, chatLog])

  useEffect(() => {
    const onAskEndSet = (event: Event) => {
      const detail = (event as CustomEvent<{ roomId?: string; seatOrder?: number; actionSeq?: number; localDeadlineAt?: number }>).detail ?? {}
      setAskEndSet({
        roomId: String(detail.roomId ?? roomId ?? ''),
        seatOrder: Number(detail.seatOrder ?? effectiveMyOdrRef.current ?? 0),
        actionSeq: Number(detail.actionSeq ?? 0) || undefined,
        localDeadlineAt: Number(detail.localDeadlineAt ?? 0) || undefined,
      })
    }
    window.addEventListener(ASK_END_SET_EVENT, onAskEndSet)
    return () => window.removeEventListener(ASK_END_SET_EVENT, onAskEndSet)
  }, [gameState?.myOdr, roomId])

  useEffect(() => {
    const onKyoResultAction = (event: Event) => {
      const detail = (event as CustomEvent<{ roomId?: string; seatOrder?: number; timeLimit?: number; actionSeq?: number; localDeadlineAt?: number }>).detail ?? {}
      setKyoResultAction({
        roomId: String(detail.roomId ?? roomId ?? ''),
        seatOrder: Number(detail.seatOrder ?? effectiveMyOdrRef.current ?? 0),
        timeLimit: Math.max(0, Number(detail.timeLimit ?? 0)),
        actionSeq: Number(detail.actionSeq ?? 0) || undefined,
        localDeadlineAt: Number(detail.localDeadlineAt ?? 0) || undefined,
      })
    }
    window.addEventListener(KYO_RESULT_ACTION_EVENT, onKyoResultAction)
    return () => window.removeEventListener(KYO_RESULT_ACTION_EVENT, onKyoResultAction)
  }, [gameState?.myOdr, roomId])

  useEffect(() => {
    if (!proxyPlay || !kyoResData || !kyoResultAction) return
    const id = window.setTimeout(() => {
      void sendKyoResultAction()
    }, 3000)
    return () => window.clearTimeout(id)
  }, [proxyPlay, kyoResData, kyoResultAction])

  const sendAskEndSetAction = async (action: number) => {
    const request = askEndSet
    if (!request) return
    setAskEndSet(null)
    if (request.localDeadlineAt !== undefined && performance.now() >= request.localDeadlineAt) return
    await SignalR.send(CMD_GAME_PLAY, {
      playType: 'MJPID_ACTION',
      roomId: request.roomId,
      seatOrder: request.seatOrder,
      action,
      bipaiIndex: [],
      actionSeq: request.actionSeq,
    }).catch(() => {})
  }

  const sendKyoResultAction = async () => {
    const request = kyoResultAction
    setKyoResData(null)
    setKyoResultAction(null)
    if (!request) return
    if (request.localDeadlineAt !== undefined && performance.now() >= request.localDeadlineAt) return
    await SignalR.send(CMD_GAME_PLAY, {
      playType: 'MJPID_ACTION',
      roomId: request.roomId,
      seatOrder: request.seatOrder,
      action: ACT_PAS,
      bipaiIndex: [],
      actionSeq: request.actionSeq,
    }).catch(() => {})
  }

  const onToggleProxyPlay = () => {
    setProxyPlay(prev => {
      const next = !prev
      setAutoTsumoGiri(next)
      setAutoPass(next)
      setAutoHora(next)
      return next
    })
  }

  const onViewerRotate = (delta: 1 | 3) => {
    window.dispatchEvent(new CustomEvent(PAIFU_ROTATE_EVENT, { detail: { delta } }))
  }

  const onViewerHandToggle = () => {
    setViewerHandHidden(prev => {
      const next = !prev
      window.dispatchEvent(new CustomEvent(PAIFU_HAND_OPEN_EVENT, { detail: { open: !next } }))
      return next
    })
  }

  const exitGameToLobby = async () => {
    await SignalR.send('c9e', buildExitRoomPayload(useAuthStore.getState().player, effectiveMyOdr, isViewerUser)).catch(() => {})
    const returnChannelId = gameState?.channelId
    if (returnChannelId) navigate(`/channel/${returnChannelId}/lobby`, { replace: true })
    else navigate(-1)
  }

  useEffect(() => {
    if (!proxyPlay || !hanResData) return
    void exitGameToLobby()
  }, [proxyPlay, hanResData])

  const closeInviteDialog = () => {
    setShowInviteDialog(false)
    setInviteTargetPix(null)
    setInviteWaiting(false)
    setInviteResult(null)
    pendingInviteTargetRef.current = null
  }

  const openInviteList = () => {
    if (inviteDisabled) return
    setShowInviteDialog(true)
    setInviteTargetPix(null)
    setInviteWaiting(false)
    setInviteResult(null)
    void SignalR.send('c7e', buildGetMemberListPayload(channelId ?? '')).catch(() => {})
  }

  const onGameInvi = async (message: string) => {
    const targetPix = inviteTargetPix
    if (!targetPix) return
    pendingInviteTargetRef.current = targetPix
    const pix = useAuthStore.getState().player?.pix ?? ''
    setInviteWaiting(true)
    await SignalR.send('c22e', {
      k3e: pix,
      targetPix,
      targetMemberNo: targetPix,
      k42e: roomId ?? '',
      k65e: message,
      k64e: false,
    }).catch(() => {
      setInviteWaiting(false)
      setInviteResult('timeout')
    })
  }

  const cancelInviteWait = () => {
    const targetPix = inviteTargetPix
    if (targetPix) {
      void SignalR.send('c22e', {
        targetPix,
        targetMemberNo: targetPix,
        k42e: roomId ?? '',
        k65e: '',
        k64e: true,
      }).catch(() => {})
    }
    closeInviteDialog()
  }

  const sendEmoticon = async (index: number) => {
    if (emoticonButtonDisabled || emoticonCooldownRef.current || index < 0 || index >= EMOTICON_COUNT) return
    emoticonCooldownRef.current = true
    setEmoticonCooldown(true)
    if (emoticonCooldownTimerRef.current != null) window.clearTimeout(emoticonCooldownTimerRef.current)
    emoticonCooldownTimerRef.current = window.setTimeout(() => {
      emoticonCooldownRef.current = false
      setEmoticonCooldown(false)
      emoticonCooldownTimerRef.current = null
    }, 1000)
    await SignalR.send(CMD_USE_EMOTICON, {
      [KEY_EMOTICON_ID]: index,
      emoticonId: index,
    }).catch(() => {
      putStatus('エモート送信に失敗しました。', legacyPalette.error)
    })
  }

  useEffect(() => {
    const onEmoticonKeyDown = (event: KeyboardEvent) => {
      if (emoticonButtonDisabled) return
      if (!/^F[1-6]$/.test(event.key) || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return
      event.preventDefault()
      void sendEmoticon(Number(event.key.slice(1)) - 1)
    }
    window.addEventListener('keydown', onEmoticonKeyDown)
    return () => window.removeEventListener('keydown', onEmoticonKeyDown)
  }, [emoticonButtonDisabled])

  const canUseAutoControl = effectiveMyOdr != null && !hanResData

  const sendChat = async () => {
    if (chatInputDisabled || !chatText.trim()) return
    const text = chatText.trim()
    const pix = useAuthStore.getState().player?.pix ?? ''
    await SignalR.send('hc1e', {
      k3e: pix,
      pix,
      k38e: 'all',
      target: 'all',
      k40e: 0,
      color: 0,
      k41e: text,
      string: text,
    }).then(() => {
      setChatText('')
    }).catch(() => {
      putStatus('チャット送信に失敗しました。', legacyPalette.error)
    })
  }

  const onChatKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      e.preventDefault()
      e.stopPropagation()
      void sendChat()
    }
    if (e.key === 'Escape') setChatText('')
  }

  /** HanRes を閉じてロビーへ戻る */
  const onCloseHanRes = () => {
    const isViewer = hanResFlags.isViewer || Boolean(gameState?.isViewer)
    setHanResData(null)
    setHanResFlags({ hasTor: false, hasTip: false, isViewer: false, isTournament: false })
    setTournamentTotalResult(null)
    setShowTournamentTotalResult(false)
    const returnChannelId = gameState?.channelId
    const returnRoomId = gameState?.roomId ?? roomId
    if (isViewer) {
      const player = useAuthStore.getState().player
      void SignalR.send('c9e', {
        playerType: 'v5e',
        k57e: 'v5e',
        playerPos: -1,
        k58e: -1,
        pix: player?.pix ?? '',
        k3e: player?.pix ?? '',
        name: player?.name ?? '',
        k8e: player?.name ?? '',
      }).catch(() => {}).finally(() => {
        if (returnChannelId) {
          navigate(`/channel/${returnChannelId}/lobby`, { replace: true })
        } else {
          navigate(-1)
        }
      })
      return
    }
    if (returnChannelId && returnRoomId) {
      navigate(`/channel/${returnChannelId}/lobby/room/${returnRoomId}`, {
        replace: true,
        state: {
          mode: 'enter',
          skipEnterChannel: true,
        },
      })
    } else {
      navigate(-1)
    }
  }

  return (
    <div className="majak-ingame-viewport">
    <div className="majak-game-screen" style={{ position: 'relative', width: GAME_WIDTH, height: GAME_HEIGHT, flex: '0 0 auto', overflow: 'hidden', background: '#000', transform: desktopScale === 1 ? undefined : `scale(${desktopScale})`, transformOrigin: 'center center' }}>
      {/* Phaser マウントコンテナ */}
      <div
        ref={containerRef}
        style={{ position: 'absolute', left: 0, top: 0, width: GAME_WIDTH, height: GAME_HEIGHT, zIndex: 1 }}
      />

      <div style={{ position: 'absolute', left: 0, top: 0, width: GAME_WIDTH, height: GAME_HEIGHT, pointerEvents: 'none', zIndex: 135, overflow: 'hidden' }}>
        {activeCallAvatars.map(item => <CallAvatarPopup key={item.id} item={item} />)}
        {activeEmoticons.map(item => <EmoticonAnimation key={item.id} item={item} now={emoticonNow} />)}
      </div>

      <GameLoadingOverlay visible={syncLoading || !signalReady} />

      {/* ── CMJRoomWnd::OnPaint room title/options/notice ── */}
      <div style={{ position: 'absolute', left: 0, top: 0, width: GAME_WIDTH, height: GAME_HEIGHT, pointerEvents: 'none', zIndex: 105, overflow: 'hidden' }}>
        <div
          style={{
            position: 'absolute',
            left: 815,
            top: 49,
            width: 126,
            height: 14,
            fontFamily: 'var(--majak-font-family-ui)',
            fontSize: 'calc(12px * var(--majak-type-scale))',
            lineHeight: '14px',
            color: legacyPalette.roomTitle,
            overflow: 'hidden',
            whiteSpace: 'nowrap',
            textOverflow: 'ellipsis',
          }}
        >
          {roomTitle}
        </div>
        {optionIcons}
        <div
          style={{
            position: 'absolute',
            left: 809,
            top: 325,
            width: 200,
            height: 12,
            backgroundImage: `url(${IMG}/mj_notice.png)`,
            backgroundRepeat: 'no-repeat',
            overflow: 'hidden',
            display: announceData ? 'none' : undefined,
          }}
        />
      </div>

      {/* ── 観戦者アバター領域: legacy CMJRoomWnd AddViewer / DelViewer ── */}
      <div style={{ position: 'absolute', left: 0, top: 0, width: GAME_WIDTH, height: GAME_HEIGHT, pointerEvents: 'none', zIndex: 120, overflow: 'hidden' }}>
        <ViewerListWnd viewers={viewers} />
      </div>

      {/* ── CMJRoomWnd miniHanStatus / miniHanChat / miniChatInput ── */}
      <div style={{ position: 'absolute', left: 0, top: 0, width: GAME_WIDTH, height: GAME_HEIGHT, zIndex: 125, overflow: 'hidden' }}>
        <div
          ref={statusLogRef}
          className="majak-room-scroll"
          style={{
            position: 'absolute',
            left: 808,
            top: 81,
            width: 208,
            height: 130,
            overflowY: 'auto',
            overflowX: 'hidden',
            fontFamily: 'var(--majak-font-family-ui)',
            fontSize: 'calc(11px * var(--majak-type-scale))',
            color: '#000',
            background: 'transparent',
            pointerEvents: 'none',
            display: announceData ? 'none' : undefined,
          }}
        >
          {statusLog.map(message => (
            <div key={message.id} style={{ color: message.color ?? '#000', fontWeight: message.bold ? 'bold' : undefined }}>{message.text}</div>
          ))}
        </div>

        <div
          ref={chatLogRef}
          className="majak-room-scroll"
          style={{
            position: 'absolute',
            left: 808,
            top: 345,
            width: 208,
            height: 258,
            overflowY: 'auto',
            overflowX: 'hidden',
            fontFamily: 'var(--majak-font-family-ui)',
            fontSize: 'calc(11px * var(--majak-type-scale))',
            color: '#000',
            background: 'transparent',
          }}
        >
          {chatLog.map(message => (
            <div key={message.id} style={{ color: message.color ?? '#000', fontWeight: message.bold ? 'bold' : undefined }}>
              {message.name ? `${message.name} : ${message.text}` : message.text}
            </div>
          ))}
        </div>

        {Array.from({ length: EMOTICON_COUNT }, (_, index) => (
          <EmoticonSpriteButton
            key={index}
            index={index}
            x={884 + index * 17}
            y={603}
            disabled={emoticonButtonDisabled}
            onClick={() => void sendEmoticon(index)}
          />
        ))}

        <input
          value={chatText}
          onChange={e => setChatText(e.target.value)}
          onKeyDown={onChatKeyDown}
          maxLength={80}
          disabled={chatInputDisabled}
          style={{
            position: 'absolute',
            left: 809,
            top: 624,
            width: 201,
            height: 16,
            fontFamily: 'var(--majak-font-family-ui)',
            fontSize: 'calc(11px * var(--majak-type-scale))',
            background: legacyPalette.chatEditBack,
            color: legacyPalette.chatEditText,
            border: 'none',
            outline: 'none',
            padding: '0 2px',
            opacity: chatInputDisabled ? 0.65 : 1,
          }}
        />
      </div>

      {showGameControlButtons && (
        <div style={{ position: 'absolute', left: 0, top: 0, width: GAME_WIDTH, height: GAME_HEIGHT, pointerEvents: 'none', zIndex: 130, overflow: 'hidden' }}>
          <GameSpriteButton src={`${IMG}/mj_btAutoPass.png`} frameW={71} frameH={28} x={802} y={677} checked={autoPass} disabled={!canUseAutoControl || proxyPlay} onClick={() => setAutoPass(prev => !prev)} title="オートパス" />
          <GameSpriteButton src={`${IMG}/mj_btAutoHoura.png`} frameW={71} frameH={28} x={874} y={677} checked={autoHora} disabled={!canUseAutoControl || proxyPlay} onClick={() => setAutoHora(prev => !prev)} title="オート和了" />
          <GameSpriteButton src={`${IMG}/mj_btDaiuchi.png`} frameW={71} frameH={56} x={946} y={677} checked={proxyPlay} disabled={!canUseAutoControl} onClick={onToggleProxyPlay} title="代打ち" />
          <GameSpriteButton src={`${IMG}/mj_btTsumoGiri.png`} frameW={71} frameH={28} x={802} y={705} checked={autoTsumoGiri} disabled={!canUseAutoControl || proxyPlay} onClick={() => setAutoTsumoGiri(prev => !prev)} title="ツモ切り" />
        </div>
      )}

      {isViewerUser && (
        <div style={{ position: 'absolute', left: 0, top: 0, width: GAME_WIDTH, height: GAME_HEIGHT, pointerEvents: 'none', zIndex: 130, overflow: 'hidden' }}>
          <GameSpriteButton src={`${IMG}/mj_btPaifuRot3.png`} frameW={46} frameH={31} x={118} y={647} onClick={() => onViewerRotate(3)} title="回転3" hidden={!isViewerUser} />
          <GameSpriteButton src={`${IMG}/mj_btPaifuRot1.png`} frameW={46} frameH={31} x={164} y={647} onClick={() => onViewerRotate(1)} title="回転1" hidden={!isViewerUser} />
          <GameSpriteButton src={`${IMG}/mj_btLookSutehai.png`} frameW={116} frameH={40} x={435} y={647} onClick={() => {}} title="捨て牌表示" hidden={!isViewerUser} disabled />
          <GameSpriteButton src={`${IMG}/mj_btExitGame.png`} frameW={116} frameH={40} x={558} y={647} onClick={() => { void exitGameToLobby() }} title="退室" hidden={!isViewerUser} />
          <GameSpriteButton src={`${IMG}/mj_btPaifuHide.png`} frameW={92} frameH={25} x={118} y={676} checked={viewerHandHidden} disabled={!viewerHandOpenEnabled} onClick={onViewerHandToggle} title="手牌表示切替" hidden={!isViewerUser} />
        </div>
      )}

      <div style={{ position: 'absolute', left: 0, top: 0, width: GAME_WIDTH, height: GAME_HEIGHT, pointerEvents: 'none', zIndex: 131, overflow: 'hidden' }}>
        <GameSpriteButton src={`${IMG}/mj_btInvite.png`} frameW={106} frameH={26} x={803} y={649} disabled={inviteDisabled} onClick={openInviteList} title="招待" />
      </div>

      {showInviteDialog && (
        <GameInviteDialog
          members={channelMembers}
          targetPix={inviteTargetPix}
          waiting={inviteWaiting}
          result={inviteResult}
          onChooseTarget={setInviteTargetPix}
          onSend={message => void onGameInvi(message)}
          onCancelWait={cancelInviteWait}
          onTimeout={() => {
            setInviteWaiting(false)
            setInviteResult('timeout')
            pendingInviteTargetRef.current = null
          }}
          onBackToMembers={() => setInviteTargetPix(null)}
          onClose={closeInviteDialog}
        />
      )}

      {/* ── CMJHanRes: 半荘/東風 最終結果 (room:game_report 受信時に表示) ── */}
      {hanResData && (
        <HanRes
          players={hanResData}
          hasTor={hanResFlags.hasTor}
          hasTip={hanResFlags.hasTip}
          isViewer={hanResFlags.isViewer}
          isTournament={hanResFlags.isTournament}
          onClose={onCloseHanRes}
        />
      )}

      {hanResData && tournamentTotalResult && !showTournamentTotalResult && (
        <NextTournamentResultButton onClick={() => setShowTournamentTotalResult(true)} />
      )}

      {showTournamentTotalResult && tournamentTotalResult && (
        <TournamentTotalResultDialog items={tournamentTotalResult} players={hanResData ?? []} onClose={() => setShowTournamentTotalResult(false)} />
      )}

      {/* ── CMJKyoRes: 1局終了結果 (playing/MJPID_ENDKYO 受信時に表示) ── */}
      {kyoResData && !hanResData && (
        <LegacyKyoRes
          data={kyoResData}
          myOdr={effectiveMyOdr ?? 0}
          canContinue={Boolean(kyoResultAction)}
          onClose={() => void sendKyoResultAction()}
        />
      )}

      {/* ── CMJSlideAnnounce: スライド公告 (mjkc19e/mjkc22e/mjkc23e 受信時に自動アニメーション) ── */}
      <div style={{ position: 'absolute', left: 0, top: 0, width: GAME_WIDTH, height: GAME_HEIGHT, pointerEvents: 'none', zIndex: 500, overflow: 'hidden' }}>
        <SlideAnnounce
          data={announceData}
          onDone={() => setAnnounceData(null)}
        />
      </div>

      {askEndSet && (
        <AskEndDlg
          onYes={() => void sendAskEndSetAction(ACT_RON)}
          onNo={() => void sendAskEndSetAction(ACT_PAS)}
        />
      )}
    </div>
    </div>
  )
}
