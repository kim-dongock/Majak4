/**
 * Legacy event popup dialogs missing from the new client.
 *
 * Sources:
 * - legacy/client/HgMajak2/Event200912PointDlg.h/cpp
 * - legacy/client/HgMajak2/EventCupPointDlg.h/cpp
 * - legacy/client/HgMajak2/Event201004IntroDlg.h/cpp
 * - legacy/client/HgMajak2/Event201004CloseDlg.h/cpp
 */
import { useEffect, useRef, useState } from 'react'

const IMG = '/assets/images/game'
const EVENT_IMG = `${IMG}/event`
const INTRO_SKIP_KEY = 'EventYMD'

export const EVENT_POINT_SUM_TYPE = {
  MAX: 1,
  MIX: 2,
  SERIES: 3,
} as const

export type EventPointSumType = typeof EVENT_POINT_SUM_TYPE[keyof typeof EVENT_POINT_SUM_TYPE]

export interface Event200912PointInfo {
  matchCount: number
  bestPoints: number[]
}

export interface EventCupPointInfo {
  totalPoint: number
  matchCount: number
  pointHistory: number[]
}

function todayYmd(): string {
  const now = new Date()
  return `${now.getFullYear()}${String(now.getMonth() + 1).padStart(2, '0')}${String(now.getDate()).padStart(2, '0')}`
}

export function needsToDisplayEvent201004Intro(): boolean {
  return localStorage.getItem(INTRO_SKIP_KEY) !== todayYmd()
}

function SpriteButton({
  src, frameW, frameH, x, y, onClick, title,
}: {
  src: string
  frameW: number
  frameH: number
  x: number
  y: number
  onClick: () => void
  title?: string
}) {
  const [frame, setFrame] = useState(0)

  return (
    <button
      title={title}
      onClick={onClick}
      onMouseEnter={() => setFrame(2)}
      onMouseLeave={() => setFrame(0)}
      onMouseDown={() => setFrame(3)}
      onMouseUp={() => setFrame(2)}
      style={{
        position: 'absolute', left: x, top: y,
        width: frameW, height: frameH,
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-frame * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        border: 'none', padding: 0, cursor: 'pointer', outline: 'none',
        imageRendering: 'pixelated',
      }}
    />
  )
}

function CheckSprite({
  x, y, checked, onToggle,
}: {
  x: number
  y: number
  checked: boolean
  onToggle: () => void
}) {
  const [pressed, setPressed] = useState(false)
  const frame = (pressed ? 1 : 0) | (checked ? 2 : 0)

  return (
    <div
      onClick={onToggle}
      onMouseDown={() => setPressed(true)}
      onMouseUp={() => setPressed(false)}
      onMouseLeave={() => setPressed(false)}
      style={{
        position: 'absolute', left: x, top: y,
        width: 14, height: 14,
        backgroundImage: `url(${IMG}/mj_pop_check.png)`,
        backgroundPosition: `${-frame * 14}px 0`,
        backgroundRepeat: 'no-repeat',
        imageRendering: 'pixelated', cursor: 'pointer',
      }}
    />
  )
}

function ModalFrame({
  width, height, dragHandleHeight, children,
}: {
  width: number
  height: number
  dragHandleHeight: number
  children: React.ReactNode
}) {
  const [pos, setPos] = useState({ x: 0, y: 0 })
  const dragging = useRef(false)
  const dragOffset = useRef({ x: 0, y: 0 })

  useEffect(() => {
    const onMove = (event: MouseEvent) => {
      if (!dragging.current) return
      setPos({ x: event.clientX - dragOffset.current.x, y: event.clientY - dragOffset.current.y })
    }
    const onUp = () => { dragging.current = false }
    window.addEventListener('mousemove', onMove)
    window.addEventListener('mouseup', onUp)
    return () => {
      window.removeEventListener('mousemove', onMove)
      window.removeEventListener('mouseup', onUp)
    }
  }, [])

  const onMouseDown = (event: React.MouseEvent<HTMLDivElement>) => {
    const rect = event.currentTarget.getBoundingClientRect()
    if (event.clientY - rect.top >= dragHandleHeight) return
    dragging.current = true
    dragOffset.current = { x: event.clientX - pos.x, y: event.clientY - pos.y }
    event.preventDefault()
  }

  return (
    <div style={{
      position: 'absolute', inset: 0,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'transparent', zIndex: 350,
    }}>
      <div
        onMouseDown={onMouseDown}
        style={{ position: 'relative', width, height, left: pos.x, top: pos.y, flexShrink: 0 }}
      >
        {children}
      </div>
    </div>
  )
}

