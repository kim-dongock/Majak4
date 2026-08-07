/**
 * CMajakChannelWnd 相当 — ロビー (チャンネル) 画面 (AP-09 §1-5)
 * レガシー: legacy/client/HgMajak2/MajakChannelWnd.h/cpp
 *
 * ウィンドウサイズ: 1014×704px (CMajakStadiumWnd と同サイズ)
 *
 * 主要コンポーネント配置 (CMajakChannelWnd::OnCreate() に準拠):
 *   - ルームリスト (CHgRoomListWnd): 背景左パネル x=8-668, y=52-534
 *   - メンバーリスト (CHgMemberListWnd): MoveWindow(678, 212, 336, 403)
 *   - アイコンボタン群 (y=622/659/696)
 *   - 無料補充ボタン (866,171)
 */

import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { useState, useEffect, useRef } from 'react'
import * as SignalR from '../../api/signalr'
import { getChannelServerUrl, getBestServer, getChannels, getPlayerContinueRoom } from '../../api/channel'
import { useAuthStore } from '../../store/authStore'
import { useCustomSkinStore } from '../../store/customSkinStore'
import { showConfirm as showConfirmMessage, showError, showMessage, checkResult, isOk, tournamentErrorMessage, tournamentRegistErrorMessage } from '../../utils/msgbox'
import { getAvatarUrl, getShortAvatarUrl, getDefaultAvatarUrl } from '../../utils/resources'
import { configureMajakSound } from '../../utils/majakSound'
import { readNoticePayload, type NoticeDisplay } from '../../utils/notice'
import { sendAccuseComplaint } from '../../utils/accuse'
import { getTabSessionId } from '../../utils/tabSession'
import WelcomeDlg    from './dialogs/WelcomeDlg'
import GetReqGameDialog from './dialogs/GetReqGameDialog'
import PlayerInfoWnd, { type PlayerInfo as DlgPlayerInfo } from './dialogs/PlayerInfoWnd'
import OptDlg, { DEFAULT_OPTION, optionToString, type MJOption, type MJOptionMask } from './dialogs/OptDlg'
import RoomCreateDlg, { type RoomCreateInfo } from './dialogs/RoomCreateDlg'
import CfgDlg, { loadMajakConfig, saveMajakConfig, type MJConfig } from './dialogs/CfgDlg'
import ItemShopDlg from './dialogs/ItemShopDlg'
import ConfirmItemDlg, { normalizeRawMajItem, type RawMajItem } from './dialogs/ConfirmItemDlg'
import CustomDlg from './dialogs/CustomDlg'
import MissionDlg from './dialogs/MissionDlg'
import RankingDlg, { type RankingData } from './dialogs/RankingDlg'
import TournamentRegistDlg, { type TournamentRegistPayload } from './dialogs/TournamentRegistDlg'
import AccuseDlg from './dialogs/AccuseDlg'
import { MAJAK_ACCUSE_EVENT, MAJAK_EXIT_REQUEST_EVENT } from '../../components/MajakFrame'
import { useOutgameLayoutMode } from '../../hooks/useOutgameLayoutMode'

const IMG = '/assets/images/game'
const ABANDON_ROOM_STORAGE_KEY = 'majak:abandonRoomOnNextLobbyEnter'

function readAbandonRoomOnEnter(channelId: string) {
  const raw = window.sessionStorage.getItem(ABANDON_ROOM_STORAGE_KEY)
  if (!raw) return null
  try {
    const value = JSON.parse(raw) as { channelId?: string; roomId?: number; fatalRoomError?: boolean }
    if (value.channelId !== channelId || !value.roomId) return null
    return value
  } catch {
    return null
  }
}

function clearAbandonRoomOnEnter() {
  window.sessionStorage.removeItem(ABANDON_ROOM_STORAGE_KEY)
}

function buildEnterChannelPayload(
  channelId: string,
  player: ReturnType<typeof useAuthStore.getState>['player'],
  abandonRoomId = 0,
  abandonRoomAfterFatalError = false,
) {
  const subId = channelId.length >= 11 ? channelId.substring(6, 11) : channelId
  const pix = player?.pix ?? ''
  const avatarId = player?.avatarId ?? ''
  const nickname = player?.name ?? ''
  return {
    gameId: 'MAJAK4',
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
    abandonPreviousRoom: abandonRoomId > 0,
    abandonRoomId,
    abandonRoomAfterFatalError,
  }
}

/** ====================================================================
 * CMJBmpButton 相当 — AP-06 §2 4フレームスプライトボタン
 * ==================================================================== */
function SpriteButton({
  src,
  frameW,
  frameH,
  x,
  y,
  onClick,
  title,
  hidden,
  disabled,
}: {
  src: string
  frameW: number
  frameH: number
  x: number
  y: number
  onClick: () => void
  title?: string
  hidden?: boolean
  disabled?: boolean
}) {
  const [frameIdx, setFrameIdx] = useState(0)
  if (hidden) return null
  const visibleFrameIdx = disabled ? 1 : frameIdx
  return (
    <button
      title={title}
      disabled={disabled}
      onClick={disabled ? undefined : onClick}
      onMouseEnter={() => { if (!disabled) setFrameIdx(2) }}
      onMouseLeave={() => { if (!disabled) setFrameIdx(0) }}
      onMouseDown={() => { if (!disabled) setFrameIdx(3) }}
      onMouseUp={() => { if (!disabled) setFrameIdx(2) }}
      style={{
        position: 'absolute',
        left: x,
        top: y,
        width: frameW,
        height: frameH,
        display: 'block',
        appearance: 'none',
        WebkitAppearance: 'none',
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-visibleFrameIdx * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        backgroundColor: 'transparent',
        border: 'none',
        padding: 0,
        margin: 0,
        cursor: disabled ? 'default' : 'pointer',
        outline: 'none',
        opacity: 1,
        imageRendering: 'pixelated',
      }}
    />
  )
}

function MobileLobbyCommandButton({
  onClick,
  children,
  hidden,
  disabled,
}: {
  onClick: () => void
  children: React.ReactNode
  hidden?: boolean
  disabled?: boolean
}) {
  if (hidden) return null
  return (
    <button
      type="button"
      className="majak-mobile-lobby-command-button"
      disabled={disabled}
      onClick={disabled ? undefined : onClick}
    >
      {children}
    </button>
  )
}

/** ====================================================================
 * ルームエントリー型 — API から受け取る型 (ApiRoomEntry) と同型にする
 * ==================================================================== */
interface RoomEntry {
  roomId:     number
  title:      string
  memberCnt:  number
  memberMax:  number
  viewerCnt:  number
  opMemberCnt: number
  seats:      RoomSeat[]
  isPrivate:  boolean
  maxViewer?: number
  roomOption: string
  serverUrl:  string
  state?:      number
  roomPlaying?: number
}

interface RoomSeat {
  pix: string
  pos: number
  disconnected: boolean
  avatarId?: string
  sex?: 'male' | 'female'
}

interface RoomAvatarMember {
  pix: string
  avatarId: string
  sex: 'male' | 'female'
}

function readRoomSeats(r: Record<string, unknown>, memberCnt: number, opMemberCnt: number): RoomSeat[] {
  if (Array.isArray(r.seats)) {
    return (r.seats as Array<Record<string, unknown>>)
      .map((seat, index) => ({
        pix: String(seat.pix ?? seat.k3e ?? seat['member' + 'Id'] ?? ''),
        pos: Number(seat.pos ?? seat.k43e ?? index),
        disconnected: Boolean(seat.disconnected ?? false),
        avatarId: seat.avatarId != null || seat.k7e != null ? String(seat.avatarId ?? seat.k7e) : undefined,
        sex: (seat.sex === 'F' || seat.sex === 'female' ? 'female' : 'male') as 'male' | 'female',
      }))
      .filter(seat => seat.pos >= 0 && seat.pos < 4)
  }

  const seats: RoomSeat[] = []
  for (let i = 0; i < memberCnt; i++) {
    seats.push({
      pix: String(r[`k3e${i}`] ?? ''),
      pos: Number(r[`k43e${i}`] ?? i),
      disconnected: false,
      avatarId: r[`k7e${i}`] != null ? String(r[`k7e${i}`]) : undefined,
      sex: (r[`k11e${i}`] === 'F' || r[`k11e${i}`] === 'female' ? 'female' : 'male') as 'male' | 'female',
    })
  }
  for (let i = 0; i < opMemberCnt; i++) {
    seats.push({
      pix: String(r[`k4e${i}`] ?? ''),
      pos: Number(r[`k189e${i}`] ?? memberCnt + i),
      disconnected: true,
      avatarId: r[`k7e${memberCnt + i}`] != null ? String(r[`k7e${memberCnt + i}`]) : undefined,
      sex: (r[`k11e${memberCnt + i}`] === 'F' || r[`k11e${memberCnt + i}`] === 'female' ? 'female' : 'male') as 'male' | 'female',
    })
  }
  return seats.filter(seat => seat.pos >= 0 && seat.pos < 4)
}

function readRoomEntry(r: Record<string, unknown>): RoomEntry {
  const memberCnt = Number(r.memberCnt ?? r.playerCnt ?? r.k48e ?? 0)
  const opMemberCnt = Number(r.opMemberCnt ?? r.k188e ?? 0)
  return {
    roomId:     Number(r.roomId ?? r.k42e ?? 0),
    title:      String(r.title ?? r.k45e ?? ''),
    memberCnt,
    memberMax:  Number(r.memberMax ?? r.k66e ?? 4),
    viewerCnt:  Number(r.viewerCnt ?? r.k49e ?? 0),
    opMemberCnt,
    seats:      readRoomSeats(r, memberCnt, opMemberCnt),
    isPrivate:  Boolean(r.isPrivate ?? r.k68e === 'Y'),
    maxViewer:  r.maxViewer != null || r.k69e != null ? Number(r.maxViewer ?? r.k69e) : undefined,
    roomOption: String(r.roomOption ?? r.k46e ?? ''),
    serverUrl:  String(r.serverUrl ?? ''),
    state:      r.state != null || r.k47e != null ? Number(r.state ?? r.k47e) : undefined,
    roomPlaying: r.roomPlaying != null || r.k143e != null ? Number(r.roomPlaying ?? r.k143e) : undefined,
  }
}

function readLegacyRoomList(data: Record<string, unknown>): RoomEntry[] {
  const roomCount = Number(data.k51e ?? 0)
  const rooms: RoomEntry[] = []

  for (let i = 1; i <= roomCount; i++) {
    const raw = data[`k42e${i}`]
    if (typeof raw !== 'string' || raw.length === 0) continue

    const params = new URLSearchParams(raw)
    const entry: Record<string, unknown> = {}
    params.forEach((value, key) => { entry[key] = value })
    rooms.push(readRoomEntry(entry))
  }

  return rooms
}

function mergeLegacyRoomSeats(data: Record<string, unknown>, rooms: RoomEntry[]): RoomEntry[] {
  const legacyByRoomId = new Map(readLegacyRoomList(data).map(room => [room.roomId, room]))
  return rooms.map(room => {
    const legacyRoom = legacyByRoomId.get(room.roomId)
    if (!legacyRoom || legacyRoom.seats.length === 0) return room
    return { ...room, seats: legacyRoom.seats, opMemberCnt: legacyRoom.opMemberCnt }
  })
}

function normalizeRoomCellSeats(room: RoomEntry): RoomSeat[] {
  const seats = room.seats.filter(seat => seat.pos >= 0 && seat.pos < 4).slice(0, 4)
  const occupiedMemberCount = Math.min(room.memberCnt + room.opMemberCnt, 4)
  if (seats.length >= occupiedMemberCount) return seats

  const usedPositions = new Set(seats.map(seat => seat.pos))
  const activeMissing = Math.max(0, room.memberCnt - seats.filter(seat => !seat.disconnected).length)
  const disconnectedMissing = Math.max(0, room.opMemberCnt - seats.filter(seat => seat.disconnected).length)
  const appendMissing = (count: number, disconnected: boolean) => {
    for (let i = 0; i < count && seats.length < occupiedMemberCount; i++) {
      const pos = [0, 1, 2, 3].find(candidate => !usedPositions.has(candidate))
      if (pos === undefined) return
      usedPositions.add(pos)
      seats.push({ pix: '', pos, disconnected })
    }
  }

  appendMissing(activeMissing, false)
  appendMissing(disconnectedMissing, true)
  return seats
}

function readLegacyRoomState(data: Record<string, unknown>): RoomEntry | null {
  const roomId = Number(data.roomId ?? data.k42e ?? 0)
  const raw = typeof data.roomInfo === 'string'
    ? data.roomInfo
    : roomId > 0 && typeof data[`k42e${roomId}`] === 'string'
      ? String(data[`k42e${roomId}`])
      : ''

  if (!raw) return roomId > 0 ? readRoomEntry(data) : null

  const entry: Record<string, unknown> = {}
  const params = new URLSearchParams(raw)
  params.forEach((value, key) => { entry[key] = value })

  return readRoomEntry({ ...entry, ...data })
}

function findContinueRoomForPix(rooms: RoomEntry[], pix: string): { room: RoomEntry; needsAutoEnter: boolean } | undefined {
  if (!pix) return undefined
  for (const room of rooms) {
    const ownSeat = room.seats.find(seat => seat.pix === pix)
    if (!ownSeat) continue
    const isPlayingRoom = room.state === 2 || Number(room.roomPlaying ?? 0) > 0
    if (ownSeat.disconnected) return { room, needsAutoEnter: true }
    if (isPlayingRoom) return { room, needsAutoEnter: false }
  }
  return undefined
}

function buildContinueAutoEnterPayload(room: RoomEntry, pix: string) {
  return {
    roomId: room.roomId,
    k42e: room.roomId,
    pix,
    k3e: pix,
    connectFor: 'GameJoin',
    k82e: 'v16e',
    playerType: 'v4e',
    k57e: 'v4e',
    roomTitle: room.title,
    k45e: room.title,
    roomPwd: '',
    k67e: '',
    roomOption: room.roomOption,
    k46e: room.roomOption,
  }
}

/** ====================================================================
 * CHgRoomListWnd 相当 — ルームリスト
 * レガシー: m_RoomSetting.m_rectRoomListWnd = {15,87,669,485}
 *   → frame(15,87)→content(15,56), 654×398
 * mj_rmimg.png:    4フレーム 150×133
 * mj_bncrall.png:  16フレーム 41×19 — 作成(0)/参加(4)/観戦(8) × 4状態
 * ==================================================================== */
const DEFAULT_ROOM_SLOT_COUNT = 12
const COLS    = 4
const TABLE_W = 150
const TABLE_H = 133
const ROOM_MARGIN = 6
const ROOM_STEP_X = TABLE_W + ROOM_MARGIN
const ROOM_STEP_Y = TABLE_H + ROOM_MARGIN
const LOBBY_LEFT_NUDGE = 8
const ROOM_JOIN = 1
const ROOM_JOINREADY = 2
const ROOM_GAMEJOIN = 4
const ROOM_GAMEVIEW = 5
const ROOM_GAMEFULL = 6
const CHAT_INIT_MESSAGE = '"/?"と打つと、チャットコマンド一覧が表示されます。'
const CHAT_COMMAND_MESSAGES = [
  '/M <userid> : Mute - 指定した人の発言を見えないようにします。',
  '/U <userid> : Unmute - 上のmuteを解除します。',
  '/W <userid> <message> : Whisper - 指定した人にささやきます。',
  '/L : Location - 自分の現在位置(ロビー/部屋)を表示します。',
]
const CHAT_MUTE_DONE = 'これから %sさんの発言を無視します。'
const CHAT_UNMUTE_DONE = '%sさんの発言の無視を解除しました。'
const CHAT_LOCATION_PREFIX = '現在位置 : '
const CHAT_TARGET_ALL = 'v3e'
const AUTO_MATCH_GUIDE_MESSAGES = [
  'このロビーで対戦するには「対戦申し込み」してください。',
  '対戦組み合わせが自動的に行われます。',
]
const AUTO_MATCH_ENTRY_MESSAGE = '対戦申し込み中です。しばらくお待ちください。'
const AUTO_MATCH_ABORT_MESSAGE = '対戦申し込みを取り消しました。'
const AUTO_MATCH_WATCHES_WARN_MESSAGE = '対局参加表明中は観戦できません。'

const KOURYU_FIELD_IDS = ['0082B', '0086B', '0085F', '0075B', '00T5A', '00000']
const DANI_FIELD_IDS = ['0ZG6A', '0ZG6B', '0ZG6C', '0ZG6D', '0ZG7A', '0ZG7B', '0ZG7C', '0ZG7D']

function getLobbySelectGroup(channelId?: string) {
  if (!channelId) return undefined
  if (DANI_FIELD_IDS.includes(channelId)) return 'dani'
  if (KOURYU_FIELD_IDS.includes(channelId)) return 'kouryu'
  return undefined
}

function isAutoMatchingChannel(channelId?: string) {
  return (channelId ?? '')[1] === 'Z'
}

function isDaniChannel(channelId?: string) {
  return (channelId ?? '')[2] === 'G'
}

function isTrainingChannel(channelId?: string) {
  return (channelId ?? '')[2] === 'T'
}

function isReplayChannel(channelId?: string) {
  return (channelId ?? '')[2] === 'V'
}

function isTournamentChannel(channelId?: string) {
  return (channelId ?? '')[2] === 'H'
}

interface RoomOptionState {
  nSet: number
  nUma: number
  nSpd: number
  bKui: boolean
  bTor: boolean
  nRed: number
  bOpenHand: boolean
  bViewChat: boolean
  nContest: number
  bWar: boolean
  bTip: boolean
  nRon: number
  bEnableChat: boolean
}

function readRoomOptionFlag(value: string | undefined): boolean {
  return value === '1'
}

function readRoomOptionNumber(value: string | undefined, fallback: number, max: number): number {
  if (value == null) return fallback
  const number = value.charCodeAt(0) - 48
  return number >= 0 && number < max ? number : fallback
}

function parseRoomOption(optionText: string): RoomOptionState | null {
  const text = optionText.trim()
  if (![8, 9, 10, 13, 14, 15].includes(text.length)) return null

  const option: RoomOptionState = {
    nSet: 1,
    nUma: 1,
    nSpd: 2,
    bKui: false,
    bTor: false,
    nRed: 0,
    bOpenHand: false,
    bViewChat: false,
    nContest: 0,
    bWar: false,
    bTip: false,
    nRon: 0,
    bEnableChat: false,
  }

  if (text.length >= 13) {
    option.bWar = readRoomOptionFlag(text[10])
    option.bTip = readRoomOptionFlag(text[11])
    option.nRon = readRoomOptionNumber(text[12], option.nRon, 3)
    option.bEnableChat = readRoomOptionFlag(text[14])
  }
  if (text.length >= 9) {
    option.nContest = readRoomOptionNumber(text[8], option.nContest, 3)
  }

  option.nSet = readRoomOptionNumber(text[0], option.nSet, 2)
  option.nUma = readRoomOptionNumber(text[1], option.nUma, 4)
  option.nSpd = readRoomOptionNumber(text[2], option.nSpd, 4)
  option.bKui = readRoomOptionFlag(text[3])
  option.bTor = readRoomOptionFlag(text[4])
  option.nRed = readRoomOptionNumber(text[5], option.nRed, 3)
  option.bOpenHand = readRoomOptionFlag(text[6])
  option.bViewChat = readRoomOptionFlag(text[7])
  return option
}

function maskRoomOption(option: RoomOptionState, channelId?: string): RoomOptionState {
  const subId = channelId ?? ''
  const masked = { ...option, nContest: 0 }
  if (subId === '00000') return { ...option }
  if (subId[1] === 'Z') {
    masked.nSpd = subId[2] === 'R' ? 1 : 0
    return masked
  }
  if (subId[2] === 'V' || subId[2] === 'D') return masked
  if (subId[2] === 'R') {
    return {
      ...masked,
      nSet: 1,
      bKui: false,
      nRed: 0,
      bTor: false,
      bWar: false,
      bTip: false,
      nRon: 0,
      nUma: subId[3] === '0' ? 2 : 1,
      bEnableChat: false,
      bViewChat: false,
      bOpenHand: false,
      nContest: readRoomOptionNumber(subId[3], 0, 3) + 1,
    }
  }

  if (subId[2] === '9') {
    masked.bWar = false
    masked.bTip = false
  } else if (subId[2] === '8') {
    masked.bWar = false
  } else if (subId[2] === '7') {
    masked.bWar = true
  }
  if (subId[3] === '4') masked.nRed = 0
  if (subId[3] === '0' || subId[3] === '1' || subId[3] === '6') masked.nSet = 0
  if (subId[3] === '2' || subId[3] === '3' || subId[3] === '7') masked.nSet = 1
  if (subId[3] === '0' || subId[3] === '2') masked.bKui = false
  if (subId[3] === '1' || subId[3] === '3') masked.bKui = true
  return masked
}

