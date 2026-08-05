/**
 * GameScene — インゲームメインシーン
 * CMJGameWnd + CMJTblGame + CMJTblPaif + CMJTblDraw 相当 (AP-09 §2-1〜§2-6)
 *
 * ── ゲーム座標系 ──────────────────────────────────────────────────────────
 * Phaser ワールド = 1019×735 (GAME_WIDTH × GAME_HEIGHT)
 * ゲームボード: x=5, y=31, w=789, h=704  (mj_board.png)
 * サイドバー:   x=794, y=31, w=225, h=704 (mj_sideBg.png)
 * ボード中心:   (399, 383)
 *
 * ── タイルコード → スプライトフレームインデックス (PaiCode.h 準拠) ──────
 *   MAN (kind=0): 0x01-0x09 → frame 0-8
 *   SOU (kind=1): 0x11-0x19 → frame 9-17
 *   PIN (kind=2): 0x21-0x29 → frame 18-26
 *   風牌 (kind=3): 0x31-0x34 → frame 27-30
 *   三元牌 (kind=3): 0x35-0x37 → frame 31-33
 *
 * ── 4 プレイヤー配置 ──────────────────────────────────────────────────────
 * odr → loc = (4 + odr - myOdr) % 4 (MJTblDraw::Odr2Loc)
 * 手牌: MJTblDraw5.cpp tehpos / tehcol / mohofs
 * 捨て牌: MJTblDraw5.cpp sthpos / sthcol / sthrow / sthrot
 *
 * ── SignalR イベント ──────────────────────────────────────────────────────
 * smmc4e     → 牌情報リスト (SendPaiInfo)
 * playing    → MJPID_INIHAN / MJPID_INIKYO / MJPID_ACTION / MJPID_ENDKYO
 * playing/MJPID_ENDKYO → 局結果
 */
import Phaser from 'phaser'
import * as SignalR from '../api/signalr'
import type { CreateGameOptions } from '../game/GameInstance'
import { resolveAutoControlAction } from '../game/autoControl'
import { DESKTOP_INGAME_LAYOUT, getIngameLayout, type IngameLayoutMode } from '../game/ingameLayout'
import { MOBILE_PLAYFIELD_OFFSET_Y, mobileCenterHudOffset, mobileVisibleWorldBounds, mobileVisibleWorldLayoutKey } from '../game/mobileIngameViewport'
import { useAuthStore } from '../store/authStore'
import { getDefaultAvatarUrl, getGameAvatarUrl } from '../utils/resources'
import { getUiFontFamily, getUiFontSize } from '../utils/typography'
import {
  MID_BAD, MID_FESRIC, MID_GOOD, MID_NORMAL, MID_RICHI, MID_TEN_ALLLAST, MID_TEN_NANBA, MID_TEN_REACH1, MID_TEN_REACH2, MID_TEN_TONBA,
  playMajakBgm, playMajakCallVoice, playMajakSfx, playMajakSid,
  SID_DICE, SID_EFFECT_R_LV1, SID_EFFECT_R_LV2, SID_EFFECT_R_LV3, SID_EFFECT_T_LV1, SID_EFFECT_T_LV2, SID_EFFECT_T_LV3, SID_EFFECT_YAKUMAN, SID_EXPOSE, SID_FURO, SID_RICSTK, SID_SIPAI, SID_THROW, SID_THROW_DORA, SID_THROW_RICH, SID_TIME, SID_TURN,
} from '../utils/majakSound'

const PAIFU_ROTATE_EVENT = 'majak:paifu-rotate'
const PAIFU_HAND_OPEN_EVENT = 'majak:paifu-hand-open'
const PAIFU_GRAPH_EVENT = 'majak:paifu-graph'
const DEBUG_GAME = import.meta.env.VITE_DEBUG_GAME === '1'
const ASK_END_SET_EVENT = 'majak:ask-end-set'
const KYO_RESULT_ACTION_EVENT = 'majak:kyo-result-action'
const GAME_FOCUS_CHAT_EVENT = 'majak:game-focus-chat'
const GAME_AUTO_CONTROL_EVENT = 'majak:auto-control'
const GAME_STATUS_EVENT = 'majak:game-status'
const GAME_SYNC_EVENT = 'majak:game-sync'
const GAME_FLOW_TRACE_PREFIX = '[GameFlow]'
const DISCARD_PROBE_PREFIX = '[GameScene/DiscardProbe]'
const RESYNC_PROBE_PREFIX = '[GameScene/ResyncProbe]'
const IMG = '/assets/images/game'
const UI_SINGLE_DELIVERY_EVENTS = new Set(['turnChange', 'actionPromptStart', 'actionPromptEnd'])
const INITIAL_RESYNC_AFTER_HISTORY_SKIP_MS = 1000
const CUSTOM_BGM_ID_EXTRA = 100008
const CUSTOM_BGM_ID_TENGOKU = 100009
const CUSTOM_ITEM_TYPE_BG_EXTRA = 11
const CUSTOM_ITEM_TYPE_BG_TENGOKU = 12
const CUSTOM_DEFAULT_ID_COSTUME = 100011
const AVAILABLE_COSTUME_IDS = new Set([9, 10, 11])
const TIME_WARNING_START_MS = 5000

type GameAutoControlState = {
  prox: boolean
  autoTap: boolean
  autoPass: boolean
  autoHora: boolean
}

/* ========================================================================
 * 定数
 * ======================================================================== */
let INGAME_LAYOUT = getIngameLayout()
let BOARD_X = INGAME_LAYOUT.board.x,   BOARD_Y = INGAME_LAYOUT.board.y
let BOARD_W = INGAME_LAYOUT.board.width, BOARD_H = INGAME_LAYOUT.board.height
let SIDE_PANEL = INGAME_LAYOUT.sidePanel
let DRAGON_OVERLAY = INGAME_LAYOUT.dragonOverlay
let CENTER_INFO = INGAME_LAYOUT.centerInfo
let X_PANEL = INGAME_LAYOUT.panel.x
let Y_PANEL = INGAME_LAYOUT.panel.y
let W_PANEL = INGAME_LAYOUT.panel.width
let H_PANEL = INGAME_LAYOUT.panel.height
let Z_PANEL = INGAME_LAYOUT.panel.depth
let ACTION_BUTTON_LAYOUT = INGAME_LAYOUT.actionButtons
const DESKTOP_DISCARD_COLS = 6
const MOBILE_DISCARD_COLS = 10
const MOBILE_TILE_SCALE = 0.76
const MOBILE_SELF_HAND_TILE_SCALE = 0.95
const MOBILE_OTHER_HAND_TILE_SCALE = MOBILE_SELF_HAND_TILE_SCALE * 0.78
const MOBILE_OTHER_HAND_STEP_RATIO = 0.72
const MOBILE_TOP_HAND_TILE_OFFSET = -2
const MOBILE_SIDE_HAND_Y_OFFSET = 0
const MOBILE_DISCARD_LAYOUT_SCALE = 1.20
const MOBILE_DISCARD_TILE_SCALE = 0.70
const MOBILE_CONTENT_BASE_ASPECT = 375 / 667
const MOBILE_CONTENT_SCALE_MIN = 0.68
const MOBILE_OPPONENT_SUMMARY_TILE_SCALE = 0.64
const MOBILE_HUD_AVATAR_WIDTH = 44
const MOBILE_HUD_AVATAR_INSET_X = 14
const MOBILE_HUD_AVATAR_INSET_TOP = 8
const MOBILE_SIDE_HAND_AVATAR_GAP = 8
const MOBILE_LEFT_SIDE_HAND_INNER_OFFSET = 28
const MOBILE_DEAD_WALL_AVATAR_GAP = 86
const MOBILE_DEAD_WALL_AVATAR_Y_OFFSET = 14
const MOBILE_SELF_HAND_BOTTOM_INSET = 12
const MOBILE_SELF_HAND_FIXED_COUNT = 14
const MOBILE_OTHER_HAND_FIXED_COUNT = 14
const MOBILE_BOARD_BACKGROUND_SCALE = 1.6
const MOBILE_DISCARD_CENTER_INFO_OFFSET = [
  { x:  39, y: 149 },
  { x: 253, y: 134 },
  { x: 218, y: -18 },
  { x: -14, y:   0 },
] as const
let DISCARD_COLS = DESKTOP_DISCARD_COLS

function applyIngameLayout(mode: IngameLayoutMode) {
  INGAME_LAYOUT = getIngameLayout(mode)
  DISCARD_COLS = mode === 'mobileLandscape' ? MOBILE_DISCARD_COLS : DESKTOP_DISCARD_COLS
  BOARD_X = INGAME_LAYOUT.board.x
  BOARD_Y = INGAME_LAYOUT.board.y
  BOARD_W = INGAME_LAYOUT.board.width
  BOARD_H = INGAME_LAYOUT.board.height
  SIDE_PANEL = INGAME_LAYOUT.sidePanel
  DRAGON_OVERLAY = INGAME_LAYOUT.dragonOverlay
  CENTER_INFO = INGAME_LAYOUT.centerInfo
  X_PANEL = INGAME_LAYOUT.panel.x
  Y_PANEL = INGAME_LAYOUT.panel.y
  W_PANEL = INGAME_LAYOUT.panel.width
  H_PANEL = INGAME_LAYOUT.panel.height
  Z_PANEL = INGAME_LAYOUT.panel.depth
  ACTION_BUTTON_LAYOUT = INGAME_LAYOUT.actionButtons
  OPN_OFS = INGAME_LAYOUT.handOpenOffset
  TEH_POS = INGAME_LAYOUT.handPosition
  TEH_COL = INGAME_LAYOUT.handStep
  MOH_OFS = INGAME_LAYOUT.drawTileOffset
  STH_POS = INGAME_LAYOUT.discardPosition
  STH_COL = INGAME_LAYOUT.discardStep
  STH_ROW = INGAME_LAYOUT.discardRowStep
  STH_ROT = INGAME_LAYOUT.rotatedDiscardOffset
  WAN_POS = INGAME_LAYOUT.deadWall.position
  WAN_EXPOSE_OFFSET_Y = INGAME_LAYOUT.deadWall.exposeOffsetY
  BOARD_EFFECT_POS = INGAME_LAYOUT.boardEffectPosition
  REACH_TILE_EFFECT_OFFSET = INGAME_LAYOUT.reachTileEffectOffset
  REACH_TILE_EFFECT_MOVE = INGAME_LAYOUT.reachTileEffectMove
  PAIFU_GRAPH = INGAME_LAYOUT.paifuGraph.bounds
  PAIFU_GRAPH_ROWS = INGAME_LAYOUT.paifuGraph.rows
  PAIFU_GRAPH_MELD_GAP = INGAME_LAYOUT.paifuGraph.optionMeldGap
}

function boardLocalPoint(point: { x: number; y: number }): { x: number; y: number } {
  return { x: BOARD_X + point.x, y: BOARD_Y + point.y }
}

let OPN_OFS = INGAME_LAYOUT.handOpenOffset
let TEH_POS = INGAME_LAYOUT.handPosition
let TEH_COL = INGAME_LAYOUT.handStep
let MOH_OFS = INGAME_LAYOUT.drawTileOffset
let STH_POS = INGAME_LAYOUT.discardPosition
let STH_COL = INGAME_LAYOUT.discardStep
let STH_ROW = INGAME_LAYOUT.discardRowStep
let STH_ROT = INGAME_LAYOUT.rotatedDiscardOffset
const BIPAI_MAX_COUNT = 136
const VIEWER_OPEN_POS = 4
const DEAD_WALL_COUNT = 14
const DEAD_WALL_START = BIPAI_MAX_COUNT - DEAD_WALL_COUNT
let WAN_POS = INGAME_LAYOUT.deadWall.position
let WAN_EXPOSE_OFFSET_Y = INGAME_LAYOUT.deadWall.exposeOffsetY
let BOARD_EFFECT_POS = INGAME_LAYOUT.boardEffectPosition
let REACH_TILE_EFFECT_OFFSET = INGAME_LAYOUT.reachTileEffectOffset
let REACH_TILE_EFFECT_MOVE = INGAME_LAYOUT.reachTileEffectMove
let PAIFU_GRAPH = INGAME_LAYOUT.paifuGraph.bounds
let PAIFU_GRAPH_ROWS = INGAME_LAYOUT.paifuGraph.rows
const PAIFU_GRAPH_OPTIONS = [
  { key: 'mj_opt_0', optionIndex: 0, defaultFrame: 1 },
  { key: 'mj_opt_3', optionIndex: 3, defaultFrame: 0 },
  { key: 'mj_opt_1', optionIndex: 1, defaultFrame: 2 },
  { key: 'mj_optron', optionIndex: 12, defaultFrame: 0 },
  { key: 'mj_opt_5', optionIndex: 5, defaultFrame: 2 },
  { key: 'mj_opt_2', optionIndex: 4, defaultFrame: 0 },
  { key: 'mj_optwar', optionIndex: 8, defaultFrame: 0 },
  { key: 'mj_opttip', optionIndex: 9, defaultFrame: 0 },
  { key: 'mj_opt_4', optionIndex: 2, defaultFrame: 2 },
  { key: 'mj_opt_6', optionIndex: 6, defaultFrame: 0 },
  { key: 'mj_opt_7', optionIndex: 7, defaultFrame: 0 },
] as const
let PAIFU_GRAPH_MELD_GAP = INGAME_LAYOUT.paifuGraph.optionMeldGap

/* ========================================================================
 * PaiCode → フレームインデックス変換 (PaiCode.h 準拠)
 * ======================================================================== */
export function paiToFrame(code: number): number {
  if (code <= 0) return 36          // 無効・裏牌は末尾
  const kind   = (code >> 4) & 0xF
  const number = code & 0xF
  if (kind < 3) return kind * 9 + (number - 1)  // MAN/SOU/PIN
  if (number >= 1 && number <= 4) return 27 + (number - 1)  // 風牌
  if (number >= 5 && number <= 7) return 31 + (number - 5)  // 三元牌
  return 36
}

/* ========================================================================
 * 捨て牌グリッド位置 (3列×6行 中心起点)
 * odr=0 下, odr=1 右, odr=2 上, odr=3 左
 * ======================================================================== */
function odrToLoc(odr: number, viewOdr: number): 0 | 1 | 2 | 3 {
  return ((4 + odr - viewOdr) % 4) as 0 | 1 | 2 | 3
}

function handPos(loc: 0 | 1 | 2 | 3, idx: number, isDrawTile: boolean, isOpenMode = false): { x: number; y: number } {
  const tableLoc = loc === 0 ? 4 : loc
  const offset = isDrawTile ? MOH_OFS[tableLoc] : { x: 0, y: 0 }
  const openOffset = isOpenMode ? OPN_OFS[tableLoc] : { x: 0, y: 0 }
  return boardLocalPoint({
    x: TEH_POS[tableLoc].x + TEH_COL[tableLoc].x * idx + offset.x + openOffset.x,
    y: TEH_POS[tableLoc].y + TEH_COL[tableLoc].y * idx + offset.y + openOffset.y,
  })
}

function mobileOuterHandPos(loc: 0 | 1 | 2 | 3, idx: number, _count: number, isDrawTile: boolean, scale: number): { x: number; y: number } | null {
  const bounds = mobileVisibleWorldBounds()
  if (!bounds) return null
  const centerX = (bounds.left + bounds.right) / 2
  const centerY = (bounds.top + bounds.bottom) / 2
  const inset = 12
  const drawGap = isDrawTile ? 8 * scale : 0

  if (loc === 0 || loc === 2) {
    const tileWidth = 37 * scale
    const step = loc === 0 ? tileWidth : tileWidth * MOBILE_OTHER_HAND_STEP_RATIO
    const layoutCount = loc === 0 ? MOBILE_SELF_HAND_FIXED_COUNT : MOBILE_OTHER_HAND_FIXED_COUNT
    const totalWidth = Math.max(0, layoutCount - 1) * step + tileWidth
    const startX = centerX - totalWidth / 2 + (loc === 2 ? step * MOBILE_TOP_HAND_TILE_OFFSET : 0)
    const x = loc === 2
      ? startX + Math.max(0, layoutCount - 1 - idx) * step - drawGap
      : startX + idx * step + drawGap
    const y = loc === 0
      ? bounds.bottom - 63 * scale - MOBILE_SELF_HAND_BOTTOM_INSET
      : bounds.top + inset
    return { x, y }
  }

  const tileWidth = 63 * scale
  const tileHeight = 37 * scale
  const step = tileHeight * MOBILE_OTHER_HAND_STEP_RATIO
  const layoutCount = MOBILE_OTHER_HAND_FIXED_COUNT
  const totalHeight = Math.max(0, layoutCount - 1) * step + tileHeight
  const avatarInnerGap = MOBILE_HUD_AVATAR_INSET_X + MOBILE_HUD_AVATAR_WIDTH + MOBILE_SIDE_HAND_AVATAR_GAP
  const x = loc === 1
    ? bounds.right - avatarInnerGap - tileWidth
    : bounds.left + avatarInnerGap + MOBILE_LEFT_SIDE_HAND_INNER_OFFSET
  const y = loc === 1
    ? centerY - totalHeight / 2 + MOBILE_SIDE_HAND_Y_OFFSET + Math.max(0, layoutCount - 1 - idx) * step - drawGap
    : centerY - totalHeight / 2 + MOBILE_SIDE_HAND_Y_OFFSET + idx * step + drawGap
  return { x, y }
}

function mobileContentScale(): number {
  const bounds = mobileVisibleWorldBounds()
  if (!bounds) return 1
  const visibleWidth = bounds.right - bounds.left
  const visibleHeight = bounds.bottom - bounds.top
  if (visibleWidth <= 0 || visibleHeight <= 0) return 1
  return Phaser.Math.Clamp(
    (visibleHeight / visibleWidth) / MOBILE_CONTENT_BASE_ASPECT,
    MOBILE_CONTENT_SCALE_MIN,
    1,
  )
}

function handTexture(loc: 0 | 1 | 2 | 3): { key: string; frame?: number } {
  if (loc === 0) return { key: 'hai_omote' }
  return { key: `hai_tachi_${loc}` }
}

function concealedHandTexture(loc: 0 | 1 | 2 | 3): { key: string; frame?: number } {
  return { key: `hai_tachi_${loc}` }
}

function openHandTexture(loc: 0 | 1 | 2 | 3): string {
  return loc === 0 ? 'hai_omote' : `hai_open_${loc}`
}

function handDepth(y: number, idx: number): number {
  return 10 + y * 0.01 + idx * 0.0001
}

function discardTexture(loc: 0 | 1 | 2 | 3, flag: number): string {
  const dir = flag === 2 ? (loc + 1) % 4 : loc
  return dir === 0 ? 'hai_sute' : `hai_open_${dir}`
}

function discardBasePos(loc: 0 | 1 | 2 | 3, mode: IngameLayoutMode): { x: number; y: number } {
  if (mode !== 'mobileLandscape') return STH_POS[loc]
  const offset = MOBILE_DISCARD_CENTER_INFO_OFFSET[loc]
  return { x: CENTER_INFO.x + offset.x, y: CENTER_INFO.y + offset.y }
}

function downTexture(loc: 0 | 1 | 2 | 3): string {
  return loc === 2 ? 'hai_ura_2' : loc === 1 || loc === 3 ? 'hai_ura_1' : 'hai_ura_0'
}

function discardPos(loc: 0 | 1 | 2 | 3, idx: number, flag: number, mode: IngameLayoutMode): { x: number; y: number } {
  const col = idx % DISCARD_COLS
  const row = Math.floor(idx / DISCARD_COLS)
  const base = discardBasePos(loc, mode)
  const layoutScale = mode === 'mobileLandscape'
    ? MOBILE_DISCARD_LAYOUT_SCALE * mobileContentScale()
    : 1
  let x = base.x + (STH_COL[loc].x * col + STH_ROW[loc].x * row) * layoutScale
  let y = base.y + (STH_COL[loc].y * col + STH_ROW[loc].y * row) * layoutScale
  if (flag === 2) {
    x += STH_ROT[loc].x * layoutScale
    y += STH_ROT[loc].y * layoutScale
  } else if (flag === 1) {
    const nextLoc = (loc + 1) % 4
    x += (STH_ROW[nextLoc].x - STH_COL[loc].x) * layoutScale
    y += (STH_ROW[nextLoc].y - STH_COL[loc].y) * layoutScale
  }
  const centerOffset = mobileCenterHudOffset(mode)
  x += centerOffset.x
  y += centerOffset.y
  return boardLocalPoint({ x, y })
}

function mobileMeldBasePos(loc: 0 | 1 | 2 | 3, meldScale: number): { x: number; y: number } | null {
  const handScale = (loc === 0 ? MOBILE_SELF_HAND_TILE_SCALE : MOBILE_OTHER_HAND_TILE_SCALE) * mobileContentScale()
  const handEnd = mobileOuterHandPos(loc, MOBILE_OTHER_HAND_FIXED_COUNT - 1, MOBILE_OTHER_HAND_FIXED_COUNT, false, handScale)
  if (!handEnd) return null

  const desktopHandLoc = loc === 0 ? 4 : loc
  const desktopHandEnd = {
    x: DESKTOP_INGAME_LAYOUT.handPosition[desktopHandLoc].x
      + DESKTOP_INGAME_LAYOUT.handStep[desktopHandLoc].x * (MOBILE_OTHER_HAND_FIXED_COUNT - 1),
    y: DESKTOP_INGAME_LAYOUT.handPosition[desktopHandLoc].y
      + DESKTOP_INGAME_LAYOUT.handStep[desktopHandLoc].y * (MOBILE_OTHER_HAND_FIXED_COUNT - 1),
  }
  const desktopMeldBase = DESKTOP_INGAME_LAYOUT.meldPosition[loc]
  return {
    x: handEnd.x + (desktopMeldBase.x - desktopHandEnd.x) * meldScale,
    y: handEnd.y + (desktopMeldBase.y - desktopHandEnd.y) * meldScale,
  }
}

function meldPos(loc: 0 | 1 | 2 | 3, row: number, col: number, flag: number, mode: IngameLayoutMode = 'desktop', scale = 1): { x: number; y: number } {
  const dir = flag === 2 ? ((loc + 1) % 4) as 0 | 1 | 2 | 3 : loc
  const meldLayout = DESKTOP_INGAME_LAYOUT
  const mobileBase = mode === 'mobileLandscape' ? mobileMeldBasePos(loc, scale) : null
  const base = mobileBase ?? meldLayout.meldPosition[loc]
  const stepScale = mode === 'mobileLandscape' ? scale : 1
  const rowStep = { x: meldLayout.discardRowStep[loc].x * stepScale, y: meldLayout.discardRowStep[loc].y * stepScale }
  const colStep = { x: meldLayout.discardStep[loc].x * stepScale, y: meldLayout.discardStep[loc].y * stepScale }
  const rotStep = { x: meldLayout.rotatedDiscardOffset[loc].x * stepScale, y: meldLayout.rotatedDiscardOffset[loc].y * stepScale }
  let x = base.x - rowStep.x * row - colStep.x * (col + 1)
  let y = base.y - rowStep.y * row - colStep.y * (col + 1)
  if (flag === 2) {
    x += colStep.x + rotStep.x - meldLayout.discardRowStep[dir].x * stepScale
    y += colStep.y + rotStep.y - meldLayout.discardRowStep[dir].y * stepScale
  } else if (flag === 1) {
    const nextLoc = ((loc + 1) % 4) as 0 | 1 | 2 | 3
    x += colStep.x - meldLayout.discardRowStep[nextLoc].x * stepScale
    y += colStep.y - meldLayout.discardRowStep[nextLoc].y * stepScale
  }
  return mobileBase ? { x, y } : boardLocalPoint({ x, y })
}

function meldTexture(loc: 0 | 1 | 2 | 3, flag: number, isDown: boolean): { key: string; frame?: number } {
  if (isDown) return { key: downTexture(loc) }
  const dir = flag === 2 ? ((loc + 1) % 4) as 0 | 1 | 2 | 3 : loc
  return { key: dir === 0 ? 'hai_sute' : `hai_open_${dir}` }
}

function skinTextureCandidate(key: string): string {
  return `${key}_skin`
}

function mobileDeadWallBasePos(): { x: number; y: number } | null {
  const bounds = mobileVisibleWorldBounds()
  if (!bounds) return null
  const tileWidth = 31 * MOBILE_TILE_SCALE
  const groupWidth = 6 * TEH_COL[0].x + tileWidth
  const rightAvatarLeft = bounds.right - MOBILE_HUD_AVATAR_WIDTH - MOBILE_HUD_AVATAR_INSET_X
  return {
    x: rightAvatarLeft - MOBILE_DEAD_WALL_AVATAR_GAP - groupWidth - BOARD_X,
    y: bounds.top + MOBILE_HUD_AVATAR_INSET_TOP - BOARD_Y + WAN_EXPOSE_OFFSET_Y + MOBILE_DEAD_WALL_AVATAR_Y_OFFSET + MOBILE_PLAYFIELD_OFFSET_Y,
  }
}

function deadWallPos(idx: number, mode: IngameLayoutMode): { x: number; y: number } {
  const base = mode === 'mobileLandscape' ? mobileDeadWallBasePos() ?? WAN_POS : WAN_POS
  return boardLocalPoint({
    x: base.x + (6 - Math.floor(idx / 2)) * TEH_COL[0].x,
    y: base.y - (idx % 2 === 0 ? WAN_EXPOSE_OFFSET_Y : 0),
  })
}

function paiToSerial(code: number): number {
  const kind = (code >> 4) & 0xF
  const number = code & 0xF
  if (kind >= 0 && kind < 3 && number >= 1 && number <= 9) return kind * 9 + (number - 1)
  if (kind === 3 && number >= 1 && number <= 4) return 27 + (number - 1)
  if (kind === 3 && number >= 5 && number <= 7) return 31 + (number - 5)
  return -1
}

function serialToPaiCode(serial: number): number {
  if (serial >= 0 && serial < 27) return ((Math.floor(serial / 9) & 0xF) << 4) | ((serial % 9) + 1)
  if (serial >= 27 && serial < 31) return 0x30 | (serial - 26)
  if (serial >= 31 && serial < 34) return 0x30 | (serial - 26)
  return 0
}

