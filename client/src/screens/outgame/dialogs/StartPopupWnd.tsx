/**
 * CMJStartPopupWnd 相当 — スタートポップアップ (AP-09 §3-1-1)
 * レガシー: legacy/client/HgMajak2/MJStartPopupWnd.h/cpp
 *
 * ウィンドウサイズ: CLIENT_W=682, CLIENT_H2=394 px (MoveWindow)
 * 背景: mj_start_popup_background.png (682×394)
 * 閉じるボタン: mj_start_popup_close.png (96×24 → 4フレーム 24×24) at (656, 2)
 * チェックボックス: check.png (56×14 → 14×14 /frame) at (262, 370)
 *   CMJChkBtn.DrawItem 方式: チェック枠(14px) + テキスト "当日はこれ以上表示しない"
 *
 * NeedsToDisplayToday() → localStorage "mj_startPopupSkipDate" で判定
 * _MarkAsToSkipDisplayingToday() → OnDestroy 時に localStorage へ書込
 */
import { useState } from 'react'
import type { GameAnnouncement } from '../../../api/announcements'

const LS_KEY = 'mj_startPopupSkipDate'

/** NeedsToDisplayToday() 相当 */
function needsToDisplayToday(): boolean {
  const stored = localStorage.getItem(LS_KEY)
  if (!stored) return true
  const today = new Date()
  const yyyymmdd = `${today.getFullYear()}${String(today.getMonth() + 1).padStart(2, '0')}${String(today.getDate()).padStart(2, '0')}`
  return stored !== yyyymmdd
}

/** _MarkAsToSkipDisplayingToday() 相当 */
function markSkipToday() {
  const today = new Date()
  const yyyymmdd = `${today.getFullYear()}${String(today.getMonth() + 1).padStart(2, '0')}${String(today.getDate()).padStart(2, '0')}`
  localStorage.setItem(LS_KEY, yyyymmdd)
}

interface Props {
  /** 閉じる時のコールバック */
  onClose: () => void
  announcement: GameAnnouncement | null
}

export default function StartPopupWnd({ onClose, announcement }: Props) {
  const [skipToday, setSkipToday] = useState(false)

  /** OnDestroy() 相当: 閉じる時にチェック状態で localStorage 書込 */
  const handleClose = () => {
    if (skipToday) markSkipToday()
    onClose()
  }



  return (
    <div
      className="majak-popup-overlay"
      onMouseDown={e => { if (e.target === e.currentTarget) handleClose() }}
    >
      <section className="majak-popup-panel majak-welcome-dialog majak-start-popup" role="dialog" aria-modal="true" aria-labelledby="start-popup-title" onMouseDown={event => event.stopPropagation()}>
        <header id="start-popup-title" className="majak-popup-titlebar"><span>お知らせ</span><button className="majak-popup-titlebar__close" type="button" onClick={handleClose} aria-label="閉じる">×</button></header>
        <div className="majak-popup-body majak-welcome-dialog__body majak-start-popup__body">
          {announcement ? <>
            <h2 className="majak-welcome-dialog__lead">{announcement.title}</h2>
            <p style={{ whiteSpace: 'pre-wrap' }}>{announcement.body}</p>
          </> : <p>現在表示するお知らせはありません。</p>}
        </div>
        <footer className="majak-popup-actions majak-start-popup__actions">
          <label><input type="checkbox" checked={skipToday} onChange={event => setSkipToday(event.target.checked)} />今日はこれを表示しない</label>
          <button type="button" className="is-primary" onClick={handleClose}>閉じる</button>
        </footer>
      </section>
    </div>
  )
}

/** needsToDisplayToday を外部からも参照できるようにエクスポート */
export { needsToDisplayToday }
