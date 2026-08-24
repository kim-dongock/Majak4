/**
 * UIScene — インゲーム HUD オーバーレイ
 * CMJTblUser + CMJTblDraw 相当 (AP-09 §2-4, §2-6)
 *
 * GameScene と並走して常に最前面に描画される。
 *
 * ── 表示要素 ───────────────────────────────────────────────────────────────
 * 各プレイヤー (odr=0:自 odr=1:下家 odr=2:対面 odr=3:上家):
 *   点数/名前     : MJTblDraw2.cpp mempos[].txt/name
 *   ターン指示   : プレイヤー HUD プレートの上部ストリップ
 *   リーチ表示   : MJTblDraw2.cpp ricpos[] のリーチ棒
 *
 * 共通:
 *   局・風       : "東1局" 等
 *   残り牌枚数
 *   タイマー     : CMJObjTimBar (493×6) 相当
 *
 * ── 座標系 ────────────────────────────────────────────────────────────────
 * ゲームボード: x=5, y=31, w=789, h=704
 * 各プレイヤー UI 位置:
 *   legacy/client/HgMajak2/MJTblDraw2.cpp CMJTblDraw::mempos
 */
import Phaser from 'phaser'
import { calculateTimeBankSegments, GAME_AUTO_PASS_HOLD_EVENT } from '../game/autoControl'
import { DESKTOP_REACH_POSITIONS, getIngameLayout, isMobileIngameLayout, MOBILE_REACH_POSITIONS, type IngameLayoutMode } from '../game/ingameLayout'
import {
  LEGACY_COSTUME_FRAME_COUNTS,
  LEGACY_REACH_FRAME_DELAYS,
  numberedLegacyKeys,
  type LegacyCostumeAction,
  type LegacyCostumeId,
} from '../game/legacyAnimations'
import MobileAvatarLayer from '../game/MobileAvatarLayer'
import { mobileCenterHudOffset, mobileEffectPointFromAnchor, mobileVisibleWorldBounds, mobileVisibleWorldLayoutKey, responsiveDesktopCenterOffset, responsiveDesktopSeatOffset, responsiveDesktopVisibleWorldBounds } from '../game/mobileIngameViewport'
import { isTengokuBoardSkin } from '../utils/legacySkinPalette'
import { playMajakSfx, playMajakSid, SID_RICSTK } from '../utils/majakSound'
import { getUiFontFamily, getUiFontSize, getUiFontSizePx } from '../utils/typography'

interface HudPoint { x: number; y: number }
interface ActionPromptTimerData {
  timeLimit?: number
  baseTimeMs?: number
  keepTimeMs?: number
  timeBankMs?: number
  timeBankEnabled?: boolean
  maxTimeMs?: number
  viewOdr?: number
}
interface OdrBoxPos {
  avt: HudPoint
  hst: HudPoint
  txt: HudPoint
  name: HudPoint
  ttl: HudPoint
  trk: HudPoint
}

interface PlayerHudState {
  pix?: string
  name: string
  level?: string
  score: number
  rating?: number
  avatarUrl?: string
  fallbackAvatarUrl?: string
  majakTitle?: number
  trickTitle?: number
  richiEffect?: number
  customCostume?: number
  customCostumeType?: number
  isHost?: boolean
  isProxy?: boolean
}

interface LegacyNumber {
  sprites: Phaser.GameObjects.Image[]
  x: number
  y: number
  frameWidth: number
  gap: number
}

interface CallAvatarHandle {
  sprite?: Phaser.GameObjects.Image
  destroy: () => void
}

interface CostumeAnimationState {
  action: LegacyCostumeAction
  returnAction: 'default' | 'reach'
  frame: number
  oneShot: boolean
}

const TURN_MARK_EVENT = 'majak:turn-mark'
const PAIFU_GRAPH_EVENT = 'majak:paifu-graph'
const UI_FLOW_TRACE_PREFIX = '[UIFlow]'
const DEBUG_UI_FLOW = import.meta.env.VITE_DEBUG_GAME === '1'
const IMG = '/assets/images/game'
const BOARD_X = 5
const BOARD_Y = 31
const CUSTOM_DEFAULT_ID_COSTUME = 100011
const AVAILABLE_COSTUME_IDS = new Set([9, 10, 11])
const HUD_TEXT_RESOLUTION = typeof window === 'undefined'
  ? 1
  : Math.min(2, Math.max(1, window.devicePixelRatio || 1))
const MOBILE_HUD_TEXT_GAP = 2
const MOBILE_HUD_INFO_TOP_OFFSET = 10
const MOBILE_HUD_INFO_WIDTH = 46
const MOBILE_HUD_INFO_ROW_HEIGHT = 17
const MOBILE_HUD_NAME_WIDTH = 49
const MOBILE_HUD_NAME_GAP = 3
const MOBILE_HUD_PANEL_PADDING_X = 2
const MOBILE_HUD_PANEL_PADDING_Y = 6
const MOBILE_HUD_COMPACT_AVATAR_PADDING = 3
const MOBILE_HUD_ICON_WIDTH = 44
const MOBILE_HUD_ICON_HEIGHT = 66
const HUD_NAME_MIN_FONT_SIZE = 8
const DESKTOP_HUD_INFO_Y_SHIFT = -24

function cssPx(value: string): number {
  const match = value.match(/\d+/)
  return match ? Number(match[0]) : 12
}

function measureHudTextWidth(text: string, fontSize: number): number {
  if (!text) return 0
  const scaledFontSize = getUiFontSizePx(fontSize)
  if (typeof document === 'undefined') return text.length * scaledFontSize
  const canvas = document.createElement('canvas')
  const context = canvas.getContext('2d')
  if (!context) return text.length * scaledFontSize
  context.font = `bold ${scaledFontSize}px ${getUiFontFamily()}`
  return context.measureText(text).width
}

function boardLocalPoint(point: HudPoint): HudPoint {
  const offset = responsiveDesktopCenterOffset(UI_LAYOUT_MODE)
  return { x: BOARD_X + point.x + offset.x, y: BOARD_Y + point.y + offset.y }
}

function playerHudPoint(point: HudPoint, loc: number): HudPoint {
  const base = boardLocalPoint(point)
  if (UI_LAYOUT_MODE === 'responsiveDesktop') {
    const bounds = responsiveDesktopVisibleWorldBounds()
    if (bounds) {
      const board = getIngameLayout(UI_LAYOUT_MODE).board
      const avatarBoxHeight = DESKTOP_PLAYER_AVATAR_SIZE.height
      const usableHeight = Math.max(0, bounds.bottom - bounds.top - avatarBoxHeight)
      const centerOffset = responsiveDesktopCenterOffset(UI_LAYOUT_MODE)
      const seatOffset = responsiveDesktopSeatOffset(UI_LAYOUT_MODE, loc)
      return {
        x: base.x + seatOffset.x - centerOffset.x,
        y: bounds.top + avatarBoxHeight / 2 + point.y / board.height * usableHeight,
      }
    }
  }

  return base
}

function seatEffectPoint(point: HudPoint, loc: number): HudPoint {
  const base = boardLocalPoint(point)
  const centerOffset = responsiveDesktopCenterOffset(UI_LAYOUT_MODE)
  const seatOffset = responsiveDesktopSeatOffset(UI_LAYOUT_MODE, loc)
  return {
    x: base.x + seatOffset.x - centerOffset.x,
    y: base.y + seatOffset.y - centerOffset.y,
  }
}

function skinTextureCandidate(key: string): string {
  return `${key}_skin`
}

interface HudMetrics {
  avatar: { width: number; height: number }
  nameWidth: number
  nameHeight: number
  nameFontSize: string
  infoFontSize: string
  turnOffsetKnown: number
  turnOffsetUnknown: number
}

const DESKTOP_HUD_METRICS: HudMetrics = {
  avatar: { width: 45, height: 102 },
  nameWidth: 98,
  nameHeight: 14,
  nameFontSize: '12px',
  infoFontSize: '12px',
  turnOffsetKnown: 50,
  turnOffsetUnknown: 33,
}

const DESKTOP_PLAYER_AVATAR_SIZE = { width: 60, height: 112 } as const
const RESPONSIVE_DESKTOP_TITLE_OFFSET_Y = -12
const RESPONSIVE_DESKTOP_PLAYER_INFO_OFFSET_Y = 12
const MOBILE_HUD_METRICS: HudMetrics = {
  avatar: { width: MOBILE_HUD_ICON_WIDTH, height: MOBILE_HUD_ICON_HEIGHT },
  nameWidth: 132,
  nameHeight: 15,
  nameFontSize: '11px',
  infoFontSize: '13px',
  turnOffsetKnown: 6,
  turnOffsetUnknown: 6,
}

let HUD_METRICS: HudMetrics = DESKTOP_HUD_METRICS

function avatarTextBounds(loc: number) {
  const pos = odrBoxPos(loc)
  if (isMobileIngameLayout(UI_LAYOUT_MODE)) {
    const txt = boardLocalPoint(pos.txt)
    return { left: txt.x, width: MOBILE_HUD_INFO_WIDTH }
  }
  const avatar = playerHudPoint(pos.avt, loc)
  const name = playerHudPoint(pos.name, loc)
  const seatPanelLeft = name.x
  const seatPanelRight = name.x + HUD_METRICS.nameWidth
  const avatarRight = avatar.x + HUD_METRICS.avatar.width
  const avatarIsLeft = avatar.x < name.x + HUD_METRICS.nameWidth / 2
  return {
    left: avatarIsLeft ? avatarRight : seatPanelLeft,
    width: avatarIsLeft ? seatPanelRight - avatarRight : avatar.x - seatPanelLeft,
  }
}

/* MJTblDraw2.cpp CMJTblDraw::mempos */
const DESKTOP_ODR_BOX_POS: OdrBoxPos[] = [
  { avt: { x:   2, y: 582 }, hst: { x:  30, y: 665 }, txt: { x:  49, y: 625 }, name: { x:   2, y: 687 }, ttl: { x:  49, y: 583 }, trk: { x:   1, y: 581 } },
  { avt: { x: 742, y: 582 }, hst: { x: 770, y: 665 }, txt: { x: 689, y: 625 }, name: { x: 689, y: 687 }, ttl: { x: 690, y: 583 }, trk: { x: 688, y: 581 } },
  { avt: { x: 742, y:   2 }, hst: { x: 770, y:  85 }, txt: { x: 689, y:  45 }, name: { x: 689, y: 107 }, ttl: { x: 690, y:   3 }, trk: { x: 688, y:   1 } },
  { avt: { x:   2, y:   2 }, hst: { x:  30, y:  85 }, txt: { x:  49, y:  45 }, name: { x:   2, y: 107 }, ttl: { x:  49, y:   3 }, trk: { x:   1, y:   1 } },
]

const MOBILE_ODR_BOX_POS: OdrBoxPos[] = [
  { avt: { x:  18, y: 398 }, hst: { x:  42, y: 456 }, txt: { x:  54, y: 414 }, name: { x:  54, y: 468 }, ttl: { x:  54, y: 398 }, trk: { x:  54, y: 396 } },
  { avt: { x: 736, y: 398 }, hst: { x: 760, y: 456 }, txt: { x: 666, y: 414 }, name: { x: 666, y: 468 }, ttl: { x: 666, y: 398 }, trk: { x: 666, y: 396 } },
  { avt: { x: 736, y: 188 }, hst: { x: 760, y: 246 }, txt: { x: 666, y: 204 }, name: { x: 666, y: 258 }, ttl: { x: 666, y: 188 }, trk: { x: 666, y: 186 } },
  { avt: { x:  18, y: 188 }, hst: { x:  42, y: 246 }, txt: { x:  54, y: 204 }, name: { x:  54, y: 258 }, ttl: { x:  54, y: 188 }, trk: { x:  54, y: 186 } },
]

let ODR_BOX_POS = DESKTOP_ODR_BOX_POS
let UI_LAYOUT_MODE: IngameLayoutMode = 'desktop'

function applyUiLayout(mode: IngameLayoutMode) {
  UI_LAYOUT_MODE = mode
  ODR_BOX_POS = isMobileIngameLayout(mode) ? MOBILE_ODR_BOX_POS : DESKTOP_ODR_BOX_POS
  const nextHudMetrics: HudMetrics = mode === 'mobileLandscape' ? MOBILE_HUD_METRICS : DESKTOP_HUD_METRICS
  HUD_METRICS = nextHudMetrics
}

