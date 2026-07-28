/**
 * CMJRoomWnd 相当 — ルーム待機画面 (AP-09 §1-12)
 * レガシー: legacy/client/HgMajak2/MJRoomWnd1.cpp / MJRoomWnd.h
 *
 * ウィンドウ全体: 1019×704px
 * ゲーム卓エリア:  m_wndGame.Create(5, 31, this) → mj_board.png (789×704) left=5, top=31
 * サイドバー:      X_SIDEBAR=794, Y_SIDEBAR=31, W_SIDEBAR=225, H_SIDEBAR=704
 *                  背景: mj_sideBg.png (225×704)
 *
 * 定数 (MJRoomWnd.h より):
 *   HG_GAME_CHATLIST_LEFT=808  TOP=345  RIGHT=1016  BOTTOM=603
 *   HG_GAME_CHATEDIT_LEFT=809  TOP=624  RIGHT=1010  BOTTOM=640
 *   MAJAK_ROOMTITLE_LEFT=815   TOP=49   RIGHT=941   BOTTOM=63
 *   HG_GAME_STATUSWND_LEFT=808 TOP=81   RIGHT=1016  BOTTOM=211
 *   HG_GAME_NOTICE_LEFT=809    TOP=325  RIGHT=1009  BOTTOM=337
 *   X_VIEWER_WINDOW=805        Y=235    RIGHT=1010  BOTTOM=310
 */
import { useNavigate, useParams, useLocation } from 'react-router-dom'
import { useState, useEffect, useRef, type CSSProperties } from 'react'
import { flushSync } from 'react-dom'
import * as SignalR from '../../api/signalr'
import { useAuthStore } from '../../store/authStore'
import { useCustomSkinStore } from '../../store/customSkinStore'
import { isOk, showError, showMessage } from '../../utils/msgbox'
import { readNoticePayload, type NoticeDisplay } from '../../utils/notice'
import { sendAccuseComplaint } from '../../utils/accuse'
import { MAJAK_ACCUSE_EVENT } from '../../components/MajakFrame'
import CfgDlg, { loadMajakConfig, saveMajakConfig, type MJConfig } from './dialogs/CfgDlg'
import AccuseDlg from './dialogs/AccuseDlg'
import PlayerInfoWnd, { type PlayerInfo as PlayerInfoDialogData } from './dialogs/PlayerInfoWnd'
import ViewerListWnd, { type ViewerEntry } from '../ingame/ViewerListWnd'
import SlideAnnounce, { type SlideAnnounceData } from '../ingame/SlideAnnounce'
import HanRes, { type HanResPlayer } from '../ingame/HanRes'
import MiniChannelWnd from './MiniChannelWnd'
import { getDefaultAvatarUrl, getGameAvatarUrl } from '../../utils/resources'
import { configureMajakSound, playMajakChat, playMajakSid, SID_EXIT, SID_JOIN } from '../../utils/majakSound'
import { createGame, destroyGame, GAME_HEIGHT, GAME_WIDTH } from '../../game/GameInstance'
import { applyTengokuTextColor, getLegacyBoardSoundSkinId, getLegacyFullUiSkinId, getLegacyRoomPalette, isTengokuBoardSkin } from '../../utils/legacySkinPalette'
import { useOutgameLayoutMode } from '../../hooks/useOutgameLayoutMode'

const IMG = '/assets/images/game'
const CUSTOM_BOARD_DEFAULT = 100000
const CMD_USE_EMOTICON = 'mjkc24e'
const CMD_GAME_PLAY = 'playing'
const ACT_PAS = 0
const KEY_EMOTICON_ID = 'mjkk63e'
const KEY_EMOTICON_AVATAR_ID = 'mjkk64e'
const EMOTICON_COUNT = 6
const EMOTICON_FRAME_MS = 33
const EMOTICON_LEGACY_IDS = [1, 3, 5, 7, 8, 11]
const ABANDON_ROOM_STORAGE_KEY = 'majak:abandonRoomOnNextLobbyEnter'
const GAME_AUTO_CONTROL_EVENT = 'majak:auto-control'
const GAME_STATUS_EVENT = 'majak:game-status'
const GAME_FOCUS_CHAT_EVENT = 'majak:game-focus-chat'
const GAME_SYNC_EVENT = 'majak:game-sync'
const KYO_RESULT_ACTION_EVENT = 'majak:kyo-result-action'
const PAIFU_ROTATE_EVENT = 'majak:paifu-rotate'
const PAIFU_HAND_OPEN_EVENT = 'majak:paifu-hand-open'
const ROOM_ACTION_RESPONSE_TIMEOUT_MS = 7000
const DEBUG_GAME = import.meta.env.VITE_DEBUG_GAME === '1'
const MAX_ROOM_LOG_MESSAGES = 200

type GameAutoControlState = {
  prox: boolean
  autoTap: boolean
  autoPass: boolean
  autoHora: boolean
}

type ActiveEmoticon = {
  id: string
  pix: string
  type: number
  pos: number
  startedAt: number

}

type EmoticonStep = {
  frame: number
  alpha: number
}

