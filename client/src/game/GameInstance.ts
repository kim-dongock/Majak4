/**
 * Phaser ゲームインスタンスを保持するシングルトン
 * CMJGameWnd / CMJTblGame 相当の処理を Phaser シーンで実装する
 */
import Phaser from 'phaser'
import PreloadScene from '../scenes/PreloadScene'
import GameScene from '../scenes/GameScene'
import UIScene from '../scenes/UIScene'
import type { GameAssistConfig } from './assistConfig'
import { INGAME_WORLD, type IngameLayoutMode } from './ingameLayout'
import { stopMajakBgm } from '../utils/majakSound'

// AP-09 §5 インゲーム解像度: 1019 × 735
export const GAME_WIDTH = INGAME_WORLD.width
export const GAME_HEIGHT = INGAME_WORLD.height
export const GAME_OPTIONS_REGISTRY_KEY = 'majak:createGameOptions'
const MOBILE_RENDER_DENSITY = 2

/** createGame オプション */
export interface CreateGameOptions {
  /** 'replay': CMJPaifWnd 相当の牌譜再生モード */
  mode?: 'game' | 'replay'
  /** Phaser 内の配置モード。ゲーム状態処理は共通で、座標だけ切り替える。 */
  layoutMode?: IngameLayoutMode
  /** 開発用 fixture の王牌スケール上書き */
  deadWallScale?: 'topHand'
  /** 通常対局時のルームID */
  roomId?: string
  /** 牌譜に保存するルーム名 */
  roomName?: string
  /** 自分の席順 */
  myOdr?: number
  /** 観戦者として表示する */
  isViewer?: boolean
  /** ルーム画面で既に取得済みのプレイヤー一覧 */
  players?: Array<Record<string, unknown>>
  /** ルームオプション */
  roomOption?: string
  /** 入力設定 (CMJConfig のキーボード関連サブセット) */
  inputConfig?: {
    nSelPasKey?: number
  }
  /** 牌譜の自動記録設定 (0=なし, 1=自分の対局, 2=観戦を含む) */
  recordPaifuMode?: number
  /** アシスト設定 (CMJConfig::m_bChkTap/Pai/Tnp/Hor) */
  assistConfig?: Partial<GameAssistConfig>
  /** 練習卓でサーバー評価の推奨打牌を表示する */
  trainingRecommendations?: boolean
  /** 装備中カスタム背景 (CUSTOMITEM_BOARD_*) */
  customBgId?: number
  /** 装備中カスタム背景タイプ (CUSTOM_ITEM_TYPE_BG_*) */
  customBoardType?: number
  /** カスタム背景未装備時に標準卓背景へ適用するテーマ色 */
  themeBoardColor?: string
  /** 操作ボタンとタイマーに適用するテーマ色 */
  themeUiColor?: string
  /** 装備中カスタム牌 (CUSTOMITEM_HAI) */
  customHaiId?: number
  /** 龍珠ゲーム開始演出 (0=なし, 1=通常, 2=大龍珠) */
  gemGame?: number
  /** 既にルーム画面で入室済みの場合、ゲームシーン開始時の c14e 再送を抑止する */
  skipInitialRoomEnter?: boolean
  /** 再接続・再読込時にサーバーの進行状態を復元する */
  requestInitialGameResync?: boolean
  /** リプレイモード時の牌譜データ */
  paifu?: unknown
}

let gameInstance: Phaser.Game | null = null
/** リプレイモード設定 — Phaser シーンが参照できるよう module スコープで保持 */
let _gameOptions: CreateGameOptions = {}
let gameHost: HTMLDivElement | null = null
let parkingHost: HTMLDivElement | null = null

export function getGameOptions(): CreateGameOptions { return _gameOptions }

function isClosedAudioContextError(error: unknown): boolean {
  const message = error instanceof Error ? error.message : String(error ?? '')
  return /Cannot (?:suspend|resume) a closed AudioContext/i.test(message)
}

