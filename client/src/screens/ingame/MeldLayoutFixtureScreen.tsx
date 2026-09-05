import { useEffect, useRef, useState } from 'react'
import { createGame, destroyGame, GAME_HEIGHT, GAME_WIDTH } from '../../game/GameInstance'
import { useOutgameLayoutMode } from '../../hooks/useOutgameLayoutMode'
import { FEMALE_AVATARS, MALE_AVATARS } from '../../utils/resources'
import LevelupDlg from '../outgame/dialogs/LevelupDlg'
import SlideAnnounce, { ANNOUNCE_GET_MAJAKTITLE, type SlideAnnounceData } from './SlideAnnounce'

type FixtureAction = 'chi' | 'pon' | 'kan' | 'ron'
type FixtureEffect =
  | 'chiVoice' | 'ponVoice' | 'kanVoice' | 'ron' | 'tsumo'
  | 'reach1' | 'reach2' | 'reach3' | 'reachTile'
  | 'seatReveal' | 'deal' | 'discard' | 'turn' | 'timer' | 'dice'
  | 'ronLow' | 'ronMangan' | 'ronYakuman' | 'tsumoLow' | 'tsumoMangan' | 'tsumoYakuman'
  | 'skill1' | 'skill2' | 'gemNormal' | 'gemBig' | 'levelUp' | 'majakTitleAward'

const PLAYER_NAMES = ['Bottom', 'Right', 'Top', 'Left']
const PLAYER_AVATARS = [MALE_AVATARS[1], FEMALE_AVATARS[3], MALE_AVATARS[7], FEMALE_AVATARS[11]]
const MOBILE_INGAME_FOCUS_WIDTH = 794
const MOBILE_INGAME_ROOM_HEIGHT = 704
const MOBILE_INGAME_OFFSET_Y = -180
const ACTION_LABELS: Record<FixtureAction, string> = {
  chi: 'チー',
  pon: 'ポン',
  kan: 'カン',
  ron: 'ロン',
}
const EFFECT_LABELS: Record<FixtureEffect, string> = {
  chiVoice: '対象アバター吹き出し：チー', ponVoice: '対象アバター吹き出し：ポン', kanVoice: '対象アバター吹き出し：カン', ron: '対象アバター吹き出し：ロン', tsumo: '対象アバター吹き出し：ツモ',
  reach1: 'リーチ演出 1', reach2: 'リーチ演出 2', reach3: 'リーチ演出 3', reachTile: 'リーチ牌発光',
  seatReveal: '席公開', deal: '配牌', discard: '打牌',
  turn: '手番表示', timer: '持ち時間', dice: 'サイコロ',
  ronLow: 'ロン 通常', ronMangan: 'ロン 満貫', ronYakuman: 'ロン 役満',
  tsumoLow: 'ツモ 通常', tsumoMangan: 'ツモ 満貫', tsumoYakuman: 'ツモ 役満',
  skill1: '称号技 Lv1（開始・アバター周辺）', skill2: '称号技 Lv2（和了・牌周辺）', gemNormal: '龍珠 通常', gemBig: '龍珠 大',
  levelUp: '資産称号昇格（対局結果後）',
  majakTitleAward: '麻雀称号獲得（右上スライド通知）',
}

const ACT_CHI = 2
const ACT_PON = 3
const ACT_KAN = 4
const ACT_RON = 5
const ACT_RIC = 9
const ACT_TSUMO = 11
const FIXTURE_REQUIRED_TEXTURES = [
  'hai_omote', 'hai_sute', 'hai_ura_2', 'hai_open_1', 'hai_open_2', 'hai_open_3',
  'eff_roneff_11', 'eff_roneff_b_20', 'eff_roneff_c_27',
  'eff_tumoeff_12', 'eff_tumoeff_b_13', 'eff_tumoeff_c_15',
  'eff_rontumoeff_d_30', 'eff_rontumo_black', 'mj_ef_yakuman12',
  'mj_ef_horafire00_02', 'mj_ef_horafire01_02', 'mj_ef_horafire02_02',
  'mj_ef_L1fire_13', 'mj_ef_L1water_11', 'mj_ef_L1earth_12', 'mj_ef_L1wind_12',
  'mj_ef_L2fire_12', 'mj_ef_L2water_09', 'mj_ef_L2earth_10', 'mj_ef_L2wind_12',
  'mj_ryu_normal_10', 'mj_ryu_big_10',
]