function LegacyText({
  x, y, w, h, children, bold = false,
}: {
  x: number
  y: number
  w: number
  h: number
  children: React.ReactNode
  bold?: boolean
}) {
  return (
    <div style={{
      position: 'absolute', left: x, top: y, width: w, height: h,
      fontFamily: 'var(--majak-font-family-ui)',
      fontSize: bold ? 'calc(20px * var(--majak-type-scale))' : 'calc(12px * var(--majak-type-scale))',
      fontWeight: bold ? 'bold' : 'normal',
      color: '#000', lineHeight: `${h}px`, textAlign: 'center',
      whiteSpace: 'pre-line', overflow: 'hidden', pointerEvents: 'none',
    }}>
      {children}
    </div>
  )
}

function pointAt(values: number[], index: number): number {
  return values[index] ?? 0
}

/** CEventPointDlg from Event200912PointDlg.cpp. */
export function Event200912PointDlg({
  info, final = false, onClose, onGoWeb,
}: {
  info: Event200912PointInfo
  final?: boolean
  onClose: () => void
  onGoWeb: () => void
}) {
  const bestPoints = Array.from({ length: 5 }, (_, index) => pointAt(info.bestPoints, index))
  const sum = bestPoints.reduce((total, value) => total + value, 0)
  const shownCount = Math.min(info.matchCount, 5)

  return (
    <ModalFrame width={452} height={381} dragHandleHeight={50}>
      <img
        src={`${IMG}/${final ? 'mj_ive_window_final' : 'mj_ive_window_trial'}.png`}
        alt=""
        draggable={false}
        style={{ position: 'absolute', left: 0, top: 0, width: 452, height: 381 }}
      />
      <LegacyText x={146} y={65} w={48} h={20} bold>{sum}</LegacyText>
      <LegacyText x={355} y={65} w={48} h={20} bold>{info.matchCount}</LegacyText>
      {bestPoints.map((point, index) => (
        <LegacyText key={index} x={48 + index * 83} y={173} w={24} h={12}>
          {index < shownCount ? point : '-'}
        </LegacyText>
      ))}
      {info.matchCount >= 5 && (
        <LegacyText x={24} y={208} w={404} h={12}>
          {`${bestPoints[4]}を超える点数を出すと総合ポイントが更新されます。`}
        </LegacyText>
      )}
      <SpriteButton src={`${IMG}/mj_ive_btn_kochira.png`} frameW={97} frameH={26} x={175} y={287} onClick={onGoWeb} title="こちら" />
      <SpriteButton src={`${IMG}/mj_shp_btn_close.png`} frameW={88} frameH={32} x={183} y={333} onClick={onClose} title="閉じる" />
    </ModalFrame>
  )
}

