/**
 * UIScene — インゲーム HUD オーバーレイ
 * CMJTblUser + CMJTblDraw 相当 (AP-09 §2-4, §2-6)
 *
 * GameScene と並走して常に最前面に描画される。
 *
 * ── 表示要素 ───────────────────────────────────────────────────────────────
 * 各プレイヤー (odr=0:自 odr=1:下家 odr=2:対面 odr=3:上家):
 *   点数/名前     : MJTblDraw2.cpp mempos[].txt/name
 *   ターン指示   : MJTblDraw::PutTurnMark の単一 mj_myTurn/mj_aiTurn
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
import { getIngameLayout, type IngameLayoutMode } from '../game/ingameLayout'
import { mobileCenterHudOffset, mobileVisibleWorldBounds } from '../game/mobileIngameViewport'
import { isTengokuBoardSkin } from '../utils/legacySkinPalette'

interface HudPoint { x: number; y: number }
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

const TURN_MARK_EVENT = 'majak:turn-mark'
const UI_FLOW_TRACE_PREFIX = '[UIFlow]'
const DEBUG_UI_FLOW = import.meta.env.VITE_DEBUG_GAME === '1'
const IMG = '/assets/images/game'
const BOARD_X = 5
const BOARD_Y = 31
const CUSTOM_DEFAULT_ID_COSTUME = 100011
const AVAILABLE_COSTUME_IDS = new Set([9, 10, 11])
const HUD_TEXT_FONT_FAMILY = "'Meiryo', 'Yu Gothic', 'Malgun Gothic', 'MS PGothic', 'MS Gothic', sans-serif"
const HUD_TEXT_RESOLUTION = typeof window === 'undefined'
  ? 1
  : Math.min(2, Math.max(1, window.devicePixelRatio || 1))
const MOBILE_HUD_TEXT_GAP = 8
const MOBILE_HUD_INFO_TOP_OFFSET = 10
const MOBILE_HUD_INFO_WIDTH = 54
const MOBILE_HUD_INFO_ROW_HEIGHT = 17
const MOBILE_HUD_NAME_WIDTH = 98
const MOBILE_HUD_NAME_GAP = 3
const MOBILE_HUD_PANEL_PADDING_X = 6
const MOBILE_HUD_PANEL_PADDING_Y = 6
const MOBILE_HUD_COMPACT_AVATAR_PADDING = 3
const MOBILE_HUD_ICON_WIDTH = 38
const MOBILE_HUD_ICON_HEIGHT = 68
const MOBILE_HUD_FULL_AVATAR_WIDTH = 46
const MOBILE_HUD_FULL_AVATAR_HEIGHT = 80
const HUD_NAME_MIN_FONT_SIZE = 8
const DESKTOP_HUD_INFO_Y_SHIFT = -24

function cssPx(value: string): number {
  const match = value.match(/\d+/)
  return match ? Number(match[0]) : 12
}

function measureHudTextWidth(text: string, fontSize: number): number {
  if (!text) return 0
  if (typeof document === 'undefined') return text.length * fontSize
  const canvas = document.createElement('canvas')
  const context = canvas.getContext('2d')
  if (!context) return text.length * fontSize
  context.font = `bold ${fontSize}px ${HUD_TEXT_FONT_FAMILY}`
  return context.measureText(text).width
}

function boardLocalPoint(point: HudPoint): HudPoint {
  return { x: BOARD_X + point.x, y: BOARD_Y + point.y }
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

const DESKTOP_PLAYER_AVATAR_SIZE = { width: 45, height: 88 } as const

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
  if (UI_LAYOUT_MODE === 'mobileLandscape') {
    const txt = boardLocalPoint(pos.txt)
    return { left: txt.x, width: MOBILE_HUD_INFO_WIDTH }
  }
  const avatar = boardLocalPoint(pos.avt)
  const name = boardLocalPoint(pos.name)
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
  ODR_BOX_POS = mode === 'mobileLandscape' ? MOBILE_ODR_BOX_POS : DESKTOP_ODR_BOX_POS
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
  return ODR_BOX_POS === MOBILE_ODR_BOX_POS ? mobileScreenCornerBoxPos(loc) : ODR_BOX_POS[loc]
}

function centerHudOffset(): HudPoint {
  return mobileCenterHudOffset(UI_LAYOUT_MODE)
}

function centerHudPoint(point: HudPoint): HudPoint {
  const base = boardLocalPoint(point)
  const offset = centerHudOffset()
  return { x: base.x + offset.x, y: base.y + offset.y }
}

const MOBILE_CENTER_INFO_CONTENT_OFFSET = { x: 0, y: -29 } as const

function centerInfoContentPoint(point: HudPoint): HudPoint {
  const adjusted = UI_LAYOUT_MODE === 'mobileLandscape'
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
const RICHI_POS = [
  { x: 343, y: 418 },
  { x: 508, y: 311 },
  { x: 343, y: 280 },
  { x: 270, y: 311 },
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

export default class UIScene extends Phaser.Scene {
  /* テキストオブジェクト */
  private nameTexts:  Phaser.GameObjects.Text[] = []
  private levelTexts: Phaser.GameObjects.Text[] = []
  private scoreTexts: Phaser.GameObjects.Text[] = []
  private rankTexts: Phaser.GameObjects.Text[] = []
  private diffTexts: Phaser.GameObjects.Text[] = []
  private mobileHudPanels: Phaser.GameObjects.Rectangle[] = []
  private mobileTurnBorders: Phaser.GameObjects.Rectangle[] = []
  private avatarSprites: Phaser.GameObjects.Image[] = []
  private majakTitleSprites: Phaser.GameObjects.Image[] = []
  private trickTitleSprites: Phaser.GameObjects.Image[] = []
  private turnMark?: Phaser.GameObjects.Image
  private hostMark?: Phaser.GameObjects.Image
  private menFonSprites: Phaser.GameObjects.Image[] = []
  private chichaSprite?: Phaser.GameObjects.Image
  private reachSprites: Phaser.GameObjects.Image[] = []
  private chaFonSprite!: Phaser.GameObjects.Image
  private kyokuNumSprite!: Phaser.GameObjects.Image
  private leftNumber!: LegacyNumber
  private riboNumber!: LegacyNumber
  private renchanNumber!: LegacyNumber
  private diceSprites: Phaser.GameObjects.Image[] = []
  private waremeSprite?: Phaser.GameObjects.Image
  private timerBack!: Phaser.GameObjects.Rectangle
  private timerBar!: Phaser.GameObjects.Rectangle
  private diceRollDelay?: Phaser.Time.TimerEvent
  private diceRollTimer?: Phaser.Time.TimerEvent
  private callSprites: Phaser.GameObjects.Image[] = []

  /* タイマー */
  private timerMaxMs = 0
  private timerEndAt = 0
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
  private mobileHudExpandedLoc: number | null = null
  private readonly reachedOdr = new Set<number>()

  constructor() {
    super({ key: 'UIScene' })
  }

  init(data: { myOdr?: number; layoutMode?: IngameLayoutMode; isViewer?: boolean; customBgId?: number; customBoardType?: number }) {
    this.myOdr = data.myOdr ?? 0
    this.layoutMode = data.layoutMode ?? 'desktop'
    this.isViewer = Boolean(data.isViewer)
    this.customBgId = Number(data.customBgId ?? 0)
    this.customBoardType = Number(data.customBoardType ?? 0)
    applyUiLayout(this.layoutMode)
  }

  create() {
    /* ── フォントスタイル ── */
    const scoreStyle: Phaser.Types.GameObjects.Text.TextStyle = {
      fontFamily: HUD_TEXT_FONT_FAMILY,
      fontSize:   HUD_METRICS.infoFontSize,
      color:      this.layoutMode === 'mobileLandscape' ? '#fff9cf' : '#ffffff',
      resolution: HUD_TEXT_RESOLUTION,
    }
    const nameStyle: Phaser.Types.GameObjects.Text.TextStyle = {
      fontFamily: HUD_TEXT_FONT_FAMILY,
      fontSize:   HUD_METRICS.nameFontSize,
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
      const avt = boardLocalPoint(pos.avt)
      const txt = boardLocalPoint(pos.txt)
      const name = boardLocalPoint(pos.name)
      const ttl = boardLocalPoint(pos.ttl)
      const trk = boardLocalPoint(pos.trk)

      this.majakTitleSprites[odr] = this.add.image(ttl.x, ttl.y, this.resolveSkinTextureKey('mj_board'))
        .setOrigin(0, 0).setDepth(2).setVisible(false)
      this.trickTitleSprites[odr] = this.add.image(trk.x, trk.y, this.resolveSkinTextureKey('mj_board'))
        .setOrigin(0, 0).setDepth(1).setVisible(false)
      this.mobileHudPanels[odr] = this.add.rectangle(0, 0, 1, 1, 0x103916, 0.78)
        .setOrigin(0, 0).setDepth(7).setVisible(false)
      this.mobileTurnBorders[odr] = this.add.rectangle(0, 0, 1, 1, 0xffffff, 0)
        .setOrigin(0, 0).setDepth(12).setVisible(false).setAlpha(0.3)
      this.tweens.add({
        targets: this.mobileTurnBorders[odr],
        alpha: 1,
        duration: 650,
        ease: 'Sine.InOut',
        yoyo: true,
        repeat: -1,
      })
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
        .setOrigin(0, 0).setDepth(302).setVisible(false)
    }

    /* PutTurnMark は画面上に 1 つだけ置く */
    this.turnMark = this.add.image(0, 0, this.resolveSkinTextureKey('mj_myTurn'))
      .setOrigin(0, 0).setDepth(1002).setVisible(false)
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
      .setOrigin(0, 0).setDepth(1001).setVisible(false)
    this.timerBar = this.add.rectangle(BOARD_X + X_TIMBAR, BOARD_Y + Y_TIMBAR, W_TIMBAR, H_TIMBAR, 0x0000ff)
      .setOrigin(0, 0).setDepth(1002).setVisible(false)
    this.updateTimerLayout()
    if (this.layoutMode === 'mobileLandscape' && !this.isViewer) this.showInactiveTimerBar()

    /* ── GameScene からのイベント受信 ── */
    const gs = this.scene.get('GameScene')

    /* ステート更新 */
    gs.events.on('stateUpdate', (data: {
      players: PlayerHudState[]
      kyoku?: string; kyokuCnt?: number; chicha?: number; oyaOrder?: number; left?: number; ribo?: number; renchan?: number; dice?: number[]; waremeOdr?: number; viewOdr?: number; roundStart?: boolean; activeTurnOdr?: number; preserveTurnMark?: boolean
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
        this.startRoundDiceRoll(data.dice, data.waremeOdr)
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

    gs.events.on('actionPromptStart', (data: { timeLimit?: number; viewOdr?: number }) => {
      if (data.viewOdr !== undefined) this.myOdr = data.viewOdr
      this.traceUiFlow('actionPromptStart event', data)
      if (Number.isFinite(data.timeLimit) && Number(data.timeLimit) > 0) this.startTimer(Number(data.timeLimit))
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
      this.updateReachTexts()
    })

    gs.events.on('callAction', (data: { odr: number; frame: number; avatarUrl: string }) => {
      this.showCallAction(data)
    })

    /* 局結果 → CMJKyoRes ダイアログへ (将来実装) */
    gs.events.on('kyoResult', (_data: Record<string, string>) => {
      this.stopTimer()
    })
  }

  update() {
    if (this.layoutMode !== 'mobileLandscape') return
    const bounds = mobileVisibleWorldBounds()
    if (!bounds) return
    const layoutKey = [bounds.left, bounds.top, bounds.right, bounds.bottom]
      .map(value => Math.round(value))
      .join(':')
    if (layoutKey === this.lastMobileHudLayoutKey) return
    this.lastMobileHudLayoutKey = layoutKey
    this.updateCenterHudLayout()
    this.updateTimerLayout()
    if (this.players.length > 0) this.updatePlayerTexts(this.players)
    if (this.activeTurnOdr !== null) this.updateTurnMarks(this.activeTurnOdr)
  }

  private resolveSkinTextureKey(key: string): string {
    const candidate = skinTextureCandidate(key)
    return this.textures.exists(candidate) ? candidate : key
  }

  private showCallAction(data: { odr: number; frame: number; avatarUrl: string }) {
    if (data.odr < 0 || data.odr >= 4) return
    const loc = this.odrToLoc(data.odr)
    const point = this.callActionPoint(loc)
    const balloon = this.add.image(point.x, point.y, this.resolveSkinTextureKey(`mj_baloon_${loc}`), data.frame)
      .setOrigin(0, 0)
      .setDepth(Z_CALL_BALLOON)
    const avatar = this.showCallAvatar(data.odr, loc, point, data.avatarUrl)
    this.callSprites.push(balloon)
    if (avatar) this.callSprites.push(avatar)
    this.time.delayedCall(1100, () => {
      balloon.destroy()
      avatar?.destroy()
      this.callSprites = this.callSprites.filter(item => item !== balloon && item !== avatar)
    })
  }

  private callActionPoint(loc: number): HudPoint {
    const point = this.layoutMode === 'mobileLandscape' ? this.mobileCallActionPoint(loc) : boardLocalPoint(CALL_POS[loc])
    if (this.layoutMode !== 'mobileLandscape') return point
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

  private showCallAvatar(_odr: number, loc: number, point: HudPoint, avatarUrl: string) {
    const offset = CALL_AVATAR_POS[loc]
    const key = this.avatarTextureKey(avatarUrl)
    const avatar = this.add.image(point.x + offset.x, point.y + offset.y, this.resolveSkinTextureKey('mj_aiAvtrW'))
      .setOrigin(0, 0)
      .setDepth(Z_CALL_AVATAR)
    this.setDynamicImage(
      avatar,
      key,
      avatarUrl,
      point.x + offset.x,
      point.y + offset.y,
      Z_CALL_AVATAR,
      'mj_aiAvtrW',
      true,
      { width: CALL_AVATAR_SIZE.w, height: CALL_AVATAR_SIZE.h },
    )
    return avatar
  }

  /* ======================================================================
   * ターンマーク更新 (CMJTblDraw::PutOdrBox 相当)
   * ====================================================================== */
  private updateTurnMarks(activeOdr: number) {
    this.activeTurnOdr = activeOdr
    const loc = this.odrToLoc(activeOdr)
    const pos = odrBoxPos(loc)?.avt
    if (!pos || !this.turnMark) return
    const baseAvt = boardLocalPoint(pos)
    const avt = this.layoutMode === 'mobileLandscape'
      ? this.mobileAvatarPoint(loc, baseAvt, this.mobileAvatarSize(true))
      : baseAvt
    const isKnownUser = Boolean(this.players[activeOdr]?.pix)
    const turnPoint = this.turnMarkPoint(loc, avt, isKnownUser)
    this.traceUiFlow('turnMark update', { activeOdr, myOdr: this.myOdr, loc, x: turnPoint.x, y: turnPoint.y, isKnownUser })
    this.turnMark
      .setTexture(this.resolveSkinTextureKey(isKnownUser ? 'mj_myTurn' : 'mj_aiTurn'))
      .setPosition(turnPoint.x, turnPoint.y)
      .setVisible(true)
    this.updateWindMarkers()
    window.dispatchEvent(new CustomEvent(TURN_MARK_EVENT, { detail: { activeOdr, viewOdr: this.myOdr } }))

    // タイマー表示は actionPromptStart/actionPromptEnd に集約する。
    if (activeOdr !== this.myOdr) {
      this.stopTimer()
    }
    if (this.players.length > 0) this.updatePlayerTexts(this.players)
  }

  private turnMarkPoint(_loc: number, avatarPoint: HudPoint, isKnownUser: boolean): HudPoint {
    if (this.layoutMode === 'mobileLandscape') {
      return {
        x: avatarPoint.x - 6,
        y: avatarPoint.y + (isKnownUser ? 44 : 30),
      }
    }
    return {
      x: avatarPoint.x - 5,
      y: avatarPoint.y + (isKnownUser ? HUD_METRICS.turnOffsetKnown : HUD_METRICS.turnOffsetUnknown),
    }
  }

  private toggleMobileHudInfo(loc: number) {
    if (this.layoutMode !== 'mobileLandscape') return
    this.mobileHudExpandedLoc = this.mobileHudExpandedLoc === loc ? null : loc
    if (this.players.length > 0) this.updatePlayerTexts(this.players)
    this.updateHostMark()
  }

  private isMobileHudInfoVisible(loc: number) {
    if (this.layoutMode !== 'mobileLandscape') return true
    if (this.mobileHudExpandedLoc === loc) return true
    return this.activeTurnOdr !== null && this.odrToLoc(this.activeTurnOdr) === loc
  }

  private mobileAvatarSize(infoVisible: boolean) {
    return infoVisible
      ? { width: MOBILE_HUD_FULL_AVATAR_WIDTH, height: MOBILE_HUD_FULL_AVATAR_HEIGHT }
      : { width: MOBILE_HUD_ICON_WIDTH, height: MOBILE_HUD_ICON_HEIGHT }
  }

  private desktopAvatarSize(player: PlayerHudState) {
    return player.pix ? DESKTOP_PLAYER_AVATAR_SIZE : HUD_METRICS.avatar
  }

  private mobileAvatarPoint(loc: number, fallback: HudPoint, size: { width: number; height: number }): HudPoint {
    if (this.layoutMode !== 'mobileLandscape') return fallback
    const bounds = mobileVisibleWorldBounds()
    if (!bounds) return fallback
    const insetX = 14
    const insetTop = 8
    const insetBottom = 14
    const isRight = loc === 1 || loc === 2
    const isBottom = loc === 0 || loc === 1
    const nameReserve = size.height > MOBILE_HUD_ICON_HEIGHT ? HUD_METRICS.nameHeight + MOBILE_HUD_NAME_GAP : 0
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
      timerRemainingMs: this.timerEndAt > 0 ? Math.max(0, Math.round(this.timerEndAt - this.time.now)) : 0,
      ...details,
    })
  }

  private fitNameText(_loc: number, x: number, text: string) {
    const baseWidth = this.layoutMode === 'mobileLandscape' ? MOBILE_HUD_NAME_WIDTH : HUD_METRICS.nameWidth
    let fontSize = cssPx(HUD_METRICS.nameFontSize)
    let measuredWidth = measureHudTextWidth(text, fontSize)
    if (this.layoutMode !== 'mobileLandscape') {
      return { x, width: baseWidth, fontSize: `${fontSize}px` }
    }
    while (measuredWidth > baseWidth - 4 && fontSize > HUD_NAME_MIN_FONT_SIZE) {
      fontSize -= 1
      measuredWidth = measureHudTextWidth(text, fontSize)
    }
    return {
      x,
      width: baseWidth,
      fontSize: `${fontSize}px`,
    }
  }

  private mobileHudPanelStyle() {
    const tengoku = isTengokuBoardSkin(this.customBgId, this.customBoardType)
    return tengoku
      ? { fill: 0x10283b, fillAlpha: 0.9, stroke: 0x58d3ff, strokeAlpha: 0.5, activeStroke: 0xa8ecff }
      : { fill: 0x103916, fillAlpha: 0.9, stroke: 0x6aa35f, strokeAlpha: 0.42, activeStroke: 0xc8f5ae }
  }

  private updateMobileHudPanel(loc: number, avt: HudPoint, avatarSize: { width: number; height: number }, nameX: number, nameY: number, nameWidth: number, textLeft: number, textY: number, textWidth: number, infoRows: number, infoRowHeight: number) {
    const panel = this.mobileHudPanels[loc]
    const turnBorder = this.mobileTurnBorders[loc]
    if (!panel || !turnBorder) return
    if (this.layoutMode !== 'mobileLandscape') {
      panel.setVisible(false)
      turnBorder.setVisible(false)
      return
    }
    const style = this.mobileHudPanelStyle()
    if (!this.isMobileHudInfoVisible(loc)) {
      const padding = MOBILE_HUD_COMPACT_AVATAR_PADDING
      panel
        .setPosition(avt.x - padding, avt.y - padding)
        .setSize(avatarSize.width + padding * 2, avatarSize.height + padding * 2)
        .setFillStyle(style.fill, style.fillAlpha)
        .setStrokeStyle(1, style.stroke, style.strokeAlpha)
        .setVisible(true)
      turnBorder.setVisible(false)
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
    turnBorder
      .setPosition(left, top)
      .setSize(right - left, bottom - top)
      .setStrokeStyle(2, style.activeStroke, 1)
      .setVisible(this.activeTurnOdr !== null && this.odrToLoc(this.activeTurnOdr) === loc)
  }

  private updatePlayerTexts(players: PlayerHudState[]) {
    this.players = players
    players.forEach((p, odr) => {
      const loc = this.odrToLoc(odr)
      const pos = odrBoxPos(loc)
      const baseAvt = boardLocalPoint(pos.avt)
      const txt = boardLocalPoint(pos.txt)
      const name = boardLocalPoint(pos.name)
      const ttl = boardLocalPoint(pos.ttl)
      const trk = boardLocalPoint(pos.trk)
      const mobileInfoVisible = this.isMobileHudInfoVisible(loc)
      const avatarSize = this.layoutMode === 'mobileLandscape' ? this.mobileAvatarSize(mobileInfoVisible) : this.desktopAvatarSize(p)
      const avt = this.mobileAvatarPoint(loc, baseAvt, avatarSize)
      const mobileTextLeft = loc === 1 || loc === 2 ? avt.x - MOBILE_HUD_INFO_WIDTH - MOBILE_HUD_TEXT_GAP : avt.x + avatarSize.width + MOBILE_HUD_TEXT_GAP
      const mobileNameX = loc === 1 || loc === 2 ? avt.x + avatarSize.width - MOBILE_HUD_NAME_WIDTH : avt.x
      const mobileNameY = avt.y + avatarSize.height + MOBILE_HUD_NAME_GAP
      const textY = this.layoutMode === 'mobileLandscape' ? avt.y + MOBILE_HUD_INFO_TOP_OFFSET : txt.y + DESKTOP_HUD_INFO_Y_SHIFT
      const textBounds = this.layoutMode === 'mobileLandscape' ? { left: mobileTextLeft, width: MOBILE_HUD_INFO_WIDTH } : avatarTextBounds(loc)
      const textAlign = this.layoutMode === 'mobileLandscape'
        ? (loc === 1 || loc === 2 ? 'right' : 'left')
        : 'center'
      const nameAlign = this.layoutMode === 'mobileLandscape' ? 'center' : textAlign
      const isComputer = !p.pix
      const displayName = p.name || p.pix || '<トントン>'
      const nameLayout = this.fitNameText(loc, this.layoutMode === 'mobileLandscape' ? mobileNameX : name.x, displayName)
      const nameY = this.layoutMode === 'mobileLandscape' ? mobileNameY : name.y
      this.nameTexts[loc].setColor(isComputer ? '#ff6060' : '#ffffff').setFontSize(nameLayout.fontSize).setPosition(nameLayout.x, nameY).setFixedSize(nameLayout.width, HUD_METRICS.nameHeight).setAlign(nameAlign).setText(displayName).setVisible(mobileInfoVisible)
      const levelText = p.level || (isComputer ? '----' : '')
      const compactInfo = this.layoutMode === 'mobileLandscape' && levelText.trim() === ''
      const infoRowHeight = this.layoutMode === 'mobileLandscape' ? MOBILE_HUD_INFO_ROW_HEIGHT : 15
      this.levelTexts[loc].setPosition(textBounds.left, textY).setFixedSize(textBounds.width, infoRowHeight).setAlign(textAlign).setText(levelText).setVisible(mobileInfoVisible && !compactInfo)
      this.scoreTexts[loc].setPosition(textBounds.left, textY + (compactInfo ? 0 : infoRowHeight)).setFixedSize(textBounds.width, infoRowHeight).setAlign(textAlign).setText(this.formatPointText(p)).setVisible(mobileInfoVisible)
      this.rankTexts[loc].setPosition(textBounds.left, textY + (compactInfo ? infoRowHeight : infoRowHeight * 2)).setFixedSize(textBounds.width, infoRowHeight).setAlign(textAlign).setText(this.formatRankText(players, odr)).setVisible(mobileInfoVisible)
      this.diffTexts[loc].setPosition(textBounds.left, textY + (compactInfo ? infoRowHeight * 2 : infoRowHeight * 3)).setFixedSize(textBounds.width, infoRowHeight).setAlign(textAlign).setText(this.formatDiffText(players, odr)).setVisible(mobileInfoVisible)
      this.updateMobileHudPanel(loc, avt, avatarSize, nameLayout.x, nameY, nameLayout.width, textBounds.left, textY, textBounds.width, compactInfo ? 3 : 4, infoRowHeight)
      const avatarUrl = this.costumeAvatarUrl(p) || p.avatarUrl || p.fallbackAvatarUrl || ''
      this.setDynamicImage(this.avatarSprites[loc], this.avatarKey(odr, p), avatarUrl, avt.x, avt.y, 10, 'mj_aiAvtrL', true, avatarSize)
      const majakTitleDepth = this.layoutMode === 'mobileLandscape' ? 9 : 2
      const trickTitleDepth = this.layoutMode === 'mobileLandscape' ? 8 : 1
      this.setDynamicImage(this.majakTitleSprites[loc], this.majakTitleKey(p.majakTitle), this.majakTitleUrl(p.majakTitle), this.layoutMode === 'mobileLandscape' ? textBounds.left : ttl.x, this.layoutMode === 'mobileLandscape' ? avt.y : ttl.y, majakTitleDepth, undefined, mobileInfoVisible)
      this.setDynamicImage(this.trickTitleSprites[loc], this.trickTitleKey(p.trickTitle), this.trickTitleUrl(p.trickTitle), this.layoutMode === 'mobileLandscape' ? textBounds.left : trk.x, this.layoutMode === 'mobileLandscape' ? avt.y - 2 : trk.y, trickTitleDepth, undefined, mobileInfoVisible)
    })
    this.updateHostMark()
  }

  private updateHostMark() {
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
    const baseAvt = boardLocalPoint(odrBoxPos(loc).avt)
    const avatarSize = this.layoutMode === 'mobileLandscape' ? this.mobileAvatarSize(true) : this.desktopAvatarSize(this.players[hostOdr])
    const avt = this.mobileAvatarPoint(loc, baseAvt, avatarSize)
    const point = this.layoutMode === 'mobileLandscape' ? { x: avt.x + 24, y: avt.y + 58 } : boardLocalPoint(odrBoxPos(loc).hst)
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
        .setVisible(true)
    }
    const chichaLoc = this.odrToLoc(this.chicha)
    const chichaPoint = this.chichaMarkerPoint(chichaLoc)
    this.chichaSprite
      ?.setTexture(this.resolveSkinTextureKey(`mj_oyahuda_${chichaLoc}`))
      .setFrame(Math.floor(this.kyokuCnt / 4))
      .setPosition(chichaPoint.x, chichaPoint.y)
      .setVisible(true)
  }

  private chichaMarkerPoint(loc: number): HudPoint {
    if (this.layoutMode !== 'mobileLandscape') return centerHudPoint(CHICHA_POS[loc])
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

  private setDynamicImage(sprite: Phaser.GameObjects.Image, key: string, url: string, x: number, y: number, depth: number, fallbackKey?: string, visible = true, displaySize?: { width: number; height: number }) {
    sprite.setPosition(x, y).setDepth(depth)
    const dynamicSize = displaySize ?? (this.avatarSprites.includes(sprite) ? HUD_METRICS.avatar : null)
    const showTexture = (textureKey: string) => {
      sprite.setTexture(textureKey)
      if (dynamicSize) sprite.setDisplaySize(dynamicSize.width, dynamicSize.height)
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
      const pos = RICHI_POS[loc]
      const point = boardLocalPoint(pos)
      const key = this.reachBarKey(this.players[odr]?.richiEffect, loc)
      this.reachSprites[loc].setTexture(this.resolveSkinTextureKey(key)).setPosition(point.x, point.y).setVisible(true)
    }
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
    if (!preserveTurnMark) this.activeTurnOdr = null
    this.reachedOdr.clear()
    if (!preserveTurnMark) {
      this.turnMark?.setVisible(false)
      window.dispatchEvent(new CustomEvent(TURN_MARK_EVENT, { detail: { activeOdr: null, viewOdr: this.myOdr } }))
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

  private startRoundDiceRoll(finalDice: number[], finalWaremeOdr?: number) {
    this.clearDiceRollTimers()
    this.diceSprites.forEach(sprite => sprite.setVisible(false))
    this.waremeSprite?.setVisible(false)
    this.diceRollDelay = this.time.delayedCall(DICE_ROLL_START_DELAY_MS, () => {
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
    const point = boardLocalPoint(pos)
    if (!this.waremeSprite) {
      this.waremeSprite = this.add.image(point.x, point.y, this.resolveSkinTextureKey(pos.key)).setOrigin(0, 0).setDepth(302)
    }
    this.waremeSprite.setTexture(this.resolveSkinTextureKey(pos.key)).setPosition(point.x, point.y).setVisible(true)
  }

  private updateTimerLayout() {
    const bounds = this.layoutMode === 'mobileLandscape' ? mobileVisibleWorldBounds() : null
    const x = bounds ? (bounds.left + bounds.right - W_TIMBAR) / 2 + MOBILE_TIMBAR_X_SHIFT : BOARD_X + X_TIMBAR
    const y = bounds ? bounds.bottom - MOBILE_TIMBAR_BOTTOM_INSET : BOARD_Y + Y_TIMBAR
    this.timerBack?.setPosition(x, y)
    this.timerBar?.setPosition(x, y)
  }

  private showInactiveTimerBar() {
    if (this.layoutMode !== 'mobileLandscape' || this.isViewer) return
    this.updateTimerLayout()
    this.timerBack.setVisible(true)
    this.timerBar.setVisible(true).setDisplaySize(W_TIMBAR, H_TIMBAR).setFillStyle(0x203a8f)
  }

  /* ======================================================================
   * タイマー (CMJRoomWnd WM_TIMER 相当)
   * ======================================================================*/
  private startTimer(timeLimit: number) {
    if (this.isViewer) {
      this.timerBack.setVisible(false)
      this.timerBar.setVisible(false)
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
    }
    const limitMs = timeLimit > 1000 ? Math.trunc(timeLimit) : Math.trunc(timeLimit * 1000)
    this.traceUiFlow('timer start', { timeLimit, limitMs })
    this.timerMaxMs = Math.max(1, limitMs)
    this.timerEndAt = this.time.now + this.timerMaxMs
    this.updateTimerLayout()
    this.timerBack.setVisible(true)
    this.timerBar.setVisible(true).setDisplaySize(W_TIMBAR, H_TIMBAR).setFillStyle(0x0000ff)
    const redrawTimer = () => {
      const remainMs = Math.max(0, this.timerEndAt - this.time.now)
      const ratio = remainMs / this.timerMaxMs
      this.timerBar.setDisplaySize(Math.max(0, Math.round(W_TIMBAR * ratio)), H_TIMBAR)
      this.timerBar.setFillStyle(remainMs <= 5000 ? 0xff0000 : 0x0000ff)
      if (remainMs <= 0) this.stopTimer()
    }
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
    if (this.layoutMode === 'mobileLandscape' && !this.isViewer) {
      this.showInactiveTimerBar()
    } else {
      this.timerBack.setVisible(false)
      this.timerBar.setVisible(false)
    }
  }
}

