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
}

export default function AskEndDlg({ onYes, onNo }: Props) {
  /** m_wndProgress.GetPos() 相当 */
  const [progress, setProgress] = useState(PROGRESS_MAX)

  /** OnTimer() 相当: 100ms ごとに -1 → 0 で自動退出 */
  useEffect(() => {
    const id = setInterval(() => {
      setProgress(p => {
        const next = p - 1
        if (next <= 0) {
          clearInterval(id)
          onYes()   /* EndDialog(IDYES) 相当 */
          return 0
        }
        return next
      })
    }, TIMER_INTERVAL_MS)
    return () => clearInterval(id)
  }, [onYes])

  /** バー残り % */
  const pct = (progress / PROGRESS_MAX) * 100

  // IDD_ASKEND_DLG: 127×65 DU → client 190×105px + titlebar 22px
  // CAPTION "あがりやめ"  FONT 9,"ＭＳ Ｐゴシック"
  // DU→px: 1DU_x=1.5px, 1DU_y=1.625px
  const SX = (du: number) => Math.round(du * 1.5)
  const SY = (du: number) => Math.round(du * 1.625)
  const FONT    = "'MS PGothic', 'MS UI Gothic', 'Meiryo', sans-serif"
  const DLG_BG  = '#d4d0c8'
  const DLG_W   = SX(127)  // 190
  const DLG_H   = SY(65)   // 105
  const TITLE_H = 22

  const btnStyle: React.CSSProperties = {
    position: 'absolute', fontFamily: FONT, fontSize: 12, color: '#000',
    background: DLG_BG,
    borderTop: '2px solid #fff', borderLeft: '2px solid #fff',
    borderRight: '2px solid #808080', borderBottom: '2px solid #808080',
    cursor: 'pointer', outline: 'none',
  }

  return (
    <div
      style={{
        position: 'absolute', inset: 0,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        background: 'transparent', zIndex: 500,
      }}
    >
      {/* IDD_ASKEND_DLG ウィンドウ (DS_MODALFRAME|WS_CAPTION) */}
      <div style={{ position: 'relative', width: DLG_W, boxShadow: '3px 3px 8px rgba(0,0,0,0.6)' }}>

        {/* タイトルバー CAPTION "退室確認" */}
        <div style={{
          height: TITLE_H,
          background: 'linear-gradient(to right, #000080, #1060d0)',
          display: 'flex', alignItems: 'center', paddingLeft: 8,
        }}>
          <span style={{ fontFamily: FONT, fontSize: 12, color: '#fff', fontWeight: 'bold' }}>あがりやめ</span>
        </div>

        {/* クライアントエリア: 190×105px */}
        <div style={{ position: 'relative', width: DLG_W, height: DLG_H, background: DLG_BG }}>

          {/* LTEXT "あがりやめせずに対局を続けますか？" (7,7,113,8) */}
          <div style={{
            position: 'absolute', left: SX(7), top: SY(7), width: SX(113),
            fontFamily: FONT, fontSize: 12, color: '#000',
          }}>
            あがりやめせずに対局を続けますか？
          </div>

          {/* CProgressCtrl IDC_PROGRESS (5,25,117,9)
              SetRange(0,100), SetPos(100), SetStep(-1) → 100ms × 100 = 10秒カウントダウン */}
          <div style={{
            position: 'absolute', left: SX(5), top: SY(25),
            width: SX(117), height: SY(9),
            background: '#c0c0c0', border: '1px solid #404040',
            overflow: 'hidden',
          }}>
            <div style={{
              width: `${pct}%`, height: '100%',
              background: '#000080',
              transition: `width ${TIMER_INTERVAL_MS}ms linear`,
            }} />
          </div>

          {/* DEFPUSHBUTTON "続ける(&Y)" IDYES (17,42,40,14) */}
          <button onClick={onYes}
            style={{ ...btnStyle, left: SX(17), top: SY(42), width: SX(40), height: SY(14), fontWeight: 'bold' }}>
            続ける
          </button>

          {/* PUSHBUTTON "やめる(&N)" IDNO (69,42,40,14) */}
          <button onClick={onNo}
            style={{ ...btnStyle, left: SX(69), top: SY(42), width: SX(40), height: SY(14) }}>
            やめる
          </button>

        </div>
      </div>
    </div>
  )
}