function mobileScreenCornerBoxPos(loc: number): OdrBoxPos {
  const fallback = MOBILE_ODR_BOX_POS[loc] ?? MOBILE_ODR_BOX_POS[0]
  const bounds = mobileVisibleWorldBounds()
  if (!bounds) return fallback

  const insetX = 14
  const insetTop = 8
  const insetBottom = 14
  const isRight = loc === 1 || loc === 2
  const isBottom = loc === 0 || loc === 1
  const avatarX = (isRight ? bounds.right - HUD_METRICS.avatar.width - insetX : bounds.left + insetX) - BOARD_X
  const bottomReserve = 0
  const avatarY = (isBottom ? bounds.bottom - HUD_METRICS.avatar.height - insetBottom - bottomReserve : bounds.top + insetTop) - BOARD_Y
  const textGap = MOBILE_HUD_TEXT_GAP
  const textX = isRight ? avatarX - MOBILE_HUD_INFO_WIDTH - textGap : avatarX + HUD_METRICS.avatar.width + textGap
  const nameX = isRight ? avatarX + HUD_METRICS.avatar.width - MOBILE_HUD_NAME_WIDTH : avatarX
  const nameY = isBottom ? avatarY - HUD_METRICS.nameHeight - MOBILE_HUD_NAME_GAP : avatarY + HUD_METRICS.avatar.height + MOBILE_HUD_NAME_GAP
  const textY = avatarY + MOBILE_HUD_INFO_TOP_OFFSET

  return {
    avt: { x: avatarX, y: avatarY },
    hst: { x: avatarX + 24, y: avatarY + 58 },
    txt: { x: textX, y: textY },
    name: { x: nameX, y: nameY },
    ttl: { x: textX, y: avatarY },
    trk: { x: textX, y: avatarY - 2 },
  }
}

function odrBoxPos(loc: number): OdrBoxPos {
  if (ODR_BOX_POS === MOBILE_ODR_BOX_POS) return mobileScreenCornerBoxPos(loc)
  const position = ODR_BOX_POS[loc]
  return position
}

function centerHudOffset(): HudPoint {
  return isMobileIngameLayout(UI_LAYOUT_MODE) ? mobileCenterHudOffset(UI_LAYOUT_MODE) : { x: 0, y: 0 }
}

function centerHudPoint(point: HudPoint): HudPoint {
  const base = boardLocalPoint(point)
  const offset = centerHudOffset()
  return { x: base.x + offset.x, y: base.y + offset.y }
}

const MOBILE_CENTER_INFO_CONTENT_OFFSET = { x: 0, y: -29 } as const

function centerInfoContentPoint(point: HudPoint): HudPoint {
  const adjusted = isMobileIngameLayout(UI_LAYOUT_MODE)
    ? { x: point.x + MOBILE_CENTER_INFO_CONTENT_OFFSET.x, y: point.y + MOBILE_CENTER_INFO_CONTENT_OFFSET.y }
    : point
  return centerHudPoint(adjusted)
}

/* 局情報エリア (中央上部) */
const X_CHANFON = 326
const Y_CHANFON = 318
const X_KYOKNUM = 358
const Y_KYOKNUM = 318

/* MajakDef.h / MJTblUser4.cpp / CMJObjTimBar */
const X_TIMBAR = 179
const Y_TIMBAR = 651
const W_TIMBAR = 493
const H_TIMBAR = 6
const MOBILE_TIMBAR_BOTTOM_INSET = 10
const MOBILE_TIMBAR_X_SHIFT = 0
const X_LEFTCNT = 438
const Y_LEFTCNT = 370
const X_RIBOCNT = 377
const Y_RIBOCNT = 348
const X_RENCCNT = 452
const Y_RENCCNT = 348
const X_DICELFT = 426
const Y_DICELFT = 321
const X_DICERGT = 445
const Y_DICERGT = 321
const DICE_ROLL_START_DELAY_MS = 2000
const DICE_ROLL_FRAME_MS = 20
const DICE_ROLL_FRAME_COUNT = 35
const WAR_POS = [
  { x: 353, y: 391, key: 'mj_wareme00' },
  { x: 470, y: 327, key: 'mj_wareme01' },
  { x: 353, y: 298, key: 'mj_wareme02' },
  { x: 292, y: 327, key: 'mj_wareme03' },
] as const
const MEN_FON_POS = [
  { x: 290, y: 381 },
  { x: 467, y: 381 },
  { x: 467, y: 293 },
  { x: 290, y: 293 },
] as const
const CHICHA_POS = [
  { x: 208, y: 522 },
  { x: 679, y: 441 },
  { x: 540, y:  76 },
  { x:  57, y: 201 },
] as const
const MOBILE_CHICHA_OFFSET = [
  { x: 106, y: -40 },
  { x: -129, y: -40 },
  { x: -129, y:  75 },
  { x: 106, y:  75 },
] as const
const CALL_POS = [
  { x: 274, y: 390 },
  { x: 491, y: 238 },
  { x: 274, y:  41 },
  { x:  31, y: 238 },
] as const
const CALL_BALLOON_SIZE = [
  { w: 236, h: 202 },
  { w: 267, h: 167 },
  { w: 236, h: 202 },
  { w: 267, h: 167 },
] as const
const MOBILE_CALL_CENTER_INFO = getIngameLayout('mobileLandscape').centerInfo
const MOBILE_CALL_SIDE_OVERLAP = 36
const MOBILE_CALL_TOP_GAP = 3
const MOBILE_CALL_BOTTOM_OVERLAP = 17
const CALL_AVATAR_SIZE = { w: 66, h: 99 } as const
const CALL_AVATAR_POS = [
  { x: 32, y: 27 },
  { x: 32, y: 27 },
  { x: 32, y: 62 },
  { x: 63, y: 27 },
] as const
const Z_CALL_BALLOON = 5000
const Z_CALL_AVATAR = Z_CALL_BALLOON + 1
const Z_REACH_STICK = 900

export default class UIScene extends Phaser.Scene {
  /* テキストオブジェクト */
  private nameTexts:  Phaser.GameObjects.Text[] = []
  private levelTexts: Phaser.GameObjects.Text[] = []
  private scoreTexts: Phaser.GameObjects.Text[] = []
  private rankTexts: Phaser.GameObjects.Text[] = []
  private diffTexts: Phaser.GameObjects.Text[] = []
  private mobileHudPanels: Phaser.GameObjects.Rectangle[] = []
  private desktopHudPanels: Phaser.GameObjects.Image[] = []
  private desktopTurnStrips: Phaser.GameObjects.Image[] = []
  private desktopHudBounds: Array<{ left: number; top: number; width: number; height: number } | undefined> = []
  private avatarBounds: Array<{ x: number; y: number; width: number; height: number } | undefined> = []
  private avatarSprites: Phaser.GameObjects.Image[] = []
  private majakTitleSprites: Phaser.GameObjects.Image[] = []
  private trickTitleSprites: Phaser.GameObjects.Image[] = []
  private hostMark?: Phaser.GameObjects.Image
  private menFonSprites: Phaser.GameObjects.Image[] = []
  private chichaSprite?: Phaser.GameObjects.Image
  private reachSprites: Phaser.GameObjects.Image[] = []
  private reachAnimationSprites: Phaser.GameObjects.Image[] = []
  private chaFonSprite!: Phaser.GameObjects.Image
  private kyokuNumSprite!: Phaser.GameObjects.Image
  private leftNumber!: LegacyNumber
  private riboNumber!: LegacyNumber
  private renchanNumber!: LegacyNumber
  private diceSprites: Phaser.GameObjects.Image[] = []
  private waremeSprite?: Phaser.GameObjects.Image
  private timerBack!: Phaser.GameObjects.Rectangle
  private timerBar!: Phaser.GameObjects.Rectangle
  private timerTurnBar!: Phaser.GameObjects.Rectangle
  private timerKeepBar!: Phaser.GameObjects.Rectangle
  private diceRollDelay?: Phaser.Time.TimerEvent
  private diceRollTimer?: Phaser.Time.TimerEvent
  private callSprites: Phaser.GameObjects.Image[] = []
  private skillEffectSprites: Phaser.GameObjects.Image[] = []
  private mobileAvatarLayer?: MobileAvatarLayer
  private costumeAnimationStates: Array<CostumeAnimationState | undefined> = [undefined, undefined, undefined, undefined]

  /* タイマー */
  private timerMaxMs = 0
  private timerEndAt = 0
  private timerBaseTimeMs = 0
  private timerKeepTimeMs = 0
  private timerBankMs = 0
  private timerBankEnabled = false
  private timerEvent?: Phaser.Time.TimerEvent
  private flowTraceSerial = 0
  private players: PlayerHudState[] = []
  private myOdr = 0
  private layoutMode: IngameLayoutMode = 'desktop'
  private isViewer = false
  private customBgId = 0
  private customBoardType = 0
  private chicha = 0
  private oyaOrder = 0
  private kyokuCnt = 0
  private activeTurnOdr: number | null = null
  private waremeOdr: number | null = null
  private lastMobileHudLayoutKey = ''
  private readonly reachedOdr = new Set<number>()
  private replayGraphVisible = false
  private graphHiddenHudObjects: Phaser.GameObjects.GameObject[] = []

  constructor() {
    super({ key: 'UIScene' })
  }

  init(data: { myOdr?: number; layoutMode?: IngameLayoutMode; isViewer?: boolean; customBgId?: number; customBoardType?: number; customHaiId?: number }) {
    this.myOdr = data.myOdr ?? 0
    this.layoutMode = data.layoutMode ?? 'desktop'
    this.isViewer = Boolean(data.isViewer)
    this.customBgId = Number(data.customBgId ?? 0)
    this.customBoardType = Number(data.customBoardType ?? 0)
    applyUiLayout(this.layoutMode)
  }