function hasHoraFormAfterAdd(counts: number[], handCount: number, addSerial: number): boolean {
  if (addSerial < 0 || addSerial >= 34 || counts[addSerial] >= 4) return false
  counts[addSerial]++
  const result = checkHoraCounts(counts, handCount + 1)
  counts[addSerial]--
  return result
}

function checkTempaiAfterDiscard(hand: TileState[], discardIdx: number): boolean {
  const counts = Array.from({ length: 34 }, () => 0)
  for (let idx = 0; idx < hand.length; idx++) {
    if (idx === discardIdx) continue
    const serial = paiToSerial(hand[idx].code)
    if (serial < 0) return false
    counts[serial]++
  }
  const handCount = Math.max(0, hand.length - 1)
  for (let serial = 0; serial < 34; serial++) {
    if (hasHoraFormAfterAdd(counts, handCount, serial)) return true
  }
  return false
}

function checkHoraCounts(counts: number[], handCount: number): boolean {
  const work = [...counts]
  return checkKokushi(work, handCount) || checkChitoi(work, handCount) || checkHead(work, 0, handCount)
}

function checkKokushi(counts: number[], handCount: number): boolean {
  if (handCount !== 14) return false
  const terminals = [0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33]
  let hasPair = false
  for (const serial of terminals) {
    if (counts[serial] === 2) hasPair = true
    else if (counts[serial] !== 1) return false
  }
  return hasPair
}

function checkChitoi(counts: number[], handCount: number): boolean {
  if (handCount !== 14) return false
  return counts.every(count => count === 0 || count === 2)
}

function checkHead(counts: number[], top: number, count: number): boolean {
  if (count < 2) return false
  while (top < 34 && counts[top] === 0) top++
  if (top >= 34) return false

  if (counts[top] >= 3) {
    counts[top] -= 3
    const triplet = checkHead(counts, top, count - 3)
    counts[top] += 3
    if (triplet) return true
    counts[top] -= 2
    const pair = checkMent(counts, top, count - 2)
    counts[top] += 2
    return pair
  }
  if (counts[top] === 2) {
    counts[top] -= 2
    let result = checkMent(counts, top + 1, count - 2)
    counts[top] += 2
    if (result) return true
    if (top < 27 && top % 9 < 7 && counts[top + 1] >= 2 && counts[top + 2] >= 2) {
      counts[top] -= 2
      counts[top + 1] -= 2
      counts[top + 2] -= 2
      result = checkHead(counts, top + 1, count - 6)
      counts[top] += 2
      counts[top + 1] += 2
      counts[top + 2] += 2
      return result
    }
    return false
  }
  if (top < 27 && top % 9 < 7 && counts[top + 1] >= 1 && counts[top + 2] >= 1) {
    counts[top]--
    counts[top + 1]--
    counts[top + 2]--
    const result = checkHead(counts, top, count - 3)
    counts[top]++
    counts[top + 1]++
    counts[top + 2]++
    return result
  }
  return false
}

function checkMent(counts: number[], top: number, count: number): boolean {
  if (count === 0) return true
  while (top < 34 && counts[top] === 0) top++
  if (top >= 34) return false
  if (counts[top] === 3) {
    counts[top] -= 3
    const result = checkMent(counts, top + 1, count - 3)
    counts[top] += 3
    return result
  }
  if (top >= 27 || top % 9 >= 7) return false
  if (counts[top] === 4) {
    if (counts[top + 1] <= 0 || counts[top + 2] <= 0) return false
    counts[top] -= 4
    counts[top + 1]--
    counts[top + 2]--
    const result = checkMent(counts, top + 1, count - 6)
    counts[top] += 4
    counts[top + 1]++
    counts[top + 2]++
    return result
  }
  if (counts[top] === 2) {
    if (counts[top + 1] < 2 || counts[top + 2] < 2) return false
    counts[top] -= 2
    counts[top + 1] -= 2
    counts[top + 2] -= 2
    const result = checkMent(counts, top + 1, count - 6)
    counts[top] += 2
    counts[top + 1] += 2
    counts[top + 2] += 2
    return result
  }
  if (counts[top + 1] > 0 && counts[top + 2] > 0) {
    counts[top]--
    counts[top + 1]--
    counts[top + 2]--
    const result = checkMent(counts, top + 1, count - 3)
    counts[top]++
    counts[top + 1]++
    counts[top + 2]++
    return result
  }
  return false
}

/* ========================================================================
 * ゲームステート
 * ======================================================================== */
interface TileState {
  code: number
  bipaiIndex?: number
  isSelected: boolean
}

interface WaitTileGuideEntry {
  code: number
  rest: number
  furiten: boolean
}

interface DiscardState {
  code: number
  bipaiIndex?: number
  isReach: boolean
}

interface DiscardFlightOrigin {
  x: number
  y: number
  scaleX: number
  scaleY: number
  depth: number
}

interface PendingDiscardState {
  odr: number
  bipaiIndex: number
  isReach: boolean
  playReachFeedback: boolean
  flightOrigin?: DiscardFlightOrigin
  animateDiscard: boolean
}

interface ActiveDiscardFlight {
  sprite: Phaser.GameObjects.Image
  target: Phaser.GameObjects.Image
  tween?: Phaser.Tweens.Tween
}

interface PaiInfoMsgState {
  bIniKyo: boolean
  openPos: number
  tiles: TileState[]
}

interface ResyncHandSnapshot {
  openPos: number
  tiles: TileState[]
}

interface ActionPromptState {
  actionSeq: number
  deadlineAt: number
  localDeadlineAt: number
  seatOrder: number
  playerMode: string
  promptSerial: number
}

interface MeldTileState {
  code: number
  flag: number
  isDown?: boolean
}

interface MeldState {
  action: Act
  tiles: MeldTileState[]
}

interface PaifuGraphDrawState {
  code: number
  small?: boolean
  noteFrame?: number
}

interface HoraYakuState {
  name: string
  fan: number
  code?: number
  isYakuman?: boolean
  tip?: number
}

interface PlayerHoraState {
  pinType: number
  isHora: boolean
  isHoju: boolean
  isTempai: boolean
  isRichi: boolean
  yaku: HoraYakuState[]
  totalFu?: number
  totalFan?: number
  totalTen?: number
  tipBal?: number
}

interface PaifuGraphRoundState {
  kyokuCnt: number
  left: number
  ribo: number
  renchan: number
  dice: number[]
  waremeOdr: number
  roomOption: string
  dora: number[]
  uraDora: number[]
}

interface PlayerState {
  hand: TileState[]       // 手牌
  discards: DiscardState[] // 捨て牌
  melds: MeldState[]      // 副露
  flowers: TileState[]    // 花牌
  isReach: boolean
  reachDiscardCarry: boolean
  score: number
  yakitori: boolean
  tip: number
  hora?: PlayerHoraState
  pix: string
  name: string
  level: string
  rating?: number
  avatarId?: string
  avatarUrl?: string
  fallbackAvatarUrl?: string
  majakTitle?: number
  trickTitle?: number
  richiEffect?: number
  customCostume?: number
  customCostumeType?: number
  sex?: string
  isHost?: boolean
  isProxy?: boolean
}

