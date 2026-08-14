/**
 * CPaifuSaveDlg 相当 — 牌譜ファイル保存ダイアログ (AP-09 §3-1-8)
 * レガシー: legacy/client/HgMajak2/PaifuSaveDlg.h/cpp
 *
 * CPaifuSaveDlg extends CFileDialog — ファイル保存ダイアログの拡張
 * Web 版ではブラウザのダウンロード機能を使用する。
 *
 * ── コントロール (DoDataExchange より) ─────────────────────────────
 *   IDC_RADIO_WHOLE  : ラジオボタン m_bKyoku=0 → 全体 (半荘全体)
 *   IDC_RADIO_KYOKU  : ラジオボタン m_bKyoku=1 → 現在の局のみ
 *   IDC_COMMENT      : テキスト入力 m_strComment → コメント
 *   IDOK:    OK — 選択されたファイル名で保存 (ブラウザダウンロード)
 *   IDCANCEL: キャンセル
 *
 * ── OnFileNameOK 相当 ─────────────────────────────────────────────
 *   UpdateData(TRUE) でフォームデータ取得
 *   onSave(fileName, bKyoku, comment) コールバック
 * ─────────────────────────────────────────────────────────────────────
 */
import { useState } from 'react'

interface Props {
  /** デフォルトファイル名 */
  defaultFileName?: string
  /** m_strComment 初期値 */
  initialComment?: string
  onSave:   (fileName: string, bKyoku: boolean, comment: string) => void
  onCancel: () => void
}

export default function PaifuSaveDlg({
  defaultFileName = 'Majak2Paifu.txt',
  initialComment = '',
  onSave,
  onCancel,
}: Props) {
  /** m_bKyoku: false=全体 / true=現在の局のみ */
  const [bKyoku,   setBKyoku]   = useState(false)
  /** m_strComment */
  const [comment,  setComment]  = useState(initialComment)
  const [fileName, setFileName] = useState(defaultFileName)

  /** OnFileNameOK 相当 — フォームデータ検証後 onSave */
  const handleOK = () => {
    const trimmed = fileName.trim() || defaultFileName
    const name = /\.[^\\/.]+$/.test(trimmed) ? trimmed : `${trimmed}.txt`
    onSave(name, bKyoku, comment)
  }

  return (
    <div className="majak-popup-overlay">
      <section className="majak-popup-panel majak-paifu-save-dialog" role="dialog" aria-modal="true" aria-labelledby="paifu-save-dialog-title">
        <header id="paifu-save-dialog-title" className="majak-popup-titlebar"><span>牌譜の保存</span><button className="majak-popup-titlebar__close" type="button" onClick={onCancel} aria-label="閉じる">×</button></header>
        <div className="majak-popup-body majak-paifu-save-dialog__body">
          <label className="majak-mobile-dialog-field">
            <span>ファイル名</span>
            <input
              type="text"
              value={fileName}
              onChange={e => setFileName(e.target.value)}
              placeholder="Majak2Paifu.txt"
            />
          </label>
          <fieldset className="majak-mobile-dialog-section majak-paifu-save-dialog__scope">
            <legend>保存範囲</legend>
            <label className="majak-mobile-choice"><input type="radio" name="kyoku" checked={!bKyoku} onChange={() => setBKyoku(false)} />読み込まれた牌譜全体を保存</label>
            <label className="majak-mobile-choice"><input type="radio" name="kyoku" checked={bKyoku} onChange={() => setBKyoku(true)} />再生中の局の牌譜のみ保存</label>
          </fieldset>
          <label className="majak-mobile-dialog-field">
            <span>コメント</span>
            <input
              type="text"
              value={comment}
              onChange={e => setComment(e.target.value)}
            />
          </label>
        </div>
        <footer className="majak-popup-actions">
          <button type="button" onClick={onCancel}>キャンセル</button>
          <button type="button" className="is-primary" onClick={handleOK}>保存</button>
        </footer>
      </section>
    </div>
  )
}