  create() {
    if (this.layoutMode === 'mobileLandscape' && this.game.canvas.parentElement instanceof HTMLElement) {
      this.mobileAvatarLayer = new MobileAvatarLayer(
        this.game.canvas.parentElement,
        (loc: number) => this.toggleMobileHudInfo(loc),
      )
      this.events.once(Phaser.Scenes.Events.SHUTDOWN, () => {
        this.mobileAvatarLayer?.destroy()
        this.mobileAvatarLayer = undefined
      })
    }
    this.createDesktopHudTextures()

    /* ── フォントスタイル ── */
    const scoreStyle: Phaser.Types.GameObjects.Text.TextStyle = {
      fontFamily: getUiFontFamily(),
      fontSize:   getUiFontSize(cssPx(HUD_METRICS.infoFontSize)),
      color:      this.layoutMode === 'mobileLandscape' ? '#fff9cf' : '#ffffff',
      resolution: HUD_TEXT_RESOLUTION,
    }
    const nameStyle: Phaser.Types.GameObjects.Text.TextStyle = {
      fontFamily: getUiFontFamily(),
      fontSize:   getUiFontSize(cssPx(HUD_METRICS.nameFontSize)),
      color:      '#ffffff',
      resolution: HUD_TEXT_RESOLUTION,
    }
    if (this.layoutMode !== 'mobileLandscape') {
      nameStyle.fontStyle = 'bold'
      nameStyle.stroke = '#101820'
      nameStyle.strokeThickness = 1
      nameStyle.shadow = { offsetX: 1, offsetY: 1, color: '#000000', blur: 0, fill: true }
    }

    /* ── 各プレイヤーの UI ── */
    for (let odr = 0; odr < 4; odr++) {
      const pos = odrBoxPos(odr)
      const avt = playerHudPoint(pos.avt, odr)
      const txt = playerHudPoint(pos.txt, odr)
      const name = playerHudPoint(pos.name, odr)
      const ttl = playerHudPoint(pos.ttl, odr)
      const trk = playerHudPoint(pos.trk, odr)

      this.majakTitleSprites[odr] = this.add.image(ttl.x, ttl.y, this.resolveSkinTextureKey('mj_board'))
        .setOrigin(0, 0).setDepth(2).setVisible(false)
      this.trickTitleSprites[odr] = this.add.image(trk.x, trk.y, this.resolveSkinTextureKey('mj_board'))
        .setOrigin(0, 0).setDepth(1).setVisible(false)
      this.mobileHudPanels[odr] = this.add.rectangle(0, 0, 1, 1, 0x103916, 0.78)
        .setOrigin(0, 0).setDepth(7).setVisible(false)
      this.desktopHudPanels[odr] = this.add.image(0, 0, this.desktopHudPanelTextureKey())
        .setOrigin(0, 0).setDepth(0).setVisible(false)
      this.desktopTurnStrips[odr] = this.add.image(0, 0, this.desktopTurnStripTextureKey())
        .setOrigin(0, 0).setDepth(8).setVisible(false)
      this.avatarSprites[odr] = this.add.image(avt.x, avt.y, this.resolveSkinTextureKey('mj_aiAvtrL'))
        .setOrigin(0, 0).setDepth(10).setDisplaySize(HUD_METRICS.avatar.width, HUD_METRICS.avatar.height).setVisible(false)
        .setInteractive({ useHandCursor: true })
        .on('pointerup', () => this.toggleMobileHudInfo(odr))

      this.levelTexts[odr] = this.add.text(txt.x, txt.y, '', scoreStyle)
        .setOrigin(0, 0).setDepth(10).setAlign('center')

      this.scoreTexts[odr] = this.add.text(txt.x, txt.y + 15, '30000', scoreStyle)
        .setOrigin(0, 0).setDepth(300).setAlign('center')

      this.rankTexts[odr] = this.add.text(txt.x, txt.y + 30, '', scoreStyle)
        .setOrigin(0, 0).setDepth(10).setAlign('center')

      this.diffTexts[odr] = this.add.text(txt.x, txt.y + 45, '', scoreStyle)
        .setOrigin(0, 0).setDepth(10).setAlign('center')

      this.nameTexts[odr] = this.add.text(name.x, name.y, '', nameStyle)
        .setOrigin(0, 0).setDepth(10).setAlign('center').setFixedSize(HUD_METRICS.nameWidth, HUD_METRICS.nameHeight)

      /* リーチ棒 (CMJTblDraw::PutRicStk) */
      this.reachSprites[odr] = this.add.image(0, 0, this.resolveSkinTextureKey('mj_richbar_0'))
        .setOrigin(0, 0).setDepth(Z_REACH_STICK).setVisible(false)
    }

    this.hostMark = this.add.image(0, 0, this.resolveSkinTextureKey('mj_hostmark'))
      .setOrigin(0, 0).setDepth(11).setVisible(false)

    for (let loc = 0; loc < 4; loc++) {
      const point = centerInfoContentPoint(MEN_FON_POS[loc])
      this.menFonSprites[loc] = this.add.image(point.x, point.y, this.resolveSkinTextureKey(`mj_myfan_${loc}`), 0)
        .setOrigin(0, 0).setDepth(302).setVisible(false)
    }
    this.chichaSprite = this.add.image(0, 0, this.resolveSkinTextureKey('mj_oyahuda_0'), 0)
      .setOrigin(0, 0).setDepth(302).setVisible(false)

    /* ── 局/風表示 (中央: CMJTblDraw m_bmpChaFon / m_bmpKyoNum) ── */
    const chaFonPoint = centerInfoContentPoint({ x: X_CHANFON, y: Y_CHANFON })
    const kyokuNumPoint = centerInfoContentPoint({ x: X_KYOKNUM, y: Y_KYOKNUM })
    this.chaFonSprite = this.add.image(chaFonPoint.x, chaFonPoint.y, this.resolveSkinTextureKey('mj_kyoku'), 0)
      .setOrigin(0, 0).setDepth(300)
    this.kyokuNumSprite = this.add.image(kyokuNumPoint.x, kyokuNumPoint.y, this.resolveSkinTextureKey('mj_kyokuNum'), 0)
      .setOrigin(0, 0).setDepth(300)

    /* ── 残り牌枚数 / リーチ棒 / 連荘数 (CMJObjNum) ── */
    const leftCountPoint = centerInfoContentPoint({ x: X_LEFTCNT, y: Y_LEFTCNT })
    const riboCountPoint = centerInfoContentPoint({ x: X_RIBOCNT, y: Y_RIBOCNT })
    const renchanCountPoint = centerInfoContentPoint({ x: X_RENCCNT, y: Y_RENCCNT })
    this.leftNumber = this.createLegacyNumber('mj_num_game00', 3, leftCountPoint.x, leftCountPoint.y, 9, 9, 300)
    this.riboNumber = this.createLegacyNumber('mj_num_game00', 2, riboCountPoint.x, riboCountPoint.y, 9, 9, 300)
    this.renchanNumber = this.createLegacyNumber('mj_num_game00', 2, renchanCountPoint.x, renchanCountPoint.y, 9, 9, 300)
    this.setLegacyNumber(this.leftNumber, 70)
    this.setLegacyNumber(this.riboNumber, 0)
    this.setLegacyNumber(this.renchanNumber, 0)
    const leftDicePoint = centerInfoContentPoint({ x: X_DICELFT, y: Y_DICELFT })
    const rightDicePoint = centerInfoContentPoint({ x: X_DICERGT, y: Y_DICERGT })
    this.diceSprites = [
      this.add.image(leftDicePoint.x, leftDicePoint.y, this.resolveSkinTextureKey('mj_dice'), 0).setOrigin(0, 0).setDepth(301).setVisible(false),
      this.add.image(rightDicePoint.x, rightDicePoint.y, this.resolveSkinTextureKey('mj_dice'), 0).setOrigin(0, 0).setDepth(301).setVisible(false),
    ]

    /* ── タイマー (CMJObjTimBar: 493×6) ── */
    this.timerBack = this.add.rectangle(BOARD_X + X_TIMBAR, BOARD_Y + Y_TIMBAR, W_TIMBAR, H_TIMBAR, 0x000000)
      .setOrigin(0, 0).setDepth(1001).setVisible(false).setInteractive()
    this.timerBar = this.add.rectangle(BOARD_X + X_TIMBAR, BOARD_Y + Y_TIMBAR, W_TIMBAR, H_TIMBAR, 0x0000ff)
      .setOrigin(0, 0).setDepth(1002).setVisible(false).setInteractive()
    this.timerTurnBar = this.add.rectangle(BOARD_X + X_TIMBAR, BOARD_Y + Y_TIMBAR, W_TIMBAR, H_TIMBAR, 0x0080ff)
      .setOrigin(0, 0).setDepth(1003).setVisible(false).setInteractive()
    this.timerKeepBar = this.add.rectangle(BOARD_X + X_TIMBAR, BOARD_Y + Y_TIMBAR, W_TIMBAR, H_TIMBAR, 0x00ffff)
      .setOrigin(0, 0).setDepth(1004).setVisible(false).setInteractive()
    const holdAutoPass = () => window.dispatchEvent(new Event(GAME_AUTO_PASS_HOLD_EVENT))
    this.timerBack.on('pointerover', holdAutoPass)
    this.timerBar.on('pointerover', holdAutoPass)
    this.timerTurnBar.on('pointerover', holdAutoPass)
    this.timerKeepBar.on('pointerover', holdAutoPass)
    this.updateTimerLayout()
    if (this.layoutMode === 'mobileLandscape' && !this.isViewer) this.showInactiveTimerBar()

    window.addEventListener(PAIFU_GRAPH_EVENT, this.onPaifuGraphVisibilityChanged)
    this.events.once(Phaser.Scenes.Events.SHUTDOWN, () => {
      window.removeEventListener(PAIFU_GRAPH_EVENT, this.onPaifuGraphVisibilityChanged)
    })

    /* ── GameScene からのイベント受信 ── */
    const gs = this.scene.get('GameScene')

    /* ステート更新 */
    gs.events.on('stateUpdate', (data: {
      players: PlayerHudState[]
      kyoku?: string; kyokuCnt?: number; chicha?: number; oyaOrder?: number; left?: number; ribo?: number; renchan?: number; dice?: number[]; waremeOdr?: number; viewOdr?: number; roundStart?: boolean; roundPresentationDelayMs?: number; activeTurnOdr?: number; preserveTurnMark?: boolean
    }) => {
      if (data.viewOdr !== undefined) this.myOdr = data.viewOdr
      if (data.chicha !== undefined) this.chicha = data.chicha
      if (data.oyaOrder !== undefined) this.oyaOrder = data.oyaOrder
      if (data.kyokuCnt !== undefined) this.kyokuCnt = data.kyokuCnt
      if (data.roundStart) this.clearRoundMarkers(Boolean(data.preserveTurnMark))
      this.updatePlayerTexts(data.players)
      if (data.kyoku) this.updateKyoku(data.kyoku)
      this.updateWindMarkers()
      const activeTurnOdr = data.activeTurnOdr
      if (typeof activeTurnOdr === 'number' && Number.isInteger(activeTurnOdr) && activeTurnOdr >= 0 && activeTurnOdr < data.players.length) this.updateTurnMarks(activeTurnOdr)
      if (data.left  !== undefined) this.setLegacyNumber(this.leftNumber, data.left)
      if (data.ribo !== undefined) this.setLegacyNumber(this.riboNumber, data.ribo)
      if (data.renchan !== undefined) this.setLegacyNumber(this.renchanNumber, data.renchan)
      if (data.roundStart && data.dice && data.dice.length >= 2) {
        this.startRoundDiceRoll(data.dice, data.waremeOdr, data.roundPresentationDelayMs)
      } else {
        if (data.dice && data.dice.length >= 2) this.updateDice(data.dice)
        if (data.waremeOdr !== undefined) this.updateWareme(data.waremeOdr)
      }
    })

    gs.events.on('viewOdrChange', (data: { viewOdr: number; players: PlayerHudState[] }) => {
      this.myOdr = data.viewOdr
      this.updatePlayerTexts(data.players)
      if (this.activeTurnOdr !== null) this.updateTurnMarks(this.activeTurnOdr)
      if (this.waremeOdr !== null) this.updateWareme(this.waremeOdr)
      this.updateReachTexts()
    })

    /* ターン切り替え (IniTurn / 捨て牌後 相当) */
    gs.events.on('turnChange', (data: { odr: number; timeLimit?: number; viewOdr?: number }) => {
      if (data.viewOdr !== undefined) this.myOdr = data.viewOdr
      this.traceUiFlow('turnChange event', data)
      this.updateTurnMarks(data.odr)
    })

    gs.events.on('actionPromptStart', (data: ActionPromptTimerData) => {
      if (data.viewOdr !== undefined) this.myOdr = data.viewOdr
      this.traceUiFlow('actionPromptStart event', { ...data })
      if (Number.isFinite(data.timeLimit) && Number(data.timeLimit) > 0) this.startTimer(data)
    })

    gs.events.on('actionPromptEnd', (data: { viewOdr?: number }) => {
      if (data.viewOdr !== undefined) this.myOdr = data.viewOdr
      this.traceUiFlow('actionPromptEnd event', data)
      this.stopTimer()
    })

    /* リーチ */
    gs.events.on('reach', (data: { odr: number; viewOdr?: number }) => {
      if (data.viewOdr !== undefined) this.myOdr = data.viewOdr
      this.reachedOdr.add(data.odr)
      if (!this.playLegacyReachDeclaration(data.odr)) this.updateReachTexts()
    })

    gs.events.on('callAction', (data: { odr: number; frame: number; avatarUrl: string; fallbackAvatarUrl: string; costumeAction?: LegacyCostumeAction }) => {
      this.showCallAction(data)
    })

    gs.events.on('titleSkill', (data: { odr: number; element: string; level: 1 | 2; delays: number[]; delay?: number }) => {
      this.showTitleSkill(data)
    })

    this.time.addEvent({ delay: 100, loop: true, callback: () => this.advanceCostumeAnimations() })

    /* 局結果 → CMJKyoRes ダイアログへ (将来実装) */
    gs.events.on('kyoResult', (_data: Record<string, string>) => {
      this.stopTimer()
    })
  }

