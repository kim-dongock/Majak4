/**
 * CHgChatNoticeDlg 相当 — チャット通報ダイアログ
 * レガシー: legacy/client/HgComM/HgChatNoticeDlg.cpp + MJAccuseManager.cpp
 */
import { useState } from 'react'
import { showMessage } from '../../../utils/msgbox'

const REASONS = [
  'めいわくな発言',
  'わいせつな発言',
  '悪口（暴言）',
  '個人情報の発言',
  '利用規約に違反する発言',
  'そのほか',
] as const

export interface AccusePayload {
  targetPix: string
  reasonIndex: number
  reason: string
  chatContent: string
}

interface Props {
  myPix: string
  myMemberName?: string
  speakers: string[]
  speakerNameById?: Map<string, string>
  chatContent: string
  onOK?: (payload: AccusePayload) => void | Promise<void>
  onClose: () => void
}

export default function AccuseDlg({ myPix, myMemberName, speakers, speakerNameById = new Map<string, string>(), chatContent, onOK, onClose }: Props) {
  const [targetPix, setTargetPix] = useState(speakers[0] ?? '')
  const [reasonIndex, setReasonIndex] = useState(0)

  const submit = async () => {
    if (!targetPix) {
      void showMessage('通報する人のIDを選択してください。')
      return
    }
    await onOK?.({ targetPix, reasonIndex, reason: REASONS[reasonIndex] ?? REASONS[0], chatContent })
    onClose()
  }

  return (
    <div className="majak-popup-overlay majak-accuse-overlay" onMouseDown={event => { if (event.target === event.currentTarget) onClose() }}>
      <form className="majak-popup-panel majak-accuse-dialog" role="dialog" aria-modal="true" aria-labelledby="majak-accuse-dialog-title" onSubmit={event => { event.preventDefault(); void submit() }}>
        <header className="majak-popup-titlebar majak-accuse-dialog__header">
          <h2 id="majak-accuse-dialog-title">通報</h2>
          <button type="button" className="majak-popup-titlebar__close" onClick={onClose} aria-label="閉じる">×</button>
        </header>
        <div className="majak-popup-body majak-accuse-dialog__body">
          <p className="majak-accuse-dialog__kicker">チャット通報</p>
          <p className="majak-accuse-dialog__notice">不適切なチャットを選んで通報します。</p>
          <label className="majak-accuse-dialog__field">
            <span>通報者</span>
            <input value={myMemberName || myPix} readOnly />
          </label>
          <label className="majak-accuse-dialog__field">
            <span>対象者</span>
            <select value={targetPix} onChange={event => setTargetPix(event.target.value)}>
              {speakers.map(pix => <option key={pix} value={pix}>{speakerNameById.get(pix) || pix}</option>)}
            </select>
          </label>
          <fieldset className="majak-accuse-dialog__reasons">
            <legend>通報理由</legend>
            <div>
              {REASONS.map((reason, index) => (
                <label key={reason}>
                  <input type="radio" name="accuseReason" checked={reasonIndex === index} onChange={() => setReasonIndex(index)} />
                  <span>{reason}</span>
                </label>
              ))}
            </div>
          </fieldset>
        </div>
        <footer className="majak-popup-actions majak-accuse-dialog__actions">
          <button type="button" onClick={onClose}>キャンセル</button>
          <button type="submit" className="is-primary">通報する</button>
        </footer>
      </form>
    </div>
  )
}