function appendRoomLog(previous: ChatMsg[], ...messages: ChatMsg[]): ChatMsg[] {
  const next = [...previous, ...messages]
  return next.length > MAX_ROOM_LOG_MESSAGES ? next.slice(-MAX_ROOM_LOG_MESSAGES) : next
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

function buildEnterChannelPayload(channelId: string, player: ReturnType<typeof useAuthStore.getState>['player']) {
  const subId = extractSubId(channelId)
  const pix = player?.pix ?? ''
  const avatarId = player?.avatarId ?? ''
  const nickname = player?.name ?? ''
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

type CreateRoomPayloadState = {
  roomId?: number
  roomOption?: string
  maxViewer?: number
  moneyRate?: number
  minMoney?: number
  maxMoney?: number
  isPrivate?: boolean
  roomTitle?: string
  roomPassword?: string
}

function buildCreateRoomPayload(
  channelId: string,
  player: ReturnType<typeof useAuthStore.getState>['player'],
  state: CreateRoomPayloadState,
) {
  const subId = channelId.length >= 11 ? channelId.substring(6, 11) : channelId
  const pix = player?.pix ?? ''
  const roomPassword = String(state.roomPassword ?? '')
  const isPrivate = Boolean(state.isPrivate)
  const maxViewer = Number(state.maxViewer ?? 12)
  const roomTitle = String(state.roomTitle ?? '')
  const roomOption = String(state.roomOption ?? '')
  const roomId = Number(state.roomId ?? 0)
  return {
    pix,
    k3e: pix,
    roomId,
    k42e: roomId,
    roomPwd: roomPassword,
    k67e: roomPassword,
    roomTitle,
    k45e: roomTitle,
    roomOption,
    k46e: roomOption,
    roomMinCnt: 0,
    k127e: 0,
    roomLimitCnt: 4,
    k66e: 4,
    roomType: isPrivate ? 'private' : 'normal',
    isPrivate,
    k68e: isPrivate ? 'Y' : 'N',
    maxViewer,
    k69e: maxViewer,
    gameId: 'MAJAK4',
    k22e: 'MAJAK4',
    subId,
    k23e: subId,
    channelId,
    k24e: channelId,
    moneyRate: Number(state.moneyRate ?? 500),
    minMoney: Number(state.minMoney ?? 0),
    maxMoney: Number(state.maxMoney ?? 0),
  }
}

function InlineGameLoadingOverlay({ visible }: { visible: boolean }) {
  if (!visible) return null
  return (
    <div style={{ position: 'absolute', inset: 0, zIndex: 250, display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'rgba(0, 0, 0, 0.58)', pointerEvents: 'auto' }}>
      <div style={{ width: 260, height: 312, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 18, background: 'rgba(255, 255, 255, 0.94)', border: '1px solid rgba(0, 0, 0, 0.24)', boxShadow: '0 8px 24px rgba(0, 0, 0, 0.45)' }}>
        <img src="/assets/images/common/ico_big_majak2.jpg" alt="" draggable={false} style={{ width: 230, height: 230, objectFit: 'cover' }} />
        <div style={{ width: 168, height: 8, border: '1px solid #385c51', background: '#d7ded9', padding: 1 }}>
          <div style={{ width: '44%', height: '100%', background: '#21765f' }} />
        </div>
      </div>
    </div>
  )
}

const BOARD_X = 5
const BOARD_Y = 0
const ROOM_W = 1019
const ROOM_H = 704
const MOBILE_INGAME_FOCUS_W = 794
const MOBILE_INGAME_VISIBLE_H = 600
const MOBILE_INGAME_OFFSET_Y = -180
const MOBILE_HAN_RES_W = 520
const MOBILE_HAN_RES_H = 580
const MOBILE_HAN_RES_OFFSET_Y = -18
const MOBILE_TOP_RIGHT_HUD_PANEL_BOTTOM = 122
const MOBILE_INGAME_TOOL_HUD_GAP = -18

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

const MEM_POS = [
  { avt: { x: 2, y: 582 }, gls: { x: 92, y: 569 }, text: { x: 49, y: 625 }, name: { x: 2, y: 687 }, ttl: { x: 49, y: 583 }, trk: { x: 1, y: 581 } },
  { avt: { x: 742, y: 582 }, gls: { x: 678, y: 569 }, text: { x: 689, y: 625 }, name: { x: 689, y: 687 }, ttl: { x: 690, y: 583 }, trk: { x: 688, y: 581 } },
  { avt: { x: 742, y: 2 }, gls: { x: 678, y: 113 }, text: { x: 689, y: 45 }, name: { x: 689, y: 107 }, ttl: { x: 690, y: 3 }, trk: { x: 688, y: 1 } },
  { avt: { x: 2, y: 2 }, gls: { x: 92, y: 113 }, text: { x: 49, y: 45 }, name: { x: 2, y: 107 }, ttl: { x: 49, y: 3 }, trk: { x: 1, y: 1 } },
] as const

const KEY_COUNT = 'k25e'
const KEY_TITLE_TYPE = 'mjkk48e'
const KEY_TITLE_CODE = 'mjkk49e'
const KEY_TITLE_NAME = 'mjkk50e'
const KEY_GEM_GAME = 'mjkk56e'
const LEGACY_PROXY_GUIDE = [
  '接続が切れた人は以後すべてツモ切りとなります。',
  '再接続すると対局中に復帰できます。',
  '成績は対局終了後の成績がそのまま反映されます。',
]
const LEGACY_TOURNAMENT_LINEOFF_GUIDE = [
  '接続が切れた人は以後すべてツモ切りとなります。',
  'トーナメント戦の落ち戻りはトーナメントロビーへお越しください。',
]
const LEGACY_CANCEL_GUIDE_DANI = '段位戦ではチャットは利用できません。'
const LEGACY_CANCEL_GUIDE_STOP = 'ポン・チー・ミンカン・ロンができるときはゲー ムの進行がしばらく停止します。'
const LEGACY_CANCEL_GUIDE_PASS = '見送りはマウスの右クリック、「パス」ボタン、[Enter]キーです。'
const LEGACY_GEM_GAME_STATUS = [
  '',
  'この対戦は龍珠獲得戦になります。',
  'この対戦は龍珠獲得戦BIGになります。',
]

const LEGACY_STATUS_GUIDE: ChatMsg[] = []

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
        padding: 0,
        outline: 'none',
        backgroundColor: 'transparent',
        backgroundImage: `url(${IMG}/emobt${String(index).padStart(2, '0')}.png)`,
        backgroundPosition: `${-16 * frame}px 0`,
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
  const pos = MEM_POS[item.pos] ?? MEM_POS[0]
  const avatarPos = roomPoint(pos.avt)
  const dir = item.pos === 1 || item.pos === 2 ? 'l' : 'r'
  return (
    <div
      style={{
        position: 'absolute',
        left: Math.max(0, Math.min(ROOM_W - 96, avatarPos.left + (dir === 'r' ? 36 : -86))),
        top: Math.max(0, Math.min(ROOM_H - 96, avatarPos.top - 8)),
        width: 96,
        height: 96,
        opacity: step.alpha / 255,
        backgroundImage: `url(${IMG}/emo_${dir}_${String(item.type).padStart(2, '0')}.png)`,
        backgroundPosition: `${-96 * step.frame}px 0`,
        backgroundRepeat: 'no-repeat',
        imageRendering: 'pixelated',
        pointerEvents: 'none',
      }}
    />
  )
}

function readRoomPlayer(data: Record<string, unknown>): PlayerInfoType | null {
  const playerId = String(data.k3e ?? data.pix ?? data['member' + 'Id'] ?? data.playerId ?? '')
  if (!playerId) return null
  const pos = Number(data.k58e ?? data.playerPos ?? data.seatPos ?? data.pos ?? -1)
  if (pos < 0 || pos > 3) return null
  const readyValue = data.ready ?? data.isReady ?? data.okButton ?? data.ok
  return {
    playerId,
    name: String(data.mjkk34e ?? data.k8e ?? data.nickName ?? data.nickname ?? data.name ?? playerId),
    rating: Number(data.k31e ?? data.rating ?? 0),
    pos: pos as 0 | 1 | 2 | 3,
    ready: readyValue === true || readyValue === 1 || readyValue === '1' || readyValue === 'true',
    isHost: Boolean(data.isHost ?? false),
    avatarId: data.k7e != null || data.avatarId != null ? String(data.k7e ?? data.avatarId) : undefined,
    sex: data.k11e != null || data.sex != null ? String(data.k11e ?? data.sex) : undefined,
    slevel: String(data.k32e ?? data.slevel ?? data.dan ?? ''),
    nlevel: Number(data.k33e ?? data.nlevel ?? 0),
    isProxy: Boolean(data.isProxy ?? data.proxy ?? false),
    skillCnt: Number(data.skillCnt ?? data.skillCount ?? 0),
    majakTitle: readTitleCode(data.mjkk47e ?? data.majakTitle),
    trickTitle: readTitleCode(data.mjkk46e ?? data.trickTitle),
  }
}

function getMajakTitleSrc(code?: number) {
  if (!code) return ''
  const prefix = code < 1000 ? 'mj_title' : 'mj_ctitle'
  const value = code < 1000 ? code : code - 1000
  return `${IMG}/${prefix}_${String(value).padStart(3, '0')}.png`
}

function getTrickTitleSrc(code?: number) {
  if (!code) return ''
  return `${IMG}/mj_skill_${String(code).padStart(3, '0')}.png`
}

function readOptionDigit(option: string | undefined, index: number, fallback: number) {
  const char = option?.charAt(index) ?? ''
  return /^\d$/.test(char) ? Number(char) : fallback
}

function extractSubId(channelId?: string) {
  const id = channelId ?? ''
  return id.length >= 11 ? id.substring(6, 11) : id
}

function asNumber(value: unknown, fallback = 0): number {
  const n = Number(value)
  return Number.isFinite(n) ? n : fallback
}

function asLegacyBool(value: unknown): boolean {
  return value === true || value === 1 || value === '1' || value === 'true' || value === 'v1e'
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

function readHanResPlayers(data: Record<string, unknown>, myPix: string): HanResPlayer[] {
  const users = Array.isArray(data.users)
    ? data.users as Array<Record<string, unknown>>
    : []
  return users.map((user, index) => ({
    pix: String(user.pix ?? user.k3e ?? user['member' + 'Id'] ?? ''),
    name: String(user.name ?? ''),
    avatarId: String(user.avatarId ?? ''),
    sex: String(user.sex ?? ''),
    charaId: asNumber(user.charaId ?? user.customCostume, 0),
    seatPos: (asNumber(user.seatPos, index) % 4) as 0 | 1 | 2 | 3,
    rank: (asNumber(user.rank ?? (asNumber(user.ranking, index + 1) - 1), index) % 4) as 0 | 1 | 2 | 3,
    point: asNumber(user.point, 0),
    setBal: asNumber(user.setBal, 0),
    setTen: asNumber(user.setTen, 0),
    setUma: asNumber(user.setUma, 0),
    setTor: user.setTor !== undefined ? asNumber(user.setTor, 0) : undefined,
    setTip: user.setTip !== undefined ? asNumber(user.setTip, 0) : undefined,
    coinGain: asNumber(user.coinGain, 0),
    coinNeed: asNumber(user.coinNeed, 0),
    prevNlevel: user.prevNlevel !== undefined ? asNumber(user.prevNlevel, 0) : undefined,
    nlevel: user.nlevel !== undefined ? asNumber(user.nlevel, 0) : undefined,
    levelName: String(user.slevel ?? ''),
    isMe: String(user.pix ?? user.k3e ?? user['member' + 'Id'] ?? '') === myPix,
  }))
}

function gemGameStatusText(value: unknown): string | null {
  const gemGame = asNumber(value, 0)
  return LEGACY_GEM_GAME_STATUS[gemGame] ?? null
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

function isDaniChannel(channelId?: string) {
  return extractSubId(channelId)[2] === 'G'
}

function isReplayChannel(channelId?: string) {
  return extractSubId(channelId)[2] === 'V'
}

function isTrainingChannel(channelId?: string) {
  return extractSubId(channelId)[2] === 'T'
}
function isAutoMatchingChannel(channelId?: string) {
  return extractSubId(channelId)[1] === 'Z'
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

function roomPoint(pos: { x: number; y: number }) {
  return { left: BOARD_X + pos.x, top: BOARD_Y + pos.y }
}

function defaultSkinSrc(src: string): string {
  return src.replace(/^(.*)\/skin\/\d+\/([^/]+)_\d+\.png$/i, '$1/$2.png')
}

function seatToLegacyLoc(seatPos: number, viewSeatPos: number | undefined): 0 | 1 | 2 | 3 {
  const view = viewSeatPos != null && viewSeatPos >= 0 && viewSeatPos <= 3 ? viewSeatPos : 0
  return ((4 + seatPos - view) % 4) as 0 | 1 | 2 | 3
}

/** ====================================================================
 * CMJBmpButton 相当 — AP-06 §2 4フレームスプライトボタン
 * ==================================================================== */
function SpriteButton({
  src,
  fallbackSrc,
  frameW,
  frameH,
  x,
  y,
  onClick,
  title,
  hidden,
  disabled,
  checked,
}: {
  src: string
  fallbackSrc?: string
  frameW: number
  frameH: number
  x: number
  y: number
  onClick: () => void
  title?: string
  hidden?: boolean
  disabled?: boolean
  checked?: boolean
}) {
  const [frameIdx, setFrameIdx] = useState(0)
  if (hidden) return null
  const spriteFrame = disabled ? 1 : checked ? 3 : frameIdx
  const derivedFallbackSrc = fallbackSrc ?? src.replace(/^(.*)\/skin\/\d+\/([^/]+)_\d+\.png$/i, '$1/$2.png')
  const backgroundImage = derivedFallbackSrc !== src ? `url(${src}), url(${derivedFallbackSrc})` : `url(${src})`
  return (
    <button
      title={title}
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
        width: frameW,
        height: frameH,
        display: 'block',
        appearance: 'none',
        WebkitAppearance: 'none',
        backgroundImage,
        backgroundPosition: `${-spriteFrame * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        backgroundColor: 'transparent',
        border: 'none',
        padding: 0,
        margin: 0,
        cursor: disabled ? 'default' : 'pointer',
        outline: 'none',
        opacity: 1,
        imageRendering: 'pixelated',
        pointerEvents: hidden ? 'none' : 'auto',
      }}
    />
  )
}

/** ====================================================================
 * プレイヤー情報 (CHgPlayerInfo 相当) — 将来 PlayerList 受信時に使用
 * ==================================================================== */
export type PlayerInfoType = {
  playerId: string
  name: string
  rating: number
  pos: 0 | 1 | 2 | 3  // 0-3: player seat
  ready: boolean
  isHost: boolean
  avatarId?: string
  sex?: string
  slevel?: string
  nlevel?: number
  isProxy?: boolean
  skillCnt?: number
  majakTitle?: number
  trickTitle?: number
}

type ChannelMemberEntry = {
  pix: string
  name: string
  rating: number
  avatarId?: string
  sex?: string
  slevel?: string
  location?: string
  roomId?: number
}

/** チャットメッセージ型
 *  レガシー MJRoomWnd1.cpp:830-865 (ProcessCommonChatService) より:
 *    送信者種別ごとに 1行全体を 1色 で描画する。
 *    self    : MAJAK_RGB_CHATSELF   = RGB(  0, 48, 96) = #003060
 *    player  : MAJAK_RGB_CHATOTHER  = RGB(  0,  0,  0) = #000000
 *    viewer  : MAJAK_RGB_CHATVIEWER = RGB( 96, 96, 96) = #606060
 *    abuse   : MAJAK_RGB_CHATABUSE  = RGB(255,  0,  0) = #ff0000
 *  行フォーマット (MJRoomWnd1.cpp:832): "{pix} : {string}"
 */
interface ChatMsg {
  id: string
  /** 表示名。内部 ID は pix に保持する。 */
  name: string
  pix?: string
  text: string
  /** 表示色。チャット受信時に送信者種別を見て決定する。 */
  color?: string
  bold?: boolean
}

/** ====================================================================
 * CMJRoomWnd 本体
 * ==================================================================== */
export default function RoomScreen() {
  const { channelId, roomId } = useParams<{   /* lobbyId は現在未使用 */
    channelId: string
    roomId: string
  }>()
  const navigate  = useNavigate()
  const location  = useLocation()
  const layoutMode = useOutgameLayoutMode()
  const isMobileIngame = layoutMode === 'mobileLandscape'
  const ingameLayoutMode = isMobileIngame ? 'mobileLandscape' : 'desktop'

  /** AP-04 §8: ナビゲーション state からサーバー URL とモードを取得
   *   mode='create' : ルーム作成モード → send('c8e', ...)
   *   mode='enter'  : 既存ルーム入室 (default) → send('c14e', ...)
   *   mode='view'   : 満員/対局中ルーム観戦 → send('c18e', ...)
   */
  type RoomLocationState = {
    serverUrl?:   string
    mode?:        'create' | 'enter' | 'view' | 'auto'
    roomOption?:  string
    maxViewer?:   number
    moneyRate?:   number
    minMoney?:    number
    maxMoney?:    number
    isPrivate?:   boolean
    roomTitle?:   string
    roomPassword?: string
    customBgId?: number
    customHaiId?: number
    customBoardType?: number
    autoEnterPayload?: Record<string, unknown>
    resumePlaying?: boolean
    skipEnterChannel?: boolean
    tournamentViewPayload?: Record<string, unknown>
  }
  const locState = (location.state ?? {}) as RoomLocationState
  const serverUrl  = locState.serverUrl ?? ''
  const createMode = locState.mode === 'create'

  const [chatLog,   setChatLog]   = useState<ChatMsg[]>([])
  const [chatText,  setChatText]  = useState('')
  const [roomTitle, setRoomTitle] = useState(locState.roomTitle ?? '')
  const [currentRoomOption, setCurrentRoomOption] = useState(locState.roomOption ?? '')
  const [statusLog, setStatusLog] = useState<ChatMsg[]>(LEGACY_STATUS_GUIDE)
  const [notice,    setNotice]    = useState<NoticeDisplay | null>(null)
  const [viewers,   setViewers]   = useState<ViewerEntry[]>([])
  const [channelMembers, setChannelMembers] = useState<ChannelMemberEntry[]>([])
  const [showInviteList, setShowInviteList] = useState(false)
  const pendingInviteTargetRef = useRef<string | null>(null)
  const [selectedViewer, setSelectedViewer] = useState<PlayerInfoDialogData | null>(null)
  const [isReady,   setIsReady]   = useState(false)
  const [players,   setPlayers]   = useState<PlayerInfoType[]>([])
  const [activeEmoticons, setActiveEmoticons] = useState<ActiveEmoticon[]>([])
  const [emoticonNow, setEmoticonNow] = useState(0)
  const [showCfg,    setShowCfg]   = useState(false)
  const [showAccuse, setShowAccuse] = useState(false)
  const [roomCfg,    setRoomCfg]   = useState<MJConfig>(() => loadMajakConfig())
  const fallbackSkin = useCustomSkinStore()
  const routeCustomBoardId = Number(locState.customBgId ?? 0)
  const routeCustomHaiId = Number(locState.customHaiId ?? 0)
  const routeCustomBoardType = Number(locState.customBoardType ?? 0)
  const customBoardId = routeCustomBoardId > 0 ? routeCustomBoardId : fallbackSkin.bgId
  const customHaiId = routeCustomHaiId > 0 ? routeCustomHaiId : fallbackSkin.haiId
  const customBoardType = routeCustomBoardType > 0 ? routeCustomBoardType : fallbackSkin.bgType
  const customBoardSuffix = String(customBoardId).padStart(2, '0')
  const fullUiSkinId = getLegacyFullUiSkinId(customBoardId, customBoardType)
  const fullUiSkinSuffix = String(fullUiSkinId ?? customBoardId).padStart(2, '0')
  const hasFullCustomBoardSkin = fullUiSkinId != null
  const tengokuBoardSkin = isTengokuBoardSkin(customBoardId, customBoardType)
  const legacyPalette = getLegacyRoomPalette(tengokuBoardSkin)
  const boardSoundSkinId = getLegacyBoardSoundSkinId(customBoardId, customBoardType)
  const bgSkinSrc = (skinKey: string, baseKey = skinKey) => hasFullCustomBoardSkin
    ? `${IMG}/skin/${fullUiSkinId}/${skinKey}_${fullUiSkinSuffix}.png`
    : `${IMG}/${baseKey}.png`
  const bgSkinFallbackSrc = (_skinKey: string, baseKey = _skinKey) => hasFullCustomBoardSkin ? `${IMG}/${baseKey}.png` : undefined
  const customBoardSrc = customBoardId > 0 && customBoardId !== CUSTOM_BOARD_DEFAULT
    ? `${IMG}/skin/${customBoardId}/mj_board_${customBoardSuffix}.png`
    : `${IMG}/mj_board.png`
  const promptSrc = {
    waitEntry: bgSkinSrc('mj_promptWaitEntry'),
    pushReady: bgSkinSrc('mj_promptPushReady'),
    waitReady: bgSkinSrc('mj_promptWaitReady'),
    pushStart: bgSkinSrc('mj_promptPushStart'),
    waitStart: bgSkinSrc('mj_promptWaitStart'),
  }
  const [announceData, setAnnounceData] = useState<SlideAnnounceData | null>(null)
  const [inlineGame, setInlineGame] = useState(false)
  const [inlineGameLoading, setInlineGameLoading] = useState(false)
  const [hanResData, setHanResData] = useState<HanResPlayer[] | null>(null)
  const [hanResFlags, setHanResFlags] = useState({ hasTor: false, hasTip: false, isViewer: false, isTournament: false })
  const [roomActionPending, setRoomActionPending] = useState(true)
  const [autoControl, setAutoControl] = useState<GameAutoControlState>({ prox: false, autoTap: false, autoPass: false, autoHora: false })
  const [viewerHandHidden, setViewerHandHidden] = useState(false)
  const [hasChanceItem, setHasChanceItem] = useState(false)
  const [chanceReserved, setChanceReserved] = useState(false)
  /** 自分が方長かどうか (ChkHost 相当) */
  const [_amHost, setAmHost] = useState(false)
  const replayChannel = isReplayChannel(channelId)
  const trainingChannel = isTrainingChannel(channelId)
  const autoMatchingChannel = isAutoMatchingChannel(channelId)
  const chatEnabled = replayChannel || (!isDaniChannel(channelId) && readOptionDigit(currentRoomOption, 14, 0) !== 0)
  const viewerChatVisible = readOptionDigit(currentRoomOption, 7, 0) !== 0
  const viewerHandOpenEnabled = readOptionDigit(currentRoomOption, 6, 0) !== 0
  const chatRulesRef = useRef({ replayChannel, chatEnabled, viewerChatVisible })

  const chatLogRef   = useRef<HTMLDivElement>(null)
  const chatInputRef = useRef<HTMLInputElement>(null)
  const lastAccuseOpenedAtRef = useRef(0)
  useEffect(() => { configureMajakSound(roomCfg) }, [roomCfg])
  const statusLogRef = useRef<HTMLDivElement>(null)
  const inlineGameRef = useRef<HTMLDivElement>(null)
  const mobileIngameShellRef = useRef<HTMLDivElement>(null)
  const [mobileIngameScale, setMobileIngameScale] = useState(1)
  const [mobileHanResScale, setMobileHanResScale] = useState(1)
  const [mobileIngameOffsetX, setMobileIngameOffsetX] = useState(0)
  const [mobileIngameOffsetY, setMobileIngameOffsetY] = useState(0)
  const [mobileIngameChatOpen, setMobileIngameChatOpen] = useState(false)
  const [mobileIngameToolOpen, setMobileIngameToolOpen] = useState(false)
  const inlineGameActiveRef = useRef(false)
  const inlineGameEndedRef = useRef(false)
  const inlineGameLoadingVisibleRef = useRef(false)
  const inlineGameLoadingStartedAtRef = useRef(0)
  const inlineGameLoadingOffTimerRef = useRef<number | null>(null)
  const roomActionPendingTimerRef = useRef<number | null>(null)
  const viewersRef = useRef<ViewerEntry[]>([])
  const messageSeqRef = useRef(0)
  const gameNavigatedRef = useRef(false)
  const roomActionSentKeyRef = useRef('')
  const exitingRoomRef = useRef(false)
  const roomJoinMessageMembersRef = useRef(new Set<string>())
  const proxyGuideShownRef = useRef(false)
  const roomEntryGuideShownRef = useRef(false)
  const gemGameGuideShownRef = useRef(false)
  const emoticonSeqRef = useRef(0)
  const logRejoinProbe = (eventName: string, details: Record<string, unknown> = {}) => {
    if (!DEBUG_GAME) return
    console.info('[RoomScreen/RejoinProbe]', eventName, {
      channelId,
      roomId,
      mode: locState.mode ?? 'enter',
      resumePlaying: Boolean(locState.resumePlaying),
      inlineGame: inlineGameActiveRef.current,
      inlineGameLoading: inlineGameLoadingVisibleRef.current,
      roomActionSentKey: roomActionSentKeyRef.current,
      pix: useAuthStore.getState().player?.pix ?? '',
      ...details,
    })
  }
  const getDocumentNavigationType = () => {
    const entry = performance.getEntriesByType('navigation')[0] as PerformanceNavigationTiming | undefined
    return entry?.type ?? ''
  }
  const clearRoomActionPending = (reason: string) => {
    if (roomActionPendingTimerRef.current !== null) {
      window.clearTimeout(roomActionPendingTimerRef.current)
      roomActionPendingTimerRef.current = null
    }
    logRejoinProbe('room action pending cleared', { reason })
    setRoomActionPending(false)
  }
  const finishInlineGame = (reason: string) => {
    inlineGameEndedRef.current = true
    gameNavigatedRef.current = false
    inlineGameLoadingVisibleRef.current = false
    if (inlineGameLoadingOffTimerRef.current !== null) {
      window.clearTimeout(inlineGameLoadingOffTimerRef.current)
      inlineGameLoadingOffTimerRef.current = null
    }
    if (roomActionPendingTimerRef.current !== null) {
      window.clearTimeout(roomActionPendingTimerRef.current)
      roomActionPendingTimerRef.current = null
    }
    logRejoinProbe('inline game ended', { reason })
    destroyGame()
    setInlineGameLoading(false)
    setInlineGame(false)
    setRoomActionPending(false)
    setIsReady(false)
    setPlayers(prev => prev.map(player => ({ ...player, ready: false })))
    setAutoControl({ prox: false, autoTap: false, autoPass: false, autoHora: false })
    setMobileIngameChatOpen(false)
    setMobileIngameToolOpen(false)
  }
  const startRoomActionPending = (command: string, details: Record<string, unknown> = {}) => {
    if (roomActionPendingTimerRef.current !== null) window.clearTimeout(roomActionPendingTimerRef.current)
    logRejoinProbe('room action pending started', { command, timeoutMs: ROOM_ACTION_RESPONSE_TIMEOUT_MS, ...details })
    setRoomActionPending(true)
    roomActionPendingTimerRef.current = window.setTimeout(() => {
      roomActionPendingTimerRef.current = null
      logRejoinProbe('room action response timeout; returning to lobby', { command, timeoutMs: ROOM_ACTION_RESPONSE_TIMEOUT_MS, ...details })
      setRoomActionPending(false)
      navigate(`/channel/${channelId ?? ''}/lobby`, { replace: true })
    }, ROOM_ACTION_RESPONSE_TIMEOUT_MS)
  }
  useEffect(() => {
    inlineGameActiveRef.current = inlineGame
    logRejoinProbe('inlineGame state changed', { inlineGame })
  }, [inlineGame])
  useEffect(() => {
    if (!inlineGame) {
      inlineGameLoadingVisibleRef.current = false
      setInlineGameLoading(false)
      return
    }
    inlineGameLoadingStartedAtRef.current = performance.now()
    inlineGameLoadingVisibleRef.current = true
    setInlineGameLoading(true)
  }, [inlineGame])
  useEffect(() => {
    const setInlineGameLoadingVisible = (visible: boolean) => {
      if (inlineGameLoadingOffTimerRef.current !== null) {
        window.clearTimeout(inlineGameLoadingOffTimerRef.current)
        inlineGameLoadingOffTimerRef.current = null
      }
      if (visible) {
        if (inlineGameLoadingVisibleRef.current) return
        inlineGameLoadingStartedAtRef.current = performance.now()
        inlineGameLoadingVisibleRef.current = true
        setInlineGameLoading(true)
        return
      }
      const elapsed = performance.now() - inlineGameLoadingStartedAtRef.current
      const delay = Math.max(0, 600 - elapsed)
      inlineGameLoadingOffTimerRef.current = window.setTimeout(() => {
        inlineGameLoadingVisibleRef.current = false
        setInlineGameLoading(false)
        inlineGameLoadingOffTimerRef.current = null
      }, delay)
    }
    const onSync = (event: Event) => {
      const detail = (event as CustomEvent<{ active?: boolean; reason?: string }>).detail ?? {}
      const active = Boolean(detail.active)
      logRejoinProbe('GAME_SYNC_EVENT', { active, reason: detail.reason ?? '' })
      if (active) {
        flushSync(() => setInlineGameLoadingVisible(true))
        return
      }
      setInlineGameLoadingVisible(false)
    }
    window.addEventListener(GAME_SYNC_EVENT, onSync)
    return () => {
      window.removeEventListener(GAME_SYNC_EVENT, onSync)
      if (inlineGameLoadingOffTimerRef.current !== null) window.clearTimeout(inlineGameLoadingOffTimerRef.current)
    }
  }, [])
  useEffect(() => { viewersRef.current = viewers }, [viewers])
  useEffect(() => {
    if (!mobileIngameChatOpen) return
    window.requestAnimationFrame(() => {
      if (chatLogRef.current) chatLogRef.current.scrollTop = chatLogRef.current.scrollHeight
      if (statusLogRef.current) statusLogRef.current.scrollTop = statusLogRef.current.scrollHeight
    })
  }, [mobileIngameChatOpen])
  useEffect(() => {
    if (!isMobileIngame) {
      setMobileIngameScale(1)
      setMobileHanResScale(1)
      setMobileIngameOffsetX(0)
      setMobileIngameOffsetY(0)
      return
    }
    const update = () => {
      const rect = mobileIngameShellRef.current?.getBoundingClientRect()
      if (!rect) {
        const viewportHanResScale = Math.min(1, window.innerWidth / MOBILE_HAN_RES_W, window.innerHeight / MOBILE_HAN_RES_H)
        setMobileHanResScale(Number.isFinite(viewportHanResScale) && viewportHanResScale > 0 ? viewportHanResScale : 1)
        setMobileIngameScale(1)
        setMobileIngameOffsetX(0)
        setMobileIngameOffsetY(0)
        return
      }
      const focusWidth = inlineGame ? MOBILE_INGAME_FOCUS_W : ROOM_W
      const availableHeight = rect.height
      const focusHeight = inlineGame ? MOBILE_INGAME_VISIBLE_H : ROOM_H
      const nextScale = inlineGame ? rect.width / focusWidth : Math.min(rect.width / focusWidth, availableHeight / focusHeight)
      const desiredHanResScale = Math.min(1, rect.width / MOBILE_HAN_RES_W, rect.height / MOBILE_HAN_RES_H)
      const nextHanResScale = inlineGame && Number.isFinite(nextScale) && nextScale > 0
        ? desiredHanResScale / nextScale
        : desiredHanResScale
      const nextOffsetX = 0
      const nextOffsetY = inlineGame ? MOBILE_INGAME_OFFSET_Y : Math.min(0, availableHeight - ROOM_H * nextScale)
      setMobileIngameScale(Number.isFinite(nextScale) && nextScale > 0 ? nextScale : 1)
      setMobileHanResScale(Number.isFinite(nextHanResScale) && nextHanResScale > 0 ? nextHanResScale : 1)
      setMobileIngameOffsetX(Number.isFinite(nextOffsetX) ? nextOffsetX : 0)
      setMobileIngameOffsetY(Number.isFinite(nextOffsetY) ? nextOffsetY : 0)
    }
    update()
    const observer = new ResizeObserver(update)
    if (mobileIngameShellRef.current) observer.observe(mobileIngameShellRef.current)
    window.addEventListener('resize', update)
    return () => {
      observer.disconnect()
      window.removeEventListener('resize', update)
    }
  }, [inlineGame, isMobileIngame])
  /** \u73fe\u5728\u306e players \u3092 SignalR \u30cf\u30f3\u30c9\u30e9\u304b\u3089\u53c2\u7167\u3059\u308b\u305f\u3081\u306e ref
   *  (useEffect \u4f9d\u5b58\u914d\u5217\u306b players \u3092\u5165\u308c\u308b\u3068\u30b3\u30fc\u30eb\u30d0\u30c3\u30af\u3092\u6bce\u56de\u518d\u767b\u9332\u3057\u3066\u3057\u307e\u3046\u305f\u3081)\n   */
  const playersRef = useRef<PlayerInfoType[]>([])
  useEffect(() => { playersRef.current = players }, [players])
  useEffect(() => {
    chatRulesRef.current = { replayChannel, chatEnabled, viewerChatVisible }
  }, [replayChannel, chatEnabled, viewerChatVisible])

  useEffect(() => {
    if (!inlineGame || !inlineGameRef.current) return
    const myPix = useAuthStore.getState().player?.pix ?? ''
    const me = playersRef.current.find(p => p.playerId === myPix)
    logRejoinProbe('createGame', {
      myOdr: me?.pos,
      isViewer: locState.mode === 'view',
      playersCount: playersRef.current.length,
      roomOption: currentRoomOption,
      layoutMode: ingameLayoutMode,
      skipInitialRoomEnter: true,
    })
    createGame(inlineGameRef.current, {
      mode: 'game',
      layoutMode: ingameLayoutMode,
      roomId: roomId ?? '',
      myOdr: me?.pos,
      isViewer: locState.mode === 'view',
      players: playersRef.current as unknown as Array<Record<string, unknown>>,
      roomOption: currentRoomOption,
      inputConfig: { nSelPasKey: roomCfg.nSelPasKey },
      customBgId: customBoardId,
      customBoardType,
      customHaiId,
      skipInitialRoomEnter: true,
    })
    return () => destroyGame()
  }, [inlineGame, roomId, customBoardId, customHaiId, ingameLayoutMode])

  const nextMessageId = () => `${Date.now()}-${messageSeqRef.current++}`

  const putRoomMessage = (text: string, color: string, bold = false) => {
    setChatLog(prev => appendRoomLog(prev, { id: nextMessageId(), name: '', text, color, bold }))
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

  const putRoomJoinMessage = (pix: string, player = true, displayName?: string) => {
    if (!pix || roomJoinMessageMembersRef.current.has(pix)) return
    roomJoinMessageMembersRef.current.add(pix)
    putRoomMessage(`${displayName || displayNameForPix(pix)}様が入室しました。`, legacyPalette.roomJoin, player)
  }

  const putRoomEntryGuideMessages = () => {
    if (roomEntryGuideShownRef.current) return
    roomEntryGuideShownRef.current = true
    setChatLog(prev => appendRoomLog(prev,
      ...(isDaniChannel(channelId) ? [{ id: nextMessageId(), name: '', text: LEGACY_CANCEL_GUIDE_DANI, color: legacyPalette.notice, bold: true }] : []),
      { id: nextMessageId(), name: '', text: LEGACY_CANCEL_GUIDE_STOP, color: legacyPalette.notice },
      { id: nextMessageId(), name: '', text: LEGACY_CANCEL_GUIDE_PASS, color: legacyPalette.normal, bold: true },
    ))
  }

  useEffect(() => {
    const onGameStatus = (event: Event) => {
      const detail = (event as CustomEvent<{ text?: string; color?: string; bold?: boolean }>).detail ?? {}
      const text = String(detail.text ?? '')
      if (text) setStatusLog(prev => appendRoomLog(prev, { id: nextMessageId(), name: '', text, color: applyTengokuTextColor(detail.color, tengokuBoardSkin), bold: Boolean(detail.bold) }))
    }
    window.addEventListener(GAME_STATUS_EVENT, onGameStatus)
    return () => window.removeEventListener(GAME_STATUS_EVENT, onGameStatus)
  }, [])

  const putProxyGuideStatus = () => {
    if (proxyGuideShownRef.current) return
    proxyGuideShownRef.current = true
    const guide = extractSubId(channelId)[2] === 'H' ? LEGACY_TOURNAMENT_LINEOFF_GUIDE : LEGACY_PROXY_GUIDE
    setStatusLog(prev => appendRoomLog(prev,
      ...guide.map(line => ({ id: nextMessageId(), name: '', text: line, color: legacyPalette.normal, bold: true })),
    ))
  }

  const putGemGameStatus = (value: unknown) => {
    if (gemGameGuideShownRef.current) return
    const text = gemGameStatusText(value)
    if (!text) return
    gemGameGuideShownRef.current = true
    setStatusLog(prev => appendRoomLog(prev, { id: nextMessageId(), name: '', text, color: legacyPalette.normal, bold: true }))
  }

  const exitRoomToLobby = async (seatPos: number | undefined, isViewer = false) => {
    if (exitingRoomRef.current) return
    exitingRoomRef.current = true
    void SignalR.send('c9e', buildExitRoomPayload(useAuthStore.getState().player, seatPos, isViewer)).catch(() => {
      window.sessionStorage.setItem(ABANDON_ROOM_STORAGE_KEY, JSON.stringify({
        channelId: channelId ?? '',
        roomId: Number(roomId ?? 0),
      }))
    })
    navigate(`/channel/${channelId ?? ''}/lobby`, { state: { leavingRoom: true } })
  }

  /** SignalR 接続 & ルーム参加 */
  useEffect(() => {
    let mounted = true

    const absorbAlreadyInRoom = (data: Record<string, unknown>) => {
      const message = String(data.k2e ?? data.message ?? '')
      const responseRoomId = Number(data.k42e ?? data.roomId ?? roomId ?? 0)
      const currentRoomId = Number(roomId ?? 0)
      if (!message.includes('既に') || !message.includes('入室')) return false
      if (currentRoomId > 0 && responseRoomId > 0 && responseRoomId !== currentRoomId) return false
      logRejoinProbe('absorb already-in-room', { responseRoomId, message, data })
      if (DEBUG_GAME) console.info('[RoomScreen] already in current room; keeping room screen', { roomId, responseRoomId, message, data })
      void SignalR.send('c16e', {}).catch(() => {})
      if (locState.resumePlaying) {
        flushSync(() => {
          inlineGameLoadingStartedAtRef.current = performance.now()
          inlineGameLoadingVisibleRef.current = true
          setInlineGameLoading(true)
          setInlineGame(true)
        })
      }
      return true
    }

    /**
     * room:enter 応答 — RoomEnterRoomCommand
     * レガシー ProcessCommand_EnterRoom:
     *   result == failure → エラー表示 (メッセージあり)
     * 応答フィールド: result(1=成功), roomId, roomOption, state
     */
    const onRoomEnter = (data: Record<string, unknown>) => {
      if (!mounted) return
      clearRoomActionPending('c14e response')
      logRejoinProbe('c14e room enter response', {
        result: data.result,
        k1e: data.k1e,
        state: data.state,
        responseRoomId: data.k42e ?? data.roomId,
        message: data.k2e ?? data.message,
        data,
      })
      if (Number(data.result) !== 1) {
        if (absorbAlreadyInRoom(data)) return
        const msg = String(data.message ?? 'ルームへの入室に失敗しました')
        showError(msg)
        navigate(`/channel/${channelId ?? ''}/lobby`)
        return
      }
      const nextRoomTitle = data.k45e ?? data.roomTitle
      if (typeof nextRoomTitle === 'string') setRoomTitle(nextRoomTitle)
      const nextRoomOption = data.k46e ?? data.roomOption
      if (typeof nextRoomOption === 'string') setCurrentRoomOption(nextRoomOption)
      setHasChanceItem(asLegacyBool(data.canUseChanceItem))
      setChanceReserved(asLegacyBool(data.k118e ?? data.reserveChance))
      const player = readRoomPlayer(data)
      if (player) {
        putRoomJoinMessage(player.playerId, player.pos >= 0, player.name)
        setPlayers(prev => prev.some(p => p.playerId === player.playerId)
          ? prev.map(p => p.playerId === player.playerId ? { ...p, ...player } : p)
          : [...prev, player])
        const myPix = useAuthStore.getState().player?.pix ?? ''
        if (player.playerId === myPix && player.pos >= 0) {
          putProxyGuideStatus()
          putRoomEntryGuideMessages()
        }
      }
      if (Number(data.state ?? -1) === 2) {
        flushSync(() => {
          inlineGameLoadingStartedAtRef.current = performance.now()
          inlineGameLoadingVisibleRef.current = true
          setInlineGameLoading(true)
          setInlineGame(true)
        })
      }
    }
    SignalR.on('c14e', onRoomEnter)

    const onReserveChance = (data: Record<string, unknown>) => {
      if (!mounted) return
      const success = Number(data.result) === 1 || data.k1e === 'v1e'
      setHasChanceItem(asLegacyBool(data.canUseChanceItem))
      setChanceReserved(success && asLegacyBool(data.k118e ?? data.reserveChance))
    }
    SignalR.on('c55e', onReserveChance)

    /**
     * room:connect_error — ルーム入室エラー
     * 応答フィールド: k42e, k2e, failcode
     */
    const onRoomEnterError = (data: Record<string, unknown>) => {
      if (!mounted) return
      clearRoomActionPending('c34e response')
      logRejoinProbe('c34e room enter error', {
        responseRoomId: data.k42e ?? data.roomId,
        message: data.k2e ?? data.message,
        failcode: data.failcode ?? data.failCode,
        data,
      })
      if (absorbAlreadyInRoom(data)) return
      showError(String(data.k2e ?? data.message ?? 'ルームへの入室に失敗しました'))
      navigate(`/channel/${channelId ?? ''}/lobby`)
    }
    SignalR.on('c34e', onRoomEnterError)

    /**
     * room:member_list (Cmd.MemberList) — アクティブプレイヤーリスト
     * レガシー AddToParser_GetMemberListResponse 相当
    * 応答フィールド: members[{pix, nickName, avatarId, playerPos, ...}]
     */
    const onPlayerList = (data: Record<string, unknown>) => {
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
          isProxy: data[`isProxy${index}`],
          skillCnt: data[`skillCnt${index}`] ?? data[`skillCount${index}`],
          ready: data[`ready${index}`] ?? data[`isReady${index}`] ?? data[`okButton${index}`],
          isHost: pix === data.k50e,
          isViewer: playerType === 'v5e',
        }
      }).filter(member => member.pix != null && String(member.pix) !== '')
      const members = Array.isArray(data.members) && data.members.length > 0
        ? data.members as Array<Record<string, unknown>>
        : legacyMembers
      const viewerMembers = members.filter(isViewerPacket)
      const list = members.filter(m => !isViewerPacket(m)).map(readRoomPlayer).filter(p => p != null)
      const previousReadyById = new Map(playersRef.current.map(player => [player.playerId, player.ready]))
      const previousReadyByPos = new Map(playersRef.current.map(player => [player.pos, player.ready]))
      const uniquePlayers = [...new Map(list.filter(p => p.playerId).map(p => [p.playerId, p])).values()]
        .map(player => ({
          ...player,
          ready: player.ready || previousReadyById.get(player.playerId) || previousReadyByPos.get(player.pos) || false,
        }))
      playersRef.current = uniquePlayers
      setPlayers(uniquePlayers)
      const entries = viewerMembers.map(toViewerEntry).filter(v => v.pix)
      setViewers(entries)
      // ChkHost 相当: 自分が方長か判定
      const myPix = useAuthStore.getState().player?.pix ?? ''
      setAmHost(uniquePlayers.some(p => p.playerId === myPix && p.isHost))
    }
    SignalR.on('c16e', onPlayerList)

    /** member:get_list — 招待ボタンから開くメンバーリスト用 */
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
            avatarId: parts[1] || undefined,
            sex: parts[3] || undefined,
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
            avatarId: undefined,
            sex: parts[1] || undefined,
            slevel: undefined,
            location: parts[2] || undefined,
            roomId: undefined,
          }
        }
        return null
      }).filter((member): member is Record<string, unknown> => member != null && String(member.pix ?? '') !== '')
      const list: Array<Record<string, unknown>> = legacyList.length > 0
        ? legacyList
        : Array.isArray(data.members) ? data.members as Array<Record<string, unknown>> : []
      const myPix = useAuthStore.getState().player?.pix ?? ''
      setChannelMembers(list
        .map(m => ({
          pix: String(m.k3e ?? m.pix ?? m['member' + 'Id'] ?? ''),
          name:     String(m.k8e ?? m.nickname ?? m.name ?? ''),
          rating:   Number(m.k31e ?? m.rating ?? 0),
          avatarId: m.k7e != null || m.avatarId != null ? String(m.k7e ?? m.avatarId) : undefined,
          sex:      m.k11e != null || m.sex != null ? String(m.k11e ?? m.sex) : undefined,
          slevel:   m.k32e != null || m.slevel != null ? String(m.k32e ?? m.slevel) : undefined,
          location: m.k12e != null || m.location != null ? String(m.k12e ?? m.location) : undefined,
          roomId:   m.k42e != null || m.roomId != null ? Number(m.k42e ?? m.roomId) : undefined,
        }))
        .filter(m => m.pix && m.pix !== myPix && (m.roomId == null || m.roomId <= 0) && m.location !== 'room'))
    }
    SignalR.on('c7e', onChannelMemberList)

    const isViewerPacket = (data: Record<string, unknown>) => {
      const playerType = data.k57e ?? data.playerType
      return playerType === 'viewer' || playerType === 'v5e' || playerType === 2 || playerType === '2'
    }

    const isRoomMemberPacket = (data: Record<string, unknown>) =>
      data.k57e != null || data.playerType != null ||
      data.k58e != null || data.playerPos != null || data.seatPos != null

    const upsertViewer = (data: Record<string, unknown>) => {
      const entry = toViewerEntry(data)
      if (!entry.pix) return
      setViewers(prev => [...prev.filter(v => v.pix !== entry.pix), entry])
    }

    const toViewerEntry = (data: Record<string, unknown>): ViewerEntry => ({
      pix: String(data.k3e ?? data.pix ?? data['member' + 'Id'] ?? ''),
      name:     String(data.mjkk34e ?? data.k8e ?? data.nickName ?? data.nickname ?? data.name ?? ''),
      avatarId: data.k7e != null || data.avatarId != null ? String(data.k7e ?? data.avatarId) : undefined,
      sex:      data.k11e != null || data.sex != null ? String(data.k11e ?? data.sex) : undefined,
      slevel:   data.k32e != null || data.slevel != null ? String(data.k32e ?? data.slevel) : undefined,
      dan:      data.dan != null ? String(data.dan) : toDanName(Number(data.gradeCurrLevel ?? -1)),
      rating:   Number(data.k31e ?? data.rating ?? 0),
      playerPos: data.k58e != null || data.playerPos != null ? Number(data.k58e ?? data.playerPos) : undefined,
    })

    const toDanName = (gradeLevel: number) => [
      '10級', '9級', '8級', '7級', '6級', '5級', '4級', '3級', '2級', '1級',
      '初段', '二段', '三段', '四段', '五段', '六段', '七段', '八段', '九段', '十段',
    ][gradeLevel] ?? ''

    const removeViewer = (data: Record<string, unknown>) => {
      const pix = String(data.k3e ?? data.pix ?? data['member' + 'Id'] ?? '')
      if (!pix) return
      setViewers(prev => prev.filter(v => v.pix !== pix))
    }

    /**
     * room:member_joined — AdditionalFunctionOfClientRoomAddMember 相当
     * プレイヤー入室時: メンバー追加 + ステータスメッセージ
    * 応答フィールド: pix, nickName, playerPos, isHost
     */
    const onMemberJoined = (data: Record<string, unknown>) => {
      if (!mounted) return
      if (!isRoomMemberPacket(data)) return
      const pix = String(data.k3e ?? data.pix ?? data['member' + 'Id'] ?? '')
      if (isViewerPacket(data)) {
        const viewerName = String(data.mjkk34e ?? data.k8e ?? data.nickName ?? data.nickname ?? data.name ?? '')
        upsertViewer(data)
        putRoomJoinMessage(pix, false, viewerName)
        return
      }
      const player = readRoomPlayer(data)
      if (player) {
        putRoomJoinMessage(player.playerId, true, player.name)
        playMajakSid(SID_JOIN, boardSoundSkinId ? { skinId: boardSoundSkinId } : {})
        setPlayers(prev => [...prev.filter(p => p.playerId !== player.playerId), player])
        const myPix = useAuthStore.getState().player?.pix ?? ''
        if (player.playerId === myPix && player.pos >= 0) {
          putProxyGuideStatus()
          putRoomEntryGuideMessages()
        }
      }
      // プレイヤーリスト再取得 (AdditionalFunctionOfClientRoomAddMember で m_tbl.AddMember() 相当)
      void SignalR.send('c16e', {}).catch(() => {})
    }
    SignalR.on('c5e', onMemberJoined)

    /**
     * room:member_left — AdditionalFunctionOfClientRoomDeleteMember 相当
     * プレイヤー退室時: メンバー削除 + ステータスメッセージ
     */
    const onMemberLeft = (data: Record<string, unknown>) => {
      if (!mounted) return
      if (!isRoomMemberPacket(data)) return
      const pix = String(data.k3e ?? data.pix ?? data['member' + 'Id'] ?? '')
      const isPlaying = Boolean(data.isPlaying ?? data.playing ?? false)
      const displayName = displayNameForPix(pix)
      const msg = isPlaying
        ? `${displayName}様の接続が切れました。`  // IDS_MAJAK_ROOMDROP 相当
        : `${displayName}様が退室しました。`      // IDS_MAJAK_ROOMEXIT 相当
      if (pix) roomJoinMessageMembersRef.current.delete(pix)
      if (isViewerPacket(data)) {
        removeViewer(data)
        putRoomMessage(msg, legacyPalette.roomExit)
        return
      }
      putRoomMessage(msg, isPlaying ? legacyPalette.roomDrop : legacyPalette.roomExit, true)
      playMajakSid(SID_EXIT, boardSoundSkinId ? { skinId: boardSoundSkinId } : {})
      setPlayers(prev => prev.filter(p => p.playerId !== pix))
      // ChkHost: 方長が変わったかもしれないので再取得
      void SignalR.send('c16e', {}).catch(() => {})
    }
    SignalR.on('c6e', onMemberLeft)

    /** mjkc5e (Cmd.AutoExitRoom) — レガシー AutoExitRoom 通知 */
    const onAutoExitRoom = (data: Record<string, unknown>) => {
      if (!mounted) return
      const pix = String(data.k3e ?? data.pix ?? data['member' + 'Id'] ?? '')
      const myPix = useAuthStore.getState().player?.pix ?? ''
      if (pix === '' || pix === myPix) {
        const message = data.k2e ?? data.message
        if (typeof message === 'string' && message.length > 0) showError(message)
        navigate(`/channel/${channelId ?? ''}/lobby`)
        return
      }
      setPlayers(prev => prev.filter(p => p.playerId !== pix))
      setViewers(prev => prev.filter(v => v.pix !== pix))
    }
    SignalR.on('mjkc5e', onAutoExitRoom)

    /**
     * mjkroom (Cmd.RoomState) — ルーム状態変化通知
     * レガシー CMJRoomWnd は created/joined などの内部 action を履歴表示しない。
     */
    const onRoomState = (_data: Record<string, unknown>) => {
      if (!mounted) return
      const action = String(_data.action ?? '')
      if (action === 'game_ended') finishInlineGame('mjkroom game_ended')
    }
    SignalR.on('mjkroom', onRoomState)

    /**
     * c32e (G::commandGameReport) — レガシー CMJRoomWnd::ProcessRoomGameReportCommand。
     * 最終結果を受け取った時点で CMJRoomWnd は MODE_PLAYEND に入り、卓側の同期処理を止める。
     */
    const onGameReport = (data: Record<string, unknown>) => {
      if (!mounted) return
      if (Number(data.result) !== 1) {
        finishInlineGame('c32e game report failure')
        setHanResData(null)
        setStatusLog(prev => appendRoomLog(prev, { id: nextMessageId(), name: '', text: 'ゲーム結果の取得に失敗しました。', color: legacyPalette.error, bold: true }))
        return
      }
      const myPix = useAuthStore.getState().player?.pix ?? ''
      const players = readHanResPlayers(data, myPix)
      finishInlineGame('c32e game report')
      setHanResFlags({
        hasTor: Boolean(data.hasTor),
        hasTip: Boolean(data.hasTip),
        isViewer: !players.some(player => player.isMe),
        isTournament: Boolean(data.isTournament),
      })
      gameEndStatusLines(data).forEach(line => {
        setStatusLog(prev => appendRoomLog(prev, { id: nextMessageId(), name: '', text: line, color: legacyPalette.normal, bold: true }))
      })
      setHanResData(players)
    }
    SignalR.on('c32e', onGameReport)

    /**
     * chat:relay (Cmd.HanChatRelay) — チャットメッセージ
     * レガシー: MJRoomWnd1.cpp:813-870 (CMJRoomWnd::ProcessCommonChatService)
    *   1. ヘッダ整形:  strHeader = pszPix + " : "
     *   2. 色決定 (スキン除く):
     *        self    : MAJAK_RGB_CHATSELF   = #003060
     *        player  : MAJAK_RGB_CHATOTHER  = #000000
     *        viewer  : MAJAK_RGB_CHATVIEWER = #606060
     *        abuse   : MAJAK_RGB_CHATABUSE  = #ff0000  (サーバから isAbuse フラグが来た場合)
     *      » 行全体を 1色で描画 (名前だけ別色 はレガシーにない)
    * フィールド: k3e, k41e / 互換: pix, string
     */
    const onChat = (data: Record<string, unknown>) => {
      if (!mounted) return
      const pix = String(data.k3e ?? data.pix ?? data['member' + 'Id'] ?? '')
      const myPix    = useAuthStore.getState().player?.pix ?? ''
      const isSelf   = pix !== '' && pix === myPix
      const isAbuse  = Boolean(data.isAbuse ?? data.abuse ?? false)
      // プレイヤー一覧にいるかどうかで player/viewer を判定
      // (レガシー MJRoomWnd1.cpp:822-828 m_tbl.GetMemInf(i) ループ相当)
      const isPlayer = playersRef.current.some(p => p.playerId === pix)
      const isUserViewer = !playersRef.current.some(p => p.playerId === myPix)
      const chatRules = chatRulesRef.current
      if (!isSelf && !chatRules.replayChannel) {
        if (!chatRules.chatEnabled) return
        if (!chatRules.viewerChatVisible && isUserViewer === isPlayer) return
      }
      let color: string
      if (isAbuse)         color = legacyPalette.chatAbuse
      else if (isSelf)     color = legacyPalette.chatSelf
      else if (isPlayer)   color = legacyPalette.chatOther
      else                 color = legacyPalette.chatViewer
      setChatLog(prev => appendRoomLog(prev,
        {
          id:   nextMessageId(),
          name: displayNameForPix(pix),
          pix,
          text: String(data.k41e ?? data.string ?? ''),
          color,
        },
      ))
      if (!isAbuse) playMajakChat()
    }
    SignalR.on('hc1e', onChat)

    const onUseEmoticon = (data: Record<string, unknown>) => {
      if (!mounted) return
      const pix = String(data.k3e ?? data.pix ?? data['member' + 'Id'] ?? '')
      const type = normalizeEmoticonType(data[KEY_EMOTICON_ID] ?? data.emoticonId ?? data.type)
      if (!pix || type == null) return
      const player = playersRef.current.find(p => p.playerId === pix)
      if (!player || player.pos < 0 || player.pos > 3) return
      const myPix = useAuthStore.getState().player?.pix ?? ''
      const me = playersRef.current.find(p => p.playerId === myPix)
      setActiveEmoticons(prev => [
        ...prev.filter(item => item.pix !== pix),
        { id: `${Date.now()}-${emoticonSeqRef.current++}`, pix, type, pos: seatToLegacyLoc(player.pos, me?.pos), startedAt: performance.now() },
      ])
    }
    SignalR.on(CMD_USE_EMOTICON, onUseEmoticon)

    /** InviteResponse — ProcessResponseInviteGameCommand: 招待応答結果 */
    const onInviteResponse = (data: Record<string, unknown>) => {
      if (!mounted) return
      const pix = String(data.k3e ?? data.pix ?? data['member' + 'Id'] ?? '')
      if (!pix || (pendingInviteTargetRef.current && pendingInviteTargetRef.current !== pix)) return
      pendingInviteTargetRef.current = null
      const yesNo = data.k64e
      const displayName = displayNameForPix(pix)
      const text = yesNo === 'v7e'
        ? `${displayName}さんがゲーム申し込みを承諾しました。`
        : yesNo === 'v6e'
          ? `${displayName}さんから応答がありませんでした。`
          : `${displayName}さんから応答がありませんでした。\n『また今 度誘ってね！』`
      const entry = { id: nextMessageId(), name: 'System', text, color: legacyPalette.notice }
      setChatLog(prev => appendRoomLog(prev, entry))
      setStatusLog(prev => appendRoomLog(prev, entry))
    }
    SignalR.on('c23e', onInviteResponse)

    /**
     * smmc1e (Cmd.SendOkButton) — 全プレイヤーの OK ボタン状態のブロードキャスト
     * レガシー: MJRoomWnd3.cpp:75-94 (DispatchServices "smmc1e")
     *   for(mem=0..MAX_PLAYER) GetVal("smmk2e{mem}", nPush)
     *     → m_nReadyFlag bitをセット/クリア
     * 各 座席位置の ready 状態を players リストに反映させる。
     */
    const onOkButtonState = (data: Record<string, unknown>) => {
      if (!mounted) return
      if (inlineGameActiveRef.current) return
      const readyByPos: Record<number, boolean> = {}
      for (let i = 0; i < 4; i++) {
        const v = data[`smmk2e${i}`]
        if (v != null) readyByPos[i] = Number(v) !== 0
      }
      setPlayers(prev => prev.map(p =>
        p.pos in readyByPos ? { ...p, ready: readyByPos[p.pos] } : p,
      ))
      // 自分の ready 状態も同期 (smmc2e 応答よりもブロードキャストが信頼できる)
      const myPix = useAuthStore.getState().player?.pix ?? ''
      const me = playersRef.current.find(p => p.playerId === myPix)
      if (me && me.pos in readyByPos) setIsReady(readyByPos[me.pos])
    }
    SignalR.on('smmc1e', onOkButtonState)

    /**
     * channel:notice (Cmd.Notice) — お知らせ表示
     * 応答フィールド: message
     */
    const onNotice = (data: Record<string, unknown>) => {
      if (!mounted) return
      setNotice(readNoticePayload(data))
    }
    SignalR.on('c40e', onNotice)

    /**
     * mjkc4e (Cmd.AutoStart) — 全員準備OK→ゲーム開始通知
     * レガシー ProcessCommand_AutoStart 相当
     * 応答フィールド: result, roomId, ...
     */
    const navigateToGame = () => {
      if (!mounted || gameNavigatedRef.current) return
      gameNavigatedRef.current = true
      inlineGameEndedRef.current = false
      setHanResData(null)
      setHanResFlags({ hasTor: false, hasTip: false, isViewer: false, isTournament: false })
      setIsReady(false)
      setPlayers(prev => prev.map(player => ({ ...player, ready: false })))
      logRejoinProbe('navigateToGame / set inlineGame true')
      if (DEBUG_GAME) console.info('[RoomScreen] inline game mount', {
        roomId,
      })
      flushSync(() => {
        inlineGameLoadingStartedAtRef.current = performance.now()
        inlineGameLoadingVisibleRef.current = true
        setInlineGameLoading(true)
        setInlineGame(true)
      })
    }

    const onGameStart = (data: Record<string, unknown>) => {
      if (!mounted) return
      logRejoinProbe('mjkc4e game start response', { result: data.k1e ?? data.result, data })
      const result = data.k1e ?? data.result
      if (result != null && !isOk(result)) {
        showError('ゲーム開始に失敗しました')
        return
      }
      putGemGameStatus(data[KEY_GEM_GAME] ?? data.gemGame)
      navigateToGame()
    }
    SignalR.on('mjkc4e', onGameStart)

    const onViewRoom = (data: Record<string, unknown>) => {
      if (!mounted) return
      clearRoomActionPending('c18e response')
      logRejoinProbe('c18e view room response', {
        result: data.k1e ?? data.result,
        responseRoomId: data.k42e ?? data.roomId,
        message: data.k2e ?? data.message,
        data,
      })
      const result = data.k1e ?? data.result
      if (result != null && !isOk(result)) {
        if (absorbAlreadyInRoom(data)) return
        showError(String(data.k2e ?? data.message ?? '観戦に失敗しました'))
        navigate(`/channel/${channelId ?? ''}/lobby`)
        return
      }
      const nextRoomTitle = data.k45e ?? data.roomTitle
      if (typeof nextRoomTitle === 'string') setRoomTitle(nextRoomTitle)
      const nextRoomOption = data.k46e ?? data.roomOption
      if (typeof nextRoomOption === 'string') setCurrentRoomOption(nextRoomOption)
      navigateToGame()
    }
    SignalR.on('c18e', onViewRoom)

    const onAutoEnterRoom = (data: Record<string, unknown>) => {
      if (!mounted) return
      clearRoomActionPending('mjkc6e response')
      logRejoinProbe('mjkc6e auto enter response', {
        result: data.k1e ?? data.result,
        responseRoomId: data.k42e ?? data.roomId,
        message: data.k2e ?? data.message,
        data,
      })
      const result = data.k1e ?? data.result
      if (result != null && !isOk(result)) {
        if (absorbAlreadyInRoom(data)) return
        showError(String(data.k2e ?? data.message ?? 'ルームへの入室に失敗しました'))
        navigate(`/channel/${channelId ?? ''}/lobby`)
        return
      }
      const nextRoomTitle = data.k45e ?? data.roomTitle
      if (typeof nextRoomTitle === 'string') setRoomTitle(nextRoomTitle)
      const nextRoomOption = data.k46e ?? data.roomOption
      if (typeof nextRoomOption === 'string') setCurrentRoomOption(nextRoomOption)
      if (locState.resumePlaying) navigateToGame()
    }
    SignalR.on('mjkc6e', onAutoEnterRoom)

    /** mjkc19e — CMJRoomWnd::ShowSlideAnnounce(ANNOUNCE_GET_TRICKTITLE/MAJAKTITLE) */
    const onGetTitle = (data: Record<string, unknown>) => {
      if (!mounted) return
      const count = asNumber(data[KEY_COUNT], 0)
      for (let index = 0; index < count; index++) {
        const type = asNumber(data[`${KEY_TITLE_TYPE}${index}`], -1)
        const code = asNumber(data[`${KEY_TITLE_CODE}${index}`], 0)
        if (type !== 0 && type !== 1) continue
        setAnnounceData({
          type: type as 0 | 1,
          code,
          name: String(data[`${KEY_TITLE_NAME}${index}`] ?? ''),
        })
      }
    }
    SignalR.on('mjkc19e', onGetTitle)

    /** mjkc22e — CMJRoomWnd::ShowSlideAnnounce(ANNOUNCE_GET_RYUTAMA) + PutStatus */
    const onGetGem = (data: Record<string, unknown>) => {
      if (!mounted) return
      const count = asNumber(data[KEY_COUNT], 0)
      setAnnounceData({ type: 2, code: count, name: '' })
      setStatusLog(prev => appendRoomLog(prev, { id: nextMessageId(), name: '', text: `龍珠を${count}個獲得しました。`, color: legacyPalette.normal, bold: true }))
    }
    SignalR.on('mjkc22e', onGetGem)

    /**
     * smmc2e (Cmd.PushOkButton) — OKボタンプッシュ応答
     * レガシー: MJRoomWnd3.cpp:98-118 (DispatchServices "smmc2e")
     * 応答フィールド: smmk3e (lack money). result は送られない。
     */
    const onOkResult = (data: Record<string, unknown>) => {
      if (!mounted) return
      const lackMoney = Number(data.smmk3e ?? data.lackMoney ?? 0)
      if (lackMoney > 0) {
        showError(`コインが不足しています。不足金額: ${lackMoney.toLocaleString()}円`)
        void exitRoomToLobby(me?.pos)
      }
    }
    SignalR.on('smmc2e', onOkResult)

    /**
    * c8e — CreateRoom コマンドの応答
     * AP-04 §8: ルーム作成成功時に受信する。
     * 応答フィールド: result(1=成功), roomId
     */
    const onRoomCreated = (data: Record<string, unknown>) => {
      if (!mounted) return
      clearRoomActionPending('c8e response')
      const isSuccess = Number(data.result) === 1 || data.k1e === 'v1e'
      const createdRoomId = Number(data.k42e ?? data.roomId ?? 0)
      if (!isSuccess || createdRoomId <= 0) {
        if (absorbAlreadyInRoom(data)) return
        showError(String(data.k2e ?? data.message ?? 'ルームの作成に失敗しました'))
        navigate(`/channel/${channelId ?? ''}/lobby`)
        return
      }
      // URL を /room/{realRoomId} に革新 (履歴を置換)
      navigate(
        `/channel/${channelId}/lobby/room/${createdRoomId}`,
        {
          replace: true,
          state: {
            serverUrl,
            mode: 'enter',
            skipEnterChannel: true,
            roomTitle,
            roomOption: currentRoomOption,
            customBgId: customBoardId,
            customHaiId,
            customBoardType,
          },
        },
      )
      void SignalR.send('c16e', {}).catch(() => {})
    }
    SignalR.on('c8e', onRoomCreated)

    /**
     * レガシー CMJRoomWnd::OnSocketClose:
     * CHgGameWnd::OnSocketClose がエラーを返した場合は ForceExit() する。
     */
    let connectionLostHandled = false
    const onConnectionLost = (error?: Error) => {
      if (!mounted || connectionLostHandled) return
      connectionLostHandled = true
      logRejoinProbe('SignalR connection lost', { errorMessage: error?.message ?? String(error ?? '') })
      if (inlineGameActiveRef.current) {
        console.warn('[RoomScreen] SignalR connection lost during inline game; waiting for game resync/reconnect', {
          channelId,
          roomId,
          pix: useAuthStore.getState().player?.pix ?? '',
          error,
        })
        return
      }
      console.error('[RoomScreen] SignalR connection lost', {
        channelId,
        roomId,
        pix: useAuthStore.getState().player?.pix ?? '',
        error,
      })
      showError('ルームサーバーとの接続が異常終了しました。')
      navigate(channelId ? `/channel/${channelId}` : '/channel', { replace: true })
    }
    SignalR.onConnectionLost(onConnectionLost)
    const onBrowserOffline = () => onConnectionLost()
    window.addEventListener('offline', onBrowserOffline)

    async function setup(forceRejoin = false) {
      const player = useAuthStore.getState().player
      // AP-04 §8: ルーム入室時に初めて WebSocket 接続
      const hubUrl = serverUrl ? `${serverUrl}/hubs/majak` : '/hubs/majak'
      logRejoinProbe('setup start', { forceRejoin, hubUrl, skipEnterChannel: Boolean(locState.skipEnterChannel) })
      // AP-04 §8: ルーム入室時の WebSocket 接続
      // ロビーで同一サーバーに接続済みなら再接続しない (signalr.ts で自動スキップ)
      // 異なるサーバー URL の場合 (マルチサーバー構成) は再接続する
      await SignalR.connect(hubUrl)
      if (!mounted) return
      logRejoinProbe('SignalR connected in setup', { forceRejoin, hubUrl })
      if (forceRejoin || !locState.skipEnterChannel) {
        logRejoinProbe('send c1e enter channel', { forceRejoin })
        await SignalR.send('c1e', buildEnterChannelPayload(channelId ?? '', player))
      }
      if (!mounted) return
      const roomActionKey = `${channelId ?? ''}:${roomId ?? ''}:${locState.mode ?? 'enter'}:${serverUrl}`
      if (!forceRejoin && roomActionSentKeyRef.current === roomActionKey) {
        const navigationType = getDocumentNavigationType()
        const shouldReturnToLobby = !inlineGameActiveRef.current && location.key === 'default'
        logRejoinProbe('skip room action: already sent key', { roomActionKey, navigationType, locationKey: location.key, shouldReturnToLobby })
        if (shouldReturnToLobby) {
          navigate(channelId ? `/channel/${channelId}` : '/channel', { replace: true })
        }
        return
      }
      roomActionSentKeyRef.current = roomActionKey
      if (createMode) {
        logRejoinProbe('send c8e create room', { roomActionKey })
        startRoomActionPending('c8e', { roomActionKey })
        await SignalR.send('c8e', buildCreateRoomPayload(channelId ?? '', player, locState))
      } else if (locState.mode === 'auto') {
        const autoEnterPayload = {
          ...(locState.autoEnterPayload ?? {}),
          pix: player?.pix ?? '',
          k3e: player?.pix ?? '',
        }
        logRejoinProbe('send mjkc6e auto enter', { roomActionKey, payload: autoEnterPayload })
        startRoomActionPending('mjkc6e', { roomActionKey })
        await SignalR.send('mjkc6e', autoEnterPayload)
      } else if (locState.mode === 'view') {
        const numericRoomId = Number(roomId ?? 0)
        logRejoinProbe('send c18e view room', { roomActionKey, numericRoomId })
        startRoomActionPending('c18e', { roomActionKey, numericRoomId })
        await SignalR.send('c18e', {
          roomId: numericRoomId,
          k42e: numericRoomId,
          roomPwd: locState.roomPassword ?? '',
          roomPassword: locState.roomPassword ?? '',
          k67e: locState.roomPassword ?? '',
          playerType: 'v5e',
          k57e: 'v5e',
          ...(locState.tournamentViewPayload ?? {}),
        })
      } else {
        // 既存ルーム入室モード: c14e (G::commandEnterRoom)
        const numericRoomId = Number(roomId ?? 0)
        logRejoinProbe('send c14e enter room', { roomActionKey, numericRoomId })
        startRoomActionPending('c14e', { roomActionKey, numericRoomId })
        await SignalR.send('c14e', {
          roomId: numericRoomId,
          k42e: numericRoomId,
          roomPassword: locState.roomPassword ?? '',
          k67e: locState.roomPassword ?? '',
        })
      }
    }
    const onReconnected = () => {
      logRejoinProbe('SignalR reconnected callback')
      roomActionSentKeyRef.current = ''
      setup(true).catch(err => {
        if (!mounted) return
        logRejoinProbe('reconnect setup failed', { errorMessage: err instanceof Error ? err.message : String(err) })
        console.error('[RoomScreen] reconnect setup failed', err)
        showError('ルームへの再接続に失敗しました')
      })
    }
    SignalR.onReconnected(onReconnected)
    setup().catch(err => {
      if (!mounted) return
      logRejoinProbe('initial setup failed', { errorMessage: err instanceof Error ? err.message : String(err) })
      console.error('[RoomScreen] setup failed', err)
      showError('ルームへの接続に失敗しました')
      navigate(`/channel/${channelId ?? ''}/lobby`)
    })

    return () => {
      mounted = false
      SignalR.offConnectionLost(onConnectionLost)
      SignalR.offReconnected(onReconnected)
      window.removeEventListener('offline', onBrowserOffline)
      SignalR.off('c14e',             onRoomEnter)
      SignalR.off('c55e',             onReserveChance)
      SignalR.off('c34e',             onRoomEnterError)
      SignalR.off('c16e',             onPlayerList)
      SignalR.off('c7e',              onChannelMemberList)
      SignalR.off('c5e',              onMemberJoined)
      SignalR.off('c6e',              onMemberLeft)
      SignalR.off('mjkroom',             onRoomState)
      SignalR.off('c32e',             onGameReport)
      SignalR.off('mjkc5e',              onAutoExitRoom)
      SignalR.off('hc1e',            onChat)
      SignalR.off(CMD_USE_EMOTICON,  onUseEmoticon)
      SignalR.off('c23e',            onInviteResponse)
      SignalR.off('smmc1e',           onOkButtonState)
      SignalR.off('c40e',            onNotice)
      SignalR.off('mjkc4e',           onGameStart)
      SignalR.off('c18e',             onViewRoom)
      SignalR.off('mjkc6e',           onAutoEnterRoom)
      SignalR.off('mjkc19e',          onGetTitle)
      SignalR.off('mjkc22e',          onGetGem)
      SignalR.off('smmc2e',           onOkResult)
      SignalR.off('c8e',             onRoomCreated)
      if (roomActionPendingTimerRef.current !== null) {
        window.clearTimeout(roomActionPendingTimerRef.current)
        roomActionPendingTimerRef.current = null
      }
      // WebSocket は LobbyScreen が管理する (同一サーバーなら切断しない)
      // 別サーバー URL の場合でも LobbyScreen 側で再接続される
    }
  }, [channelId, roomId, navigate, serverUrl, createMode])

  /** チャットログ自動スクロール */
  useEffect(() => {
    if (chatLogRef.current)   chatLogRef.current.scrollTop   = chatLogRef.current.scrollHeight
    if (statusLogRef.current) statusLogRef.current.scrollTop = statusLogRef.current.scrollHeight
  }, [chatLog, statusLog])

  useEffect(() => {
    if (!notice) return
    const timer = window.setTimeout(() => setNotice(null), notice.durationMs)
    return () => window.clearTimeout(timer)
  }, [notice])

  const myPix = useAuthStore.getState().player?.pix ?? ''
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

  const chatInputDisabled = !chatEnabled

  /** チャット送信 (Enter キー → HG_UWM_IMGCHATSENDSTRING 相当) */
  const sendChat = async () => {
    if (chatInputDisabled || !chatText.trim()) return
    // chat:relay = Cmd.HanChatRelay; G::keyString(k41e) / Web 互換 string
    try {
      await SignalR.send('hc1e', { k41e: chatText, string: chatText })
      setChatText('')
    } catch (err) {
      console.error('[RoomScreen] chat send failed', err)
      showError('チャット送信に失敗しました')
    }
  }
  const onChatKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') sendChat()
    /** ESC → HG_UWM_IMGCHATSENDESCKEY 相当: 入力欄クリア */
    if (e.key === 'Escape') setChatText('')
  }

  useEffect(() => {
    const focusChat = () => {
      if (!chatInputDisabled) chatInputRef.current?.focus()
    }
    window.addEventListener(GAME_FOCUS_CHAT_EVENT, focusChat)
    return () => window.removeEventListener(GAME_FOCUS_CHAT_EVENT, focusChat)
  }, [chatInputDisabled])

  useEffect(() => {
    if (activeEmoticons.length === 0) return
    let frame = 0
    const tick = () => {
      const now = performance.now()
      setEmoticonNow(now)
      setActiveEmoticons(prev => prev.filter(item => {
        const duration = (EMOTICON_STEPS[item.type]?.length ?? EMOTICON_STEPS[0].length) * EMOTICON_FRAME_MS
        return now - item.startedAt <= duration
      }))
      frame = window.requestAnimationFrame(tick)
    }
    frame = window.requestAnimationFrame(tick)
    return () => window.cancelAnimationFrame(frame)
  }, [activeEmoticons.length])

  /** 準備完了トグル (CMJRoomWnd::SendRdyPlay 相当)
   * レガシー: MJRoomWnd3.cpp:483-489
   *   CHgParser mp(G::serviceRoom, "smmc2e");
   *   mp.AddValue( G::keyDummy, 0 );
   *   SendDataEncrypt(mp);
   * サーバーは seat 位置を見てトグルし、smmc1e で全員の状態をブロードキャストするため、
   * クライアントからは "ready" 値を送らず、ready 状態の更新は smmc1e 受信に任せる。
   * (ライトフィードバックのため setIsReady は仮更新を保持)
   */
  const onToggleReady = async () => {
    if (!hasPlayerSeat) return
    if (autoMatchingChannel) return
    if (trainingChannel && Boolean(me?.isHost) && !trainingReadyToStart) return
    if (!(trainingChannel && me?.isHost)) setIsReady(prev => !prev)
    // Cmd.PushOkButton = "smmc2e" (C→S 送信 / S→C 応答)
    await SignalR.send('smmc2e', { dummy: 0 })
  }

  /** 招待 (ID_GAMEINVI 相当)
   * 原典: CHgChannelWnd::SendInviteGameToMember / Cmd_Channel_InviteGameToMemberToS
   * フィールド: targetPix (parser target), k3e, k42e, k65e, k64e
   */
  const onGameInvi = async (targetPix: string) => {
    setShowInviteList(false)
    pendingInviteTargetRef.current = targetPix
    const pix = useAuthStore.getState().player?.pix ?? ''
    await SignalR.send('c22e', {
      k3e: pix,
      targetPix,
      k42e: roomId ?? '',
      k65e: '一緒に対戦しませんか？',
      k64e: false,
    })
  }

  const openInviteList = () => {
    if (autoMatchingChannel) return
    setShowInviteList(true)
    void SignalR.send('c7e', buildGetMemberListPayload(channelId ?? '')).catch(() => {})
  }

  const openChannelMemberInfo = (targetPix: string) => {
    const member = channelMembers.find(item => item.pix === targetPix)
    if (!member) return
    setSelectedViewer({
      pix: member.pix,
      name: member.name,
      avatarId: member.avatarId,
      sex: member.sex === 'F' || member.sex === 'female' ? 'female' : 'male',
    })
  }

  const myPixForSeat = useAuthStore.getState().player?.pix ?? ''
  const me = players.find(p => p.playerId === myPixForSeat)
  const routeExpectsPlayerSeat = locState.mode === 'create' || locState.mode === 'enter' || locState.mode === 'auto' || Boolean(locState.resumePlaying)
  const membershipResolved = players.length > 0 || viewers.length > 0
  const isViewerUser = locState.mode === 'view' || (!routeExpectsPlayerSeat && membershipResolved && !me)
  const hasPlayerSeat = Boolean(me) && !isViewerUser
  const emoticonButtonDisabled = !hasPlayerSeat || chatInputDisabled
  const sendEmoticon = async (type: number) => {
    if (emoticonButtonDisabled || type < 0 || type >= EMOTICON_COUNT) return
    try {
      await SignalR.send(CMD_USE_EMOTICON, {
        [KEY_EMOTICON_ID]: type,
        [KEY_EMOTICON_AVATAR_ID]: useAuthStore.getState().player?.avatarId ?? '',
      })
    } catch (err) {
      console.error('[RoomScreen] emoticon send failed', err)
      showError('表情送信に失敗しました')
    }
  }
  const isRoomFull = players.length >= 4
  const skillChannel = extractSubId(channelId)[2] === 'R'
  const allReady = players.length > 0 && players.every(p => p.ready)
  const nonHostPlayers = players.filter(p => !p.isHost)
  const trainingReadyToStart = nonHostPlayers.every(p => p.ready)
  const effectiveRoomFull = trainingChannel || isRoomFull
  const effectiveAllReady = trainingChannel ? trainingReadyToStart : allReady
  const autoControlEnabled = inlineGame && hasPlayerSeat
  const childAutoControlEnabled = autoControlEnabled && !autoControl.prox
  const subId = extractSubId(channelId)
  const chanceButtonVisible = hasChanceItem
  const chanceButtonSendAllowed = hasPlayerSeat && !(subId[1] === '1' && subId[2] === 'D')
  const mobileIngameToolDrawerStyle = {
    '--majak-mobile-ingame-tool-top': `${Math.round(MOBILE_TOP_RIGHT_HUD_PANEL_BOTTOM * mobileIngameScale + MOBILE_INGAME_TOOL_HUD_GAP)}px`,
  } as CSSProperties
  const closeHanRes = () => {
    setHanResData(null)
    if (hanResFlags.isViewer) {
      void exitRoomToLobby(undefined, true)
    }
  }
  const dispatchAutoControl = (state: GameAutoControlState) => {
    window.dispatchEvent(new CustomEvent(GAME_AUTO_CONTROL_EVENT, { detail: state }))
  }
  const updateAutoControl = (updater: (prev: GameAutoControlState) => GameAutoControlState) => {
    setAutoControl(prev => {
      const next = updater(prev)
      dispatchAutoControl(next)
      return next
    })
  }
  const onSetProx = () => updateAutoControl(prev => {
    const prox = !prev.prox
    return { prox, autoTap: prox, autoPass: prox, autoHora: prox }
  })
  const onSetAuto = () => updateAutoControl(prev => ({ ...prev, autoTap: !prev.autoTap }))
  const onSetPass = () => updateAutoControl(prev => ({ ...prev, autoPass: !prev.autoPass }))
  const onSetHora = () => updateAutoControl(prev => ({ ...prev, autoHora: !prev.autoHora }))
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
  useEffect(() => {
    const onKyoResultAction = (event: Event) => {
      if (inlineGameEndedRef.current) return
      if (isViewerUser) return
      const detail = (event as CustomEvent<{ roomId?: string; seatOrder?: number; actionSeq?: number; localDeadlineAt?: number }>).detail ?? {}
      const localDeadlineAt = Number(detail.localDeadlineAt ?? 0)
      if (localDeadlineAt > 0 && performance.now() >= localDeadlineAt) return
      void SignalR.send(CMD_GAME_PLAY, {
        playType: 'MJPID_ACTION',
        roomId: String(detail.roomId ?? roomId ?? ''),
        seatOrder: Number(detail.seatOrder ?? me?.pos ?? 0),
        action: ACT_PAS,
        bipaiIndex: [],
        actionSeq: Number(detail.actionSeq ?? 0) || undefined,
      }).catch(() => {})
    }
    window.addEventListener(KYO_RESULT_ACTION_EVENT, onKyoResultAction)
    return () => window.removeEventListener(KYO_RESULT_ACTION_EVENT, onKyoResultAction)
  }, [isViewerUser, me?.pos, roomId])
  const onReserveChance = async () => {
    if (!chanceButtonSendAllowed || !hasChanceItem) return
    const reserveChance = !chanceReserved
    setChanceReserved(reserveChance)
    try {
      await SignalR.send('c55e', { k118e: reserveChance, reserveChance })
    } catch (err) {
      setChanceReserved(!reserveChance)
      console.error('[RoomScreen] reserve chance failed', err)
    }
  }
  const promptImage = autoMatchingChannel || !me
    ? null
    : trainingChannel && me.isHost
      ? trainingReadyToStart ? promptSrc.pushStart : promptSrc.waitReady
    : !effectiveRoomFull
      ? promptSrc.waitEntry
      : !me.ready
        ? promptSrc.pushReady
        : !effectiveAllReady
          ? promptSrc.waitReady
          : promptSrc.waitStart
  const readyButtonImage = trainingChannel && me?.isHost
    ? bgSkinSrc('mj_btStart')
    : bgSkinSrc('mj_btOK', 'mj_btOk')
  const readyButtonFallbackImage = trainingChannel && me?.isHost
    ? bgSkinFallbackSrc('mj_btStart')
    : bgSkinFallbackSrc('mj_btOK', 'mj_btOk')
  const readyButtonDisabled = trainingChannel && me?.isHost
    ? !trainingReadyToStart
    : autoMatchingChannel
  const optionString = currentRoomOption
  const optionIcons = [
    optionSprite(OPTION_ICON.set, readOptionDigit(optionString, 0, 1), 948, 8, 1),
    optionSprite(OPTION_ICON.kui, readOptionDigit(optionString, 3, 0), 965, 8, 1),
    optionSprite(OPTION_ICON.uma, readOptionDigit(optionString, 1, 2), 982, 8, 3),
    optionSprite(OPTION_ICON.ron, readOptionDigit(optionString, 12, 0), 999, 8, 2),
    optionSprite(OPTION_ICON.red, readOptionDigit(optionString, 5, 2), 948, 25, 2),
    optionSprite(OPTION_ICON.spd, readOptionDigit(optionString, 2, 2), 965, 25, 3),
    optionSprite(OPTION_ICON.opn, readOptionDigit(optionString, 6, 0), 982, 25, 1),
    optionSprite(readOptionDigit(optionString, 14, 0) ? OPTION_ICON.cht : OPTION_ICON.ach, readOptionDigit(optionString, 14, 0) ? readOptionDigit(optionString, 7, 0) : 0, 999, 25, 1),
  ]

  useEffect(() => {
    if (!inlineGame || emoticonButtonDisabled) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return
      const match = /^F([1-6])$/.exec(event.key)
      if (!match) return
      event.preventDefault()
      void sendEmoticon(Number(match[1]) - 1)
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [emoticonButtonDisabled, inlineGame])

  if (roomActionPending && !inlineGame) {
    const pendingStage = (
      <div style={{ position: 'relative', width: ROOM_W, height: ROOM_H, overflow: 'hidden', background: '#000' }}>
        <InlineGameLoadingOverlay visible />
      </div>
    )
    if (isMobileIngame) {
      return (
        <div ref={mobileIngameShellRef} className="majak-mobile-ingame-shell majak-mobile-room-waiting-shell">
          <div
            className="majak-mobile-ingame-scale"
            style={{
              width: ROOM_W,
              height: ROOM_H,
              transform: `translateX(-50%) translateY(${mobileIngameOffsetY}px) scale(${mobileIngameScale})`,
            }}
          >
            {pendingStage}
          </div>
        </div>
      )
    }
    return pendingStage
  }

  if (isMobileIngame && !inlineGame) {
    const mobileSeats = ([0, 1, 2, 3] as const).map(loc => ({
      loc,
      player: players.find(player => seatToLegacyLoc(player.pos, me?.pos) === loc),
    }))
    const readyLabel = autoMatchingChannel
      ? '待機中'
      : trainingChannel && me?.isHost
        ? '開始'
        : isReady ? '準備取消' : '準備完了'
    const readyButtonAttention = readyLabel === '準備完了' && !readyButtonDisabled

    return (
      <div className={`majak-mobile-room-waiting-screen${tengokuBoardSkin ? ' is-tengoku-skin' : ''}`}>
        <section className="majak-mobile-room-table">
          <div className="majak-mobile-room-table__header">
            <div>
              <div className="majak-mobile-eyebrow">ROOM</div>
              <h1>{roomTitle || `${roomId ?? ''}番部屋`}</h1>
            </div>
            <div className="majak-mobile-room-table__meta">
              <span>{players.length}/4</span>
              {viewers.length > 0 && <span>観戦 {viewers.length}</span>}
            </div>
          </div>

          <div className="majak-mobile-room-seats">
            {mobileSeats.map(({ loc, player }) => {
              const avatarFallback = getDefaultAvatarUrl(player?.sex === 'F' || player?.sex === 'female' ? 'female' : 'male')
              const levelText = player
                ? skillChannel ? `@ ${Number(player.skillCnt ?? 0)}局` : player.slevel || '庶民'
                : '空席'
              const ratingText = player && !skillChannel && player.rating > 0 && Number(player.nlevel ?? 0) < 100
                ? `[${String(player.rating).padStart(4, ' ')}]`
                : ''
              return (
                <article key={loc} className={`majak-mobile-room-seat majak-mobile-room-seat--${loc}${player?.ready ? ' is-ready' : ''}${player?.isHost ? ' is-host' : ''}`}>
                  {player ? (
                    <>
                      <img
                        src={player.avatarId ? getGameAvatarUrl(player.avatarId) : avatarFallback}
                        alt=""
                        draggable={false}
                        onError={event => { event.currentTarget.src = avatarFallback }}
                      />
                      <div className="majak-mobile-room-seat__info">
                        <strong>{player.name || player.playerId}</strong>
                        <span>{levelText} {ratingText}</span>
                        <em>{player.isHost ? '親方' : player.ready ? '準備完了' : '待機中'}</em>
                      </div>
                    </>
                  ) : (
                    <div className="majak-mobile-room-seat__empty">空席</div>
                  )}
                </article>
              )
            })}
          </div>

          <div className="majak-mobile-room-primary-actions">
            <button type="button" onClick={() => { void exitRoomToLobby(me?.pos) }}>退室</button>
            {hasPlayerSeat && (
              <button
                type="button"
                className={`majak-mobile-room-ready-button${isReady ? ' is-active' : ''}${readyButtonAttention ? ' is-attention' : ''}`}
                onClick={onToggleReady}
                disabled={readyButtonDisabled}
              >
                {readyLabel}
              </button>
            )}
            {hasPlayerSeat && <button type="button" onClick={openInviteList} disabled={autoMatchingChannel}>招待</button>}
            {chanceButtonVisible && hasPlayerSeat && <button type="button" className={chanceReserved ? 'is-active' : undefined} onClick={() => { void onReserveChance() }} disabled={!chanceButtonSendAllowed}>チャンス</button>}
          </div>
        </section>

        <aside className="majak-mobile-room-side">
          <div className="majak-mobile-room-log majak-mobile-room-log--status" ref={statusLogRef}>
            {statusLog.map(message => <div key={message.id} style={{ color: message.color ?? undefined, fontWeight: message.bold ? 'bold' : undefined }}>{message.text}</div>)}
          </div>

          <div className="majak-mobile-room-log majak-mobile-room-log--chat" ref={chatLogRef}>
            {chatLog.map(message => (
              <div key={message.id} style={{ color: message.color ?? undefined, fontWeight: message.bold ? 'bold' : undefined }}>
                {message.name ? `${message.name} : ${message.text}` : message.text}
              </div>
            ))}
          </div>

          <div className="majak-mobile-room-chat-row">
            <input
              ref={chatInputRef}
              value={chatText}
              onChange={event => setChatText(event.target.value)}
              onKeyDown={onChatKeyDown}
              maxLength={80}
              disabled={chatInputDisabled}
            />
            <button type="button" onClick={() => { void sendChat() }} disabled={chatInputDisabled || !chatText.trim()}>送信</button>
          </div>

          {hasPlayerSeat && (
            <div className="majak-mobile-room-auto-actions">
              <button type="button" className={autoControl.autoPass ? 'is-active' : undefined} onClick={onSetPass} disabled={!childAutoControlEnabled}>オートパス</button>
              <button type="button" className={autoControl.autoHora ? 'is-active' : undefined} onClick={onSetHora} disabled={!childAutoControlEnabled}>オート和了</button>
              <button type="button" className={autoControl.autoTap ? 'is-active' : undefined} onClick={onSetAuto} disabled={!childAutoControlEnabled}>ツモ切り</button>
              <button type="button" className={autoControl.prox ? 'is-active' : undefined} onClick={onSetProx} disabled={!autoControlEnabled}>代打ち</button>
            </div>
          )}
        </aside>

        {showInviteList && (
          <MiniChannelWnd
            channelId={channelId}
            members={channelMembers}
            fullScreen
            compact
            placement="bottom"
            onClose={() => setShowInviteList(false)}
            onReqGame={pix => void onGameInvi(pix)}
            onViewProfile={openChannelMemberInfo}
          />
        )}

        {selectedViewer && <PlayerInfoWnd player={selectedViewer} onClose={() => setSelectedViewer(null)} />}

        {showCfg && (
          <CfgDlg
            initial={roomCfg}
            onOK={cfg => { saveMajakConfig(cfg); setRoomCfg(cfg); setShowCfg(false) }}
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
              await sendAccuseComplaint({ pix: myPix, channelId, roomId: Number(roomId), ...payload })
              void showMessage('通報を受け付けました。', 'お知らせ')
            }}
            onClose={() => setShowAccuse(false)}
          />
        )}

        {hanResData && (
          <HanRes
            players={hanResData}
            hasTor={hanResFlags.hasTor}
            hasTip={hanResFlags.hasTip}
            isViewer={hanResFlags.isViewer}
            isTournament={hanResFlags.isTournament}
            displayScale={isMobileIngame ? mobileHanResScale : 1}
            displayOffsetY={isMobileIngame ? MOBILE_HAN_RES_OFFSET_Y : 0}
            backdrop
            onClose={closeHanRes}
          />
        )}

        <SlideAnnounce data={announceData} onDone={() => setAnnounceData(null)} top={7} />
      </div>
    )
  }

  if (inlineGame) {
    const inlineGameStage = (
      <div style={{ position: 'relative', width: ROOM_W, height: ROOM_H, overflow: 'hidden', background: '#000' }}>
        <div ref={inlineGameRef} style={{ position: 'absolute', left: 0, top: -31, width: GAME_WIDTH, height: GAME_HEIGHT }} />
        <InlineGameLoadingOverlay visible={!isMobileIngame && inlineGameLoading} />

        <div style={{ position: 'absolute', left: 0, top: 0, width: ROOM_W, height: ROOM_H, zIndex: 24, pointerEvents: 'none' }}>
          {activeEmoticons.map(item => <EmoticonAnimation key={item.id} item={item} now={emoticonNow} />)}
        </div>

        {!isMobileIngame && Array.from({ length: EMOTICON_COUNT }, (_, index) => (
          <EmoticonSpriteButton
            key={index}
            index={index}
            x={884 + index * 17}
            y={572}
            disabled={emoticonButtonDisabled}
            onClick={() => void sendEmoticon(index)}
          />
        ))}

        {/* Inline gameplay still uses CMJRoomWnd right panel on desktop; mobile exposes chat/actions as overlay controls. */}
        {!isMobileIngame && (
          <>
            <div
              style={{
                position: 'absolute',
                left: 815,
                top: 18,
                width: 126,
                fontFamily: "'MS PGothic', 'MS Gothic', sans-serif",
                fontSize: 12,
                lineHeight: '14px',
                color: legacyPalette.roomTitle,
                overflow: 'hidden',
                whiteSpace: 'nowrap',
                textOverflow: 'ellipsis',
                zIndex: 20,
                pointerEvents: 'none',
              }}
            >
              {roomTitle}
            </div>

            <div style={{ position: 'absolute', left: 0, top: 0, width: ROOM_W, height: ROOM_H, zIndex: 20, pointerEvents: 'none' }}>
              {optionIcons}
            </div>
          </>
        )}

        <div
          ref={isMobileIngame ? undefined : statusLogRef}
          className="majak-room-scroll"
          style={{
            position: 'absolute',
            left: 808,
            top: 50,
            width: 208,
            height: 130,
            boxSizing: 'border-box',
            paddingLeft: 5,
            paddingRight: 9,
            overflowY: 'auto',
            overflowX: 'hidden',
            fontFamily: "'Noto Sans JP', 'MS Gothic', monospace",
            fontSize: 11,
            color: legacyPalette.roomTitle,
            zIndex: 21,
            pointerEvents: 'auto',
            display: isMobileIngame || announceData ? 'none' : undefined,
          }}
        >
          {statusLog.map(m => (
            <div key={m.id} style={{ color: m.color ?? '#000', fontWeight: m.bold ? 'bold' : undefined }}>{m.text}</div>
          ))}
        </div>

        {!isMobileIngame && (
          <div style={{ position: 'absolute', left: 0, top: 0, zIndex: 22 }}>
            <ViewerListWnd viewers={viewers} y={204} />
          </div>
        )}

        {selectedViewer && (
          <PlayerInfoWnd
            player={selectedViewer}
            onClose={() => setSelectedViewer(null)}
          />
        )}

        <div
          style={{
            position: 'absolute',
            left: 809,
            top: 294,
            width: 200,
            height: 12,
            fontFamily: "'Noto Sans JP', 'MS Gothic', monospace",
            fontSize: 10,
            color: notice?.color ?? 'rgb(254,225,225)',
            overflow: 'hidden',
            whiteSpace: 'nowrap',
            pointerEvents: 'none',
            zIndex: 21,
            display: isMobileIngame || announceData ? 'none' : undefined,
            textShadow: '1px 1px 0 rgba(0,0,0,0.65)',
          }}
        >
          {notice?.text ?? ''}
        </div>

        <div
          ref={isMobileIngame ? undefined : chatLogRef}
          className="majak-room-scroll"
          style={{
            position: 'absolute',
            left: 808,
            top: 314,
            width: 208,
            height: 258,
            boxSizing: 'border-box',
            paddingLeft: 5,
            paddingRight: 9,
            overflowY: 'auto',
            overflowX: 'hidden',
            fontFamily: "'Noto Sans JP', 'MS Gothic', monospace",
            fontSize: 11,
            color: '#000',
            background: 'transparent',
            zIndex: 21,
            pointerEvents: 'auto',
            display: isMobileIngame ? 'none' : undefined,
          }}
        >
          {chatLog.map(m => (
            <div key={m.id} style={{ color: m.color ?? '#000', fontWeight: m.bold ? 'bold' : undefined }}>
              {m.name ? `${m.name} : ${m.text}` : m.text}
            </div>
          ))}
        </div>

        <input
          ref={isMobileIngame ? undefined : chatInputRef}
          value={chatText}
          onChange={e => setChatText(e.target.value)}
          onKeyDown={onChatKeyDown}
          maxLength={80}
          disabled={chatInputDisabled}
          style={{
            position: 'absolute',
            left: 809,
            top: 593,
            width: 201,
            height: 16,
            fontFamily: "'Noto Sans JP', 'MS Gothic', monospace",
            fontSize: 11,
            background: legacyPalette.chatEditBack,
            color: legacyPalette.chatEditText,
            border: 'none',
            outline: 'none',
            padding: '0 2px',
            opacity: chatInputDisabled ? 0.65 : 1,
            zIndex: 30,
            display: isMobileIngame ? 'none' : undefined,
          }}
        />
        <div style={{ position: 'absolute', left: 0, top: 0, width: ROOM_W, height: ROOM_H, zIndex: 30, pointerEvents: 'none' }}>
          <SpriteButton
            src={bgSkinSrc('mj_btPaifuRot3')}
            frameW={46} frameH={31}
            x={118} y={647}
            onClick={() => onViewerRotate(3)}
            title="回転3"
            hidden={isMobileIngame || !isViewerUser}
          />
          <SpriteButton
            src={bgSkinSrc('mj_btPaifuRot1')}
            frameW={46} frameH={31}
            x={164} y={647}
            onClick={() => onViewerRotate(1)}
            title="回転1"
            hidden={isMobileIngame || !isViewerUser}
          />
          <SpriteButton
            src={bgSkinSrc('mj_btLookSutehai')}
            frameW={116} frameH={40}
            x={435} y={647}
            onClick={() => {}}
            title="捨て牌表示"
            hidden={isMobileIngame || !isViewerUser}
            disabled
          />
          <SpriteButton
            src={bgSkinSrc('mj_btExitGame')}
            frameW={116} frameH={40}
            x={isViewerUser ? 558 : 435} y={647}
            onClick={() => { void exitRoomToLobby(me?.pos) }}
            title="退室"
            hidden={isMobileIngame || !isViewerUser}
          />
          <SpriteButton
            src={bgSkinSrc('mj_btPaifuHide')}
            frameW={92} frameH={25}
            x={118} y={676}
            onClick={onViewerHandToggle}
            title="手牌表示切替"
            hidden={isMobileIngame || !isViewerUser}
            disabled={!viewerHandOpenEnabled}
            checked={viewerHandHidden}
          />
          <SpriteButton
            src={bgSkinSrc('mj_btAutoPass')}
            frameW={71} frameH={28}
            x={802} y={646}
            onClick={onSetPass}
            title="オートパス"
            hidden={isMobileIngame || !hasPlayerSeat}
            disabled={!childAutoControlEnabled}
            checked={autoControl.autoPass}
          />
          <SpriteButton
            src={bgSkinSrc('mj_btAutoHoura')}
            frameW={71} frameH={28}
            x={874} y={646}
            onClick={onSetHora}
            title="オート和了"
            hidden={isMobileIngame || !hasPlayerSeat}
            disabled={!childAutoControlEnabled}
            checked={autoControl.autoHora}
          />
          <SpriteButton
            src={bgSkinSrc('mj_btDaiuchi')}
            frameW={71} frameH={56}
            x={946} y={646}
            onClick={onSetProx}
            title="代打ち"
            hidden={isMobileIngame || !hasPlayerSeat}
            disabled={!autoControlEnabled}
            checked={autoControl.prox}
          />
          <SpriteButton
            src={bgSkinSrc('mj_btTsumoGiri')}
            frameW={71} frameH={28}
            x={802} y={674}
            onClick={onSetAuto}
            title="ツモ切り"
            hidden={isMobileIngame || !hasPlayerSeat}
            disabled={!childAutoControlEnabled}
            checked={autoControl.autoTap}
          />
          <SpriteButton
            src={bgSkinSrc('mj_btInvite')}
            frameW={106} frameH={26}
            x={803} y={618}
            onClick={openInviteList}
            title="招待"
            hidden={isMobileIngame || !hasPlayerSeat}
            disabled={autoMatchingChannel}
          />
          <SpriteButton
            src={`${IMG}/mj_btChance.png`}
            frameW={40} frameH={20}
            x={628} y={569}
            onClick={() => { void onReserveChance() }}
            title="チャンス"
            hidden={isMobileIngame || !chanceButtonVisible || !hasPlayerSeat}
            checked={chanceReserved}
          />
        </div>

        {showInviteList && !isMobileIngame && (
          <MiniChannelWnd
            channelId={channelId}
            members={channelMembers}
            fullScreen
            onClose={() => setShowInviteList(false)}
            onReqGame={pix => void onGameInvi(pix)}
            onViewProfile={openChannelMemberInfo}
          />
        )}

        {showCfg && (
          <CfgDlg
            initial={roomCfg}
            onOK={cfg => { saveMajakConfig(cfg); setRoomCfg(cfg); setShowCfg(false) }}
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
              await sendAccuseComplaint({ pix: myPix, channelId, roomId: Number(roomId), ...payload })
              void showMessage('通報を受け付けました。', 'お知らせ')
            }}
            onClose={() => setShowAccuse(false)}
          />
        )}

        <SlideAnnounce
          data={announceData}
          onDone={() => setAnnounceData(null)}
          top={7}
        />
      </div>
    )

    if (isMobileIngame) {
      return (
        <div ref={mobileIngameShellRef} className="majak-mobile-ingame-shell">
          <div
            className="majak-mobile-ingame-scale"
            style={{
              left: 0,
              width: MOBILE_INGAME_FOCUS_W,
              height: ROOM_H,
              overflow: 'hidden',
              transform: `translate(${mobileIngameOffsetX}px, ${mobileIngameOffsetY}px) scale(${mobileIngameScale})`,
              transformOrigin: 'top left',
            }}
          >
            {inlineGameStage}
          </div>
          <InlineGameLoadingOverlay visible={inlineGameLoading} />
          {!mobileIngameChatOpen && (
            <div className={`majak-mobile-ingame-tool-drawer${mobileIngameToolOpen ? ' is-open' : ''}`} style={mobileIngameToolDrawerStyle}>
              <button
                type="button"
                className="majak-mobile-ingame-tool-toggle"
                onClick={() => setMobileIngameToolOpen(open => {
                  const nextOpen = !open
                  if (!nextOpen) setMobileIngameChatOpen(false)
                  return nextOpen
                })}
                aria-expanded={mobileIngameToolOpen}
              >
                {mobileIngameToolOpen ? '▲' : '▼'}
              </button>
              <div className="majak-mobile-ingame-action-bar">
                {hasPlayerSeat && (
                  <>
                    <button type="button" onClick={openInviteList} disabled={autoMatchingChannel}>招待</button>
                    <button type="button" className={autoControl.autoPass ? 'is-active' : undefined} onClick={onSetPass} disabled={!childAutoControlEnabled}>オートパス</button>
                    <button type="button" className={autoControl.autoHora ? 'is-active' : undefined} onClick={onSetHora} disabled={!childAutoControlEnabled}>オート和了</button>
                    <button type="button" className={autoControl.autoTap ? 'is-active' : undefined} onClick={onSetAuto} disabled={!childAutoControlEnabled}>ツモ切り</button>
                    <button type="button" className={autoControl.prox ? 'is-active' : undefined} onClick={onSetProx} disabled={!autoControlEnabled}>代打ち</button>
                    {chanceButtonVisible && <button type="button" className={chanceReserved ? 'is-active' : undefined} onClick={() => { void onReserveChance() }} disabled={!chanceButtonSendAllowed}>チャンス</button>}
                  </>
                )}
                {isViewerUser && (
                  <>
                    <button type="button" onClick={() => onViewerRotate(3)}>回転3</button>
                    <button type="button" onClick={() => onViewerRotate(1)}>回転1</button>
                    <button type="button" disabled>捨て牌</button>
                    <button type="button" onClick={() => { void exitRoomToLobby(me?.pos) }}>退室</button>
                    <button
                      type="button"
                      className={viewerHandHidden ? 'is-active' : undefined}
                      onClick={onViewerHandToggle}
                      disabled={!viewerHandOpenEnabled}
                    >
                      手牌
                    </button>
                  </>
                )}
                <button
                  type="button"
                  className="majak-mobile-ingame-chat-toggle"
                  onClick={() => setMobileIngameChatOpen(true)}
                  aria-expanded={mobileIngameChatOpen}
                >
                  チャット
                </button>
              </div>
            </div>
          )}
          {mobileIngameChatOpen && (
            <aside className={`majak-mobile-ingame-chat-panel${tengokuBoardSkin ? ' is-tengoku-skin' : ''}`}>
              <button
                type="button"
                className="majak-mobile-ingame-chat-toggle majak-mobile-ingame-chat-panel__close is-open"
                onClick={() => setMobileIngameChatOpen(false)}
                aria-expanded={mobileIngameChatOpen}
              >
                閉じる
              </button>
              <div className="majak-mobile-ingame-chat-panel__status" ref={statusLogRef}>
                {statusLog.map(message => <div key={message.id} style={{ color: message.color ?? undefined, fontWeight: message.bold ? 'bold' : undefined }}>{message.text}</div>)}
              </div>
              <div className="majak-mobile-ingame-chat-panel__log" ref={chatLogRef}>
                {chatLog.map(message => (
                  <div key={message.id} style={{ color: message.color ?? undefined, fontWeight: message.bold ? 'bold' : undefined }}>
                    {message.name ? `${message.name} : ${message.text}` : message.text}
                  </div>
                ))}
              </div>
              <div className="majak-mobile-ingame-chat-panel__input">
                <input
                  ref={chatInputRef}
                  value={chatText}
                  onChange={event => setChatText(event.target.value)}
                  onKeyDown={onChatKeyDown}
                  maxLength={80}
                  disabled={chatInputDisabled}
                />
                <button type="button" onClick={() => { void sendChat() }} disabled={chatInputDisabled || !chatText.trim()}>送信</button>
              </div>
            </aside>
          )}
          {showInviteList && (
            <MiniChannelWnd
              channelId={channelId}
              members={channelMembers}
              fullScreen
              compact
              placement="bottom"
              onClose={() => setShowInviteList(false)}
              onReqGame={pix => void onGameInvi(pix)}
              onViewProfile={openChannelMemberInfo}
            />
          )}
        </div>
      )
    }

    return inlineGameStage
  }

  const waitingRoomStage = (
    /* CMJRoomWnd content area below MajakFrame title bar: W_BOARD/H_BOARD = 789×704 */
    <div style={{ position: 'relative', width: ROOM_W, height: ROOM_H, overflow: 'hidden' }}>
      <style>{`
        @keyframes majakRoomPromptFade {
          0% { opacity: 0.5; }
          50% { opacity: 1; }
          100% { opacity: 0.5; }
        }
      `}</style>

      {/* ── ゲーム卓エリア: m_wndGame.Create(5, 31, this) → content y=0 ── */}
      <img
        src={customBoardSrc}
        alt=""
        draggable={false}
        onError={event => { event.currentTarget.src = `${IMG}/mj_board.png` }}
        style={{ position: 'absolute', left: BOARD_X, top: BOARD_Y, width: 789, height: 704 }}
      />

      {/* ── CMJGameWnd::PutPanel: PANELMODE_ROOM=mj_resBtBoard, PANELMODE_VIEW=mj_watchBoard at X_PANEL=102, Y_PANEL=644 */}
      <img
        src={bgSkinSrc(isViewerUser ? 'mj_watchBoard' : 'mj_resBtBoard')}
        alt=""
        draggable={false}
        onError={event => { event.currentTarget.src = `${IMG}/${isViewerUser ? 'mj_watchBoard' : 'mj_resBtBoard'}.png` }}
        style={{ position: 'absolute', left: 102, top: 644, width: 580, height: 60, pointerEvents: 'none' }}
      />

      {/* ── CMJTblUser::Prompt: Set(209-5,315-31) on m_wndGame → content (204,284), z=1000, 2s alpha fade ── */}
      {promptImage && (
        <img
          src={promptImage}
          alt=""
          draggable={false}
          onError={event => { event.currentTarget.src = defaultSkinSrc(event.currentTarget.src) }}
          style={{
            position: 'absolute',
            left: 204,
            top: 284,
            width: 379,
            height: 139,
            zIndex: 1000,
            animation: 'majakRoomPromptFade 2s linear infinite',
            pointerEvents: 'none',
          }}
        />
      )}

      {/* ── CMJTblDraw::PutOdrBox / PutWaitMark — mempos[] coordinates on m_wndGame ── */}
      {players.map(player => {
        const pos = MEM_POS[seatToLegacyLoc(player.pos, me?.pos)]
        const avatarPos = roomPoint(pos.avt)
        const namePos = roomPoint(pos.name)
        const textPos = roomPoint(pos.text)
        const waitPos = roomPoint(pos.gls)
        const titlePos = roomPoint(pos.ttl)
        const trickPos = roomPoint(pos.trk)
        const avatarFallback = getDefaultAvatarUrl(player.sex === 'F' || player.sex === 'female' ? 'female' : 'male')
        const majakTitleSrc = getMajakTitleSrc(player.majakTitle)
        const trickTitleSrc = getTrickTitleSrc(player.trickTitle)
        const isProxyPlayer = Boolean(player.isProxy) || (player.playerId === myPix && autoControl.prox)
        const playerTextColor = isProxyPlayer ? 'rgb(224,0,0)' : '#fff'
        const levelText = skillChannel ? `@ ${Number(player.skillCnt ?? 0)}局` : player.slevel || ''
        const ratingText = !skillChannel && player.rating > 0 && Number(player.nlevel ?? 0) < 100
          ? `[${String(player.rating).padStart(4, ' ')}]`
          : ''
        const seatPanelLeft = namePos.left
        const seatPanelRight = namePos.left + 98
        const avatarRight = avatarPos.left + 45
        const textBlockLeft = avatarPos.left < namePos.left + 49 ? avatarRight : seatPanelLeft
        const textBlockWidth = avatarPos.left < namePos.left + 49 ? seatPanelRight - avatarRight : avatarPos.left - seatPanelLeft
        return (
          <div
            key={player.playerId}
            style={{ position: 'absolute', left: 0, top: 0, width: ROOM_W, height: ROOM_H, zIndex: 10, pointerEvents: 'none' }}
          >
            <img
              src={player.avatarId ? getGameAvatarUrl(player.avatarId) : avatarFallback}
              alt=""
              draggable={false}
              onError={e => {
                e.currentTarget.src = avatarFallback
              }}
              style={{
                position: 'absolute',
                left: avatarPos.left,
                top: avatarPos.top,
                width: 45,
                height: 102,
                objectFit: 'contain',
                zIndex: 10,
                pointerEvents: 'none',
              }}
            />
            {majakTitleSrc && (
              <img
                src={majakTitleSrc}
                alt=""
                draggable={false}
                style={{ position: 'absolute', left: titlePos.left, top: titlePos.top, zIndex: 12, pointerEvents: 'none' }}
              />
            )}
            {trickTitleSrc && (
              <img
                src={trickTitleSrc}
                alt=""
                draggable={false}
                style={{ position: 'absolute', left: trickPos.left, top: trickPos.top, zIndex: 11, pointerEvents: 'none' }}
              />
            )}
            <div style={{
              position: 'absolute',
              left: namePos.left,
              top: namePos.top,
              width: 98,
              height: 14,
              fontFamily: "'MS PGothic', 'MS Gothic', sans-serif",
              fontSize: 12,
              lineHeight: '14px',
              color: playerTextColor,
              zIndex: 10,
              overflow: 'hidden',
              whiteSpace: 'nowrap',
              textAlign: 'center',
              pointerEvents: 'none',
            }}>
              {player.name}
            </div>
            <div style={{
              position: 'absolute',
              left: textBlockLeft,
              top: textPos.top,
              width: textBlockWidth,
              height: 30,
              fontFamily: "'MS PGothic', 'MS Gothic', sans-serif",
              fontSize: 12,
              lineHeight: '15px',
              color: playerTextColor,
              zIndex: 10,
              overflow: 'hidden',
              whiteSpace: 'nowrap',
              textAlign: 'center',
              pointerEvents: 'none',
            }}>
              <div>{levelText}</div>
              <div>{ratingText}</div>
            </div>
            {!autoMatchingChannel && !player.ready && (
              <img
                src={`${IMG}/mj_IcnWait.png`}
                alt=""
                draggable={false}
                style={{ position: 'absolute', left: waitPos.left, top: waitPos.top, width: 20, height: 22, zIndex: 11, pointerEvents: 'none' }}
              />
            )}
          </div>
        )
      })}

      {/* ── サイドバー背景 mj_sideBg.png (225×704) X_SIDEBAR=794, Y_SIDEBAR=31 → content y=0 ── */}
      <img
        src={bgSkinSrc('mj_sideBg')}
        alt=""
        draggable={false}
        onError={event => { event.currentTarget.src = `${IMG}/mj_sideBg.png` }}
        style={{ position: 'absolute', left: 794, top: 0, width: 225, height: 704 }}
      />

      {/* ── ルームタイトル MAJAK_ROOMTITLE_LEFT=815 TOP=49 W=126 H=14 ── */}
      <div
        style={{
          position: 'absolute',
          left: 815,
          top: 18,
          width: 126,
          height: 14,
          fontFamily: "'MS PGothic', 'MS Gothic', sans-serif",
          fontSize: 12,
          lineHeight: '14px',
          color: legacyPalette.roomTitle,
          overflow: 'hidden',
          whiteSpace: 'nowrap',
          textOverflow: 'ellipsis',
        }}
      >
        {roomTitle}
      </div>

      {/* ── ルールアイコン: MJRoomWnd1.cpp OnPaint BitBlt(948+17*n,39/56) → content y=8/25 ── */}
      {optionIcons}

      {/* ── ステータスログ HG_GAME_STATUSWND: (808,81)-(1016,211) ──
           プレイヤー情報 (PlayerList 受信後) を上部に表示し、
           ステータスメッセージをその下に表示する。
           CMJTblUser::PutScore / 座席情報 相当 */}
      <div
        ref={statusLogRef}
        className="majak-room-scroll"
        style={{
          position: 'absolute',
          left: 808,
          top: 50,
          width: 208,   /* 1016-808 */
          height: 130,  /* 211-81 */
          boxSizing: 'border-box',
          paddingLeft: 5,
          paddingRight: 9,
          overflowY: 'auto',
          overflowX: 'hidden',
          fontFamily: "'Noto Sans JP', 'Noto Sans JP', 'MS Gothic', monospace",
          fontSize: 11,
          color: '#000',
          background: 'transparent',
          pointerEvents: 'none',
          display: announceData ? 'none' : undefined,
        }}
      >
        {/* ステータスメッセージ */}
        {statusLog.map(m => (
          <div key={m.id} style={{ color: m.color ?? '#000', fontWeight: m.bold ? 'bold' : undefined }}>{m.text}</div>
        ))}
      </div>

      {/* ── 観戦者アバター領域: frame Y_VIEWER_WINDOW=235 → content y=204 ── */}
      <ViewerListWnd viewers={viewers} y={204} />

      {selectedViewer && (
        <PlayerInfoWnd
          player={selectedViewer}
          onClose={() => setSelectedViewer(null)}
        />
      )}

      {/* ── お知らせ HG_GAME_NOTICE: (809,325)-(1009,337) ── */}
      <div
        style={{
          position: 'absolute',
          left: 809,
          top: 294,
          width: 200,   /* 1009-809 */
          height: 12,   /* 337-325 */
          fontFamily: "'Noto Sans JP', 'Noto Sans JP', 'MS Gothic', monospace",
          fontSize: 10,
          color: notice?.color ?? 'rgb(254,225,225)',
          overflow: 'hidden',
          whiteSpace: 'nowrap',
          pointerEvents: 'none',
          zIndex: 25,
          display: announceData ? 'none' : undefined,
          textShadow: '1px 1px 0 rgba(0,0,0,0.65)',
        }}
      >
        {notice?.text ?? ''}
      </div>

      {/* ── チャットログ HG_GAME_CHATLIST: (808,345)-(1016,603) ── */}
      <div
        ref={chatLogRef}
        className="majak-room-scroll"
        style={{
          position: 'absolute',
          left: 808,
          top: 314,
          width: 208,   /* 1016-808 */
          height: 258,  /* 603-345 */
          boxSizing: 'border-box',
          paddingLeft: 5,
          paddingRight: 9,
          overflowY: 'auto',
          overflowX: 'hidden',
          fontFamily: "'Noto Sans JP', 'Noto Sans JP', 'MS Gothic', monospace",
          fontSize: 11,
          color: '#000',
          background: 'transparent',
        }}
      >
        {chatLog.map(m => (
          /* レガシー MJRoomWnd1.cpp:832 strHeader = pszPix + " : ";
             « 名前と本文を 1色 で描画 (名前のみ別色 はレガシーに存在しない) */
          <div key={m.id} style={{ color: m.color ?? '#000', fontWeight: m.bold ? 'bold' : undefined }}>
            {m.name ? `${m.name} : ${m.text}` : m.text}
          </div>
        ))}
      </div>

      {/* ── チャット入力 HG_GAME_CHATEDIT: (809,624)-(1010,640) ── */}
      <input
        ref={chatInputRef}
        value={chatText}
        onChange={e => setChatText(e.target.value)}
        onKeyDown={onChatKeyDown}
        maxLength={80}
        disabled={chatInputDisabled}
        style={{
          position: 'absolute',
          left: 809,
          top: 593,
          width: 201,   /* 1010-809 */
          height: 16,   /* 640-624 */
          fontFamily: "'Noto Sans JP', 'Noto Sans JP', 'MS Gothic', monospace",
          fontSize: 11,
          background: legacyPalette.chatEditBack,
          color: legacyPalette.chatEditText,
          border: 'none',
          outline: 'none',
          padding: '0 2px',
          opacity: chatInputDisabled ? 0.65 : 1,
        }}
      />

      {/* ── 観戦 PANELMODE_VIEW: X_VIEWROT3/1, X_VIEWHIDE, X_KYOHIDE, X_KYOCONT */}
      <SpriteButton
        src={bgSkinSrc('mj_btPaifuRot3')}
        frameW={46} frameH={31}
        x={118} y={647}
        onClick={() => onViewerRotate(3)}
        title="回転3"
        hidden={!isViewerUser}
      />
      <SpriteButton
        src={bgSkinSrc('mj_btPaifuRot1')}
        frameW={46} frameH={31}
        x={164} y={647}
        onClick={() => onViewerRotate(1)}
        title="回転1"
        hidden={!isViewerUser}
      />
      <SpriteButton
        src={bgSkinSrc('mj_btLookSutehai')}
        frameW={116} frameH={40}
        x={435} y={647}
        onClick={() => {}}
        title="捨て牌表示"
        hidden={!isViewerUser}
        disabled
      />

      {/* ── 退室ボタン: X_SETEXIT=440-5, Y_SETEXIT=678-31 on m_wndGame → content (435,647) */}
      <SpriteButton
        src={bgSkinSrc('mj_btExitGame')}
        frameW={116} frameH={40}
        x={isViewerUser ? 558 : 435} y={647}
        onClick={() => { void exitRoomToLobby(me?.pos) }}
        title="退室"
      />
      <SpriteButton
        src={bgSkinSrc('mj_btPaifuHide')}
        frameW={92} frameH={25}
        x={118} y={676}
        onClick={onViewerHandToggle}
        title="手牌表示切替"
        hidden={!isViewerUser}
        disabled={!viewerHandOpenEnabled}
        checked={viewerHandHidden}
      />

       {/* ── OK (準備完了) ボタン: X_KYOCONT=563-5, Y_KYOCONT=678-31 on m_wndGame → content (558,647)
           GetSetContBtn 相当: OnToggleReady → SendRdyPlay
         レガシー PANELMODE_ROOM 初回(nPrevMode==PANELMODE_NONE) は m_btnKyoCont(mj_btOk) を表示 */}
      <SpriteButton
        src={readyButtonImage}
        fallbackSrc={readyButtonFallbackImage}
        frameW={116} frameH={40}
        x={558} y={647}
        onClick={onToggleReady}
        title={autoMatchingChannel ? undefined : trainingChannel && me?.isHost ? '開始' : isReady ? '準備完了 (キャンセル)' : 'OK (準備完了)'}
        hidden={!hasPlayerSeat}
        disabled={readyButtonDisabled}
        checked={isReady}
      />

      {/* ── 右下操作ボタン: m_pRoomWnd->GetScreen() はサイドバー用。full x=X_SET*, content y=Y_SET*-31 */}
      <SpriteButton
        src={bgSkinSrc('mj_btAutoPass')}
        frameW={71} frameH={28}
        x={802} y={646}
        onClick={onSetPass}
        title="オートパス"
        hidden={!hasPlayerSeat}
        disabled={!childAutoControlEnabled}
        checked={autoControl.autoPass}
      />
      <SpriteButton
        src={bgSkinSrc('mj_btAutoHoura')}
        frameW={71} frameH={28}
        x={874} y={646}
        onClick={onSetHora}
        title="オート和了"
        hidden={!hasPlayerSeat}
        disabled={!childAutoControlEnabled}
        checked={autoControl.autoHora}
      />
      <SpriteButton
        src={bgSkinSrc('mj_btDaiuchi')}
        frameW={71} frameH={56}
        x={946} y={646}
        onClick={onSetProx}
        title="代打ち"
        hidden={!hasPlayerSeat}
        disabled={!autoControlEnabled}
        checked={autoControl.prox}
      />
      <SpriteButton
        src={bgSkinSrc('mj_btTsumoGiri')}
        frameW={71} frameH={28}
        x={802} y={674}
        onClick={onSetAuto}
        title="ツモ切り"
        hidden={!hasPlayerSeat}
        disabled={!childAutoControlEnabled}
        checked={autoControl.autoTap}
      />

      {/* ── 招待ボタン mj_btInvite.png 80×23 at (X_ROOMINVI-X_SIDEBAR+X_SIDEBAR, Y_ROOMINVI-Y_SIDEBAR+Y_SIDEBAR)
           = full-window (803,649) → MajakFrame content (803,618)
           原典: m_btnInvi.Create("mj_btInvite", ..., X_ROOMINVI-X_SIDEBAR, Y_ROOMINVI-Y_SIDEBAR, ...)
           クリック時はメンバーリストを表示し、選択した pix を招待する。 */}
      <SpriteButton
        src={bgSkinSrc('mj_btInvite')}
        frameW={106} frameH={26}
        x={803} y={618}
        onClick={openInviteList}
        title="招待"
        hidden={!hasPlayerSeat}
        disabled={autoMatchingChannel}
      />
      <SpriteButton
        src={`${IMG}/mj_btChance.png`}
        frameW={40} frameH={20}
        x={628} y={569}
        onClick={() => { void onReserveChance() }}
        title="チャンス"
        hidden={!chanceButtonVisible || !hasPlayerSeat}
        checked={chanceReserved}
      />

      {showInviteList && (
        <MiniChannelWnd
          channelId={channelId}
          members={channelMembers}
          fullScreen
          onClose={() => setShowInviteList(false)}
          onReqGame={pix => void onGameInvi(pix)}
          onViewProfile={openChannelMemberInfo}
        />
      )}

      {/* ── CMJCfgDlg: ゲーム設定 (設定ボタン押下時) ── */}
      {showCfg && (
        <CfgDlg
          initial={roomCfg}
          onOK={cfg => { saveMajakConfig(cfg); setRoomCfg(cfg); setShowCfg(false) }}
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
            await sendAccuseComplaint({ pix: myPix, channelId, roomId: Number(roomId), ...payload })
            void showMessage('通報を受け付けました。', 'お知らせ')
          }}
          onClose={() => setShowAccuse(false)}
        />
      )}

      {hanResData && (
        <HanRes
          players={hanResData}
          hasTor={hanResFlags.hasTor}
          hasTip={hanResFlags.hasTip}
          isViewer={hanResFlags.isViewer}
          isTournament={hanResFlags.isTournament}
          displayScale={isMobileIngame ? mobileHanResScale : 1}
          displayOffsetY={isMobileIngame ? MOBILE_HAN_RES_OFFSET_Y : 0}
          backdrop
          onClose={closeHanRes}
        />
      )}

      {/* ── CMJSlideAnnounce: mjkc19e/mjkc22e 受信時のスライド公告 ── */}
      <SlideAnnounce
        data={announceData}
        onDone={() => setAnnounceData(null)}
        top={7}
      />

    </div>
  )

  if (isMobileIngame) {
    return (
      <div ref={mobileIngameShellRef} className="majak-mobile-ingame-shell majak-mobile-room-waiting-shell">
        <div
          className="majak-mobile-ingame-scale"
          style={{
            width: ROOM_W,
            height: ROOM_H,
            transform: `translateX(-50%) translateY(${mobileIngameOffsetY}px) scale(${mobileIngameScale})`,
          }}
        >
          {waitingRoomStage}
        </div>
      </div>
    )
  }

  return waitingRoomStage
}