const HAND_CODES = [0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x21, 0x22, 0x23, 0x31]
const MAX_MELD_COUNT = 4
const MAX_DISCARD_COUNT = 18

function hand() {
  return HAND_CODES.map((code, index) => ({
    code,
    bipaiIndex: index,
    isSelected: false,
  }))
}

function discards(seatOrder: number) {
  return Array.from({ length: MAX_DISCARD_COUNT }, (_, index) => ({
    code: HAND_CODES[(seatOrder * 3 + index) % HAND_CODES.length],
    bipaiIndex: 100 + seatOrder * MAX_DISCARD_COUNT + index,
    isReach: false,
  }))
}

function calledMeld(action: Exclude<FixtureAction, 'ron'>, seatOrder: number, meldIndex: number) {
  const tileCode = [0x11, 0x22, 0x33, 0x36][(seatOrder + meldIndex) % 4]
  const tileCount = action === 'kan' ? 4 : 3
  const calledColumn = (seatOrder + meldIndex) % 3
  if (action === 'chi') {
    const startCode = [0x11, 0x21, 0x12, 0x22][(seatOrder + meldIndex) % 4]
    return {
      action: 2,
      tiles: Array.from({ length: tileCount }, (_, column) => ({
        code: startCode + column,
        flag: column === calledColumn ? 2 : 0,
      })),
    }
  }
  return {
    action: action === 'pon' ? 3 : 4,
    tiles: Array.from({ length: tileCount }, (_, column) => ({
      code: tileCode,
      flag: column === calledColumn ? 2 : column > calledColumn ? 1 : 0,
    })),
  }
}

function mountFixture(game: Phaser.Game, action: FixtureAction, meldCounts: readonly number[]) {
  const scene = game.scene.getScene('GameScene') as (Phaser.Scene & { [key: string]: unknown }) | null
  if (!scene || !FIXTURE_REQUIRED_TEXTURES.every(texture => game.textures.exists(texture))) return false
  const players = scene['players'] as Array<Record<string, unknown>>
  if (!Array.isArray(players) || players.length !== 4) return false
  scene['chicha'] = 0
  scene['paifuGraphRound'] = {
    kyokuCnt: 0,
    left: 70,
    ribo: 0,
    renchan: 0,
    dice: [2, 4],
    waremeOdr: -1,
    roomOption: '',
    dora: [0x11],
    uraDora: [],
  }

  players.forEach((player, odr) => {
    const meldCount = action === 'ron' ? 0 : Math.min(MAX_MELD_COUNT, Math.max(0, meldCounts[odr] ?? 0))
    Object.assign(player, {
      pix: `meld-fixture-${odr}`,
      name: `${PLAYER_NAMES[odr]} ${ACTION_LABELS[action]}`,
      level: 'Fixture',
      score: 25000,
      sex: odr % 2 === 0 ? 'M' : 'F',
      avatarUrl: PLAYER_AVATARS[odr],
      fallbackAvatarUrl: PLAYER_AVATARS[odr],
      hand: hand(),
      discards: discards(odr),
      flowers: [],
      melds: action === 'ron'
        ? []
        : Array.from({ length: meldCount }, (_, meldIndex) => calledMeld(action, odr, meldIndex)),
      isReach: false,
      reachDiscardCarry: false,
    })
  })

  for (let odr = 0; odr < players.length; odr++) {
    ;(scene['redrawHand'] as (seatOrder: number) => void)(odr)
    ;(scene['redrawDiscards'] as (seatOrder: number) => void)(odr)
    ;(scene['redrawMelds'] as (seatOrder: number) => void)(odr)
  }
  ;(scene['redrawDeadWall'] as () => void)()
  const playerState = { players, viewOdr: 0 }
  scene.events.emit('stateUpdate', playerState)
  const refreshPlayerHud = () => {
    const uiScene = scene.scene.get('UIScene') as Phaser.Scene & { [key: string]: unknown }
    const updatePlayerTexts = uiScene?.['updatePlayerTexts'] as ((states: Array<Record<string, unknown>>) => void) | undefined
    if (updatePlayerTexts) {
      updatePlayerTexts.call(uiScene, players)
      return
    }
    scene.time.delayedCall(100, refreshPlayerHud)
  }
  scene.time.delayedCall(100, refreshPlayerHud)
  return true
}

