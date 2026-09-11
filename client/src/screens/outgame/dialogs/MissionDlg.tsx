import { useEffect, useRef, useState } from 'react'
import * as SignalR from '../../../api/signalr'
import { showError, showMessage } from '../../../utils/msgbox'
import MissionRewardGuideDlg from './MissionRewardGuideDlg'

const WEEKLY_THRESHOLDS = [50, 100, 150, 200, 300, 400, 500, 600]
const DAILY_MISSIONS = [
  ['ログインする', 5], ['東風2回 / 半荘1回プレイ', 5], ['東風4回 / 半荘2回プレイ', 5], ['東風6回 / 半荘3回プレイ', 5], ['東風8回 / 半荘4回プレイ', 5], ['東風10回 / 半荘5回プレイ', 10], ['1位を1回取る', 5], ['1位を2回取る', 10], ['龍珠交換をする', 10], ['龍珠を麻雀で獲得する', 20], ['コイン / 便利アイテム購入', 20],
] as const

interface MissionData {
  pointDayOwn: number
  pointDayMax: number
  pointWeekOwn: number
  pointWeekMax: number
  dailyMissions: number[]
  weeklyRewards: number[]
}

interface Props {
  onClose: () => void
  onMoneyUpdate?: (money: number) => void
  onGemUpdate?: (gem: number) => void
}

const emptyData: MissionData = { pointDayOwn: 0, pointDayMax: 0, pointWeekOwn: 0, pointWeekMax: 0, dailyMissions: new Array(11).fill(0), weeklyRewards: new Array(8).fill(0) }

export default function MissionDlg({ onClose, onMoneyUpdate, onGemUpdate }: Props) {
  const [data, setData] = useState<MissionData>(emptyData)
  const [showGuide, setShowGuide] = useState(false)
  const pendingRewardId = useRef<number | null>(null)

  useEffect(() => {
    const handler = (raw: Record<string, unknown>) => {
      if (Number(raw.result) !== 1) { showError(String(raw.message ?? 'ミッションデータの取得に失敗しました')); return }
      setData({
        pointDayOwn: Number(raw.mjkk105e ?? 0), pointDayMax: Number(raw.mjkk106e ?? 11),
        pointWeekOwn: Number(raw.mjkk107e ?? 0), pointWeekMax: Number(raw.mjkk108e ?? 77),
        dailyMissions: Array.from({ length: 11 }, (_, index) => Number(raw[`mjkk${109 + index}e`] ?? 0)),
        weeklyRewards: Array.from({ length: 8 }, (_, index) => Number(raw[`mjkk${120 + index}e`] ?? 1)),
      })
    }
    SignalR.on('mjkc32e', handler)
    SignalR.send('mjkc32e', {}).catch(() => {})
    return () => SignalR.off('mjkc32e', handler)
  }, [])

  useEffect(() => {
    const handler = (raw: Record<string, unknown>) => {
      const rewardId = pendingRewardId.current
      pendingRewardId.current = null
      if (Number(raw.result) !== 1) {
        showError(String(raw.message ?? '報酬の受取に失敗しました'))
        if (rewardId != null) setData(previous => ({ ...previous, weeklyRewards: previous.weeklyRewards.map((value, index) => index === rewardId - 1 ? 0 : value) }))
        return
      }
      if (typeof raw.gammoney === 'number') onMoneyUpdate?.(raw.gammoney)
      if (typeof raw.gemcount === 'number') onGemUpdate?.(raw.gemcount)
      showMessage(String(raw.message ?? ''), 'ミッション賞')
    }
    SignalR.on('mjkc33e', handler)
    return () => SignalR.off('mjkc33e', handler)
  }, [onGemUpdate, onMoneyUpdate])

  const receive = (rewardId: number) => {
    pendingRewardId.current = rewardId
    setData(previous => ({ ...previous, weeklyRewards: previous.weeklyRewards.map((value, index) => index === rewardId - 1 ? 1 : value) }))
    SignalR.send('mjkc33e', { 'mjkk128e': String(rewardId) }).catch(() => {
      pendingRewardId.current = null
      setData(previous => ({ ...previous, weeklyRewards: previous.weeklyRewards.map((value, index) => index === rewardId - 1 ? 0 : value) }))
    })
  }
  const dayProgress = data.pointDayMax > 0 ? Math.min(100, data.pointDayOwn / data.pointDayMax * 100) : 0
  const weekProgress = data.pointWeekMax > 0 ? Math.min(100, data.pointWeekOwn / data.pointWeekMax * 100) : 0

  return <div className="majak-popup-overlay" role="dialog" aria-modal="true" aria-label="ミッション">
    <section className="majak-popup-panel mission-dialog">
      <header className="majak-popup-titlebar"><h2>ミッション</h2><button className="majak-popup-titlebar__close" type="button" onClick={onClose} aria-label="閉じる">×</button></header>
      <section className="mission-dialog__summary"><div><span>本日の達成</span><strong>{data.pointDayOwn} / {data.pointDayMax}</strong><i><b style={{ width: `${dayProgress}%` }} /></i></div><div><span>今週のポイント</span><strong>{data.pointWeekOwn} / {data.pointWeekMax}</strong><i><b style={{ width: `${weekProgress}%` }} /></i></div></section>
      <main className="majak-popup-body mission-dialog__body"><section className="mission-dialog__daily"><h3>デイリーミッション</h3><ol className="mission-dialog__list">{DAILY_MISSIONS.map(([name, points], index) => <li key={name} className={`mission-dialog__mission${data.dailyMissions[index] === 1 ? ' is-complete' : ''}`}><i className="mission-dialog__check">{data.dailyMissions[index] === 1 ? '✓' : ''}</i><span>{name}</span><b>{points} P</b></li>)}</ol></section><section><h3>ウィークリー報酬</h3><div className="mission-dialog__reward-grid">{WEEKLY_THRESHOLDS.map((threshold, index) => { const available = data.weeklyRewards[index] === 0; const reached = data.pointWeekOwn >= threshold; return <article key={threshold} className={available ? 'is-available' : ''}><span>{threshold} P</span><strong>週間報酬 {index + 1}</strong><button type="button" disabled={!available} onClick={() => receive(index + 1)}>{available ? '受け取る' : reached ? '受取済' : `あと ${threshold - data.pointWeekOwn} P`}</button></article> })}</div></section></main>
      <footer className="majak-popup-actions"><button type="button" onClick={() => setShowGuide(true)}>GPガイド</button><button type="button" onClick={() => SignalR.send('mjkc32e', {}).catch(() => {})}>更新</button><button type="button" onClick={onClose}>閉じる</button></footer>
    </section>
    {showGuide && <MissionRewardGuideDlg onClose={() => setShowGuide(false)} />}
  </div>
}
