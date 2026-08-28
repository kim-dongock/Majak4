import type Phaser from 'phaser'
import { createGame } from '../game/GameInstance'
import { emitSignalRTestMessage, installSignalRTestAdapter } from '../api/signalr'

interface SentMessage {
  kind: 'send' | 'invoke'
  name: string
  payload: unknown
}

interface GameSceneProbe extends Phaser.Scene {
  boardBackground?: Phaser.GameObjects.Image
  players: Array<{ hand: Array<{ bipaiIndex?: number }>; handSprites?: Phaser.GameObjects.Image[] }>
  handSprites: Phaser.GameObjects.Image[][]
  legacyEffectSprites: Phaser.GameObjects.Image[]
  selectedDiscardBipaiIndex?: number
  myOdr: number
}

const sent: SentMessage[] = []
const fixtureParams = new URLSearchParams(window.location.search)
installSignalRTestAdapter({
  send: (cmd, params) => { sent.push({ kind: 'send', name: cmd, payload: params }) },
  invoke: (method, args) => {
    sent.push({ kind: 'invoke', name: method, payload: args })
  },
})

const game = createGame(document.querySelector<HTMLElement>('#game')!, {
  mode: 'game',
  layoutMode: 'mobileLandscape',
  roomId: '901',
  myOdr: 1,
  roomName: 'Virtual training room',
  roomOption: '102002000000000',
  customBgId: Number(fixtureParams.get('bg') ?? 0),
  customBoardType: Number(fixtureParams.get('bgType') ?? 0),
  customHaiId: Number(fixtureParams.get('hai') ?? 0),
  skipInitialRoomEnter: true,
  requestInitialGameResync: false,
  players: [
    { pix: 'npc-east', name: 'NPC East', playerPos: 0 },
    { pix: 'virtual-user', name: 'Virtual User', playerPos: 1 },
    { pix: 'npc-west', name: 'NPC West', playerPos: 2 },
    { pix: 'npc-north', name: 'NPC North', playerPos: 3 },
  ],
})

function scene(): GameSceneProbe {
  return game.scene.getScene('GameScene') as GameSceneProbe
}

function textureSourceUrl(source: unknown): string {
  if (typeof source !== 'object' || source === null || !('src' in source)) return ''
  return typeof source.src === 'string' ? source.src : ''
}

function initialPai() {
  return Array.from({ length: 136 }, (_, idx) => ({
    idx,
    code: ((Math.floor(idx / 4) % 3) << 4) | ((Math.floor(idx / 4) % 9) + 1),
  }))
}

function emitFreshGameStart() {
  emitSignalRTestMessage('playing', {
    playType: 'MJPID_INIHAN',
    chicha: 0,
    players: [0, 1, 2, 3],
    memberInfo: [
      { pix: 'npc-east', name: 'NPC East' },
      { pix: 'virtual-user', name: 'Virtual User' },
      { pix: 'npc-west', name: 'NPC West' },
      { pix: 'npc-north', name: 'NPC North' },
    ],
  })
  emitSignalRTestMessage('smmc4e', {
    bInit: true,
    openPos: 1,
    pai: initialPai(),
  })
  emitSignalRTestMessage('playing', {
    playType: 'MJPID_INIKYO',
    presentationId: 1,
    kyokuCnt: 0,
    dice: [4, 1],
    leftCount: 69,
    memberPoints: [25000, 25000, 25000, 25000],
    yakitori: [false, false, false, false],
    tip: [0, 0, 0, 0],
  })
}

function tileCenter(handIndex: number) {
  const target = scene().handSprites[scene().myOdr][handIndex]
  if (!target?.active) throw new Error(`Hand sprite ${handIndex} is not active.`)
  const bounds = target.getBounds()
  const canvasBounds = game.canvas.getBoundingClientRect()
  return {
    x: canvasBounds.left + (bounds.centerX / game.scale.width) * canvasBounds.width,
    y: canvasBounds.top + (bounds.centerY / game.scale.height) * canvasBounds.height,
    bipaiIndex: scene().players[scene().myOdr].hand[handIndex]?.bipaiIndex,
  }
}

function emitOtherSeatPrompt() {
  const now = Date.now()
  emitSignalRTestMessage('playing', {
    playType: 'MJPID_ACTIONS',
    seatOrder: 0,
    playerMode: 'Turn',
    actFlags: 0,
    actions: [],
    tapCandidates: [],
    timeLimit: 2,
    actionSeq: 10,
    serverNow: now,
    deadlineAt: now + 2000,
    baseTimeMs: 2000,
    keepTimeMs: 1000,
    timeBankMs: 0,
    timeBankEnabled: false,
  })
}

function emitLocalTurnPrompt() {
  const hand = scene().players[scene().myOdr].hand
  const tapCandidates = hand.map(tile => tile.bipaiIndex).filter((idx): idx is number => idx !== undefined).reverse()
  const now = Date.now()
  emitSignalRTestMessage('playing', {
    playType: 'MJPID_ACTIONS',
    seatOrder: scene().myOdr,
    playerMode: 'Turn',
    actFlags: 1 << 6,
    actions: [{ act: 'Tap', code: 6, bipaiIndex: tapCandidates }],
    tapCandidates,
    timeLimit: 2,
    actionSeq: 11,
    serverNow: now,
    deadlineAt: now + 1200,
    baseTimeMs: 1200,
    keepTimeMs: 0,
    timeBankMs: 0,
    timeBankEnabled: true,
  })
}

window.__majakGameFixture = {
  ready: () => game.scene.isActive('GameScene'),
  emitFreshGameStart,
  emitOtherSeatPrompt,
  emitLocalTurnPrompt,
  tileCenter,
  setTimeScale: (value: number) => { scene().time.timeScale = value },
  seatRevealCount: () => scene().legacyEffectSprites.filter(sprite => sprite.active).length,
  seatRevealTextures: () => scene().legacyEffectSprites.filter(sprite => sprite.active).map(sprite => sprite.texture.key),
  visibleHandCount: () => scene().handSprites.flat().filter(sprite => sprite.active && sprite.visible).length,
  selectedBipaiIndex: () => scene().selectedDiscardBipaiIndex,
  boardTexture: () => ({
    exists: Boolean(scene().boardBackground?.active),
    key: scene().boardBackground?.texture.key ?? '',
    source: textureSourceUrl(game.textures.get('mj_board').source[0]?.source),
    scaleX: scene().boardBackground?.scaleX ?? 0,
    scaleY: scene().boardBackground?.scaleY ?? 0,
  }),
  textureExists: (key: string) => game.textures.exists(key),
  sent: () => structuredClone(sent),
}

declare global {
  interface Window {
    __majakGameFixture: {
      ready: () => boolean
      emitFreshGameStart: () => void
      emitOtherSeatPrompt: () => void
      emitLocalTurnPrompt: () => void
      tileCenter: (handIndex: number) => { x: number; y: number; bipaiIndex?: number }
      setTimeScale: (value: number) => void
      seatRevealCount: () => number
      seatRevealTextures: () => string[]
      visibleHandCount: () => number
      selectedBipaiIndex: () => number | undefined
      boardTexture: () => { exists: boolean; key: string; source: string; scaleX: number; scaleY: number }
      textureExists: (key: string) => boolean
      sent: () => SentMessage[]
    }
  }
}
