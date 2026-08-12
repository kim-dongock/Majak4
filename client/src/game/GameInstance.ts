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
  /** 装備中カスタム背景 (CUSTOMITEM_BOARD_*) */
  customBgId?: number
  /** 装備中カスタム背景タイプ (CUSTOM_ITEM_TYPE_BG_*) */
  customBoardType?: number
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
let gameParent: HTMLElement | null = null

export function getGameOptions(): CreateGameOptions { return _gameOptions }

function isClosedAudioContextError(error: unknown): boolean {
  const message = error instanceof Error ? error.message : String(error ?? '')
  return /Cannot (?:suspend|resume) a closed AudioContext/i.test(message)
}

function sameGameOptions(a: CreateGameOptions, b: CreateGameOptions): boolean {
  return a.mode === b.mode &&
    a.layoutMode === b.layoutMode &&
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
    a.customBgId === b.customBgId &&
    a.customBoardType === b.customBoardType &&
    a.customHaiId === b.customHaiId &&
    a.gemGame === b.gemGame &&
    a.skipInitialRoomEnter === b.skipInitialRoomEnter &&
    a.requestInitialGameResync === b.requestInitialGameResync &&
    a.paifu === b.paifu
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
    backgroundColor: options.layoutMode === 'mobileLandscape' ? 'rgba(0,0,0,0)' : '#000000',
    transparent: options.layoutMode === 'mobileLandscape',
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