  update() {
    if (!isMobileIngameLayout(this.layoutMode) && this.layoutMode !== 'responsiveDesktop') return
    const bounds = mobileVisibleWorldBounds()
    if (!bounds) return
    const layoutKey = mobileVisibleWorldLayoutKey(this.layoutMode)
    if (layoutKey === this.lastMobileHudLayoutKey) return
    this.lastMobileHudLayoutKey = layoutKey
    this.updateCenterHudLayout()
    this.updateTimerLayout()
    if (this.players.length > 0) this.updatePlayerTexts(this.players)
    if (this.activeTurnOdr !== null) this.updateTurnMarks(this.activeTurnOdr)
    if (this.waremeOdr !== null) this.updateWareme(this.waremeOdr)
  }

  private resolveSkinTextureKey(key: string): string {
    const candidate = skinTextureCandidate(key)
    return this.textures.exists(candidate) ? candidate : key
  }

  private showCallAction(data: { odr: number; frame: number; avatarUrl: string; fallbackAvatarUrl: string; costumeAction?: LegacyCostumeAction }) {
    if (data.odr < 0 || data.odr >= 4) return
    if (data.costumeAction) this.startCostumeAction(data.odr, data.costumeAction)
    const loc = this.odrToLoc(data.odr)
    const point = this.callActionPoint(loc)
    const balloon = this.add.image(point.x, point.y, this.resolveSkinTextureKey(`mj_baloon_${loc}`), data.frame)
      .setOrigin(0, 0)
      .setDepth(Z_CALL_BALLOON)
    const avatar = this.showCallAvatar(data.odr, loc, point, data.avatarUrl, data.fallbackAvatarUrl)
    this.callSprites.push(balloon)
    if (avatar.sprite) this.callSprites.push(avatar.sprite)
    this.time.delayedCall(1100, () => {
      balloon.destroy()
      avatar.destroy()
      this.callSprites = this.callSprites.filter(item => item !== balloon && item !== avatar.sprite)
    })
  }

  private showTitleSkill(data: { odr: number; element: string; level: 1 | 2; delays: number[]; delay?: number }) {
    if (data.odr < 0 || data.odr >= 4) return
    const loc = this.odrToLoc(data.odr)
    const avatar = this.avatarBounds[loc]
    const keys = Array.from({ length: data.delays.length }, (_, index) => `mj_ef_L${data.level}${data.element}_${String(index + 1).padStart(2, '0')}`)
    const frame = this.textures.getFrame(keys[0])
    const width = frame?.width ?? 102
    const height = frame?.height ?? 124
    const point = avatar
      ? {
          x: data.level === 1 ? avatar.x + avatar.width / 2 - 57 : avatar.x + (avatar.width - width) / 2,
          y: avatar.y + (avatar.height - height) / 2,
        }
      : this.callActionPoint(loc)
    this.playLegacySkillFrameSequence(keys, point.x, point.y, data.delays, data.delay)
  }

  playLegacySkillFrameSequence(keys: string[], x: number, y: number, frameDelays: readonly number[], delay = 0) {
    const duration = frameDelays.reduce((sum, value) => sum + value, 0)
    if (keys.length === 0 || !this.textures.exists(keys[0])) return delay + duration
    const start = () => {
      if (document.visibilityState !== 'visible' || window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) return
      const sprite = this.add.image(x, y, keys[0])
        .setOrigin(0, 0)
        .setDepth(10000)
        .setBlendMode(Phaser.BlendModes.ADD)
      this.skillEffectSprites.push(sprite)
      let elapsed = 0
      for (let frame = 1; frame < keys.length; frame++) {
        elapsed += frameDelays[frame - 1] ?? 0
        this.time.delayedCall(elapsed, () => {
          if (sprite.active && this.textures.exists(keys[frame])) sprite.setTexture(keys[frame])
        })
      }
      this.time.delayedCall(duration, () => {
        sprite.destroy()
        this.skillEffectSprites = this.skillEffectSprites.filter(item => item !== sprite)
      })
    }
    if (delay > 0) this.time.delayedCall(delay, start)
    else start()
    return delay + duration
  }

  private callActionPoint(loc: number): HudPoint {
    const point = isMobileIngameLayout(this.layoutMode) ? this.mobileCallActionPoint(loc) : seatEffectPoint(CALL_POS[loc], loc)
    if (!isMobileIngameLayout(this.layoutMode)) return point
    const bounds = mobileVisibleWorldBounds()
    const size = CALL_BALLOON_SIZE[loc]
    if (!bounds || !size) return point
    const inset = 8
    const minX = bounds.left + inset
    const maxX = bounds.right - size.w - inset
    const minY = bounds.top + inset
    const maxY = bounds.bottom - size.h - inset
    return {
      x: Phaser.Math.Clamp(point.x, minX, Math.max(minX, maxX)),
      y: Phaser.Math.Clamp(point.y, minY, Math.max(minY, maxY)),
    }
  }

  private mobileCallActionPoint(loc: number): HudPoint {
    const size = CALL_BALLOON_SIZE[loc] ?? CALL_BALLOON_SIZE[0]
    const centerX = MOBILE_CALL_CENTER_INFO.x + MOBILE_CALL_CENTER_INFO.width / 2
    const centerY = MOBILE_CALL_CENTER_INFO.y + MOBILE_CALL_CENTER_INFO.height / 2
    let x = centerX - size.w / 2
    let y = centerY - size.h / 2
    if (loc === 0) y = MOBILE_CALL_CENTER_INFO.y + MOBILE_CALL_CENTER_INFO.height - MOBILE_CALL_BOTTOM_OVERLAP
    else if (loc === 1) x = MOBILE_CALL_CENTER_INFO.x + MOBILE_CALL_CENTER_INFO.width - MOBILE_CALL_SIDE_OVERLAP
    else if (loc === 2) y = MOBILE_CALL_CENTER_INFO.y - size.h - MOBILE_CALL_TOP_GAP
    else if (loc === 3) x = MOBILE_CALL_CENTER_INFO.x - size.w + MOBILE_CALL_SIDE_OVERLAP
    return centerHudPoint({ x: Math.round(x), y: Math.round(y) })
  }

  private showCallAvatar(_odr: number, loc: number, point: HudPoint, avatarUrl: string, fallbackAvatarUrl: string): CallAvatarHandle {
    const offset = CALL_AVATAR_POS[loc]
    const x = point.x + offset.x
    const y = point.y + offset.y
    if (this.layoutMode === 'mobileLandscape' && this.mobileAvatarLayer) {
      return {
        destroy: this.mobileAvatarLayer.showCallAvatar({
          url: avatarUrl,
          fallbackUrl: fallbackAvatarUrl,
          x,
          y,
          width: CALL_AVATAR_SIZE.w,
          height: CALL_AVATAR_SIZE.h,
        }),
      }
    }
    const key = this.avatarTextureKey(avatarUrl)
    const avatar = this.add.image(x, y, this.resolveSkinTextureKey('mj_aiAvtrW'))
      .setOrigin(0, 0)
      .setDepth(Z_CALL_AVATAR)
    this.setDynamicImage(
      avatar,
      key,
      avatarUrl,
      x,
      y,
      Z_CALL_AVATAR,
      'mj_aiAvtrW',
      true,
      { width: CALL_AVATAR_SIZE.w, height: CALL_AVATAR_SIZE.h },
      'contain',
    )
    return { sprite: avatar, destroy: () => avatar.destroy() }
  }

  /* ======================================================================
   * ターンマーク更新 (CMJTblDraw::PutOdrBox 相当)
   * ====================================================================== */
  private updateTurnMarks(activeOdr: number) {
    this.activeTurnOdr = activeOdr
    const loc = this.odrToLoc(activeOdr)
    this.traceUiFlow('turnMark update', { activeOdr, myOdr: this.myOdr, loc })
    this.updateWindMarkers()
    window.dispatchEvent(new CustomEvent(TURN_MARK_EVENT, { detail: { activeOdr, viewOdr: this.myOdr } }))

    // タイマー表示は actionPromptStart/actionPromptEnd に集約する。
    if (activeOdr !== this.myOdr) {
      this.stopTimer()
    }
    if (this.players.length > 0) this.updatePlayerTexts(this.players)
  }

  private toggleMobileHudInfo(loc: number) {
    if (this.layoutMode !== 'mobileLandscape') return
    void loc
    if (this.players.length > 0) this.updatePlayerTexts(this.players)
    this.updateHostMark()
  }

  private isMobileHudInfoVisible(loc: number) {
    if (this.layoutMode === 'mobileLandscape') return false
    return this.isMobileAvatarExpanded(loc)
  }

  private isMobileAvatarExpanded(loc: number) {
    if (this.layoutMode !== 'mobileLandscape') return true
    return this.activeTurnOdr !== null && this.odrToLoc(this.activeTurnOdr) === loc
  }

  private mobileAvatarSize(_infoVisible: boolean) {
    return { width: MOBILE_HUD_ICON_WIDTH, height: MOBILE_HUD_ICON_HEIGHT }
  }

  private desktopAvatarSize(_player: PlayerHudState) {
    return DESKTOP_PLAYER_AVATAR_SIZE
  }

  private desktopAvatarPoint(point: HudPoint): HudPoint {
    return {
      x: point.x - (DESKTOP_PLAYER_AVATAR_SIZE.width - DESKTOP_HUD_METRICS.avatar.width) / 2,
      y: point.y - (DESKTOP_PLAYER_AVATAR_SIZE.height - DESKTOP_HUD_METRICS.avatar.height) / 2,
    }
  }

  private mobileAvatarPoint(loc: number, fallback: HudPoint, size: { width: number; height: number }): HudPoint {
    if (!isMobileIngameLayout(this.layoutMode)) return fallback
    const bounds = mobileVisibleWorldBounds()
    if (!bounds) return fallback
    const insetX = 14
    const insetTop = 8
    const insetBottom = 14
    const isRight = loc === 1 || loc === 2
    const isBottom = loc === 0 || loc === 1
    const nameReserve = HUD_METRICS.nameHeight + MOBILE_HUD_NAME_GAP
    return {
      x: isRight ? bounds.right - size.width - insetX : bounds.left + insetX,
      y: isBottom ? bounds.bottom - size.height - insetBottom - nameReserve : bounds.top + insetTop,
    }
  }

  private odrToLoc(odr: number): number {
    return ((odr - this.myOdr) % 4 + 4) % 4
  }

  private traceUiFlow(eventName: string, details: Record<string, unknown> = {}) {
    if (!DEBUG_UI_FLOW) return
    console.info(`${UI_FLOW_TRACE_PREFIX} #${++this.flowTraceSerial} ${eventName}`, {
      myOdr: this.myOdr,
      activeTurnOdr: this.activeTurnOdr,
      timerMaxMs: this.timerMaxMs,
      timerRemainingMs: this.timerEndAt > 0 ? Math.max(0, Math.round(this.timerEndAt - performance.now())) : 0,
      ...details,
    })
  }

  private fitNameText(_loc: number, x: number, text: string) {
    const baseWidth = isMobileIngameLayout(this.layoutMode) ? MOBILE_HUD_NAME_WIDTH : HUD_METRICS.nameWidth
    let fontSize = cssPx(HUD_METRICS.nameFontSize)
    let measuredWidth = measureHudTextWidth(text, fontSize)
    if (!isMobileIngameLayout(this.layoutMode)) {
      return { x, width: baseWidth, fontSize: getUiFontSize(fontSize) }
    }
    while (measuredWidth > baseWidth - 4 && fontSize > HUD_NAME_MIN_FONT_SIZE) {
      fontSize -= 1
      measuredWidth = measureHudTextWidth(text, fontSize)
    }
    return {
      x,
      width: baseWidth,
      fontSize: getUiFontSize(fontSize),
    }
  }

  private mobileHudPanelStyle() {
    const tengoku = isTengokuBoardSkin(this.customBgId, this.customBoardType)
    return tengoku
      ? { fill: 0x10283b, fillAlpha: 0.9, stroke: 0x58d3ff, strokeAlpha: 0.5, activeStroke: 0xa8ecff }
      : { fill: 0x103916, fillAlpha: 0.9, stroke: 0x6aa35f, strokeAlpha: 0.42, activeStroke: 0xc8f5ae }
  }

