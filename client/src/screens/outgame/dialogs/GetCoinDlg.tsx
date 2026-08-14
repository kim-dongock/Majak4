/**
 * CMJGetCoinDlg 相当 — マネー獲得通知 (AP-09 §3-2-13)
 * レガシー: legacy/client/HgMajak2/MJGetCoinDlg.h/cpp
 *
 * ウィンドウ: MoveWindow(rc.left+146, rc.top+192, 508, 366) → 508×366px
 * (親ウィンドウの左上 + オフセット)
 *
 * 表示制御: 前回表示から7日以内は非表示 (registry → localStorage)
 *
 * ── 画像配置 (OnCreate / OnPaint より — 座標すべてレガシー準拠) ─────────────
 *
 * 背景 (1フレーム 508×366):
 *   mj_ive_window_07.png  at (0, 0)
 *   "無料マネー獲得！" タイトル, マネー画像, 説明テキスト,
 *   "一週間このウィンドウを開かない" テキスト は背景に焼き込み済み
 *
 * ×とじる ボタン (4フレーム 88×32):
 *   mj_shp_btn_close.png  at (210, 297)  IDOK → OnOK
 *   背景の "×とじる" 表示位置に重ねて配置
 *
 * 「一週間このウィンドウを開かない」チェックボックス (4フレーム 14×14):
 *   mj_pop_check.png  at (349, 338)  IDC_CHECK_NOOPEN
 *   背景のチェック欄位置に重ねて配置
 *   ※ チェックボックスラベルは背景に焼き込み済み
 *
 * ── 動作ロジック (OnInitDialog / OnOK より) ──────────────────────────────
 * NeedsToShowToday(): localStorage "mj_getCoinDlg_{key}_ymd" で判定
 *   前回表示日から7日未満なら表示しない (GetDays 相当)
 * OnOK():
 *   チェックありの場合 → 今日の日付を localStorage に保存
 *   チェックなしの場合 → localStorage のキーを削除
 * ────────────────────────────────────────────────────────────────────────
 */
import { useState, useEffect } from 'react'

interface Props {
  /** m_szKey — localStorage のキー識別子 */
  storageKey?: string
  onClose: () => void
}

const LS_PREFIX = 'mj_getCoinDlg_'

/** GetDays 相当: 日付から通算日数を計算 (1年1月1日からの経過日) */
function getDays(y: number, m: number, d: number): number {
  if (m <= 2) { --y; m += 12 }
  const dy = 365 * (y - 1)
  const c  = Math.floor(y / 100)
  const dl = Math.floor(y / 4) - c + Math.floor(c / 4)
  const dm = Math.floor((m * 979 - 1033) / 32)
  return dy + dl + dm + d - 1
}

/** 前回表示から 7 日以内かチェック */
function isWithin7Days(storageKey: string): boolean {
  const lsKey = LS_PREFIX + storageKey + '_ymd'
  const stored = localStorage.getItem(lsKey)
  if (!stored) return false
  const prev = Number(stored)
  const py = Math.floor(prev / 10000)
  const pm = Math.floor((prev % 10000) / 100)
  const pd = prev % 100
  const now = new Date()
  const today = getDays(now.getFullYear(), now.getMonth() + 1, now.getDate())
  const past  = getDays(py, pm, pd)
  return (today - past) < 7
}

function needsToShow(storageKey: string): boolean {
  return !isWithin7Days(storageKey)
}

export default function GetCoinDlg({ storageKey = 'default', onClose }: Props) {
  const [noOpen, setNoOpen] = useState(false)
  const [visible, setVisible] = useState(false)

  /* OnInitDialog 相当: 7日以内に表示済みなら即閉じる */
  useEffect(() => {
    if (!needsToShow(storageKey)) {
      onClose()
    } else {
      setVisible(true)
    }
  }, [storageKey, onClose])

  /** OnOK() 相当: チェック状態を localStorage に保存 */
  const handleClose = () => {
    const lsKey = LS_PREFIX + storageKey + '_ymd'
    if (noOpen) {
      const now = new Date()
      const ymd = now.getFullYear() * 10000 + (now.getMonth() + 1) * 100 + now.getDate()
      localStorage.setItem(lsKey, String(ymd))
    } else {
      localStorage.removeItem(lsKey)
    }
    onClose()
  }

  if (!visible) return null

  return (
    <div className="majak-popup-overlay">
      <section className="majak-popup-panel majak-reward-notice" role="dialog" aria-modal="true" aria-label="無料GP補充">
        <div className="majak-popup-body majak-reward-notice__body majak-free-gp-notice__body">
          <section className="majak-free-gp-notice__summary" aria-label="補充完了">
            <p className="majak-reward-notice__success">GPが補充されました。</p>
            <p>GPは交流広場・段位戦の場代に使用します。</p>
          </section>
          <div className="majak-free-gp-notice__rules">
            <p className="majak-free-gp-notice__rule">所持GPが1,000 GP未満の時、1日1回1,000 GPまで補充できます。</p>
          </div>
          <p className="majak-free-gp-notice__reset">無料GP補充の切り替え時間は午前6時です。</p>
        </div>
        <footer className="majak-popup-actions majak-reward-notice__actions">
          <label><input type="checkbox" checked={noOpen} onChange={event => setNoOpen(event.target.checked)} />一週間このウィンドウを開かない</label>
          <button type="button" className="is-primary" onClick={handleClose}>閉じる</button>
        </footer>
      </section>
    </div>
  )
}

/** needsToShow を外部から参照できるようにエクスポート */
export { needsToShow as coinDlgNeedsShow }