function createEmptyPlayerState(): PlayerState {
  return {
    hand: [], discards: [], melds: [], flowers: [], isReach: false, reachDiscardCarry: false,
    score: Number.NaN, yakitori: false, tip: 0, pix: '', name: '', level: '', isProxy: false,
  }
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

function readOptionalTitleCode(value: unknown): number | undefined {
  return value === undefined || value === null || value === '' ? undefined : readTitleCode(value)
}

function isViewerPacket(data: Record<string, unknown>) {
  const playerType = data.k57e ?? data.playerType ?? data.type
  return playerType === 'viewer' || playerType === 'v5e' || playerType === 2 || playerType === '2'
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value != null && typeof value === 'object'
}

function asFiniteNumber(value: unknown): number | undefined {
  const number = Number(value)
  return Number.isFinite(number) ? number : undefined
}

function asBoolean(value: unknown): boolean | undefined {
  if (typeof value === 'boolean') return value
  if (typeof value === 'number') return Number.isFinite(value) ? value !== 0 : undefined
  if (typeof value === 'string') {
    const normalized = value.trim().toLowerCase()
    if (normalized === 'true' || normalized === '1') return true
    if (normalized === 'false' || normalized === '0') return false
  }
  return undefined
}

/* ========================================================================
 * GameScene
 * ======================================================================== */
export default class GameScene extends Phaser.Scene {
  /* Phaser オブジェクト */
  private handSprites: Phaser.GameObjects.Image[][] = [[], [], [], []]
  private mobileOpponentHandCountTexts: Array<Phaser.GameObjects.Text | undefined> = [undefined, undefined, undefined, undefined]
  private suteSprites: Phaser.GameObjects.Image[][] = [[], [], [], []]
  private meldSprites: Phaser.GameObjects.Image[][] = [[], [], [], []]
  private deadWallSprites: Phaser.GameObjects.Image[] = []
  private actionPanelSprite?: Phaser.GameObjects.Image
  private actionButtonSprites = new Map<string, Phaser.GameObjects.Sprite>()
  private mobileActionButtonsVisible = false
  private horaErrorSprite?: Phaser.GameObjects.Sprite
  private boardEffectSprites: Phaser.GameObjects.Image[] = []
  private reachTileEffectSprites: Phaser.GameObjects.Image[] = []
  private hoverCursor?: Phaser.GameObjects.Image
  private selectedCursor?: Phaser.GameObjects.Image
  private tenpaiMarkerSprites: Phaser.GameObjects.Image[] = []
  private waitTileGuideContainer?: Phaser.GameObjects.Container
  private actionBtns: Phaser.GameObjects.Sprite[] = []
  private paifuGraphLayer?: Phaser.GameObjects.Container
  private paifuGraphBody?: Phaser.GameObjects.Container
  private paifuGraphObjects: Phaser.GameObjects.GameObject[] = []
  private boardMaskGraphics?: Phaser.GameObjects.Graphics
  private boardMask?: Phaser.Display.Masks.GeometryMask
  private boardBackground?: Phaser.GameObjects.Image
  private dragonOverlayBg?: Phaser.GameObjects.Image
  private centerInfoBg?: Phaser.GameObjects.Image
  private paifuGraphInitialHands: TileState[][] = [[], [], [], []]
  private paifuGraphDraws: PaifuGraphDrawState[][] = [[], [], [], []]
  private paifuGraphDiscards: DiscardState[][] = [[], [], [], []]
  private paifuGraphRound: PaifuGraphRoundState = {
    kyokuCnt: 0, left: 70, ribo: 0, renchan: 0, dice: [], waremeOdr: -1, roomOption: '', dora: [], uraDora: [],
  }
  private actionOfferByName = new Map<string, { code: Act; bipaiIndex: number[] }>()
  private actionChoicesByName = new Map<string, Array<{ code: Act; bipaiIndex: number[] }>>()
  private pendingActionChoice: { def: { act: string; code: Act }; acts: string[]; choices: Array<{ code: Act; bipaiIndex: number[] }> } | null = null
  private currentHoraErrorReason = ''
  private selectedIdx = -1
  private canDiscardOnTileClick = false
  private autoDiscardTimer?: Phaser.Time.TimerEvent
  private actionResponseTimer?: Phaser.Time.TimerEvent
  private autoControlTimer?: Phaser.Time.TimerEvent
  private timeWarningTimers: Phaser.Time.TimerEvent[] = []
  private currentActionSeatOrder: number | null = null
  private actionPromptSerial = 0
  private flowTraceSerial = 0
  private actionSendInFlight = false
  private gameResyncInFlight = false
  private pendingAction: { seatOrder: number; action: number; actionSeq?: number } | null = null
  private currentActionPrompt: ActionPromptState | null = null
  private lastLiveHistoryAppliedAt = 0
  private keyboardActionIndex = -1
  private keyboardHandler?: (event: KeyboardEvent) => void
  private contextMenuHandler?: (event: MouseEvent) => void
  private readonly knownPai = new Map<number, number>()
  private readonly pendingDiscardsByBipaiIndex = new Map<number, PendingDiscardState>()
  private activeDiscardFlights: Array<ActiveDiscardFlight | undefined> = [undefined, undefined, undefined, undefined]
  private lastDiscardOdr: number | null = null
  private readonly appliedActionKeys = new Set<string>()
  private paiInfoQueue: PaiInfoMsgState[] = []
  private pendingResyncHandSnapshot?: ResyncHandSnapshot
  private latestActionPaiInfoTiles: TileState[][] = [[], [], [], []]
  private currentActionOffers: string[] = []

  /* ゲームステート */
  private players: PlayerState[] = Array.from({ length: 4 }, createEmptyPlayerState)
  private myOdr = 0   // 自分の順番 (0=南, 1=西, 2=北, 3=東)
  private roomPosToOdr: number[] = [0, 1, 2, 3]
  private memberOdrById = new Map<string, number>()
  private hanchanOrderInitialized = false
  private chicha = 0
  // currentOdr: 将来ターン演出に使用
  private roomId = ''
  private isReplay = false
  private isViewer = false
  private layoutMode: IngameLayoutMode = 'desktop'
  private replayPaifuData: unknown
  private replayPaifuApplied = false
  private isReplayApplyingHistory = false
  private skipInitialRoomEnter = false
  private replayHandOpen = true
  private signalRHandlers: Array<{ cmd: string; handler: SignalR.MessageHandler }> = []
  private acceptingSignalR = false
  private replayRotateHandler?: EventListener
  private replayHandOpenHandler?: EventListener
  private replayGraphHandler?: EventListener
  private autoControlHandler?: EventListener
  private autoControl: GameAutoControlState = { prox: false, autoTap: false, autoPass: false, autoHora: false }
  private inputConfig = { nSelPasKey: 0 }
  private customBgId = 0
  private customBoardType = 0
  private currentBgmSkinId: number | undefined
  private currentRoundUsesTengokuBgm = false
  private currentRoundIsCarnival = false
  private mobileHandSummaryStateKey = ''
  private mobileCenterInfoLayoutKey = ''

  constructor() {
    super({ key: 'GameScene' })
  }

  private tileScale(): number {
    return this.layoutMode === 'mobileLandscape' ? MOBILE_TILE_SCALE : 1
  }

  private handTileScale(odr: number, loc: 0 | 1 | 2 | 3): number {
    if (this.layoutMode !== 'mobileLandscape') return 1
    void odr
    const baseScale = loc === 0 && !this.isReplay ? MOBILE_SELF_HAND_TILE_SCALE : MOBILE_OTHER_HAND_TILE_SCALE
    return baseScale * mobileContentScale()
  }

  private meldTileScale(odr: number, loc: 0 | 1 | 2 | 3): number {
    void odr
    void loc
    return this.tileScale()
  }

  private discardTileScale(): number {
    return this.layoutMode === 'mobileLandscape'
      ? MOBILE_DISCARD_TILE_SCALE * mobileContentScale()
      : this.tileScale()
  }

  private shouldUseMobileOpponentHandSummary(odr: number, loc: 0 | 1 | 2 | 3): boolean {
    void odr
    void loc
    return false
  }

  private soundSkinOptions(skinId = this.currentBgmSkinId): { skinId?: number } {
    return skinId ? { skinId } : {}
  }

  init(data: CreateGameOptions & { roomId?: string; myOdr?: number }) {
    this.layoutMode = data.layoutMode ?? 'desktop'
    applyIngameLayout(this.layoutMode)
    this.roomId = data.roomId ?? ''
    this.myOdr  = data.myOdr  ?? 0
    this.roomPosToOdr = [0, 1, 2, 3]
    this.memberOdrById.clear()
    this.hanchanOrderInitialized = false
    this.isReplay = data.mode === 'replay'
    this.isViewer = Boolean(data.isViewer)
    this.customBgId = Number(data.customBgId ?? 0)
    this.customBoardType = Number(data.customBoardType ?? 0)
    this.replayPaifuData = data.paifu
    this.replayPaifuApplied = false
    this.isReplayApplyingHistory = false
    this.pendingResyncHandSnapshot = undefined
    this.skipInitialRoomEnter = Boolean(data.skipInitialRoomEnter)
    this.inputConfig = { nSelPasKey: data.inputConfig?.nSelPasKey === 1 ? 1 : 0 }
    if (DEBUG_GAME) console.info('[GameScene] init', {
      roomId: this.roomId,
      myOdr: this.myOdr,
      mode: data.mode,
      layoutMode: this.layoutMode,
      skipInitialRoomEnter: this.skipInitialRoomEnter,
      nSelPasKey: this.inputConfig.nSelPasKey,
    })
    if (Array.isArray(data.players)) {
      data.players.forEach(player => this.mergePlayerInfo(player))
    }
    this.paifuGraphRound.roomOption = data.roomOption ?? this.paifuGraphRound.roomOption
  }

  create() {
    this.createBoardMask()

    /* ── ボード背景 mj_board.png (789×704) at (5,31) ── */
    this.boardBackground = this.add.image(BOARD_X + BOARD_W / 2, BOARD_Y + BOARD_H / 2, this.resolveSkinTextureKey('mj_board')).setDepth(-100)
    if (this.layoutMode === 'mobileLandscape') {
      this.clipToBoard(this.boardBackground.setScale(MOBILE_BOARD_BACKGROUND_SCALE))
    }
    if (this.textures.exists('mj_taku_dragon_skin')) {
      this.dragonOverlayBg = this.clipToBoard(this.add.image(BOARD_X + DRAGON_OVERLAY.x, BOARD_Y + DRAGON_OVERLAY.y, 'mj_taku_dragon_skin')
        .setOrigin(0, 0)
        .setDepth(-90)
        .setBlendMode(Phaser.BlendModes.ADD))
    }

    /* ── サイドバー mj_sideBg.png (225×704) at (794,31) ── */
    this.add.image(SIDE_PANEL.x + SIDE_PANEL.width / 2, SIDE_PANEL.y + SIDE_PANEL.height / 2, this.resolveSkinTextureKey('mj_sideBg')).setDepth(-100)

    /* ── ゲーム情報エリア mj_h_bg.png (265×161) at board-local (262,275) ── */
    this.centerInfoBg = this.clipToBoard(this.add.image(BOARD_X + CENTER_INFO.x + CENTER_INFO.width / 2, BOARD_Y + CENTER_INFO.y + CENTER_INFO.height / 2, this.resolveSkinTextureKey('mj_h_bg')).setDepth(-50))
    this.updateCenterInfoLayout()

    /* ── CMJGameWnd::PutPanel: PANELMODE_VIEW uses mj_watchBoard; PLAY uses mj_uiBoard ── */
    if (!this.isReplay) {
      const panelKey = this.isViewer ? 'mj_watchBoard' : 'mj_uiBoard'
      this.actionPanelSprite = this.clipToBoard(this.add.image(BOARD_X + X_PANEL + W_PANEL / 2, BOARD_Y + Y_PANEL + H_PANEL / 2, this.resolveSkinTextureKey(panelKey))
        .setDisplaySize(W_PANEL, H_PANEL)
        .setDepth(Z_PANEL)
        .setVisible(this.layoutMode !== 'mobileLandscape'))
      if (!this.isViewer) this.createActionButtons()
    }

    /* ── UIScene 起動 ── */
    this.scene.launch('UIScene', { gameScene: this, myOdr: this.myOdr, layoutMode: this.layoutMode, isViewer: this.isViewer, customBgId: this.customBgId, customBoardType: this.customBoardType })

    /* ── SignalR イベント登録 ── */
    this.acceptingSignalR = true
    this.setupSignalR()
    this.notifyGameClientReady()
    this.requestInitialRoomState()
    this.setupReplayControlEvents()
    this.setupAutoControlEvents()
    this.setupKeyboardEvents()
    this.setupContextMenuEvents()
    this.events.once(Phaser.Scenes.Events.SHUTDOWN, () => this.teardownSceneResources())
    this.events.once(Phaser.Scenes.Events.DESTROY, () => this.teardownSceneResources())
  }

  private teardownSceneResources() {
    this.acceptingSignalR = false
    this.clearAllDiscardFlights()
    this.teardownSignalR()
    this.teardownReplayControlEvents()
    this.teardownAutoControlEvents()
    this.teardownKeyboardEvents()
    this.teardownContextMenuEvents()
    this.boardMaskGraphics?.destroy()
    this.boardMaskGraphics = undefined
    this.boardMask = undefined
  }

  private createBoardMask() {
    this.boardMaskGraphics?.destroy()
    const graphics = this.make.graphics({ x: 0, y: 0 })
    graphics.fillStyle(0xffffff)
    const maskX = this.layoutMode === 'mobileLandscape' ? 0 : BOARD_X
    const maskW = this.layoutMode === 'mobileLandscape' ? BOARD_X + BOARD_W : BOARD_W
    graphics.fillRect(maskX, BOARD_Y, maskW, BOARD_H)
    this.boardMaskGraphics = graphics
    this.boardMask = graphics.createGeometryMask()
  }

  private clipToBoard<T extends Phaser.GameObjects.GameObject>(obj: T): T {
    if (this.boardMask) {
      (obj as T & { setMask: (mask: Phaser.Display.Masks.GeometryMask) => T }).setMask(this.boardMask)
    }
    return obj
  }

  private updateCenterInfoLayout() {
    const offset = mobileCenterHudOffset(this.layoutMode)
    const centerX = BOARD_X + CENTER_INFO.x + CENTER_INFO.width / 2 + offset.x
    const centerY = BOARD_Y + CENTER_INFO.y + CENTER_INFO.height / 2 + offset.y
    if (this.boardBackground) {
      this.boardBackground.setPosition(
        this.layoutMode === 'mobileLandscape' ? centerX : BOARD_X + BOARD_W / 2 + offset.x,
        this.layoutMode === 'mobileLandscape' ? centerY : BOARD_Y + BOARD_H / 2 + offset.y,
      )
    }
    if (this.dragonOverlayBg) {
      if (this.layoutMode === 'mobileLandscape') {
        this.dragonOverlayBg
          .setOrigin(0.5, 0.5)
          .setScale(0.25)
          .setDepth(-90)
          .setPosition(centerX, centerY)
      } else {
        this.dragonOverlayBg
          .setOrigin(0, 0)
          .setScale(1)
          .setPosition(BOARD_X + DRAGON_OVERLAY.x, BOARD_Y + DRAGON_OVERLAY.y)
      }
    }
    if (!this.centerInfoBg) return
    this.centerInfoBg.setPosition(centerX, centerY)
  }

  /* ======================================================================
   * SignalR イベント
   * ====================================================================== */
  private setupSignalR() {
    this.teardownSignalR()

    /* smmc4e — 牌情報リスト (レガシー SendPaiInfo / ProcessCommand_PaiInfoList) */
    const handlePaiInfo: SignalR.MessageHandler = data => {
      if (!this.canHandleSignalR()) return
      const openPos = Number(data.openPos ?? this.myOdr)
      const isPlayerOpenPos = openPos >= 0 && (openPos < this.players.length || openPos === VIEWER_OPEN_POS)
      const isInit = Boolean(data.bInit ?? data.init)
      const isResyncSnapshot = Boolean(data.resyncSnapshot)
      const pai = Array.isArray(data.pai) ? data.pai as Array<Record<string, unknown>> : []
      if (DEBUG_GAME) console.info('[GameScene] smmc4e PaiInfo', {
        openPos,
        isInit,
        paiCount: pai.length,
        previousMyOdr: this.myOdr,
      })
      const tiles = pai.map(p => {
        const code = Number(p.code ?? 0)
        const bipaiIndex = Number(p.idx ?? p.bipaiIndex ?? -1)
        return { code, bipaiIndex: bipaiIndex >= 0 ? bipaiIndex : undefined, isSelected: false }
      })
      tiles.forEach(tile => {
        if (tile.bipaiIndex !== undefined && tile.bipaiIndex >= 0) this.knownPai.set(tile.bipaiIndex, tile.code)
      })
      const currentHand = (Array.isArray(data.currentHand) ? data.currentHand as Array<Record<string, unknown>> : []).map(p => {
        const code = Number(p.code ?? 0)
        const bipaiIndex = Number(p.idx ?? p.bipaiIndex ?? -1)
        return { code, bipaiIndex: bipaiIndex >= 0 ? bipaiIndex : undefined, isSelected: false }
      })
      this.logDiscardProbe('recv smmc4e', {
        openPos,
        isInit,
        paiCount: tiles.length,
        sample: tiles.slice(0, 16).map(tile => ({ idx: tile.bipaiIndex, code: tile.code })),
        queueLengthBeforePush: this.paiInfoQueue.length,
        pendingDiscardIndexes: [...this.pendingDiscardsByBipaiIndex.keys()],
      })

      if (isResyncSnapshot && openPos >= 0 && openPos < this.players.length && currentHand.length > 0) {
        tiles.forEach(tile => {
          if (tile.bipaiIndex !== undefined && tile.bipaiIndex >= 0) this.knownPai.set(tile.bipaiIndex, tile.code)
        })
        this.paiInfoQueue = []
        this.pendingDiscardsByBipaiIndex.clear()
        this.pendingResyncHandSnapshot = { openPos, tiles: currentHand }
        this.applyResyncHandSnapshot(this.pendingResyncHandSnapshot)
        return
      }

      if (isPlayerOpenPos && tiles.length > 0) this.pushPaiInfo(isInit, openPos, tiles)
      if (!isInit && this.pendingDiscardsByBipaiIndex.size > 0) {
        tiles.forEach(tile => {
          if (tile.bipaiIndex !== undefined && tile.bipaiIndex >= 0) this.knownPai.set(tile.bipaiIndex, tile.code)
        })
        this.flushPendingDiscards()
      }
    }
    this.onSignalR('smmc4e', handlePaiInfo)

    const handleMemberList: SignalR.MessageHandler = data => {
      if (!this.canHandleSignalR()) return
      const count = Number(data.k25e ?? data.count ?? 0)
      const hostPix = String(data.k50e ?? data.roomHost ?? '')
      const legacyMembers = Array.from({ length: count }, (_, index) => ({
        pix: data[`k3e${index}`],
        name: data[`k8e${index}`] ?? data[`mjkk34e${index}`] ?? data[`k3e${index}`],
        avatarId: data[`k7e${index}`],
        sex: data[`k11e${index}`],
        playerType: data[`k57e${index}`],
        playerPos: data[`k58e${index}`],
        seatPos: data[`k58e${index}`],
        rating: data[`k31e${index}`],
        slevel: data[`k32e${index}`],
        majakTitle: data[`mjkk47e${index}`],
        trickTitle: data[`mjkk46e${index}`],
        richiEffect: data[`mjkk54e${index}`] ?? data[`richiEffect${index}`],
        customCostume: data[`mjkk136e${index}`],
        customCostumeType: data[`mjkk137e${index}`],
        isHost: hostPix !== '' && String(data[`k3e${index}`] ?? '') === hostPix,
        isProxy: data[`isProxy${index}`] ?? data[`proxy${index}`] ?? data[`isOutPlayer${index}`],
      })).filter(member => member.pix != null && String(member.pix) !== '')
      const members = Array.isArray(data.members) && data.members.length > 0
        ? data.members as Array<Record<string, unknown>>
        : legacyMembers
      members.filter(member => !isViewerPacket(member)).forEach(member => this.mergePlayerInfo(member))
      if (hostPix) this.applyHostPix(hostPix)
      this.emitToUiScene('stateUpdate', { players: this.players, viewOdr: this.myOdr })
    }
    this.onSignalR('c16e', handleMemberList)

    /* playing — レガシー commandGamePlay */
    const handleGamePlay: SignalR.MessageHandler = data => {
      if (!this.canHandleSignalR()) return
      const playType = String(data.playType ?? '')
      this.traceGameFlow('rx playing', {
        playType,
        seatOrder: data.seatOrder,
        playerMode: data.playerMode,
        action: data.action,
        actionSeq: data.actionSeq,
        actFlags: data.actFlags,
        actions: data.actions,
        tapCandidates: data.tapCandidates,
      })
      if (DEBUG_GAME) console.info('[GameScene] playing packet', {
        playType,
        myOdr: this.myOdr,
        seatOrder: data.seatOrder,
        playerMode: data.playerMode,
        actFlags: data.actFlags,
        action: data.action,
        actions: data.actions,
        tapCandidates: data.tapCandidates,
      })
      const isInitKyokuPacket = playType === 'MJPID_INIKYO'
      const action = Number(data.action ?? -1)
      const actionSeatOrder = Number(data.seatOrder ?? data.order ?? -1)
      const actionBipaiIndex = Array.isArray(data.bipaiIndex) ? data.bipaiIndex.map(Number) : []
      if (playType === 'MJPID_ACTION' && (action === Act.Tap || action === Act.Ric)) {
        this.logDiscardProbe('recv discard ACTION before PaiInfo pop', {
          action,
          actionSeatOrder,
          bipaiIndex: actionBipaiIndex,
          actionSeq: data.actionSeq,
          paiInfoQueue: this.paiInfoQueue.map(msg => ({ ini: msg.bIniKyo, openPos: msg.openPos, count: msg.tiles.length })),
          hand: this.handProbe(actionSeatOrder, actionBipaiIndex[0]),
        })
      }
      if (playType === 'MJPID_ACTION') this.popPaiInfo(false)
      if (playType && playType !== 'MJPID_INIHAN') this.emitGameSync(false, 'game-content-ready')
      if (playType === 'MJPID_INIHAN') {
        this.chicha = Number(data.chicha ?? data.nChicha ?? this.chicha)
        this.applyHanchanOrder(data)
        const memberInfo = Array.isArray(data.memberInfo) ? data.memberInfo : []
        memberInfo.forEach((member, odr) => {
          if (!isRecord(member)) return
          this.mergePlayerInfoAtOdr(odr, member)
        })
        this.resolveMyOdrFromPlayers()
        this.emitToUiScene('stateUpdate', { players: this.players, viewOdr: this.myOdr })
        return
      }
      if (isInitKyokuPacket) {
        this.clearLiveActionState('init kyoku')
        const kyokuCnt = Number(data.kyokuCnt ?? 0)
        const oyaOrder = ((this.chicha + kyokuCnt) % this.players.length + this.players.length) % this.players.length
        const dice = Array.isArray(data.dice) ? data.dice.map(Number) : []
        this.resetRoundState(false)
        const points = Array.isArray(data.memberPoints) ? data.memberPoints as unknown[] : []
        const yakitori = Array.isArray(data.yakitori) ? data.yakitori as unknown[] : []
        const tip = Array.isArray(data.tip) ? data.tip as unknown[] : []
        for (let odr = 0; odr < this.players.length; odr++) {
          if (odr < points.length) this.players[odr].score = Number(points[odr] ?? this.players[odr].score)
          const yakitoriValue = asBoolean(yakitori[odr])
          if (yakitoriValue !== undefined) this.players[odr].yakitori = yakitoriValue
          const tipValue = asFiniteNumber(tip[odr])
          if (tipValue !== undefined) this.players[odr].tip = tipValue
        }
        this.paifuGraphRound = {
          kyokuCnt,
          left: Number(data.leftCount ?? 70),
          ribo: Number(data.riboCnt ?? 0),
          renchan: Number(data.renChanCnt ?? 0),
          dice,
          waremeOdr: Number(data.waremeOdr ?? -1),
          roomOption: String(data.roomOption ?? data.k46e ?? this.paifuGraphRound.roomOption ?? ''),
          dora: [],
          uraDora: [],
        }
        this.popPaiInfo(true, oyaOrder, dice)
        this.redrawDeadWall()
        if (!this.shouldSuppressLivePlayback()) {
          this.playRoundStartSounds(data)
          this.playRoundBgm(data, kyokuCnt)
        }
        this.emitToUiScene('stateUpdate', {
          players: this.players,
          kyoku: this.formatKyoku(kyokuCnt),
          kyokuCnt,
          chicha: this.chicha,
          oyaOrder,
          left: Number(data.leftCount ?? 70),
          ribo: Number(data.riboCnt ?? 0),
          renchan: Number(data.renChanCnt ?? 0),
          dice: dice.length > 0 ? dice : undefined,
          waremeOdr: Number(data.waremeOdr ?? -1),
          viewOdr: this.myOdr,
          roundStart: true,
          activeTurnOdr: this.isReplayApplyingHistory ? undefined : (Number.isFinite(oyaOrder) ? oyaOrder : 0),
          preserveTurnMark: this.isReplayApplyingHistory,
        })
        this.ensureConcealedOpponentHands()
        this.emitToUiScene('turnChange', {
          odr: Number.isFinite(oyaOrder) ? oyaOrder : 0,
          viewOdr: this.myOdr,
        })
        this.emitGameSync(false, 'initial-kyoku-ready')
        return
      }
      if (playType === 'MJPID_ENDKYO') {
        this.canDiscardOnTileClick = false
        this.currentActionSeatOrder = null
        this.currentActionPrompt = null
        this.clearAutoDiscardTimer()
        this.clearActionResponseTimer()
        this.clearAutoControlTimer()
        this.clearTimeWarningTimers()
        this.clearActionButtons()
        this.applyKyoResultState(data)
        this.playKyoResultHoraEffect(data)
        this.paifuGraphRound.dora = Array.isArray(data.dora) ? data.dora.map(Number) : []
        this.paifuGraphRound.uraDora = Array.isArray(data.uraDora) ? data.uraDora.map(Number) : []
        this.redrawDeadWall()
        this.redrawPaifuGraphContent()
        this.emitToUiScene('stateUpdate', { players: this.players, viewOdr: this.myOdr })
        this.emitToUiScene('kyoResult', data)
        return
      }

      const actionOffers = this.extractActionOffers(data)
      if (playType === 'MJPID_ACTIONS') {
        const seatOrder = Number(data.seatOrder ?? this.myOdr)
        const hasActionSeatOrder = Number.isInteger(seatOrder) && seatOrder >= 0 && seatOrder < this.players.length
        const isForLocalPlayer = !this.isViewer && hasActionSeatOrder && seatOrder === this.myOdr
        const actionSeq = Number(data.actionSeq ?? 0)
        const serverNow = Number(data.serverNow ?? 0)
        const deadlineAt = Number(data.deadlineAt ?? 0)
        const timeLimitMs = Math.max(0, Number(data.timeLimit ?? 0) * 1000)
        const remainingMs = Number.isFinite(serverNow) && serverNow > 0 && Number.isFinite(deadlineAt) && deadlineAt > 0
          ? Math.max(0, deadlineAt - serverNow)
          : timeLimitMs
        const playerMode = String(data.playerMode ?? '')
        const isRepeatedCurrentPrompt = isForLocalPlayer
          && this.currentActionPrompt !== null
          && Number.isFinite(actionSeq)
          && actionSeq > 0
          && this.currentActionPrompt.actionSeq === actionSeq
          && this.currentActionPrompt.seatOrder === seatOrder
          && this.currentActionPrompt.playerMode === playerMode
          && performance.now() < this.currentActionPrompt.localDeadlineAt
        if (isRepeatedCurrentPrompt) {
          this.traceGameFlow('ignore repeated ACTIONS prompt', { seatOrder, actionSeq, playerMode, actionSendInFlight: this.actionSendInFlight })
          return
        }
        if (this.actionSendInFlight && isForLocalPlayer) {
          this.clearPendingAction('local ACTIONS arrived while send pending', { seatOrder, actionSeq, playerMode })
        }
        this.currentActionOffers = actionOffers
        this.currentHoraErrorReason = isForLocalPlayer ? String(data.horaErrorReason ?? '') : ''
        if (this.currentHoraErrorReason) this.emitHoraErrorStatus(this.currentHoraErrorReason)
        const isTurnMode = playerMode === 'Turn'
        if (isForLocalPlayer) this.actionPromptSerial++
        const actionPromptSerial = this.actionPromptSerial
        this.currentActionPrompt = isForLocalPlayer && Number.isFinite(actionSeq) && actionSeq > 0 && remainingMs > 0
          ? { actionSeq, deadlineAt, localDeadlineAt: performance.now() + remainingMs, seatOrder, playerMode, promptSerial: actionPromptSerial }
          : null
        this.clearTimeWarningTimers()
        this.currentActionSeatOrder = isForLocalPlayer ? seatOrder : null
        this.canDiscardOnTileClick = isForLocalPlayer && isTurnMode && actionOffers.includes('Tap')
        if (!this.canDiscardOnTileClick) this.selectedIdx = -1
        if (this.canDiscardOnTileClick) this.reconcileTapCandidates(seatOrder)
        if (isForLocalPlayer) this.redrawHand(this.myOdr)
        this.clearActionResponseTimer()
        if (isForLocalPlayer && !this.canSendCurrentPrompt('expired on receive')) return
        this.traceGameFlow('resolve ACTIONS', {
          seatOrder,
          isForLocalPlayer,
          playerMode,
          actionSeq,
          remainingMs,
          actionOffers,
          canDiscardOnTileClick: this.canDiscardOnTileClick,
        })
        if (this.canDiscardOnTileClick) {
          this.scheduleAutoDiscard(Number(data.timeLimit ?? 0), actionPromptSerial)
        } else {
          this.clearAutoDiscardTimer()
        }
        const hasInputWarningMode = this.canDiscardOnTileClick
          || (playerMode !== 'Kyo' && playerMode !== 'Aga' && actionOffers.some(action => action !== 'Pass'))
        if (isForLocalPlayer && remainingMs > 0 && hasInputWarningMode && !this.shouldSuppressLivePlayback()) this.scheduleTimeWarnings(remainingMs, actionPromptSerial)
        const shouldStartActionPromptTimer = isForLocalPlayer && remainingMs > 0 && hasInputWarningMode
        if (shouldStartActionPromptTimer && !isTurnMode) {
          this.emitToUiScene('actionPromptStart', {
            timeLimit: remainingMs,
            viewOdr: this.myOdr,
          })
        }
        if (DEBUG_GAME) console.info('[GameScene] MJPID_ACTIONS resolved', {
          seatOrder,
          myOdr: this.myOdr,
          actionOffers,
          canDiscardOnTileClick: this.canDiscardOnTileClick,
          isForLocalPlayer,
          playerMode,
          actFlags: data.actFlags,
        })
        if (DEBUG_GAME && Number.isFinite(seatOrder) && seatOrder !== this.myOdr) {
          console.info('[GameScene] MJPID_ACTIONS received for another player', { seatOrder, myOdr: this.myOdr, data })
        }
        if (!isForLocalPlayer) {
          this.clearActionButtons()
        }
        if (isForLocalPlayer && playerMode === 'Kyo') {
          this.canDiscardOnTileClick = false
          this.clearActionButtons()
          window.dispatchEvent(new CustomEvent(KYO_RESULT_ACTION_EVENT, {
            detail: {
              roomId: this.roomId,
              seatOrder: Number.isFinite(seatOrder) ? seatOrder : this.myOdr,
              timeLimit: Number(data.timeLimit ?? 0),
              actionSeq: this.currentActionPrompt?.actionSeq ?? actionSeq,
              localDeadlineAt: this.currentActionPrompt?.localDeadlineAt,
            },
          }))
          return
        }

        if (isForLocalPlayer && playerMode === 'Aga') {
          this.canDiscardOnTileClick = false
          this.clearActionButtons()
          window.dispatchEvent(new CustomEvent(ASK_END_SET_EVENT, {
            detail: {
              roomId: this.roomId,
              seatOrder: Number.isFinite(seatOrder) ? seatOrder : this.myOdr,
              actionSeq: this.currentActionPrompt?.actionSeq ?? actionSeq,
              localDeadlineAt: this.currentActionPrompt?.localDeadlineAt,
            },
          }))
          return
        }
        if (isForLocalPlayer && !this.canDiscardOnTileClick && actionOffers.includes('Pass')) {
          this.scheduleDefaultPass(actionOffers, Number(data.timeLimit ?? 0), actionPromptSerial)
        }
        if (isTurnMode) {
          if (isForLocalPlayer && !this.shouldSuppressLivePlayback()) playMajakSid(SID_TURN, this.soundSkinOptions())
          this.ensureTurnDrawTile(Number.isFinite(seatOrder) ? seatOrder : this.myOdr)
          this.traceGameFlow('emit turnChange', {
            seatOrder,
            isForLocalPlayer,
            actionSeq,
            timeLimit: data.timeLimit,
          })
          this.emitToUiScene('turnChange', {
            odr: Number.isFinite(seatOrder) ? seatOrder : this.myOdr,
            viewOdr: this.myOdr,
          })
          if (shouldStartActionPromptTimer) {
            this.emitToUiScene('actionPromptStart', {
              timeLimit: remainingMs,
              viewOdr: this.myOdr,
            })
          }
        }
        if (actionOffers.length > 0 && this.isLocalPlayerOdr(this.currentActionSeatOrder)) {
          this.showActionButtons(actionOffers)
          this.scheduleAutoControl(actionOffers, actionPromptSerial)
        }
      }
      if (playType !== 'MJPID_ACTIONS' && actionOffers.length > 0 && this.isLocalPlayerOdr(this.currentActionSeatOrder)) {
        this.showActionButtons(actionOffers)
        this.scheduleAutoControl(actionOffers)
      }

      if (action >= 0) {
        const actionKey = this.actionPacketKey(data, actionSeatOrder, action)
        if (this.appliedActionKeys.has(actionKey)) {
          this.traceGameFlow('drop duplicate ACTION', { actionKey, actionSeatOrder, action, actionSeq: data.actionSeq })
          this.completePendingActionIfMatched(actionSeatOrder, action, Number(data.actionSeq ?? 0))
          if (DEBUG_GAME) console.warn('[GameScene] duplicate MJPID_ACTION ignored', { actionKey, data })
        } else {
          this.appliedActionKeys.add(actionKey)
          this.traceGameFlow('apply ACTION', { actionKey, actionSeatOrder, action, actionSeq: data.actionSeq, bipaiIndex: data.bipaiIndex })
          this.completePendingActionIfMatched(actionSeatOrder, action, Number(data.actionSeq ?? 0))
          this.actionPromptSerial++
          this.clearAutoDiscardTimer()
          this.clearActionResponseTimer()
          this.clearAutoControlTimer()
          this.clearTimeWarningTimers()
          this.actionOfferByName.clear()
          this.currentActionOffers = []
          this.canDiscardOnTileClick = false
          this.currentActionSeatOrder = null
          this.currentActionPrompt = null
          this.clearTenpaiMarkers()
          this.emitToUiScene('actionPromptEnd', { viewOdr: this.myOdr })
          this.applyActionPacket(data)
        }
      }
      if (Number.isFinite(Number(data.leftCount))) {
        this.paifuGraphRound.left = Number(data.leftCount)
        this.emitToUiScene('stateUpdate', {
          players: this.players,
          left: Number(data.leftCount),
          viewOdr: this.myOdr,
        })
      }
    }
    this.onSignalR('playing', handleGamePlay)

    const handleHistory: SignalR.MessageHandler = data => {
      if (!this.canHandleSignalR() || this.isReplay) return
      const showHistoryLoading = !this.gameResyncInFlight
      if (showHistoryLoading) this.emitGameSync(true, 'history')
      try {
        const packets = this.extractReplayPackets(data)
        this.logResyncProbe('history received', {
          showHistoryLoading,
          historyCount: data.historyCount,
          packetCount: packets.length,
          packets: packets.map(packet => ({ cmd: packet.cmd, playType: packet.data.playType, openPos: packet.data.openPos, isInit: packet.data.bInit ?? packet.data.init, leftCount: packet.data.leftCount })).slice(0, 24),
          queueBefore: this.paiInfoQueue.map(msg => ({ ini: msg.bIniKyo, openPos: msg.openPos, count: msg.tiles.length })),
        })
        if (packets.length === 0) {
          if (DEBUG_GAME) console.warn('[GameScene] live history has no Web packets to apply', { data })
          this.logResyncProbe('history ignored: no packets', { dataKeys: isRecord(data) ? Object.keys(data) : [] })
          return
        }
        const pendingSeedInitPaiInfo = this.findPendingSeedInitPaiInfo()
        if (!this.canApplyLiveHistoryPackets(packets, pendingSeedInitPaiInfo)) {
          this.logResyncProbe('history ignored: incomplete init sequence', { packetCount: packets.length })
          return
        }
        this.logResyncProbe('history apply start', {
          pendingSeedInitPaiInfo: pendingSeedInitPaiInfo ? { openPos: pendingSeedInitPaiInfo.openPos, count: pendingSeedInitPaiInfo.tiles.length } : null,
        })
        this.clearLiveActionState('history resync')
        this.paiInfoQueue = pendingSeedInitPaiInfo ? [this.clonePaiInfoMsg(pendingSeedInitPaiInfo)] : []
        this.isReplayApplyingHistory = true
        try {
          let seededInitConsumed = !pendingSeedInitPaiInfo
          packets.forEach(packet => {
            if (!seededInitConsumed && this.isInitPaiInfoPacket(packet)) return
            if (packet.cmd === 'smmc4e') handlePaiInfo(packet.data)
            else {
              handleGamePlay(packet.data)
              if (String(packet.data.playType ?? '') === 'MJPID_INIKYO') seededInitConsumed = true
            }
          })
        } finally {
          this.isReplayApplyingHistory = false
        }
        this.applyPendingResyncHandSnapshot()
        this.redrawAllPerspectivePai()
        this.lastLiveHistoryAppliedAt = performance.now()
        this.emitToUiScene('stateUpdate', { players: this.players, viewOdr: this.myOdr })
        this.logResyncProbe('history apply complete', {
          currentKyoku: this.paifuGraphRound.kyokuCnt,
          left: this.paifuGraphRound.left,
          handCounts: this.players.map(player => player.hand.length),
          discardCounts: this.players.map(player => player.discards.length),
          queueAfter: this.paiInfoQueue.map(msg => ({ ini: msg.bIniKyo, openPos: msg.openPos, count: msg.tiles.length })),
        })
        if (DEBUG_GAME) console.info('[GameScene] live history applied', { packetCount: packets.length })
      } finally {
        if (showHistoryLoading) this.emitGameSync(false, 'history')
      }
    }
    this.onSignalR('history', handleHistory)

    if (this.isReplay) this.applyReplayPaifuData(handlePaiInfo, handleGamePlay)

  }

  private actionPacketKey(data: Record<string, unknown>, seatOrder: number, action: number): string {
    const actionSeq = Number(data.actionSeq ?? 0)
    const indices = Array.isArray(data.bipaiIndex) ? data.bipaiIndex.map(Number) : []
    if (Number.isFinite(actionSeq) && actionSeq > 0) return `${actionSeq}:${seatOrder}:${action}:${indices.join(',')}`
    return `${seatOrder}:${action}:${indices.join(',')}`
  }

  private traceGameFlow(eventName: string, details: Record<string, unknown> = {}) {
    if (!DEBUG_GAME) return
    console.info(`${GAME_FLOW_TRACE_PREFIX} #${++this.flowTraceSerial} ${eventName}`, {
      roomId: this.roomId,
      myOdr: this.myOdr,
      actionSendInFlight: this.actionSendInFlight,
      pendingAction: this.pendingAction,
      currentActionSeatOrder: this.currentActionSeatOrder,
      currentPrompt: this.currentActionPrompt
        ? {
            actionSeq: this.currentActionPrompt.actionSeq,
            seatOrder: this.currentActionPrompt.seatOrder,
            playerMode: this.currentActionPrompt.playerMode,
            localRemainingMs: Math.max(0, Math.round(this.currentActionPrompt.localDeadlineAt - performance.now())),
          }
        : null,
      canDiscardOnTileClick: this.canDiscardOnTileClick,
      ...details,
    })
  }

  private logDiscardProbe(eventName: string, details: Record<string, unknown> = {}) {
    if (!DEBUG_GAME) return
    console.info(DISCARD_PROBE_PREFIX, eventName, {
      roomId: this.roomId,
      isViewer: this.isViewer,
      myOdr: this.myOdr,
      handCounts: this.players.map(player => player.hand.length),
      discardCounts: this.players.map(player => player.discards.length),
      ...details,
    })
  }

  private logResyncProbe(eventName: string, details: Record<string, unknown> = {}) {
    if (!DEBUG_GAME) return
    console.info(RESYNC_PROBE_PREFIX, eventName, {
      roomId: this.roomId,
      isViewer: this.isViewer,
      myOdr: this.myOdr,
      kyokuCnt: this.paifuGraphRound.kyokuCnt,
      left: this.paifuGraphRound.left,
      handCounts: this.players.map(player => player.hand.length),
      discardCounts: this.players.map(player => player.discards.length),
      queue: this.paiInfoQueue.map(msg => ({ ini: msg.bIniKyo, openPos: msg.openPos, count: msg.tiles.length })),
      ...details,
    })
  }

  private handProbe(odr: number, bipaiIndex?: number) {
    if (odr < 0 || odr >= this.players.length) return { odr, valid: false }
    const hand = this.players[odr].hand
    const handIdx = bipaiIndex === undefined ? -1 : hand.findIndex(tile => tile.bipaiIndex === bipaiIndex)
    return {
      odr,
      valid: true,
      isLocal: this.isLocalPlayerOdr(odr),
      handLength: hand.length,
      handLengthMod3: hand.length % 3,
      targetBipaiIndex: bipaiIndex,
      handIdx,
      knownCode: bipaiIndex === undefined ? 0 : this.knownPai.get(bipaiIndex) ?? 0,
      handSample: hand.slice(0, 16).map(tile => ({ idx: tile.bipaiIndex, code: tile.code })),
    }
  }

  private completePendingActionIfMatched(seatOrder: number, action: number, actionSeq: number) {
    if (!this.pendingAction) return
    if (this.pendingAction.seatOrder !== seatOrder || this.pendingAction.action !== action) return
    if (this.pendingAction.actionSeq !== undefined && Number.isFinite(actionSeq) && actionSeq > 0 && this.pendingAction.actionSeq !== actionSeq) {
      this.traceGameFlow('pending ACTION not completed: seq mismatch', { seatOrder, action, actionSeq })
      return
    }
    this.clearPendingAction('complete pending ACTION', { seatOrder, action, actionSeq })
  }

  private clearPendingAction(reason: string, details: Record<string, unknown> = {}) {
    this.traceGameFlow(reason, details)
    this.actionSendInFlight = false
    this.pendingAction = null
  }

  private applyReplayPaifuData(handlePaiInfo: SignalR.MessageHandler, handleGamePlay: SignalR.MessageHandler) {
    if (this.replayPaifuApplied) return
    this.replayPaifuApplied = true
    const packets = this.extractReplayPackets(this.replayPaifuData)
    if (packets.length === 0) {
      if (DEBUG_GAME) console.warn('[GameScene] replay paifu has no Web packet history to apply', { paifu: this.replayPaifuData })
      return
    }
    this.time.delayedCall(0, () => {
      if (!this.canHandleSignalR()) return
      this.isReplayApplyingHistory = true
      try {
        packets.forEach(packet => {
          if (packet.cmd === 'smmc4e') handlePaiInfo(packet.data)
          else handleGamePlay(packet.data)
        })
      } finally {
        this.isReplayApplyingHistory = false
      }
      this.redrawAllReplayPai()
      this.emitToUiScene('stateUpdate', { players: this.players, viewOdr: this.myOdr })
      if (DEBUG_GAME) console.info('[GameScene] replay paifu applied', { packetCount: packets.length })
    })
  }

  private shouldSuppressLivePlayback() {
    return this.isReplayApplyingHistory
  }

  private canApplyLiveHistoryPackets(packets: Array<{ cmd: 'playing' | 'smmc4e'; data: Record<string, unknown> }>, pendingInitPaiInfo?: PaiInfoMsgState) {
    const hasInitKyoku = packets.some(packet => packet.cmd === 'playing' && String(packet.data.playType ?? '') === 'MJPID_INIKYO')
    if (!hasInitKyoku) return true
    const hasInitPaiInfo = packets.some(packet => packet.cmd === 'smmc4e' && Boolean(packet.data.bInit ?? packet.data.init))
    if (hasInitPaiInfo) return true
    if (pendingInitPaiInfo?.bIniKyo && pendingInitPaiInfo.tiles.length > 0) return true
    if (DEBUG_GAME) console.warn('[GameScene] skip incomplete live history: init kyoku without initial PaiInfo', {
      packetCount: packets.length,
      pendingInitPaiInfo: pendingInitPaiInfo ? { openPos: pendingInitPaiInfo.openPos, count: pendingInitPaiInfo.tiles.length } : null,
    })
    return false
  }

  private findPendingSeedInitPaiInfo() {
    const preferredOpenPos = this.isViewer ? VIEWER_OPEN_POS : this.myOdr
    return this.paiInfoQueue.find(msg => msg.bIniKyo && msg.openPos === preferredOpenPos)
      ?? this.paiInfoQueue.find(msg => msg.bIniKyo && msg.openPos !== VIEWER_OPEN_POS)
      ?? this.paiInfoQueue.find(msg => msg.bIniKyo)
  }

  private isInitPaiInfoPacket(packet: { cmd: 'playing' | 'smmc4e'; data: Record<string, unknown> }) {
    if (packet.cmd !== 'smmc4e') return false
    return Boolean(packet.data.bInit ?? packet.data.init)
  }

  private extractReplayPackets(value: unknown): Array<{ cmd: 'playing' | 'smmc4e'; data: Record<string, unknown> }> {
    if (typeof value === 'string') {
      try {
        return this.extractReplayPackets(JSON.parse(value))
      } catch {
        return []
      }
    }
    if (Array.isArray(value)) return value.flatMap(item => this.extractReplayPackets(item))
    if (!isRecord(value)) return []

    const nested = value.paifu ?? value.history ?? value.packets ?? value.events ?? value.playHistory ?? value.data
    if (nested !== undefined && nested !== value) {
      const nestedPackets = this.extractReplayPackets(nested)
      if (nestedPackets.length > 0) return nestedPackets
    }

    const payload = isRecord(value.payload) ? value.payload : isRecord(value.message) ? value.message : isRecord(value.body) ? value.body : value
    const rawCmd = String(value.cmd ?? value.command ?? value.commandCode ?? value.service ?? '')
    if (rawCmd === 'smmc4e' || rawCmd === 'PaiInfoList') return [{ cmd: 'smmc4e', data: payload }]
    if (rawCmd === 'playing' || rawCmd === 'GamePlay') return [{ cmd: 'playing', data: payload }]
    if (Array.isArray(payload.pai) && (payload.openPos !== undefined || payload.bInit !== undefined || payload.init !== undefined)) return [{ cmd: 'smmc4e', data: payload }]
    if (typeof payload.playType === 'string') return [{ cmd: 'playing', data: payload }]
    return []
  }

  private canHandleSignalR(): boolean {
    return this.acceptingSignalR && Boolean(this.sys && this.add && this.scene)
  }

  private onSignalR(cmd: string, handler: SignalR.MessageHandler) {
    SignalR.on(cmd, handler)
    this.signalRHandlers.push({ cmd, handler })
  }

  private requestInitialRoomState() {
    if (this.isReplay || !this.roomId) return
    const numericRoomId = Number(this.roomId)
    if (!Number.isFinite(numericRoomId) || numericRoomId <= 0) return
    this.emitGameSync(true, 'initial-room-state')

    this.time.delayedCall(0, () => {
      if (!this.canHandleSignalR()) {
        this.emitGameSync(false, 'initial-room-state')
        return
      }
      if (this.skipInitialRoomEnter) {
        const msSinceHistory = performance.now() - this.lastLiveHistoryAppliedAt
        if (this.lastLiveHistoryAppliedAt > 0 && msSinceHistory <= INITIAL_RESYNC_AFTER_HISTORY_SKIP_MS) {
          this.logResyncProbe('skip initial RequestGameResync: fresh history already applied', { msSinceHistory })
          this.emitGameSync(false, 'initial-room-state:fresh-history')
          return
        }
        this.logResyncProbe('request initial room state through c16e + RequestGameResync', { skipInitialRoomEnter: true, msSinceHistory })
        void SignalR.send('c16e', {})
          .then(() => {
            this.logResyncProbe('initial c16e sent before RequestGameResync')
            this.requestGameResync('initial-room-state')
          })
          .catch(error => {
            this.logResyncProbe('initial c16e failed before RequestGameResync', { errorMessage: error instanceof Error ? error.message : String(error) })
            this.requestGameResync('initial-room-state')
          })
        return
      }
      this.logResyncProbe('send c14e from GameScene initial room state', { numericRoomId })
      void SignalR.send('c14e', { roomId: numericRoomId, k42e: numericRoomId })
        .then(() => SignalR.send('c16e', {}))
        .catch(() => {})
    })
  }

  private requestGameResync(reason: string) {
    const numericRoomId = Number(this.roomId)
    if (!Number.isFinite(numericRoomId) || numericRoomId <= 0) return
    this.gameResyncInFlight = true
    this.emitGameSync(true, reason)
    this.logResyncProbe('RequestGameResync invoke start', { reason, numericRoomId })
    if (DEBUG_GAME) console.info('[GameScene] request game resync', { reason, roomId: this.roomId })
    void SignalR.invoke('RequestGameResync', numericRoomId)
      .then(() => {
        this.logResyncProbe('RequestGameResync invoke resolved', { reason, numericRoomId })
      })
      .catch(error => {
        this.logResyncProbe('RequestGameResync invoke failed', { reason, numericRoomId, errorMessage: error instanceof Error ? error.message : String(error) })
        this.requestInitialRoomState()
      })
      .finally(() => {
        this.time.delayedCall(1500, () => {
          this.gameResyncInFlight = false
          this.emitGameSync(false, reason)
        })
      })
  }

  private notifyGameClientReady(attempt = 0) {
    const roomId = Number(this.roomId)
    if (!Number.isInteger(roomId) || roomId <= 0) return
    void SignalR.invoke('NotifyGameClientReady', roomId).catch(error => {
      if (this.canHandleSignalR() && attempt < 20) {
        this.time.delayedCall(250, () => this.notifyGameClientReady(attempt + 1))
        return
      }
      console.warn('[GameScene] NotifyGameClientReady failed', { roomId, error })
    })
  }

  private teardownSignalR() {
    for (const { cmd, handler } of this.signalRHandlers) {
      SignalR.off(cmd, handler)
    }
    this.signalRHandlers = []
  }

  private setupReplayControlEvents() {
    this.teardownReplayControlEvents()
    if (!this.isReplay && !this.isViewer) return

    this.replayRotateHandler = (event: Event) => {
      const detail = (event as CustomEvent<{ delta?: number }>).detail
      const delta = Number(detail?.delta ?? 0)
      if (delta !== 1 && delta !== 3) return
      this.myOdr = ((this.myOdr + delta) & 3) as 0 | 1 | 2 | 3
      this.redrawAllPerspectivePai()
      this.emitViewOdrChange()
    }
    this.replayHandOpenHandler = (event: Event) => {
      const detail = (event as CustomEvent<{ open?: boolean }>).detail
      this.replayHandOpen = detail?.open !== false
      this.redrawAllPerspectivePai()
    }
    this.replayGraphHandler = (event: Event) => {
      const detail = (event as CustomEvent<{ visible?: boolean }>).detail
      if (detail?.visible === false) this.hidePaifuGraph()
      else this.showPaifuGraph()
    }

    window.addEventListener(PAIFU_ROTATE_EVENT, this.replayRotateHandler)
    window.addEventListener(PAIFU_HAND_OPEN_EVENT, this.replayHandOpenHandler)
    window.addEventListener(PAIFU_GRAPH_EVENT, this.replayGraphHandler)
  }

  private teardownReplayControlEvents() {
    if (this.replayRotateHandler) {
      window.removeEventListener(PAIFU_ROTATE_EVENT, this.replayRotateHandler)
      this.replayRotateHandler = undefined
    }
    if (this.replayHandOpenHandler) {
      window.removeEventListener(PAIFU_HAND_OPEN_EVENT, this.replayHandOpenHandler)
      this.replayHandOpenHandler = undefined
    }
    if (this.replayGraphHandler) {
      window.removeEventListener(PAIFU_GRAPH_EVENT, this.replayGraphHandler)
      this.replayGraphHandler = undefined
    }
    this.hidePaifuGraph()
  }

  private setupAutoControlEvents() {
    this.teardownAutoControlEvents()
    if (this.isReplay) return
    this.autoControlHandler = (event: Event) => {
      const detail = (event as CustomEvent<Partial<GameAutoControlState>>).detail ?? {}
      const previous = this.autoControl
      this.autoControl = {
        prox: Boolean(detail.prox),
        autoTap: Boolean(detail.autoTap),
        autoPass: Boolean(detail.autoPass),
        autoHora: Boolean(detail.autoHora),
      }
      if (previous.autoTap && !this.autoControl.autoTap) this.clearAutoDiscardTimer()
      if (previous.autoPass && !this.autoControl.autoPass) this.clearActionResponseTimer()
      if (previous.prox && !this.autoControl.prox) {
        this.clearAutoDiscardTimer()
        this.clearActionResponseTimer()
      }
      this.clearAutoControlTimer()
      if (DEBUG_GAME) console.info('[GameScene] auto control updated', this.autoControl)
      if (this.isLocalPlayerOdr(this.currentActionSeatOrder) && this.currentActionOffers.length > 0) {
        this.scheduleAutoControl(this.currentActionOffers)
      }
    }
    window.addEventListener(GAME_AUTO_CONTROL_EVENT, this.autoControlHandler)
  }

  private teardownAutoControlEvents() {
    if (!this.autoControlHandler) return
    window.removeEventListener(GAME_AUTO_CONTROL_EVENT, this.autoControlHandler)
    this.autoControlHandler = undefined
  }

  private resolveSkinTextureKey(key: string): string {
    const candidate = skinTextureCandidate(key)
    return this.textures.exists(candidate) ? candidate : key
  }

  private resolveSkinTexture(texture: { key: string; frame?: number }): { key: string; frame?: number } {
    return { ...texture, key: this.resolveSkinTextureKey(texture.key) }
  }

  private emitToUiScene(eventName: string, payload: unknown) {
    if (!this.canHandleSignalR()) return
    if (!this.isReplay && this.isReplayApplyingHistory && UI_SINGLE_DELIVERY_EVENTS.has(eventName)) return
    this.events.emit(eventName, payload)
  }

  private emitGameStatus(text: string, color = '#000', bold = false) {
    window.dispatchEvent(new CustomEvent(GAME_STATUS_EVENT, { detail: { text, color, bold } }))
  }

  private emitGameSync(active: boolean, reason: string) {
    window.dispatchEvent(new CustomEvent(GAME_SYNC_EVENT, { detail: { active, reason } }))
  }

  private emitHoraErrorStatus(reason: string) {
    const text = reason === 'furiten'
      ? 'フリテン'
      : reason === 'sameTurnFuriten'
        ? '同巡フリテン'
        : reason === 'invalid'
          ? '和了無効'
          : ''
    if (text) this.emitGameStatus(text, '#ff1414')
  }

  private emitViewOdrChange() {
    this.emitToUiScene('viewOdrChange', {
      viewOdr: this.myOdr,
      players: this.players,
    })
  }

  private resolveMyOdrFromPlayers() {
    if (this.isViewer) return
    const localPix = useAuthStore.getState().player?.pix ?? ''
    if (!localPix) return
    const engineOrder = this.players.findIndex(player => player.pix === localPix)
    if (engineOrder < 0 || engineOrder === this.myOdr) return
    this.myOdr = engineOrder
    this.emitViewOdrChange()
  }

  private isLocalPlayerOdr(odr: number | null | undefined): boolean {
    return !this.isViewer && odr === this.myOdr
  }

  private mergePlayerInfo(data: Record<string, unknown>) {
    const pos = this.resolvePlayerOdr(data)
    if (pos < 0 || pos >= this.players.length) return
    this.mergePlayerInfoAtOdr(pos, data)
  }

  private applyHanchanOrder(data: Record<string, unknown>) {
    const wasInitialized = this.hanchanOrderInitialized
    this.roomPosToOdr = Array.from({ length: this.players.length }, (_, index) => index)
    const engineToRoom = Array.isArray(data.players) ? data.players.map(Number) : []
    const roomPositionPlayers = this.players
    const engineOrderPlayers = Array.from({ length: this.players.length }, createEmptyPlayerState)
    engineToRoom.forEach((roomPos, odr) => {
      if (Number.isInteger(roomPos) && roomPos >= 0 && roomPos < this.players.length && odr >= 0 && odr < this.players.length) {
        this.roomPosToOdr[roomPos] = odr
        if (!wasInitialized) engineOrderPlayers[odr] = roomPositionPlayers[roomPos]
      }
    })
    if (!wasInitialized && engineToRoom.length > 0) this.players = engineOrderPlayers

    this.memberOdrById.clear()
    const memberInfo = Array.isArray(data.memberInfo) ? data.memberInfo : []
    memberInfo.forEach((member, odr) => {
      if (!isRecord(member)) return
      const pix = String(member.k3e ?? member.pix ?? member.playerId ?? '')
      if (pix && odr >= 0 && odr < this.players.length) this.memberOdrById.set(pix, odr)
    })
    this.hanchanOrderInitialized = true
  }

  private resolvePlayerOdr(data: Record<string, unknown>) {
    const pix = String(data.k3e ?? data.pix ?? data.playerId ?? '')
    const mappedOdr = pix ? this.memberOdrById.get(pix) : undefined
    if (mappedOdr !== undefined) return mappedOdr

    const rawPos = Number(data.k58e ?? data.playerPos ?? data.seatPos ?? data.pos ?? -1)
    if (!Number.isInteger(rawPos) || rawPos < 0 || rawPos >= this.players.length) return -1
    return this.hanchanOrderInitialized ? this.roomPosToOdr[rawPos] ?? rawPos : rawPos
  }

  private mergePlayerInfoAtOdr(pos: number, data: Record<string, unknown>) {
    if (pos < 0 || pos >= this.players.length) return
    const player = this.players[pos]
    const pix = String(data.k3e ?? data.pix ?? data.playerId ?? player.pix ?? '')
    const hostPix = String(data.k50e ?? data.roomHost ?? data.hostPix ?? '')
    const avatarId = data.k7e ?? data.avatarId
    const sex = String(data.k11e ?? data.sex ?? '')
    const fallbackSex = sex === 'F' || sex === 'female' ? 'female' : 'male'
    const customCostume = Number(data.mjkk136e ?? data.customCostume ?? data.charaId ?? player.customCostume ?? 0)
    const customCostumeType = Number(data.mjkk137e ?? data.customCostumeType ?? data.charaType ?? player.customCostumeType ?? 0)
    player.pix = pix || player.pix
    player.name = String(data.mjkk34e ?? data.k8e ?? data.nickName ?? data.nickname ?? data.name ?? player.name ?? pix)
    player.level = String(data.k32e ?? data.slevel ?? data.dan ?? player.level ?? '')
    player.rating = Number(data.k31e ?? data.rating ?? player.rating ?? 0)
    const majakTitle = readOptionalTitleCode(data.mjkk47e ?? data.majakTitle)
    const trickTitle = readOptionalTitleCode(data.mjkk46e ?? data.trickTitle)
    const richiEffect = readOptionalTitleCode(data.mjkk54e ?? data.richiEffect)
    if (majakTitle !== undefined) player.majakTitle = majakTitle
    if (trickTitle !== undefined) player.trickTitle = trickTitle
    if (richiEffect !== undefined) player.richiEffect = richiEffect
    if (data.isHost !== undefined) player.isHost = Boolean(data.isHost)
    const proxyValue = asBoolean(data.isProxy ?? data.proxy ?? data.isOutPlayer)
    if (proxyValue !== undefined) player.isProxy = proxyValue
    if (hostPix) this.applyHostPix(hostPix)
    if (Number.isFinite(customCostume) && customCostume > 0) player.customCostume = customCostume
    if (Number.isFinite(customCostumeType) && customCostumeType > 0) player.customCostumeType = customCostumeType
    if (sex) player.sex = sex
    if (avatarId != null && String(avatarId) !== '') {
      player.avatarId = String(avatarId)
      player.avatarUrl = getGameAvatarUrl(player.avatarId)
    }
    player.fallbackAvatarUrl = getDefaultAvatarUrl(fallbackSex)
  }

  private applyKyoResultState(data: Record<string, unknown>) {
    const pinType = Number(data.pinType ?? -1)
    const yakuByPlayer = isRecord(data.yakuByPlayer) ? data.yakuByPlayer : {}
    const totalsByPlayer = isRecord(data.totalsByPlayer) ? data.totalsByPlayer : {}
    const resultPlayers = Array.isArray(data.players) ? data.players : []

    for (let odr = 0; odr < this.players.length; odr++) {
      const playerResult = isRecord(resultPlayers[odr]) ? resultPlayers[odr] : {}
      const totals = isRecord(totalsByPlayer[String(odr)]) ? totalsByPlayer[String(odr)] as Record<string, unknown> : {}
      const yakuList = Array.isArray(yakuByPlayer[String(odr)]) ? yakuByPlayer[String(odr)] as unknown[] : []
      const pointBal = asFiniteNumber(playerResult.tenBal)
      const tipBal = asFiniteNumber(playerResult.tipBal)
      const resultScore = asFiniteNumber(playerResult.point ?? playerResult.score ?? playerResult.gameScore)
      const resultTip = asFiniteNumber(playerResult.tip)
      const resultYakitori = asBoolean(playerResult.yakitori)
      if (resultScore !== undefined) this.players[odr].score = resultScore
      else if (pointBal !== undefined) this.players[odr].score = (Number.isFinite(this.players[odr].score) ? this.players[odr].score : 0) + pointBal
      if (resultTip !== undefined) this.players[odr].tip = resultTip
      else if (tipBal !== undefined) this.players[odr].tip += tipBal
      if (resultYakitori !== undefined) this.players[odr].yakitori = resultYakitori
      else if (asBoolean(playerResult.isHora)) this.players[odr].yakitori = false
      this.players[odr].hora = {
        pinType,
        isHora: Boolean(playerResult.isHora),
        isHoju: Boolean(playerResult.isHoju),
        isTempai: Boolean(playerResult.isTempai),
        isRichi: Boolean(playerResult.isRichi),
        yaku: yakuList.filter(isRecord).map(yaku => ({
          name: String(yaku.name ?? ''),
          fan: Number(yaku.fan ?? 0),
          code: asFiniteNumber(yaku.code),
          isYakuman: asBoolean(yaku.isYakuman),
          tip: asFiniteNumber(yaku.tip),
        })),
        totalFu: asFiniteNumber(totals.totalFu),
        totalFan: asFiniteNumber(totals.totalFan),
        totalTen: asFiniteNumber(totals.totalTen),
        tipBal: asFiniteNumber(totals.tipBal ?? playerResult.tipBal),
      }
    }
  }

  private playKyoResultHoraEffect(data: Record<string, unknown>) {
    if (this.shouldSuppressLivePlayback()) return
    const pinType = Number(data.pinType ?? -1)
    if (pinType !== 0 && pinType !== 1) return
    const totalsByPlayer = isRecord(data.totalsByPlayer) ? data.totalsByPlayer : {}
    const yakuByPlayer = isRecord(data.yakuByPlayer) ? data.yakuByPlayer : {}
    const resultPlayers = Array.isArray(data.players) ? data.players : []

    for (let odr = 0; odr < this.players.length; odr++) {
      const playerResult = isRecord(resultPlayers[odr]) ? resultPlayers[odr] : {}
      const yakuList = Array.isArray(yakuByPlayer[String(odr)]) ? yakuByPlayer[String(odr)] as unknown[] : []
      if (!asBoolean(playerResult.isHora) && yakuList.length === 0) continue
      const totals = isRecord(totalsByPlayer[String(odr)]) ? totalsByPlayer[String(odr)] as Record<string, unknown> : {}
      const basicTen = asFiniteNumber(totals.totalTen)
      const isYakuman = yakuList.filter(isRecord).some(yaku => asBoolean(yaku.isYakuman))
      this.playHoraEffectSound(pinType, basicTen, isYakuman)
    }
  }

  private playHoraEffectSound(pinType: number, basicTen: number | undefined, isYakuman: boolean) {
    const ten = basicTen ?? 0
    const level = isYakuman || ten >= 4000 ? 3 : ten >= 3000 ? 2 : 1
    const sid = pinType === 0
      ? level === 3 ? SID_EFFECT_R_LV3 : level === 2 ? SID_EFFECT_R_LV2 : SID_EFFECT_R_LV1
      : level === 3 ? SID_EFFECT_T_LV3 : level === 2 ? SID_EFFECT_T_LV2 : SID_EFFECT_T_LV1
    playMajakSid(sid, this.soundSkinOptions())
    if (isYakuman || ten >= 8000) playMajakSid(SID_EFFECT_YAKUMAN, this.soundSkinOptions())
  }

  private applyHostPix(hostPix: string) {
    this.players.forEach(player => { player.isHost = player.pix === hostPix })
  }

  /* ======================================================================
   * 描画メソッド (CMJTblDraw 相当)
   * ====================================================================== */

  /** 手牌再描画 (PutHaipai / PutTsumo 相当) */
  private redrawHand(odr: number) {
    if (this.isReplayApplyingHistory) return
    /* 既存スプライトを破棄 */
    this.handSprites[odr].forEach(s => s.destroy())
    this.handSprites[odr] = []
    this.mobileOpponentHandCountTexts[odr]?.destroy()
    this.mobileOpponentHandCountTexts[odr] = undefined

    const tiles = this.players[odr].hand
    const loc = odrToLoc(odr, this.myOdr)
    const isMe  = !this.isViewer && loc === 0 && !this.isReplay

    if (this.shouldUseMobileOpponentHandSummary(odr, loc)) {
      this.redrawMobileOpponentHandSummary(odr, loc, tiles.length)
      this.redrawPaifuGraphContent()
      return
    }

    if (!this.isViewer && odr === this.myOdr) {
      this.selectedCursor?.destroy()
      this.selectedCursor = undefined
      this.clearTenpaiMarkers()
    }

    tiles.forEach((tile, idx) => {
      const isDrawTile = idx === tiles.length - 1 && tiles.length % 3 === 2
      const useOpenOrDownLayout = this.isViewer || (this.isReplay && this.replayHandOpen)
      const handScale = this.handTileScale(odr, loc)
      const position = this.layoutMode === 'mobileLandscape'
        ? mobileOuterHandPos(loc, idx, tiles.length, isDrawTile, handScale) ?? handPos(loc, idx, isDrawTile, useOpenOrDownLayout)
        : handPos(loc, idx, isDrawTile, useOpenOrDownLayout)
      const { x, y } = position
      const texture = this.resolveSkinTexture(handTexture(loc))
      const concealedTexture = this.resolveSkinTexture(concealedHandTexture(loc))
      const depth = handDepth(y, idx)
      let spr: Phaser.GameObjects.Image

      if (isMe) {
        /* 自分の手牌: 表向き + インタラクティブ */
        const frame = paiToFrame(tile.code)
        spr = this.add.image(x, y, texture.key, frame)
          .setOrigin(0, 0)
          .setScale(handScale)
          .setDepth(depth)
          .setInteractive({ useHandCursor: true })
          .on('pointerdown', () => this.onTilePointerDown(idx))
          .on('pointerup', () => this.onTilePointerUp(idx))
          .on('pointerover', () => this.onTilePointerOver(idx))
          .on('pointerout', () => this.onTilePointerOut(idx))
        this.clipToBoard(spr)
        if (tile.isSelected) {
          this.selectedCursor = this.clipToBoard(this.add.image(x, y - 5, 'cursor_mouse')
            .setOrigin(0, 0)
            .setScale(handScale)
            .setDepth(1001))
        }
      } else if ((this.isReplay && this.replayHandOpen) || (this.isViewer && this.replayHandOpen && tile.code > 0)) {
        const frame = paiToFrame(tile.code)
        spr = this.clipToBoard(this.add.image(x, y, this.resolveSkinTextureKey(openHandTexture(loc)), frame)
          .setOrigin(0, 0)
          .setScale(handScale)
          .setDepth(depth))
      } else if (this.isViewer) {
        spr = this.clipToBoard(this.add.image(x, y, this.resolveSkinTextureKey(downTexture(loc)))
          .setOrigin(0, 0)
          .setScale(handScale)
          .setDepth(depth))
      } else {
        /* 他家の手牌: レガシー方向別 tachi 画像 */
        spr = this.clipToBoard(this.add.image(x, y, concealedTexture.key)
          .setOrigin(0, 0)
          .setScale(handScale)
          .setDepth(depth))
      }
      this.handSprites[odr].push(spr)
    })
    this.updateMobileActionHandVisibility()
    if (!this.isViewer && odr === this.myOdr) this.redrawTenpaiMarkers()
    this.redrawPaifuGraphContent()
  }

  private updateMobileActionHandVisibility() {
    if (this.layoutMode !== 'mobileLandscape' || this.isViewer || this.isReplay) return
    const visible = !this.mobileActionButtonsVisible
    this.handSprites[this.myOdr].forEach(sprite => sprite.setVisible(visible))
    this.selectedCursor?.setVisible(visible)
    this.tenpaiMarkerSprites.forEach(sprite => sprite.setVisible(visible))
  }

  private redrawMobileOpponentHandSummary(odr: number, loc: 0 | 1 | 2 | 3, count: number) {
    const visibleBacks = Math.min(2, Math.max(0, count))
    const textureKey = this.resolveSkinTextureKey(downTexture(loc))
    const start = handPos(loc, 0, false)
    const step = loc === 1
      ? { x: 0, y: -16 }
      : loc === 3
        ? { x: 0, y: 16 }
        : loc === 2
          ? { x: -20, y: 0 }
          : { x: 20, y: 0 }

    for (let idx = 0; idx < visibleBacks; idx++) {
      const x = start.x + step.x * idx
      const y = start.y + step.y * idx
      const sprite = this.clipToBoard(this.add.image(x, y, textureKey)
        .setOrigin(0, 0)
        .setScale(MOBILE_OPPONENT_SUMMARY_TILE_SCALE)
        .setDepth(handDepth(y, idx)))
      this.handSprites[odr].push(sprite)
    }

    const lastX = start.x + step.x * Math.max(visibleBacks - 1, 0)
    const lastY = start.y + step.y * Math.max(visibleBacks - 1, 0)
    const badgeX = loc === 1 ? lastX + 16 : loc === 3 ? lastX - 32 : loc === 2 ? lastX - 34 : lastX + 22
    const badgeY = loc === 1 ? lastY + 2 : loc === 3 ? lastY + 12 : loc === 2 ? lastY + 18 : lastY + 18
    this.mobileOpponentHandCountTexts[odr] = this.clipToBoard(this.add.text(badgeX, badgeY, String(count), {
      fontFamily: getUiFontFamily(),
      fontSize: getUiFontSize(14),
      fontStyle: 'bold',
      color: '#ffffff',
      backgroundColor: 'rgba(0, 0, 0, 0.68)',
      padding: { left: 5, right: 5, top: 2, bottom: 2 },
    }).setOrigin(0, 0).setDepth(1003))
  }

  private clearTenpaiMarkers() {
    this.tenpaiMarkerSprites.forEach(sprite => sprite.destroy())
    this.tenpaiMarkerSprites = []
  }

  private redrawTenpaiMarkers() {
    this.clearTenpaiMarkers()
    if (this.isReplay || !this.canDiscardOnTileClick || this.players[this.myOdr].isReach) return
    const hand = this.players[this.myOdr].hand
    for (let idx = 0; idx < hand.length; idx++) {
      if (!checkTempaiAfterDiscard(hand, idx)) continue
      const sprite = this.handSprites[this.myOdr][idx]
      if (!sprite) continue
      this.tenpaiMarkerSprites.push(this.clipToBoard(this.add.image(sprite.x + 9, sprite.y - 8, this.resolveSkinTextureKey('mj_tenpaiicon'))
        .setOrigin(0, 0)
        .setDepth(1001)))
    }
  }

  /** 捨て牌再描画 (PutAction discard 相当) */
  private redrawDiscards(odr: number) {
    if (this.isReplayApplyingHistory) return
    this.clearDiscardFlight(odr)
    this.suteSprites[odr].forEach(s => s.destroy())
    this.suteSprites[odr] = []

    const loc = odrToLoc(odr, this.myOdr)
    let nextFlag = 0
    this.players[odr].discards.forEach((discard, idx) => {
      let flag = discard.isReach ? 2 : nextFlag
      if (idx % DISCARD_COLS === 0 && flag === 1) flag = 0
      nextFlag = flag === 2 ? 1 : flag
      const { x, y } = discardPos(loc, idx, flag, this.layoutMode)
      const frame = paiToFrame(discard.code)
      const spr = this.add.image(x, y, this.resolveSkinTextureKey(discardTexture(loc, flag)), frame)
        .setOrigin(0, 0)
        .setScale(this.discardTileScale())
        .setDepth(y)
      this.clipToBoard(spr)
      this.suteSprites[odr].push(spr)
    })
    this.redrawPaifuGraphContent()
  }

  private shouldAnimateLiveDiscard(): boolean {
    return !this.isReplay
      && !this.shouldSuppressLivePlayback()
      && !this.gameResyncInFlight
      && document.visibilityState === 'visible'
      && !window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
  }

  private captureDiscardFlightOrigin(odr: number, handIdx: number): DiscardFlightOrigin | undefined {
    const sprites = this.handSprites[odr]
    const source = sprites[handIdx] ?? sprites[sprites.length - 1]
    if (!source?.active || !source.visible) return undefined
    return {
      x: source.x,
      y: source.y,
      scaleX: source.scaleX,
      scaleY: source.scaleY,
      depth: source.depth,
    }
  }

  private animateLatestDiscard(odr: number, origin: DiscardFlightOrigin | undefined) {
    if (!origin || !this.shouldAnimateLiveDiscard()) return
    const target = this.suteSprites[odr][this.suteSprites[odr].length - 1]
    if (!target?.active) return

    this.clearDiscardFlight(odr)
    target.setVisible(false)
    const sprite = this.clipToBoard(this.add.image(origin.x, origin.y, target.texture.key, target.frame.name)
      .setOrigin(target.originX, target.originY)
      .setScale(origin.scaleX, origin.scaleY)
      .setDepth(Math.max(origin.depth, target.depth) + 1))
    const flight: ActiveDiscardFlight = { sprite, target }
    this.activeDiscardFlights[odr] = flight

    const progress = { value: 0 }
    const centerX = BOARD_X + BOARD_W / 2
    const centerY = BOARD_Y + BOARD_H / 2
    const controlX = (origin.x + target.x) / 2 + (centerX - (origin.x + target.x) / 2) * 0.08
    const controlY = (origin.y + target.y) / 2 + (centerY - (origin.y + target.y) / 2) * 0.08
    const tiltDirection = odrToLoc(odr, this.myOdr) % 2 === 0 ? 1 : -1

    flight.tween = this.tweens.add({
      targets: progress,
      value: 1,
      duration: 167,
      ease: 'Cubic.Out',
      onUpdate: () => {
        const t = progress.value
        const inverse = 1 - t
        const lift = Math.sin(Math.PI * t)
        sprite.setPosition(
          inverse * inverse * origin.x + 2 * inverse * t * controlX + t * t * target.x,
          inverse * inverse * origin.y + 2 * inverse * t * controlY + t * t * target.y,
        )
        sprite.setScale(
          Phaser.Math.Linear(origin.scaleX, target.scaleX, t) * (1 + 0.06 * lift),
          Phaser.Math.Linear(origin.scaleY, target.scaleY, t) * (1 + 0.06 * lift),
        )
        sprite.setAngle(tiltDirection * 6 * lift)
      },
      onComplete: () => {
        if (this.activeDiscardFlights[odr] !== flight) return
        if (target.active) target.setVisible(true)
        if (sprite.active) sprite.destroy()
        this.activeDiscardFlights[odr] = undefined
      },
    })
  }

  private clearDiscardFlight(odr: number) {
    const flight = this.activeDiscardFlights[odr]
    if (!flight) return
    this.activeDiscardFlights[odr] = undefined
    flight.tween?.stop()
    if (flight.target.active) flight.target.setVisible(true)
    if (flight.sprite.active) flight.sprite.destroy()
  }

  private clearAllDiscardFlights() {
    for (let odr = 0; odr < this.activeDiscardFlights.length; odr++) this.clearDiscardFlight(odr)
  }

  private redrawMelds(odr: number) {
    if (this.isReplayApplyingHistory) return
    this.meldSprites[odr].forEach(s => s.destroy())
    this.meldSprites[odr] = []

    const loc = odrToLoc(odr, this.myOdr)
    const player = this.players[odr]
    const flowerOffset = player.flowers.length > 0 ? 1 : 0
    const meldScale = this.meldTileScale(odr, loc)

    player.flowers.forEach((tile, col) => {
      const { x, y } = meldPos(loc, 0, col, 0, this.layoutMode, meldScale)
      const frame = paiToFrame(tile.code)
      const texture = this.resolveSkinTexture(meldTexture(loc, 0, false))
      const spr = this.clipToBoard(this.add.image(x, y, texture.key, frame).setOrigin(0, 0).setScale(meldScale).setDepth(y))
      this.meldSprites[odr].push(spr)
    })

    player.melds.forEach((meld, meldIdx) => {
      const row = meldIdx + flowerOffset
      meld.tiles.forEach((tile, col) => {
        const { x, y } = meldPos(loc, row, col, tile.flag, this.layoutMode, meldScale)
        const texture = this.resolveSkinTexture(meldTexture(loc, tile.flag, Boolean(tile.isDown)))
        const frame = texture.frame ?? paiToFrame(tile.code)
        const spr = this.clipToBoard(this.add.image(x, y, texture.key, frame).setOrigin(0, 0).setScale(meldScale).setDepth(y))
        this.meldSprites[odr].push(spr)
      })
    })
    this.redrawPaifuGraphContent()
  }

  private clearDeadWall() {
    this.deadWallSprites.forEach(sprite => sprite.destroy())
    this.deadWallSprites = []
  }

  private redrawDeadWall() {
    if (this.isReplayApplyingHistory) return
    this.clearDeadWall()
    const haipaiPos = this.currentHaipaiPos()
    if (haipaiPos === undefined) return

    const exposed = new Map<number, number>()
    this.paifuGraphRound.dora.forEach((code, idx) => {
      if (!code || code <= 0) return
      const wallIdx = this.deadWallIndexForBipai(this.getDoraIndex(idx), haipaiPos)
      if (wallIdx !== undefined) exposed.set(wallIdx, code)
    })

    for (let idx = 0; idx < DEAD_WALL_COUNT; idx++) {
      const { x, y } = deadWallPos(idx, this.layoutMode)
      const code = exposed.get(idx)
      const sprite = code
        ? this.add.image(x, y, this.resolveSkinTextureKey('hai_sute'), paiToFrame(code))
        : this.add.image(x, y, this.resolveSkinTextureKey('hai_ura_2'))
      this.clipToBoard(sprite
        .setOrigin(0, 0)
        .setScale(this.tileScale())
        .setDepth(y + (idx % 2 === 0 ? WAN_EXPOSE_OFFSET_Y * 2 : 0)))
      this.deadWallSprites.push(sprite)
    }
  }

  private currentHaipaiPos(): number | undefined {
    const oyaOrder = this.mod(this.chicha + this.paifuGraphRound.kyokuCnt, this.players.length)
    return this.getHaipaiPos(oyaOrder, this.paifuGraphRound.dice)
  }

  private deadWallIndexForBipai(bipaiIndex: number | undefined, haipaiPos: number): number | undefined {
    if (bipaiIndex === undefined) return undefined
    const wallIndex = this.mod(bipaiIndex + BIPAI_MAX_COUNT - haipaiPos, BIPAI_MAX_COUNT)
    if (wallIndex < DEAD_WALL_START || wallIndex >= BIPAI_MAX_COUNT) return undefined
    return wallIndex - DEAD_WALL_START
  }

  private showPaifuGraph() {
    if (this.paifuGraphLayer) {
      this.paifuGraphLayer.setVisible(true)
      this.redrawPaifuGraphContent()
      return
    }

    const layer = this.add.container(0, 0).setDepth(3000)
    const shade = this.add.rectangle(0, 0, this.scale.width, this.scale.height, 0x000000, 0.45)
      .setOrigin(0, 0)
      .setInteractive()
    const body = this.add.container(PAIFU_GRAPH.x, PAIFU_GRAPH.y)
    body.add(this.add.image(0, 0, this.resolveSkinTextureKey('mj_recBg')).setOrigin(0, 0))
    const closeBg = this.add.rectangle(PAIFU_GRAPH.x + PAIFU_GRAPH.w + 4, PAIFU_GRAPH.y - 22, 22, 20, 0xd4d0c8)
      .setOrigin(0, 0)
      .setInteractive({ useHandCursor: true })
      .on('pointerdown', () => this.hidePaifuGraph())
    const closeText = this.add.text(PAIFU_GRAPH.x + PAIFU_GRAPH.w + 10, PAIFU_GRAPH.y - 22, '×', {
      fontFamily: getUiFontFamily(), fontSize: getUiFontSize(12), color: '#000000',
    })

    layer.add([shade, body, closeBg, closeText])
    this.paifuGraphLayer = layer
    this.paifuGraphBody = body
    this.redrawPaifuGraphContent()
  }

  private hidePaifuGraph() {
    this.paifuGraphObjects.forEach(obj => obj.destroy())
    this.paifuGraphObjects = []
    this.paifuGraphBody = undefined
    this.paifuGraphLayer?.destroy()
    this.paifuGraphLayer = undefined
  }

  private redrawPaifuGraphContent() {
    if (this.isReplayApplyingHistory) return
    if (!this.paifuGraphBody || !this.paifuGraphLayer?.visible) return

    this.paifuGraphObjects.forEach(obj => obj.destroy())
    this.paifuGraphObjects = []

    this.addPaifuGraphRoundInfo()

    for (let odr = 0; odr < this.players.length; odr++) {
      const row = PAIFU_GRAPH_ROWS[odr]
      const player = this.players[odr]
      this.addPaifuGraphText(row.name.x, row.name.y, player.name || `Player ${odr + 1}`, 59)
      this.addPaifuGraphText(row.point.x, row.point.y, String(player.score), 56, 'right')

      const initialHand = this.paifuGraphInitialHands[odr].length > 0
        ? this.paifuGraphInitialHands[odr]
        : player.hand.slice(0, Math.min(13, player.hand.length))
      initialHand.slice(0, 13).forEach((tile, idx) => {
        this.addPaifuGraphTile('mj_recPaeFt', paiToFrame(tile.code), row.initial.x + 20 * idx, row.initial.y)
      })

      this.paifuGraphDraws[odr].forEach((draw, idx) => {
        const texture = draw.small ? 'mj_recPaeSm' : 'mj_recPaeFt'
        const x = row.draw.x + 20 * idx + (draw.small ? 7 : 0)
        const y = row.draw.y + (draw.small ? 10 : 0)
        this.addPaifuGraphTile(texture, paiToFrame(draw.code), x, y)
        if (draw.noteFrame !== undefined) {
          this.addPaifuGraphTile('mj_recNotesIcn', draw.noteFrame, row.draw.x + 20 * idx, row.draw.y - 9)
        }
      })

      const graphDiscards = this.paifuGraphDiscards[odr].length > 0 ? this.paifuGraphDiscards[odr] : player.discards
      graphDiscards.forEach((discard, idx) => {
        const x = row.discard.x + 20 * idx
        this.addPaifuGraphTile('mj_recPaeFt', paiToFrame(discard.code), x, row.discard.y)
        if (discard.isReach) this.addPaifuGraphImage('mj_recReachBar', x, row.discard.y - 3)
      })

      const closedHand = player.hand.slice(0, player.hand.length % 3 === 2 ? -1 : undefined)
      closedHand.forEach((tile, idx) => {
        this.addPaifuGraphTile('mj_recPaeFt', paiToFrame(tile.code), row.final.x + 20 * idx, row.final.y)
      })
      this.addPaifuGraphMelds(row.final.x + 20 * closedHand.length + 23, row.final.y, player)
    }
  }

  private addPaifuGraphRoundInfo() {
    const windFrame = Math.floor(this.paifuGraphRound.kyokuCnt / 4)
    const kyokuFrame = this.paifuGraphRound.kyokuCnt % 4
    this.addPaifuGraphTile('mj_kyoku', Phaser.Math.Clamp(windFrame, 0, 3), 11, 15)
    this.addPaifuGraphTile('mj_kyokuNum', Phaser.Math.Clamp(kyokuFrame, 0, 3), 44, 15)
    this.addPaifuGraphStkNumber(143, 25, this.paifuGraphRound.ribo)
    this.addPaifuGraphStkNumber(170, 25, this.paifuGraphRound.renchan)
    this.paifuGraphRound.dice.slice(0, 2).forEach((dice, idx) => {
      this.addPaifuGraphTile('mj_dice', Phaser.Math.Clamp(Math.trunc(dice), 0, 5), idx === 0 ? 227 : 242, 29)
    })
    this.paifuGraphRound.dora.forEach((code, idx) => {
      this.addPaifuGraphTile('mj_recPaeFt', paiToFrame(code), 295 + 20 * idx, 22)
    })
    this.paifuGraphRound.uraDora.forEach((code, idx) => {
      this.addPaifuGraphTile('mj_recPaeFt', paiToFrame(code), 400 + 20 * idx, 22)
    })
    PAIFU_GRAPH_OPTIONS.forEach((option, idx) => {
      const frame = this.readOptionDigit(this.paifuGraphRound.roomOption, option.optionIndex, option.defaultFrame)
      this.addPaifuGraphTile(option.key, frame, 534 + 17 * idx, 30)
    })
  }

  private addPaifuGraphMelds(startX: number, y: number, player: PlayerState) {
    let x = startX
    const melds = [...player.melds].reverse()
    for (const meld of melds) {
      x += PAIFU_GRAPH_MELD_GAP
      const width = meld.tiles.length === 4 ? 88 : 68
      meld.tiles.forEach((tile, idx) => {
        const tileX = x + 20 * idx
        if (tile.isDown) {
          this.addPaifuGraphImage('mj_recPaeBk', tileX, y)
        } else if (tile.flag === 2) {
          this.addPaifuGraphTile('mj_recPaeSd', paiToFrame(tile.code), tileX - 8, y + 7)
        } else {
          this.addPaifuGraphTile('mj_recPaeFt', paiToFrame(tile.code), tileX + (tile.flag === 1 ? -8 : 0), y)
        }
      })
      x += width
    }
    if (player.flowers.length > 0) x += PAIFU_GRAPH_MELD_GAP
    player.flowers.forEach((tile, idx) => {
      this.addPaifuGraphTile('mj_recPaeFt', paiToFrame(tile.code), x + 20 * idx, y)
    })
  }

  private addPaifuGraphStkNumber(x: number, y: number, value: number) {
    String(Math.max(0, Math.trunc(value))).split('').forEach((digit, idx) => {
      this.addPaifuGraphTile('mj_num_rh', Number(digit), x + 10 * idx, y)
    })
  }

  private addPaifuGraphText(x: number, y: number, text: string, width: number, align: 'left' | 'right' | 'center' = 'center') {
    const obj = this.add.text(x, y, text, {
      fontFamily: getUiFontFamily(), fontSize: getUiFontSize(12), color: '#000000',
      fixedWidth: width, align,
    }).setCrop(0, 0, width, 14)
    this.paifuGraphBody?.add(obj)
    this.paifuGraphObjects.push(obj)
  }

  private addPaifuGraphTile(texture: string, frame: number, x: number, y: number) {
    const obj = this.add.image(x, y, this.resolveSkinTextureKey(texture), frame).setOrigin(0, 0)
    this.paifuGraphBody?.add(obj)
    this.paifuGraphObjects.push(obj)
  }

  private addPaifuGraphImage(texture: string, x: number, y: number) {
    const obj = this.add.image(x, y, this.resolveSkinTextureKey(texture)).setOrigin(0, 0)
    this.paifuGraphBody?.add(obj)
    this.paifuGraphObjects.push(obj)
  }

  private addPaifuGraphCall(odr: number, code: number, action: Act) {
    if (code <= 0 || odr < 0 || odr >= this.players.length) return
    const noteFrame = action === Act.Chi ? 1
      : action === Act.Pon ? 0
        : action === Act.Hua ? 5
          : 2
    this.paifuGraphDraws[odr].push({
      code,
      small: action === Act.Kan || action === Act.Ank || action === Act.Cha || action === Act.Hua,
      noteFrame,
    })
    this.redrawPaifuGraphContent()
  }

  private cloneTiles(tiles: TileState[]): TileState[] {
    return tiles.map(tile => ({ ...tile }))
  }

  private readOptionDigit(option: string, index: number, fallback: number): number {
    const digit = option[index]
    return digit >= '0' && digit <= '9' ? Number(digit) : fallback
  }

  /* ======================================================================
   * タイル選択 / 打牌 (CMJUserIF 相当)
   * ====================================================================== */
  private onTilePointerOver(idx: number) {
    if (DEBUG_GAME && this.canDiscardOnTileClick) console.info('[GameScene] tile hover discard candidate', {
      idx,
      myOdr: this.myOdr,
      tile: this.players[this.myOdr].hand[idx],
    })
    if (!this.canDiscardOnTileClick && !this.pendingActionChoice) return
    const hand = this.players[this.myOdr].hand
    if (idx < 0 || idx >= hand.length) return
    const loc = odrToLoc(this.myOdr, this.myOdr)
    const isDrawTile = idx === hand.length - 1 && hand.length % 3 === 2
    const handScale = this.handTileScale(this.myOdr, loc)
    const position = this.layoutMode === 'mobileLandscape'
      ? mobileOuterHandPos(loc, idx, hand.length, isDrawTile, handScale) ?? handPos(loc, idx, isDrawTile)
      : handPos(loc, idx, isDrawTile)
    const { x, y } = position
    this.hoverCursor?.destroy()
    this.hoverCursor = this.clipToBoard(this.add.image(x, y - 5, 'cursor_mouse')
      .setOrigin(0, 0)
      .setScale(handScale)
      .setDepth(1001))
    if (this.canDiscardOnTileClick) this.showWaitTileGuide(idx)
  }

  private onTilePointerOut(_idx: number) {
    this.hoverCursor?.destroy()
    this.hoverCursor = undefined
    this.clearWaitTileGuide()
  }

  private clearWaitTileGuide() {
    this.waitTileGuideContainer?.destroy()
    this.waitTileGuideContainer = undefined
  }

  private showWaitTileGuide(idx: number) {
    this.clearWaitTileGuide()
    if (this.isReplay || !this.canDiscardOnTileClick || this.players[this.myOdr].isReach) return
    const hand = this.players[this.myOdr].hand
    if (idx < 0 || idx >= hand.length) return
    const entries = this.calculateWaitTileGuide(idx)
    if (entries.length === 0) return

    const wL = 30
    const wM = 34
    const wR = 9
    const xN = 39
    const yN = 3
    const yP = 16
    const yF = 58
    const yPos = 488
    const width = wL + wR + wM * entries.length
    const tileSprite = this.handSprites[this.myOdr][idx]
    const rawX = (tileSprite?.x ?? BOARD_X) - BOARD_X + 37 / 2 - width / 2
    const localX = Math.min(Math.max(0, rawX), BOARD_W - width)
    const point = boardLocalPoint({ x: localX, y: yPos })
    const guide = this.clipToBoard(this.add.container(point.x, point.y).setDepth(2000))

    guide.add(this.add.image(0, 0, this.resolveSkinTextureKey('mj_machihai_base01')).setOrigin(0, 0).setAlpha(0.5))
    guide.add(this.add.image(0, 0, this.resolveSkinTextureKey('mj_machihai_frame01')).setOrigin(0, 0))
    entries.forEach((entry, entryIdx) => {
      const x = wL + entryIdx * wM
      guide.add(this.add.image(x, 0, this.resolveSkinTextureKey('mj_machihai_base02')).setOrigin(0, 0).setAlpha(0.5))
      guide.add(this.add.image(x, 0, this.resolveSkinTextureKey('mj_machihai_frame02')).setOrigin(0, 0))
      guide.add(this.add.image(xN + entryIdx * wM, yN, this.resolveSkinTextureKey('mj_machihai_num'), Math.max(0, Math.min(9, entry.rest))).setOrigin(0, 0))
      guide.add(this.add.image(x, yP, this.resolveSkinTextureKey('hai_omote'), paiToFrame(entry.code)).setOrigin(0, 0))
      if (entry.furiten) guide.add(this.add.image(x, yF, this.resolveSkinTextureKey('mj_machihai_furiten')).setOrigin(0, 0))
    })
    const rightX = wL + wM * entries.length
    guide.add(this.add.image(rightX, 0, this.resolveSkinTextureKey('mj_machihai_base03')).setOrigin(0, 0).setAlpha(0.5))
    guide.add(this.add.image(rightX, 0, this.resolveSkinTextureKey('mj_machihai_frame03')).setOrigin(0, 0))
    this.waitTileGuideContainer = guide
  }

  private calculateWaitTileGuide(discardIdx: number): WaitTileGuideEntry[] {
    const hand = this.players[this.myOdr].hand
    const counts = Array.from({ length: 34 }, () => 0)
    for (let idx = 0; idx < hand.length; idx++) {
      if (idx === discardIdx) continue
      const serial = paiToSerial(hand[idx].code)
      if (serial < 0) return []
      counts[serial]++
    }
    const handCount = Math.max(0, hand.length - 1)
    const visibleCounts = this.visibleTileCounts()
    const selfDiscardCounts = this.selfDiscardCounts()
    const discardSerial = paiToSerial(hand[discardIdx]?.code ?? 0)
    if (discardSerial >= 0) selfDiscardCounts[discardSerial]++

    const entries: WaitTileGuideEntry[] = []
    for (let serial = 0; serial < 34; serial++) {
      if (!hasHoraFormAfterAdd(counts, handCount, serial)) continue
      entries.push({
        code: serialToPaiCode(serial),
        rest: Math.max(0, 4 - visibleCounts[serial]),
        furiten: selfDiscardCounts[serial] > 0,
      })
    }
    return entries.slice(0, 13)
  }

  private visibleTileCounts(): number[] {
    const counts = Array.from({ length: 34 }, () => 0)
    const addCode = (code: number) => {
      const serial = paiToSerial(code)
      if (serial >= 0) counts[serial]++
    }
    this.players[this.myOdr].hand.forEach(tile => addCode(tile.code))
    this.players.forEach(player => {
      player.discards.forEach(tile => addCode(tile.code))
      player.flowers.forEach(tile => addCode(tile.code))
      player.melds.forEach(meld => meld.tiles.forEach(tile => addCode(tile.code)))
    })
    this.paifuGraphRound.dora.forEach(addCode)
    return counts
  }

  private selfDiscardCounts(): number[] {
    const counts = Array.from({ length: 34 }, () => 0)
    this.players[this.myOdr].discards.forEach(tile => {
      const serial = paiToSerial(tile.code)
      if (serial >= 0) counts[serial]++
    })
    return counts
  }

  private setupKeyboardEvents() {
    this.teardownKeyboardEvents()
    this.keyboardHandler = (event: KeyboardEvent) => this.handleKeyboardEvent(event)
    window.addEventListener('keydown', this.keyboardHandler)
  }

  private teardownKeyboardEvents() {
    if (!this.keyboardHandler) return
    window.removeEventListener('keydown', this.keyboardHandler)
    this.keyboardHandler = undefined
  }

  private setupContextMenuEvents() {
    this.teardownContextMenuEvents()
    this.contextMenuHandler = (event: MouseEvent) => this.handleContextMenu(event)
    this.game.canvas.addEventListener('contextmenu', this.contextMenuHandler)
  }

  private teardownContextMenuEvents() {
    if (!this.contextMenuHandler) return
    this.game.canvas.removeEventListener('contextmenu', this.contextMenuHandler)
    this.contextMenuHandler = undefined
  }

  private handleContextMenu(event: MouseEvent) {
    event.preventDefault()
    if (this.isReplay || this.actionSendInFlight) return
    if (!this.isLocalPlayerOdr(this.currentActionSeatOrder)) return
    if (!this.actionOfferByName.has('Pass')) return
    const visibleActs = this.resolveVisibleActions(this.currentActionOffers)
    if (visibleActs.size === 1 && visibleActs.has('Pass')) return
    const pass = this.ACT_BTNS.find(button => button.act === 'Pass')
    if (pass) void this.sendAction(pass, this.currentActionOffers)
  }

  private handleKeyboardEvent(event: KeyboardEvent) {
    if (event.code === 'Escape') {
      window.dispatchEvent(new CustomEvent(GAME_FOCUS_CHAT_EVENT))
      event.preventDefault()
      return
    }
    if (this.isReplay || this.actionSendInFlight) return
    if (this.currentActionSeatOrder !== null && !this.isLocalPlayerOdr(this.currentActionSeatOrder)) return
    const canSelectTile = this.canDiscardOnTileClick || this.pendingActionChoice !== null
    const canSelectAction = this.getKeyboardActionDefs().length > 0
    if (!canSelectTile && !canSelectAction) return

    switch (event.code) {
      case 'ArrowLeft':
      case 'Numpad4':
        this.keyboardActionIndex = -1
        if (this.moveHandSelection(-1)) event.preventDefault()
        break
      case 'ArrowRight':
      case 'Numpad6':
        this.keyboardActionIndex = -1
        if (this.moveHandSelection(1)) event.preventDefault()
        break
      case 'ArrowDown':
      case 'Numpad2':
        if (this.moveKeyboardActionSelection(1)) event.preventDefault()
        break
      case 'ArrowUp':
      case 'Numpad8':
        if (this.inputConfig.nSelPasKey === 1
          ? this.executeSelectedPassKeyboardAction() || this.cancelKeyboardActionSelection()
          : this.cancelKeyboardActionSelection()) event.preventDefault()
        break
      case 'Enter':
      case 'Space':
      case 'Numpad0':
        if (this.pendingActionChoice
          ? this.executeSelectedHandTile()
          : this.executeConfiguredKeyboardAction() || this.executeSelectedHandTile()) event.preventDefault()
        break
    }
  }

  private resolveVisibleActions(acts: string[]) {
    const visibleActs = new Set(acts)
    if (visibleActs.has('Tap')) {
      visibleActs.delete('Pass')
      if (visibleActs.has('Tsumo')) visibleActs.delete('Ron')
    } else if (visibleActs.has('Ron')) {
      visibleActs.delete('Tsumo')
    }
    if (!visibleActs.has('Tap') && visibleActs.has('Pass')) {
      visibleActs.delete('Tao')
      visibleActs.delete('Hua')
    }
    return visibleActs
  }

  private getKeyboardActionDefs() {
    const visibleActs = this.resolveVisibleActions(this.currentActionOffers)
    if (visibleActs.size === 1 && visibleActs.has('Pass')) return []
    return this.ACT_BTNS.filter(def => visibleActs.has(def.act))
  }

  private setKeyboardActionSelection(index: number) {
    const defs = this.getKeyboardActionDefs()
    if (defs.length === 0) {
      this.keyboardActionIndex = -1
      return false
    }
    this.keyboardActionIndex = (index + defs.length) % defs.length
    const selected = defs[this.keyboardActionIndex]
    const visibleActs = new Set(defs.map(def => def.act))
    this.setActionButtonsEnabled(visibleActs)
    for (const def of defs) {
      const btn = this.actionButtonSprites.get(def.act)
      if (btn) btn.setFrame(def.act === selected.act ? 2 : 0)
    }
    return true
  }

  private moveKeyboardActionSelection(delta: -1 | 1) {
    const next = this.keyboardActionIndex >= 0 ? this.keyboardActionIndex + delta : 0
    return this.setKeyboardActionSelection(next)
  }

  private cancelKeyboardActionSelection() {
    if (this.pendingActionChoice) {
      this.pendingActionChoice = null
      this.keyboardActionIndex = -1
      this.redrawHand(this.myOdr)
      this.showActionButtons(this.currentActionOffers)
      return true
    }
    const defs = this.getKeyboardActionDefs()
    if (defs.length === 0) return false
    this.keyboardActionIndex = -1
    this.setActionButtonsEnabled(new Set(defs.map(def => def.act)))
    return true
  }

  private executeSelectedPassKeyboardAction() {
    const defs = this.getKeyboardActionDefs()
    if (defs.length === 0 || this.keyboardActionIndex < 0) return false
    const selected = defs[this.keyboardActionIndex]
    if (selected.act !== 'Pass') return false
    void this.sendAction(selected, this.currentActionOffers)
    return true
  }

  private executeConfiguredKeyboardAction() {
    if (this.inputConfig.nSelPasKey !== 1) return this.executeSelectedKeyboardAction()
    const defs = this.getKeyboardActionDefs()
    if (defs.length === 0 || this.keyboardActionIndex < 0) return false
    if (defs[this.keyboardActionIndex].act === 'Pass') return true
    return this.executeSelectedKeyboardAction()
  }

  private executeSelectedKeyboardAction() {
    const defs = this.getKeyboardActionDefs()
    if (defs.length === 0 || this.keyboardActionIndex < 0) return false
    const def = defs[this.keyboardActionIndex]
    void this.sendAction(def, this.currentActionOffers)
    return true
  }

  private moveHandSelection(delta: -1 | 1) {
    const hand = this.players[this.myOdr].hand
    if (hand.length === 0) return false
    if (this.selectedIdx >= 0 && this.selectedIdx < hand.length) hand[this.selectedIdx].isSelected = false
    this.selectedIdx = this.selectedIdx >= 0
      ? (this.selectedIdx + delta + hand.length) % hand.length
      : delta > 0 ? 0 : hand.length - 1
    hand[this.selectedIdx].isSelected = true
    this.redrawHand(this.myOdr)
    return true
  }

  private executeSelectedHandTile() {
    const hand = this.players[this.myOdr].hand
    if (hand.length === 0) return false
    const idx = this.selectedIdx >= 0 && this.selectedIdx < hand.length ? this.selectedIdx : hand.length - 1
    if (this.pendingActionChoice) {
      void this.selectActionChoiceTile(idx)
      return true
    }
    if (!this.canDiscardOnTileClick) return false
    void this.discard(idx)
    return true
  }

  private onTilePointerDown(idx: number) {
    const hand = this.players[this.myOdr].hand
    if (DEBUG_GAME) console.info('[GameScene] tile pointerdown', {
      idx,
      myOdr: this.myOdr,
      handLength: hand.length,
      canDiscardOnTileClick: this.canDiscardOnTileClick,
      selectedIdx: this.selectedIdx,
      tile: hand[idx],
    })
    if (this.pendingActionChoice) {
      void this.selectActionChoiceTile(idx)
      return
    }
    if (!this.canDiscardOnTileClick) return
    if (idx < 0 || idx >= hand.length) {
      if (DEBUG_GAME) console.warn('[GameScene] tile click ignored: invalid index', { idx, handLength: hand.length })
      return
    }

    if (this.selectedIdx >= 0) hand[this.selectedIdx].isSelected = false
    this.selectedIdx = idx
    hand[idx].isSelected = true
    this.redrawHand(this.myOdr)
  }

  private onTilePointerUp(idx: number) {
    const hand = this.players[this.myOdr].hand
    if (DEBUG_GAME) console.info('[GameScene] tile pointerup', {
      idx,
      myOdr: this.myOdr,
      handLength: hand.length,
      canDiscardOnTileClick: this.canDiscardOnTileClick,
      selectedIdx: this.selectedIdx,
      tile: hand[idx],
    })
    if (idx < 0 || idx >= hand.length) {
      if (DEBUG_GAME) console.warn('[GameScene] tile pointerup ignored: invalid index', { idx, handLength: hand.length })
      return
    }
    if (this.pendingActionChoice) return
    if (!this.canDiscardOnTileClick) {
      if (DEBUG_GAME) console.warn('[GameScene] tile pointerup ignored: discard is not allowed yet', {
        idx,
        myOdr: this.myOdr,
        canDiscardOnTileClick: this.canDiscardOnTileClick,
      })
      return
    }
    this.discard(idx)
  }

  /** 打牌送信 (WM_LBUTTONDOWN → PutSutepai 相当) */
  private async discard(idx: number) {
    if (this.isReplay) return
    if (this.actionSendInFlight) return
    if (!this.canSendCurrentPrompt('discard')) return
    const actionSeatOrder = this.getActionSeatOrder()
    const actionSeq = this.currentActionPrompt?.actionSeq
    const tile = this.players[actionSeatOrder].hand[idx]
    if (!tile) {
      console.error('[GameScene] discard failed: tile not found', { idx, myOdr: this.myOdr, actionSeatOrder, hand: this.players[actionSeatOrder].hand })
      return
    }
    const tapOffer = this.actionOfferByName.get('Tap')
    if (tapOffer && !tapOffer.bipaiIndex.includes(tile.bipaiIndex ?? -1)) {
      console.warn('[GameScene] discard ignored: tile is not in server Tap candidates', { idx, tile, tapCandidates: tapOffer.bipaiIndex })
      return
    }
    if (tile.bipaiIndex === undefined) {
      console.warn('[GameScene] discard ignored: missing bipaiIndex', { idx, tile, myOdr: this.myOdr, actionSeatOrder })
      return
    }
    this.actionPromptSerial++
    this.clearAutoDiscardTimer()
    this.clearActionResponseTimer()
    this.clearAutoControlTimer()
    this.hoverCursor?.destroy()
    this.hoverCursor = undefined
    this.clearTenpaiMarkers()
    this.clearWaitTileGuide()
    this.actionOfferByName.clear()
    this.currentActionOffers = []
    this.currentActionSeatOrder = null
    this.actionSendInFlight = true
    this.canDiscardOnTileClick = false
    this.selectedIdx = -1
    this.emitToUiScene('actionPromptEnd', { viewOdr: this.myOdr })
    const payload = {
      playType: 'MJPID_ACTION',
      roomId: this.roomId,
      seatOrder: actionSeatOrder,
      action: Act.Tap,
      bipaiIndex: [tile.bipaiIndex],
      actionSeq,
    }
    this.pendingAction = { seatOrder: payload.seatOrder, action: payload.action, actionSeq: payload.actionSeq }
    this.traceGameFlow('tx discard ACTION', { payload })
    if (DEBUG_GAME) console.info('[GameScene] sending discard', { idx, tile, payload })
    try {
      await SignalR.send('playing', payload)
      if (DEBUG_GAME) console.info('[GameScene] discard sent', payload)
    } catch (error) {
      this.actionSendInFlight = false
      this.pendingAction = null
      console.error('[GameScene] discard send failed', { payload, error })
      if (this.isConnectionSendError(error)) {
        this.expireCurrentActionPrompt('discard send connection lost')
        return
      }
      this.canDiscardOnTileClick = true
      this.scheduleAutoDiscard(3)
    }
  }

  private async sendAction(def: { act: string; code: Act }, acts: string[]) {
    if (this.actionSendInFlight) return
    const offer = this.actionOfferByName.get(def.act)
    const choices = this.actionChoicesByName.get(def.act) ?? []
    if (choices.length > 1) {
      this.beginActionChoice(def, acts, choices)
      return
    }
    const bipaiIndex = choices[0]?.bipaiIndex
      ?? offer?.bipaiIndex
      ?? []
    const actionCode = offer?.code ?? choices[0]?.code ?? def.code
    if (this.actionRequiresBipai(actionCode) && bipaiIndex.length === 0) {
      console.warn('[GameScene] action ignored: missing server bipaiIndex', { action: def.act, actionCode })
      return
    }
    await this.sendActionWithBipai(def, acts, actionCode, bipaiIndex)
  }

  private actionRequiresBipai(actionCode: Act) {
    return actionCode === Act.Tap
      || actionCode === Act.Ric
      || actionCode === Act.Ank
      || actionCode === Act.Cha
      || actionCode === Act.Kan
      || actionCode === Act.Pon
      || actionCode === Act.Chi
      || actionCode === Act.Hua
  }

  private beginActionChoice(def: { act: string; code: Act }, acts: string[], choices: Array<{ code: Act; bipaiIndex: number[] }>) {
    this.keyboardActionIndex = -1
    this.pendingActionChoice = { def, acts, choices }
    this.canDiscardOnTileClick = false
    this.selectedIdx = -1
    this.clearActionButtons()
    this.redrawHand(this.myOdr)
  }

  private async selectActionChoiceTile(idx: number) {
    const pending = this.pendingActionChoice
    if (!pending) return
    const hand = this.players[this.myOdr].hand
    const tile = hand[idx]
    const bipaiIndex = tile?.bipaiIndex
    if (bipaiIndex === undefined) return

    const selectedBipai = this.selectedIdx >= 0 ? hand[this.selectedIdx]?.bipaiIndex : undefined
    const exact = selectedBipai !== undefined && selectedBipai !== bipaiIndex
      ? pending.choices.find(choice => choice.bipaiIndex.includes(selectedBipai) && choice.bipaiIndex.includes(bipaiIndex))
      : undefined
    if (exact) {
      await this.sendActionWithBipai(pending.def, pending.acts, exact.code, exact.bipaiIndex)
      return
    }

    const matches = pending.choices.filter(choice => choice.bipaiIndex.includes(bipaiIndex))
    if (matches.length === 1) {
      await this.sendActionWithBipai(pending.def, pending.acts, matches[0].code, matches[0].bipaiIndex)
      return
    }

    if (this.selectedIdx >= 0) hand[this.selectedIdx].isSelected = false
    this.selectedIdx = idx
    tile.isSelected = true
    this.redrawHand(this.myOdr)
  }

  private async sendActionWithBipai(def: { act: string; code: Act }, acts: string[], actionCode: Act, bipaiIndex: number[]) {
    if (this.actionSendInFlight) return
    if (!this.canSendCurrentPrompt('sendAction')) return
    const actionSeatOrder = this.getActionSeatOrder()
    const actionSeq = this.currentActionPrompt?.actionSeq
    this.actionPromptSerial++
    this.clearActionButtons()
    this.clearAutoDiscardTimer()
    this.clearActionResponseTimer()
    this.clearAutoControlTimer()
    this.hoverCursor?.destroy()
    this.hoverCursor = undefined
    this.clearTenpaiMarkers()
    this.clearWaitTileGuide()
    this.actionOfferByName.clear()
    this.actionChoicesByName.clear()
    this.pendingActionChoice = null
    this.currentActionOffers = []
    this.currentActionSeatOrder = null
    this.actionSendInFlight = true
    this.canDiscardOnTileClick = false
    this.emitToUiScene('actionPromptEnd', { viewOdr: this.myOdr })
    const payload = {
      playType: 'MJPID_ACTION',
      roomId: this.roomId,
      seatOrder: actionSeatOrder,
      action: actionCode,
      bipaiIndex,
      actionSeq,
    }
    this.pendingAction = { seatOrder: payload.seatOrder, action: payload.action, actionSeq: payload.actionSeq }
    this.traceGameFlow('tx button ACTION', { def, payload })
    if (DEBUG_GAME) console.info('[GameScene] sending action', { def, payload })
    try {
      await SignalR.send('playing', payload)
      if (DEBUG_GAME) console.info('[GameScene] action sent', payload)
    } catch (error) {
      this.actionSendInFlight = false
      this.pendingAction = null
      console.error('[GameScene] action send failed', { payload, error })
      if (this.isConnectionSendError(error)) {
        this.expireCurrentActionPrompt('action send connection lost')
        return
      }
      this.showActionButtons(acts)
    }
  }

  private isConnectionSendError(error: unknown) {
    if (!SignalR.isConnected()) return true
    const message = error instanceof Error ? error.message : String(error ?? '')
    return message.includes('underlying connection being closed')
      || message.includes('SignalR not connected')
      || message.includes('Invocation canceled')
  }

  private applyAutoControl(acts: string[]) {
    if (this.isReplay) return
    const autoAction = resolveAutoControlAction(this.autoControl, acts)
    const choose = (act: string) => this.ACT_BTNS.find(button => button.act === act)

    if (autoAction === 'Tap') {
      this.discard(this.players[this.getActionSeatOrder()].hand.length - 1)
      return
    }

    const def = autoAction ? choose(autoAction) : undefined
    if (def) void this.sendAction(def, acts)
  }

  private scheduleAutoControl(acts: string[], promptSerial = this.actionPromptSerial) {
    this.clearAutoControlTimer()
    if (this.isReplay) return
    if (!resolveAutoControlAction(this.autoControl, acts)) return
    this.autoControlTimer = this.time.delayedCall(50, () => {
      if (promptSerial !== this.actionPromptSerial) return
      if (!this.isLocalPlayerOdr(this.currentActionSeatOrder)) return
      if (!this.canSendCurrentPrompt('auto control')) return
      if (!resolveAutoControlAction(this.autoControl, acts)) return
      this.applyAutoControl(acts)
    })
  }

  private scheduleAutoDiscard(timeLimitSeconds: number, promptSerial = this.actionPromptSerial) {
    this.clearAutoDiscardTimer()
    if (this.isReplay || !this.canDiscardOnTileClick) return
    const delay = Math.max(1000, Math.trunc((Number.isFinite(timeLimitSeconds) && timeLimitSeconds > 0 ? timeLimitSeconds : 5) * 1000))
    this.autoDiscardTimer = this.time.delayedCall(delay, () => {
      if (promptSerial !== this.actionPromptSerial) return
      if (!this.canSendCurrentPrompt('auto discard timeout')) return
      if (!this.isLocalPlayerOdr(this.currentActionSeatOrder)) return
      if (!this.canDiscardOnTileClick) return
      const actionSeatOrder = this.getActionSeatOrder()
      const hand = this.players[actionSeatOrder].hand
      const idx = hand.length - 1
      if (idx < 0) {
        console.error('[GameScene] auto discard failed: hand is empty', { myOdr: this.myOdr, actionSeatOrder })
        return
      }
      if (DEBUG_GAME) console.warn('[GameScene] auto discard by turn timeout', { myOdr: this.myOdr, actionSeatOrder, idx, tile: hand[idx] })
      this.discard(idx)
    })
  }

  private scheduleDefaultPass(acts: string[], timeLimitSeconds: number, promptSerial = this.actionPromptSerial) {
    this.clearActionResponseTimer()
    if (this.isReplay) return
    const pass = this.ACT_BTNS.find(button => button.act === 'Pass')
    if (!pass) return
    const passOnly = acts.length > 0 && acts.every(action => action === 'Pass')
    if (passOnly) {
      if (promptSerial !== this.actionPromptSerial) return
      if (!this.isLocalPlayerOdr(this.currentActionSeatOrder)) return
      if (this.canDiscardOnTileClick) return
      if (!this.actionOfferByName.has('Pass')) return
      if (DEBUG_GAME) console.warn('[GameScene] immediate default pass for pass-only prompt', { myOdr: this.myOdr, actionSeatOrder: this.getActionSeatOrder(), acts })
      void this.sendAction(pass, acts)
      return
    }
    const delay = Math.max(1000, Math.trunc((Number.isFinite(timeLimitSeconds) && timeLimitSeconds > 0 ? timeLimitSeconds : 5) * 1000))
    this.actionResponseTimer = this.time.delayedCall(delay, () => {
      if (promptSerial !== this.actionPromptSerial) return
      if (!this.canSendCurrentPrompt('default pass timeout')) return
      if (!this.isLocalPlayerOdr(this.currentActionSeatOrder)) return
      if (this.canDiscardOnTileClick) return
      if (!this.actionOfferByName.has('Pass')) return
      if (DEBUG_GAME) console.warn('[GameScene] default pass by action timeout', { myOdr: this.myOdr, actionSeatOrder: this.getActionSeatOrder(), acts })
      void this.sendAction(pass, acts)
    })
  }

  private scheduleTimeWarnings(remainingMs: number, promptSerial = this.actionPromptSerial) {
    this.clearTimeWarningTimers()
    if (this.isReplay || remainingMs <= 0) return
    const firstWarning = Math.min(TIME_WARNING_START_MS, Math.max(0, Math.trunc((remainingMs - 500) / 1000) * 1000))
    for (let warningMs = firstWarning; warningMs > 0; warningMs -= 1000) {
      const delay = remainingMs - warningMs
      if (delay < 0) continue
      this.timeWarningTimers.push(this.time.delayedCall(delay, () => {
        if (promptSerial !== this.actionPromptSerial) return
        if (!this.currentActionPrompt) return
        this.emitGameStatus(`${Math.ceil(warningMs / 1000)}秒・・・`, '#ff1414', true)
        playMajakSid(SID_TIME, this.soundSkinOptions())
      }))
    }
  }

  private getActionSeatOrder() {
    return this.currentActionSeatOrder ?? this.myOdr
  }

  private canSendCurrentPrompt(reason: string) {
    const prompt = this.currentActionPrompt
    if (!prompt) return true
    if (performance.now() < prompt.localDeadlineAt) return true
    this.expireCurrentActionPrompt(reason)
    return false
  }

  private expireCurrentActionPrompt(reason: string) {
    if (DEBUG_GAME) console.warn('[GameScene] action prompt expired', { reason, prompt: this.currentActionPrompt, localNow: performance.now() })
    this.actionPromptSerial++
    this.clearAutoDiscardTimer()
    this.clearActionResponseTimer()
    this.clearAutoControlTimer()
    this.clearTimeWarningTimers()
    this.clearActionButtons()
    this.actionOfferByName.clear()
    this.actionChoicesByName.clear()
    this.pendingActionChoice = null
    this.currentActionOffers = []
    this.currentActionSeatOrder = null
    this.currentActionPrompt = null
    this.canDiscardOnTileClick = false
    this.selectedIdx = -1
    this.clearTenpaiMarkers()
    this.clearWaitTileGuide()
    this.emitToUiScene('actionPromptEnd', { viewOdr: this.myOdr })
  }

  private reconcileTapCandidates(seatOrder: number) {
    const tapOffer = this.actionOfferByName.get('Tap')
    if (!tapOffer || seatOrder < 0 || seatOrder >= this.players.length) return
    const hand = this.players[seatOrder].hand
    const byBipaiIndex = new Map<number, TileState>()
    hand.forEach(tile => {
      if (tile.bipaiIndex !== undefined) byBipaiIndex.set(tile.bipaiIndex, tile)
    })
    if (tapOffer.bipaiIndex.length < Math.max(hand.length, 13)) {
      const missingCandidates = tapOffer.bipaiIndex
        .filter(bipaiIndex => !byBipaiIndex.has(bipaiIndex))
        .map(bipaiIndex => this.tileFromKnownPai(bipaiIndex))
        .filter((tile): tile is TileState => Boolean(tile))
      if (this.isLocalPlayerOdr(seatOrder) && hand.length % 3 === 1 && missingCandidates.length === 1) {
        hand.push(missingCandidates[0])
        this.redrawHand(seatOrder)
      }
      return
    }
    const reconciled = tapOffer.bipaiIndex
      .map(bipaiIndex => byBipaiIndex.get(bipaiIndex) ?? this.tileFromKnownPai(bipaiIndex))
      .filter((tile): tile is TileState => Boolean(tile))
    if (reconciled.length !== tapOffer.bipaiIndex.length) return

    const sameHand = hand.length === reconciled.length
      && hand.every((tile, idx) => tile.bipaiIndex === reconciled[idx].bipaiIndex)
    if (sameHand) return
    this.players[seatOrder].hand = reconciled
    if (this.paifuGraphInitialHands[seatOrder].length === 0 && reconciled.length >= 13) {
      this.paifuGraphInitialHands[seatOrder] = this.cloneTiles(reconciled)
    }
    if (DEBUG_GAME) console.warn('[GameScene] hand reconciled to server Tap candidates', { seatOrder, tapCandidates: tapOffer.bipaiIndex })
    this.redrawHand(seatOrder)
  }

  private tileFromKnownPai(bipaiIndex: number): TileState | undefined {
    const code = this.knownPai.get(bipaiIndex)
    return code ? { code, bipaiIndex, isSelected: false } : undefined
  }

  private clearAutoDiscardTimer() {
    this.autoDiscardTimer?.destroy()
    this.autoDiscardTimer = undefined
  }

  private clearLiveActionState(reason: string) {
    this.traceGameFlow('clear live action state', { reason })
    this.actionPromptSerial++
    this.actionSendInFlight = false
    this.pendingAction = null
    this.currentActionPrompt = null
    this.currentActionSeatOrder = null
    this.canDiscardOnTileClick = false
    this.selectedIdx = -1
    this.keyboardActionIndex = -1
    this.actionOfferByName.clear()
    this.actionChoicesByName.clear()
    this.pendingActionChoice = null
    this.currentActionOffers = []
    this.currentHoraErrorReason = ''
    this.clearAutoDiscardTimer()
    this.clearActionResponseTimer()
    this.clearAutoControlTimer()
    this.clearTimeWarningTimers()
    this.clearActionButtons()
    this.clearTenpaiMarkers()
    this.clearWaitTileGuide()
    this.emitToUiScene('actionPromptEnd', { viewOdr: this.myOdr })
  }

  private clearActionResponseTimer() {
    this.actionResponseTimer?.destroy()
    this.actionResponseTimer = undefined
  }

  private clearAutoControlTimer() {
    this.autoControlTimer?.destroy()
    this.autoControlTimer = undefined
  }

  private clearTimeWarningTimers() {
    this.timeWarningTimers.forEach(timer => timer.destroy())
    this.timeWarningTimers = []
  }

  /* ======================================================================
   * 操作ボタン (CMJUserIF1 相当)
   * ====================================================================== */
  private get ACT_BTNS(): { key: string; label: string; act: string; code: Act; x: number; y: number; w: number; h: number }[] {
    return [
      { key: 'btn_kan',   label: 'カン',  act: 'Kan',   code: Act.Kan, x: ACTION_BUTTON_LAYOUT.kan.x,   y: ACTION_BUTTON_LAYOUT.kan.y,   w: ACTION_BUTTON_LAYOUT.kan.width,   h: ACTION_BUTTON_LAYOUT.kan.height },
      { key: 'btn_pon',   label: 'ポン',  act: 'Pon',   code: Act.Pon, x: ACTION_BUTTON_LAYOUT.pon.x,   y: ACTION_BUTTON_LAYOUT.pon.y,   w: ACTION_BUTTON_LAYOUT.pon.width,   h: ACTION_BUTTON_LAYOUT.pon.height },
      { key: 'btn_chi',   label: 'チー',  act: 'Chi',   code: Act.Chi, x: ACTION_BUTTON_LAYOUT.chi.x,   y: ACTION_BUTTON_LAYOUT.chi.y,   w: ACTION_BUTTON_LAYOUT.chi.width,   h: ACTION_BUTTON_LAYOUT.chi.height },
      { key: 'btn_reach', label: 'リーチ', act: 'Reach', code: Act.Ric, x: ACTION_BUTTON_LAYOUT.reach.x, y: ACTION_BUTTON_LAYOUT.reach.y, w: ACTION_BUTTON_LAYOUT.reach.width, h: ACTION_BUTTON_LAYOUT.reach.height },
      { key: 'btn_ron',   label: 'ロン',  act: 'Ron',   code: Act.Ron, x: ACTION_BUTTON_LAYOUT.ron.x,   y: ACTION_BUTTON_LAYOUT.ron.y,   w: ACTION_BUTTON_LAYOUT.ron.width, h: ACTION_BUTTON_LAYOUT.ron.height },
      { key: 'btn_tsumo', label: 'ツモ',  act: 'Tsumo', code: Act.Tsu, x: ACTION_BUTTON_LAYOUT.tsumo.x, y: ACTION_BUTTON_LAYOUT.tsumo.y, w: ACTION_BUTTON_LAYOUT.tsumo.width, h: ACTION_BUTTON_LAYOUT.tsumo.height },
      { key: 'btn_pass',  label: 'パス',  act: 'Pass',  code: Act.Pas, x: ACTION_BUTTON_LAYOUT.pass.x,  y: ACTION_BUTTON_LAYOUT.pass.y,  w: ACTION_BUTTON_LAYOUT.pass.width,  h: ACTION_BUTTON_LAYOUT.pass.height },
      { key: 'btn_flow',  label: '流局',  act: 'Tao',   code: Act.Tao, x: ACTION_BUTTON_LAYOUT.flow.x,  y: ACTION_BUTTON_LAYOUT.flow.y,  w: ACTION_BUTTON_LAYOUT.flow.width,  h: ACTION_BUTTON_LAYOUT.flow.height },
      { key: 'btn_hua',   label: '花',    act: 'Hua',   code: Act.Hua, x: ACTION_BUTTON_LAYOUT.hua.x,   y: ACTION_BUTTON_LAYOUT.hua.y,   w: ACTION_BUTTON_LAYOUT.hua.width,   h: ACTION_BUTTON_LAYOUT.hua.height },
    ]
  }
  private readonly DEFAULT_ACTION_BUTTONS = new Set(['Kan', 'Pon', 'Chi', 'Reach', 'Ron', 'Pass'])
  private readonly MOBILE_HAND_ACTION_BUTTONS = new Set(['Kan', 'Pon', 'Chi', 'Reach', 'Ron', 'Pass'])

  private createActionButtons() {
    for (const def of this.ACT_BTNS) {
      const pos = boardLocalPoint({ x: def.x, y: def.y })
      const btn = this.clipToBoard(this.add.sprite(pos.x + def.w / 2, pos.y + def.h / 2, this.resolveSkinTextureKey(def.key), 1)
        .setDisplaySize(def.w, def.h)
        .setDepth(Z_PANEL + 10)
        .setVisible(true))
      this.actionButtonSprites.set(def.act, btn)
    }
    const horaErrorLayout = ACTION_BUTTON_LAYOUT.horaError
    const horaPos = boardLocalPoint({ x: horaErrorLayout.x, y: horaErrorLayout.y })
    this.horaErrorSprite = this.clipToBoard(this.add.sprite(horaPos.x + horaErrorLayout.width / 2, horaPos.y + horaErrorLayout.height / 2, this.resolveSkinTextureKey('btn_fury'), 0)
      .setDisplaySize(horaErrorLayout.width, horaErrorLayout.height)
      .setDepth(Z_PANEL + 11)
      .setVisible(false))
    this.setActionButtonsEnabled(new Set())
  }

  private visibleActionButtonDefs(visibleActs: Set<string>) {
    return this.ACT_BTNS.filter(def => visibleActs.has(def.act))
  }

  private displayedActionButtonDefs(visibleActs: Set<string>) {
    if (this.layoutMode === 'mobileLandscape' && this.shouldReplaceMobileHandWithActions(visibleActs)) {
      return this.ACT_BTNS.filter(def => this.MOBILE_HAND_ACTION_BUTTONS.has(def.act) || visibleActs.has(def.act))
    }
    return this.visibleActionButtonDefs(visibleActs)
  }

  private shouldReplaceMobileHandWithActions(visibleActs: Set<string>) {
    if (this.layoutMode !== 'mobileLandscape' || this.isViewer || this.isReplay) return false
    if (this.canDiscardOnTileClick || this.currentActionPrompt?.playerMode === 'Turn') return false
    return this.visibleActionButtonDefs(visibleActs).length > 0
  }

  private updateActionButtonPositions(visibleActs: Set<string>) {
    if (this.layoutMode !== 'mobileLandscape') {
      for (const def of this.ACT_BTNS) {
        const btn = this.actionButtonSprites.get(def.act)
        if (!btn) continue
        const pos = boardLocalPoint({ x: def.x, y: def.y })
        btn.setPosition(pos.x + def.w / 2, pos.y + def.h / 2)
      }
      return
    }

    const displayedDefs = this.displayedActionButtonDefs(visibleActs)
    if (displayedDefs.length === 0) return
    const replaceHand = this.shouldReplaceMobileHandWithActions(visibleActs)
    const gap = 4
    const totalWidth = displayedDefs.reduce((sum, def) => sum + def.w, 0) + gap * Math.max(0, displayedDefs.length - 1)
    const maxHeight = Math.max(...displayedDefs.map(def => def.h))
    const handCount = MOBILE_SELF_HAND_FIXED_COUNT
    const handScale = MOBILE_SELF_HAND_TILE_SCALE * mobileContentScale()
    const handStart = mobileOuterHandPos(0, 0, handCount, false, handScale) ?? handPos(0, 0, false)
    const tileWidth = 37 * handScale
    const handWidth = Math.max(0, handCount - 1) * tileWidth + tileWidth
    const handHeight = 63 * handScale
    let x = handStart.x + handWidth / 2 - totalWidth / 2
    const y = replaceHand
      ? handStart.y + Math.max(0, (handHeight - maxHeight) / 2) - 2
      : handStart.y - maxHeight - 8

    for (const def of displayedDefs) {
      const btn = this.actionButtonSprites.get(def.act)
      if (btn) btn.setPosition(x + def.w / 2, y + def.h / 2)
      x += def.w + gap
    }
  }

  private setHoraErrorVisible(reason: string) {
    if (!this.horaErrorSprite) return
    const frame = reason === 'furiten' ? 1 : reason === 'sameTurnFuriten' ? 2 : reason === 'invalid' ? 0 : -1
    if (frame < 0) {
      this.horaErrorSprite.setVisible(false)
      return
    }
    this.horaErrorSprite.setFrame(frame).setVisible(true)
  }

  private setActionButtonsEnabled(visibleActs: Set<string>) {
    const tsumoVisible = visibleActs.has('Tsumo')
    const passAlternateVisible = visibleActs.has('Tao') || visibleActs.has('Hua')
    const replaceMobileHand = this.shouldReplaceMobileHandWithActions(visibleActs)
    const displayedMobileActs = new Set(this.displayedActionButtonDefs(visibleActs).map(def => def.act))
    this.updateActionButtonPositions(visibleActs)
    for (const def of this.ACT_BTNS) {
      const btn = this.actionButtonSprites.get(def.act)
      if (!btn) continue
      const enabled = visibleActs.has(def.act)
      const defaultVisible = this.layoutMode !== 'mobileLandscape' && this.DEFAULT_ACTION_BUTTONS.has(def.act)
        && !(def.act === 'Ron' && tsumoVisible)
        && !(def.act === 'Pass' && passAlternateVisible)
      const mobileVisible = replaceMobileHand && displayedMobileActs.has(def.act)
      btn.removeAllListeners()
      btn.disableInteractive()
      btn.setFrame(enabled ? 0 : 1)
      if (!enabled && !defaultVisible && !mobileVisible) {
        btn.setVisible(false)
        continue
      }
      btn.setVisible(true)
      if (!enabled) continue
      btn.setFrame(0)
        .setInteractive({ useHandCursor: true })
        .on('pointerover', () => btn.setFrame(2))
        .on('pointerout',  () => btn.setFrame(0))
        .on('pointerdown', () => btn.setFrame(3))
        .on('pointerup', async () => {
          btn.setFrame(2)
          await this.sendAction(def, [...visibleActs])
        })
    }
      this.actionPanelSprite?.setVisible(this.layoutMode !== 'mobileLandscape')
      this.mobileActionButtonsVisible = replaceMobileHand
      this.updateMobileActionHandVisibility()
  }

  showActionButtons(acts: string[]) {
    if (this.isReplay) {
      if (DEBUG_GAME) console.info('[GameScene] showActionButtons ignored in replay', { acts })
      return
    }
    this.clearActionButtons()
    this.keyboardActionIndex = -1
    const visibleActs = this.resolveVisibleActions(acts)
    if (DEBUG_GAME) console.info('[GameScene] showActionButtons resolved', { acts, visibleActs: [...visibleActs], myOdr: this.myOdr, selectedIdx: this.selectedIdx })
    if (visibleActs.size === 1 && visibleActs.has('Pass')) {
      if (DEBUG_GAME) console.info('[GameScene] pass-only prompt uses automatic response without an active button', { acts, myOdr: this.myOdr })
      this.setActionButtonsEnabled(new Set())
      this.setHoraErrorVisible(this.currentHoraErrorReason)
      return
    }
    this.setActionButtonsEnabled(visibleActs)
    const showHoraError = !visibleActs.has('Ron') && !visibleActs.has('Tsumo')
    this.setHoraErrorVisible(showHoraError ? this.currentHoraErrorReason : '')
  }

  clearActionButtons() {
    this.actionBtns.forEach(b => b.destroy())
    this.actionBtns = []
    this.setActionButtonsEnabled(new Set())
    this.setHoraErrorVisible('')
  }

  private extractActionOffers(data: Record<string, unknown>): string[] {
    const names = new Set<string>()
    this.actionOfferByName.clear()
    this.actionChoicesByName.clear()

    const addByCode = (code: number, indices?: number[]) => {
      const def = this.findActionButton(code)
      if (!def) return
      names.add(def.act)
      if (indices) {
        const item = { code: code as Act, bipaiIndex: indices }
        this.actionOfferByName.set(def.act, item)
        const choices = this.actionChoicesByName.get(def.act) ?? []
        choices.push(item)
        this.actionChoicesByName.set(def.act, choices)
      }
    }

    const actions = Array.isArray(data.actions) ? data.actions : []
    for (const action of actions) {
      if (typeof action === 'string') {
        const actionName = action === 'Flow' ? 'Tao' : action
        const def = this.ACT_BTNS.find(button => button.act === actionName)
        if (def) names.add(def.act)
        continue
      }
      if (!action || typeof action !== 'object') continue
      const record = action as Record<string, unknown>
      const code = Number(record.code ?? record.action ?? -1)
      const indices = Array.isArray(record.bipaiIndex)
        ? record.bipaiIndex.map(Number)
        : Array.isArray(record.indices)
          ? record.indices.map(Number)
          : undefined
      const defByCode = Number.isFinite(code) && code >= 0 ? this.findActionButton(code) : undefined
      if (defByCode) addByCode(code, indices)
      const rawName = String(record.act ?? record.name ?? '')
      const name = rawName === 'Flow' ? 'Tao' : rawName
      const def = this.ACT_BTNS.find(button => button.act === name)
      if (def && (!defByCode || def.act === defByCode.act)) {
        names.add(def.act)
        if (indices) {
          const item = { code: code >= 0 ? code as Act : def.code, bipaiIndex: indices }
          this.actionOfferByName.set(def.act, item)
          const choices = this.actionChoicesByName.get(def.act) ?? []
          if (!choices.some(choice => choice.code === item.code && choice.bipaiIndex.join(',') === item.bipaiIndex.join(','))) choices.push(item)
          this.actionChoicesByName.set(def.act, choices)
        }
      }
    }

    const tapCandidates = Array.isArray(data.tapCandidates)
      ? data.tapCandidates.map(Number).filter(Number.isFinite)
      : []
    if (tapCandidates.length > 0) {
      names.add('Tap')
      this.actionOfferByName.set('Tap', { code: Act.Tap, bipaiIndex: tapCandidates })
    }

    const flagValue = Number(data.actFlags ?? data.actionFlags ?? data.actFlg ?? data.actFlag ?? -1)
    if (Number.isFinite(flagValue) && flagValue > 0) {
      if ((flagValue & (1 << Act.Tap)) !== 0) names.add('Tap')
      for (const def of this.ACT_BTNS) {
        if ((flagValue & (1 << def.code)) !== 0) addByCode(def.code)
      }
    }

    return [...names]
  }

  private findActionButton(code: number) {
    if (code === Act.Ank || code === Act.Cha) return this.ACT_BTNS.find(button => button.act === 'Kan')
    if (code === Act.Tsu) return this.ACT_BTNS.find(button => button.act === 'Tsumo')
    return this.ACT_BTNS.find(button => button.code === code)
  }

  update() {
    const centerInfoLayoutKey = mobileVisibleWorldLayoutKey(this.layoutMode)
    if (centerInfoLayoutKey !== this.mobileCenterInfoLayoutKey) {
      this.mobileCenterInfoLayoutKey = centerInfoLayoutKey
      this.updateCenterInfoLayout()
      if (this.layoutMode === 'mobileLandscape') {
        this.players.forEach((_player, odr) => {
          this.redrawHand(odr)
          this.redrawDiscards(odr)
        })
      }
    }

    if (this.layoutMode !== 'mobileLandscape' || this.isReplay) return
    const summaryKey = this.players
      .map((player, odr) => `${odr}:${player.hand.length}:${odrToLoc(odr, this.myOdr)}:${this.mobileOpponentHandCountTexts[odr] ? 1 : 0}:${this.handSprites[odr].length}`)
      .join('|')
    if (summaryKey === this.mobileHandSummaryStateKey) return
    this.mobileHandSummaryStateKey = summaryKey
    this.players.forEach((_player, odr) => {
      const loc = odrToLoc(odr, this.myOdr)
      if (!this.mobileOpponentHandCountTexts[odr] && this.handSprites[odr].length > 2) return
      if (this.players[odr].hand.length <= this.handSprites[odr].length && !this.mobileOpponentHandCountTexts[odr]) return
      void loc
      this.redrawHand(odr)
    })
  }

  private applyActionPacket(data: Record<string, unknown>) {
    const odr = Number(data.seatOrder ?? data.order ?? 0)
    const action = Number(data.action ?? -1)
    const indices = Array.isArray(data.bipaiIndex) ? data.bipaiIndex.map(Number) : []
    const suppressLivePlayback = this.shouldSuppressLivePlayback()
    let materializedDiscard = false
    if (!suppressLivePlayback && action >= 0 && odr >= 0 && odr < this.players.length) this.showCallAction(odr, action as Act)

    if ((action === Act.Tap || action === Act.Ric) && odr >= 0 && odr < this.players.length) {
      const bipaiIndex = indices[0]
      const hand = this.players[odr].hand
      const handIdx = hand.findIndex(t => t.bipaiIndex === bipaiIndex)
      const animateDiscard = action === Act.Tap && this.shouldAnimateLiveDiscard()
      const flightOrigin = animateDiscard ? this.captureDiscardFlightOrigin(odr, handIdx) : undefined
      this.logDiscardProbe('apply discard ACTION start', {
        action,
        odr,
        bipaiIndex,
        actionSeq: data.actionSeq,
        hand: this.handProbe(odr, bipaiIndex),
      })
      const removed = handIdx >= 0 ? hand.splice(handIdx, 1)[0] : undefined
      if (handIdx >= 0) hand.sort((a, b) => paiToFrame(a.code) - paiToFrame(b.code))
      if (handIdx < 0 && !this.isLocalPlayerOdr(odr)) {
        this.logDiscardProbe('opponent hand missing action bipaiIndex; placeholder pop path', {
          action,
          odr,
          bipaiIndex,
          handLengthBeforeFallback: hand.length,
          handLengthMod3BeforeFallback: hand.length % 3,
          knownCode: this.knownPai.get(bipaiIndex) ?? 0,
          latestActionPaiInfoTiles: (this.latestActionPaiInfoTiles[odr] ?? []).slice(0, 16).map(tile => ({ idx: tile.bipaiIndex, code: tile.code })),
        })
        if (hand.length % 3 === 1) hand.push({ code: 0, isSelected: false })
        if (hand.length > 0) hand.pop()
      }
      const code = handIdx >= 0 && removed && removed.code > 0
        ? removed.code
        : this.knownPai.get(bipaiIndex) ?? 0
      const isDoraDiscard = code > 0 && this.isDoraTile(code)
      if (handIdx < 0 && this.isLocalPlayerOdr(odr)) {
        if (DEBUG_GAME) console.warn('[GameScene] action apply could not find bipaiIndex in hand', { odr, action, bipaiIndex, hand, data })
      }
      if (code > 0) {
        const appended = this.appendDiscard(odr, bipaiIndex, code, action === Act.Ric || this.players[odr].reachDiscardCarry)
        this.logDiscardProbe('materialize discard ACTION', { action, odr, bipaiIndex, code, appended })
        materializedDiscard = appended
      } else if (Number.isInteger(bipaiIndex) && bipaiIndex >= 0) {
        this.pendingDiscardsByBipaiIndex.set(bipaiIndex, {
          odr,
          bipaiIndex,
          isReach: action === Act.Ric || this.players[odr].reachDiscardCarry,
          playReachFeedback: !suppressLivePlayback && action === Act.Ric,
          flightOrigin,
          animateDiscard,
        })
        this.logDiscardProbe('defer discard ACTION until PaiInfo reveals code', { action, odr, bipaiIndex })
      }
      this.lastDiscardOdr = odr
      this.redrawHand(odr)
      this.redrawDiscards(odr)
      if (materializedDiscard && animateDiscard) this.animateLatestDiscard(odr, flightOrigin)
      if (!suppressLivePlayback && action === Act.Tap) playMajakSid(isDoraDiscard ? SID_THROW_DORA : SID_THROW, this.soundSkinOptions())
      if (!suppressLivePlayback && action === Act.Ric) {
        this.playReachDiscardSound(odr)
        this.showReachTileEffect(odr)
      }
    }

    if (action < 0) {
      console.warn('[GameScene] action packet ignored: invalid action', { data })
    }

    if (action === Act.Ron && odr >= 0 && odr < this.players.length) {
      const claimedOdr = this.readClaimedOdr(data, odr, action)
      const claimed = this.players[claimedOdr]?.discards.pop()
      if (claimed?.isReach) this.players[claimedOdr].reachDiscardCarry = true
      if (claimed) {
        this.lastDiscardOdr = null
        this.redrawDiscards(claimedOdr)
      }
    }

    if (action === Act.Ric) {
      this.players[odr].isReach = true
      this.emitToUiScene('reach', { odr, viewOdr: this.myOdr })
      if (!suppressLivePlayback && materializedDiscard) {
        this.playReachBgm()
        playMajakSid(SID_RICSTK, this.soundSkinOptions())
      }
    }

    if ([Act.Chi, Act.Pon, Act.Kan, Act.Ank, Act.Cha, Act.Hua].includes(action) && odr >= 0 && odr < this.players.length) {
      if (action === Act.Kan || action === Act.Ank || action === Act.Cha) {
        this.syncLiveDoraIndicators(this.paifuGraphRound.dora.length + 1)
      }
      this.applyMeldAction(odr, action as Act, indices, this.readClaimedOdr(data, odr, action as Act), suppressLivePlayback)
    }
  }

  private showCallAction(odr: number, action: Act) {
    this.playCallActionSound(odr, action)
    this.showBoardEffect(odr, action)

    const frame = this.callBalloonFrame(action)
    if (frame === undefined) return
    const player = this.players[odr]
    this.emitToUiScene('callAction', {
      odr,
      frame,
      avatarUrl: this.callAvatarUrl(player),
      fallbackAvatarUrl: player.avatarUrl || player.fallbackAvatarUrl || this.callAvatarUrl(player),
    })
  }

  private callAvatarUrl(player: PlayerState) {
    const costumeId = Number(player.customCostume ?? 0)
    if (Number.isFinite(costumeId) && costumeId > 0 && costumeId !== CUSTOM_DEFAULT_ID_COSTUME && AVAILABLE_COSTUME_IDS.has(costumeId)) {
      const costumeType = Number(player.customCostumeType ?? 0)
      if (costumeType <= 0 || (costumeType >= 30 && costumeType < 40)) {
        const imageId = String(costumeId).padStart(2, '0')
        return `${IMG}/skin/${costumeId}/mj_costume_default_${imageId}.png`
      }
    }
    if (player.avatarId) return getGameAvatarUrl(player.avatarId)
    return player.fallbackAvatarUrl ?? getDefaultAvatarUrl(player.sex === 'F' || player.sex === 'female' ? 'female' : 'male')
  }

  private callBalloonFrame(action: Act): number | undefined {
    if (action === Act.Ric) return 0
    if (action === Act.Tsu) return 1
    if (action === Act.Pon) return 2
    if (action === Act.Kan || action === Act.Ank || action === Act.Cha) return 3
    if (action === Act.Chi) return 4
    if (action === Act.Ron) return 5
    return undefined
  }

  private appendDiscard(odr: number, bipaiIndex: number | undefined, code: number, isReach: boolean) {
    if (odr < 0 || odr >= this.players.length || code <= 0) return false
    if (bipaiIndex !== undefined && this.players[odr].discards.some(discard => discard.bipaiIndex === bipaiIndex)) {
      this.logDiscardProbe('skip duplicate discard append', { odr, bipaiIndex, code, isReach })
      return false
    }
    const discard = { code, bipaiIndex, isReach }
    this.players[odr].reachDiscardCarry = false
    this.players[odr].discards.push(discard)
    this.paifuGraphDiscards[odr].push(discard)
    return true
  }

  private flushPendingDiscards() {
    if (this.pendingDiscardsByBipaiIndex.size === 0) return
    const changedOdr = new Set<number>()
    const flightOrigins = new Map<number, DiscardFlightOrigin>()
    for (const [bipaiIndex, pending] of [...this.pendingDiscardsByBipaiIndex]) {
      const code = this.knownPai.get(bipaiIndex) ?? 0
      if (code <= 0) continue
      this.pendingDiscardsByBipaiIndex.delete(bipaiIndex)
      this.logDiscardProbe('flush pending discard', { bipaiIndex, code, pending })
      if (!this.appendDiscard(pending.odr, pending.bipaiIndex, code, pending.isReach)) continue
      changedOdr.add(pending.odr)
      if (pending.animateDiscard && pending.flightOrigin) flightOrigins.set(pending.odr, pending.flightOrigin)
      if (pending.isReach && pending.playReachFeedback) {
        this.playReachBgm()
        playMajakSid(SID_RICSTK, this.soundSkinOptions())
        this.playReachDiscardSound(pending.odr)
        this.showReachTileEffect(pending.odr)
      }
    }
    changedOdr.forEach(odr => {
      this.redrawDiscards(odr)
      this.animateLatestDiscard(odr, flightOrigins.get(odr))
      this.redrawPaifuGraphContent()
    })
  }

  private showBoardEffect(odr: number, action: Act) {
    const prefix = this.boardEffectPrefix(action)
    if (!prefix) return
    const loc = odrToLoc(odr, this.myOdr)
    const point = boardLocalPoint(BOARD_EFFECT_POS[loc])
    let frame = 1
    const sprite = this.clipToBoard(this.add.image(point.x, point.y, `${prefix}_${String(frame).padStart(2, '0')}`)
      .setOrigin(0, 0)
      .setDepth(Z_PANEL + 5))
    this.boardEffectSprites.push(sprite)
    const timer = this.time.addEvent({
      delay: 45,
      repeat: 13,
      callback: () => {
        frame++
        if (frame <= 14) sprite.setTexture(`${prefix}_${String(frame).padStart(2, '0')}`)
      },
    })
    this.time.delayedCall(45 * 15, () => {
      timer.destroy()
      sprite.destroy()
      this.boardEffectSprites = this.boardEffectSprites.filter(item => item !== sprite)
    })
  }

  private boardEffectPrefix(action: Act): string | undefined {
    if (action === Act.Ron) return 'mj_ron_w'
    if (action === Act.Tsu) return 'mj_tumo_w'
    return undefined
  }

  private showReachTileEffect(odr: number) {
    const player = this.players[odr]
    const discardIndex = player.discards.length - 1
    if (discardIndex < 0) return

    const loc = odrToLoc(odr, this.myOdr)
    let nextFlag = 0
    let reachFlag = 2
    for (let idx = 0; idx <= discardIndex; idx++) {
      let flag = player.discards[idx].isReach ? 2 : nextFlag
      if (idx % DISCARD_COLS === 0 && flag === 1) flag = 0
      nextFlag = flag === 2 ? 1 : flag
      if (idx === discardIndex) reachFlag = flag
    }

    const tilePoint = discardPos(loc, discardIndex, reachFlag, this.layoutMode)
    const dir = loc & 1 ? '01' : '00'
    const offset = REACH_TILE_EFFECT_OFFSET[loc]
    const move = REACH_TILE_EFFECT_MOVE[loc]
    let effectX = tilePoint.x + offset.x
    let effectY = tilePoint.y + offset.y

    for (let frame = 0; frame < 5; frame++) {
      const sprite = this.clipToBoard(this.add.image(effectX, effectY, `mj_ef_reachhai${dir}_${String(frame).padStart(2, '0')}`)
        .setOrigin(0, 0)
        .setDepth(tilePoint.y + 1))
      sprite.setVisible(false)
      this.reachTileEffectSprites.push(sprite)
      this.time.delayedCall(25 * frame, () => sprite.setVisible(true))
      this.time.delayedCall(25 * (frame + 1), () => sprite.destroy())
      effectX += move.x
      effectY += move.y
    }

    for (let frame = 0; frame < 7; frame++) {
      const sprite = this.clipToBoard(this.add.image(tilePoint.x, tilePoint.y, `mj_ef_reachhai_lgt${dir}_${String(frame).padStart(2, '0')}`)
        .setOrigin(0, 0)
        .setDepth(tilePoint.y + 1))
      sprite.setVisible(false)
      this.reachTileEffectSprites.push(sprite)
      this.time.delayedCall(125 + 50 * frame, () => sprite.setVisible(true))
      this.time.delayedCall(125 + 50 * (frame + 1), () => sprite.destroy())
    }

    this.time.delayedCall(500, () => {
      this.reachTileEffectSprites = this.reachTileEffectSprites.filter(sprite => sprite.active)
    })
  }

  private playCallActionSound(odr: number, action: Act) {
    if (action === Act.Tap) return

    const player = this.players[odr]
    const options = {
      odr,
      sex: player?.sex,
      customCostume: player?.customCostume,
      customCostumeType: player?.customCostumeType,
      soundSkinId: this.currentBgmSkinId,
    }

    if (action === Act.Ric) playMajakCallVoice('reach', options)
    else if (action === Act.Tsu) playMajakCallVoice('tsumo', options)
    else if (action === Act.Ron) playMajakCallVoice('ron', options)
    else if (action === Act.Pon) playMajakCallVoice('pon', options)
    else if (action === Act.Chi) playMajakCallVoice('chi', options)
    else if (action === Act.Kan || action === Act.Ank || action === Act.Cha) playMajakCallVoice('kan', options)
    else if (action === Act.Hua) playMajakCallVoice('hua', options)
  }

  private pushPaiInfo(ini: boolean, openPos: number, tiles: TileState[]) {
    if (this.paiInfoQueue.length === 0 || ini) {
      this.paiInfoQueue.push({ bIniKyo: ini, openPos, tiles: [] })
    }
    const msg = this.paiInfoQueue[this.paiInfoQueue.length - 1]
    msg.tiles.push(...tiles)
  }

  private clonePaiInfoMsg(msg: PaiInfoMsgState): PaiInfoMsgState {
    return { bIniKyo: msg.bIniKyo, openPos: msg.openPos, tiles: this.cloneTiles(msg.tiles) }
  }

  private applyResyncHandSnapshot(snapshot: ResyncHandSnapshot) {
    if (snapshot.openPos < 0 || snapshot.openPos >= this.players.length) return
    snapshot.tiles.forEach(tile => {
      if (tile.bipaiIndex !== undefined && tile.bipaiIndex >= 0) this.knownPai.set(tile.bipaiIndex, tile.code)
    })
    this.players[snapshot.openPos].hand = this.cloneTiles(snapshot.tiles)
    this.redrawHand(snapshot.openPos)
  }

  private applyPendingResyncHandSnapshot() {
    const snapshot = this.pendingResyncHandSnapshot
    if (!snapshot) return
    this.pendingResyncHandSnapshot = undefined
    this.applyResyncHandSnapshot(snapshot)
  }

  private popPaiInfo(ini: boolean, initialOyaOrder?: number, initialDice?: number[]) {
    if (ini) {
      let dropped = 0
      while (this.paiInfoQueue.length > 0 && !this.paiInfoQueue[0].bIniKyo) {
        this.paiInfoQueue.shift()
        dropped++
      }
      if (dropped > 0 && DEBUG_GAME) console.warn('[GameScene] dropped stale non-init PaiInfo before init kyoku', { dropped })
    }
    const msg = this.paiInfoQueue[0]
    if (!msg) {
      this.logDiscardProbe('pop PaiInfo skipped: empty queue', { ini })
      return
    }
    if (!ini && msg.bIniKyo) {
      this.logDiscardProbe('pop PaiInfo skipped: next packet is init', { ini, nextOpenPos: msg.openPos, nextCount: msg.tiles.length })
      return
    }
    if (ini && !msg.bIniKyo) {
      if (DEBUG_GAME) console.warn('[GameScene] PaiInfo pop expected init packet', { msg })
      this.logDiscardProbe('pop PaiInfo skipped: expected init packet', { ini, nextOpenPos: msg.openPos, nextCount: msg.tiles.length })
      return
    }
    this.paiInfoQueue.shift()
    this.logDiscardProbe('pop PaiInfo apply', { ini, openPos: msg.openPos, isInit: msg.bIniKyo, paiCount: msg.tiles.length })
    this.applyPaiInfo(msg, initialOyaOrder, initialDice)
  }

  private applyPaiInfo(msg: PaiInfoMsgState, initialOyaOrder?: number, initialDice?: number[]) {
    const openPos = msg.openPos
    msg.tiles.forEach(tile => {
      if (tile.bipaiIndex !== undefined && tile.bipaiIndex >= 0) this.knownPai.set(tile.bipaiIndex, tile.code)
    })
    this.logDiscardProbe('apply PaiInfo', {
      openPos,
      isInit: msg.bIniKyo,
      paiCount: msg.tiles.length,
      sample: msg.tiles.slice(0, 16).map(tile => ({ idx: tile.bipaiIndex, code: tile.code })),
      pendingDiscardIndexes: [...this.pendingDiscardsByBipaiIndex.keys()],
    })
    this.flushPendingDiscards()
    if (msg.bIniKyo) this.applyInitialDoraFromPaiInfo(msg.tiles, initialOyaOrder, initialDice)
    if (openPos === VIEWER_OPEN_POS) {
      this.applyViewerPaiInfo(msg, initialOyaOrder, initialDice)
      return
    }
    if (openPos < 0 || openPos >= this.players.length) return
    this.latestActionPaiInfoTiles[openPos] = msg.bIniKyo ? [] : this.cloneTiles(msg.tiles)
    if (msg.bIniKyo) {
      if (!this.isReplay && !this.isViewer && openPos !== this.myOdr) {
        this.myOdr = openPos
        this.emitViewOdrChange()
      }
      const expectedHandCount = openPos === initialOyaOrder ? 14 : 13
      const initialHand = this.selectInitialHandTiles(openPos, msg.tiles, initialOyaOrder, initialDice, expectedHandCount)
      if (initialHand.length === 0) return
      this.players[openPos].hand = this.cloneTiles(initialHand)
      this.paifuGraphInitialHands[openPos] = this.cloneTiles(initialHand)
      this.redrawHand(openPos)
    }
  }

  private applyViewerPaiInfo(msg: PaiInfoMsgState, initialOyaOrder?: number, initialDice?: number[]) {
    if (!msg.bIniKyo) {
      return
    }

    for (let odr = 0; odr < this.players.length; odr++) {
      const initialHand = this.buildInitialHandTiles(odr, msg.tiles, initialOyaOrder, initialDice)
      if (initialHand.length === 0) continue
      this.players[odr].hand = this.cloneTiles(initialHand)
      this.paifuGraphInitialHands[odr] = this.cloneTiles(initialHand.filter(tile => tile.code > 0))
      this.redrawHand(odr)
    }
  }

  private buildInitialHandTiles(openPos: number, tiles: TileState[], oyaOrder?: number, dice?: number[]): TileState[] {
    const handIndices = this.buildInitialHandIndexSet(openPos, oyaOrder, dice)
    if (!handIndices) return []
    const byBipaiIndex = new Map<number, TileState>()
    tiles.forEach(tile => {
      if (tile.bipaiIndex !== undefined) byBipaiIndex.set(tile.bipaiIndex, tile)
    })
    return handIndices.map(bipaiIndex => byBipaiIndex.get(bipaiIndex) ?? { code: 0, bipaiIndex, isSelected: false })
  }

  private selectInitialHandTiles(openPos: number, tiles: TileState[], oyaOrder?: number, dice?: number[], fallbackCount = 13): TileState[] {
    const handIndices = this.buildInitialHandIndexSet(openPos, oyaOrder, dice)
    if (!handIndices) return this.selectInitialHandTilesFromVisiblePai(tiles, this.getDoraIndex(0), fallbackCount)
    const byBipaiIndex = new Map<number, TileState>()
    tiles.forEach(tile => {
      if (tile.bipaiIndex !== undefined) byBipaiIndex.set(tile.bipaiIndex, tile)
    })
    const hand = handIndices
      .map(bipaiIndex => byBipaiIndex.get(bipaiIndex))
      .filter((tile): tile is TileState => Boolean(tile))
    if (hand.length !== handIndices.length && DEBUG_GAME) {
      console.warn('[GameScene] initial PaiInfo did not contain every expected hand tile', { openPos, expected: handIndices, received: tiles })
    }
    return hand.length === handIndices.length
      ? hand
      : this.selectInitialHandTilesFromVisiblePai(tiles, this.getDoraIndex(0), handIndices.length)
  }

  private selectInitialHandTilesFromVisiblePai(tiles: TileState[], excludedBipaiIndex?: number, count = 13): TileState[] {
    return tiles
      .filter(tile => tile.code > 0 && tile.bipaiIndex !== excludedBipaiIndex)
      .slice(0, count)
  }

  private applyInitialDoraFromPaiInfo(tiles: TileState[], oyaOrder?: number, dice?: number[]) {
    const haipaiPos = this.getHaipaiPos(oyaOrder, dice)
    if (haipaiPos === undefined) return
    const doraIndex = (haipaiPos + 130) % 136
    const doraTile = tiles.find(tile => tile.bipaiIndex === doraIndex)
    if (!doraTile || doraTile.code <= 0) return
    this.paifuGraphRound.dora = [doraTile.code]
    this.redrawDeadWall()
    this.redrawPaifuGraphContent()
  }

  private syncLiveDoraIndicators(minCount: number) {
    const count = Math.max(0, Math.min(5, Math.trunc(minCount)))
    if (count === 0) return
    const indicators = [...this.paifuGraphRound.dora]
    let changed = false
    for (let idx = 0; idx < count; idx++) {
      const doraIndex = this.getDoraIndex(idx)
      const code = doraIndex === undefined ? undefined : this.knownPai.get(doraIndex)
      if (!code || code <= 0) continue
      if (indicators[idx] !== code) {
        indicators[idx] = code
        changed = true
      }
    }
    if (!changed) return
    this.paifuGraphRound.dora = indicators
    this.redrawDeadWall()
    this.redrawPaifuGraphContent()
  }

  private buildInitialHandIndexSet(openPos: number, oyaOrder?: number, dice?: number[]): number[] | undefined {
    if (openPos < 0 || openPos >= this.players.length) return undefined
    if (oyaOrder === undefined || oyaOrder < 0 || oyaOrder >= this.players.length) return undefined
    const haipaiPos = this.getHaipaiPos(oyaOrder, dice)
    if (haipaiPos === undefined) return undefined
    const indices: number[] = []
    let seatOdr = oyaOrder
    let offset = 0
    for (let round = 0; round < 3; round++) {
      for (let player = 0; player < this.players.length; player++) {
        for (let count = 0; count < 4; count++) {
          if (seatOdr === openPos) indices.push((haipaiPos + offset) % 136)
          offset++
        }
        seatOdr = (seatOdr + 1) % this.players.length
      }
    }
    for (let player = 0; player < this.players.length; player++) {
      if (seatOdr === openPos) indices.push((haipaiPos + offset) % 136)
      offset++
      seatOdr = (seatOdr + 1) % this.players.length
    }
    if (openPos === oyaOrder) indices.push((haipaiPos + offset) % 136)
    return indices
  }

  private getHaipaiPos(oyaOrder?: number, dice?: number[]): number | undefined {
    if (oyaOrder === undefined || oyaOrder < 0 || oyaOrder >= this.players.length) return undefined
    if (!dice || dice.length < 2 || !Number.isFinite(dice[0]) || !Number.isFinite(dice[1])) return undefined
    const wareme = dice[0] + dice[1] + 2
    return (wareme + this.mod(12 + this.players.length - oyaOrder - wareme + 1, this.players.length) * 17) * 2
  }

  private getDoraIndex(index: number): number | undefined {
    const oyaOrder = this.mod(this.chicha + this.paifuGraphRound.kyokuCnt, this.players.length)
    const haipaiPos = this.getHaipaiPos(oyaOrder, this.paifuGraphRound.dice)
    if (haipaiPos === undefined) return undefined
    return this.mod(haipaiPos + 130 - index * 2, 136)
  }

  private mod(value: number, divisor: number) {
    return ((value % divisor) + divisor) % divisor
  }

  private ensureConcealedOpponentHands() {
    for (let odr = 0; odr < this.players.length; odr++) {
      if (this.isLocalPlayerOdr(odr) || this.players[odr].hand.length > 0) continue
      this.ensureConcealedHandCount(odr, 13)
    }
  }

  private ensureConcealedHandCount(odr: number, count: number) {
    if (odr < 0 || odr >= this.players.length || this.isLocalPlayerOdr(odr)) return
    const hand = this.players[odr].hand
    while (hand.length < count) hand.push({ code: 0, isSelected: false })
    if (hand.length > count) hand.splice(count)
    this.redrawHand(odr)
  }

  private ensureTurnDrawTile(odr: number) {
    if (odr < 0 || odr >= this.players.length || this.isLocalPlayerOdr(odr)) return
    const hand = this.players[odr].hand
    if (hand.length > 0 && hand.length % 3 === 1) {
      hand.push(this.latestUnheldPaiInfoTile(odr) ?? { code: 0, isSelected: false })
      this.redrawHand(odr)
      if (!this.shouldSuppressLivePlayback()) playMajakSid(SID_EXPOSE, this.soundSkinOptions())
    }
  }

  private latestUnheldPaiInfoTile(odr: number): TileState | undefined {
    const held = new Set<number>()
    this.players[odr].hand.forEach(tile => {
      if (tile.bipaiIndex !== undefined) held.add(tile.bipaiIndex)
    })
    this.players[odr].discards.forEach(tile => {
      if (tile.bipaiIndex !== undefined) held.add(tile.bipaiIndex)
    })
    return [...(this.latestActionPaiInfoTiles[odr] ?? [])]
      .reverse()
      .find(tile => tile.code > 0 && tile.bipaiIndex !== undefined && !held.has(tile.bipaiIndex))
  }

  private readClaimedOdr(data: Record<string, unknown>, odr: number, action: Act): number {
    const explicit = Number(data.preOdr ?? data.fromOdr ?? data.discardOdr ?? data.claimedOdr ?? -1)
    if (Number.isInteger(explicit) && explicit >= 0 && explicit < this.players.length) return explicit
    if (this.lastDiscardOdr !== null) return this.lastDiscardOdr
    if (action === Act.Chi) return (odr + 3) % 4
    return (odr + 3) % 4
  }

  private applyMeldAction(odr: number, action: Act, indices: number[], claimedOdr: number, suppressLivePlayback = false) {
    if (action === Act.Hua) {
      const tile = this.takeHandTilesByBipai(odr, indices)[0]
      if (tile) {
        this.players[odr].flowers.push(tile)
        this.addPaifuGraphCall(odr, tile.code, action)
      }
      this.redrawHand(odr)
      this.redrawMelds(odr)
      if (!suppressLivePlayback) playMajakSid(SID_FURO, this.soundSkinOptions())
      return
    }

    const handTiles = this.takeHandTilesByBipai(odr, indices)
    if (action === Act.Cha) {
      this.applyAddedKanAction(odr, handTiles[0])
      this.redrawHand(odr)
      this.redrawMelds(odr)
      if (!suppressLivePlayback) playMajakSid(SID_FURO, this.soundSkinOptions())
      return
    }

    const claimed = action === Act.Ank
      ? undefined
      : this.players[claimedOdr]?.discards.pop()

    if (claimed) {
      if (claimed.isReach) this.players[claimedOdr].reachDiscardCarry = true
      this.lastDiscardOdr = null
      this.redrawDiscards(claimedOdr)
      this.addPaifuGraphCall(odr, claimed.code, action)
    } else if (action === Act.Ank && handTiles[0]) {
      this.addPaifuGraphCall(odr, handTiles[0].code, action)
    }

    if (action === Act.Ank) {
      const tiles = handTiles.slice(0, 4)
      if (tiles.length === 4) {
        this.players[odr].melds.push({
          action,
          tiles: [
            { code: tiles[1].code, flag: 0, isDown: true },
            { code: tiles[3].code, flag: 0 },
            { code: tiles[2].code, flag: 0 },
            { code: tiles[0].code, flag: 0, isDown: true },
          ],
        })
      }
    } else if (action === Act.Chi && claimed) {
      const tiles = [...handTiles, { code: claimed.code, isSelected: false }]
        .sort((a, b) => paiToFrame(a.code) - paiToFrame(b.code))
      this.players[odr].melds.push({
        action,
        tiles: tiles.map(tile => ({ code: tile.code, flag: tile.code === claimed.code ? 2 : 0 })),
      })
    } else if ((action === Act.Pon || action === Act.Kan) && claimed) {
      const needed = action === Act.Pon ? 3 : 4
      const relative = (4 + claimedOdr - odr) % 4
      this.players[odr].melds.push({
        action,
        tiles: this.arrangeCalledMeldTiles(claimed.code, handTiles, needed, relative),
      })
    }

    this.redrawHand(odr)
    this.redrawMelds(odr)
    if (!suppressLivePlayback && [Act.Chi, Act.Pon, Act.Kan, Act.Ank].includes(action)) playMajakSid(SID_FURO, this.soundSkinOptions())
  }

  private applyAddedKanAction(odr: number, added?: TileState) {
    if (!added) return
    const meld = [...this.players[odr].melds]
      .reverse()
      .find(item => item.action === Act.Pon && item.tiles.some(tile => tile.code === added.code))
    if (!meld) {
      console.warn('[GameScene] added kan ignored: matching pon meld not found', { odr, added })
      return
    }
    const calledIndex = meld.tiles.findIndex(tile => tile.flag === 2)
    const insertIndex = calledIndex >= 0 ? calledIndex : Math.min(2, meld.tiles.length)
    meld.action = Act.Cha
    meld.tiles.splice(insertIndex, 0, { code: added.code, flag: 0 })
    if (meld.tiles.length > 4) meld.tiles.splice(4)
    this.addPaifuGraphCall(odr, added.code, Act.Cha)
  }

  private arrangeCalledMeldTiles(claimedCode: number, handTiles: TileState[], needed: number, relative: number): MeldTileState[] {
    const calledCol = Phaser.Math.Clamp(relative - 1, 0, needed - 1)
    const sorted = [...handTiles].sort((a, b) => paiToFrame(b.code) - paiToFrame(a.code))
    const tiles: MeldTileState[] = []
    for (let col = 0; col < needed; col++) {
      if (col === calledCol) {
        tiles.push({ code: claimedCode, flag: 2 })
      } else {
        const tile = sorted.shift()
        tiles.push({ code: tile && tile.code > 0 ? tile.code : claimedCode, flag: col > calledCol ? 1 : 0 })
      }
    }
    return tiles
  }

  private takeHandTilesByBipai(odr: number, indices: number[]): TileState[] {
    const hand = this.players[odr].hand
    const taken: TileState[] = []
    for (const bipaiIndex of indices) {
      const handIdx = hand.findIndex(tile => tile.bipaiIndex === bipaiIndex)
      const knownCode = this.knownPai.get(bipaiIndex)
      if (handIdx >= 0) {
        const tile = hand.splice(handIdx, 1)[0]
        taken.push(knownCode && knownCode > 0 ? { ...tile, code: knownCode } : tile)
      } else {
        if (!this.isLocalPlayerOdr(odr) && hand.length > 0) {
          const hidden = hand.pop()
          if (hidden) taken.push({ ...hidden, code: knownCode ?? hidden.code, bipaiIndex })
        } else if (knownCode) {
          taken.push({ code: knownCode, bipaiIndex, isSelected: false })
        }
      }
    }
    this.players[odr].hand.sort((a, b) => paiToFrame(a.code) - paiToFrame(b.code))
    return taken
  }

  private playRoundStartSounds(data: Record<string, unknown>) {
    const soundSkinId = this.resolveBgmSkinId(data) ?? this.currentBgmSkinId
    playMajakSid(SID_SIPAI, this.soundSkinOptions(soundSkinId))
    const dice = Array.isArray(data.dice) ? data.dice : []
    if (dice.length > 0) this.time.delayedCall(2000, () => playMajakSid(SID_DICE, this.soundSkinOptions(soundSkinId)))
  }

  private playRoundBgm(data: Record<string, unknown>, kyokuCnt: number) {
    this.currentBgmSkinId = this.resolveBgmSkinId(data)
    this.currentRoundUsesTengokuBgm = this.isTengokuBgm(data, this.currentBgmSkinId)
    this.currentRoundIsCarnival = Boolean(data.isCarnivalChannel ?? data.carnivalChannel)
    const mid = this.currentRoundIsCarnival
      ? MID_FESRIC
      : this.currentRoundUsesTengokuBgm
        ? this.selectTengokuRoundBgm(kyokuCnt)
        : this.selectNormalRoundBgm(kyokuCnt)
    playMajakBgm(mid, this.currentBgmSkinId ? { skinId: this.currentBgmSkinId } : {})
  }

  private playReachBgm() {
    const mid = this.currentRoundIsCarnival
      ? MID_FESRIC
      : this.currentRoundUsesTengokuBgm
        ? this.players.filter(player => player.isReach).length > 1 ? MID_TEN_REACH2 : MID_TEN_REACH1
        : MID_RICHI
    playMajakBgm(mid, this.currentBgmSkinId ? { skinId: this.currentBgmSkinId } : {})
  }

  private selectTengokuRoundBgm(kyokuCnt: number): number {
    const isTonpu = this.readOptionDigit(this.paifuGraphRound.roomOption, 0, 1) === 0
    if ((isTonpu && kyokuCnt === 3) || kyokuCnt === 7) return MID_TEN_ALLLAST
    return kyokuCnt < 4 ? MID_TEN_TONBA : MID_TEN_NANBA
  }

  private selectNormalRoundBgm(kyokuCnt: number): number {
    if (kyokuCnt < 4 || this.isReplay || this.myOdr < 0 || this.myOdr >= this.players.length) return MID_NORMAL
    const scores = this.players.map(player => player.score)
    if (scores.some(score => !Number.isFinite(score))) return MID_NORMAL
    const rank = this.scoreRank(scores, this.myOdr)
    if (rank === 0) {
      const adjusted = [...scores]
      adjusted[this.myOdr] -= 8000
      if (this.scoreRank(adjusted, this.myOdr) === 0) return MID_GOOD
    } else if (rank === 3) {
      const adjusted = [...scores]
      adjusted[this.myOdr] += 8000
      if (this.scoreRank(adjusted, this.myOdr) === 3) return MID_BAD
    }
    return MID_NORMAL
  }

  private scoreRank(scores: number[], odr: number): number {
    return scores
      .map((score, index) => ({ score, index }))
      .sort((a, b) => b.score - a.score || a.index - b.index)
      .findIndex(item => item.index === odr)
  }

  private resolveBgmSkinId(data: Record<string, unknown>): number | undefined {
    const direct = asFiniteNumber(data.bgmSkinId ?? data.customBgmSkinId ?? data.soundSkinId)
    if (direct && direct > 0) return direct
    const board = asFiniteNumber(data.customBoard ?? data.mjkk134e ?? data.boardId) ?? this.customBgId
    if (board === CUSTOM_BGM_ID_EXTRA || board === CUSTOM_BGM_ID_TENGOKU) return board
    const bgType = asFiniteNumber(data.customBoardType ?? data.skinTypeBG ?? data.bgType)
    if (bgType === CUSTOM_ITEM_TYPE_BG_EXTRA) return CUSTOM_BGM_ID_EXTRA
    if (bgType === CUSTOM_ITEM_TYPE_BG_TENGOKU) return CUSTOM_BGM_ID_TENGOKU
    if (board === 100001) return CUSTOM_BGM_ID_EXTRA
    if (board === 100002) return CUSTOM_BGM_ID_TENGOKU
    return undefined
  }

  private isTengokuBgm(data: Record<string, unknown>, bgmSkinId: number | undefined): boolean {
    if (bgmSkinId === CUSTOM_BGM_ID_TENGOKU) return true
    const board = asFiniteNumber(data.customBoard ?? data.mjkk134e ?? data.boardId) ?? this.customBgId
    const bgType = asFiniteNumber(data.customBoardType ?? data.skinTypeBG ?? data.bgType)
    return board === CUSTOM_BGM_ID_TENGOKU || bgType === CUSTOM_ITEM_TYPE_BG_TENGOKU
  }

  private playReachDiscardSound(odr: number) {
    playMajakSfx('mjkReach_pai', this.soundSkinOptions())
    const discardCount = this.players[odr]?.discards.length ?? 0
    if (discardCount % DISCARD_COLS !== 1) playMajakSid(SID_THROW_RICH, this.soundSkinOptions())
  }

  private isDoraTile(code: number): boolean {
    return this.paifuGraphRound.dora.some(dora => this.nextDoraCode(dora) === code)
  }

  private nextDoraCode(code: number): number {
    const kind = (code >> 4) & 0xF
    const number = code & 0xF
    if (kind < 3) return (kind << 4) | (number >= 9 ? 1 : number + 1)
    if (number >= 1 && number <= 4) return (kind << 4) | (number >= 4 ? 1 : number + 1)
    if (number >= 5 && number <= 7) return (kind << 4) | (number >= 7 ? 5 : number + 1)
    return code
  }

  private formatKyoku(kyokuCnt: number): string {
    const wind = kyokuCnt < 4 ? '東' : '南'
    return `${wind}${(kyokuCnt % 4) + 1}局`
  }

  private resetRoundState(preserveHands = false) {
    this.clearAllDiscardFlights()
    this.selectedIdx = -1
    this.lastDiscardOdr = null
    this.appliedActionKeys.clear()
    this.pendingDiscardsByBipaiIndex.clear()
    this.paifuGraphDraws = [[], [], [], []]
    this.paifuGraphDiscards = [[], [], [], []]
    this.latestActionPaiInfoTiles = [[], [], [], []]
    this.paifuGraphRound = {
      ...this.paifuGraphRound,
      dora: [],
      uraDora: [],
    }
    this.clearDeadWall()
    if (!preserveHands) this.knownPai.clear()
    this.clearActionButtons()
    this.selectedCursor?.destroy()
    this.selectedCursor = undefined
    this.clearTenpaiMarkers()
    this.clearWaitTileGuide()
    for (let odr = 0; odr < this.players.length; odr++) {
      if (!preserveHands) this.players[odr].hand = []
      this.paifuGraphInitialHands[odr] = preserveHands ? this.cloneTiles(this.players[odr].hand) : []
      this.players[odr].discards = []
      this.players[odr].melds = []
      this.players[odr].flowers = []
      this.players[odr].isReach = false
      this.players[odr].reachDiscardCarry = false
      this.players[odr].hora = undefined
      this.redrawHand(odr)
      this.redrawDiscards(odr)
      this.redrawMelds(odr)
    }
  }

  private redrawAllReplayPai() {
    if (!this.isReplay) return
    this.redrawAllPerspectivePai()
  }

  private redrawAllPerspectivePai() {
    this.redrawDeadWall()
    for (let odr = 0; odr < this.players.length; odr++) {
      this.redrawHand(odr)
      this.redrawDiscards(odr)
      this.redrawMelds(odr)
    }
  }
}

const enum Act {
  Inv = 0,
  Pas = 1,
  Chi = 2,
  Pon = 3,
  Kan = 4,
  Ron = 5,
  Tap = 6,
  Ank = 7,
  Cha = 8,
  Ric = 9,
  Tao = 10,
  Tsu = 11,
  Hua = 12,
}

