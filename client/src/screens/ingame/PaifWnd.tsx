/**
 * CMJPaifWnd 相当 — 牌譜 (リプレイ) 再生画面 (AP-09 §2-11)
 * レガシー: legacy/client/HgMajak2/MJPaifWnd.h/cpp
 *
 * レガシーでは CMJGameWnd (Phaser に相当) が PANELMODE_PAIF モードで動作する。
 * Web 版では GameInstance を replay モードで起動し、PANELMODE_PAIF の
 * スプライトボタンを React オーバーレイとして配置する。
 *
 * ── CMJPaifWnd の実装 (レガシー) ────────────────────────────────────
 *   - OnPaint(): m_Screen.Draw(&dc) — Phaser 描画に相当
 *   - OnClose(): ShowWindow(SW_HIDE) — 閉じる
 *   実質 Phaser GameScene の薄いラッパー
 *
 * ── PANELMODE_PAIF 操作 UI (レガシー CMJGameWnd) ───────────────────
 *   MJWindow1.cpp: m_btnPaifu*.Create("mj_btPaifu*", X_REP*, Y_REP*)
 *   MJWindow2.cpp: PANELMODE_PAIF で各ボタン ShowWindow(SW_SHOW)
 * ─────────────────────────────────────────────────────────────────────
 */
import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import { createGame, destroyGame, GAME_HEIGHT, GAME_WIDTH } from '../../game/GameInstance'
import { getPaifuReplayPayload } from '../../api/paifu'
import GameReconnectLoading from '../../components/GameReconnectLoading'
import { GAME_LOAD_PROGRESS_EVENT, type GameLoadStep } from '../../game/gameLoadProgress'
import { useOutgameLayoutMode } from '../../hooks/useOutgameLayoutMode'
import { getDefaultAvatarUrl, getShortAvatarUrl } from '../../utils/resources'
import PaifuSaveDlg from '../outgame/dialogs/PaifuSaveDlg'
import { loadLastUsedPaifuFileName, saveLastUsedPaifuFileName } from '../../game/paifuRecording'

const PAIFU_ROTATE_EVENT = 'majak:paifu-rotate'
const PAIFU_HAND_OPEN_EVENT = 'majak:paifu-hand-open'
const PAIFU_GRAPH_EVENT = 'majak:paifu-graph'
const PAIFU_REPLAY_PACKET_EVENT = 'majak:paifu-replay-packet'
const PAIFU_REPLAY_READY_EVENT = 'majak:paifu-replay-ready'
const REPLAY_PACKET_INTERVAL_MS = 350
const ROOM_HEIGHT = 704
const MOBILE_INGAME_FOCUS_W = 794
const MOBILE_INGAME_OFFSET_Y = -180