function buildRoomOptionMask(channelId?: string): MJOptionMask {
  const subId = channelId ?? ''
  const mask: MJOptionMask = {}
  if (subId === '00000') return mask

  if (subId[1] === 'Z') {
    mask.nSpd = subId[2] === 'R' ? 1 : 0
  }

  mask.nContest = 0
  if (subId[2] === 'V' || subId[2] === 'D') return mask

  if (subId[2] === 'R') {
    return {
      ...mask,
      nSet: 1,
      bKui: 0,
      nRed: 0,
      bTor: 0,
      bWar: 0,
      bTip: 0,
      nRon: 0,
      nUma: subId[3] === '0' ? 2 : 1,
      bEnableChat: 0,
      bViewChat: 0,
      bOpenHand: 0,
      nContest: readRoomOptionNumber(subId[3], 0, 9) + 1,
    }
  }

  if (subId[2] === 'M') {
    if (subId[3] === '0') {
      return {
        ...mask,
        nSet: 1,
        bKui: 0,
        nRed: 2,
        bTor: 1,
        bWar: 1,
        bTip: 1,
        nRon: 2,
        nUma: 2,
        bEnableChat: 0,
        bViewChat: 0,
        bOpenHand: 0,
        nContest: 0,
      }
    }
    return mask
  }

  if (subId[2] === '9') {
    mask.bWar = 0
    mask.bTip = 0
  } else if (subId[2] === '8') {
    mask.bWar = 0
  } else if (subId[2] === '7') {
    mask.bWar = 1
  }

  if (subId[3] === '4') mask.nRed = 0
  if (subId[3] === '0' || subId[3] === '1' || subId[3] === '6') mask.nSet = 0
  if (subId[3] === '2' || subId[3] === '3' || subId[3] === '7') mask.nSet = 1
  if (subId[3] === '0' || subId[3] === '2') mask.bKui = 0
  if (subId[3] === '1' || subId[3] === '3') mask.bKui = 1

  return mask
}

function optionFrame(value: number | boolean): number {
  return typeof value === 'boolean' ? (value ? 1 : 0) : value
}

function isRoomOptionMasked(mask: MJOptionMask, key: keyof MJOptionMask): boolean {
  return Object.prototype.hasOwnProperty.call(mask, key)
}

function RoomOptionIcon({ src, frame }: { src: string; frame: number }) {
  return (
    <span
      aria-hidden="true"
      className="majak-lobby-room-rule-icon"
      style={{
        backgroundImage: `url(${IMG}/${src})`,
        backgroundPosition: `${-frame * 12}px 0`,
        backgroundRepeat: 'no-repeat',
        backgroundSize: 'auto 12px',
      }}
    />
  )
}

function RoomOptionIcons({ optionText, channelId }: { optionText: string; channelId?: string }) {
  const parsed = parseRoomOption(optionText)
  if (!parsed) return null
  const option = maskRoomOption(parsed, channelId)
  const displayMask = buildRoomOptionMask(channelId)

  if (isAutoMatchingChannel(channelId)) {
    return (
      <>
        <RoomOptionIcon src="mj_opt_4.png" frame={option.nSpd} />
        {!isDaniChannel(channelId) && <RoomOptionIcon src="mj_optcon.png" frame={option.nContest} />}
      </>
    )
  }

  return (
    <>
      {!isRoomOptionMasked(displayMask, 'nSet') && <RoomOptionIcon src="mj_opt_0.png" frame={option.nSet} />}
      {!isRoomOptionMasked(displayMask, 'bKui') && <RoomOptionIcon src="mj_opt_3.png" frame={optionFrame(option.bKui)} />}
      {!isRoomOptionMasked(displayMask, 'nUma') && <RoomOptionIcon src="mj_opt_1.png" frame={option.nUma} />}
      {!isRoomOptionMasked(displayMask, 'nRon') && <RoomOptionIcon src="mj_optron.png" frame={option.nRon} />}
      {!isRoomOptionMasked(displayMask, 'nRed') && <RoomOptionIcon src="mj_opt_5.png" frame={option.nRed} />}
      {!isRoomOptionMasked(displayMask, 'bTor') && <RoomOptionIcon src="mj_opt_2.png" frame={optionFrame(option.bTor)} />}
      {!isRoomOptionMasked(displayMask, 'bWar') && <RoomOptionIcon src="mj_optwar.png" frame={optionFrame(option.bWar)} />}
      {!isRoomOptionMasked(displayMask, 'bTip') && <RoomOptionIcon src="mj_opttip.png" frame={optionFrame(option.bTip)} />}
      {!isRoomOptionMasked(displayMask, 'nSpd') && <RoomOptionIcon src="mj_opt_4.png" frame={option.nSpd} />}
      {!isRoomOptionMasked(displayMask, 'bOpenHand') && <RoomOptionIcon src="mj_opt_6.png" frame={optionFrame(option.bOpenHand)} />}
      {!isRoomOptionMasked(displayMask, 'bEnableChat') && !isRoomOptionMasked(displayMask, 'bViewChat') && <RoomOptionIcon src={option.bEnableChat ? 'mj_opt_7.png' : 'mj_opt_8.png'} frame={option.bEnableChat ? optionFrame(option.bViewChat) : 0} />}
    </>
  )
}

function buildTournamentMemberPayload(pix: string) {
  return { pix, k3e: pix }
}

function buildTournamentNoPayload(pix: string, seqNo: number) {
  return { pix, k3e: pix, tournamentNo: seqNo, mjkk88e: seqNo }
}

interface LobbyLocationState {
  lobbyOption?: Partial<MJOption>
  leavingRoom?: boolean
}

function RoomListPanel({
  rooms,
  members,
  slotCount,
  variant = 'desktop',
  channelId,
  onEnter,
  onCreateRoom,
  directRoomActionDisabled,
}: {
  rooms: RoomEntry[]
  members: RoomAvatarMember[]
  slotCount: number
  channelId?: string
  variant?: 'desktop' | 'mobile'
  onEnter: (roomId: string) => void
  onCreateRoom: (slotNo: number) => void
  directRoomActionDisabled?: boolean
}) {
  // レガシー: m_RoomSetting.m_nRoomCount = keyMaxRoom。最大ルーム数分だけ空き部屋を描画する。
  const slots: (RoomEntry | null)[] = Array.from({ length: slotCount }, (_, i) =>
    rooms.find(r => r.roomId === i + 1) ?? null
  )

  if (variant === 'mobile') {
    return (
      <div className="majak-mobile-lobby-room-grid">
        {slots.map((room, i) => (
          <RoomCell
            key={i}
            slotNo={i + 1}
            room={room}
            members={members}
            channelId={channelId}
            variant="mobile"
            onEnter={onEnter}
            onCreateRoom={onCreateRoom}
            directRoomActionDisabled={directRoomActionDisabled}
          />
        ))}
      </div>
    )
  }

  return (
    <div
      className="majak-desktop-lobby-room-list"
      style={{
        position: 'absolute',
        left: 15 - LOBBY_LEFT_NUDGE,
        top: 56,
        width: 654,
        height: 398,
        overflowY: 'auto',
        overflowX: 'hidden',
      }}
    >
      {slots.map((room, i) => (
        <RoomCell
          key={i}
          slotNo={i + 1}
          room={room}
          members={members}
          channelId={channelId}
          onEnter={onEnter}
          onCreateRoom={onCreateRoom}
          directRoomActionDisabled={directRoomActionDisabled}
        />
      ))}
    </div>
  )
}

/** ====================================================================
 * RoomCell — 1室分のエントリー
 * mj_bncrall.png 16フレーム×41px:
 *   frame 0-3  : 作成 (normal/disabled/hover/pressed)
 *   frame 4-7  : 参加 (normal/disabled/hover/pressed)
 *   frame 8-11 : 観戦 (normal/disabled/hover/pressed)
 * ==================================================================== */
function RoomCell({
  slotNo, room, members, channelId, onEnter, onCreateRoom, directRoomActionDisabled,
  variant = 'desktop',
}: {
  slotNo: number
  room: RoomEntry | null
    variant?: 'desktop' | 'mobile'
  members: RoomAvatarMember[]
  channelId?: string
  onEnter: (roomId: string, asViewer: boolean) => void
  onCreateRoom: (slotNo: number) => void
  directRoomActionDisabled?: boolean
}) {
  const isEmpty  = room === null
  const occupiedMemberCount = !isEmpty ? room.memberCnt + room.opMemberCnt : 0
  const isFull   = !isEmpty && occupiedMemberCount >= room.memberMax
  const roomState = room?.state
  const isViewable = !isEmpty
    && (roomState != null ? roomState === ROOM_GAMEVIEW : isFull)
  const isJoinable = !isEmpty
    && (roomState != null
      ? roomState === ROOM_JOIN || roomState === ROOM_JOINREADY || roomState === ROOM_GAMEJOIN
      : !isFull)
  const hasRoomAction = isEmpty || isJoinable || isViewable
  const roomActionBlocked = Boolean(directRoomActionDisabled && !isViewable)
  const isMobile = variant === 'mobile'
  const seats: RoomSeat[] = !isEmpty && room.seats.length > 0
    ? normalizeRoomCellSeats(room)
    : Array.from({ length: Math.min(occupiedMemberCount, 4) }, (_, pos) => ({ pix: '', pos, disconnected: false }))

  const isPlayingRoom = !isEmpty
    && (roomState === ROOM_GAMEJOIN || roomState === ROOM_GAMEVIEW || roomState === ROOM_GAMEFULL || Number(room.roomPlaying ?? 0) > 0)
  const cellScale = isMobile ? 'var(--majak-mobile-room-cell-scale, 0.68)' : '1'
  const cellSize = (value: number) => isMobile ? `calc(${value}px * ${cellScale})` : value
  const orderedSeats = [...seats].sort((left, right) => left.pos - right.pos)
  const roomStatus = isEmpty ? '空室' : isPlayingRoom ? '対局中' : isFull ? '満席' : '待機中'

  const handleClick = () => {
    if (roomActionBlocked || !hasRoomAction) return
    if (isEmpty) onCreateRoom(slotNo)
    else onEnter(String(room.roomId), isViewable)
  }

  return (
    <div
      className={`majak-lobby-room-cell${isMobile ? ' majak-lobby-room-cell--mobile' : ' majak-lobby-room-cell--desktop'}`}
      style={{
        position: variant === 'mobile' ? 'relative' : 'absolute',
        left: variant === 'mobile' ? undefined : ROOM_MARGIN + ROOM_STEP_X * ((slotNo - 1) % COLS),
        top: variant === 'mobile' ? undefined : ROOM_MARGIN + ROOM_STEP_Y * Math.floor((slotNo - 1) / COLS),
        width: cellSize(TABLE_W),
        height: cellSize(TABLE_H),
        flex: '0 0 auto',
        overflow: 'visible',
      }}
      onDoubleClick={handleClick}
    >
      <div style={{
        position: 'absolute',
        left: 0,
        top: 0,
        width: TABLE_W,
        height: TABLE_H,
        transform: `scale(${cellScale})`,
        transformOrigin: 'left top',
        overflow: 'visible',
      }}>
        <article className={`majak-lobby-room-card${isEmpty ? ' is-empty' : ''}${isPlayingRoom ? ' is-playing' : ''}${isFull ? ' is-full' : ''}`}>
          <header className="majak-lobby-room-card__head">
            <span className="majak-lobby-room-card__number">{String(slotNo).padStart(2, '0')}</span>
            <strong className="majak-lobby-room-card__title">{room?.title || '空きルーム'}</strong>
            {!isEmpty && room.isPrivate && <span className="majak-lobby-room-card__lock" aria-label="パスワードあり">鍵</span>}
            <span className="majak-lobby-room-card__capacity">{occupiedMemberCount}/{room?.memberMax ?? 4}</span>
          </header>
          <div className="majak-lobby-room-card__seats" aria-label={`${occupiedMemberCount}人参加中`}>
            {[0, 1, 2, 3].map(seatIndex => {
              const seat = orderedSeats[seatIndex]
              const member = seat ? members.find(item => item.pix === seat.pix) : undefined
              const avatarId = seat?.avatarId ?? member?.avatarId
              const sex = seat?.sex ?? member?.sex ?? 'male'
              return (
                <span key={seat ? `${seat.pix || 'seat'}-${seat.pos}-${seatIndex}` : `empty-${seatIndex}`} className={`majak-lobby-room-card__seat${seat ? ' is-occupied' : ''}${seat?.disconnected ? ' is-disconnected' : ''}`}>
                  {seat && <img src={avatarId ? getShortAvatarUrl(avatarId) : getDefaultAvatarUrl(sex)} alt="" draggable={false} onError={event => { event.currentTarget.src = getDefaultAvatarUrl(sex) }} />}
                </span>
              )
            })}
          </div>
          <div className="majak-lobby-room-card__bottom">
            <div className="majak-lobby-room-rules">{!isEmpty && <RoomOptionIcons optionText={room.roomOption} channelId={channelId} />}</div>
            <div className="majak-lobby-room-card__action-row">
              <span className={`majak-lobby-room-card__status${isPlayingRoom ? ' is-playing' : ''}${isFull ? ' is-full' : ''}`}>{roomStatus}</span>
              {hasRoomAction && !roomActionBlocked && <button type="button" className={`majak-lobby-room-action-button${isViewable ? ' is-watch' : ''}`} onClick={handleClick}>{isEmpty ? '作成' : isViewable ? '観戦' : '参加'}</button>}
            </div>
          </div>
        </article>
      </div>
    </div>
  )
}

/** ====================================================================
 * CMJMemberListWnd 相当 — メンバーリスト (678,212, 336×403px)
 * 背景: mj_userlist_bg.png (336×403)
 *
 * CMJMemberListFilter.CheckMember() 相当:
 *   条件フィルターは性別/年齢/称号(段位)の条件ダイアログで決まるため、
 *   条件未設定の初期状態では全メンバーを表示する。
 *
 * フィルターボタン:
 *   mj_btn_userlist_filter.png (84×36, 4フレーム 21×36)  IDC_MEMBERLIST_CONDITION
 *   mj_btn_userlist_all.png   (84×36, 4フレーム 21×36)   IDC_MEMBERLIST_ALL
 * ==================================================================== */
interface MemberEntry {
  pix: string
  name: string
  rating: number
  slevel: string
  nlevel: number
  location: string
  winCount: number
  loseCount: number
  drawCount: number
  sex: 'male' | 'female'
  avatarId: string
  roomId: number
}

function formatMemberLocation(roomId: number): string {
  return roomId > 0 ? `${roomId}番部屋` : 'ロビー'
}

function readMemberEntry(m: Record<string, unknown>): MemberEntry {
  const roomId = Number(m.k42e ?? m.roomId ?? 0)
  return {
    pix: String(m.k3e ?? m.pix ?? m['member' + 'Id'] ?? ''),
    name:     String(m.k8e ?? m.nickname ?? m.name ?? ''),
    rating:   Number(m.k31e ?? m.rating ?? 0),
    slevel:   String(m.k32e ?? m.slevel ?? ''),
    nlevel:   Number(m.k33e ?? m.nlevel ?? m.nLevel ?? 0),
    location: formatMemberLocation(roomId),
    winCount: Number(m.k27e ?? m.winCnt ?? m.winCount ?? 0),
    loseCount: Number(m.k28e ?? m.defeatCnt ?? m.loseCount ?? 0),
    drawCount: Number(m.k29e ?? m.drawCnt ?? m.drawCount ?? 0),
    sex:      (m.k11e === 'F' || m.sex === 'F' || m.sex === 'female' ? 'female' : 'male') as 'male' | 'female',
    avatarId: String(m.k7e ?? m.avatarId ?? ''),
    roomId,
  }
}

function uniqueMemberEntries(members: MemberEntry[]): MemberEntry[] {
  const byPix = new Map<string, MemberEntry>()
  for (const member of members) {
    if (!member.pix) continue
    byPix.set(member.pix, { ...byPix.get(member.pix), ...member })
  }
  return Array.from(byPix.values())
}

function membersWithSelfFirst(members: MemberEntry[], myPix: string): MemberEntry[] {
  if (!myPix) return members
  const self = members.find(member => member.pix === myPix)
  return self ? [self, ...members.filter(member => member.pix !== myPix)] : members
}

function readLegacyMemberInfo(raw: unknown): MemberEntry | null {
  if (typeof raw !== 'string' || raw === '') return null
  const parts = raw.split('\t')
  if (parts.length >= 22) {
    return {
      pix: parts[0] ?? '',
      avatarId: parts[1] ?? '',
      name: parts[2] || parts[27] || parts[0] || '',
      sex: (parts[3] === 'F' ? 'female' : 'male') as 'male' | 'female',
      location: parts[5] || 'ロビー',
      rating: Number(parts[12] ?? 0),
      slevel: parts[13] === ' ' ? '' : parts[13] ?? '',
      nlevel: Number(parts[14] ?? 0),
      winCount: Number(parts[8] ?? 0),
      loseCount: Number(parts[9] ?? 0),
      drawCount: Number(parts[10] ?? 0),
      roomId: 0,
    }
  }
  if (parts.length >= 12) {
    return {
      pix: parts[0] ?? '',
      avatarId: '',
      name: parts[17] && parts[17] !== ' ' ? parts[17] : parts[0] ?? '',
      sex: (parts[1] === 'F' ? 'female' : 'male') as 'male' | 'female',
      location: parts[2] || 'ロビー',
      rating: Number(parts[7] ?? 0),
      slevel: '',
      nlevel: Number(parts[8] ?? 0),
      winCount: Number(parts[4] ?? 0),
      loseCount: Number(parts[5] ?? 0),
      drawCount: Number(parts[6] ?? 0),
      roomId: 0,
    }
  }
  return null
}

function readMemberListPayload(data: Record<string, unknown>): MemberEntry[] {
  const list = Array.isArray(data.members) ? data.members as Array<Record<string, unknown>> : []
  if (list.length > 0) return uniqueMemberEntries(list.map(readMemberEntry))

  const count = Number(data.k25e ?? data.count ?? 0)
  const legacyMembers = Array.from({ length: count }, (_, index) => readLegacyMemberInfo(data[`k3e${index}`]))
    .filter((member): member is MemberEntry => member != null && member.pix !== '')
  return uniqueMemberEntries(legacyMembers)
}

function buildGetMemberListPayload(channelId: string) {
  const subId = channelId.length >= 11 ? channelId.substring(6, 11) : channelId
  return {
    gameId: 'MAJAK4',
    k22e: 'MAJAK4',
    subId,
    k23e: subId,
    channelId,
    k24e: channelId,
  }
}

function isLegacyInviteCancel(value: unknown) {
  return value === true || value === 1 || value === '1' || value === 'true'
}

function buildInviteResponsePayload(inviterId: string, roomId: number, accept: boolean) {
  const pix = useAuthStore.getState().player?.pix ?? ''
  return {
    k3e: pix,
    inviterId,
    roomId: String(roomId),
    accept: accept ? '1' : '0',
    k64e: accept ? 'v7e' : 'v8e',
  }
}

function inviteResponseMessage(displayName: string, yesNo: unknown) {
  if (yesNo === 'v6e') return `${displayName}さんから応答がありませんでした。`
  if (yesNo === 'v7e') return `${displayName}さんがゲーム申し込みを承諾しました。`
  return `${displayName}さんから応答がありませんでした。\n『また今 度誘ってね！』`
}

function MobileMemberListPanel({
  members,
  selectedMember,
  onSelectMember,
  onViewProfile,
}: {
  members: MemberEntry[]
  selectedMember: string | null
  onSelectMember: (pix: string) => void
  onViewProfile: (pix: string) => void
}) {
  const lastTapRef = useRef<{ pix: string; at: number }>({ pix: '', at: 0 })

  const handleMemberTap = (pix: string) => {
    onSelectMember(pix)

    const now = Date.now()
    const { pix: lastPix, at } = lastTapRef.current
    if (lastPix === pix && now - at <= 320) {
      onViewProfile(pix)
      lastTapRef.current = { pix: '', at: 0 }
      return
    }

    lastTapRef.current = { pix, at: now }
  }

  return (
    <aside className="majak-mobile-lobby-members">
      {members.map(member => (
        <button
          key={member.pix}
          type="button"
          className={`majak-mobile-lobby-member${selectedMember === member.pix ? ' is-selected' : ''}`}
          onClick={() => handleMemberTap(member.pix)}
          onDoubleClick={() => onViewProfile(member.pix)}
        >
          <img
            src={member.avatarId ? getShortAvatarUrl(member.avatarId) : getDefaultAvatarUrl(member.sex)}
            alt=""
            draggable={false}
            onError={e => { (e.currentTarget as HTMLImageElement).src = getDefaultAvatarUrl(member.sex) }}
          />
          <span>
            <span className="majak-mobile-lobby-member__identity">
              <span className="majak-mobile-lobby-member__name">{member.name}</span>
              <span className="majak-mobile-lobby-member__title">{member.slevel || '庶民'}</span>
            </span>
            <span className="majak-mobile-lobby-member__location">{member.location}</span>
          </span>
        </button>
      ))}
    </aside>
  )
}

