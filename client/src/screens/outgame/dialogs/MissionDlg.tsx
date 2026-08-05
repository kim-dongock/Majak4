/**
 * CMJDailyMissionDialog 相当 — 週間ミッションダイアログ (AP-09 §3-3)
 * レガシー: legacy/client/HgMajak2/MJDailyMissionDialog.h/cpp
 *          (MajakFrame.cpp: m_dlgDailyMission / MAJAK3_MISSION 参照)
 *
 * ── 画像リソース (OnCreate / OnPaint より) ───────────────────────────
 *   mj_loginbo_bg.png          背景 (550×440)
 *   mj_loginbo_btn_close.png   とじるボタン (4フレーム 88×26)
 *   mj_loginbo_btn_accept.png  受取ボタン (4フレーム 68×26)
 *   mj_loginbo_icon_check.png  達成チェックマーク (1フレーム 20×20)
 *
 * ── 通信プロトコル ────────────────────────────────────────────────────
 *   リクエスト: mjkc32e (GetMissionList) → マウント時に送信
 *   レスポンス: mjkc32e (同コード) で返却
 *     mjkk105e = PointDayOwn  (本日の達成ミッション数)
 *     mjkk106e = PointDayMax  (11)
 *     mjkk107e = PointWeekOwn (今週の合計ポイント)
 *     mjkk108e = PointWeekMax (77)
 *     mjkk109e〜mjkk119e = DailyMission1〜11 (0=未達成, 1=達成)
 *     mjkk120e〜mjkk127e = WeeklyReward1〜8  (0=受取可能, 1=受取済または点数不足)
 *   報酬受取: mjkc33e (RcvWeeklyReward) / Key.WeeklyRewardId = "mjkk128e"
 *     レスポンス: mjkc33e (同コード)
 *
 * ── デイリーミッション一覧 (MJK_DAILYMISSIONMAST より) ─────────────
 *   1: ログインする (5P)
 *   2: 東風2回/半荘1回プレイ (5P)
 *   3: 東風4回/半荘2回プレイ (5P)
 *   4: 東風6回/半荘3回プレイ (5P)
 *   5: 東風8回/半荘4回プレイ (5P)
 *   6: 東風10回/半荘5回プレイ (10P)
 *   7: 1位を1回取る (5P)
 *   8: 1位を2回取る (10P)
 *   9: 龍珠交換をする (10P)
 *  10: 龍珠を麻雀で獲得する (20P)
 *  11: コイン/便利アイテム購入 (20P)
 *
 * ── 週間報酬 (MJK_WEEKLYREWARDMAST より) ─────────────────────────
 *   1=50P, 2=100P, 3=150P, 4=200P, 5=300P, 6=400P, 7=500P, 8=600P
 */
import { useEffect, useRef, useState } from 'react'
import * as SignalR from '../../../api/signalr'
import { showError, showMessage } from '../../../utils/msgbox'
import { useOutgameLayoutMode } from '../../../hooks/useOutgameLayoutMode'

const IMG = '/assets/images/game'
const MISSION_W = 697
const MISSION_H = 527

/** 週間報酬 必要ポイント */
const WEEKLY_THRESHOLDS = [50, 100, 150, 200, 300, 400, 500, 600]

const DAILY_MISSION_NAMES = [
  'ログインする',
  '東風2回 / 半荘1回プレイ',
  '東風4回 / 半荘2回プレイ',
  '東風6回 / 半荘3回プレイ',
  '東風8回 / 半荘4回プレイ',
  '東風10回 / 半荘5回プレイ',
  '1位を1回取る',
  '1位を2回取る',
  '龍珠交換をする',
  '龍珠を麻雀で獲得する',
  'コイン / 便利アイテム購入',
]

const DAILY_MISSION_POINTS = [5, 5, 5, 5, 5, 10, 5, 10, 10, 20, 20]

interface MissionData {
  pointDayOwn:   number
  pointDayMax:   number
  pointWeekOwn:  number
  pointWeekMax:  number
  dailyMissions: number[]   // 11件 (0=未達成, 1=達成)
  weeklyRewards: number[]   // 8件 (0=受取可能, 1=受取済または点数不足)
}

const EMPTY_DATA: MissionData = {
  pointDayOwn:   0,
  pointDayMax:   0,
  pointWeekOwn:  0,
  pointWeekMax:  0,
  dailyMissions: new Array(11).fill(0),
  weeklyRewards: new Array(8).fill(0),
}

