/**
 * CMJLeadDlg 相当 — リード表示 (点数1位達成演出) (AP-09 §3-3-3)
 * レガシー: legacy/client/HgMajak2/MJLeadDlg.h/cpp
 *
 * ウィンドウ: MoveWindow(0,0,419,516) → 419×516px, CenterWindow(GetParent())
 *
 * CMJGetCoinDlg と同一パターン (7日間表示抑制 / チェックボックス付き)
 *
 * ── 画像配置 (OnCreate / OnPaint より — 座標すべてレガシー準拠) ─────────────
 *
 * 背景 (1フレーム 419×516):
 *   mj_pop_high_base.png  at (0, 0)
 *   内容テキスト / "一週間このウィンドウを開かない" ラベルは背景に焼き込み済み
 *
 * ×とじる ボタン (4フレーム 88×32):
 *   mj_shp_btn_close.png  at (166, 468)  IDOK → OnOK
 *
 * 「一週間このウィンドウを開かない」チェックボックス (4フレーム 14×14):
 *   mj_pop_check.png  at (300, 447)  IDC_CHECK_NOOPEN
 *   ラベルテキストは背景に焼き込み済み
 *
 * ── 動作ロジック (OnInitDialog / OnOK より) ──────────────────────────────
 * NeedsToShow(): localStorage "mj_leadDlg_{key}_ymd" で判定
 *   前回表示日から7日未満なら表示しない (GetDays 相当)
 * OnOK():
 *   チェックありの場合 → 今日の日付を localStorage に保存
 *   チェックなしの場合 → localStorage のキーを削除
 * ────────────────────────────────────────────────────────────────────────
 */
import { useState, useEffect } from 'react'
const LS_PREFIX = 'mj_leadDlg_'

/** GetDays 相当: 日付から通算日数 */
function getDays(y: number, m: number, d: number): number {
  if (m <= 2) { --y; m += 12 }
  const c = Math.floor(y / 100)
  return 365 * (y - 1) + Math.floor(y / 4) - c + Math.floor(c / 4) +
         Math.floor((m * 979 - 1033) / 32) + d - 1
}

/** 前回表示から7日以内かチェック */
function isWithin7Days(storageKey: string): boolean {
  const stored = localStorage.getItem(LS_PREFIX + storageKey + '_ymd')
  if (!stored) return false
  const prev = Number(stored)
  const now = new Date()
  const today = getDays(now.getFullYear(), now.getMonth() + 1, now.getDate())
  const past  = getDays(Math.floor(prev / 10000), Math.floor((prev % 10000) / 100), prev % 100)
  return (today - past) < 7
}

function needsToShow(storageKey: string): boolean {
  return !isWithin7Days(storageKey)
}

interface Props {
  storageKey?: string
  onClose: () => void
}

export default function LeadDlg({ storageKey = 'default', onClose }: Props) {
  const [noOpen,  setNoOpen]  = useState(false)
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
      <section className="majak-popup-panel majak-reward-notice majak-lead-notice" role="dialog" aria-modal="true" aria-labelledby="lead-dialog-title">
        <header id="lead-dialog-title" className="majak-popup-titlebar"><span>本格レートに挑戦しよう！</span><button className="majak-popup-titlebar__close" type="button" onClick={handleClose} aria-label="閉じる">×</button></header>
        <div className="majak-popup-body majak-reward-notice__body majak-lead-notice__body">
          <section className="majak-lead-notice__eligibility" aria-label="入場条件">
            <p className="majak-reward-notice__success">本格レートに入れる条件がそろっています。</p>
            <p>ここから入場可能です。</p>
          </section>
          <section className="majak-lead-notice__benefits" aria-label="プレイ報酬">
            <p>プレイするたびに麻雀称号や技を獲得！</p>
            <p>麻雀称号と技はコレクションページで確認できます。</p>
          </section>
        </div>
        <footer className="majak-popup-actions majak-reward-notice__actions">
          <label><input type="checkbox" checked={noOpen} onChange={event => setNoOpen(event.target.checked)} />一週間このメッセージを表示しない</label>
          <button type="button" className="is-primary" onClick={handleClose}>閉じる</button>
        </footer>
      </section>
    </div>
  )
}

/** 外部参照用エクスポート */
export { needsToShow as leadDlgNeedsShow }