function MemberListPanel({
  members,
  selectedMember,
  isDani,
  onSelectMember,
  onViewProfile,
}: {
  members: MemberEntry[]
  selectedMember: string | null
  isDani: boolean
  onSelectMember: (pix: string) => void
  onViewProfile: (pix: string) => void
}) {
  /** CMJMemberListFilter 相当: 条件設定ダイアログ未移植のため、現状は条件なしを保持 */
  const [filterMode, setFilterMode] = useState<'all' | 'filter'>('all')
  const filteredMembers = members

  /** フィルターボタンのフレーム状態 */
  const [filterFi, setFilterFi] = useState(0)
  const [allFi,    setAllFi]    = useState(0)
  const nicknameColumnWidth = isDani ? 104 : 150
  const levelColumnWidth = isDani ? 80 : 80
  const locationColumnWidth = isDani ? 66 : 80

  return (
    <div
      className="majak-desktop-lobby-members"
      style={{
        position: 'absolute',
        left: 678,
        top: 181,
        width: 336,
        height: 403,
        overflow: 'hidden',
      }}
    >
      <div className="majak-desktop-lobby-member-heading">メンバー一覧</div>
      {/* 背景 mj_userlist_bg.png (336×403) */}
      <img
        src={`${IMG}/mj_userlist_bg.png`}
        alt=""
        draggable={false}
        style={{ position: 'absolute', left: 0, top: 0, width: 336, height: 403 }}
      />

      {/* 全体/現在 メンバー数表示 — レガシー: rcFilterText=(196,8...), rcNumberText=(196,23...), DT_RIGHT */}
      <div style={{
        position: 'absolute', left: 196, top: 8,
        width: 130,
        fontFamily: 'var(--majak-font-family-ui)', fontSize: 'calc(10px * var(--majak-type-scale))', color: '#000',
        textAlign: 'right', lineHeight: '12px',
      }}>
        <div>全体</div>
        <div style={{ marginTop: 3 }}>現在:{members.length}名</div>
      </div>

      {/* フィルターボタン */}
      <button
        onClick={() => setFilterMode('filter')}
        onMouseEnter={() => setFilterFi(2)}
        onMouseLeave={() => setFilterFi(filterMode === 'filter' ? 3 : 0)}
        onMouseDown={() => setFilterFi(3)}
        onMouseUp={() => setFilterFi(2)}
        title="フィルター表示"
        style={{
          position: 'absolute', left: 0, top: 0,
          transform: 'translate(3px, 3px)',
          width: 21, height: 36,
          backgroundImage: `url(${IMG}/mj_btn_userlist_filter.png)`,
          backgroundPosition: `${-(filterMode === 'filter' ? 3 : filterFi) * 21}px 0`,
          backgroundRepeat: 'no-repeat',
          border: 'none', padding: 0, cursor: 'pointer',
          outline: 'none', imageRendering: 'pixelated',
        }}
      />

      {/* ================================================================
          全表示ボタン: mj_btn_userlist_all.png (84×36, 4フレーム 21×36)
          m_btnListAll.Create(0, ..., 0, 0, ..., IDC_MEMBERLIST_ALL)
          選択中 = frame3 (pressed 状態)
          ================================================================ */}
      <button
        onClick={() => setFilterMode('all')}
        onMouseEnter={() => setAllFi(2)}
        onMouseLeave={() => setAllFi(filterMode === 'all' ? 3 : 0)}
        onMouseDown={() => setAllFi(3)}
        onMouseUp={() => setAllFi(2)}
        title="全て表示"
        style={{
          position: 'absolute', left: 21, top: 0,
          transform: 'translate(6px, 3px)',
          width: 21, height: 36,
          backgroundImage: `url(${IMG}/mj_btn_userlist_all.png)`,
          backgroundPosition: `${-(filterMode === 'all' ? 3 : allFi) * 21}px 0`,
          backgroundRepeat: 'no-repeat',
          border: 'none', padding: 0, cursor: 'pointer',
          outline: 'none', imageRendering: 'pixelated',
        }}
      />

      {/* メンバーリスト本体 — レガシー: rcListWnd=CRect(1,43,335,402), CHgListCtrl */}
      <div style={{
        position: 'absolute', left: 1, top: 43,
        width: 334, height: 18,
        display: 'flex', alignItems: 'center',
        background: '#f0f0f0',
        fontFamily: 'var(--majak-font-family-ui)', fontSize: 'calc(11px * var(--majak-type-scale))', color: '#000',
        border: '1px solid #b8b8b8',
        boxSizing: 'border-box',
      }}>
        <span style={{ width: nicknameColumnWidth, paddingLeft: 4 }}>ニックネーム</span>
        <span style={{ width: levelColumnWidth }}>{isDani ? '段位' : '資産'}</span>
        <span style={{ width: locationColumnWidth }}>位置</span>
      </div>
      <div
        style={{
          position: 'absolute',
          left: 1,
          top: 61,
          width: 334,
          height: 341,
          overflowY: 'auto',
          overflowX: 'hidden',
          background: '#fff',
          border: '1px solid #b8b8b8',
          borderTop: 'none',
          boxSizing: 'border-box',
        }}
      >
        {filteredMembers.map((member, index) => (
          <div
            key={`${member.pix}-${index}`}
            onClick={() => onSelectMember(member.pix)}
            onDoubleClick={() => onViewProfile(member.pix)}
            style={{
              display: 'flex',
              alignItems: 'center',
              height: 36,
              cursor: 'pointer',
              fontFamily: 'var(--majak-font-family-ui)',
              fontSize: 'calc(11px * var(--majak-type-scale))',
              color: selectedMember === member.pix ? '#ffffff' : '#000',
              borderBottom: '1px solid #ccc',
              background: selectedMember === member.pix ? '#356246' : '#fff',
            }}
          >
            {/* アバターサムネイル — AP-08: getShortAvatarUrl (接続者リスト用) */}
            <img
              src={getShortAvatarUrl(member.avatarId)}
              alt=""
              draggable={false}
              onError={e => { (e.currentTarget as HTMLImageElement).src = getDefaultAvatarUrl(member.sex === 'female' ? 'female' : 'male') }}
              style={{
                width: 22,
                height: 22,
                objectFit: 'cover',
                objectPosition: 'center 2%',
                imageRendering: 'pixelated',
                flexShrink: 0,
                marginLeft: 4,
              }}
            />
            <span style={{ width: nicknameColumnWidth - 28, paddingLeft: 2, overflow: 'hidden', whiteSpace: 'nowrap', textOverflow: 'ellipsis' }}>
              {member.name}
            </span>
            <span style={{ width: levelColumnWidth, overflow: 'hidden', whiteSpace: 'nowrap' }}>{isDani ? member.slevel : (member.slevel || '庶民')}</span>
            <span style={{ width: locationColumnWidth }}>{member.location}</span>
          </div>
        ))}
      </div>
    </div>
  )
}

interface TournamentEntry {
  seqNo: number
  playName: string
  playStatus: number
  playerNum: number
  maxPlayerNum: number
  hasPassword: number
  playMode: number
  playNum: number
  playTime: number
  joinMoney: number
  gradeMoney1: number
  gradeMoney2: number
  gradeMoney3: number
  gradeMoney4: number
  playStartDt: string
  playEndDt: string
  playSchedule: string
  roomOption: string
  maxViewer: number
  planPix: string
  resultMember1: string
  resultMember2: string
  resultMember3: string
  resultMember4: string
}

interface TournamentDetailEntry {
  subId: string
  roomId: number
  pixes: string[]
  slotNos: string[]
  gradeIds: string[]
  startPlanDt: string
  startDt: string
  endDt: string
}

function readTournamentEntry(raw: Record<string, unknown>): TournamentEntry {
  return {
    seqNo: Number(raw.seqNo ?? raw.tournamentNo ?? 0),
    playName: String(raw.playName ?? ''),
    playStatus: Number(raw.playStatus ?? 0),
    playerNum: Number(raw.playerNum ?? 0),
    maxPlayerNum: Number(raw.maxPlayerNum ?? 0),
    hasPassword: Number(raw.hasPassword ?? 0),
    playMode: Number(raw.playMode ?? 0),
    playNum: Number(raw.playNum ?? 0),
    playTime: Number(raw.playTime ?? 0),
    joinMoney: Number(raw.joinMoney ?? 0),
    gradeMoney1: Number(raw.gradeMoney1 ?? 0),
    gradeMoney2: Number(raw.gradeMoney2 ?? 0),
    gradeMoney3: Number(raw.gradeMoney3 ?? 0),
    gradeMoney4: Number(raw.gradeMoney4 ?? 0),
    playStartDt: String(raw.playStartDt ?? ''),
    playEndDt: String(raw.playEndDt ?? ''),
    playSchedule: String(raw.playSchedule ?? ''),
    roomOption: String(raw.roomOption ?? ''),
    maxViewer: Number(raw.maxViewer ?? 0),
    planPix: String(raw.planPix ?? raw['plan' + 'Member' + 'Id'] ?? ''),
    resultMember1: String(raw.resultMember1 ?? ''),
    resultMember2: String(raw.resultMember2 ?? ''),
    resultMember3: String(raw.resultMember3 ?? ''),
    resultMember4: String(raw.resultMember4 ?? ''),
  }
}

function readTournamentListPayload(data: Record<string, unknown>): TournamentEntry[] {
  const list = Array.isArray(data.tournamentList) ? data.tournamentList as Array<Record<string, unknown>> : []
  return list.map(readTournamentEntry).filter(item => item.seqNo > 0)
}

function readTournamentDetailEntry(raw: Record<string, unknown>): TournamentDetailEntry {
  return {
    subId: String(raw.subId ?? ''),
    roomId: Number(raw.roomId ?? 0),
    pixes: [1, 2, 3, 4].map(index => String(raw[`pix${index}`] ?? raw[`member${'Id'}${index}`] ?? '')),
    slotNos: [1, 2, 3, 4].map(index => String(raw[`slotNo${index}`] ?? raw[`member${'No'}${index}`] ?? '')),
    gradeIds: [raw.gradeId1, raw.gradeId2, raw.gradeId3, raw.gradeId4].map(value => String(value ?? '')),
    startPlanDt: String(raw.startPlanDt ?? ''),
    startDt: String(raw.startDt ?? ''),
    endDt: String(raw.endDt ?? ''),
  }
}

function readTournamentDetailPayload(data: Record<string, unknown>) {
  const plan = data.tournamentList && typeof data.tournamentList === 'object' && !Array.isArray(data.tournamentList)
    ? readTournamentEntry(data.tournamentList as Record<string, unknown>)
    : null
  const details = Array.isArray(data.tournamentDetail)
    ? (data.tournamentDetail as Array<Record<string, unknown>>).map(readTournamentDetailEntry)
    : []
  return { plan, details }
}

function formatTournamentStatus(item: TournamentEntry) {
  if (item.playStatus === 1) return '終了'
  if (item.playStatus === 2 || item.playStatus === 3) return `${item.playerNum}/${item.maxPlayerNum}`
  if (item.playStatus === 4) return '対局中'
  if (item.playStatus === 5) return '人数不足'
  if (item.playStatus === 9) return '運営中'
  return item.playStatus > 0 ? String(item.playStatus) : ''
}

function TournamentListPanel({
  tournaments,
  selectedSeqNo,
  joinSeqNo,
  onSelect,
}: {
  tournaments: TournamentEntry[]
  selectedSeqNo: number | null
  joinSeqNo: number
  onSelect: (entry: TournamentEntry) => void
}) {
  const rows: (TournamentEntry | null)[] = Array.from({ length: Math.max(10, tournaments.length) }, (_, index) => tournaments[index] ?? null)

  return (
    <>
      {/* CHgTaikaiListHead::Create(CRect(15,137,668,154)) → content y=106 */}
      <div style={{ position: 'absolute', left: 15, top: 106, width: 653, height: 18, fontFamily: 'var(--majak-font-family-ui)', fontSize: 'calc(13px * var(--majak-type-scale))', color: '#000' }}>
        {[
          [0, 250, '大会名'],
          [250, 60, 'パス'],
          [310, 100, '参加人数/規模'],
          [410, 80, '勝抜人数'],
          [490, 147, '日時'],
        ].map(([left, width, label]) => (
          <div key={String(left)} style={{ position: 'absolute', left: Number(left), top: 0, width: Number(width), height: 18, lineHeight: '18px', textAlign: 'center', background: '#d4d0c8', border: '1px solid #808080', boxSizing: 'border-box' }}>
            {label}
          </div>
        ))}
      </div>

      {/* CHgTournamentList2::Create(CRect(15,155,668,635)) → content y=124 */}
      <div style={{ position: 'absolute', left: 15, top: 124, width: 653, height: 480, overflowY: 'auto', overflowX: 'hidden' }}>
        {rows.map((entry, index) => (
          <TournamentRow
            key={entry?.seqNo ?? `empty-${index}`}
            entry={entry}
            index={index}
            selected={entry != null && entry.seqNo === selectedSeqNo}
            joined={entry != null && entry.seqNo === joinSeqNo}
            onSelect={onSelect}
          />
        ))}
      </div>
    </>
  )
}

