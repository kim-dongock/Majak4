/**
 * CMJAskEndDlg 相当 — あがりやめ確認 (AP-09 §3-3-1)
 * レガシー: legacy/client/HgMajak2/MJAskEndDlg.h/cpp
 *
 * ── AP-11 §8 適用 ─────────────────────────────────────────────────────────
 * このダイアログは .him ファイルを使用しない (MFC 標準コントロールのみ)。
 * → 全コントロールを HTML 標準コンポーネントに置き換える。
 *
 * コントロール:
 *   CProgressCtrl (IDC_PROGRESS) → <progress> / CSS プログレスバー
 *   CButton (IDYES)               → <button>   続ける
 *   CButton (IDNO)                → <button>   やめる
 *
 * ── 動作ロジック ─────────────────────────────────────────────────────────
 * OnInitDialog():
 *   m_wndProgress.SetRange(0, 100)
 *   m_wndProgress.SetPos(100)         ← 100% から開始
 *   m_wndProgress.SetStep(-1)
 *   SetTimer(0, 100, NULL)            ← 100ms 間隔でカウントダウン
 *
 * OnTimer():
 *   m_wndProgress.StepIt()            ← pos を 1 減らす (100ms × 100 = 10秒)
 *   pos <= 0 → EndDialog(IDYES)       ← 自動で続ける
 *
 * OnYes() → EndDialog(IDYES) → onYes() コールバック (RON: 続ける)
 * OnNo()  → EndDialog(IDNO)  → onNo() コールバック  (PAS: やめる)
 * OnCancel() → 何もしない     ← × ボタンによる閉じ操作を防止
 * ────────────────────────────────────────────────────────────────────────
 */
import { useEffect, useState } from 'react'

const TIMER_INTERVAL_MS = 100   // SetTimer(0, 100, NULL)
const PROGRESS_MAX      = 100   // SetRange(0, 100)
// TIMEOUT_TOTAL_MS = 10,000ms = 10秒 (TIMER_INTERVAL_MS × PROGRESS_MAX)

interface Props {
  onYes: () => void  // IDYES: 続ける
  onNo:  () => void  // IDNO:  やめる
  deadlineAt?: number
}

export default function AskEndDlg({ onYes, onNo, deadlineAt }: Props) {
  /** m_wndProgress.GetPos() 相当 */
  const [progress, setProgress] = useState(() => deadlineAt === undefined
    ? PROGRESS_MAX
    : Math.min(PROGRESS_MAX, Math.max(0, Math.ceil((deadlineAt - performance.now()) / TIMER_INTERVAL_MS)))
  )

  /** OnTimer() 相当: 100ms ごとに -1 → 0 で自動退出 */
  useEffect(() => {
    const id = setInterval(() => {
      setProgress(p => {
        const next = deadlineAt === undefined
          ? p - 1
          : Math.min(PROGRESS_MAX, Math.max(0, Math.ceil((deadlineAt - performance.now()) / TIMER_INTERVAL_MS)))
        if (next <= 0) {
          clearInterval(id)
          onYes()   /* EndDialog(IDYES) 相当 */
          return 0
        }
        return next
      })
    }, TIMER_INTERVAL_MS)
    return () => clearInterval(id)
  }, [deadlineAt, onYes])

  const remainingSeconds = Math.ceil(progress / (1000 / TIMER_INTERVAL_MS))

  return (
    <div className="majak-popup-overlay">
      <section className="majak-popup-panel majak-ask-end-dialog" role="dialog" aria-modal="true" aria-labelledby="ask-end-dialog-title">
        <header id="ask-end-dialog-title" className="majak-popup-titlebar">
          <span>あがりやめ</span>
          <button className="majak-popup-titlebar__close" type="button" onClick={onNo} aria-label="閉じる">×</button>
        </header>
        <div className="majak-popup-body majak-ask-end-dialog__body">
          <p>あがりやめせずに対局を続けますか？</p>
          <p className="majak-ask-end-dialog__timer">残り {remainingSeconds} 秒</p>
          <div className="majak-ask-end-dialog__progress" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={progress}>
            <div style={{ width: `${progress}%`, transition: `width ${TIMER_INTERVAL_MS}ms linear` }} />
          </div>
        </div>
        <footer className="majak-popup-actions">
          <button type="button" className="is-primary" onClick={onYes}>続ける</button>
          <button type="button" onClick={onNo}>やめる</button>
        </footer>
      </section>
    </div>
  )
}
