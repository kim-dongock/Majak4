import { useEffect, useState, type ReactNode } from 'react'

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

function pointAt(values: number[], index: number): number {
  return values[index] ?? 0
}

function EventDialogShell({ title, children, onClose, actions }: {
  title: string
  children: ReactNode
  onClose?: () => void
  actions?: ReactNode
}) {
  return (
    <div className="majak-popup-overlay">
      <section className="majak-popup-panel event-dialog" role="dialog" aria-modal="true" aria-label={title}>
        <header className="majak-popup-titlebar event-dialog__header">
          <h2>{title}</h2>
          {onClose && <button className="majak-popup-titlebar__close" type="button" onClick={onClose} aria-label="閉じる">×</button>}
        </header>
        <main className="majak-popup-body event-dialog__body">{children}</main>
        {actions && <footer className="majak-popup-actions event-dialog__actions">{actions}</footer>}
      </section>
    </div>
  )
}

function EventPointContent({ totalPoint, matchCount, points, notice }: {
  totalPoint: number
  matchCount: number
  points: number[]
  notice?: string
}) {
  return <>
    <section className="event-dialog__summary" aria-label="イベント集計">
      <div><span>総合ポイント</span><strong>{totalPoint}</strong></div>
      <div><span>対戦回数</span><strong>{matchCount}</strong></div>
    </section>
    <section className="event-dialog__scores" aria-label="対戦ポイント履歴">
      {points.map((point, index) => <div key={index}><span>第{index + 1}戦</span><strong>{index < matchCount ? point : '-'}</strong></div>)}
    </section>
    {notice && <p className="event-dialog__notice">{notice}</p>}
  </>
}

export function Event200912PointDlg({ info, final = false, onClose, onGoWeb }: {
  info: Event200912PointInfo
  final?: boolean
  onClose: () => void
  onGoWeb: () => void
}) {
  const points = Array.from({ length: 5 }, (_, index) => pointAt(info.bestPoints, index))
  const totalPoint = points.reduce((total, point) => total + point, 0)
  const notice = info.matchCount >= 5 ? `${points[4]}を超える点数を出すと総合ポイントが更新されます。` : undefined

  return <EventDialogShell title={final ? 'イベント最終結果' : 'イベントポイント'} onClose={onClose} actions={<><button className="event-dialog__secondary" type="button" onClick={onGoWeb}>イベント詳細</button><button type="button" onClick={onClose}>閉じる</button></>}>
    <EventPointContent totalPoint={totalPoint} matchCount={info.matchCount} points={points} notice={notice} />
  </EventDialogShell>
}

export function EventCupPointDlg({ info, pointSumType, onClose, onGoWeb }: {
  info: EventCupPointInfo
  pointSumType: EventPointSumType
  onClose: () => void
  onGoWeb: () => void
}) {
  const max = pointSumType === EVENT_POINT_SUM_TYPE.MIX ? 7 : 5
  const points = Array.from({ length: max }, (_, index) => pointAt(info.pointHistory, index))
  const notice = info.matchCount >= max
    ? pointSumType === EVENT_POINT_SUM_TYPE.MAX
      ? `${points[4]}を超える点数を出すと総合ポイントが更新されます。`
      : pointSumType === EVENT_POINT_SUM_TYPE.MIX
        ? `次の対戦で${points[4]}を超えるか${points[5]}を下回る点数が出ると、総合ポイントが更新されます。`
        : '次の対戦で5戦前の点数が消え、最新の対戦ポイントが反映されます。'
    : undefined

  return <EventDialogShell title="イベントポイント" onClose={onClose} actions={<><button className="event-dialog__secondary" type="button" onClick={onGoWeb}>イベント詳細</button><button type="button" onClick={onClose}>閉じる</button></>}>
    <EventPointContent totalPoint={info.totalPoint} matchCount={info.matchCount} points={points} notice={notice} />
  </EventDialogShell>
}

export function Event201004IntroDlg({ onClose, onSiteClick }: {
  onClose: () => void
  onSiteClick: () => void
}) {
  const [noOpenToday, setNoOpenToday] = useState(false)
  const [visible, setVisible] = useState(false)

  useEffect(() => {
    if (needsToDisplayEvent201004Intro()) setVisible(true)
    else onClose()
  }, [onClose])

  const handleClose = () => {
    if (noOpenToday) localStorage.setItem(INTRO_SKIP_KEY, todayYmd())
    else localStorage.removeItem(INTRO_SKIP_KEY)
    onClose()
  }

  if (!visible) return null

  return <EventDialogShell title="イベントのお知らせ" onClose={handleClose} actions={<><button className="event-dialog__secondary" type="button" onClick={onSiteClick}>イベント詳細</button><button type="button" onClick={handleClose}>閉じる</button></>}>
    <section className="event-dialog__intro">
      <h3>イベント開催中</h3>
      <p>イベントの進行状況と報酬はイベントページで確認できます。</p>
      <label className="event-dialog__skip"><input type="checkbox" checked={noOpenToday} onChange={event => setNoOpenToday(event.target.checked)} />本日は表示しない</label>
    </section>
  </EventDialogShell>
}

export function Event201004CloseDlg({ onQuit, onContinue }: {
  onQuit: () => void
  onContinue: () => void
}) {
  return <EventDialogShell title="イベントを終了しますか？" actions={<><button className="event-dialog__secondary" type="button" onClick={onQuit}>終了</button><button type="button" onClick={onContinue}>もう一度</button></>}>
    <section className="event-dialog__intro"><p>イベントを終了するか、もう一度挑戦するか選択してください。</p></section>
  </EventDialogShell>
}