function sameGameOptions(a: CreateGameOptions, b: CreateGameOptions): boolean {
  return a.mode === b.mode &&
    a.layoutMode === b.layoutMode &&
    a.deadWallScale === b.deadWallScale &&
    a.roomId === b.roomId &&
    a.roomName === b.roomName &&
    a.myOdr === b.myOdr &&
    a.isViewer === b.isViewer &&
    a.roomOption === b.roomOption &&
    a.inputConfig?.nSelPasKey === b.inputConfig?.nSelPasKey &&
    a.recordPaifuMode === b.recordPaifuMode &&
    a.assistConfig?.bChkTap === b.assistConfig?.bChkTap &&
    a.assistConfig?.bChkPai === b.assistConfig?.bChkPai &&
    a.assistConfig?.bChkTnp === b.assistConfig?.bChkTnp &&
    a.assistConfig?.bChkHor === b.assistConfig?.bChkHor &&
    a.trainingRecommendations === b.trainingRecommendations &&
    a.customBgId === b.customBgId &&
    a.customBoardType === b.customBoardType &&
    a.themeBoardColor === b.themeBoardColor &&
    a.themeUiColor === b.themeUiColor &&
    a.customHaiId === b.customHaiId &&
    a.gemGame === b.gemGame &&
    a.skipInitialRoomEnter === b.skipInitialRoomEnter &&
    a.requestInitialGameResync === b.requestInitialGameResync &&
    a.paifu === b.paifu
}

function sameResourceOptions(a: CreateGameOptions, b: CreateGameOptions): boolean {
  return a.layoutMode === b.layoutMode &&
    a.customBgId === b.customBgId &&
    a.customBoardType === b.customBoardType &&
    a.themeBoardColor === b.themeBoardColor &&
    a.themeUiColor === b.themeUiColor &&
    a.customHaiId === b.customHaiId
}

function ensureGameHost(parent: HTMLElement): HTMLDivElement {
  gameHost ??= document.createElement('div')
  gameHost.style.width = '100%'
  gameHost.style.height = '100%'
  if (gameHost.parentElement !== parent) parent.appendChild(gameHost)
  return gameHost
}

function parkGameHost(): void {
  if (!gameHost || typeof document === 'undefined') return
  parkingHost ??= document.createElement('div')
  parkingHost.hidden = true
  if (!parkingHost.parentElement) document.body.appendChild(parkingHost)
  if (gameHost.parentElement !== parkingHost) parkingHost.appendChild(gameHost)
}

function configureMobileHighDensityRenderer(game: Phaser.Game): void {
  if (game.renderer.type !== Phaser.WEBGL) return

  const logicalWidth = game.scale.width
  const logicalHeight = game.scale.height
  const backingWidth = logicalWidth * MOBILE_RENDER_DENSITY
  const backingHeight = logicalHeight * MOBILE_RENDER_DENSITY
  const canvas = game.canvas

  canvas.dataset.majakLogicalWidth = String(logicalWidth)
  canvas.dataset.majakLogicalHeight = String(logicalHeight)
  canvas.style.width = `${logicalWidth}px`
  canvas.style.height = `${logicalHeight}px`

  if (canvas.width !== backingWidth || canvas.height !== backingHeight) {
    canvas.width = backingWidth
    canvas.height = backingHeight
  }
  if (game.renderer.width !== backingWidth || game.renderer.height !== backingHeight) {
    game.renderer.resize(backingWidth, backingHeight)
  }

  const scrollX = -logicalWidth * (MOBILE_RENDER_DENSITY - 1) / 2
  const scrollY = -logicalHeight * (MOBILE_RENDER_DENSITY - 1) / 2
  const canvasRect = canvas.getBoundingClientRect()
  if (canvasRect.width > 0 && canvasRect.height > 0) {
    game.scale.displayScale.set(
      logicalWidth * MOBILE_RENDER_DENSITY / canvasRect.width,
      logicalHeight * MOBILE_RENDER_DENSITY / canvasRect.height,
    )
  }
  for (const scene of game.scene.getScenes(true)) {
    for (const camera of scene.cameras.cameras) {
      if (camera.width === backingWidth && camera.height === backingHeight &&
        camera.zoom === MOBILE_RENDER_DENSITY && camera.scrollX === scrollX && camera.scrollY === scrollY) continue
      camera.setViewport(0, 0, backingWidth, backingHeight)
      camera.setZoom(MOBILE_RENDER_DENSITY)
      camera.setScroll(scrollX, scrollY)
    }
  }
}

