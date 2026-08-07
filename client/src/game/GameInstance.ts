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

/** createGame オプション */
export interface CreateGameOptions {
  /** 'replay': CMJPaifWnd 相当の牌譜再生モード */
  mode?: 'game' | 'replay'
  /** Phaser 内の配置モード。ゲーム状態処理は共通で、座標だけ切り替える。 */
  layoutMode?: IngameLayoutMode
  /** 通常対局時のルームID */
  roomId?: string
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
  /** アシスト設定 (CMJConfig::m_bChkTap/Pai/Tnp/Hor) */
  assistConfig?: Partial<GameAssistConfig>
  /** 装備中カスタム背景 (CUSTOMITEM_BOARD_*) */
  customBgId?: number
  /** 装備中カスタム背景タイプ (CUSTOM_ITEM_TYPE_BG_*) */
  customBoardType?: number
  /** 装備中カスタム牌 (CUSTOMITEM_HAI) */
  customHaiId?: number
  /** 既にルーム画面で入室済みの場合、ゲームシーン開始時の c14e 再送を抑止する */
  skipInitialRoomEnter?: boolean
  /** リプレイモード時の牌譜データ */
  paifu?: unknown
}

let gameInstance: Phaser.Game | null = null
/** リプレイモード設定 — Phaser シーンが参照できるよう module スコープで保持 */
let _gameOptions: CreateGameOptions = {}
let gameParent: HTMLElement | null = null
const warmedAssetSignatures = new Set<string>()
const assetWarmups = new Map<string, Promise<void>>()

export function getGameOptions(): CreateGameOptions { return _gameOptions }

function isClosedAudioContextError(error: unknown): boolean {
  const message = error instanceof Error ? error.message : String(error ?? '')
  return /Cannot (?:suspend|resume) a closed AudioContext/i.test(message)
}

function sameGameOptions(a: CreateGameOptions, b: CreateGameOptions): boolean {
  return a.mode === b.mode &&
    a.layoutMode === b.layoutMode &&
    a.roomId === b.roomId &&
    a.myOdr === b.myOdr &&
    a.isViewer === b.isViewer &&
    a.roomOption === b.roomOption &&
    a.inputConfig?.nSelPasKey === b.inputConfig?.nSelPasKey &&
    a.assistConfig?.bChkTap === b.assistConfig?.bChkTap &&
    a.assistConfig?.bChkPai === b.assistConfig?.bChkPai &&
    a.assistConfig?.bChkTnp === b.assistConfig?.bChkTnp &&
    a.assistConfig?.bChkHor === b.assistConfig?.bChkHor &&
    a.customBgId === b.customBgId &&
    a.customBoardType === b.customBoardType &&
    a.customHaiId === b.customHaiId &&
    a.skipInitialRoomEnter === b.skipInitialRoomEnter &&
    a.paifu === b.paifu
}

function assetSignature(options: CreateGameOptions): string {
  return JSON.stringify({
    customBgId: options.customBgId ?? 0,
    customBoardType: options.customBoardType ?? 0,
    customHaiId: options.customHaiId ?? 0,
  })
}

export function warmGameAssetCache(options: CreateGameOptions = {}): Promise<void> {
  const signature = assetSignature(options)
  if (warmedAssetSignatures.has(signature)) return Promise.resolve()
  const existing = assetWarmups.get(signature)
  if (existing) return existing

  const warmup = new Promise<void>((resolve, reject) => {
    const parent = document.createElement('div')
    Object.assign(parent.style, {
      position: 'fixed',
      left: '-10000px',
      top: '-10000px',
      width: '1px',
      height: '1px',
      overflow: 'hidden',
      pointerEvents: 'none',
    })
    parent.setAttribute('aria-hidden', 'true')
    document.body.appendChild(parent)

    let warmupGame: Phaser.Game | null = null
    const cleanup = () => {
      window.setTimeout(() => {
        warmupGame?.destroy(true)
        parent.remove()
      }, 0)
    }

    try {
      warmupGame = new Phaser.Game({
        type: Phaser.CANVAS,
        width: 1,
        height: 1,
        parent,
        audio: { noAudio: true },
        render: { antialias: false, pixelArt: true },
        scene: [new PreloadScene({
          options,
          preloadOnly: true,
          onComplete: () => {
            warmedAssetSignatures.add(signature)
            cleanup()
            resolve()
          },
        })],
      })
    } catch (error) {
      cleanup()
      reject(error)
    }
  }).finally(() => {
    assetWarmups.delete(signature)
  })

  assetWarmups.set(signature, warmup)
  return warmup
}

export function createGame(parent: HTMLElement, options: CreateGameOptions = {}): Phaser.Game {
  if (gameInstance && (gameParent !== parent || !sameGameOptions(_gameOptions, options))) {
    destroyGame()
  }

  _gameOptions = options
  if (gameInstance) {
    gameInstance.registry.set(GAME_OPTIONS_REGISTRY_KEY, _gameOptions)
    return gameInstance
  }

  gameInstance = new Phaser.Game({
    type: Phaser.AUTO,
    width: GAME_WIDTH,
    height: GAME_HEIGHT,
    parent,
    backgroundColor: '#000000',
    disableContextMenu: true,
    audio: {
      noAudio: true,
    },
    render: {
      antialias: false,
      pixelArt: true,
      roundPixels: true,
    },
    scene: [PreloadScene, GameScene, UIScene],
    scale: {
      mode: Phaser.Scale.NONE,
    },
    callbacks: {
      postBoot: game => {
        game.registry.set(GAME_OPTIONS_REGISTRY_KEY, _gameOptions)
      },
    },
  })
  gameParent = parent

  return gameInstance
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
    gameParent = null
  }
}