type ReplayPacket = {
  cmd: 'playing' | 'smmc4e'
  data: Record<string, unknown>
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function formatPlayedAtJst(value: string): string {
  const normalized = value.trim()
  const utcValue = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(normalized) ? normalized : `${normalized}Z`
  const date = new Date(utcValue)
  if (Number.isNaN(date.getTime())) return value.replace('T', ' ').slice(0, 16)

  const parts = new Intl.DateTimeFormat('ja-JP', {
    timeZone: 'Asia/Tokyo',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(date)
  const values = Object.fromEntries(parts.map(part => [part.type, part.value]))
  return `${values.year}/${values.month}/${values.day} ${values.hour}:${values.minute}`
}

function extractReplayPackets(value: unknown): ReplayPacket[] {
  if (typeof value === 'string') {
    try {
      return extractReplayPackets(JSON.parse(value))
    } catch {
      return []
    }
  }
  if (Array.isArray(value)) return value.flatMap(extractReplayPackets)
  if (!isRecord(value)) return []

  const nested = value.paifu ?? value.Paifu
    ?? value.history ?? value.History
    ?? value.packets ?? value.Packets
    ?? value.events ?? value.Events
    ?? value.playHistory ?? value.PlayHistory
    ?? value.data ?? value.Data
  if (nested !== undefined && nested !== value) {
    const packets = extractReplayPackets(nested)
    if (packets.length > 0) return packets
  }

  const payload = isRecord(value.payload) ? value.payload
    : isRecord(value.Payload) ? value.Payload
      : isRecord(value.message) ? value.message
        : isRecord(value.Message) ? value.Message
          : isRecord(value.body) ? value.body
            : isRecord(value.Body) ? value.Body
              : isRecord(value.data) ? value.data
                : isRecord(value.Data) ? value.Data
                  : value
  const command = String(value.cmd ?? value.Cmd ?? value.command ?? value.Command ?? value.commandCode ?? value.CommandCode ?? value.service ?? value.Service ?? '')
  if (command === 'smmc4e' || command === 'PaiInfoList') return [{ cmd: 'smmc4e', data: payload }]
  if (command === 'playing' || command === 'GamePlay') return [{ cmd: 'playing', data: payload }]
  if (Array.isArray(payload.pai) && (payload.openPos !== undefined || payload.bInit !== undefined || payload.init !== undefined)) return [{ cmd: 'smmc4e', data: payload }]
  if (typeof payload.playType === 'string') return [{ cmd: 'playing', data: payload }]
  return []
}

function readReplayMetadata(value: unknown) {
  if (!isRecord(value)) return { roomName: '', playedAt: '', result: '', members: [] as Array<Record<string, unknown>> }
  const members = value.members ?? value.Members
  return {
    roomName: String(value.roomName ?? value.RoomName ?? ''),
    playedAt: String(value.playedAt ?? value.PlayedAt ?? ''),
    result: String(value.result ?? value.Result ?? ''),
    members: Array.isArray(members) ? members.filter(isRecord) : [],
  }
}

function readReplayMembers(packets: ReplayPacket[]): Array<Record<string, unknown>> {
  const gameStart = packets.find(packet => packet.cmd === 'playing' && packet.data.playType === 'MJPID_INIHAN')
  const memberInfo = gameStart?.data.memberInfo
  return Array.isArray(memberInfo) ? memberInfo.filter(isRecord) : []
}

/** 牌譜ソース */
export interface PaifuSource {
  /** ローカルファイル or サーバーから取得した牌譜 JSON */
  data: unknown
  /** 牌譜タイトル */
  title?: string
  /** CMJPaifu::GetComment 相当 */
  comment?: string
}

export default function PaifWnd() {
  const containerRef = useRef<HTMLDivElement>(null)
  const mobileShellRef = useRef<HTMLDivElement>(null)
  const navigate     = useNavigate()
  const location     = useLocation()
  const layoutMode = useOutgameLayoutMode()
  const isMobileIngame = layoutMode === 'mobileLandscape'
  const ingameLayoutMode = isMobileIngame ? 'mobileLandscape' : 'responsiveDesktop'
  const navState = location.state as { paifu?: PaifuSource } | null
  const initialSource = navState?.paifu
  const replayArchiveId = useMemo(() => {
    const value = Number(new URLSearchParams(location.search).get('archiveId'))
    return Number.isSafeInteger(value) && value > 0 ? value : undefined
  }, [location.search])

  /** m_btnPaifuPlay / m_btnPaifuHide のチェック状態 */
  const [isPlaying, setIsPlaying] = useState(false)
  const [handHidden, setHandHidden] = useState(true)
  const [isGraphVisible, setIsGraphVisible] = useState(false)
  const [showSaveDlg, setShowSaveDlg] = useState(false)
  const [mobileNavOpen, setMobileNavOpen] = useState(false)
  const [mobileViewOpen, setMobileViewOpen] = useState(false)
  const [packetCursor, setPacketCursor] = useState(0)
  const [replaySession, setReplaySession] = useState(0)
  const [isReplayReady, setIsReplayReady] = useState(false)
  const [isArchiveLoading, setIsArchiveLoading] = useState(!initialSource?.data)
  const [replayLoadStep, setReplayLoadStep] = useState<GameLoadStep>(initialSource?.data ? 'resources' : 'server')
  const [mobileIngameScale, setMobileIngameScale] = useState(1)
  const appliedPacketCursorRef = useRef(0)
  const replaySeedCursorRef = useRef(0)
  const nextReplayPacketAtRef = useRef(0)
  const playAfterReplayReadyRef = useRef(false)
  const autoPlayOnReplayReadyRef = useRef(true)

  const [source, setSource] = useState<PaifuSource | undefined>(initialSource)
  const hasPaifu = Boolean(source?.data)
  const replayPackets = useMemo(() => extractReplayPackets(source?.data), [source?.data])
  const replayMetadata = useMemo(() => readReplayMetadata(source?.data), [source?.data])
  const replayMembers = useMemo(() => readReplayMembers(replayPackets), [replayPackets])
  const displayedMembers = replayMembers.length > 0 ? replayMembers : replayMetadata.members
  const kyokuStarts = useMemo(
    () => replayPackets.flatMap((packet, index) => packet.cmd === 'playing' && packet.data.playType === 'MJPID_INIKYO' ? [index] : []),
    [replayPackets],
  )

  useEffect(() => {
    if (initialSource?.data) {
      setSource(initialSource)
      setIsArchiveLoading(false)
      setReplayLoadStep('resources')
      return
    }
    if (!replayArchiveId) {
      navigate('/paifu', { replace: true })
      return
    }
    let cancelled = false
    void getPaifuReplayPayload(replayArchiveId)
      .then(data => {
        if (!cancelled) {
          setSource({ data, title: String(replayArchiveId) })
          setIsArchiveLoading(false)
          setReplayLoadStep('resources')
        }
      })
      .catch(() => {
        if (!cancelled) navigate('/paifu', { replace: true })
      })
    return () => { cancelled = true }
  }, [initialSource, navigate, replayArchiveId])

  useEffect(() => {
    if (!containerRef.current || !source?.data) return
    setIsReplayReady(false)
    setReplayLoadStep('scene')
    const handleReplayReady = () => {
      setReplayLoadStep('ready')
      setIsReplayReady(true)
      if (playAfterReplayReadyRef.current || autoPlayOnReplayReadyRef.current) {
        playAfterReplayReadyRef.current = false
        autoPlayOnReplayReadyRef.current = false
        setIsPlaying(true)
      }
    }
    const handleLoadProgress = (event: Event) => {
      const step = (event as CustomEvent<{ step?: GameLoadStep }>).detail?.step
      if (step) setReplayLoadStep(step)
    }
    window.addEventListener(PAIFU_REPLAY_READY_EVENT, handleReplayReady)
    window.addEventListener(GAME_LOAD_PROGRESS_EVENT, handleLoadProgress)
    const seedCursor = replaySeedCursorRef.current
    appliedPacketCursorRef.current = seedCursor
    createGame(containerRef.current, {
      mode: 'replay',
      layoutMode: ingameLayoutMode,
      players: replayMembers,
      paifu: { packets: replayPackets.slice(0, seedCursor) },
    })
    return () => {
      window.removeEventListener(PAIFU_REPLAY_READY_EVENT, handleReplayReady)
      window.removeEventListener(GAME_LOAD_PROGRESS_EVENT, handleLoadProgress)
      destroyGame()
    }
  }, [ingameLayoutMode, isMobileIngame, replayMembers, replayPackets, replaySession, source?.data])

  useEffect(() => {
    if (!isMobileIngame) {
      setMobileIngameScale(1)
      return
    }
    const update = () => {
      const rect = mobileShellRef.current?.getBoundingClientRect()
      if (!rect) return
      const nextScale = rect.width / MOBILE_INGAME_FOCUS_W
      setMobileIngameScale(Number.isFinite(nextScale) && nextScale > 0 ? nextScale : 1)
    }
    update()
    const observer = new ResizeObserver(update)
    if (mobileShellRef.current) observer.observe(mobileShellRef.current)
    window.addEventListener('resize', update)
    return () => {
      observer.disconnect()
      window.removeEventListener('resize', update)
    }
  }, [isMobileIngame])

  useEffect(() => {
    if (!isReplayReady) return
    const appliedCursor = appliedPacketCursorRef.current
    if (packetCursor <= appliedCursor) return
    for (let index = appliedCursor; index < packetCursor; index += 1) {
      window.dispatchEvent(new CustomEvent(PAIFU_REPLAY_PACKET_EVENT, { detail: { packet: replayPackets[index] } }))
    }
    appliedPacketCursorRef.current = packetCursor
  }, [isReplayReady, packetCursor, replayPackets])

  useEffect(() => {
    if (!isPlaying || !isReplayReady) {
      nextReplayPacketAtRef.current = 0
      return
    }
    if (packetCursor >= replayPackets.length) {
      setIsPlaying(false)
      return
    }
    const now = performance.now()
    if (nextReplayPacketAtRef.current <= 0) nextReplayPacketAtRef.current = now + REPLAY_PACKET_INTERVAL_MS
    if (now - nextReplayPacketAtRef.current > REPLAY_PACKET_INTERVAL_MS) nextReplayPacketAtRef.current = now
    const delay = Math.max(0, nextReplayPacketAtRef.current - now)
    const timer = window.setTimeout(() => {
      nextReplayPacketAtRef.current += REPLAY_PACKET_INTERVAL_MS
      setPacketCursor(cursor => Math.min(cursor + 1, replayPackets.length))
    }, delay)
    return () => window.clearTimeout(timer)
  }, [isPlaying, isReplayReady, packetCursor, replayPackets.length])

  /** OnPaifuSave — CPaifuSaveDlg を開いてブラウザダウンロード */
  const handleSave = () => {
    if (!hasPaifu) return
    setShowSaveDlg(true)
  }

  const savePaifu = (fileName: string, bKyoku: boolean, comment: string) => {
    const paifuBody = typeof source?.data === 'string'
      ? source.data
      : JSON.stringify({ bKyoku, paifu: source?.data }, null, 2)
    const body = `<${comment}\r\n${paifuBody}`
    const blob = new Blob([body], { type: 'text/plain' })
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = fileName
    anchor.click()
    URL.revokeObjectURL(url)
    saveLastUsedPaifuFileName(fileName)
    setShowSaveDlg(false)
  }

  /** OnPaifuGrph — グラフ表示の開閉 */
  const handleGraph = () => {
    if (!hasPaifu) return
    setIsGraphVisible(visible => {
      const nextVisible = !visible
      window.dispatchEvent(new CustomEvent(PAIFU_GRAPH_EVENT, { detail: { visible: nextVisible } }))
      return nextVisible
    })
  }

  /** OnPaifuHide — 手牌表示 OPEN/HAND 切替 */
  const handleHide = () => setHandHidden(v => {
    const nextHidden = !v
    window.dispatchEvent(new CustomEvent(PAIFU_HAND_OPEN_EVENT, { detail: { open: !nextHidden } }))
    return nextHidden
  })

  /** OnRotate1 / OnRotate3 */
  const handleRotate = (delta: 1 | 3) => {
    window.dispatchEvent(new CustomEvent(PAIFU_ROTATE_EVENT, { detail: { delta } }))
  }

  const jumpToPacket = (nextCursor: number) => {
    const boundedCursor = Math.max(0, Math.min(replayPackets.length, nextCursor))
    playAfterReplayReadyRef.current = false
    autoPlayOnReplayReadyRef.current = false
    setIsPlaying(false)
    replaySeedCursorRef.current = boundedCursor
    setPacketCursor(boundedCursor)
    setReplaySession(session => session + 1)
  }

  const handlePrev = () => {
    const previous = [...kyokuStarts].reverse().find(index => index < Math.max(0, packetCursor - 1))
    jumpToPacket(previous ?? 0)
  }

  const handleBack = () => jumpToPacket(packetCursor - 1)

  const handleStep = () => {
    setIsPlaying(false)
    setPacketCursor(cursor => Math.min(replayPackets.length, cursor + 1))
  }

  const handleNext = () => {
    const next = kyokuStarts.find(index => index > packetCursor)
    jumpToPacket(next ?? replayPackets.length)
  }

  /** CMJGameWnd::OnMouseWheel — wheel navigates replay, Shift jumps by kyoku. */
  const handleWheel = (event: React.WheelEvent<HTMLDivElement>) => {
    if (!hasPaifu) return
    if (event.deltaY > 0) {
      if (event.shiftKey) {
        if (canNext) handleNext()
      } else if (canNext) {
        handleStep()
      }
    } else if (event.deltaY < 0) {
      if (event.shiftKey) {
        if (canPrev) handlePrev()
      } else if (canBack) {
        handleBack()
      }
    }
  }

  /** OnPaifuPlay */
  const handlePlay = () => {
    if (isPlaying) {
      setIsPlaying(false)
      return
    }
    if (packetCursor >= replayPackets.length) {
      jumpToPacket(0)
      playAfterReplayReadyRef.current = true
      return
    }
    setIsPlaying(true)
  }

  /** 閉じる — 牌譜一覧へ戻る */
  const handleClose = () => navigate('/paifu', { replace: true })

  const canUseNavi = hasPaifu && isReplayReady && replayPackets.length > 0
  const canPrev = canUseNavi && packetCursor > 0
  const canBack = canUseNavi && packetCursor > 0
  const canNext = canUseNavi && packetCursor < replayPackets.length
  const playDisabled = !hasPaifu || !isReplayReady || (!isPlaying && replayPackets.length === 0)
  const replayProgress = replayPackets.length > 0 ? Math.min(100, packetCursor / replayPackets.length * 100) : 0

  const replaySidebar = (
    <aside className="majak-responsive-ingame-sidebar majak-responsive-paifu__sidebar">
      <div className="majak-responsive-ingame-sidebar__status">
        <div className="majak-responsive-paifu__progress" aria-label={`進行 ${packetCursor} / ${replayPackets.length}`}>
          <div className="majak-responsive-paifu__progress-label"><span>進行</span><strong>{packetCursor} / {replayPackets.length}</strong></div>
          <div className="majak-responsive-paifu__progress-track"><div style={{ width: `${replayProgress}%` }} /></div>
        </div>
        <strong>{replayMetadata.roomName || source?.title || '牌譜'}</strong>
        {replayMetadata.playedAt && <div>{formatPlayedAtJst(replayMetadata.playedAt)}</div>}
        {replayMetadata.result && <div>{replayMetadata.result}</div>}
      </div>
      <div className="majak-responsive-ingame-sidebar__chat majak-responsive-paifu__members">
        <strong>対局者</strong>
        {displayedMembers.map((member, index) => {
          const avatarId = String(member.k7e ?? member.avatarUrl ?? member.AvatarUrl ?? member.avatarId ?? member.AvatarId ?? member.avatar ?? member.Avatar ?? '')
          const sex = String(member.k11e ?? member.sex ?? member.Sex ?? '').toLowerCase()
          const avatarSex = sex === 'f' || sex === 'female' ? 'female' : 'male'
          const avatarFallback = getDefaultAvatarUrl(avatarSex)
          const gamMoneyValue = member.gamMoney ?? member.gameMoney ?? member.GamMoney
          const ratingValue = member.k31e ?? member.rating ?? member.Rating
          const gamMoney = gamMoneyValue === undefined || gamMoneyValue === null || gamMoneyValue === '' ? undefined : Number(gamMoneyValue)
          const rating = ratingValue === undefined || ratingValue === null || ratingValue === '' ? undefined : Number(ratingValue)
          return (
            <div key={`${String(member.name ?? member.Name ?? '')}-${index}`}>
              <img src={avatarId ? getShortAvatarUrl(avatarId) : avatarFallback} alt="" draggable={false} onError={event => { event.currentTarget.src = avatarFallback }} />
              <span>{String(member.mjkk34e ?? member.k8e ?? member.nickName ?? member.nickname ?? member.name ?? member.Name ?? '-')}</span>
              <small>{String(member.k32e ?? member.slevel ?? member.title ?? member.Title ?? '')}</small>
              <small className="majak-responsive-paifu__member-gp">GP {typeof gamMoney === 'number' && Number.isFinite(gamMoney) ? gamMoney.toLocaleString() : '-'}</small>
              <em>R {typeof rating === 'number' && Number.isFinite(rating) ? rating : '-'}</em>
            </div>
          )
        })}
      </div>
      <div className="majak-responsive-ingame-sidebar__actions">
        <div className="majak-responsive-paifu__control-group">
          <button type="button" onClick={handlePrev} disabled={!canPrev}>前局</button>
          <button type="button" onClick={handleBack} disabled={!canBack}>戻る</button>
          <button type="button" onClick={handlePlay} disabled={playDisabled} className={isPlaying ? 'is-active' : undefined}>{isPlaying ? '停止' : '再生'}</button>
          <button type="button" onClick={handleStep} disabled={!canNext}>次へ</button>
          <button type="button" onClick={handleNext} disabled={!canNext}>次局</button>
        </div>
        <div className="majak-responsive-paifu__control-group majak-responsive-paifu__view-controls">
          <button type="button" onClick={handleGraph} disabled={!hasPaifu} className={isGraphVisible ? 'is-active' : undefined}>{isGraphVisible ? 'グラフを閉じる' : 'グラフ'}</button>
          <button type="button" onClick={() => handleRotate(3)}>回転</button>
          <button type="button" onClick={handleHide} className={handHidden ? 'is-active' : undefined}>{handHidden ? '手牌表示' : '手牌非表示'}</button>
          <button type="button" onClick={handleSave} disabled={!hasPaifu}>保存</button>
          <button type="button" onClick={handleClose}>閉じる</button>
        </div>
      </div>
    </aside>
  )

  const replayStage = (
    <div className="majak-inline-game-stage" style={{ position: 'relative', width: isMobileIngame ? GAME_WIDTH : '100%', height: isMobileIngame ? ROOM_HEIGHT : '100%', overflow: 'hidden', background: 'transparent' }}>
      <div ref={containerRef} style={{ position: 'absolute', left: 0, top: isMobileIngame ? -31 : 0, width: isMobileIngame ? GAME_WIDTH : '100%', height: isMobileIngame ? GAME_HEIGHT : '100%' }} />
    </div>
  )

  const desktopReplay = (
    <div className="majak-responsive-desktop-frame majak-responsive-paifu__frame">
      <div className="majak-responsive-ingame-shell">
        <div className="majak-responsive-ingame-playfield">
          <div className="majak-responsive-ingame-world">
            {replayStage}
          </div>
        </div>
        {replaySidebar}
      </div>
    </div>
  )

  return (
    <main className="majak-responsive-paifu" onWheel={handleWheel}>
      {isMobileIngame ? (
        <div ref={mobileShellRef} className="majak-mobile-ingame-shell">
          <div
            className="majak-mobile-ingame-scale"
            style={{
              left: '50%',
              width: MOBILE_INGAME_FOCUS_W,
              height: ROOM_HEIGHT,
              overflow: 'hidden',
              transform: `translate(-50%, ${MOBILE_INGAME_OFFSET_Y}px) scale(${mobileIngameScale})`,
              transformOrigin: 'top center',
            }}
          >
            {replayStage}
          </div>
          <div className={`majak-mobile-ingame-tool-drawer majak-paifu-mobile-controls majak-paifu-mobile-controls--left${mobileNavOpen ? ' is-open' : ''}`}>
            <button
              type="button"
              className="majak-mobile-ingame-tool-toggle"
              aria-label={mobileNavOpen ? '再生移動を閉じる' : '再生移動を開く'}
              aria-expanded={mobileNavOpen}
              onClick={() => setMobileNavOpen(open => !open)}
            >
              {mobileNavOpen ? '▲' : '▼'}
            </button>
            <div className="majak-mobile-ingame-action-bar">
              <button type="button" onClick={handlePrev} disabled={!canPrev}>前局</button>
              <button type="button" onClick={handleBack} disabled={!canBack}>戻る</button>
              <button type="button" onClick={handlePlay} disabled={playDisabled}>{isPlaying ? '停止' : '再生'}</button>
              <button type="button" onClick={handleStep} disabled={!canNext}>次へ</button>
              <button type="button" onClick={handleNext} disabled={!canNext}>次局</button>
            </div>
          </div>
          <div className={`majak-mobile-ingame-tool-drawer majak-paifu-mobile-controls majak-paifu-mobile-controls--right${mobileViewOpen ? ' is-open' : ''}`}>
            <button
              type="button"
              className="majak-mobile-ingame-tool-toggle"
              aria-label={mobileViewOpen ? '表示操作を閉じる' : '表示操作を開く'}
              aria-expanded={mobileViewOpen}
              onClick={() => setMobileViewOpen(open => !open)}
            >
              {mobileViewOpen ? '▲' : '▼'}
            </button>
            <div className="majak-mobile-ingame-action-bar">
              <button type="button" onClick={handleGraph} disabled={!hasPaifu} className={isGraphVisible ? 'is-active' : undefined}>{isGraphVisible ? 'グラフを閉じる' : 'グラフ'}</button>
              <button type="button" onClick={() => handleRotate(3)}>回転</button>
              <button type="button" onClick={handleHide} className={handHidden ? 'is-active' : undefined}>{handHidden ? '手牌表示' : '手牌非表示'}</button>
              <button type="button" onClick={handleSave} disabled={!hasPaifu}>保存</button>
              <button type="button" onClick={handleClose}>閉じる</button>
            </div>
          </div>
        </div>
      ) : (
        desktopReplay
      )}

      {showSaveDlg && (
        <PaifuSaveDlg
          defaultFileName={source?.title ? `${source.title}.txt` : loadLastUsedPaifuFileName()}
          initialComment={source?.comment ?? ''}
          onSave={savePaifu}
          onCancel={() => setShowSaveDlg(false)}
        />
      )}
      <GameReconnectLoading
        visible={isArchiveLoading || !isReplayReady}
        currentStep={isArchiveLoading ? 'server' : replayLoadStep}
        complete={false}
        fixed
      />
    </main>
  )
}