  private desktopHudPanelTextureKey() {
    return isTengokuBoardSkin(this.customBgId, this.customBoardType) ? 'desktopHudPanelTengoku' : 'desktopHudPanelDefault'
  }

  private desktopTurnStripTextureKey() {
    return isTengokuBoardSkin(this.customBgId, this.customBoardType) ? 'desktopHudTurnStripTengoku' : 'desktopHudTurnStripDefault'
  }

  private createDesktopHudTextures() {
    const style = this.mobileHudPanelStyle()
    const panelKey = this.desktopHudPanelTextureKey()
    const stripKey = this.desktopTurnStripTextureKey()
    if (!this.textures.exists(panelKey)) {
      const panel = this.make.graphics({ x: 0, y: 0 })
      panel.fillStyle(style.fill, 0.94)
      panel.fillRoundedRect(0, 0, 128, 128, 4)
      panel.fillStyle(style.stroke, 0.18)
      panel.fillRoundedRect(3, 3, 122, 22, 2)
      panel.lineStyle(2, style.stroke, 0.9)
      panel.strokeRoundedRect(1, 1, 126, 126, 4)
      panel.lineStyle(1, 0xffffff, 0.2)
      panel.strokeRoundedRect(4, 4, 120, 120, 2)
      panel.generateTexture(panelKey, 128, 128)
      panel.destroy()
    }
    if (!this.textures.exists(stripKey)) {
      const strip = this.make.graphics({ x: 0, y: 0 })
      strip.fillStyle(style.activeStroke, 0.9)
      strip.fillRoundedRect(0, 0, 128, 18, 3)
      strip.lineStyle(1, style.stroke, 1)
      strip.strokeRoundedRect(0, 0, 128, 18, 3)
      strip.lineStyle(1, 0xffffff, 0.35)
      strip.lineBetween(4, 3, 124, 3)
      strip.generateTexture(stripKey, 128, 18)
      strip.destroy()
    }
  }

  private updateMobileHudPanel(loc: number, avt: HudPoint, avatarSize: { width: number; height: number }, nameX: number, nameY: number, nameWidth: number, textLeft: number, textY: number, textWidth: number, infoRows: number, infoRowHeight: number) {
    const panel = this.mobileHudPanels[loc]
    const turnStrip = this.desktopTurnStrips[loc]
    if (!panel) return
    if (!isMobileIngameLayout(this.layoutMode)) {
      panel.setVisible(false)
      return
    }
    const style = this.mobileHudPanelStyle()
    if (!this.isMobileHudInfoVisible(loc)) {
      const padding = MOBILE_HUD_COMPACT_AVATAR_PADDING
      const left = Math.min(avt.x, nameX) - padding
      const right = Math.max(avt.x + avatarSize.width, nameX + nameWidth) + padding
      const bottom = Math.max(avt.y + avatarSize.height, nameY + HUD_METRICS.nameHeight) + padding
      panel
        .setPosition(left, avt.y - padding)
        .setSize(right - left, bottom - avt.y + padding)
        .setFillStyle(style.fill, style.fillAlpha)
        .setStrokeStyle(1, style.stroke, style.strokeAlpha)
        .setVisible(true)
      if (turnStrip) {
        const active = this.activeTurnOdr !== null && this.odrToLoc(this.activeTurnOdr) === loc
        if (turnStrip.getData('turnActive') !== active) {
          turnStrip.setData('turnActive', active)
          this.tweens.killTweensOf(turnStrip)
          turnStrip.setAlpha(1).setVisible(active)
          if (active) this.tweens.add({ targets: turnStrip, alpha: 0.25, duration: 650, ease: 'Sine.InOut', yoyo: true, repeat: -1 })
        }
        turnStrip
          .setPosition(left, avt.y - padding)
          .setTexture(this.desktopTurnStripTextureKey())
          .setDisplaySize(right - left, 6)
          .setVisible(active)
      }
      return
    }
    const left = Math.min(avt.x, nameX, textLeft) - MOBILE_HUD_PANEL_PADDING_X
    const top = Math.min(avt.y, nameY, textY) - MOBILE_HUD_PANEL_PADDING_Y
    const right = Math.max(avt.x + avatarSize.width, nameX + nameWidth, textLeft + textWidth) + MOBILE_HUD_PANEL_PADDING_X
    const bottom = Math.max(avt.y + avatarSize.height, nameY + HUD_METRICS.nameHeight, textY + infoRows * infoRowHeight) + MOBILE_HUD_PANEL_PADDING_Y
    panel
      .setPosition(left, top)
      .setSize(right - left, bottom - top)
      .setFillStyle(style.fill, style.fillAlpha)
      .setStrokeStyle(1, style.stroke, style.strokeAlpha)
      .setVisible(true)
  }

  private updateDesktopHudPanel(loc: number, avt: HudPoint, avatarSize: { width: number; height: number }, textLeft: number, textY: number, textWidth: number, infoRows: number, infoRowHeight: number) {
    const panel = this.desktopHudPanels[loc]
    const turnStrip = this.desktopTurnStrips[loc]
    if (!panel || !turnStrip) return null
    if (this.layoutMode !== 'responsiveDesktop') {
      panel.setVisible(false)
      turnStrip.setVisible(false)
      this.desktopHudBounds[loc] = undefined
      return null
    }
    const paddingX = avatarSize.width * 0.12
    const paddingY = HUD_METRICS.nameHeight / 2
    const contentLeft = Math.min(avt.x, textLeft)
    const contentRight = Math.max(avt.x + avatarSize.width, textLeft + textWidth)
    const panelWidth = Math.max(contentRight - contentLeft + paddingX * 2, HUD_METRICS.nameWidth + paddingX * 2)
    const left = (contentLeft + contentRight - panelWidth) / 2
    const top = Math.min(avt.y, textY) - paddingY
    const contentBottom = Math.max(avt.y + avatarSize.height, textY + infoRows * infoRowHeight)
    const bottom = contentBottom + paddingY
    panel
      .setPosition(left, top)
      .setTexture(this.desktopHudPanelTextureKey())
      .setDisplaySize(panelWidth, bottom - top)
      .setVisible(true)
    this.desktopHudBounds[loc] = { left, top, width: panelWidth, height: bottom - top }
    const stripWidth = panelWidth
    const stripHeight = Math.max(HUD_METRICS.nameHeight, avatarSize.height * 0.16)
    const stripX = left
    const stripY = top
    const active = this.activeTurnOdr !== null && this.odrToLoc(this.activeTurnOdr) === loc
    if (turnStrip.getData('turnActive') !== active) {
      turnStrip.setData('turnActive', active)
      this.tweens.killTweensOf(turnStrip)
      turnStrip.setAlpha(1).setVisible(active)
      if (active) this.tweens.add({ targets: turnStrip, alpha: 0.25, duration: 650, ease: 'Sine.InOut', yoyo: true, repeat: -1 })
    }
    turnStrip
      .setPosition(stripX, stripY)
      .setTexture(this.desktopTurnStripTextureKey())
      .setDisplaySize(stripWidth, stripHeight)
    return {
      x: left + paddingX,
      y: contentBottom - HUD_METRICS.nameHeight,
      width: panelWidth - paddingX * 2,
    }
  }

  private updatePlayerTexts(players: PlayerHudState[]) {
    this.players = players
    const hudVisible = !this.replayGraphVisible
    players.forEach((p, odr) => {
      const loc = this.odrToLoc(odr)
      const pos = odrBoxPos(loc)
      const baseAvt = playerHudPoint(pos.avt, loc)
      const txt = playerHudPoint(pos.txt, loc)
      const name = playerHudPoint(pos.name, loc)
      const ttl = playerHudPoint(pos.ttl, loc)
      const trk = playerHudPoint(pos.trk, loc)
      const mobileInfoVisible = this.isMobileHudInfoVisible(loc)
      const nameVisible = isMobileIngameLayout(this.layoutMode) || mobileInfoVisible
      const avatarSize = this.layoutMode === 'mobileLandscape' ? this.mobileAvatarSize(this.isMobileAvatarExpanded(loc)) : this.desktopAvatarSize(p)
      const avt = isMobileIngameLayout(this.layoutMode)
        ? this.mobileAvatarPoint(loc, baseAvt, avatarSize)
        : this.desktopAvatarPoint(baseAvt)
      this.avatarBounds[loc] = { x: avt.x, y: avt.y, width: avatarSize.width, height: avatarSize.height }
      const mobileTextLeft = loc === 1 || loc === 2 ? avt.x - MOBILE_HUD_INFO_WIDTH - MOBILE_HUD_TEXT_GAP : avt.x + avatarSize.width + MOBILE_HUD_TEXT_GAP
      const mobileNameX = avt.x + (avatarSize.width - MOBILE_HUD_NAME_WIDTH) / 2
      const mobileNameY = avt.y + avatarSize.height + MOBILE_HUD_NAME_GAP
      const desktopInfoOffsetY = this.layoutMode === 'responsiveDesktop' ? RESPONSIVE_DESKTOP_PLAYER_INFO_OFFSET_Y : 0
      const textY = isMobileIngameLayout(this.layoutMode) ? avt.y + MOBILE_HUD_INFO_TOP_OFFSET : txt.y + DESKTOP_HUD_INFO_Y_SHIFT + desktopInfoOffsetY
      const textBounds = isMobileIngameLayout(this.layoutMode) ? { left: mobileTextLeft, width: MOBILE_HUD_INFO_WIDTH } : avatarTextBounds(loc)
      const textAlign = isMobileIngameLayout(this.layoutMode)
        ? (loc === 1 || loc === 2 ? 'right' : 'left')
        : 'center'
      const isComputer = !p.pix
      const displayName = p.name || p.pix || '<トントン>'
      const levelText = p.level || (isComputer ? '----' : '')
      const compactInfo = this.layoutMode === 'mobileLandscape' && levelText.trim() === ''
      const infoRowHeight = this.layoutMode === 'mobileLandscape' ? MOBILE_HUD_INFO_ROW_HEIGHT : 15
      const desktopPanelName = this.updateDesktopHudPanel(loc, avt, avatarSize, textBounds.left, textY, textBounds.width, compactInfo ? 3 : 4, infoRowHeight)
      const nameAlign = desktopPanelName ? 'center' : isMobileIngameLayout(this.layoutMode) ? 'center' : textAlign
      const nameLayout = desktopPanelName
        ? { x: desktopPanelName.x, width: desktopPanelName.width, fontSize: this.fitNameText(loc, desktopPanelName.x, displayName).fontSize }
        : this.fitNameText(loc, isMobileIngameLayout(this.layoutMode) ? mobileNameX : name.x, displayName)
      const nameY = desktopPanelName?.y ?? (isMobileIngameLayout(this.layoutMode) ? mobileNameY : name.y)
      this.nameTexts[loc].setColor(isComputer ? '#ff6060' : '#ffffff').setFontSize(nameLayout.fontSize).setPosition(nameLayout.x, nameY).setFixedSize(nameLayout.width, HUD_METRICS.nameHeight).setAlign(nameAlign).setText(displayName).setVisible(nameVisible && hudVisible)
      this.levelTexts[loc].setPosition(textBounds.left, textY).setFixedSize(textBounds.width, infoRowHeight).setAlign(textAlign).setText(levelText).setVisible(mobileInfoVisible && !compactInfo && hudVisible)
      this.scoreTexts[loc].setPosition(textBounds.left, textY + (compactInfo ? 0 : infoRowHeight)).setFixedSize(textBounds.width, infoRowHeight).setAlign(textAlign).setText(this.formatPointText(p)).setVisible(mobileInfoVisible && hudVisible)
      this.rankTexts[loc].setPosition(textBounds.left, textY + (compactInfo ? infoRowHeight : infoRowHeight * 2)).setFixedSize(textBounds.width, infoRowHeight).setAlign(textAlign).setText(this.formatRankText(players, odr)).setVisible(mobileInfoVisible && hudVisible)
      this.diffTexts[loc].setPosition(textBounds.left, textY + (compactInfo ? infoRowHeight * 2 : infoRowHeight * 3)).setFixedSize(textBounds.width, infoRowHeight).setAlign(textAlign).setText(this.formatDiffText(players, odr)).setVisible(mobileInfoVisible && hudVisible)
      this.updateMobileHudPanel(loc, avt, avatarSize, nameLayout.x, nameY, nameLayout.width, textBounds.left, textY, textBounds.width, compactInfo ? 3 : 4, infoRowHeight)
      const costumeFrame = this.costumeFrameResource(odr, p)
      const costumeUrl = this.costumeAvatarUrl(p)
      const avatarUrl = costumeFrame?.url || costumeUrl || p.avatarUrl || p.fallbackAvatarUrl || ''
      if (this.mobileAvatarLayer) {
        this.avatarSprites[loc].setVisible(false)
        this.mobileAvatarLayer.update(loc, {
          url: avatarUrl || `${IMG}/mj_aiAvtrL.png`,
          fallbackUrl: p.fallbackAvatarUrl || `${IMG}/mj_aiAvtrL.png`,
          x: avt.x,
          y: avt.y,
          width: avatarSize.width,
          height: avatarSize.height,
          visible: hudVisible,
          alt: displayName,
        })
      } else {
        const avatarFit = costumeFrame || costumeUrl ? 'cover' : 'contain'
        this.setDynamicImage(this.avatarSprites[loc], costumeFrame?.key ?? this.avatarKey(odr, p), avatarUrl, avt.x, avt.y, 10, 'mj_aiAvtrL', true, avatarSize, avatarFit)
      }
      if (!hudVisible) {
        this.mobileHudPanels[loc].setVisible(false)
        this.desktopHudPanels[loc].setVisible(false)
        this.desktopTurnStrips[loc].setVisible(false)
        this.avatarSprites[loc].setVisible(false)
      }
      const majakTitleDepth = isMobileIngameLayout(this.layoutMode) ? 9 : 2
      const trickTitleDepth = isMobileIngameLayout(this.layoutMode) ? 8 : 1
      const titleVisible = (isMobileIngameLayout(this.layoutMode) || mobileInfoVisible) && hudVisible
      const desktopTitleOffsetY = this.layoutMode === 'responsiveDesktop' ? RESPONSIVE_DESKTOP_TITLE_OFFSET_Y : 0
      this.setDynamicImage(this.majakTitleSprites[loc], this.majakTitleKey(p.majakTitle), this.majakTitleUrl(p.majakTitle), isMobileIngameLayout(this.layoutMode) ? textBounds.left : ttl.x, (isMobileIngameLayout(this.layoutMode) ? avt.y : ttl.y) + desktopTitleOffsetY, majakTitleDepth, undefined, titleVisible)
      this.setDynamicImage(this.trickTitleSprites[loc], this.trickTitleKey(p.trickTitle), this.trickTitleUrl(p.trickTitle), isMobileIngameLayout(this.layoutMode) ? textBounds.left : trk.x, (isMobileIngameLayout(this.layoutMode) ? avt.y - 2 : trk.y) + desktopTitleOffsetY, trickTitleDepth, undefined, titleVisible)
    })
    this.updateHostMark()
  }