interface Props {
  onClose: () => void
  /** mjkc17e / mjkc33e での GamMoney 更新時に呼ぶコールバック (任意) */
  onMoneyUpdate?: (money: number) => void
  /** mjkc33e での GemCount 更新時に呼ぶコールバック (任意) */
  onGemUpdate?: (gem: number) => void
}

function SpriteButton({
  src, frameW, frameH, x, y, onClick, disabled = false, title,
}: {
  src: string; frameW: number; frameH: number
  x: number; y: number
  onClick: () => void; disabled?: boolean; title?: string
}) {
  const [fi, setFi] = useState(disabled ? 1 : 0)
  useEffect(() => { setFi(disabled ? 1 : 0) }, [disabled])
  return (
    <button
      title={title}
      disabled={disabled}
      onClick={disabled ? undefined : onClick}
      onMouseEnter={() => !disabled && setFi(2)}
      onMouseLeave={() => !disabled && setFi(disabled ? 1 : 0)}
      onMouseDown={() => !disabled && setFi(3)}
      onMouseUp={() => !disabled && setFi(2)}
      style={{
        position: 'absolute', left: x, top: y,
        width: frameW, height: frameH,
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-fi * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        border: 'none', padding: 0,
        cursor: disabled ? 'not-allowed' : 'pointer',
        outline: 'none', imageRendering: 'pixelated',
      }}
    />
  )
}

