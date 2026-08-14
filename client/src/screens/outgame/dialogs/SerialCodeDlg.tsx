/**
 * CSerialCodeDlg 相当 — シリアルコード入力ダイアログ (AP-09 §3-1-9)
 * レガシー: legacy/client/HgMajak2/SerialCodeDlg.cpp
 *
 * IDD_SERIAL_CODE_DLG: DIALOG 0,0,141,91  CAPTION "シリアルコード"
 *   DS_SETFONT|DS_MODALFRAME|WS_POPUP|WS_CAPTION  FONT 9,"MS UI Gothic"
 *   LTEXT    "シリアルコードを入力してください" IDC_STATIC (13,17,95,8)
 *   EDITTEXT IDC_EDTBOX_SERIAL_CODE             (22,31,96,16)
 *   DEFPUSHBUTTON "Send"   IDOK                 (78,66,50,14)
 *   PUSHBUTTON    "Cancel" IDCANCEL             (11,66,50,14)
 *
 * AP-11 §8: カスタム .him 不使用 → 全コントロール HTML 標準
 * DU→px: 1DU_x=1.5px, 1DU_y=1.625px
 *
 * 動作:
 *   - テキスト入力 → IDOK → 親の CMajakChannelWnd 相当へ serialCode を返す
 *   - 空入力の場合は MessageBox 表示
 *   - m_cSerialCode.LimitText(16) → maxLength=16
 */
import { useState } from 'react'
import { showMessage } from '../../../utils/msgbox'

interface Props {
  onOK: (serialCode: string) => void
  onClose: () => void
}

export default function SerialCodeDlg({ onOK, onClose }: Props) {
  const [code, setCode] = useState('')

  /**
   * IDOK — CSerialCodeDlg::OnOK 相当。
   * 空入力は MessageBox("シリアルコードが入力されていません", "入力エラー") で留まる。
   * 送信自体は親の CMajakChannelWnd::OnBtnSerialCodeClicked 相当で行う。
   */
  const handleOK = () => {
    if (code.trim().length === 0) {
      showMessage('シリアルコードが入力されていません', '入力エラー')
      return
    }
    onOK(code.trim())
    onClose()
  }

  return (
    <div className="majak-popup-overlay majak-mobile-dialog-overlay">
      <section className="majak-popup-panel majak-mobile-serial-dialog" role="dialog" aria-modal="true" aria-labelledby="serial-code-dialog-title">
        <header id="serial-code-dialog-title" className="majak-popup-titlebar"><span>シリアルコード</span><button className="majak-popup-titlebar__close" type="button" onClick={onClose} aria-label="閉じる">×</button></header>
        <div className="majak-popup-body">
          <label className="majak-mobile-dialog-field majak-mobile-serial-field">
            <span>シリアルコードを入力してください</span>
            <input
              type="text"
              value={code}
              onChange={event => setCode(event.target.value.slice(0, 16))}
              onKeyDown={event => { if (event.key === 'Enter') handleOK() }}
              maxLength={16}
              autoFocus
              inputMode="text"
            />
          </label>
        </div>
        <footer className="majak-popup-actions">
          <button type="button" onClick={onClose}>キャンセル</button>
          <button type="button" className="is-primary" onClick={handleOK}>送信</button>
        </footer>
      </section>
    </div>
  )
}