  private updateHostMark() {
    if (this.replayGraphVisible) {
      this.hostMark?.setVisible(false)
      return
    }
    const hostOdr = this.players.findIndex(player => player.isHost)
    if (hostOdr < 0 || !this.hostMark) {
      this.hostMark?.setVisible(false)
      return
    }
    const loc = this.odrToLoc(hostOdr)
    if (this.layoutMode === 'mobileLandscape' && !this.isMobileHudInfoVisible(loc)) {
      this.hostMark.setVisible(false)
      return
    }
    const baseAvt = playerHudPoint(odrBoxPos(loc).avt, loc)
    const avatarSize = this.layoutMode === 'mobileLandscape' ? this.mobileAvatarSize(this.isMobileAvatarExpanded(loc)) : this.desktopAvatarSize(this.players[hostOdr])
    const avt = isMobileIngameLayout(this.layoutMode)
      ? this.mobileAvatarPoint(loc, baseAvt, avatarSize)
      : this.desktopAvatarPoint(baseAvt)
    const desktopHudBounds = this.layoutMode === 'responsiveDesktop' ? this.desktopHudBounds[loc] : undefined
    const point = desktopHudBounds
      ? { x: desktopHudBounds.left + 3, y: desktopHudBounds.top + 3 }
      : isMobileIngameLayout(this.layoutMode) ? { x: avt.x + 24, y: avt.y + 58 } : { x: avt.x + 3, y: avt.y + 3 }
    this.hostMark.setPosition(point.x, point.y).setVisible(true)
  }

  private formatPointText(player: PlayerHudState) {
    if (Number.isFinite(player.score)) return String(player.score)
    if (Number.isFinite(player.rating)) return `[${String(player.rating).padStart(4, ' ')}]`
    return ''
  }

  private formatRankText(players: PlayerHudState[], odr: number) {
    const point = players[odr]?.score
    if (!Number.isFinite(point)) return ''
    let rank = 1
    for (let other = 0; other < players.length; other++) {
      if (other === odr) continue
      const otherPoint = players[other]?.score
      if (!Number.isFinite(otherPoint)) continue
      if (otherPoint > point || (otherPoint === point && this.oyaDistance(other) < this.oyaDistance(odr))) rank++
    }
    return `${rank}位`
  }

  private formatDiffText(players: PlayerHudState[], odr: number) {
    const point = players[odr]?.score
    const base = players[this.myOdr]?.score
    if (!Number.isFinite(point) || !Number.isFinite(base)) return ''
    const diff = point - base
    return `(${diff >= 0 ? '+' : ''}${diff})`
  }

  private oyaDistance(odr: number) {
    return (odr + 4 - this.chicha) % 4
  }

  private odrToFon(odr: number) {
    return (odr + 4 - this.oyaOrder) % 4
  }

  private updateWindMarkers() {
    for (let odr = 0; odr < 4; odr++) {
      const loc = this.odrToLoc(odr)
      const point = centerInfoContentPoint(MEN_FON_POS[loc])
      this.menFonSprites[loc]
        .setTexture(this.resolveSkinTextureKey(`mj_myfan_${loc}`))
        .setFrame(this.odrToFon(odr) + (this.activeTurnOdr === odr ? 4 : 0))
        .setPosition(point.x, point.y)
        .setVisible(!this.replayGraphVisible)
    }
    const chichaLoc = this.odrToLoc(this.chicha)
    const chichaPoint = this.chichaMarkerPoint(chichaLoc)
    this.chichaSprite
      ?.setTexture(this.resolveSkinTextureKey(`mj_oyahuda_${chichaLoc}`))
      .setFrame(Math.floor(this.kyokuCnt / 4))
      .setPosition(chichaPoint.x, chichaPoint.y)
      .setVisible(!this.replayGraphVisible)
  }

  private onPaifuGraphVisibilityChanged = (event: Event) => {
    const visible = (event as CustomEvent<{ visible?: unknown }>).detail?.visible === true
    if (this.replayGraphVisible === visible) return

    this.replayGraphVisible = visible
    if (visible) this.hideHudForPaifuGraph()
    else this.restoreHudAfterPaifuGraph()
  }

  private hideHudForPaifuGraph() {
    const objects = [
      ...this.majakTitleSprites,
      ...this.trickTitleSprites,
      ...this.mobileHudPanels,
      ...this.desktopHudPanels,
      ...this.desktopTurnStrips,
      ...this.avatarSprites,
      ...this.levelTexts,
      ...this.scoreTexts,
      ...this.rankTexts,
      ...this.diffTexts,
      ...this.nameTexts,
      ...this.reachSprites,
      ...this.menFonSprites,
      ...this.diceSprites,
      this.hostMark,
      this.chichaSprite,
      this.chaFonSprite,
      this.kyokuNumSprite,
      this.waremeSprite,
      this.timerBack,
      this.timerBar,
      this.timerTurnBar,
      this.timerKeepBar,
      ...this.leftNumber?.sprites ?? [],
      ...this.riboNumber?.sprites ?? [],
      ...this.renchanNumber?.sprites ?? [],
    ].filter((object): object is Phaser.GameObjects.GameObject => Boolean(object))

    this.graphHiddenHudObjects = objects.filter(object => object.visible)
    this.graphHiddenHudObjects.forEach(object => object.setVisible(false))
    this.mobileAvatarLayer?.setVisible(false)
  }

  private restoreHudAfterPaifuGraph() {
    this.graphHiddenHudObjects.forEach(object => object.setVisible(true))
    this.graphHiddenHudObjects = []
    this.mobileAvatarLayer?.setVisible(true)
  }

  private chichaMarkerPoint(loc: number): HudPoint {
    if (!isMobileIngameLayout(this.layoutMode)) return centerHudPoint(CHICHA_POS[loc])
    const avatarPoint = boardLocalPoint(odrBoxPos(loc).avt)
    const offset = MOBILE_CHICHA_OFFSET[loc]
    return { x: avatarPoint.x + offset.x, y: avatarPoint.y + offset.y }
  }

  private updateCenterHudLayout() {
    if (!this.chaFonSprite || !this.kyokuNumSprite || !this.leftNumber || !this.riboNumber || !this.renchanNumber) return
    const chaFonPoint = centerInfoContentPoint({ x: X_CHANFON, y: Y_CHANFON })
    const kyokuNumPoint = centerInfoContentPoint({ x: X_KYOKNUM, y: Y_KYOKNUM })
    const leftCountPoint = centerInfoContentPoint({ x: X_LEFTCNT, y: Y_LEFTCNT })
    const riboCountPoint = centerInfoContentPoint({ x: X_RIBOCNT, y: Y_RIBOCNT })
    const renchanCountPoint = centerInfoContentPoint({ x: X_RENCCNT, y: Y_RENCCNT })
    const leftDicePoint = centerInfoContentPoint({ x: X_DICELFT, y: Y_DICELFT })
    const rightDicePoint = centerInfoContentPoint({ x: X_DICERGT, y: Y_DICERGT })

    this.chaFonSprite?.setPosition(chaFonPoint.x, chaFonPoint.y)
    this.kyokuNumSprite?.setPosition(kyokuNumPoint.x, kyokuNumPoint.y)
    this.moveLegacyNumber(this.leftNumber, leftCountPoint.x, leftCountPoint.y)
    this.moveLegacyNumber(this.riboNumber, riboCountPoint.x, riboCountPoint.y)
    this.moveLegacyNumber(this.renchanNumber, renchanCountPoint.x, renchanCountPoint.y)
    this.diceSprites[0]?.setPosition(leftDicePoint.x, leftDicePoint.y)
    this.diceSprites[1]?.setPosition(rightDicePoint.x, rightDicePoint.y)
    this.updateWindMarkers()
    this.updateReachTexts()
  }

  private avatarKey(_odr: number, player: PlayerHudState) {
    return this.avatarTextureKey(this.costumeAvatarUrl(player) || player.avatarUrl || player.fallbackAvatarUrl || '')
  }

  private avatarTextureKey(url: string) {
    return url ? `avatar_${this.sanitizeKey(url)}` : ''
  }

  private costumeAvatarUrl(player: PlayerHudState) {
    const costumeId = Number(player.customCostume ?? 0)
    if (!Number.isFinite(costumeId) || costumeId <= 0 || costumeId === CUSTOM_DEFAULT_ID_COSTUME) return ''
    if (!AVAILABLE_COSTUME_IDS.has(costumeId)) return ''
    const costumeType = Number(player.customCostumeType ?? 0)
    if (costumeType > 0 && (costumeType < 30 || costumeType >= 40)) return ''
    const imageId = String(costumeId).padStart(2, '0')
    return `${IMG}/skin/${costumeId}/mj_costume_default_${imageId}.png`
  }

  private costumeFrameResource(odr: number, player: PlayerHudState) {
    const costumeId = Number(player.customCostume ?? 0)
    if (costumeId !== 9 && costumeId !== 10 && costumeId !== 11) return undefined
    const costumeType = Number(player.customCostumeType ?? 0)
    if (costumeType > 0 && (costumeType < 30 || costumeType >= 40)) return undefined
    const state = this.costumeAnimationStates[odr]
    const action = state?.action ?? (this.reachedOdr.has(odr) ? 'reach' : 'default')
    const frameCount = LEGACY_COSTUME_FRAME_COUNTS[costumeId][action]
    const frame = Math.min(state?.frame ?? 0, frameCount - 1)
    const suffix = String(costumeId).padStart(2, '0')
    const key = `mj_costume_${action}_${suffix}_${String(frame).padStart(2, '0')}`
    return { key, url: `${IMG}/skin/${costumeId}/${key}.png` }
  }