function TournamentRow({
  entry,
  index,
  selected,
  joined,
  onSelect,
}: {
  entry: TournamentEntry | null
  index: number
  selected: boolean
  joined: boolean
  onSelect: (entry: TournamentEntry) => void
}) {
  const [frameIdx, setFrameIdx] = useState(0)
  const frameW = 635
  const frame = selected ? 3 : frameIdx
  const textColor = joined ? '#0000ff' : '#000000'
  const fontWeight = joined ? 'bold' : 'normal'

  return (
    <button
      type="button"
      disabled={!entry}
      onClick={() => { if (entry) onSelect(entry) }}
      onMouseEnter={() => { if (entry && !selected) setFrameIdx(2) }}
      onMouseLeave={() => { if (entry && !selected) setFrameIdx(0) }}
      onMouseDown={() => { if (entry) setFrameIdx(3) }}
      onMouseUp={() => { if (entry && !selected) setFrameIdx(2) }}
      style={{
        position: 'absolute',
        left: 0,
        top: index * 48,
        width: frameW,
        height: 48,
        backgroundImage: `url(${IMG}/mj_list_tournament.png)`,
        backgroundPosition: `${-frame * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        backgroundColor: 'transparent',
        border: 'none',
        padding: 0,
        margin: 0,
        textAlign: 'left',
        cursor: entry ? 'pointer' : 'default',
        imageRendering: 'pixelated',
      }}
    >
      {entry && (
        <>
          <span style={{ position: 'absolute', left: 10, top: 15, width: 240, height: 20, overflow: 'hidden', whiteSpace: 'nowrap', textOverflow: 'ellipsis', fontFamily: 'var(--majak-font-family-ui)', fontSize: 'calc(14px * var(--majak-type-scale))', color: textColor, fontWeight }}>{entry.playName}</span>
          <span style={{ position: 'absolute', left: 250, top: 15, width: 60, textAlign: 'center', fontFamily: 'var(--majak-font-family-ui)', fontSize: 'calc(14px * var(--majak-type-scale))', color: textColor, fontWeight }}>{entry.hasPassword === 1 ? '有' : '無'}</span>
          <span style={{ position: 'absolute', left: 310, top: 15, width: 100, textAlign: 'center', fontFamily: 'var(--majak-font-family-ui)', fontSize: 'calc(14px * var(--majak-type-scale))', color: textColor, fontWeight }}>{formatTournamentStatus(entry)}</span>
          <span style={{ position: 'absolute', left: 410, top: 15, width: 80, textAlign: 'center', fontFamily: 'var(--majak-font-family-ui)', fontSize: 'calc(14px * var(--majak-type-scale))', color: textColor, fontWeight }}>{entry.playNum}</span>
          <span style={{ position: 'absolute', left: 490, top: 15, width: 145, textAlign: 'center', fontFamily: 'var(--majak-font-family-ui)', fontSize: 'calc(14px * var(--majak-type-scale))', color: textColor, fontWeight }}>{entry.playStartDt}</span>
        </>
      )}
    </button>
  )
}

function TournamentDetailPanel({ tournament }: { tournament: TournamentEntry | null }) {
  const lines = getTournamentDetailLines(tournament)

  return (
    <div style={{ position: 'absolute', left: 678, top: 221, width: 336, height: 368, border: '2px inset #d4d0c8', boxSizing: 'border-box', background: '#fff', overflowY: 'auto', fontFamily: 'var(--majak-font-family-ui)', fontSize: 'calc(12px * var(--majak-type-scale))', lineHeight: '18px', color: '#000', padding: 4 }}>
      {lines.map((line, index) => <div key={`${index}-${line}`} style={{ whiteSpace: 'nowrap' }}>{line}</div>)}
    </div>
  )
}

function getTournamentDetailLines(tournament: TournamentEntry | null, memberNameByPix = new Map<string, string>()) {
  return tournament ? [
    '【概要】',
    `大会名：${tournament.playName}`,
    `開催者：${memberNameByPix.get(tournament.planPix) || tournament.planPix}`,
    `パスワード：${tournament.hasPassword === 0 ? 'なし' : 'あり'}`,
    `日付：${tournament.playStartDt.slice(5, 10)}`,
    `開始時間：${tournament.playStartDt.slice(11, 16)}`,
    `終了予定時間：${tournament.playEndDt.slice(11, 16)}`,
    `参加費 ：${tournament.joinMoney} MP`,
    `賞金：1位 ${tournament.gradeMoney1} MP`,
    `賞金：2位 ${tournament.gradeMoney2} MP`,
    `賞金：3位 ${tournament.gradeMoney3} MP`,
    `賞金：4位 ${tournament.gradeMoney4} MP`,
    ' ',
    '【トーナメント内容】',
    `人数：${tournament.maxPlayerNum} 人トーナメント`,
    `勝抜け人数：${tournament.playNum}人`,
    `1試合あたりの試合数：${tournament.playMode} 試合`,
    `1試合あたりの時間：${tournament.playTime}`,
  ] : []
}

function MobileTournamentMatchPanel({
  tournament,
  details,
  memberNameByPix,
  onWatch,
}: {
  tournament: TournamentEntry | null
  details: TournamentDetailEntry[]
  memberNameByPix: Map<string, string>
  onWatch: (detail: TournamentDetailEntry, detailIndex: number) => void
}) {
  const imageName = getTournamentMatchImage(tournament)
  const size = getTournamentMatchSize(tournament)
  const viewportRef = useRef<HTMLDivElement>(null)
  const [scale, setScale] = useState(1)

  useEffect(() => {
    const viewport = viewportRef.current
    if (!viewport) return

    const updateScale = () => setScale(Math.min(1, viewport.clientWidth / size.width))
    updateScale()
    const observer = new ResizeObserver(updateScale)
    observer.observe(viewport)
    return () => observer.disconnect()
  }, [size.width])

  return (
    <div ref={viewportRef} className="majak-mobile-tournament-bracket">
      <div style={{ position: 'relative', width: size.width * scale, height: size.height * scale, margin: '0 auto' }}>
        <div style={{ position: 'absolute', left: 0, top: 0, width: size.width, height: size.height, transform: `scale(${scale})`, transformOrigin: 'top left' }}>
          <img
            src={`${IMG}/${imageName}`}
            alt=""
            draggable={false}
            style={{ position: 'absolute', inset: 0, width: size.width, height: size.height, imageRendering: 'pixelated' }}
          />
          <TournamentMatchLabels tournament={tournament} details={details} memberNameByPix={memberNameByPix} />
          <TournamentWatchingButtons tournament={tournament} details={details} onWatch={onWatch} />
        </div>
      </div>
    </div>
  )
}

function getTournamentMatchImage(tournament: TournamentEntry | null) {
  if (!tournament) return 'mj_tournament_match_type_a.png'
  if (tournament.maxPlayerNum === 4) return 'mj_tournament_match_type_a.png'
  if (tournament.maxPlayerNum === 8) return 'mj_tournament_match_type_b.png'
  if (tournament.maxPlayerNum === 16) return tournament.playNum === 1 ? 'mj_tournament_match_type_c.png' : 'mj_tournament_match_type_d.png'
  if (tournament.maxPlayerNum === 32) return 'mj_tournament_match_type_e.png'
  return 'mj_tournament_match_type_f.png'
}

function getTournamentMatchSize(tournament: TournamentEntry | null) {
  if (!tournament || tournament.maxPlayerNum <= 16) return { width: 633, height: 396 }
  if (tournament.maxPlayerNum === 32) return { width: 633, height: 560 }
  return { width: 633, height: 1040 }
}

function TournamentMatchPanel({
  tournament,
  details,
  memberNameByPix,
  onWatch,
}: {
  tournament: TournamentEntry | null
  details: TournamentDetailEntry[]
  memberNameByPix: Map<string, string>
  onWatch: (detail: TournamentDetailEntry, detailIndex: number) => void
}) {
  const imageName = getTournamentMatchImage(tournament)
  const size = getTournamentMatchSize(tournament)

  return (
    <div style={{ position: 'absolute', left: 16, top: 57, width: 651, height: 397, overflowY: 'auto', overflowX: 'hidden' }}>
      <div style={{ position: 'relative', width: size.width, height: size.height }}>
        <img
          src={`${IMG}/${imageName}`}
          alt=""
          draggable={false}
          style={{ position: 'absolute', left: 0, top: 0, width: size.width, height: size.height, imageRendering: 'pixelated' }}
        />
        <TournamentMatchLabels tournament={tournament} details={details} memberNameByPix={memberNameByPix} />
        <TournamentWatchingButtons tournament={tournament} details={details} onWatch={onWatch} />
      </div>
    </div>
  )
}

function getTournamentWatchingButtonPositions(tournament: TournamentEntry | null): Array<[number, number]> {
  if (!tournament || tournament.maxPlayerNum === 4) return [[296, 330]]
  if (tournament.maxPlayerNum === 8) return [[157, 271], [433, 271], [296, 323]]
  if (tournament.maxPlayerNum === 16 && tournament.playNum === 1) return [[109, 151], [109, 351], [481, 151], [481, 351], [296, 325]]
  if (tournament.maxPlayerNum === 16) return [[109, 171], [109, 371], [481, 171], [481, 371], [205, 271], [385, 271], [296, 325]]
  if (tournament.maxPlayerNum === 32) return [[109, 171], [109, 291], [109, 411], [109, 531], [481, 171], [481, 291], [481, 411], [481, 531], [157, 231], [157, 471], [433, 231], [433, 471], [205, 351], [385, 351], [296, 404]]
  return [[109, 151], [109, 271], [109, 391], [109, 511], [109, 631], [109, 751], [109, 871], [109, 991], [481, 151], [481, 271], [481, 391], [481, 511], [481, 631], [481, 751], [481, 871], [481, 991], [205, 331], [205, 811], [385, 331], [385, 811], [296, 645]]
}

function TournamentWatchingButtons({
  tournament,
  details,
  onWatch,
}: {
  tournament: TournamentEntry | null
  details: TournamentDetailEntry[]
  onWatch: (detail: TournamentDetailEntry, detailIndex: number) => void
}) {
  const positions = getTournamentWatchingButtonPositions(tournament)
  if (!tournament || tournament.maxViewer === 0) return null
  return <>{positions.map(([left, top], index) => {
    const detail = details[index]
    if (!detail || detail.roomId <= 0 || detail.endDt) return null
    return (
      <SpriteButton
        key={`${detail.roomId}-${index}`}
        src={`${IMG}/mj_btn_watching.png`}
        frameW={42}
        frameH={17}
        x={left}
        y={top}
        onClick={() => onWatch(detail, index)}
        title="観戦"
      />
    )
  })}</>
}

function TournamentMatchLabels({
  tournament,
  details,
  memberNameByPix = new Map<string, string>(),
}: {
  tournament: TournamentEntry | null
  details: TournamentDetailEntry[]
  memberNameByPix?: Map<string, string>
}) {
  if (!tournament) return null
  const labelStyle: React.CSSProperties = {
    position: 'absolute',
    height: 15,
    overflow: 'hidden',
    whiteSpace: 'nowrap',
    textOverflow: 'ellipsis',
    fontFamily: 'var(--majak-font-family-ui)',
    fontSize: 'calc(12px * var(--majak-type-scale))',
    lineHeight: '15px',
    color: '#ffffff',
    pointerEvents: 'none',
  }

  const slotNoByPix = new Map<string, string>()
  for (const detail of details) {
    detail.pixes.forEach((pix, index) => {
      if (pix) slotNoByPix.set(pix, detail.slotNos[index] || `${index + 1}`.padStart(2, '0'))
    })
  }
  const memberLabel = (detail: TournamentDetailEntry | undefined, memberIndex: number) => {
    if (!detail) return ''
    const slotNo = detail.slotNos[memberIndex] || `${memberIndex + 1}`.padStart(2, '0')
    const pix = detail.pixes[memberIndex]
    return pix ? `${slotNo}:${memberNameByPix.get(pix) || pix}` : `${slotNo}:トントン(NPC)`
  }
  const gradeLabel = (detail: TournamentDetailEntry | undefined, gradeIndex: number) => {
    const gradeId = detail?.gradeIds[gradeIndex] ?? ''
    return gradeId ? slotNoByPix.get(gradeId) ?? gradeId : ''
  }
  const renderLabels = (labels: string[], positions: number[][], keyPrefix: string) => (
    <>
      {positions.map(([left = 0, top = 0, width = 0], index) => {
        const label = labels[index] ?? ''
        if (!label) return null
        return <span key={`${keyPrefix}-${index}`} style={{ ...labelStyle, left, top, width }}>{label}</span>
      })}
    </>
  )

  if (tournament.maxPlayerNum === 4) {
    const positions = [[49, 122, 99], [49, 342, 99], [484, 122, 99], [484, 342, 99]]
    return renderLabels(positions.map((_, index) => {
      const pix = details[0]?.pixes[index] ?? ''
      return memberNameByPix.get(pix) || pix
    }), positions, 'p4')
  }

  if (tournament.maxPlayerNum === 8) {
    const positions = [[4, 102, 99], [4, 162, 99], [4, 302, 99], [4, 362, 99], [529, 102, 99], [529, 162, 99], [529, 302, 99], [529, 362, 99]]
    const resultPositions = [[160, 212, 35], [160, 252, 35], [436, 212, 35], [436, 252, 35]]
    return (
      <>
        {renderLabels(details.slice(0, 2).flatMap(detail => detail.pixes.map((_, memberIndex) => memberLabel(detail, memberIndex))), positions, 'p8-member')}
        {renderLabels(details.slice(0, 2).flatMap(detail => [gradeLabel(detail, 0), gradeLabel(detail, 1)]), resultPositions, 'p8-result')}
      </>
    )
  }

  const firstRoundDetailCount = tournament.maxPlayerNum / 4
  const leftDetailCount = firstRoundDetailCount / 2
  const firstRoundPositions = Array.from({ length: firstRoundDetailCount }, (_, detailIndex) => {
    const sideRight = detailIndex >= leftDetailCount
    const sideDetailIndex = detailIndex % leftDetailCount
    const baseY = 103 + sideDetailIndex * 120
    return Array.from({ length: 4 }, (_, memberIndex) => [sideRight ? 529 : 4, baseY + memberIndex * 20, 99] as [number, number, number])
  }).flat()
  const firstRoundLabels = details.slice(0, firstRoundDetailCount).flatMap(detail => detail.pixes.map((_, memberIndex) => memberLabel(detail, memberIndex)))

  const firstRoundResultPositions = tournament.maxPlayerNum === 16 && tournament.playNum === 1
    ? [[112, 132, 35], [112, 332, 35], [484, 132, 35], [484, 332, 35]]
    : tournament.maxPlayerNum === 16
      ? [[112, 112, 35], [112, 152, 35], [112, 312, 35], [112, 352, 35], [484, 112, 35], [484, 152, 35], [484, 312, 35], [484, 352, 35]]
      : tournament.maxPlayerNum === 32
        ? [[112, 112, 35], [112, 152, 35], [112, 232, 35], [112, 272, 35], [112, 352, 35], [112, 392, 35], [112, 472, 35], [112, 512, 35], [484, 112, 35], [484, 152, 35], [484, 232, 35], [484, 272, 35], [484, 352, 35], [484, 392, 35], [484, 472, 35], [484, 512, 35]]
        : [[112, 132, 35], [112, 252, 35], [112, 372, 35], [112, 492, 35], [112, 612, 35], [112, 732, 35], [112, 852, 35], [112, 972, 35], [484, 132, 35], [484, 252, 35], [484, 372, 35], [484, 492, 35], [484, 612, 35], [484, 732, 35], [484, 852, 35], [484, 972, 35]]
  const winCount = tournament.playNum === 1 ? 1 : 2
  const firstRoundResultLabels = details.slice(0, firstRoundDetailCount).flatMap(detail => Array.from({ length: winCount }, (_, index) => gradeLabel(detail, index)))

  const secondRoundPositions = tournament.maxPlayerNum === 16 && tournament.playNum === 1
    ? []
    : tournament.maxPlayerNum === 16
      ? [[208, 212, 35], [208, 252, 35], [388, 212, 35], [388, 252, 35]]
      : tournament.maxPlayerNum === 32
        ? [[160, 172, 35], [159, 212, 36], [160, 412, 35], [160, 452, 35], [436, 172, 35], [436, 212, 35], [436, 412, 35], [436, 452, 35]]
        : [[208, 312, 35], [208, 792, 35], [388, 312, 35], [388, 792, 35]]
  const secondRoundStart = firstRoundDetailCount
  const secondRoundCount = tournament.maxPlayerNum === 16 && tournament.playNum === 1 ? 0 : secondRoundPositions.length / winCount
  const secondRoundLabels = details.slice(secondRoundStart, secondRoundStart + secondRoundCount).flatMap(detail => Array.from({ length: winCount }, (_, index) => gradeLabel(detail, index)))

  const thirdRoundPositions = tournament.maxPlayerNum === 32
    ? [[208, 292, 35], [208, 332, 35], [388, 292, 35], [388, 332, 35]]
    : []
  const thirdRoundStart = secondRoundStart + secondRoundCount
  const thirdRoundCount = thirdRoundPositions.length / winCount
  const thirdRoundLabels = details.slice(thirdRoundStart, thirdRoundStart + thirdRoundCount).flatMap(detail => Array.from({ length: winCount }, (_, index) => gradeLabel(detail, index)))

  const finalPositions = tournament.maxPlayerNum === 32
    ? [[266, 267, 99], [266, 301, 99], [266, 335, 99], [266, 368, 99]]
    : tournament.maxPlayerNum === 64
      ? [[266, 507, 99], [266, 541, 99], [266, 575, 99], [266, 608, 99]]
      : [[266, 187, 99], [266, 221, 99], [266, 255, 99], [266, 288, 99]]
  const finalLabels = [tournament.resultMember1, tournament.resultMember2, tournament.resultMember3, tournament.resultMember4]
    .map(pix => memberNameByPix.get(pix) || pix)

  return (
    <>
      {renderLabels(firstRoundLabels, firstRoundPositions, 'large-member')}
      {renderLabels(firstRoundResultLabels, firstRoundResultPositions, 'large-result1')}
      {renderLabels(secondRoundLabels, secondRoundPositions, 'large-result2')}
      {renderLabels(thirdRoundLabels, thirdRoundPositions, 'large-result3')}
      {tournament.playStatus === 1 && renderLabels(finalLabels, finalPositions, 'large-final')}
    </>
  )
}

/** ====================================================================
 * CMJChkBtn 相当 — check.png (56×14, 4フレーム×14px) + テキスト
 * DrawItem() 再現: チェック欄(14×14) + 左18pxオフセットでテキスト
 * nState: bit0=pressed/disabled, bit1=checked
 * ==================================================================== */
function ChkBtn({
  x, y, w, h, label, onToggle, disabled = false,
}: {
  x: number; y: number; w: number; h: number
  label: string
  onToggle: (checked: boolean) => void
  disabled?: boolean
}) {
  const [checked, setChecked] = useState(false)
  const [pressed, setPressed] = useState(false)

  const nState = ((pressed || disabled) ? 1 : 0) | (checked ? 2 : 0)

  const toggle = () => {
    if (disabled) return
    const next = !checked
    setChecked(next)
    onToggle(next)
  }

  return (
    <div
      onClick={toggle}
      onMouseDown={() => { if (!disabled) setPressed(true) }}
      onMouseUp={()   => setPressed(false)}
      onMouseLeave={() => setPressed(false)}
      style={{
        position: 'absolute',
        left: x,
        top: y,
        width: w,
        height: h,
        display: 'flex',
        alignItems: 'center',
        cursor: disabled ? 'default' : 'pointer',
        userSelect: 'none',
      }}
    >
      {/* check.png 14×14 フレーム at (left+1, top+1) */}
      <div
        style={{
          width: 14,
          height: 14,
          backgroundImage: `url(${IMG}/check.png)`,
          backgroundPosition: `${-nState * 14}px 0`,
          backgroundRepeat: 'no-repeat',
          imageRendering: 'pixelated',
          flexShrink: 0,
        }}
      />
      {/* テキスト: rc.left+=18 オフセット */}
      <span
        style={{
          marginLeft: 4,
          fontFamily: 'var(--majak-font-family-ui)',
          fontSize: 'calc(11px * var(--majak-type-scale))',
          color: pressed || disabled ? '#888' : '#000',
          whiteSpace: 'nowrap',
          overflow: 'hidden',
        }}
      >
        {label}
      </span>
    </div>
  )
}

/** ====================================================================
 * CMajakChannelWnd 本体
 * ==================================================================== */

/** チャットメッセージ型 */
interface ChatMsg {
  id: number
  name: string
  pix?: string
  text: string
  color?: string
}

let nextSystemChatId = 1

function systemChatMessages(lines: string[], color = '#000'): ChatMsg[] {
  return lines.map(text => ({
    id: nextSystemChatId++,
    name: 'System',
    text,
    color,
  }))
}

function formatChatTemplate(template: string, pix: string): string {
  return template.replace('%s', pix)
}

function readChatCommandParts(text: string): { command: string; args: string[]; rest: string } {
  const trimmed = text.trim()
  if (!trimmed.startsWith('/') && !trimmed.startsWith('／')) return { command: '', args: [], rest: '' }
  const body = trimmed.slice(1).trimStart()
  const command = (body.split(/\s+/, 1)[0] ?? '').replace('？', '?').toLowerCase()
  const rest = body.slice(command.length).trimStart()
  return { command, args: rest.length > 0 ? rest.split(/\s+/) : [], rest }
}

function normalizeChatTarget(target: unknown): string {
  const value = String(target ?? '').trim()
  return value.length === 0 ? CHAT_TARGET_ALL : value
}

export default function LobbyScreen() {
  const { channelId } = useParams<{ channelId: string }>()
  const navigate = useNavigate()
  const location = useLocation()
  const navState = location.state as LobbyLocationState | null
  const layoutMode = useOutgameLayoutMode()

  const [rooms,   _setRooms]   = useState<RoomEntry[]>([])
  const roomsRef = useRef<RoomEntry[]>([])
  const [roomSlotCount, setRoomSlotCount] = useState(DEFAULT_ROOM_SLOT_COUNT)
  const [members, _setMembers] = useState<MemberEntry[]>([])
  const [isLobbyDataReady, setIsLobbyDataReady] = useState(false)
  const membersRef = useRef<MemberEntry[]>([])
  const memberNameByPix = new Map(members.map(member => [member.pix, member.name || member.pix]))
  const displayNameForPix = (pix: string) => memberNameByPix.get(pix) || pix
  const [chatLog, setChatLog] = useState<ChatMsg[]>([])
  const [chatText, setChatText] = useState('')
  const [notice, setNotice] = useState<NoticeDisplay | null>(null)
  const [selectedMember, setSelectedMember] = useState<string | null>(null)
  const chatLogRef = useRef<HTMLDivElement>(null)
  const mutedPixesRef = useRef<Set<string>>(new Set())
  const lastAccuseOpenedAtRef = useRef(0)
  const continueRestoreHandledRef = useRef(false)
  const continueRestoreLookupPendingRef = useRef(false)
  const keepSignalRForRoomRef = useRef(false)
  const signalRConnectionOwnerRef = useRef<symbol | null>(null)
  const connectedServerUrlRef = useRef('')

  /** ダイアログ表示状態 */
  const [showWelcome,  setShowWelcome]  = useState(false)
  const [showOpt,      setShowOpt]      = useState(false)
  const [showCfg,      setShowCfg]      = useState(false)
  const [showCustom,   setShowCustom]   = useState(false)
  const [showShop,     setShowShop]     = useState(false)
  const [showConfirm,  setShowConfirm]  = useState(false)
  const [showMission,  setShowMission]  = useState(false)
  const [showAccuse,   setShowAccuse]   = useState(false)
  const [showTournamentRegist, setShowTournamentRegist] = useState(false)
  const [showTournamentJoin, setShowTournamentJoin] = useState(false)
  const [tournamentJoinPassword, setTournamentJoinPassword] = useState('')
  const [tournamentActionPending, setTournamentActionPending] = useState(false)
  const [rankingData,  setRankingData]  = useState<RankingData | null>(null)
  const [showRoomCreate, setShowRoomCreate] = useState(false)
  const [pendingRoomCreate, setPendingRoomCreate] = useState<RoomCreateInfo | null>(null)
  const [pendingRoomCreateSlot, setPendingRoomCreateSlot] = useState<number | null>(null)
  const [showPlayerInfo, setShowPlayerInfo] = useState<DlgPlayerInfo | null>(null)
  const [oneToOneChat, setOneToOneChat] = useState<{ target: string; partnerName: string; partnerOnline: boolean; messages: Array<{ sender: string; text: string; system?: boolean }> } | null>(null)
  const [oneToOneChatText, setOneToOneChatText] = useState('')
  const [oneToOneChatViewport, setOneToOneChatViewport] = useState(() => ({
    top: 0,
    left: 0,
    width: typeof window === 'undefined' ? 0 : window.innerWidth,
    height: typeof window === 'undefined' ? 0 : window.innerHeight,
  }))
  const [inviteData,   setInviteData]   = useState<{
    inviterId: string; inviterName: string; roomId: number; roomPwd: string; avatarId?: string
    roomName?: string; roomOption?: string; inviteMessage?: string; inviterSex?: string; inviterRating?: number; inviterLevel?: string
  } | null>(null)
  const [customEquipIds, setCustomEquipIds] = useState({ charaId: 0, haiId: 0, bgId: 0 })
  const customEquipIdsRef = useRef(customEquipIds)
  const setCustomSkinEquip = useCustomSkinStore(state => state.setEquip)
  const setCustomSkinEquips = useCustomSkinStore(state => state.setEquips)
  const customEquipRouteState = () => ({
    customBgId: customEquipIdsRef.current.bgId,
    customHaiId: customEquipIdsRef.current.haiId,
    customBoardType: useCustomSkinStore.getState().bgType,
  })
  const [roomOpt, setRoomOpt]   = useState<MJOption>(() => ({ ...DEFAULT_OPTION, ...(navState?.lobbyOption ?? {}) }))
  const [clientCfg, setClientCfg] = useState<MJConfig>(() => loadMajakConfig())
  /** ゲームコイン残高 — mjkc17e / channel:entered で更新 (原典: m_pMember->m_llGamMoney) */
  const [gamMoney, setGamMoney] = useState<number>(0)
  /** 龍珠残高 — channel:entered / mjkc33e で更新 (原典: m_pMember->m_nGemCount) */
  const [gemCount, setGemCount] = useState<number>(0)
  /** キャッシュ残高 — channel:entered で更新 */
  const [cashCount, setCashCount] = useState<number>(0)
  /** 麻雀アイテム所持情報 — c1e channel enter cache (原典: theApp.m_UserInfo.m_listMajItem) */
  const [majItems, setMajItems] = useState<RawMajItem[]>([])
  /** 称号 — channel:entered で更新 (原典: m_pMember->m_szSlevel) */
  const [slevel, setSlevel] = useState<string>('')
  /** チャンネル名 — channel:entered で更新 (CHANELMAST.CHANELNAME) */
  const [channelName, setChannelName] = useState<string>('')
  /** トリック称号名 — channel:entered 原典: keyTrickTitleName */
  const [trickTitleName, setTrickTitleName] = useState<string>('')
  /** 麻雀称号名 — channel:entered 原典: keyMajakTitleName */
  const [majakTitleName, setMajakTitleName] = useState<string>('')
  const [rejectInvite, setRejectInvite] = useState(false)

  useEffect(() => {
    if (!oneToOneChat || layoutMode === 'desktop') return
    const visualViewport = window.visualViewport
    const update = () => setOneToOneChatViewport({
      top: visualViewport?.offsetTop ?? 0,
      left: visualViewport?.offsetLeft ?? 0,
      width: visualViewport?.width ?? window.innerWidth,
      height: visualViewport?.height ?? window.innerHeight,
    })
    update()
    window.addEventListener('resize', update)
    visualViewport?.addEventListener('resize', update)
    visualViewport?.addEventListener('scroll', update)
    return () => {
      window.removeEventListener('resize', update)
      visualViewport?.removeEventListener('resize', update)
      visualViewport?.removeEventListener('scroll', update)
    }
  }, [layoutMode, oneToOneChat])
  const [rejectChat, setRejectChat] = useState(false)
  const [tournamentList, setTournamentList] = useState<TournamentEntry[]>([])
  const [tournamentJoinSeqNo, setTournamentJoinSeqNo] = useState(0)
  const [selectedTournamentSeqNo, setSelectedTournamentSeqNo] = useState<number | null>(null)
  const [tournamentPage, setTournamentPage] = useState<'list' | 'match'>('list')
  const [tournamentDetailPlan, setTournamentDetailPlan] = useState<TournamentEntry | null>(null)
  const [tournamentDetails, setTournamentDetails] = useState<TournamentDetailEntry[]>([])
  const tournamentDetailActionRef = useRef<'select' | 'page' | null>(null)
  const player = useAuthStore.getState().player
  const displayMembers = membersWithSelfFirst(members, player?.pix ?? '')
  const autoMatchingChannel = isAutoMatchingChannel(channelId)
  const daniChannel = isDaniChannel(channelId)
  const trainingChannel = isTrainingChannel(channelId)
  const replayChannel = isReplayChannel(channelId)
  const tournamentChannel = isTournamentChannel(channelId)
  const showShopButtons = !trainingChannel
  const showRankingButton = !trainingChannel && daniChannel && !tournamentChannel
  const showMissionButton = !trainingChannel && !daniChannel && !tournamentChannel
  const showFreeChargeButton = !trainingChannel && !tournamentChannel
  const selectedTournamentFromList = tournamentList.find(item => item.seqNo === selectedTournamentSeqNo) ?? null

  useEffect(() => {
    setIsLobbyDataReady(false)
    const initialMessages = systemChatMessages([CHAT_INIT_MESSAGE])
    if (autoMatchingChannel) {
      initialMessages.push(...systemChatMessages(AUTO_MATCH_GUIDE_MESSAGES, '#ff0000'))
    }
    setChatLog(initialMessages)
  }, [channelId, autoMatchingChannel])
  const selectedTournament = tournamentDetailPlan?.seqNo === selectedTournamentSeqNo ? tournamentDetailPlan : selectedTournamentFromList
  const isTournamentJoined = selectedTournament != null && selectedTournament.seqNo === tournamentJoinSeqNo
  const tournamentCancelDeadline = selectedTournament
    ? new Date(selectedTournament.playStartDt.replace(' ', 'T')).getTime() - 5 * 60 * 1000
    : 0
  const isTournamentEntryOpen = selectedTournament?.playStatus === 2
    && Number.isFinite(tournamentCancelDeadline)
    && Date.now() < tournamentCancelDeadline
  const canTournamentJoin = selectedTournament != null && !isTournamentJoined && isTournamentEntryOpen
  const canTournamentJoinCancel = isTournamentJoined && isTournamentEntryOpen

  useEffect(() => { roomsRef.current = rooms }, [rooms])
  useEffect(() => { membersRef.current = members }, [members])
  useEffect(() => { configureMajakSound(clientCfg) }, [clientCfg])

  /** AP-04 §8 (改定): ロビー入室時に WebSocket 接続を確立する (レガシー設計準拠)
   * マウント時:
   *   1. GET /api/channel/{id}/server → Redis リースから担当サーバー URL を取得
   *   2. SignalR.connect(serverUrl) → WebSocket 接続
  *   3. send('c1e') → DB ロード + チャンネルグループ登録 + Redis メンバー登録
   * アンマウント時: SignalR.disconnect() → コネクション切断 + Redis メンバー削除
   *
  * ルーム/メンバー一覧: c1e で初期データ受信
   * リアルタイム更新: mjkroom / channel:member_joined/left WebSocket イベント
   */
  useEffect(() => {
    let mounted = true
    const connectionOwner = Symbol('LobbyScreen')

    if (channelId) {
      getChannels().then(channels => {
        if (!mounted) return
        const channel = channels.find(c => c.subId === channelId || c.chanelId === channelId)
        setRoomSlotCount(channel?.maxRoom && channel.maxRoom > 0 ? channel.maxRoom : DEFAULT_ROOM_SLOT_COUNT)
      }).catch(() => {
        if (mounted) setRoomSlotCount(DEFAULT_ROOM_SLOT_COUNT)
      })
    }

    async function init() {
      let connectedServerUrl = ''
      const getConnectedServerUrl = () => connectedServerUrl

      const tryContinueRestoreFromApi = async (pix: string) => {
        if (!pix || continueRestoreLookupPendingRef.current) return
        continueRestoreLookupPendingRef.current = true
        try {
          const room = await getPlayerContinueRoom(pix).catch(() => null)
          if (!mounted || continueRestoreHandledRef.current) return
          if (readAbandonRoomOnEnter(channelId ?? '')) return
          const continueChannelId = room?.channelId ?? room?.chanelId
          if (!room?.roomId || !continueChannelId || continueChannelId !== channelId) return

          const continueRoom: RoomEntry = {
            roomId: room.roomId,
            title: room.title ?? '',
            memberCnt: 0,
            memberMax: 4,
            viewerCnt: 0,
            opMemberCnt: 1,
            seats: [{ pix, pos: 0, disconnected: true }],
            isPrivate: false,
            roomOption: room.roomOption ?? '',
            serverUrl: room.serverUrl || getConnectedServerUrl(),
            roomPlaying: 1,
          }

          continueRestoreHandledRef.current = true
          keepSignalRForRoomRef.current = true
          navigate(
            `/channel/${channelId}/lobby/room/${continueRoom.roomId}`,
            {
              replace: true,
              state: {
                serverUrl: continueRoom.serverUrl,
                mode: 'auto',
                resumePlaying: true,
                skipEnterChannel: true,
                roomOption: continueRoom.roomOption,
                roomTitle: continueRoom.title,
                ...customEquipRouteState(),
                autoEnterPayload: buildContinueAutoEnterPayload(continueRoom, pix),
              },
            },
          )
        } finally {
          continueRestoreLookupPendingRef.current = false
        }
      }

      const tryContinueRestore = (roomList: RoomEntry[]) => {
        if (!mounted || continueRestoreHandledRef.current) return
        if (readAbandonRoomOnEnter(channelId ?? '')) return
        const pix = player?.pix ?? ''
        const restoreTarget = findContinueRoomForPix(roomList, pix)
        if (!restoreTarget) {
          void tryContinueRestoreFromApi(pix)
          return
        }
        const continueRoom = restoreTarget.room

        continueRestoreHandledRef.current = true
        keepSignalRForRoomRef.current = true
        navigate(
          `/channel/${channelId}/lobby/room/${continueRoom.roomId}`,
          {
            replace: true,
            state: {
              serverUrl: continueRoom.serverUrl || getConnectedServerUrl(),
              mode: restoreTarget.needsAutoEnter ? 'auto' : 'enter',
              resumePlaying: true,
              skipEnterChannel: true,
              roomOption: continueRoom.roomOption,
              roomTitle: continueRoom.title,
              ...customEquipRouteState(),
              autoEnterPayload: restoreTarget.needsAutoEnter ? buildContinueAutoEnterPayload(continueRoom, pix) : undefined,
            },
          },
        )
      }

      const tryTournamentRestore = (data: Record<string, unknown>, roomList: RoomEntry[]) => {
        if (!mounted || continueRestoreHandledRef.current) return false
        if (readAbandonRoomOnEnter(channelId ?? '')) return false
        const tournamentRoomId = Number(data.tournamentRoomId ?? data.mjkk102e ?? 0)
        const pix = player?.pix ?? ''
        if (tournamentRoomId <= 0 || !pix) return false

        const listedRoom = roomList.find(room => room.roomId === tournamentRoomId)
        const tournamentRoom: RoomEntry = listedRoom ?? {
          roomId: tournamentRoomId,
          title: '',
          memberCnt: 0,
          memberMax: 4,
          viewerCnt: 0,
          opMemberCnt: 1,
          seats: [],
          isPrivate: false,
          roomOption: '',
          serverUrl: getConnectedServerUrl(),
          roomPlaying: 1,
        }

        continueRestoreHandledRef.current = true
        keepSignalRForRoomRef.current = true
        navigate(`/channel/${channelId}/lobby/room/${tournamentRoomId}`, {
          replace: true,
          state: {
            serverUrl: tournamentRoom.serverUrl || getConnectedServerUrl(),
            mode: 'auto',
            resumePlaying: true,
            skipEnterChannel: true,
            roomOption: tournamentRoom.roomOption,
            roomTitle: tournamentRoom.title,
            ...customEquipRouteState(),
            autoEnterPayload: buildContinueAutoEnterPayload(tournamentRoom, pix),
          },
        })
        return true
      }

      const requestTournamentList = () => {
        SignalR.send('mjkc26e', buildTournamentMemberPayload(player?.pix ?? '')).catch(() => {})
      }

      /**
      * c1e — EnterChannel コマンドに対するサーバー応答
       * レガシー ProcessEnterChannelCommand:
       *   failcode=30002 → SendMoneyReplenishment(EXITREASON_NO_ADMISSION)
       *   その他エラー    → エラー表示
       *   success        → ルーム/メンバーリスト展開 + ウェルカム表示
       * 応答フィールド: result(1=成功), rooms[], members[], gammoney 等
       */
      const onChannelEntered = (data: Record<string, unknown>) => {
        if (!mounted) return
        const result = data.k1e ?? data.result
        if (!isOk(result)) {
          console.error('[LobbyScreen] EnterChannel failed response', {
            channelId,
            pix: player?.pix ?? '',
            result,
            k1e: data.k1e,
            resultField: data.result,
            k2e: data.k2e,
            message: data.message,
            error: data.error,
            payload: data,
          })
        }
        if (!checkResult(data, 'チャンネルへの入室に失敗しました')) {
          navigate('/channel', { replace: true })
          return
        }
        clearAbandonRoomOnEnter()
        console.info('[LobbyScreen] EnterChannel succeeded response', {
          channelId,
          pix: player?.pix ?? '',
          result: data.k1e ?? data.result,
          members: Array.isArray(data.members) ? data.members.length : undefined,
          rooms: Array.isArray(data.rooms) ? data.rooms.length : undefined,
        })
        const roomList = Array.isArray(data.rooms)
          ? mergeLegacyRoomSeats(data, (data.rooms as Array<Record<string, unknown>>).map(readRoomEntry))
          : []
        _setRooms(roomList)
        if (Array.isArray(data.customEquips)) {
          const nextCustomEquip = setCustomSkinEquips(data.customEquips as Array<Record<string, unknown>>)
          const nextCustomEquipIds = { charaId: nextCustomEquip.charaId, haiId: nextCustomEquip.haiId, bgId: nextCustomEquip.bgId }
          console.info('[LobbyScreen] EnterChannel customEquips cache', nextCustomEquipIds)
          customEquipIdsRef.current = nextCustomEquipIds
          setCustomEquipIds(nextCustomEquipIds)
        }
        if (!tryTournamentRestore(data, roomList)) tryContinueRestore(roomList)
        const memberList = Array.isArray(data.members) ? data.members as Array<Record<string, unknown>> : []
        _setMembers(uniqueMemberEntries(memberList.map(readMemberEntry)))
        // DrawMemberInfo 相当: gammoney / gemcount / slevel を初期表示に反映
        if (typeof data.gammoney === 'number') setGamMoney(data.gammoney as number)
        if (typeof data.gemcount === 'number') setGemCount(data.gemcount as number)
        if (typeof data.cashCount === 'number') setCashCount(data.cashCount as number)
        if (Array.isArray(data.majItems)) {
          const nextMajItems = (data.majItems as Array<Record<string, unknown>>)
            .map(normalizeRawMajItem)
            .filter(item => item.itemCode !== '')
          console.info('[LobbyScreen] EnterChannel majItems cache', {
            count: nextMajItems.length,
            items: nextMajItems.map(item => ({ itemCode: item.itemCode, useFlag: item.useFlag, qty: item.qty, endDt: item.endDt })),
          })
          setMajItems(nextMajItems)
        }
        if (typeof data.slevel   === 'string') setSlevel(data.slevel as string)
        if (typeof data.channelName === 'string') setChannelName(data.channelName as string)
        if (typeof data.trickTitleName === 'string') setTrickTitleName(data.trickTitleName as string)
        if (typeof data.majakTitleName === 'string') setMajakTitleName(data.majakTitleName as string)
        setIsLobbyDataReady(true)
        if (tournamentChannel) requestTournamentList()
      }
      SignalR.on('c1e', onChannelEntered)

      /** mjkc26e — CMajakChannelWnd::RequestTournamentList 応答 */
      const onTournamentList = (data: Record<string, unknown>) => {
        if (!mounted) return
        const list = readTournamentListPayload(data)
        setTournamentList(list)
        setTournamentJoinSeqNo(Number(data.tournamentJoinChk ?? 0))
        setSelectedTournamentSeqNo(prev => prev != null && list.some(item => item.seqNo === prev) ? prev : list[0]?.seqNo ?? null)
      }
      SignalR.on('mjkc26e', onTournamentList)

      const onTournamentListChanged = () => {
        if (!mounted || !tournamentChannel) return
        requestTournamentList()
      }
      SignalR.on('tournament:list_changed', onTournamentListChanged)

      /** mjkc30e — CMajakChannelWnd::OnBtnTournament 応答 → OnEnterTournamentRoom */
      const onTournamentDetail = (data: Record<string, unknown>) => {
        if (!mounted) return
        if (Number(data.result ?? 0) !== 1) {
          tournamentDetailActionRef.current = null
          return
        }
        const { plan, details } = readTournamentDetailPayload(data)
        if (plan) {
          setTournamentDetailPlan(plan)
          setTournamentList(prev => prev.some(item => item.seqNo === plan.seqNo)
            ? prev.map(item => item.seqNo === plan.seqNo ? plan : item)
            : [...prev, plan])
          setSelectedTournamentSeqNo(plan.seqNo)
        }
        setTournamentDetails(details)
        if (tournamentDetailActionRef.current === 'page') {
          setTournamentPage('match')
          SignalR.send('c7e', buildGetMemberListPayload(channelId ?? '')).catch(() => {})
        }
        tournamentDetailActionRef.current = null
      }
      SignalR.on('mjkc30e', onTournamentDetail)

      /** mjkc27e — CMJMemberInfoDialog::UpdateMeetingRegist */
      const onTournamentRegistResult = (data: Record<string, unknown>) => {
        if (!mounted) return
        if (typeof data.gamMoney === 'number') setGamMoney(data.gamMoney as number)
        if (Number(data.result ?? 0) === 1) {
          setShowTournamentRegist(false)
          requestTournamentList()
          return
        }
        showError(tournamentRegistErrorMessage(data.failCode))
      }
      SignalR.on('mjkc27e', onTournamentRegistResult)

      const onTournamentJoinResult = (data: Record<string, unknown>) => {
        if (!mounted) return
        setTournamentActionPending(false)
        if (Number(data.result ?? 0) !== 1) {
          showError(tournamentErrorMessage(Number(data.failCode ?? 0), 'join'))
          return
        }
        if (typeof data.gamMoney === 'number') setGamMoney(data.gamMoney as number)
        setShowTournamentJoin(false)
        setTournamentJoinPassword('')
        requestTournamentList()
      }
      const onTournamentJoinCancelResult = (data: Record<string, unknown>) => {
        if (!mounted) return
        setTournamentActionPending(false)
        if (Number(data.result ?? 0) !== 1) {
          showError(tournamentErrorMessage(Number(data.failCode ?? 0), 'cancel'))
          return
        }
        if (typeof data.gamMoney === 'number') setGamMoney(data.gamMoney as number)
        requestTournamentList()
      }
      SignalR.on('mjkc28e', onTournamentJoinResult)
      SignalR.on('mjkc29e', onTournamentJoinCancelResult)

      /**
       * room:get_list — GetRoomListCommand 応答 (room:get_list)
       * レガシー ProcessCommand_GetRoomList: result check → room list update
       * 応答フィールド: k51e, k42e1..k42eN / 互換: result(1=成功), count, rooms[]
       */
      const onRoomList = (data: Record<string, unknown>) => {
        if (!mounted) return
        const legacyRoomCount = Number(data.k51e ?? 0)
        if (data.k51e == null && Number(data.result) !== 1) return  // 静かに無視
        if (legacyRoomCount > 0) setRoomSlotCount(legacyRoomCount)
        const list = Array.isArray(data.rooms)
          ? mergeLegacyRoomSeats(data, (data.rooms as Array<Record<string, unknown>>).map(readRoomEntry))
          : readLegacyRoomList(data)
        _setRooms(list)
        tryContinueRestore(list)
      }
      SignalR.on('c12e', onRoomList)

      /**
       * member:get_list — GetMemberListCommand 応答
       * 応答フィールド: result(1=成功), count, members[]
       */
      const onMemberList = (data: Record<string, unknown>) => {
        if (!mounted) return
        if (Number(data.result) !== 1 && data.k1e !== 'v1e') return
        _setMembers(readMemberListPayload(data))
      }
      SignalR.on('c7e', onMemberList)

      /**
       * chat:relay — HanChatAllRelayCommand ブロードキャスト
       * フィールド: k3e, k57e, k38e, k40e, k41e / 互換: pix, playerType, target, string
       */
      const onChat = (data: Record<string, unknown>) => {
        if (!mounted) return
        if (rejectChat) return
        const senderPix = String(data.k3e ?? data.pix ?? data['member' + 'Id'] ?? '')
        if (mutedPixesRef.current.has(senderPix)) return

        const target = normalizeChatTarget(data.k38e ?? data.target)
        const isWhisper = target !== CHAT_TARGET_ALL && target.toLowerCase() !== 'all'
        const myPix = player?.pix ?? ''
        if (isWhisper && target !== myPix && senderPix !== myPix) return

        setChatLog(prev => [
          ...prev,
          {
            id:   Date.now(),
            name: displayNameForPix(senderPix),
            pix: senderPix,
            text: String(data.k41e ?? data.string ?? ''),
            color: isWhisper ? '#808080' : undefined,
          },
        ])
      }
      SignalR.on('hc1e', onChat)

      const onOneToOneChat = (data: Record<string, unknown>) => {
        if (!mounted) return
        const sendMember = data.sendMember as Record<string, unknown> | undefined
        const receiveMember = data.receiveMember as Record<string, unknown> | undefined
        const sender = String(sendMember?.pix ?? data.sender ?? '')
        const recipient = String(receiveMember?.pix ?? data.target ?? '')
        const myPix = player?.pix ?? ''
        if (!myPix || sender !== myPix) return
        const target = recipient
        const partnerName = String(receiveMember?.name ?? displayNameForPix(target))
        if (target) setOneToOneChat(current => current?.target === target
          ? { ...current, partnerName, partnerOnline: true }
          : { target, partnerName, partnerOnline: true, messages: [] })
      }
      SignalR.on('hc6e', onOneToOneChat)

      const onOneToOneChatString = (data: Record<string, unknown>) => {
        if (!mounted) return
        const sendMember = data.sendMember as Record<string, unknown> | undefined
        const receiveMember = data.receiveMember as Record<string, unknown> | undefined
        const sender = String(sendMember?.pix ?? data.sender ?? '')
        const recipient = String(receiveMember?.pix ?? data.target ?? '')
        const myPix = player?.pix ?? ''
        if (!myPix || (sender !== myPix && recipient !== myPix)) return
        const target = sender === myPix ? recipient : sender
        const partnerName = String((sender === myPix ? receiveMember : sendMember)?.name ?? displayNameForPix(target))
        const text = String(data.k41e ?? data.string ?? '')
        if (!target || !text) return
        setOneToOneChat(current => current?.target === target
          ? { ...current, partnerName, partnerOnline: true, messages: [...current.messages, { sender, text }] }
          : { target, partnerName, partnerOnline: true, messages: [{ sender, text }] })
      }
      SignalR.on('hc7e', onOneToOneChatString)

      const onOneToOneChatEnd = (data: Record<string, unknown>) => {
        const sendMember = data.sendMember as Record<string, unknown> | undefined
        const sender = String(sendMember?.pix ?? data.sender ?? '')
        const recipient = String((data.receiveMember as Record<string, unknown> | undefined)?.pix ?? data.target ?? '')
        const myPix = player?.pix ?? ''
        if (recipient !== myPix || !sender) return
        setOneToOneChat(current => current?.target === sender
          ? { ...current, partnerName: String(sendMember?.name ?? current.partnerName), partnerOnline: false, messages: [...current.messages, { sender, text: `${String(sendMember?.name ?? current.partnerName)}さんがチャットを終了しました。`, system: true }] }
          : current)
      }
      SignalR.on('hc8e', onOneToOneChatEnd)

      /** channel:notice — G::commandNotice: keyString を公知領域へ表示 */
      const onNotice = (data: Record<string, unknown>) => {
        if (!mounted) return
        setNotice(readNoticePayload(data))
      }
      SignalR.on('c40e', onNotice)

      /**
       * mjkroom — RoomStateCommand: ルーム一覧変化通知 (入退室など)
       * レガシー ProcessRoomInfoCommand: 受信した room info を適用する
       */
      const onRoomState = (data: Record<string, unknown>) => {
        if (!mounted) return
        const entry = readLegacyRoomState(data)
        if (!entry || entry.roomId <= 0) {
          SignalR.send('c12e', {}).catch(() => {})
          return
        }

        setRoomSlotCount(prev => Math.max(prev, entry.roomId))
        let nextRoomsForContinue: RoomEntry[] | null = null
        _setRooms(prev => {
          const withoutRoom = prev.filter(room => room.roomId !== entry.roomId)
          if (entry.memberCnt <= 0 && entry.viewerCnt <= 0 && (entry.opMemberCnt ?? 0) <= 0) {
            nextRoomsForContinue = withoutRoom
            return withoutRoom
          }
          const nextRooms = [...withoutRoom, entry].sort((a, b) => a.roomId - b.roomId)
          nextRoomsForContinue = nextRooms
          return nextRooms
        })
        if (nextRoomsForContinue) tryContinueRestore(nextRoomsForContinue)
        SignalR.send('c7e', buildGetMemberListPayload(channelId ?? '')).catch(() => {})
      }
      SignalR.on('mjkroom', onRoomState)

      /** channel:member_joined — AddMember 相当: メンバーリストを再取得 */
      const onMemberJoined = (_data: Record<string, unknown>) => {
        if (!mounted) return
        SignalR.send('c7e', buildGetMemberListPayload(channelId ?? '')).catch(() => {})
      }

      /** channel:member_left — DeleteMember 相当: G::keyPix のメンバーを削除 */
      const onMemberLeft = (data: Record<string, unknown>) => {
        if (!mounted) return
        const pix = String(data.k3e ?? data.pix ?? data['member' + 'Id'] ?? '')
        if (!pix) return
        SignalR.send('c7e', buildGetMemberListPayload(channelId ?? '')).catch(() => {
          _setMembers(prev => prev.filter(member => member.pix !== pix))
        })
      }
      SignalR.on('c5e', onMemberJoined)
      SignalR.on('c6e', onMemberLeft)

      /** ゲーム招待受信 (CMJGetReqGameDialog 相当) */
      const onInviteGame = (data: Record<string, unknown>) => {
        if (!mounted) return
        const inviterId = String(data.k3e ?? data.inviterId ?? '')
        const roomId = Number(data.k42e ?? data.roomId ?? 0)
        if (!inviterId || roomId <= 0) return

        if (isLegacyInviteCancel(data.k64e)) {
          setInviteData(prev => prev?.inviterId === inviterId && prev.roomId === roomId ? null : prev)
          return
        }

        if (rejectInvite) {
          SignalR.send('c23e', buildInviteResponsePayload(inviterId, roomId, false)).catch(() => {})
          return
        }
        setInviteData(prev => {
          if (prev) {
            SignalR.send('c23e', buildInviteResponsePayload(inviterId, roomId, false)).catch(() => {})
            return prev
          }
          const inviter = membersRef.current.find(member => member.pix === inviterId)
          const room = roomsRef.current.find(item => item.roomId === roomId)
          return {
            inviterId,
            inviterName:   inviter?.name || inviterId,
            roomId,
            roomPwd:       String(data.k67e ?? data.roomPwd ?? ''),
            avatarId:      inviter?.avatarId,
            roomName:      room?.title ?? String(data.k45e ?? data.roomName ?? ''),
            roomOption:    room?.roomOption ?? String(data.k46e ?? data.roomOption ?? ''),
            inviteMessage: String(data.k65e ?? data.inviteMessage ?? ''),
            inviterSex:    inviter?.sex,
            inviterRating: inviter?.rating,
            inviterLevel:  inviter?.slevel,
          }
        })
      }
      SignalR.on('c22e', onInviteGame)

      /** InviteResponse — ProcessResponseInviteGameCommand: 招待応答結果 */
      const onInviteResponse = (data: Record<string, unknown>) => {
        if (!mounted) return
        const pix = String(data.k3e ?? data.pix ?? data['member' + 'Id'] ?? '')
        if (!pix) return
        setChatLog(prev => [
          ...prev,
          { id: Date.now(), name: 'System', text: inviteResponseMessage(displayNameForPix(pix), data.k64e), color: '#004000' },
        ])
      }
      SignalR.on('c23e', onInviteResponse)

      /**
       * mjkc17e — MoneyReplenishmentCommand 応答
       * サーバーは result = "success" / "failure" (文字列) を使う
       * レガシー ProcessCommand_MoneyReplenishment:
       *   result == failure → エラー表示
       *   success           → コイン残高更新 (チャンネル全員へブロードキャスト)
       */
      const onMoneyReplenishment = (data: Record<string, unknown>) => {
        if (!mounted) return
        if (!checkResult(data, 'GP補充に失敗しました')) return
        // 原典: ProcessMoneyReplenishmentCommand → pData->m_llGamMoney を更新
        if (typeof data.gammoney === 'number') setGamMoney(data.gammoney as number)
      }
      SignalR.on('mjkc17e', onMoneyReplenishment)

      /** mjkc25e — OnBtnRankingClicked → ShowRankingDialog 相当 */
      const onRatingRankInfo = (data: Record<string, unknown>) => {
        if (!mounted) return
        if (Number(data.result ?? 0) !== 1) {
          showMessage('ランキング情報を取得中です。しばらくしてからお試しください。')
          return
        }
        setRankingData({
          rankDate: data.rankDate as number | string | undefined,
          rankId: data.rankId as number | string | undefined,
          gradeRankList: Array.isArray(data.gradeRankList) ? data.gradeRankList as RankingData['gradeRankList'] : [],
          gradeRankSelf: data.gradeRankSelf && typeof data.gradeRankSelf === 'object' ? data.gradeRankSelf as RankingData['gradeRankSelf'] : undefined,
        })
      }
      SignalR.on('mjkc25e', onRatingRankInfo)

      /**
       * mjkc2e — AutoMatchingCommand 応答
       * レガシー ProcessMajAutoMatching:
       *   failcode=E_INSUFFICIENT_MONEY → SendMoneyReplenishment(EXITREASON_SHORTOFMONEY)
       *   その他エラー                  → エラー表示
       * 応答フィールド: result(1=成功/0=失敗), failCode(string), message
       */
      const onAutoMatching = (data: Record<string, unknown>) => {
        if (!mounted) return
        const result = Number(data.result)
        if (result === 0) {
          setIsMatching(false)
          const failCode = String(data.failCode ?? data.failcode ?? '')
          if (failCode === '4' || failCode === 'E_INSUFFICIENT_MONEY') {
            showError(String(data.message ?? 'GPが不足しています。'))
          } else {
            showError(String(data.message ?? 'オートマッチングに失敗しました'))
          }
          return
        }

        // Queue registration intentionally has no response. Ignore packets without a result
        // instead of clearing the locally displayed matching state.
        if (result !== 1) return

        const matchedRoomId = Number(data.roomId ?? data.k42e ?? 0)
        if (!matchedRoomId) {
          setIsMatching(false)
          showError('オートマッチングに失敗しました')
          return
        }
        const connectedServerUrl = getConnectedServerUrl()

        keepSignalRForRoomRef.current = true
        navigate(
          `/channel/${channelId}/lobby/room/${matchedRoomId}`,
          {
            state: {
              serverUrl: connectedServerUrl,
              mode: 'auto',
              skipEnterChannel: true,
              roomOption: String(data.roomOption ?? data.k46e ?? ''),
              roomTitle: String(data.k45e ?? ''),
              roomPassword: String(data.roomPwd ?? data.k67e ?? ''),
              ...customEquipRouteState(),
              autoEnterPayload: {
                roomId: matchedRoomId,
                k42e: matchedRoomId,
                pix: player?.pix ?? '',
                k3e: player?.pix ?? '',
                connectFor: String(data.k82e ?? data.connectFor ?? 'v16e'),
                k82e: String(data.k82e ?? 'v16e'),
                playerType: 'v4e',
                k57e: 'v4e',
                playerPos: -1,
                k58e: -1,
                roomTitle: String(data.k45e ?? ''),
                k45e: String(data.k45e ?? ''),
                roomPwd: String(data.roomPwd ?? data.k67e ?? ''),
                k67e: String(data.roomPwd ?? data.k67e ?? ''),
                roomMinCnt: data.roomMinCnt ?? data.k127e ?? 4,
                k127e: data.k127e ?? data.roomMinCnt ?? 4,
                roomLimitCnt: data.roomLimitCnt ?? data.k66e ?? 4,
                k66e: data.k66e ?? data.roomLimitCnt ?? 4,
                roomOption: String(data.roomOption ?? data.k46e ?? ''),
                k46e: String(data.roomOption ?? data.k46e ?? ''),
                ipaddress: String(data.k52e ?? ''),
                k52e: String(data.k52e ?? ''),
                port: data.k53e ?? 0,
                k53e: data.k53e ?? 0,
              },
            },
          },
        )
      }
      SignalR.on('mjkc2e', onAutoMatching)

      /** mjkc3e — ProcessMajCancelAutoMatching: サーバ応答後に解除状態へ反映 */
      const onCancelAutoMatching = (_data: Record<string, unknown>) => {
        if (!mounted) return
        setIsMatching(false)
        setChatLog(prev => [...prev, ...systemChatMessages([AUTO_MATCH_ABORT_MESSAGE], '#000040')])
      }
      SignalR.on('mjkc3e', onCancelAutoMatching)

      /**
       * room:connect_error — AutoEnterRoomCommand エラー
       * オートマッチング確定後、ルーム入室処理で失敗した場合に受信
       * レガシー: HG_UWM_CONDITION_UNMATCH 相当
       * 応答フィールド: k42e, k2e, failcode
       */
      const onRoomConnectError = (data: Record<string, unknown>) => {
        if (!mounted) return
        showError(String(data.k2e ?? data.message ?? 'ルーム接続に失敗しました'))
      }
      SignalR.on('c34e', onRoomConnectError)

      let connectionLostHandled = false
      const onConnectionLost = (error?: Error) => {
        if (!mounted || connectionLostHandled) return
        connectionLostHandled = true
        console.error('[LobbyScreen] SignalR connection lost', {
          channelId,
          pix: player?.pix ?? '',
          error,
        })
        showError('サーバーとの接続が切断されました')
        navigate(channelId ? `/channel/${channelId}` : '/channel', { replace: true })
      }
      SignalR.onConnectionLost(onConnectionLost)
      const onBrowserOffline = () => onConnectionLost()
      window.addEventListener('offline', onBrowserOffline)

      const cleanupSignalR = () => {
        SignalR.offConnectionLost(onConnectionLost)
        window.removeEventListener('offline', onBrowserOffline)
        SignalR.off('c1e',               onChannelEntered)
        SignalR.off('mjkc26e',           onTournamentList)
        SignalR.off('tournament:list_changed', onTournamentListChanged)
        SignalR.off('mjkc30e',           onTournamentDetail)
        SignalR.off('mjkc27e',           onTournamentRegistResult)
        SignalR.off('mjkc28e',           onTournamentJoinResult)
        SignalR.off('mjkc29e',           onTournamentJoinCancelResult)
        SignalR.off('c12e',              onRoomList)
        SignalR.off('c7e',               onMemberList)
        SignalR.off('hc1e',              onChat)
        SignalR.off('hc6e',              onOneToOneChat)
        SignalR.off('hc7e',              onOneToOneChatString)
        SignalR.off('hc8e',              onOneToOneChatEnd)
        SignalR.off('c40e',              onNotice)
        SignalR.off('mjkroom',           onRoomState)
        SignalR.off('c5e',               onMemberJoined)
        SignalR.off('c6e',               onMemberLeft)
        SignalR.off('c22e',              onInviteGame)
        SignalR.off('c23e',              onInviteResponse)
        SignalR.off('mjkc17e',           onMoneyReplenishment)
        SignalR.off('mjkc25e',           onRatingRankInfo)
        SignalR.off('mjkc2e',            onAutoMatching)
        SignalR.off('mjkc3e',            onCancelAutoMatching)
        SignalR.off('c34e',              onRoomConnectError)
        if (!keepSignalRForRoomRef.current && signalRConnectionOwnerRef.current === connectionOwner) {
          signalRConnectionOwnerRef.current = null
          SignalR.disconnect().catch(() => {})
        }
      }

      /**
       * チャンネル担当サーバー URL を Redis リースから取得し WebSocket 接続
       * GET /api/channel/{id}/server → ResolveChannelServerAsync()
       * レガシー: HgChannelWnd::OnSocketConnect 相当
       */
      try {
        const resolvedServerUrl = await getChannelServerUrl(channelId ?? '')
        if (!mounted) {
          cleanupSignalR()
          return cleanupSignalR
        }
        connectedServerUrl = resolvedServerUrl
        connectedServerUrlRef.current = resolvedServerUrl
        console.info('[LobbyScreen] resolved channel server', { channelId, resolvedServerUrl })
        signalRConnectionOwnerRef.current = connectionOwner
        await SignalR.connect(`${resolvedServerUrl}/hubs/majak`)
        if (!mounted) {
          cleanupSignalR()
          return cleanupSignalR
        }
        const abandonRoom = readAbandonRoomOnEnter(channelId ?? '')
        const enterPayload = buildEnterChannelPayload(
          channelId ?? '',
          player,
          abandonRoom?.roomId ?? 0,
          Boolean(abandonRoom?.fatalRoomError),
        )
        const { password: _password, ...enterLogPayload } = enterPayload
        console.info('[LobbyScreen] sending EnterChannel c1e', enterLogPayload)
        await SignalR.send('c1e', enterPayload)
      } catch (err) {
        console.error('[LobbyScreen] channel init/send failed', {
          channelId,
          pix: player?.pix ?? '',
          error: err,
        })
        cleanupSignalR()
        throw err
      }

      return cleanupSignalR
    }

    let cleanup: (() => void) | undefined
    init()
      .then(fn => {
        if (mounted) cleanup = fn
        else fn()
      })
      .catch(err => {
        if (!mounted) return
        console.error('[LobbyScreen] init failed', err)
        showError('サーバーへの接続に失敗しました')
        navigate(channelId ? `/channel/${channelId}` : '/channel', { replace: true })
      })

    return () => {
      mounted = false
      cleanup?.()
    }
  }, [channelId, navigate, rejectInvite, rejectChat])

  /** チャットログ自動スクロール */
  useEffect(() => {
    if (chatLogRef.current) {
      chatLogRef.current.scrollTop = chatLogRef.current.scrollHeight
    }
  }, [chatLog])

  useEffect(() => {
    if (!notice) return
    const timer = window.setTimeout(() => setNotice(null), notice.durationMs)
    return () => window.clearTimeout(timer)
  }, [notice])

  const myPix = player?.pix ?? ''
  const accuseChatMessages = chatLog.filter(msg => msg.name && msg.name !== 'System' && msg.text)
  const accuseChatContent = accuseChatMessages.map(msg => `[${msg.name}] ${msg.text}`).join('\n')
  const accuseSpeakers = [...new Set(accuseChatMessages.map(msg => msg.pix ?? msg.name).filter(pix => pix !== myPix))]
  const accuseSpeakerNameById = new Map(accuseSpeakers.map(pix => [pix, displayNameForPix(pix)]))
  const accuseSpeakerKey = accuseSpeakers.join('\0')

  useEffect(() => {
    const onAccuse = () => {
      if (accuseSpeakers.length === 0 || accuseChatContent.length === 0) {
        void showMessage('メッセージがありません。', 'お知らせ')
        return
      }
      const now = window.performance.now()
      if (now - lastAccuseOpenedAtRef.current < 30000) return
      lastAccuseOpenedAtRef.current = now
      setShowAccuse(true)
    }
    window.addEventListener(MAJAK_ACCUSE_EVENT, onAccuse)
    return () => window.removeEventListener(MAJAK_ACCUSE_EVENT, onAccuse)
  }, [accuseChatContent, accuseSpeakerKey])

  /**
   * チャット送信 (HanChatAllCommand 相当)
   * レガシー CHgChannelWnd::OnBtnTalk: HanChatAllRelay(command=chat:relay) を送信
   */
  const sendChat = async () => {
    const text = chatText.trim()
    if (!text) return
    setChatText('')
    const { command, args, rest } = readChatCommandParts(text)
    if (command === '?' || command === 'h') {
      setChatLog(prev => [...prev, ...systemChatMessages(CHAT_COMMAND_MESSAGES)])
      return
    }
    if (command === 'l') {
      setChatLog(prev => [...prev, ...systemChatMessages([`${CHAT_LOCATION_PREFIX}${channelName || channelId || ''}`])])
      return
    }
    if (command === 'm') {
      const targetId = args[0] ?? ''
      if (targetId) {
        mutedPixesRef.current.add(targetId)
        setChatLog(prev => [...prev, ...systemChatMessages([formatChatTemplate(CHAT_MUTE_DONE, targetId)])])
      }
      return
    }
    if (command === 'u') {
      const targetId = args[0] ?? ''
      if (targetId && mutedPixesRef.current.has(targetId)) {
        mutedPixesRef.current.delete(targetId)
        setChatLog(prev => [...prev, ...systemChatMessages([formatChatTemplate(CHAT_UNMUTE_DONE, targetId)])])
      }
      return
    }
    if (command === 'w') {
      const targetId = args[0] ?? ''
      const whisperText = targetId ? rest.slice(targetId.length).trimStart() : ''
      if (targetId && whisperText) {
        await SignalR.send('hc1e', { k38e: targetId, target: targetId, k41e: whisperText, string: whisperText }).catch(() => {})
      }
      return
    }
    if (command) return

    await SignalR.send('hc1e', { k38e: CHAT_TARGET_ALL, target: CHAT_TARGET_ALL, k41e: text, string: text }).catch(() => {})
  }

  /** WM_KEYDOWN / Enter キー → 送信 */
  const onChatKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      e.preventDefault()
      void sendChat()
    }
  }

  /** オートマッチング状態 (m_bWantMatching 相当) */
  const [isMatching, setIsMatching] = useState(false)

  /**
   * OnBtnIdleAtMtchClicked 相当 — mjkc2e (申込) / mjkc3e (取消)
   * AtMtchStateReflect(TRUE/FALSE) のチャット通知もここで行う
   */
  const onToggleAutoMatch = async () => {
    const next = !isMatching
    if (next) {
      setIsMatching(true)
      setChatLog(prev => [...prev, ...systemChatMessages([AUTO_MATCH_ENTRY_MESSAGE], '#000040')])
      try {
        await SignalR.send('mjkc2e', { pix: player?.pix ?? '', k3e: player?.pix ?? '' })
      } catch {
        setIsMatching(false)
        showError('オートマッチングの申し込みに失敗しました')
      }
    } else {
      await SignalR.send('mjkc3e', { pix: player?.pix ?? '', k3e: player?.pix ?? '' }).catch(() => {})
    }
  }

  /** ルーム入室 (OnEnterRoom 相当) — ルームに serverUrl を添えて遷移 */
  const onEnterRoom = (roomId: string, asViewer = false) => {
    if (autoMatchingChannel && isMatching && asViewer) {
      showError(AUTO_MATCH_WATCHES_WARN_MESSAGE)
      return
    }
    const room = rooms.find(r => String(r.roomId) === roomId)
    const roomServerUrl = room?.serverUrl || connectedServerUrlRef.current
    keepSignalRForRoomRef.current = true
    navigate(
      `/channel/${channelId}/lobby/room/${roomId}`,
      {
        state: {
          serverUrl: roomServerUrl,
          mode: asViewer ? 'view' : 'enter',
          skipEnterChannel: true,
          roomTitle: room?.title ?? '',
          roomOption: room?.roomOption ?? '',
          maxViewer: room?.maxViewer,
          ...customEquipRouteState(),
        },
      },
    )
  }

  /** チャンネル変更 (OnBtnChangeChannelClicked → ExitChannel → OnRefreshChannelList 相当) */
  const onChangeLobby = async () => {
    const group = getLobbySelectGroup(channelId)
    try {
      await SignalR.send('c2e', {})
    } catch {}
    await SignalR.disconnect().catch(() => {})
    navigate(group ? `/channel/select/${group}` : '/channel')
  }

  /** 終了 (IDC_SETTING_BTN_EXT 相当) */
  const onExit = () => window.dispatchEvent(new Event(MAJAK_EXIT_REQUEST_EVENT))

  /** ルーム作成 (CHgChannelWnd::OnCreateRoom 相当)
   * AP-04 §8: ルーム数が最少のサーバー URL を取得して RoomScreen へ遷移する。
   * 同一サーバーの場合はロビーの WebSocket 接続を維持して RoomScreen へ渡す。
   */
  const createRoomWithOption = async (nextRoomOpt: MJOption, roomInfo: RoomCreateInfo, slotNo: number) => {
    try {
      const bestServerUrl = await getBestServer()
      const optStr = optionToString(nextRoomOpt)
      const maxViewer = roomInfo.viewerEnable ? 12 : 0
      keepSignalRForRoomRef.current = true
      navigate(
        `/channel/${channelId}/lobby/room/${slotNo}`,
        {
          state: {
            serverUrl: bestServerUrl,
            mode: 'create',
            skipEnterChannel: true,
            roomId: slotNo,
            roomOption: optStr,
            maxViewer,
            moneyRate: 500,
            minMoney: 0,
            maxMoney: 0,
            isPrivate: roomInfo.isPrivate,
            roomTitle: roomInfo.title,
            roomPassword: roomInfo.password,
            ...customEquipRouteState(),
          },
        },
      )
    } catch (e) {
      showError('サーバーへの接続に失敗しました')
    }
  }

  const onCreateRoom = (slotNo: number) => {
    setPendingRoomCreateSlot(slotNo)
    setShowRoomCreate(true)
  }

  const openMemberProfile = (pix: string) => {
    const m = members.find(x => x.pix === pix)
    if (m) setShowPlayerInfo({
      pix: m.pix,
      name: m.name,
      sex: m.sex,
      rating: m.rating,
      avatarId: m.avatarId,
      slevel: m.slevel,
      nLevel: m.nlevel,
      location: m.location,
      winCount: m.winCount,
      loseCount: m.loseCount,
      drawCount: m.drawCount,
    })
  }

  /** プロフィール表示 (IDC_MEMBERLIST_MEMBERINFO 相当) → CMJPlayerInfo ダイアログ */
  const onViewProfile = () => {
    if (!selectedMember) return
    openMemberProfile(selectedMember)
  }

  /** 1対1チャット呼びかけ (IDC_MEMBERLIST_REQONETOONE 相当) */
  const startOneToOneChat = (pix: string) => {
    const m = members.find(x => x.pix === pix)
    if (!m) return
    if (m.pix === player?.pix) {
      showMessage('自分自身には1:1チャットを申し込めません。')
      return
    }
    if (m.roomId > 0) {
      showMessage('対局中のメンバーには1:1チャットを申し込めません。')
      return
    }
    SignalR.send('hc6e', { target: pix, k38e: pix }).catch(() => {})
  }

  const onReqOneToOne = () => {
    if (!selectedMember) return
    startOneToOneChat(selectedMember)
  }

  const sendOneToOneChat = () => {
    if (!oneToOneChat) return
    const text = oneToOneChatText.trim()
    if (!text) return
    SignalR.send('hc7e', { target: oneToOneChat.target, k38e: oneToOneChat.target, string: text, k41e: text }).catch(() => {})
    setOneToOneChatText('')
  }

  const endOneToOneChat = () => {
    if (!oneToOneChat) return
    SignalR.send('hc8e', { target: oneToOneChat.target, k38e: oneToOneChat.target }).catch(() => {})
    setOneToOneChat(null)
  }

  const openRanking = async () => {
    const now = new Date()
    const rankDate = now.getFullYear() * 100 + now.getMonth() + 1
    await SignalR.send('mjkc25e', {
      mjkk73e: 99,
      mjkk74e: rankDate,
      mjkk75e: 3,
      k3e: player?.pix ?? '',
    }).catch(() => {})
  }

  /** ルームリスト更新 (RefreshボタンWM_COMMAND 相当) */
  const onRefreshRoomList = async () => {
    if (tournamentChannel) {
      await SignalR.send('mjkc26e', buildTournamentMemberPayload(player?.pix ?? '')).catch(() => {})
      return
    }
    // room:get_list = Cmd.GetRoomList
    await SignalR.send('c12e', {}).catch(() => {})
  }

  const onSelectTournament = (entry: TournamentEntry) => {
    setSelectedTournamentSeqNo(entry.seqNo)
    tournamentDetailActionRef.current = 'select'
    SignalR.send('mjkc30e', buildTournamentNoPayload(player?.pix ?? '', entry.seqNo)).catch(() => {})
  }

  const onTournamentPage = async () => {
    if (!selectedTournament) return
    tournamentDetailActionRef.current = 'page'
    await SignalR.send('mjkc30e', buildTournamentNoPayload(player?.pix ?? '', selectedTournament.seqNo)).catch(() => {
      tournamentDetailActionRef.current = null
    })
  }

  const onTournamentBack = () => {
    setTournamentPage('list')
    setTournamentDetails([])
    SignalR.send('mjkc26e', buildTournamentMemberPayload(player?.pix ?? '')).catch(() => {})
  }

  const onTournamentWatch = (detail: TournamentDetailEntry, detailIndex: number) => {
    if (!selectedTournament || detail.roomId <= 0) return
    const roomMembers = detail.pixes.filter(pix => pix.length > 0).join('|')
    const tournamentSubId = Number(detail.subId || detailIndex + 1)
    if (!roomMembers || tournamentSubId <= 0) return
    if (tournamentJoinSeqNo !== 0) {
      showMessage('参加しているトーナメントがあります。開催時間前にはトーナメント一覧画面へ移動してください。', '警告')
    }
    const room = rooms.find(item => item.roomId === detail.roomId)
    const roomServerUrl = room?.serverUrl || connectedServerUrlRef.current
    keepSignalRForRoomRef.current = true
    navigate(`/channel/${channelId}/lobby/room/${detail.roomId}`, {
      state: {
        serverUrl: roomServerUrl,
        mode: 'view',
        skipEnterChannel: true,
        roomTitle: selectedTournament.playName,
        roomOption: selectedTournament.roomOption,
        maxViewer: selectedTournament.maxViewer,
        ...customEquipRouteState(),
        tournamentViewPayload: {
          tournamentNo: selectedTournament.seqNo,
          mjkk88e: selectedTournament.seqNo,
          tournamentSubId,
          mjkk99e: tournamentSubId,
          tournamentChkRoomMember: roomMembers,
          mjkk101e: roomMembers,
        },
      },
    })
  }

  const onTournamentRegist = (payload: TournamentRegistPayload) => {
    const pix = player?.pix ?? ''
    SignalR.send('mjkc27e', {
      pix,
      k3e: pix,
      roomOption: payload.roomOption,
      k46e: payload.roomOption,
      tournamentBaseRule: payload.tournamentBaseRule,
      mjkk84e: payload.tournamentBaseRule,
      tournamentMoneyRule: payload.tournamentMoneyRule,
      mjkk85e: payload.tournamentMoneyRule,
      tournamentName: payload.tournamentName,
      mjkk86e: payload.tournamentName,
      tournamentDate: payload.tournamentDate,
      mjkk87e: payload.tournamentDate,
      password: payload.password,
      k6e: payload.password,
      maxViewer: payload.maxViewer,
      k69e: payload.maxViewer,
      tournamentRegistFlag: payload.tournamentRegistFlag,
      mjkk94e: payload.tournamentRegistFlag,
    }).catch(() => {
      showError('サーバーへの送信に失敗しました')
    })
  }

  const onTournamentJoin = () => {
    if (!selectedTournament || !canTournamentJoin) return
    setTournamentJoinPassword('')
    setShowTournamentJoin(true)
  }

  const onTournamentJoinSubmit = async () => {
    if (!selectedTournament || tournamentActionPending) return
    setTournamentActionPending(true)
    const password = tournamentJoinPassword.slice(0, 8)
    await SignalR.send('mjkc28e', {
      ...buildTournamentNoPayload(player?.pix ?? '', selectedTournament.seqNo),
      password,
      k6e: password,
    }).catch(() => {
      setTournamentActionPending(false)
      showError('サーバーへの送信に失敗しました')
    })
  }

  const onTournamentJoinCancel = async () => {
    if (!selectedTournament || !canTournamentJoinCancel || tournamentActionPending) return
    const confirmed = await showConfirmMessage(`「${selectedTournament.playName}」への参加を取り消しますか？`, '参加取消確認')
    if (!confirmed) return
    setTournamentActionPending(true)
    await SignalR.send('mjkc29e', buildTournamentNoPayload(player?.pix ?? '', selectedTournament.seqNo)).catch(() => {
      setTournamentActionPending(false)
      showError('サーバーへの送信に失敗しました')
    })
  }

  const shopDialogs = (
    <>
      {showCustom && (
        <CustomDlg
          currentCharaId={customEquipIds.charaId}
          currentHaiId={customEquipIds.haiId}
          currentBgId={customEquipIds.bgId}
          onEquipChange={({ itemId, itemType }) => {
            setCustomEquipIds(prev => {
              const next = itemType >= 30 && itemType < 40
                ? { ...prev, charaId: itemId }
                : itemType >= 20 && itemType < 30
                  ? { ...prev, haiId: itemId }
                  : itemType >= 10 && itemType < 20
                    ? { ...prev, bgId: itemId }
                    : prev
              customEquipIdsRef.current = next
              setCustomSkinEquip(itemId, itemType)
              return next
            })
          }}
          onClose={() => setShowCustom(false)}
          onRequestShop={() => {
            setShowCustom(false)
            setShowShop(true)
          }}
        />
      )}

      {/* CItemShopDlg: IDC_BTN_ITEMSHOP 押下時表示 */}
      {showShop && (
        <ItemShopDlg
          onClose={() => setShowShop(false)}
          gamMoney={gamMoney}
          cashCount={cashCount}
          gemCount={gemCount}
          onBalanceUpdate={({ cashCount, gamMoney, gemCount }) => {
            if (typeof cashCount === 'number') setCashCount(cashCount)
            setGamMoney(gamMoney)
            setGemCount(gemCount)
          }}
          onConfirmItem={() => setShowConfirm(true)}
        />
      )}

      {/* CMJConfirmItemDlg: ItemShopDlg::OnBtnConfirmItem */}
      {showConfirm && (
        <ConfirmItemDlg
          majItems={majItems}
          onMajItemsChange={setMajItems}
          onClose={() => setShowConfirm(false)}
        />
      )}
    </>
  )

  const lobbyDialogs = (
    <>
      {showWelcome && <WelcomeDlg onClose={() => setShowWelcome(false)} />}

      {inviteData && (
        <GetReqGameDialog
          {...inviteData}
          onClose={() => setInviteData(null)}
          onAccepted={() => onEnterRoom(String(inviteData.roomId))}
        />
      )}

      {showPlayerInfo && (
        <PlayerInfoWnd
          player={showPlayerInfo}
          onClose={() => setShowPlayerInfo(null)}
          showOneToOne
          onOneToOne={() => {
            if (showPlayerInfo) startOneToOneChat(showPlayerInfo.pix)
            setShowPlayerInfo(null)
          }}
        />
      )}

      {oneToOneChat && (
        <div
          className={`majak-one-to-one-chat${layoutMode === 'desktop' ? '' : ' is-mobile'}`}
          role="dialog"
          aria-modal="true"
          aria-labelledby="majak-one-to-one-chat-title"
          style={layoutMode === 'desktop' ? undefined : {
            top: oneToOneChatViewport.top,
            left: oneToOneChatViewport.left,
            width: oneToOneChatViewport.width,
            height: oneToOneChatViewport.height,
          }}
        >
          <section className="majak-one-to-one-chat__window">
            <header>
              <div><span>{oneToOneChat.partnerOnline ? 'チャット中' : '退室しました'}</span><h2 id="majak-one-to-one-chat-title">{oneToOneChat.partnerName}</h2></div>
              <button type="button" onClick={endOneToOneChat} aria-label="閉じる">×</button>
            </header>
            <div className="majak-one-to-one-chat__messages">
              {oneToOneChat.messages.length === 0 && <p className="majak-one-to-one-chat__empty">チャットを開始しました。</p>}
              {oneToOneChat.messages.map((message, index) => <p key={`${message.sender}-${index}`} className={`${message.system ? 'is-system' : ''}${message.sender === player?.pix ? ' is-mine' : ''}`}><span>{message.text}</span></p>)}
            </div>
            <div className="majak-one-to-one-chat__input"><input value={oneToOneChatText} maxLength={80} disabled={!oneToOneChat.partnerOnline} onChange={event => setOneToOneChatText(event.currentTarget.value)} onKeyDown={event => { if (event.key === 'Enter') { event.preventDefault(); sendOneToOneChat() } }} autoFocus /><button type="button" disabled={!oneToOneChat.partnerOnline} onClick={sendOneToOneChat}>送信</button></div>
          </section>
        </div>
      )}

      {showRoomCreate && (
        <RoomCreateDlg
          initialTitle=""
          onOK={(info) => {
            setShowRoomCreate(false)
            setPendingRoomCreate(info)
            setShowOpt(true)
          }}
          onCancel={() => setShowRoomCreate(false)}
        />
      )}

      {showOpt && (
        <OptDlg
          initial={roomOpt}
          mask={buildRoomOptionMask(channelId)}
          onOK={opt => {
            setRoomOpt(opt)
            setShowOpt(false)
            if (pendingRoomCreate && pendingRoomCreateSlot != null) {
              const roomInfo = pendingRoomCreate
              const slotNo = pendingRoomCreateSlot
              setPendingRoomCreate(null)
              setPendingRoomCreateSlot(null)
              void createRoomWithOption(opt, roomInfo, slotNo)
            }
          }}
          onCancel={() => {
            setPendingRoomCreate(null)
            setPendingRoomCreateSlot(null)
            setShowOpt(false)
          }}
        />
      )}

      {shopDialogs}

      {rankingData && (
        <RankingDlg data={rankingData} memberNameByPix={memberNameByPix} onClose={() => setRankingData(null)} />
      )}

      {showCfg && (
        <CfgDlg
          initial={clientCfg}
          onOK={cfg => { saveMajakConfig(cfg); setClientCfg(cfg); setShowCfg(false) }}
          onCancel={() => setShowCfg(false)}
          onModify={configureMajakSound}
        />
      )}

      {showAccuse && (
        <AccuseDlg
          myPix={myPix}
          myMemberName={displayNameForPix(myPix)}
          speakers={accuseSpeakers}
          speakerNameById={accuseSpeakerNameById}
          chatContent={accuseChatContent}
          onOK={async payload => {
            await sendAccuseComplaint({ pix: myPix, channelId, ...payload })
            void showMessage('通報を受け付けました。', 'お知らせ')
          }}
          onClose={() => setShowAccuse(false)}
        />
      )}

      {showMission && (
        <MissionDlg
          onClose={() => setShowMission(false)}
          onMoneyUpdate={money => setGamMoney(money)}
          onGemUpdate={gem => setGemCount(gem)}
        />
      )}

      {showTournamentRegist && (
        <TournamentRegistDlg
          onOK={onTournamentRegist}
          onCancel={() => setShowTournamentRegist(false)}
        />
      )}

      {showTournamentJoin && selectedTournament && (
        <div className="majak-tournament-entry-overlay" role="presentation">
          <section className="majak-tournament-entry-dialog" role="dialog" aria-modal="true" aria-labelledby="tournament-entry-title">
            <h2 id="tournament-entry-title">トーナメント参加確認</h2>
            <dl>
              <div><dt>大会名</dt><dd>{selectedTournament.playName}</dd></div>
              <div><dt>開催日時</dt><dd>{selectedTournament.playStartDt}</dd></div>
              <div><dt>参加費</dt><dd>{selectedTournament.joinMoney.toLocaleString()} GP</dd></div>
            </dl>
            {selectedTournament.hasPassword === 1 && (
              <label>
                パスワード
                <input
                  type="password"
                  maxLength={8}
                  autoFocus
                  autoComplete="off"
                  value={tournamentJoinPassword}
                  onChange={event => setTournamentJoinPassword(event.currentTarget.value)}
                  onKeyDown={event => { if (event.key === 'Enter') void onTournamentJoinSubmit() }}
                />
              </label>
            )}
            <div className="majak-tournament-entry-actions">
              <button type="button" onClick={() => void onTournamentJoinSubmit()} disabled={tournamentActionPending}>
                {tournamentActionPending ? '送信中...' : '参加する'}
              </button>
              <button type="button" onClick={() => setShowTournamentJoin(false)} disabled={tournamentActionPending}>キャンセル</button>
            </div>
          </section>
        </div>
      )}
    </>
  )

  if (!isLobbyDataReady && !navState?.leavingRoom) {
    return (
      <div className="majak-boot-loading">
        <div className="majak-boot-loading__panel">
          <img className="majak-boot-loading__logo" src="/assets/images/common/ico_big_majak2.jpg" alt="" draggable={false} />
          <div className="majak-sync-spinner" aria-hidden="true" />
        </div>
      </div>
    )
  }

  if (layoutMode === 'mobileLandscape' && tournamentChannel) {
    const mobileTitle = channelName || 'トーナメント'
    const tournamentDetailLines = getTournamentDetailLines(selectedTournament, memberNameByPix)
    return (
      <div className="majak-mobile-screen majak-mobile-lobby-screen majak-mobile-tournament-screen">
        <section className="majak-mobile-lobby-toolbar">
          <div>
            <div className="majak-mobile-eyebrow">TOURNAMENT</div>
            <h1>{mobileTitle}</h1>
          </div>
          <div className="majak-mobile-lobby-actions">
            <button type="button" className="majak-mobile-lobby-header-button" onClick={() => setShowShop(true)}>ショップ</button>
            <button type="button" className="majak-mobile-lobby-header-button" onClick={() => setShowMission(true)}>ミッション</button>
            <button type="button" className="majak-mobile-lobby-header-button" onClick={() => setShowCustom(true)}>所持品</button>
            <button
              type="button"
              className="majak-mobile-lobby-header-button"
              onClick={tournamentPage === 'match' ? onTournamentBack : onChangeLobby}
            >
              {tournamentPage === 'match' ? '一覧に戻る' : 'ロビー変更'}
            </button>
          </div>
        </section>
        {notice && <div className="majak-mobile-lobby-notice" style={{ color: notice.color }}>{notice.text}</div>}
        {tournamentPage === 'match' ? (
          <div className="majak-mobile-tournament-match-layout">
            <section className="majak-mobile-tournament-bracket-column">
              <div className="majak-mobile-tournament-match-actions">
                <button type="button" onClick={onRefreshRoomList}>更新</button>
                <button type="button" onClick={onTournamentBack}>一覧に戻る</button>
              </div>
              <MobileTournamentMatchPanel tournament={selectedTournament} details={tournamentDetails} memberNameByPix={memberNameByPix} onWatch={onTournamentWatch} />
            </section>
            <aside className="majak-mobile-tournament-members">
              <MobileMemberListPanel
                members={displayMembers}
                selectedMember={selectedMember}
                onSelectMember={setSelectedMember}
                onViewProfile={openMemberProfile}
              />
            </aside>
          </div>
        ) : (
          <div className="majak-mobile-tournament-layout">
            <section className="majak-mobile-tournament-list" aria-label="トーナメント一覧">
              <div className="majak-mobile-tournament-list__head">
                <span>大会名</span><span>参加</span><span>日時</span>
              </div>
              <div className="majak-mobile-tournament-list__body">
                {tournamentList.length === 0 ? (
                  <p className="majak-mobile-tournament-empty">開催予定の大会はありません。</p>
                ) : tournamentList.map(entry => {
                  const selected = entry.seqNo === selectedTournamentSeqNo
                  const joined = entry.seqNo === tournamentJoinSeqNo
                  return (
                    <button
                      key={entry.seqNo}
                      type="button"
                      className={`majak-mobile-tournament-row${selected ? ' is-selected' : ''}${joined ? ' is-joined' : ''}`}
                      onClick={() => onSelectTournament(entry)}
                    >
                      <span className="majak-mobile-tournament-row__name">{entry.playName}{entry.hasPassword === 1 ? ' 鍵' : ''}</span>
                      <span>{formatTournamentStatus(entry)}</span>
                      <span>{entry.playStartDt.slice(5, 16)}</span>
                    </button>
                  )
                })}
              </div>
            </section>
            <aside className="majak-mobile-tournament-detail">
              <div className="majak-mobile-tournament-detail__content">
                {tournamentDetailLines.length === 0
                  ? <p>大会を選択してください。</p>
                  : tournamentDetailLines.map((line, index) => <div key={`${index}-${line}`}>{line || '\u00a0'}</div>)}
              </div>
              <div className="majak-mobile-tournament-actions">
                {!isTournamentJoined ? (
                  <button type="button" onClick={onTournamentJoin} disabled={!canTournamentJoin || tournamentActionPending}>参加する</button>
                ) : (
                  <button type="button" onClick={() => void onTournamentJoinCancel()} disabled={!canTournamentJoinCancel || tournamentActionPending}>参加をやめる</button>
                )}
                <button
                  type="button"
                  onClick={() => {
                    if (gamMoney < 10000) {
                      showMessage('大会を開催するには無料GPを10,000 GP以上持っている必要があります。', '確認')
                      return
                    }
                    setShowTournamentRegist(true)
                  }}
                ><span>新規</span><span>大会登録</span></button>
                <button type="button" onClick={() => void onTournamentPage()} disabled={!selectedTournament}><span>トーナメント</span><span>ページ</span></button>
              </div>
            </aside>
          </div>
        )}
        {lobbyDialogs}
      </div>
    )
  }

  if (layoutMode !== 'desktop') {
    const mobileTitle = channelName
    return (
      <div className="majak-mobile-screen majak-mobile-lobby-screen">
        <section className="majak-mobile-lobby-toolbar">
          <div>
            <div className="majak-mobile-eyebrow">LOBBY</div>
            <h1>{mobileTitle}</h1>
          </div>
          <div className="majak-mobile-lobby-actions">
            {showShopButtons && <button type="button" className="majak-mobile-lobby-header-button" onClick={() => setShowShop(true)}>ショップ</button>}
            {showMissionButton && <button type="button" className="majak-mobile-lobby-header-button" onClick={() => setShowMission(true)}>ミッション</button>}
            {showShopButtons && <button type="button" className="majak-mobile-lobby-header-button" onClick={() => setShowCustom(true)}>所持品</button>}
            <button type="button" className="majak-mobile-lobby-header-button" onClick={onChangeLobby}>ロビー変更</button>
          </div>
        </section>
        {notice && <div className="majak-mobile-lobby-notice" style={{ color: notice.color }}>{notice.text}</div>}
        <div className="majak-mobile-lobby-body">
          <RoomListPanel
            rooms={rooms}
            members={members}
            slotCount={roomSlotCount}
            channelId={channelId}
            variant="mobile"
            onEnter={onEnterRoom}
            onCreateRoom={onCreateRoom}
            directRoomActionDisabled={autoMatchingChannel}
          />
          <aside className="majak-mobile-lobby-side">
            <div className="majak-mobile-lobby-command-panel">
              {autoMatchingChannel && (
                <>
                  <button
                    type="button"
                    className={`majak-mobile-auto-match-button${isMatching ? ' is-matching' : ''}`}
                    onClick={() => { void onToggleAutoMatch() }}
                  >
                    {isMatching ? '対戦申し込みを取り消す' : '対戦申し込み'}
                  </button>
                  {isMatching && (
                    <div className="majak-mobile-auto-match-status" role="status">
                      <strong>マッチング中!</strong>
                      <span>対戦相手を探しています…</span>
                    </div>
                  )}
                </>
              )}
              <div className="majak-mobile-lobby-command-grid">
                <MobileLobbyCommandButton onClick={openRanking} hidden={!showRankingButton}>ランキング</MobileLobbyCommandButton>
                <MobileLobbyCommandButton onClick={onReqOneToOne} disabled={!selectedMember}>1:1チャット</MobileLobbyCommandButton>
                <MobileLobbyCommandButton onClick={onViewProfile} disabled={!selectedMember}>プロフィール</MobileLobbyCommandButton>
              </div>
              <div className="majak-mobile-lobby-command-checks">
                <label><input type="checkbox" checked={rejectInvite} disabled={autoMatchingChannel} onChange={event => setRejectInvite(event.currentTarget.checked)} />招待拒否</label>
                <label><input type="checkbox" checked={rejectChat} onChange={event => setRejectChat(event.currentTarget.checked)} />チャット拒</label>
              </div>
            </div>
            <MobileMemberListPanel
              members={displayMembers}
              selectedMember={selectedMember}
              onSelectMember={setSelectedMember}
              onViewProfile={openMemberProfile}
            />
          </aside>
        </div>
        {lobbyDialogs}
      </div>
    )
  }

  return (
    /* CMajakChannelWnd クライアント領域: 1014×740px (H_FRAME=1024×740, タイトル31px込み) */
    <div className="majak-desktop-lobby-screen" style={{ position: 'relative', width: 1014, height: 740 }}>

      {/* ── 背景: 通常ロビー / トーナメント一覧 / トーナメント表 ── */}
      <img
        src={`${IMG}/${tournamentChannel ? (tournamentPage === 'match' ? 'mj_tournament_match_bg.png' : 'mj_tournamentlist_bg.png') : 'mj_roomlist_bg.png'}`}
        alt=""
        draggable={false}
        style={{ position: 'absolute', left: 0, top: 0, width: 1014, height: 704 }}
      />

      {tournamentChannel ? (
        tournamentPage === 'match' ? (
        <>
          <TournamentMatchPanel tournament={selectedTournament} details={tournamentDetails} memberNameByPix={memberNameByPix} onWatch={onTournamentWatch} />
          <MemberListPanel members={displayMembers} selectedMember={selectedMember} isDani={daniChannel} onSelectMember={setSelectedMember} onViewProfile={openMemberProfile} />
        </>
        ) : (
        <>
          <TournamentListPanel
            tournaments={tournamentList}
            selectedSeqNo={selectedTournamentSeqNo}
            joinSeqNo={tournamentJoinSeqNo}
            onSelect={onSelectTournament}
          />
          <TournamentDetailPanel tournament={selectedTournament} />
        </>
        )
      ) : (
        <>
          {/* ── ルームリスト (CHgRoomListWnd) — 空きセルの「作成」ボタンをクリックでルーム作成 ── */}
          <RoomListPanel rooms={rooms} members={members} slotCount={roomSlotCount} channelId={channelId} onEnter={onEnterRoom} onCreateRoom={onCreateRoom} directRoomActionDisabled={autoMatchingChannel} />

          {/* ── メンバーリスト (CHgMemberListWnd) MoveWindow(678,212,336×403) ── */}
          <MemberListPanel members={displayMembers} selectedMember={selectedMember} isDani={daniChannel} onSelectMember={setSelectedMember} onViewProfile={openMemberProfile} />
        </>
      )}

      {/* ── 公知領域 (m_rectNotice) frame(37,576,669,588)→content(37,545,669,557) 632×12px
           m_pNoticeWnd->MoveWindow(37, 576, 632, 12) 相当 ── */}
      {!tournamentChannel && <div style={{
        position: 'absolute', left: 37 - LOBBY_LEFT_NUDGE, top: 545,
        width: 632, height: 12,
        overflow: 'hidden',
        fontFamily: 'var(--majak-font-family-ui)',
        fontSize: 'calc(10px * var(--majak-type-scale))',
        color: notice?.color ?? 'rgb(254,225,225)',
        lineHeight: '12px',
        whiteSpace: 'nowrap',
        pointerEvents: 'none',
        zIndex: 25,
        textShadow: '1px 1px 0 rgba(0,0,0,0.65)',
      }}>
        {notice?.text ?? ''}
      </div>}

      {/* ── チャットログ (HanChatWnd 相当) — スクロール可能な全履歴 ──
              AddStringToChatList 相当: 最新メッセージへ自動スクロール */}
      {(!tournamentChannel || tournamentPage === 'match') && <div style={{ position: 'absolute', left: 12 - LOBBY_LEFT_NUDGE, top: 566, width: 660, height: 133, overflow: 'hidden' }}>
        <div
          ref={chatLogRef}
          style={{
            position: 'absolute',
            left: 0,
            top: 0,
            width: 660,
            height: 113,
            overflowY: 'auto',
            overflowX: 'hidden',
            fontFamily: 'var(--majak-font-family-ui)',
            fontSize: 'calc(12px * var(--majak-type-scale))',
            color: '#000',
            background: '#fff',
            border: '2px inset #d4d0c8',
            boxSizing: 'border-box',
            pointerEvents: 'auto',
          }}
        >
          {chatLog.map(msg => {
            const isSystem = msg.name === 'System'
            return (
              <div key={msg.id} style={{ color: msg.color ?? '#000', whiteSpace: isSystem ? 'normal' : 'nowrap', overflow: isSystem ? 'visible' : 'hidden', textOverflow: isSystem ? 'clip' : 'ellipsis' }}>
                {msg.name && !isSystem ? `[${msg.name}] ` : ''}{msg.text}
              </div>
            )
          })}
        </div>

        {/* ── チャット入力 — レガシー: sideMargin=0, MoveWindow(0, height-20, width-20, 100) ── */}
        <input
          value={chatText}
          onChange={e => setChatText(e.target.value)}
          onKeyDown={onChatKeyDown}
          maxLength={80}
          style={{
            position: 'absolute',
            left: 0,
            top: 113,
            width: 640,
            height: 20,
            fontFamily: 'var(--majak-font-family-ui)',
            fontSize: 'calc(12px * var(--majak-type-scale))',
            background: '#fff',
            color: '#000',
            border: '2px inset #d4d0c8',
            boxSizing: 'border-box',
            outline: 'none',
            padding: '0 2px',
          }}
        />

        {/* HanChatWnd simple color button: MoveWindow(width-20, height-20, 19, 20), SS_SUNKEN */}
        <button
          type="button"
          aria-label="chat color"
          style={{
            position: 'absolute',
            left: 640,
            top: 113,
            width: 19,
            height: 20,
            background: '#000',
            border: '2px inset #d4d0c8',
            boxSizing: 'border-box',
            padding: 0,
          }}
        />
      </div>}

      {/* ── m_imgAtMtchBtnBk.Draw(&dc, 15, 485) → content(15,454) ──────────────
           レガシー MajakChannelWnd::OnEnter で設定:
           IS_AUTOMATCHING → mj_rm_ui_atmc_Bg00.him (654×79)  + ボタン at (38,468)
           交流戦 (通常)   → mj_rm_ui_koryu_Bg.him  (654×79)  ── */}
      {!tournamentChannel && (autoMatchingChannel ? (
        <>
          <img src={`${IMG}/mj_rm_ui_atmc_Bg00.png`} alt="" draggable={false}
            style={{ position: 'absolute', left: 15 - LOBBY_LEFT_NUDGE, top: 454, width: 654, height: 79, imageRendering: 'pixelated' }} />
          {/* mj_rm_ui_btn_at00/01.him (222×58, 4フレーム) frame(38,499)→content(38,468) */}
          <SpriteButton
            src={`${IMG}/${isMatching ? 'mj_rm_ui_btn_at01.png' : 'mj_rm_ui_btn_at00.png'}`}
            frameW={222} frameH={58} x={38 - LOBBY_LEFT_NUDGE} y={468}
            onClick={onToggleAutoMatch}
            title={isMatching ? '対局参加表明を取り消す' : '対局に参加表明する'}
          />
          {isMatching && (
            <div className="majak-desktop-auto-match-status" role="status">
              <strong>マッチング中!</strong>
              <span>対戦相手を探しています…</span>
            </div>
          )}
        </>
      ) : (
        <img src={`${IMG}/${replayChannel ? 'mj_rm_ui_paifu_Bg.png' : trainingChannel ? 'mj_rm_ui_Practice_bg.png' : 'mj_rm_ui_koryu_Bg.png'}`} alt="" draggable={false}
          style={{ position: 'absolute', left: 15 - LOBBY_LEFT_NUDGE, top: 454, width: 654, height: 79, imageRendering: 'pixelated' }} />
      ))}

      {/* ── ルームリスト更新ボタン (mj_btn_refresh.png 15×15) ── */}
      <SpriteButton
        src={`${IMG}/mj_btn_refresh.png`}
        frameW={15} frameH={15}
        x={tournamentChannel ? 653 : 654 - LOBBY_LEFT_NUDGE} y={tournamentChannel ? 107 : 5}
        onClick={onRefreshRoomList}
        title="更新"
        hidden={tournamentChannel && tournamentPage === 'match'}
      />

      {/* ── チャンネル名表示 — レガシー: CHgChannelWnd::SetStaticText(m_pGameInfo->m_szDescription)
           CMajakChannelWnd::OnPaint static RECT rc={124,49,420,65} → content y=18 ── */}
      {channelName && (
        <div style={{
          position: 'absolute', left: 124 - LOBBY_LEFT_NUDGE, top: 18, width: 296, height: 16,
          fontFamily: 'var(--majak-font-family-ui)',
          fontSize: 'calc(16px * var(--majak-type-scale))', lineHeight: '16px', fontWeight: 'bold', color: '#000',
          pointerEvents: 'none', overflow: 'hidden', whiteSpace: 'nowrap',
        }}>{channelName}</div>
      )}

      {/* ── プレイヤー情報エリア (MJDrawFrame::DrawMemberInfo 相当) ──────────────
           座標: 全て frame座標 → content座標(-31)
           X_AVATAR=686,Y_AVATAR=49→(686,18)  W_AVATAR=66,H_AVATAR=150
           X_INFOBAK=689,Y_INFOBAK=41→(689,10)
           X_PIX=865,Y_PIX=53→(865,22)  width=144
           X_MONEY=912,Y_MONEY=74→(912,43)   label x=864
           X_TITLE=912,Y_TITLE=90→(912,59)   label x=864
           X_RATING=912,Y_RATING=106→(912,75) label x=864
           X_TRICK=912,Y_TRICK=122→(912,91)   label x=864
           mj_title_base at (756,47)→(756,16) ── */}

      {/* 白背景 (FillSolidRect) */}
      <div style={{
        position: 'absolute', left: 686, top: 18,
        width: 66, height: 150,
        background: '#fff',
        pointerEvents: 'none',
      }} />

      {/* アバター (AP-08: getAvatarUrl 使用、onErrorでデフォルト画像にフォールバック)
           X_AVATAR=686,Y_AVATAR=49→content(686,18), W=66, H=150 */}
      <img
        src={getAvatarUrl(player?.avatarId)}
        alt=""
        draggable={false}
        onError={e => { (e.currentTarget as HTMLImageElement).src = getDefaultAvatarUrl(player?.sex === 'F' ? 'female' : 'male') }}
        style={{
          position: 'absolute',
          left: 686,
          top: 24,
          width: 66,
          height: 144,
          objectFit: 'contain',
          objectPosition: 'center center',
          pointerEvents: 'none',
        }}
      />

      {/* mj_title_base.png — レガシー: m_UserTitleBase は麻雀称号保有時のみ描画 */}
      {majakTitleName && (
        <img
          src={`${IMG}/mj_title_base.png`}
          alt=""
          draggable={false}
          style={{ position: 'absolute', left: 756, top: 16, width: 100, height: 52, imageRendering: 'pixelated', pointerEvents: 'none' }}
        />
      )}

      {/* Nickname (legacy member-id position) */}
      <div style={{
        position: 'absolute', left: 865, top: 22, width: 144,
        fontSize: 'calc(12px * var(--majak-type-scale))', fontFamily: 'var(--majak-font-family-ui)',
        color: 'rgb(0,114,188)', fontWeight: 'bold',
        textAlign: 'center', pointerEvents: 'none',
      }}>{player?.name ?? ''}</div>

      {/* コイン (X_MONEY=912,Y_MONEY=74) → content(912,43) */}
      <div style={{ position: 'absolute', left: 864, top: 43, fontSize: 'calc(11px * var(--majak-type-scale))', fontFamily: 'var(--majak-font-family-ui)', color: 'rgb(0,114,188)', pointerEvents: 'none' }}>GP</div>
      <div style={{ position: 'absolute', left: 912, top: 43, width: 96, fontSize: 'calc(11px * var(--majak-type-scale))', fontFamily: 'var(--majak-font-family-ui)', color: 'rgb(0,114,188)', pointerEvents: 'none' }}>{` : ${gamMoney.toLocaleString()} GP`}</div>

      {/* 資産 (X_TITLE=912,Y_TITLE=90) → content(912,59) */}
      <div style={{ position: 'absolute', left: 864, top: 59, fontSize: 'calc(11px * var(--majak-type-scale))', fontFamily: 'var(--majak-font-family-ui)', color: 'rgb(0,114,188)', pointerEvents: 'none' }}>資産</div>
      <div style={{ position: 'absolute', left: 912, top: 59, width: 96, fontSize: 'calc(11px * var(--majak-type-scale))', fontFamily: 'var(--majak-font-family-ui)', color: 'rgb(0,114,188)', pointerEvents: 'none' }}>{slevel ? ` : ${slevel}` : ' :'}</div>

      {/* 実績称号 (X_RATING=912,Y_RATING=106) → content(912,75) */}
      <div style={{ position: 'absolute', left: 864, top: 75, fontSize: 'calc(11px * var(--majak-type-scale))', fontFamily: 'var(--majak-font-family-ui)', color: 'rgb(0,114,188)', pointerEvents: 'none' }}>実績称号</div>
      <div style={{ position: 'absolute', left: 912, top: 75, width: 96, fontSize: 'calc(11px * var(--majak-type-scale))', fontFamily: 'var(--majak-font-family-ui)', color: 'rgb(0,114,188)', pointerEvents: 'none' }}>{majakTitleName ? ` : ${majakTitleName}` : ' :'}</div>

      {/* 技 (X_TRICK=912,Y_TRICK=122) → content(912,91) */}
      <div style={{ position: 'absolute', left: 864, top: 91, fontSize: 'calc(11px * var(--majak-type-scale))', fontFamily: 'var(--majak-font-family-ui)', color: 'rgb(0,114,188)', pointerEvents: 'none' }}>技</div>
      <div style={{ position: 'absolute', left: 912, top: 91, width: 96, fontSize: 'calc(11px * var(--majak-type-scale))', fontFamily: 'var(--majak-font-family-ui)', color: 'rgb(0,114,188)', pointerEvents: 'none' }}>{trickTitleName ? ` : ${trickTitleName}` : ' :'}</div>

      {/* ── アイコンボタン群 y=622 ── */}

      {/* mj_btn_shop.png (80×69) IDC_BTN_ITEMSHOP at (677, Y_BTN_ICON_OFF_1=622) */}
      <SpriteButton
        src={`${IMG}/mj_btn_shop.png`}
        frameW={80} frameH={69}
        x={(tournamentChannel && tournamentPage === 'list' ? 764 : 677) - LOBBY_LEFT_NUDGE} y={591}
        onClick={() => setShowShop(true)}
        title="ショップ"
        hidden={!showShopButtons}
      />

      {/* mj_btn_possession.png (80×69) IDC_BTN_CUSTOM at (X_BTN_ICON_OFF_1=764, 622) */}
      <SpriteButton
        src={`${IMG}/mj_btn_possession.png`}
        frameW={80} frameH={69}
        x={(tournamentChannel && tournamentPage === 'list' ? 849 : 764) - LOBBY_LEFT_NUDGE} y={591}
        onClick={() => setShowCustom(true)}
        hidden={!showShopButtons}
      />

      {/* トーナメント: IDC_BTN_PARTICIPATION / IDC_BTN_JOINSTOP / IDC_BTN_MEETINGREG / IDC_BTN_TOURNAMENT */}
      <SpriteButton
        src={`${IMG}/mj_btn_tournament_join.png`}
        frameW={80} frameH={32}
        x={677 - LOBBY_LEFT_NUDGE} y={591}
        onClick={onTournamentJoin}
        title="参加"
        hidden={!tournamentChannel || tournamentPage !== 'list' || isTournamentJoined}
        disabled={!canTournamentJoin}
      />
      <SpriteButton
        src={`${IMG}/mj_btn_tournament_joinstop.png`}
        frameW={80} frameH={32}
        x={677 - LOBBY_LEFT_NUDGE} y={591}
        onClick={onTournamentJoinCancel}
        title="取消"
        hidden={!tournamentChannel || tournamentPage !== 'list' || !isTournamentJoined}
        disabled={!canTournamentJoinCancel}
      />
      <SpriteButton
        src={`${IMG}/mj_btn_tournament_new.png`}
        frameW={80} frameH={32}
        x={677 - LOBBY_LEFT_NUDGE} y={628}
        onClick={() => {
          if (gamMoney < 10000) {
            showMessage('大会を開催するには無料GPを10,000 GP以上持っている必要があります。', '確認')
            return
          }
          setShowTournamentRegist(true)
        }}
        title="大会登録"
        hidden={!tournamentChannel || tournamentPage !== 'list'}
      />
      <SpriteButton
        src={`${IMG}/mj_btn_tournament_page.png`}
        frameW={80} frameH={32}
        x={677 - LOBBY_LEFT_NUDGE} y={665}
        onClick={onTournamentPage}
        title="大会表"
        hidden={!tournamentChannel || tournamentPage !== 'list'}
        disabled={!selectedTournament}
      />

      {/* 段位戦: m_btnRanking.ShowWindow(nShow), m_btnDailyMission.ShowWindow(SW_HIDE) */}
      <SpriteButton
        src={`${IMG}/mj_btn_ranking.png`}
        frameW={80} frameH={69}
        x={849 - LOBBY_LEFT_NUDGE} y={591}
        onClick={openRanking}
        title="ランキング"
        hidden={!showRankingButton}
      />

      {/* mj_btn_mission.png (80×69) IDC_BTN_DAILY_MISSION at (X_BTN_ICON_OFF_2=849, Y_BTN_ICON_OFF_2=622) */}
      <SpriteButton
        src={`${IMG}/mj_btn_mission.png`}
        frameW={80} frameH={69}
        x={849 - LOBBY_LEFT_NUDGE} y={591}
        onClick={() => setShowMission(true)}
        title="ミッション"
        hidden={!showMissionButton}
      />

      {/* ── アイコンボタン群 y=659 ── */}

      {/* mj_btn_chglobby.png (80×32) IDC_BTN_CHANGELOBBY at (X_BTN_ICON_OFF_3=934, Y_BTN_ICON_OFF_3=622) */}
      <SpriteButton
        src={`${IMG}/${tournamentChannel && tournamentPage === 'match' ? 'mj_btn_tournament_back.png' : 'mj_btn_chglobby.png'}`}
        frameW={80} frameH={32}
        x={934 - LOBBY_LEFT_NUDGE} y={591}
        onClick={tournamentChannel && tournamentPage === 'match' ? onTournamentBack : onChangeLobby}
        title={tournamentChannel && tournamentPage === 'match' ? '戻る' : 'ロビー変更'}
      />

      {/* ── アイコンボタン群 y=696 ── */}

      {/* mj_btn_profile.png (82×26) IDC_MEMBERLIST_MEMBERINFO at (678, Y_BTN_ICON_OFF_9=696) */}
      <SpriteButton
        src={`${IMG}/mj_btn_profile.png`}
        frameW={82} frameH={26}
        x={678 - LOBBY_LEFT_NUDGE} y={665}
        onClick={onViewProfile}
        title="プロフィール"
        hidden={tournamentChannel && tournamentPage !== 'match'}
      />

      {/* mj_btn_1on1.png (82×26) IDC_MEMBERLIST_REQONETOONE at (X_BTN_ICON_OFF_1=764, 696) */}
      <SpriteButton
        src={`${IMG}/mj_btn_1on1.png`}
        frameW={82} frameH={26}
        x={764 - LOBBY_LEFT_NUDGE} y={665}
        onClick={onReqOneToOne}
        title="1対1チャット"
        hidden={tournamentChannel && tournamentPage !== 'match'}
      />

      {/* 招待拒否 CMJChkBtn::DrawItem 再現
           check.png: 56×14 (4フレーム, 14×14/frame)
           CRect(849,696,918,711) = 69×15px
           nState=0:未チェック / nState=2:チェック済み */}
      {(!tournamentChannel || tournamentPage === 'match') && (
        <ChkBtn
          x={849 - LOBBY_LEFT_NUDGE} y={665} w={69} h={15}
          label="招待拒否"
          disabled={autoMatchingChannel}
          onToggle={(checked) => {
            /* OnRejectInvite 相当 — IDC_CK_REJECTINVITE */
            setRejectInvite(checked)
          }}
        />
      )}

      {/* チャット拒否 CMJChkBtn::DrawItem 再現
           CRect(849,711,918,726) = 69×15px */}
      {(!tournamentChannel || tournamentPage === 'match') && (
        <ChkBtn
          x={849 - LOBBY_LEFT_NUDGE} y={680} w={69} h={15}
          label="チャット拒否"
          onToggle={(checked) => {
            /* OnRejectChat 相当 — IDC_CK_REJECTCAHT */
            setRejectChat(checked)
          }}
        />
      )}

      {/* mj_btn_exit_b.png (80×32) IDC_SETTING_BTN_EXT at (X_BTN_ICON_OFF_9=934, Y_BTN_ICON_OFF_9=696) */}
      <SpriteButton
        src={`${IMG}/mj_btn_exit_b.png`}
        frameW={80} frameH={32}
        x={934 - LOBBY_LEFT_NUDGE} y={665}
        onClick={onExit}
        title="終了"
      />

      {/* ── 無料補充ボタン mj_btn_insurance2.png (138×29) at (X_BTN_CHARGE=866, Y_BTN_CHARGE=171) ── */}
      <SpriteButton
        src={`${IMG}/mj_btn_insurance2.png`}
        frameW={138} frameH={29}
        x={866} y={140}
        onClick={async () => {
          /* OnBtnInsuranceClicked 相当 — commandMoneyReplenishment (mjkc17e)
           * Key.ReplenishmentType = "mjkk42e"; 0=無料補充 */
          if (isMatching) {
            setChatLog(prev => [...prev, ...systemChatMessages(['対局参加表明中は無料補充できません。'], '#c00000')])
            return
          }
          await SignalR.send('mjkc17e', { 'mjkk42e': '0' }).catch(() => {})
        }}
        title="無料GP補充"
        hidden={!showFreeChargeButton}
      />

      {lobbyDialogs}
    </div>
  )
}