function ResponsiveMissionDialog({
  data,
  onReceive,
  onRefresh,
  onClose,
}: {
  data: MissionData
  onReceive: (rewardId: number) => void
  onRefresh: () => void
  onClose: () => void
}) {
  const layoutMode = useOutgameLayoutMode()
  const modeClass = layoutMode === 'desktop' ? '' : ` mission-dialog--${layoutMode}`
  const dailyProgress = data.pointDayMax > 0 ? Math.min(100, data.pointDayOwn / data.pointDayMax * 100) : 0
  const weeklyProgress = data.pointWeekMax > 0 ? Math.min(100, data.pointWeekOwn / data.pointWeekMax * 100) : 0

  return <div className={`mission-dialog-overlay${modeClass}`} role="dialog" aria-modal="true" aria-label="ミッション">
    <section className={`mission-dialog${modeClass}`}>
      <header className="mission-dialog__header">
        <div><p>MAJAK4 MISSION</p><h2>ミッション</h2></div>
        <div className="mission-dialog__header-actions">
          <button type="button" onClick={onRefresh}>更新</button>
          <button type="button" onClick={onClose} aria-label="閉じる">x</button>
        </div>
      </header>
      <div className="mission-dialog__summary">
        <div><span>本日の達成</span><strong>{data.pointDayOwn} / {data.pointDayMax}</strong><i><b style={{ width: `${dailyProgress}%` }} /></i></div>
        <div><span>今週のポイント</span><strong>{data.pointWeekOwn} / {data.pointWeekMax}</strong><i><b style={{ width: `${weeklyProgress}%` }} /></i></div>
      </div>
      <main className="mission-dialog__content">
        <section className="mission-dialog__daily">
          <h3>デイリーミッション</h3>
          <ol>
            {DAILY_MISSION_NAMES.map((name, index) => {
              const completed = data.dailyMissions[index] === 1
              return <li key={name} className={completed ? 'is-complete' : ''}>
                <span className="mission-dialog__check">{completed ? '✓' : ''}</span>
                <span>{name}</span><b>{DAILY_MISSION_POINTS[index]} P</b>
              </li>
            })}
          </ol>
        </section>
        <section className="mission-dialog__weekly">
          <h3>ウィークリー報酬</h3>
          <div className="mission-dialog__reward-grid">
            {WEEKLY_THRESHOLDS.map((threshold, index) => {
              const available = data.weeklyRewards[index] === 0
              const reached = data.pointWeekOwn >= threshold
              const label = available ? '受け取る' : reached ? '受取済' : `あと ${threshold - data.pointWeekOwn} P`
              return <article key={threshold} className={available ? 'is-available' : ''}>
                <span>{threshold} P</span>
                <strong>週間報酬 {index + 1}</strong>
                <button type="button" disabled={!available} onClick={() => onReceive(index + 1)}>{label}</button>
              </article>
            })}
          </div>
        </section>
      </main>
      <footer><button type="button" onClick={onClose}>閉じる</button></footer>
    </section>
    <style>{`
      .mission-dialog-overlay { position: absolute; inset: 0; z-index: 250; display: grid; place-items: center; padding: 20px; overflow: hidden; background: rgba(8,16,20,.7); box-sizing: border-box; font-family: var(--majak-font-family-ui); }
      .mission-dialog { width: min(1050px, 100%); height: min(650px, 100%); min-height: 0; display: flex; flex-direction: column; overflow: hidden; color: #172323; background: #f5f2e9; border: 1px solid #7d8e80; box-shadow: 0 24px 72px rgba(0,0,0,.42); }
      .mission-dialog__header { display: flex; align-items: center; justify-content: space-between; gap: 18px; padding: 16px 24px; color: #fff; background: #174b43; }
      .mission-dialog__header p { margin: 0; color: #d7b95d; font: 700 calc(10px * var(--majak-type-scale))/1 var(--majak-font-family-ui); letter-spacing: 1px; }
      .mission-dialog__header h2 { margin: 2px 0 0; font-size: calc(25px * var(--majak-type-scale)); font-weight: 700; letter-spacing: 0; }
      .mission-dialog__header-actions { display: flex; gap: 8px; }
      .mission-dialog__header button { min-width: 36px; height: 36px; border: 1px solid rgba(255,255,255,.7); border-radius: 0; padding: 0 10px; color: #fff; background: transparent; font: 700 calc(13px * var(--majak-type-scale))/1 var(--majak-font-family-ui); cursor: pointer; }
      .mission-dialog__header-actions button:last-child { font-size: calc(22px * var(--majak-type-scale)); }
      .mission-dialog__summary { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1px; background: #c1cbc0; border-bottom: 1px solid #c1cbc0; }
      .mission-dialog__summary > div { min-width: 0; display: grid; grid-template-columns: auto 1fr; gap: 6px 12px; align-items: center; padding: 11px 18px; background: #f7faf4; }
      .mission-dialog__summary span { color: #607069; font: 700 calc(11px * var(--majak-type-scale))/1 var(--majak-font-family-ui); }
      .mission-dialog__summary strong { color: #1f4d42; font: 700 calc(17px * var(--majak-type-scale))/1.1 var(--majak-font-family-ui); text-align: right; }
      .mission-dialog__summary i { grid-column: 1 / -1; height: 6px; overflow: hidden; background: #d8e0d5; }
      .mission-dialog__summary b { display: block; height: 100%; background: #b84228; }
      .mission-dialog__content { min-height: 0; flex: 1; display: grid; grid-template-columns: minmax(0, 1.15fr) minmax(0, .85fr); gap: 18px; padding: 18px 24px; overflow: auto; }
      .mission-dialog__content section { min-width: 0; }
      .mission-dialog__content h3 { margin: 0 0 10px; color: #31473f; font-size: calc(16px * var(--majak-type-scale)); font-weight: 700; }
      .mission-dialog__daily h3 { font-size: var(--majak-font-17); }
      .mission-dialog__daily ol { display: grid; gap: 5px; margin: 0; padding: 0; list-style: none; }
      .mission-dialog__daily li { display: grid; grid-template-columns: 24px minmax(0, 1fr) auto; gap: 9px; align-items: center; min-height: 34px; padding: 7px 10px; border: 1px solid #d2dacf; background: #fffdf8; color: #52645d; font: var(--majak-font-13)/1.3 var(--majak-font-family-ui); }
      .mission-dialog__daily li.is-complete { color: #1f4d42; border-color: #aebfab; background: #edf3e8; }
      .mission-dialog__check { display: grid; place-items: center; width: 20px; height: 20px; border: 1px solid #aeb8ae; color: #fff; background: #fff; font: 700 var(--majak-font-15)/1 var(--majak-font-family-ui); }
      .mission-dialog__daily .is-complete .mission-dialog__check { border-color: #1c5a4d; background: #1c5a4d; }
      .mission-dialog__daily b { color: #a06425; font: 700 var(--majak-font-12)/1 var(--majak-font-family-ui); white-space: nowrap; }
      .mission-dialog__weekly h3 { font-size: var(--majak-font-15); }
      .mission-dialog__reward-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; }
      .mission-dialog__reward-grid article { min-width: 0; min-height: 105px; display: flex; flex-direction: column; gap: 7px; padding: 11px; border: 1px solid #c8d0c2; background: #fffdf8; }
      .mission-dialog__reward-grid article.is-available { border-top: 4px solid #d19f35; background: #ecf0e4; }
      .mission-dialog__reward-grid span { color: #a06425; font: 700 var(--majak-font-10)/1 var(--majak-font-family-ui); }
      .mission-dialog__reward-grid strong { color: #1f302b; font-size: var(--majak-font-15); font-weight: 400; }
      .mission-dialog__reward-grid button, .mission-dialog footer button { margin-top: auto; border: 0; border-radius: 3px; padding: 9px 13px; color: #fff; background: #1c5a4d; font: 700 calc(13px * var(--majak-type-scale))/1 var(--majak-font-family-ui); cursor: pointer; }
      .mission-dialog__reward-grid button { font-size: var(--majak-font-12); }
      .mission-dialog__reward-grid button:disabled { color: #87918c; background: #d7ddd5; cursor: not-allowed; }
      .mission-dialog footer { display: flex; justify-content: flex-end; padding: 12px 24px; border-top: 1px solid #c8d0c2; background: #e8ede4; }
      .mission-dialog footer button { margin: 0; color: #32453e; border: 1px solid #839087; background: transparent; }
      .mission-dialog--mobileLandscape, .mission-dialog--mobilePortrait { width: 100%; height: 100%; }
      .mission-dialog-overlay--mobileLandscape, .mission-dialog-overlay--mobilePortrait { padding: 0; }
      .mission-dialog--mobileLandscape .mission-dialog__header, .mission-dialog--mobilePortrait .mission-dialog__header { padding: 8px 10px; }
      .mission-dialog--mobileLandscape .mission-dialog__header p, .mission-dialog--mobilePortrait .mission-dialog__header p { display: none; }
      .mission-dialog--mobileLandscape .mission-dialog__header h2, .mission-dialog--mobilePortrait .mission-dialog__header h2 { margin: 0; font-size: calc(17px * var(--majak-type-scale)); }
      .mission-dialog--mobileLandscape .mission-dialog__header button, .mission-dialog--mobilePortrait .mission-dialog__header button { min-width: 28px; height: 28px; padding: 0 7px; font-size: calc(11px * var(--majak-type-scale)); }
      .mission-dialog--mobileLandscape .mission-dialog__header-actions button:last-child, .mission-dialog--mobilePortrait .mission-dialog__header-actions button:last-child { font-size: calc(18px * var(--majak-type-scale)); }
      .mission-dialog--mobileLandscape .mission-dialog__summary, .mission-dialog--mobilePortrait .mission-dialog__summary { grid-template-columns: repeat(2, minmax(0, 1fr)); }
      .mission-dialog--mobileLandscape .mission-dialog__summary > div, .mission-dialog--mobilePortrait .mission-dialog__summary > div { gap: 4px; padding: 7px 8px; }
      .mission-dialog--mobileLandscape .mission-dialog__summary span, .mission-dialog--mobilePortrait .mission-dialog__summary span { font-size: calc(10px * var(--majak-type-scale)); }
      .mission-dialog--mobileLandscape .mission-dialog__summary strong, .mission-dialog--mobilePortrait .mission-dialog__summary strong { font-size: calc(16px * var(--majak-type-scale)); }
      .mission-dialog--mobileLandscape .mission-dialog__content, .mission-dialog--mobilePortrait .mission-dialog__content { gap: 10px; padding: 9px; }
      .mission-dialog--mobileLandscape .mission-dialog__content h3, .mission-dialog--mobilePortrait .mission-dialog__content h3 { margin-bottom: 6px; font-size: calc(13px * var(--majak-type-scale)); }
      .mission-dialog--mobileLandscape .mission-dialog__daily h3, .mission-dialog--mobilePortrait .mission-dialog__daily h3 { font-size: var(--majak-font-14); }
      .mission-dialog--mobileLandscape .mission-dialog__daily ol, .mission-dialog--mobilePortrait .mission-dialog__daily ol { gap: 3px; }
      .mission-dialog--mobileLandscape .mission-dialog__daily li, .mission-dialog--mobilePortrait .mission-dialog__daily li { grid-template-columns: 16px minmax(0, 1fr) auto; gap: 4px; min-height: 25px; padding: 4px; font-size: var(--majak-font-11); }
      .mission-dialog--mobileLandscape .mission-dialog__check, .mission-dialog--mobilePortrait .mission-dialog__check { width: 14px; height: 14px; font-size: var(--majak-font-11); }
      .mission-dialog--mobileLandscape .mission-dialog__daily b, .mission-dialog--mobilePortrait .mission-dialog__daily b { font-size: var(--majak-font-11); }
      .mission-dialog--mobileLandscape .mission-dialog__weekly h3, .mission-dialog--mobilePortrait .mission-dialog__weekly h3 { font-size: var(--majak-font-12); }
      .mission-dialog--mobileLandscape .mission-dialog__reward-grid, .mission-dialog--mobilePortrait .mission-dialog__reward-grid { gap: 6px; }
      .mission-dialog--mobileLandscape .mission-dialog__reward-grid article, .mission-dialog--mobilePortrait .mission-dialog__reward-grid article { min-height: 75px; gap: 4px; padding: 6px; }
      .mission-dialog--mobileLandscape .mission-dialog__reward-grid strong, .mission-dialog--mobilePortrait .mission-dialog__reward-grid strong { font-size: var(--majak-font-10); }
      .mission-dialog--mobileLandscape .mission-dialog__reward-grid span, .mission-dialog--mobilePortrait .mission-dialog__reward-grid span { font-size: var(--majak-font-9); }
      .mission-dialog--mobileLandscape .mission-dialog__reward-grid button, .mission-dialog--mobilePortrait .mission-dialog__reward-grid button, .mission-dialog--mobileLandscape .mission-dialog footer button, .mission-dialog--mobilePortrait .mission-dialog footer button { padding: 7px; font-size: calc(11px * var(--majak-type-scale)); }
      .mission-dialog--mobileLandscape .mission-dialog__reward-grid button, .mission-dialog--mobilePortrait .mission-dialog__reward-grid button { font-size: var(--majak-font-10); }
      .mission-dialog--mobileLandscape .mission-dialog footer, .mission-dialog--mobilePortrait .mission-dialog footer { padding: 9px; }
      .mission-dialog--mobilePortrait .mission-dialog__content { grid-template-columns: 1fr; overflow: auto; }
    `}</style>
  </div>
}