  private startCostumeAction(odr: number, action: LegacyCostumeAction) {
    const costumeId = Number(this.players[odr]?.customCostume ?? 0)
    if (costumeId !== 9 && costumeId !== 10 && costumeId !== 11) return
    const returnAction = action === 'ron' || action === 'tsumo'
      ? 'default'
      : this.reachedOdr.has(odr) || action === 'reach' ? 'reach' : 'default'
    this.costumeAnimationStates[odr] = {
      action,
      returnAction,
      frame: 0,
      oneShot: action !== 'default' && action !== 'reach',
    }
    this.updatePlayerTexts(this.players)
  }

  private advanceCostumeAnimations() {
    let changed = false
    this.players.forEach((player, odr) => {
      const costumeId = Number(player.customCostume ?? 0)
      if (costumeId !== 9 && costumeId !== 10 && costumeId !== 11) return
      const typedCostumeId = costumeId as LegacyCostumeId
      const state = this.costumeAnimationStates[odr] ?? {
        action: this.reachedOdr.has(odr) ? 'reach' : 'default',
        returnAction: this.reachedOdr.has(odr) ? 'reach' : 'default',
        frame: 0,
        oneShot: false,
      }
      state.frame++
      if (state.frame >= LEGACY_COSTUME_FRAME_COUNTS[typedCostumeId][state.action]) {
        state.action = state.oneShot ? state.returnAction : state.action
        state.frame = 0
        state.oneShot = false
      }
      this.costumeAnimationStates[odr] = state
      changed = true
    })
    if (changed) this.updatePlayerTexts(this.players)
  }

  private majakTitleKey(code?: number) {
    if (!code) return ''
    return `hud_majak_title_${code}`
  }

  private trickTitleKey(code?: number) {
    if (!code) return ''
    return `hud_trick_title_${code}`
  }

  private majakTitleUrl(code?: number) {
    if (!code) return ''
    const prefix = code < 1000 ? 'mj_title' : 'mj_ctitle'
    const value = code < 1000 ? code : code - 1000
    return `${IMG}/${prefix}_${String(value).padStart(3, '0')}.png`
  }

  private trickTitleUrl(code?: number) {
    if (!code) return ''
    return `${IMG}/mj_skill_${String(code).padStart(3, '0')}.png`
  }

  private sanitizeKey(value: string) {
    return value.replace(/[^a-z0-9_]/gi, '_').slice(-80)
  }

  private setDynamicImage(sprite: Phaser.GameObjects.Image, key: string, url: string, x: number, y: number, depth: number, fallbackKey?: string, visible = true, displaySize?: { width: number; height: number }, fit: 'stretch' | 'contain' | 'cover' = 'stretch') {
    sprite.setPosition(x, y).setDepth(depth)
    const dynamicSize = displaySize ?? (this.avatarSprites.includes(sprite) ? HUD_METRICS.avatar : null)
    const showTexture = (textureKey: string) => {
      if (dynamicSize) {
        this.textures.get(textureKey).setFilter(Phaser.Textures.FilterMode.LINEAR)
      }
      sprite.setTexture(textureKey)
      sprite.setCrop()
      if (dynamicSize && fit === 'cover') {
        const source = this.textures.get(textureKey).getSourceImage() as { width?: number; height?: number }
        const sourceWidth = Number(source.width ?? 0)
        const sourceHeight = Number(source.height ?? 0)
        const targetAspect = dynamicSize.width / dynamicSize.height
        const sourceAspect = sourceWidth > 0 && sourceHeight > 0 ? sourceWidth / sourceHeight : targetAspect
        let cropX = 0
        let cropY = 0
        let cropWidth = sourceWidth
        let cropHeight = sourceHeight
        if (sourceAspect > targetAspect) {
          cropWidth = sourceHeight * targetAspect
          cropX = (sourceWidth - cropWidth) / 2
        } else if (sourceAspect < targetAspect) {
          cropHeight = sourceWidth / targetAspect
          cropY = (sourceHeight - cropHeight) / 2
        }
        sprite
          .setCrop(cropX, cropY, cropWidth, cropHeight)
          .setPosition(x, y)
          .setDisplaySize(dynamicSize.width, dynamicSize.height)
      } else if (dynamicSize && fit === 'contain') {
        const source = this.textures.get(textureKey).getSourceImage() as { width?: number; height?: number }
        const sourceWidth = Number(source.width ?? 0)
        const sourceHeight = Number(source.height ?? 0)
        const scale = sourceWidth > 0 && sourceHeight > 0
          ? Math.min(dynamicSize.width / sourceWidth, dynamicSize.height / sourceHeight)
          : 1
        const width = sourceWidth > 0 ? sourceWidth * scale : dynamicSize.width
        const height = sourceHeight > 0 ? sourceHeight * scale : dynamicSize.height
        sprite
          .setPosition(x + (dynamicSize.width - width) / 2, y + (dynamicSize.height - height) / 2)
          .setDisplaySize(width, height)
      } else if (dynamicSize) {
        sprite.setPosition(x, y).setDisplaySize(dynamicSize.width, dynamicSize.height)
      }
      sprite.setVisible(visible)
    }
    sprite.setData('dynamicImageKey', key)
    if (!key || !url) {
      if (fallbackKey && this.textures.exists(fallbackKey)) {
        showTexture(fallbackKey)
        return
      }
      sprite.setVisible(false)
      return
    }
    if (this.textures.exists(key)) {
      showTexture(key)
      return
    }
    if (fallbackKey && this.textures.exists(fallbackKey)) {
      showTexture(fallbackKey)
    } else {
      sprite.setVisible(false)
    }
    this.load.image(key, url)
    this.load.once(`filecomplete-image-${key}`, () => {
      if (sprite.active && sprite.getData('dynamicImageKey') === key) showTexture(key)
    })
    this.load.start()
  }

  private updateReachTexts() {
    this.reachSprites.forEach(sprite => sprite.setVisible(false))
    for (const odr of this.reachedOdr) {
      const loc = this.odrToLoc(odr)
      const pos = isMobileIngameLayout(this.layoutMode) ? MOBILE_REACH_POSITIONS[loc] : DESKTOP_REACH_POSITIONS[loc]
      const point = isMobileIngameLayout(this.layoutMode) ? centerHudPoint(pos) : boardLocalPoint(pos)
      const key = this.reachBarKey(this.players[odr]?.richiEffect, loc)
      this.reachSprites[loc].setTexture(this.resolveSkinTextureKey(key)).setPosition(point.x, point.y).setVisible(true)
    }
  }

  private playLegacyReachDeclaration(odr: number) {
    if (document.visibilityState !== 'visible' || window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) return false
    const effect = Number(this.players[odr]?.richiEffect ?? 0)
    if (effect < 1 || effect > 3) return false
    const loc = this.odrToLoc(odr)
    this.reachSprites[loc].setVisible(false)

    if (effect === 1 || effect === 2) {
      const delays = LEGACY_REACH_FRAME_DELAYS[effect]
      const keys = effect === 1
        ? numberedLegacyKeys(`mj_ryu_richbar0${loc}`, delays.length)
        : numberedLegacyKeys(loc % 2 === 0 ? 'mj_ryu_richbar_side' : 'mj_ryu_richbar_length', delays.length)
      const positions = effect === 1
        ? [{ x: 328, y: 351 }, { x: 445, y: 110 }, { x: 152, y: 213 }, { x: 207, y: 288 }]
        : [{ x: 193, y: 235 }, { x: 330, y: 123 }, { x: 193, y: 97 }, { x: 92, y: 123 }]
      playMajakSfx(effect === 1 ? 'mjkreach01' : 'mjkreach02')
      const point = this.layoutMode === 'responsiveDesktop'
        ? seatEffectPoint(positions[loc], loc)
        : this.mobileReachAnimationPoint(loc, positions[loc])
      this.playReachFrameSequence(keys, delays, point)
      return true
    }

    const bigPositions = [{ x: 333, y: 413 }, { x: 507, y: 297 }, { x: 333, y: 275 }, { x: 269, y: 297 }]
    const spinPositions = [{ x: 333, y: 364 }, { x: 453, y: 294 }, { x: 333, y: 226 }, { x: 215, y: 294 }]
    const effectPositions = [{ x: 373, y: 397 }, { x: 488, y: 332 }, { x: 373, y: 259 }, { x: 250, y: 332 }]
    const bigKey = loc % 2 === 0 ? 'mj_GrichBar_0' : 'mj_GrichBar_1'
    const spinPoint = this.layoutMode === 'responsiveDesktop'
      ? seatEffectPoint(spinPositions[loc], loc)
      : this.mobileReachAnimationPoint(loc, spinPositions[loc])
    const bigPoint = this.layoutMode === 'responsiveDesktop'
      ? seatEffectPoint(bigPositions[loc], loc)
      : this.mobileReachAnimationPoint(loc, bigPositions[loc])
    const spin = this.add.image(spinPoint.x, spinPoint.y, 'mj_GrichBar_Spin1').setOrigin(0, 0).setDepth(Z_REACH_STICK + 1).setVisible(false)
    const big = this.add.image(bigPoint.x, bigPoint.y, bigKey).setOrigin(0, 0).setDepth(Z_REACH_STICK + 1)
    this.reachAnimationSprites.push(big, spin)
    this.time.delayedCall(300, () => {
      big.setVisible(false)
      spin.setVisible(true)
      let frame = 0
      const spinTimer = this.time.addEvent({
        delay: 6,
        repeat: 19,
        callback: () => {
          if (spin.active) spin.setTexture(frame++ % 2 === 0 ? 'mj_GrichBar_Spin1' : 'mj_GrichBar_Spin2')
        },
      })
      this.time.delayedCall(120, () => {
        spinTimer.destroy()
        spin.setVisible(false)
        big.setVisible(true)
        this.time.delayedCall(20, () => {
          big.destroy()
          spin.destroy()
          const reachKey = loc % 2 === 0 ? 'mj_richbar_0_Festa' : 'mj_richbar_1_Festa'
          const flashKey = loc % 2 === 0 ? 'mj_Grich_Effect_0' : 'mj_Grich_Effect_1'
          const reachPoint = this.layoutMode === 'responsiveDesktop'
            ? seatEffectPoint(DESKTOP_REACH_POSITIONS[loc], loc)
            : this.mobileReachAnimationPoint(loc, DESKTOP_REACH_POSITIONS[loc])
          const flashPoint = this.layoutMode === 'responsiveDesktop'
            ? seatEffectPoint(effectPositions[loc], loc)
            : this.mobileReachAnimationPoint(loc, effectPositions[loc])
          const reach = this.add.image(reachPoint.x, reachPoint.y, reachKey).setOrigin(0, 0).setDepth(Z_REACH_STICK + 1)
          const flash = this.add.image(flashPoint.x, flashPoint.y, flashKey).setOrigin(0, 0).setDepth(Z_REACH_STICK + 2)
          this.reachAnimationSprites.push(reach, flash)
          playMajakSid(SID_RICSTK)
          this.time.delayedCall(20, () => flash.destroy())
          this.time.delayedCall(320, () => {
            reach.destroy()
            this.finishReachDeclaration()
          })
        })
      })
    })
    return true
  }

  private mobileReachAnimationPoint(loc: number, point: HudPoint): HudPoint {
    if (!isMobileIngameLayout(this.layoutMode)) return point
    return mobileEffectPointFromAnchor(point, DESKTOP_REACH_POSITIONS[loc], centerHudPoint(MOBILE_REACH_POSITIONS[loc]))
  }

  private playReachFrameSequence(keys: string[], delays: readonly number[], point: HudPoint) {
    const sprite = this.add.image(point.x, point.y, keys[0]).setOrigin(0, 0).setDepth(Z_REACH_STICK + 1)
    this.reachAnimationSprites.push(sprite)
    let elapsed = 0
    for (let frame = 1; frame < keys.length; frame++) {
      elapsed += delays[frame - 1]
      this.time.delayedCall(elapsed, () => {
        if (sprite.active) sprite.setTexture(keys[frame])
      })
    }
    const duration = delays.reduce((sum, delay) => sum + delay, 0)
    this.time.delayedCall(duration, () => {
      sprite.destroy()
      this.finishReachDeclaration()
    })
  }

