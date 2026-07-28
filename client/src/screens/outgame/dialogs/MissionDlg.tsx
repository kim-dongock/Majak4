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

const IMG = '/assets/images/game'
const MISSION_W = 697
const MISSION_H = 527

/** 週間報酬 必要ポイント */
const WEEKLY_THRESHOLDS = [50, 100, 150, 200, 300, 400, 500, 600]

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
          fontFamily: "'MS PGothic', 'Noto Sans JP', 'MS UI Gothic', sans-serif",
          fontSize: 18, fontWeight: 'bold', color: '#fff',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          pointerEvents: 'none',
        }}>
          {`${data.pointDayOwn}/${data.pointDayMax}`}
        </div>

        {/* 今週の獲得ポイント
             PaintDailyMission: DrawText(str, CRect(475,125,670,143), DT_CENTER) */}
        <div style={{
          position: 'absolute', left: 475, top: 125, width: 195, height: 18,
          fontFamily: "'MS PGothic', 'Noto Sans JP', 'MS UI Gothic', sans-serif",
          fontSize: 18, fontWeight: 'bold', color: '#fff',
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