function replayRonEffects(game: Phaser.Game) {
  const scene = game.scene.getScene('GameScene') as Phaser.Scene & { [key: string]: unknown }
  const showBoardEffect = scene['showBoardEffect'] as ((seatOrder: number, action: number) => void) | undefined
  if (!showBoardEffect) return
  for (let odr = 0; odr < 4; odr++) showBoardEffect.call(scene, odr, 5)
}

function playFixtureEffect(game: Phaser.Game, effect: FixtureEffect, odr: number) {
  const scene = game.scene.getScene('GameScene') as Phaser.Scene & { [key: string]: unknown }
  const players = scene['players'] as Array<Record<string, unknown>>
  if (!Array.isArray(players) || !players[odr]) return
  const invoke = (name: string, ...args: unknown[]) => {
    const method = scene[name] as ((...values: unknown[]) => unknown) | undefined
    return method?.call(scene, ...args)
  }

  const voiceActions: Partial<Record<FixtureEffect, number>> = {
    chiVoice: ACT_CHI,
    ponVoice: ACT_PON,
    kanVoice: ACT_KAN,
    ron: ACT_RON,
    tsumo: ACT_TSUMO,
  }
  const voiceAction = voiceActions[effect]
  if (voiceAction !== undefined) {
    invoke('showCallAction', odr, voiceAction)
    return
  }

  if (effect === 'reach1' || effect === 'reach2' || effect === 'reach3') {
    players[odr].richiEffect = Number(effect.slice(-1))
    invoke('playCallActionSound', odr, ACT_RIC)
    scene.events.emit('stateUpdate', { players, viewOdr: 0 })
    scene.events.emit('reach', { odr, viewOdr: 0 })
    return
  }
  if (effect === 'reachTile') {
    const discards = players[odr].discards
    if (!Array.isArray(discards) || discards.length === 0) {
      players[odr].discards = [{ code: 0x11, bipaiIndex: 200 + odr, isReach: true }]
      invoke('redrawDiscards', odr)
    }
    invoke('showReachTileEffect', odr)
    return
  }
  if (effect === 'turn') {
    scene.events.emit('turnChange', { odr, viewOdr: 0 })
    return
  }
  if (effect === 'seatReveal') {
    scene['pendingMatchStartSeatReveal'] = true
    invoke('animateMatchStartSeatReveal')
    return
  }
  if (effect === 'deal') {
    invoke('animateInitialDeal', odr, 0, () => {
      const handSprites = scene['handSprites'] as Array<Array<Phaser.GameObjects.Image>>
      handSprites?.forEach(sprites => sprites.forEach(sprite => sprite.setVisible(true)))
    })
    return
  }
  if (effect === 'discard') {
    const hand = players[odr].hand as Array<Record<string, unknown>>
    const handIndex = Math.max(0, (Array.isArray(hand) ? hand.length : 1) - 1)
    const origin = invoke('captureDiscardFlightOrigin', odr, handIndex)
    const discards = players[odr].discards
    if (!Array.isArray(discards) || discards.length === 0) {
      players[odr].discards = [{ code: 0x11, bipaiIndex: 300 + odr, isReach: false }]
      invoke('redrawDiscards', odr)
    }
    invoke('animateLatestDiscard', odr, origin)
    return
  }
  if (effect === 'timer') {
    scene.events.emit('actionPromptStart', {
      viewOdr: 0,
      timeLimit: 8000,
      baseTimeMs: 2000,
      keepTimeMs: 2000,
      timeBankMs: 4000,
      timeBankEnabled: true,
      maxTimeMs: 8000,
    })
    return
  }
  if (effect === 'dice') {
    scene.events.emit('stateUpdate', {
      players,
      viewOdr: 0,
      kyoku: '東1局',
      roundStart: true,
      dice: [2, 4],
      waremeOdr: odr,
      roundPresentationDelayMs: 0,
    })
    return
  }
  if (effect === 'skill1') {
    players.forEach((player, index) => { player.trickTitle = [4, 7, 10, 13][index] })
    invoke('playLegacyLevel1Skills', 0)
    return
  }
  if (effect === 'skill2') {
    players[odr].trickTitle = 5
    invoke('playLegacyLevel2Skill', { odr, pinType: 1, totalTen: 4000, isYakuman: false, level: 3, scoreEffectDurationMs: 450, level2SkillDurationMs: 1420 })
    return
  }
  if (effect === 'gemNormal' || effect === 'gemBig') {
    scene['gemGame'] = effect === 'gemNormal' ? 1 : 2
    invoke('playLegacyGemGame', 0)
    return
  }

  const isTsumo = effect.startsWith('tsumo')
  const isYakuman = effect.endsWith('Yakuman')
  const totalTen = isYakuman ? 8000 : effect.endsWith('Mangan') ? 4000 : 2000
  players[odr].trickTitle = 0
  if (!isTsumo) {
    const sourceOdr = (odr + 3) % players.length
    const sourceDiscards = players[sourceOdr].discards
    if (!Array.isArray(sourceDiscards) || sourceDiscards.length === 0) {
      players[sourceOdr].discards = [{ code: 0x11, bipaiIndex: 400 + sourceOdr, isReach: false }]
      invoke('redrawDiscards', sourceOdr)
    }
    const discardSprites = scene['suteSprites'] as Array<Array<Phaser.GameObjects.Image>>
    const sourceSprites = discardSprites?.[sourceOdr]
    const source = sourceSprites?.[sourceSprites.length - 1]
    if (source) {
      scene['lastRonSource'] = {
        x: source.x,
        y: source.y,
        width: source.displayWidth,
        height: source.displayHeight,
        isReach: false,
      }
    }
  }
  invoke('playKyoResultHoraEffect', {
    pinType: isTsumo ? 1 : 0,
    players: players.map((_, index) => ({ isHora: index === odr })),
    totalsByPlayer: { [String(odr)]: { totalTen } },
    yakuByPlayer: {
      [String(odr)]: isYakuman
        ? [{ name: '役満', fan: 13, isYakuman: true }]
        : [{ name: '立直', fan: 1 }],
    },
  })
}