  private finishReachDeclaration() {
    this.reachAnimationSprites = this.reachAnimationSprites.filter(sprite => sprite.active)
    this.updateReachTexts()
  }

  private reachBarKey(richiEffect: number | undefined, loc: number) {
    const side = loc === 0 || loc === 2
    switch (richiEffect) {
      case 1: return side ? 'mj_ryu_richbar_side_b' : 'mj_ryu_richbar_length_b'
      case 2: return side ? 'mj_ryu_richbar_side_y' : 'mj_ryu_richbar_length_y'
      case 3: return side ? 'mj_richbar_0_Festa' : 'mj_richbar_1_Festa'
      default: return side ? 'mj_richbar_0' : 'mj_richbar_1'
    }
  }

  private updateKyoku(kyoku: string) {
    const windFrame = kyoku.startsWith('南') ? 1 : kyoku.startsWith('西') ? 2 : kyoku.startsWith('北') ? 3 : 0
    const match = kyoku.match(/(\d+)/)
    const kyokuFrame = Phaser.Math.Clamp((match ? Number(match[1]) : 1) - 1, 0, 3)
    this.chaFonSprite.setFrame(windFrame)
    this.kyokuNumSprite.setFrame(kyokuFrame)
  }

  private createLegacyNumber(key: string, digits: number, x: number, y: number, frameWidth: number, gap: number, depth: number): LegacyNumber {
    const startX = x - gap * (digits - 1)
    const textureKey = this.resolveSkinTextureKey(key)
    const sprites = Array.from({ length: digits }, (_, idx) => this.add.image(startX + gap * idx, y, textureKey, 0)
      .setOrigin(0, 0).setDepth(depth).setVisible(false))
    return { sprites, x, y, frameWidth, gap }
  }

  private moveLegacyNumber(num: LegacyNumber, x: number, y: number) {
    num.x = x
    num.y = y
    const startX = x - num.gap * (num.sprites.length - 1)
    num.sprites.forEach((sprite, idx) => {
      sprite.setPosition(startX + num.gap * idx, y)
    })
  }

  private setLegacyNumber(num: LegacyNumber, value: number) {
    const text = String(Math.max(0, Math.trunc(value)))
    num.sprites.forEach(sprite => sprite.setVisible(false))
    const shown = text.slice(-num.sprites.length)
    const start = num.sprites.length - shown.length
    shown.split('').forEach((digit, idx) => {
      num.sprites[start + idx]
        .setFrame(Number(digit))
        .setVisible(true)
    })
  }

  private clearRoundMarkers(preserveTurnMark = false) {
    this.reachAnimationSprites.forEach(sprite => sprite.destroy())
    this.reachAnimationSprites = []
    if (!preserveTurnMark) this.activeTurnOdr = null
    this.reachedOdr.clear()
    this.costumeAnimationStates.forEach(state => {
      if (!state) return
      state.action = 'default'
      state.returnAction = 'default'
      state.frame = 0
      state.oneShot = false
    })
    if (!preserveTurnMark) {
      this.desktopTurnStrips.forEach(strip => {
        this.tweens.killTweensOf(strip)
        strip.setData('turnActive', false).setVisible(false)
      })
      window.dispatchEvent(new CustomEvent(TURN_MARK_EVENT, { detail: { activeOdr: null, viewOdr: this.myOdr } }))
      if (this.players.length > 0) this.updatePlayerTexts(this.players)
    }
    this.updateReachTexts()
    this.clearDiceRollTimers()
    this.diceSprites.forEach(sprite => sprite.setVisible(false))
    this.waremeSprite?.setVisible(false)
    if (!preserveTurnMark) this.stopTimer()
  }

  private clearDiceRollTimers() {
    this.diceRollDelay?.destroy()
    this.diceRollDelay = undefined
    this.diceRollTimer?.destroy()
    this.diceRollTimer = undefined
  }

  private startRoundDiceRoll(finalDice: number[], finalWaremeOdr?: number, presentationDelayMs = 0) {
    this.clearDiceRollTimers()
    this.diceSprites.forEach(sprite => sprite.setVisible(false))
    this.waremeSprite?.setVisible(false)
    this.diceRollDelay = this.time.delayedCall(presentationDelayMs + DICE_ROLL_START_DELAY_MS, () => {
      let frame = 1
      this.updateDice([
        Phaser.Math.Between(0, 5),
        Phaser.Math.Between(0, 5),
      ])
      if (finalWaremeOdr !== undefined && finalWaremeOdr >= 0) this.updateWaremeLoc(Phaser.Math.Between(0, 3))
      this.diceRollTimer = this.time.addEvent({
        delay: DICE_ROLL_FRAME_MS,
        repeat: DICE_ROLL_FRAME_COUNT - 1,
        callback: () => {
          if (frame >= DICE_ROLL_FRAME_COUNT) {
            this.updateDice(finalDice)
            if (finalWaremeOdr !== undefined) this.updateWareme(finalWaremeOdr)
            this.clearDiceRollTimers()
            return
          }
          this.updateDice([
            Phaser.Math.Between(0, 5),
            Phaser.Math.Between(0, 5),
          ])
          if (finalWaremeOdr !== undefined && finalWaremeOdr >= 0) this.updateWaremeLoc(Phaser.Math.Between(0, 3))
          frame++
        },
      })
    })
  }

  private updateDice(dice: number[]) {
    this.diceSprites.forEach((sprite, idx) => {
      const frame = Phaser.Math.Clamp(Math.trunc(dice[idx] ?? 0), 0, 5)
      sprite.setFrame(frame).setVisible(true)
    })
  }

  private updateWareme(waremeOdr: number) {
    this.waremeOdr = waremeOdr
    if (waremeOdr < 0) {
      this.waremeSprite?.setVisible(false)
      return
    }
    const loc = this.odrToLoc(waremeOdr)
    this.updateWaremeLoc(loc)
  }

  private updateWaremeLoc(loc: number) {
    const pos = WAR_POS[loc]
    if (!pos) {
      this.waremeSprite?.setVisible(false)
      return
    }
    const point = centerInfoContentPoint(pos)
    if (!this.waremeSprite) {
      this.waremeSprite = this.add.image(point.x, point.y, this.resolveSkinTextureKey(pos.key)).setOrigin(0, 0).setDepth(302)
    }
    this.waremeSprite.setTexture(this.resolveSkinTextureKey(pos.key)).setPosition(point.x, point.y).setVisible(true)
  }

  private updateTimerLayout() {
    const bounds = isMobileIngameLayout(this.layoutMode) ? mobileVisibleWorldBounds() : null
    const responsiveLayout = this.layoutMode === 'responsiveDesktop' ? getIngameLayout(this.layoutMode) : null
    const gameScene = this.scene.get('GameScene') as Phaser.Scene & { getActionPanelBounds?: () => Phaser.Geom.Rectangle | null }
    const panelBounds = responsiveLayout ? gameScene.getActionPanelBounds?.() : null
    const x = responsiveLayout && panelBounds
      ? panelBounds.left + X_TIMBAR - responsiveLayout.panel.x
      : bounds ? (bounds.left + bounds.right - W_TIMBAR) / 2 + MOBILE_TIMBAR_X_SHIFT : BOARD_X + X_TIMBAR
    const y = responsiveLayout && panelBounds
      ? panelBounds.top + Y_TIMBAR - responsiveLayout.panel.y
      : bounds ? bounds.bottom - MOBILE_TIMBAR_BOTTOM_INSET : BOARD_Y + Y_TIMBAR
    this.timerBack?.setPosition(x, y)
    this.timerBar?.setPosition(x, y)
    this.timerTurnBar?.setPosition(x, y)
    this.timerKeepBar?.setPosition(x, y)
  }

  private showInactiveTimerBar() {
    if (this.layoutMode !== 'mobileLandscape' || this.isViewer) return
    this.updateTimerLayout()
    this.timerBack.setVisible(true)
    this.timerBar.setVisible(true).setDisplaySize(W_TIMBAR, H_TIMBAR).setFillStyle(0x203a8f)
    this.timerTurnBar.setVisible(false)
    this.timerKeepBar.setVisible(false)
  }

  /* ======================================================================
   * タイマー (CMJRoomWnd WM_TIMER 相当)
   * ======================================================================*/
  private startTimer(data: ActionPromptTimerData) {
    if (this.isViewer) {
      this.timerBack.setVisible(false)
      this.timerBar.setVisible(false)
      this.timerTurnBar.setVisible(false)
      this.timerKeepBar.setVisible(false)
      return
    }
    if (this.timerEvent || this.timerBack.visible || this.timerBar.visible) {
      this.stopTimer()
    } else {
      this.timerEvent = undefined
      this.timerMaxMs = 0
      this.timerEndAt = 0
      this.timerBack.setVisible(false)
      this.timerBar.setVisible(false)
      this.timerTurnBar.setVisible(false)
      this.timerKeepBar.setVisible(false)
    }
    const timeLimit = Number(data.timeLimit ?? 0)
    const limitMs = Math.max(0, Math.trunc(timeLimit))
    this.timerBaseTimeMs = Math.max(0, Number(data.baseTimeMs ?? limitMs))
    this.timerKeepTimeMs = Math.max(0, Number(data.keepTimeMs ?? 0))
    this.timerBankMs = Math.max(0, Number(data.timeBankMs ?? 0))
    this.timerBankEnabled = Boolean(data.timeBankEnabled)
    this.timerMaxMs = Math.max(1, Number(data.maxTimeMs ?? 0), this.timerBaseTimeMs + this.timerBankMs)
    this.timerEndAt = performance.now() + Math.max(1, limitMs)
    this.traceUiFlow('timer start', { ...data, limitMs })
    this.updateTimerLayout()
    this.timerBack.setVisible(true)
    const redrawTimer = () => {
      const remainMs = Math.max(0, this.timerEndAt - performance.now())
      const segments = calculateTimeBankSegments(
        remainMs,
        this.timerBaseTimeMs,
        this.timerKeepTimeMs,
        this.timerBankMs,
        this.timerBankEnabled,
      )
      const scale = W_TIMBAR / this.timerMaxMs
      const baseWidth = Math.max(0, Math.round(segments.turnMs * scale))
      const bankWidth = Math.max(0, Math.round(segments.bankMs * scale))
      const keepWidth = Math.max(0, Math.round(segments.keepMs * scale))
      const x = this.timerBack.x
      const y = this.timerBack.y
      this.timerBar.setPosition(x, y).setDisplaySize(baseWidth, H_TIMBAR)
        .setFillStyle(this.timerBankEnabled ? 0x0000ff : 0xff0000).setVisible(baseWidth > 0)
      this.timerTurnBar.setPosition(x + baseWidth, y).setDisplaySize(bankWidth, H_TIMBAR)
        .setFillStyle(this.timerBankEnabled ? 0x0080ff : 0xff8080).setVisible(bankWidth > 0)
      this.timerKeepBar.setPosition(x + baseWidth + bankWidth, y).setDisplaySize(keepWidth, H_TIMBAR)
        .setFillStyle(0x00ffff).setVisible(keepWidth > 0)
      if (remainMs <= 0) this.stopTimer()
    }
    redrawTimer()
    this.timerEvent = this.time.addEvent({
      delay: 50,
      loop: true,
      callback: redrawTimer,
    })
  }

  private stopTimer() {
    this.traceUiFlow('timer stop')
    this.timerEvent?.destroy()
    this.timerEvent = undefined
    this.timerMaxMs = 0
    this.timerEndAt = 0
    this.timerBaseTimeMs = 0
    this.timerKeepTimeMs = 0
    this.timerBankMs = 0
    this.timerBankEnabled = false
    if (this.layoutMode === 'mobileLandscape' && !this.isViewer) {
      this.showInactiveTimerBar()
    } else {
      this.timerBack.setVisible(false)
      this.timerBar.setVisible(false)
      this.timerTurnBar.setVisible(false)
      this.timerKeepBar.setVisible(false)
    }
  }
}