export function createGame(parent: HTMLElement, options: CreateGameOptions = {}): Phaser.Game {
  if (gameInstance && !sameResourceOptions(_gameOptions, options)) {
    destroyGame()
  }

  const optionsChanged = !sameGameOptions(_gameOptions, options)
  _gameOptions = options
  if (gameInstance) {
    ensureGameHost(parent)
    gameInstance.loop.wake()
    gameInstance.scale.refresh()
    gameInstance.registry.set(GAME_OPTIONS_REGISTRY_KEY, _gameOptions)
    if (optionsChanged || !gameInstance.scene.isActive('GameScene')) {
      gameInstance.scene.stop('UIScene')
      gameInstance.scene.stop('GameScene')
      gameInstance.scene.start('GameScene', _gameOptions)
    }
    return gameInstance
  }

  const resizeToParent = options.layoutMode === 'responsiveDesktop'
  const useSharpPixelRendering = options.layoutMode !== 'mobileLandscape'
  const initialWidth = resizeToParent ? Math.max(1, parent.clientWidth) : GAME_WIDTH
  const initialHeight = resizeToParent ? Math.max(1, parent.clientHeight) : GAME_HEIGHT

  const host = ensureGameHost(parent)
  gameInstance = new Phaser.Game({
    type: Phaser.AUTO,
    width: initialWidth,
    height: initialHeight,
    parent: host,
    backgroundColor: options.layoutMode === 'desktop' ? '#000000' : 'rgba(0,0,0,0)',
    transparent: options.layoutMode !== 'desktop',
    disableContextMenu: true,
    audio: {
      noAudio: true,
    },
    render: {
      antialias: !useSharpPixelRendering,
      antialiasGL: !useSharpPixelRendering,
      pixelArt: useSharpPixelRendering,
      roundPixels: true,
      clearBeforeRender: true,
    },
    scene: [PreloadScene, GameScene, UIScene],
    scale: {
      mode: resizeToParent ? Phaser.Scale.RESIZE : Phaser.Scale.NONE,
      width: initialWidth,
      height: initialHeight,
    },
    callbacks: {
      postBoot: game => {
        game.canvas.style.imageRendering = useSharpPixelRendering ? 'pixelated' : 'auto'
        game.registry.set(GAME_OPTIONS_REGISTRY_KEY, _gameOptions)
        if (options.layoutMode === 'mobileLandscape') {
          game.events.on(Phaser.Core.Events.POST_RENDER, () => configureMobileHighDensityRenderer(game))
        }
      },
    },
  })

  return gameInstance
}

export function suspendGame(): void {
  if (!gameInstance) return
  stopMajakBgm()
  gameInstance.scene.stop('UIScene')
  gameInstance.scene.stop('GameScene')
  parkGameHost()
  gameInstance.loop.sleep()
}

export function freezeGameFrame(): void {
  if (!gameInstance) return
  stopMajakBgm()
  gameInstance.loop.sleep()
}

export function destroyGame(): void {
  if (gameInstance) {
    stopMajakBgm()
    try {
      gameInstance.destroy(true)
    } catch (error) {
      if (!isClosedAudioContextError(error)) throw error
    }
    gameInstance = null
    _gameOptions = {}
    gameHost?.remove()
    parkingHost?.remove()
    gameHost = null
    parkingHost = null
  }
}