export default function MeldLayoutFixtureScreen() {
  const containerRef = useRef<HTMLDivElement>(null)
  const mobileShellRef = useRef<HTMLElement>(null)
  const gameRef = useRef<Phaser.Game | null>(null)
  const outgameLayoutMode = useOutgameLayoutMode()
  const forcedLayout = new URLSearchParams(window.location.search).get('layout')
  const mode = forcedLayout === 'desktop'
    ? 'responsiveDesktop'
    : forcedLayout === 'mobile'
      ? 'mobileLandscape'
      : outgameLayoutMode === 'desktop' ? 'responsiveDesktop' : 'mobileLandscape'
  const [action, setAction] = useState<FixtureAction>('chi')
  const [meldCounts, setMeldCounts] = useState<[number, number, number, number]>([4, 4, 4, 4])
  const [showControls, setShowControls] = useState(false)
  const [effect, setEffect] = useState<FixtureEffect>('ron')
  const [effectOdr, setEffectOdr] = useState(0)
  const [showLevelUp, setShowLevelUp] = useState(false)
  const [titleAwardAnnouncement, setTitleAwardAnnouncement] = useState<SlideAnnounceData | null>(null)
  const actionRef = useRef<FixtureAction>(action)
  const meldCountsRef = useRef<[number, number, number, number]>(meldCounts)
  const [ready, setReady] = useState(false)
  const [chatText, setChatText] = useState('')
  const [autoPass, setAutoPass] = useState(false)
  const [autoHora, setAutoHora] = useState(false)
  const [autoTsumoGiri, setAutoTsumoGiri] = useState(false)
  const [proxyPlay, setProxyPlay] = useState(false)
  const [mobileScale, setMobileScale] = useState(1)

  actionRef.current = action
  meldCountsRef.current = meldCounts

  useEffect(() => {
    const container = containerRef.current
    if (!container) return
    setReady(false)
    let animationFrame = 0
    let game: Phaser.Game
    const initialize = () => {
      if (mountFixture(game, actionRef.current, meldCountsRef.current)) {
        setReady(true)
        return
      }
      animationFrame = requestAnimationFrame(initialize)
    }
    game = createGame(container, {
      mode: 'game',
      layoutMode: mode,
      deadWallScale: 'topHand',
      roomId: 'meld-layout-fixture',
      roomName: 'Meld layout fixture',
      myOdr: 0,
      skipInitialRoomEnter: true,
      requestInitialGameResync: false,
    })
    gameRef.current = game
    ;(window as Window & { __meldLayoutFixtureGame?: Phaser.Game }).__meldLayoutFixtureGame = game
    animationFrame = requestAnimationFrame(initialize)
    return () => {
      cancelAnimationFrame(animationFrame)
      delete (window as Window & { __meldLayoutFixtureGame?: Phaser.Game }).__meldLayoutFixtureGame
      gameRef.current = null
      destroyGame()
    }
  }, [mode])

  useEffect(() => {
    const game = gameRef.current
    if (!game) return
    setReady(false)
    let animationFrame = 0
    const updateFixture = () => {
      if (gameRef.current !== game) return
      if (mountFixture(game, action, meldCounts)) {
        setReady(true)
        return
      }
      animationFrame = requestAnimationFrame(updateFixture)
    }
    animationFrame = requestAnimationFrame(updateFixture)
    return () => cancelAnimationFrame(animationFrame)
  }, [action, meldCounts])

  const isMobile = mode === 'mobileLandscape'

  useEffect(() => {
    if (!isMobile) return
    const shell = mobileShellRef.current
    if (!shell) return
    const updateScale = () => {
      const rect = shell.getBoundingClientRect()
      const nextScale = rect.width / MOBILE_INGAME_FOCUS_WIDTH
      setMobileScale(Number.isFinite(nextScale) && nextScale > 0 ? nextScale : 1)
    }
    updateScale()
    const observer = new ResizeObserver(updateScale)
    observer.observe(shell)
    return () => observer.disconnect()
  }, [isMobile])

  const effectControls = (
    <>
      <select aria-label="効果対象" value={effectOdr} onChange={event => setEffectOdr(Number(event.target.value))}>
        {PLAYER_NAMES.map((name, index) => <option key={name} value={index}>{name}</option>)}
      </select>
      <select aria-label="ゲーム効果" value={effect} onChange={event => setEffect(event.target.value as FixtureEffect)}>
        {(Object.keys(EFFECT_LABELS) as FixtureEffect[]).map(value => <option key={value} value={value}>{EFFECT_LABELS[value]}</option>)}
      </select>
      <button type="button" onClick={() => {
        if (effect === 'levelUp') {
          setShowLevelUp(true)
          return
        }
        if (effect === 'majakTitleAward') {
          setTitleAwardAnnouncement({ type: ANNOUNCE_GET_MAJAKTITLE, code: 24, name: '称号獲得プレビュー' })
          return
        }
        if (gameRef.current) playFixtureEffect(gameRef.current, effect, effectOdr)
      }}>再生</button>
    </>
  )

  const meldCountControls = (
    <>
      {PLAYER_NAMES.map((name, odr) => (
        <label key={name} style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
          <span>{name}</span>
          <select
            aria-label={`${name} 副露数`}
            value={meldCounts[odr]}
            disabled={action === 'ron'}
            onChange={event => setMeldCounts(counts => counts.map((count, index) => index === odr ? Number(event.target.value) : count) as [number, number, number, number])}
          >
            {Array.from({ length: MAX_MELD_COUNT + 1 }, (_, count) => <option key={count} value={count}>{count}副露</option>)}
          </select>
        </label>
      ))}
    </>
  )

  if (isMobile) {
    return (
      <main ref={mobileShellRef} className="majak-mobile-ingame-shell">
        <div
          className="majak-mobile-ingame-scale"
          style={{
            left: 0,
            width: MOBILE_INGAME_FOCUS_WIDTH,
            height: MOBILE_INGAME_ROOM_HEIGHT,
            overflow: 'hidden',
            transform: `translate(0px, ${MOBILE_INGAME_OFFSET_Y}px) scale(${mobileScale})`,
            transformOrigin: 'top left',
          }}
        >
          <div className="majak-inline-game-stage" style={{ width: GAME_WIDTH, height: GAME_HEIGHT, overflow: 'hidden' }}>
            <div ref={containerRef} style={{ position: 'absolute', inset: 0, width: GAME_WIDTH, height: GAME_HEIGHT }} />
          </div>
        </div>
        <button type="button" onClick={() => setShowControls(value => !value)} style={{ position: 'absolute', zIndex: 301, top: 8, left: 8 }}>
          {showControls ? '閉じる' : '操作'}
        </button>
        {showControls && (
          <header style={{ position: 'absolute', zIndex: 300, top: 42, left: 8, right: 8, display: 'flex', flexWrap: 'wrap', gap: 6, alignItems: 'center', padding: 6, color: '#ffffff', background: 'rgba(0, 0, 0, 0.72)' }}>
            <strong>Open Meld Layout Fixture</strong>
            {(Object.keys(ACTION_LABELS) as FixtureAction[]).map(value => (
              <button key={value} type="button" onClick={() => setAction(value)} disabled={action === value}>{ACTION_LABELS[value]}</button>
            ))}
            {meldCountControls}
            {action === 'ron' && <button type="button" onClick={() => gameRef.current && replayRonEffects(gameRef.current)}>ロン 4方向再生</button>}
            {effectControls}
            <span>{ready ? 'Fixture data rendered' : 'Loading scene...'}</span>
          </header>
        )}
        <div style={{ position: 'absolute', inset: 0, width: GAME_WIDTH, height: GAME_HEIGHT, pointerEvents: 'none', zIndex: 500, overflow: 'hidden' }}>
          <SlideAnnounce data={titleAwardAnnouncement} onDone={() => setTitleAwardAnnouncement(null)} />
        </div>
        {showLevelUp && <LevelupDlg level={8} lentMoney={50_000} onClose={() => setShowLevelUp(false)} />}
      </main>
    )
  }

  return (
    <main className="majak-ingame-viewport" style={{ background: '#071713' }}>
      <section
        className="majak-responsive-desktop-frame"
        style={{
          position: 'relative',
          width: 'min(1320px, calc(100vw - 48px))',
          height: '100dvh',
          flex: '0 0 auto',
          display: 'flex',
          overflow: 'hidden',
          border: '2px solid #0b552a',
          boxSizing: 'border-box',
          boxShadow: 'inset 0 0 0 2px #063618, 0 0 0 1px rgba(84, 168, 91, 0.48)',
        }}
      >
        <div className="majak-responsive-ingame-shell">
          <div className="majak-responsive-ingame-playfield">
            <div className="majak-responsive-ingame-world" ref={containerRef} />
            <button type="button" onClick={() => setShowControls(value => !value)} style={{ position: 'absolute', zIndex: 21, top: 8, left: 8 }}>
              {showControls ? '閉じる' : '操作'}
            </button>
            {showControls && (
              <header style={{ position: 'absolute', zIndex: 20, top: 42, left: 8, right: 8, display: 'flex', flexWrap: 'wrap', gap: 8, alignItems: 'center', padding: 8, color: '#ffffff', background: 'rgba(0, 0, 0, 0.62)' }}>
                <strong>Open Meld Layout Fixture</strong>
                {(Object.keys(ACTION_LABELS) as FixtureAction[]).map(value => (
                  <button key={value} type="button" onClick={() => setAction(value)} disabled={action === value}>{ACTION_LABELS[value]}</button>
                ))}
                {meldCountControls}
                {action === 'ron' && <button type="button" onClick={() => gameRef.current && replayRonEffects(gameRef.current)}>ロン 4方向再生</button>}
                {effectControls}
                <span>{ready ? 'Fixture data rendered' : 'Loading scene...'}</span>
              </header>
            )}
            <p style={{ position: 'absolute', zIndex: 20, left: 8, bottom: 8, margin: 0, padding: '5px 8px', color: '#ffffff', background: 'rgba(0, 0, 0, 0.62)', fontSize: 14 }}>
              {action === 'ron'
                ? 'Four discard sources are shown. Replay Ron to verify all four call positions.'
                : `${ACTION_LABELS[action]} is shown at Bottom, Right, Top, and Left. Each seat uses a different called-tile column.`}
            </p>
            <div style={{ position: 'absolute', inset: 0, width: GAME_WIDTH, height: GAME_HEIGHT, pointerEvents: 'none', zIndex: 500, overflow: 'hidden' }}>
              <SlideAnnounce data={titleAwardAnnouncement} onDone={() => setTitleAwardAnnouncement(null)} />
            </div>
            {showLevelUp && <LevelupDlg level={8} lentMoney={50_000} onClose={() => setShowLevelUp(false)} />}
          </div>
          <aside className="majak-responsive-ingame-sidebar">
            <div className="majak-responsive-ingame-sidebar__status">
              <div>東1局　25000点持ち</div>
              <div>Bottom が チーしました。</div>
              <div>Right が ポンしました。</div>
              <div>Top が カンしました。</div>
            </div>
            <div className="majak-responsive-ingame-sidebar__chat">
              <div>Bottom : よろしくお願いします。</div>
              <div>Right : よろしくお願いします。</div>
            </div>
            <div className="majak-responsive-ingame-sidebar__input">
              <input value={chatText} onChange={event => setChatText(event.target.value)} maxLength={80} aria-label="チャット" />
              <button type="button" disabled={!chatText.trim()} onClick={() => setChatText('')}>送信</button>
            </div>
            <div className="majak-responsive-ingame-sidebar__actions">
              <button type="button">招待</button>
              <button type="button" className={autoPass ? 'is-active' : undefined} onClick={() => setAutoPass(value => !value)} disabled={proxyPlay}>オートパス</button>
              <button type="button" className={autoHora ? 'is-active' : undefined} onClick={() => setAutoHora(value => !value)} disabled={proxyPlay}>オート和了</button>
              <button type="button" className={autoTsumoGiri ? 'is-active' : undefined} onClick={() => setAutoTsumoGiri(value => !value)} disabled={proxyPlay}>ツモ切り</button>
              <button type="button" className={proxyPlay ? 'is-active' : undefined} onClick={() => setProxyPlay(value => !value)}>代打ち</button>
            </div>
          </aside>
        </div>
      </section>
    </main>
  )
}