export default function MissionDlg({ onClose, onMoneyUpdate, onGemUpdate }: Props) {
  const [data, setData] = useState<MissionData>(EMPTY_DATA)
  const [dialogScale, setDialogScale] = useState(1)
  const pendingRewardId = useRef<number | null>(null)

  useEffect(() => {
    const updateScale = () => {
      const margin = 16
      setDialogScale(Math.min(1, (window.innerWidth - margin) / MISSION_W, (window.innerHeight - margin) / MISSION_H))
    }
    updateScale()
    window.addEventListener('resize', updateScale)
    return () => window.removeEventListener('resize', updateScale)
  }, [])

  /* ドラッグ移動 */
  const [pos, setPos]     = useState({ x: 0, y: 0 })
  const dragging          = useRef(false)
  const dragOffset        = useRef({ dx: 0, dy: 0 })
  const onDragStart       = (e: React.MouseEvent) => {
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect()
    if (e.clientY - rect.top >= 40) return
    dragging.current   = true
    dragOffset.current = { dx: e.clientX - pos.x, dy: e.clientY - pos.y }
    e.preventDefault()
  }
  useEffect(() => {
    const onMove = (e: MouseEvent) => {
      if (!dragging.current) return
      setPos({ x: e.clientX - dragOffset.current.dx, y: e.clientY - dragOffset.current.dy })
    }
    const onUp = () => { dragging.current = false }
    window.addEventListener('mousemove', onMove)
    window.addEventListener('mouseup',   onUp)
    return () => { window.removeEventListener('mousemove', onMove); window.removeEventListener('mouseup', onUp) }
  }, [])

  /** mjkc33e — CMJMemberInfoDialog::OnGotMissionItem 相当 */
  useEffect(() => {
    const handler = (raw: Record<string, unknown>) => {
      const rewardId = pendingRewardId.current
      pendingRewardId.current = null
      if (Number(raw.result) !== 1) {
        showError(String(raw.message ?? '報酬の受取に失敗しました'))
        if (rewardId != null) {
          setData(prev => {
            const weeklyRewards = [...prev.weeklyRewards]
            weeklyRewards[rewardId - 1] = 0
            return { ...prev, weeklyRewards }
          })
        }
        return
      }
      if (typeof raw.gammoney === 'number') onMoneyUpdate?.(raw.gammoney as number)
      if (typeof raw.gemcount === 'number') onGemUpdate?.(raw.gemcount as number)
      showMessage(String(raw.message ?? ''), 'ミッション賞')
    }
    SignalR.on('mjkc33e', handler)
    return () => SignalR.off('mjkc33e', handler)
  }, [onGemUpdate, onMoneyUpdate])

  /**
   * マウント時: mjkc32e (GetMissionList) 送信
   * レスポンス: mjkc32e イベント
   */
  useEffect(() => {
    const handler = (raw: Record<string, unknown>) => {
      if (Number(raw.result) !== 1) {
        showError(String(raw.message ?? 'ミッションデータの取得に失敗しました'))
        return
      }
      const daily  = Array.from({ length: 11 }, (_, i) => Number(raw[`mjkk${109 + i}e`] ?? 0))
      const weekly = Array.from({ length: 8  }, (_, i) => Number(raw[`mjkk${120 + i}e`] ?? 1))
      setData({
        pointDayOwn:   Number(raw['mjkk105e'] ?? 0),
        pointDayMax:   Number(raw['mjkk106e'] ?? 11),
        pointWeekOwn:  Number(raw['mjkk107e'] ?? 0),
        pointWeekMax:  Number(raw['mjkk108e'] ?? 77),
        dailyMissions: daily,
        weeklyRewards: weekly,
      })
    }
    SignalR.on('mjkc32e', handler)
    SignalR.send('mjkc32e', {}).catch(() => {})
    return () => SignalR.off('mjkc32e', handler)
  }, [])

  /**
   * 週間報酬受取 — mjkc33e (RcvWeeklyReward)
   * Key.WeeklyRewardId = "mjkk128e"
   */
  const handleReceive = async (rewardId: number) => {
    pendingRewardId.current = rewardId
    setData(prev => {
      const weeklyRewards = [...prev.weeklyRewards]
      weeklyRewards[rewardId - 1] = 1
      return { ...prev, weeklyRewards }
    })
    await SignalR.send('mjkc33e', { 'mjkk128e': String(rewardId) }).catch(() => {
      pendingRewardId.current = null
      setData(prev => {
        const weeklyRewards = [...prev.weeklyRewards]
        weeklyRewards[rewardId - 1] = 0
        return { ...prev, weeklyRewards }
      })
    })
  }

  /* チェックマーク座標 (PaintDailyMission より)
     DrawTransparent(&dc, 235, 88/120/152/.../408)
     x=235, y0=88, 行間隔=32px */
  const MISSION_CHECK_X = 235
  const MISSION_Y0      = 88
  const MISSION_ROW     = 32

  /* 週間報酬 受取ボタン座標 (InitDialogDailyMission より)
     Row1: [319,409,499,589] y=277
     Row2: [319,409,499,589] y=407 */
  const REWARD_COL_X = [319, 409, 499, 589]
  const REWARD_ROW_Y = [277, 407]

  const useResponsiveMission = true
  if (useResponsiveMission) {
    return <ResponsiveMissionDialog
      data={data}
      onReceive={handleReceive}
      onRefresh={() => { SignalR.send('mjkc32e', {}).catch(() => {}) }}
      onClose={onClose}
    />
  }

  return (
    <div style={{
      position: dialogScale < 1 ? 'fixed' : 'absolute', inset: 0, zIndex: 250,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'transparent',
    }}>
      <div style={{ width: MISSION_W * dialogScale, height: MISSION_H * dialogScale }}>
      <div
        style={{
          position: 'relative',
          width: MISSION_W,
          height: MISSION_H,
          left: dialogScale < 1 ? 0 : pos.x,
          top: dialogScale < 1 ? 0 : pos.y,
          transform: `scale(${dialogScale})`,
          transformOrigin: 'top left',
        }}
        onMouseDown={dialogScale < 1 ? undefined : onDragStart}
      >
        {/* 背景: mj_loginbo_bg.png (697×527) — ミッション名・ラベルは背景に含まれる */}
        <img
          src={`${IMG}/mj_loginbo_bg.png`}
          alt=""
          draggable={false}
          style={{ position: 'absolute', left: 0, top: 0, width: MISSION_W, height: MISSION_H, userSelect: 'none' }}
        />

        {/* ── 左パネル: 達成チェックマークのみ (ミッション名は背景画像に含まれるため非描画)
             PaintDailyMission: DrawTransparent(&dc, 235, 88/120/152/.../408)
             x=235, y0=88, 間隔=32px */}
        {data.dailyMissions.map((status, i) => {
          const rowY = MISSION_Y0 + i * MISSION_ROW
          const done = status === 1
          return done ? (
            <img
              key={i}
              src={`${IMG}/mj_loginbo_icon_check.png`}
              alt="達成"
              draggable={false}
              style={{
                position: 'absolute',
                left: MISSION_CHECK_X,
                top:  rowY,
                width: 42, height: 32,
                imageRendering: 'pixelated', pointerEvents: 'none',
              }}
            />
          ) : null
        })}

        {/* ── 右パネル: 本日の獲得ポイント
             PaintDailyMission: DrawText(str, CRect(475,75,670,93), DT_CENTER)
             フォント: -18px Bold 白 */}
        <div style={{
          position: 'absolute', left: 475, top: 75, width: 195, height: 18,
          fontFamily: 'var(--majak-font-family-ui)',
          fontSize: 'calc(18px * var(--majak-type-scale))', fontWeight: 'bold', color: '#fff',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          pointerEvents: 'none',
        }}>
          {`${data.pointDayOwn}/${data.pointDayMax}`}
        </div>

        {/* 今週の獲得ポイント
             PaintDailyMission: DrawText(str, CRect(475,125,670,143), DT_CENTER) */}
        <div style={{
          position: 'absolute', left: 475, top: 125, width: 195, height: 18,
          fontFamily: 'var(--majak-font-family-ui)',
          fontSize: 'calc(18px * var(--majak-type-scale))', fontWeight: 'bold', color: '#fff',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          pointerEvents: 'none',
        }}>
          {`${data.pointWeekOwn}/${data.pointWeekMax}`}
        </div>

        {/* ── 週間報酬 8件 (2行×4列)
             InitDialogDailyMission:
               Row1 x=[319,409,499,589] y=277  (Item 1-4)
               Row2 x=[319,409,499,589] y=407  (Item 5-8)
             原典 EnableWindow(!bMissionItem):
               status=0 (MSN_RS_NOTRCV=0) → 有効 (受取可能)
               status=1 (MSN_RS_RCV=1)    → 無効 (受取済 OR 点数不足)
             ※ 点数不足もMSN_RS_RCVと同じ値=1を返す (legacy準拠)
             ボタンは常に表示、状態で有効/無効を切り替える */}
        {data.weeklyRewards.map((status, i) => {
          const col    = i % 4
          const row    = Math.floor(i / 4)
          const btnX   = REWARD_COL_X[col]
          const btnY   = REWARD_ROW_Y[row]
          const canRcv = status === 0   // MSN_RS_NOTRCV=0 のみ有効
          return (
            <SpriteButton
              key={i}
              src={`${IMG}/mj_loginbo_btn_accept.png`}
              frameW={62} frameH={30}
              x={btnX} y={btnY}
              disabled={!canRcv}
              onClick={() => handleReceive(i + 1)}
              title={
                status === 1 ? '受取済またはポイント不足' :
                `${WEEKLY_THRESHOLDS[i]}P 報酬を受取`
              }
            />
          )
        })}

        {/* ── とじるボタン: mj_loginbo_btn_close.png (4フレーム 88×32)
             InitDialogDailyMission: m_btnOK.Create(..., 576, 473, this, IDOK) */}
        <SpriteButton
          src={`${IMG}/mj_loginbo_btn_close.png`}
          frameW={88} frameH={32}
          x={576} y={473}
          onClick={onClose}
          title="閉じる"
        />

        {/* ── 更新ボタン: mj_loginbo_btn_reflesh.png (4フレーム 85×29)
             InitDialogDailyMission: m_btnUpdate.Create(..., 576, 14, this, IDC_MISSION_UPDATE) */}
        <SpriteButton
          src={`${IMG}/mj_loginbo_btn_reflesh.png`}
          frameW={85} frameH={29}
          x={576} y={14}
          onClick={() => {
            SignalR.send('mjkc32e', {}).catch(() => {})
          }}
          title="更新"
        />
      </div>
      </div>
    </div>
  )
}