/** CEventPointDlg from EventCupPointDlg.cpp. */
export function EventCupPointDlg({
  info, pointSumType, onClose, onGoWeb,
}: {
  info: EventCupPointInfo
  pointSumType: EventPointSumType
  onClose: () => void
  onGoWeb: () => void
}) {
  const background = pointSumType === EVENT_POINT_SUM_TYPE.MIX
    ? 'mj_ive_window_max_mini'
    : pointSumType === EVENT_POINT_SUM_TYPE.SERIES
      ? 'mj_ive_window_series'
      : 'mj_ive_window_trial'
  const max = pointSumType === EVENT_POINT_SUM_TYPE.MIX ? 7 : 5
  const shownCount = Math.min(info.matchCount, max)
  const points = Array.from({ length: max }, (_, index) => pointAt(info.pointHistory, index))
  const startX = pointSumType === EVENT_POINT_SUM_TYPE.MIX ? 36 : 48
  const gap = pointSumType === EVENT_POINT_SUM_TYPE.MIX ? 59 : 83

  let updateMessage: string | null = null
  if (info.matchCount >= max) {
    if (pointSumType === EVENT_POINT_SUM_TYPE.MAX) {
      updateMessage = `${points[4]}を超える点数を出すと総合ポイントが更新されます。`
    } else if (pointSumType === EVENT_POINT_SUM_TYPE.MIX) {
        updateMessage = `次の対戦で${points[4]}を超えるか${points[5]}を下回る点数が出すと\n総合ポイントが更新されます。`
    } else {
        updateMessage = '次の対戦で5戦前の点数が消え、最新の 対戦ポイントが反映されます。'
    }
  }

  return (
    <ModalFrame width={452} height={381} dragHandleHeight={50}>
      <img
        src={`${IMG}/${background}.png`}
        alt=""
        draggable={false}
        style={{ position: 'absolute', left: 0, top: 0, width: 452, height: 381 }}
      />
      <LegacyText x={146} y={65} w={48} h={20} bold>{info.totalPoint}</LegacyText>
      <LegacyText x={355} y={65} w={48} h={20} bold>{info.matchCount}</LegacyText>
      {points.map((point, index) => (
        <LegacyText key={index} x={startX + index * gap} y={173} w={24} h={12}>
          {index < shownCount ? point : '-'}
        </LegacyText>
      ))}
      {updateMessage && (
        <LegacyText x={24} y={208} w={404} h={pointSumType === EVENT_POINT_SUM_TYPE.MIX ? 28 : 12}>
          {updateMessage}
        </LegacyText>
      )}
      <SpriteButton src={`${IMG}/mj_ive_btn_kochira.png`} frameW={97} frameH={26} x={175} y={287} onClick={onGoWeb} title="こちら" />
      <SpriteButton src={`${IMG}/mj_shp_btn_close.png`} frameW={88} frameH={32} x={183} y={333} onClick={onClose} title="閉じる" />
    </ModalFrame>
  )
}

/** CEventIntroDlg from Event201004IntroDlg.cpp. */
export function Event201004IntroDlg({
  onClose, onSiteClick,
}: {
  onClose: () => void
  onSiteClick: () => void
}) {
  const [noOpenToday, setNoOpenToday] = useState(false)
  const [visible, setVisible] = useState(false)

  useEffect(() => {
    if (needsToDisplayEvent201004Intro()) {
      setVisible(true)
    } else {
      onClose()
    }
  }, [onClose])

  const handleClose = () => {
    if (noOpenToday) {
      localStorage.setItem(INTRO_SKIP_KEY, todayYmd())
    } else {
      localStorage.removeItem(INTRO_SKIP_KEY)
    }
    onClose()
  }

  if (!visible) return null

  return (
    <ModalFrame width={508} height={356} dragHandleHeight={40}>
      <img
        src={`${EVENT_IMG}/mj_gw_ive_window_01.png`}
        alt=""
        draggable={false}
        style={{ position: 'absolute', left: 0, top: 0, width: 508, height: 356 }}
      />
      <SpriteButton src={`${IMG}/mj_shp_btn_close.png`} frameW={88} frameH={32} x={210} y={285} onClick={handleClose} title="閉じる" />
      <SpriteButton src={`${EVENT_IMG}/mj_ive_btn_kochira.png`} frameW={97} frameH={26} x={231} y={234} onClick={onSiteClick} title="こちら" />
      <CheckSprite x={349} y={329} checked={noOpenToday} onToggle={() => setNoOpenToday(value => !value)} />
    </ModalFrame>
  )
}

/** CEventCloseDlg from Event201004CloseDlg.cpp. */
export function Event201004CloseDlg({
  onQuit, onContinue,
}: {
  onQuit: () => void
  onContinue: () => void
}) {
  return (
    <ModalFrame width={508} height={356} dragHandleHeight={40}>
      <img
        src={`${EVENT_IMG}/mj_gw_ive_window_02.png`}
        alt=""
        draggable={false}
        style={{ position: 'absolute', left: 0, top: 0, width: 508, height: 356 }}
      />
      <SpriteButton src={`${EVENT_IMG}/mj_ive_btn_end.png`} frameW={132} frameH={32} x={115} y={305} onClick={onQuit} title="終了" />
      <SpriteButton src={`${EVENT_IMG}/mj_ive_btn_replay.png`} frameW={132} frameH={32} x={261} y={305} onClick={onContinue} title="もう一度" />
    </ModalFrame>
  )
}