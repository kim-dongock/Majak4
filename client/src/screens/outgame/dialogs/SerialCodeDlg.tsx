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
import { useOutgameLayoutMode } from '../../../hooks/useOutgameLayoutMode'

// DU→px (9pt "MS UI Gothic" @96dpi)
const SX = (du: number) => Math.round(du * 1.5)
const SY = (du: number) => Math.round(du * 1.625)
const FONT   = 'var(--majak-font-family-ui)'
const DLG_BG = '#d4d0c8'

interface Props {
  onOK: (serialCode: string) => void
  onClose: () => void
}

export default function SerialCodeDlg({ onOK, onClose }: Props) {
  const layoutMode = useOutgameLayoutMode()
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

  if (layoutMode === 'mobileLandscape') {
    return (
      <div className="majak-mobile-dialog-overlay">
        <div className="majak-mobile-serial-dialog majak-mobile-dialog-panel">
          <div className="majak-mobile-dialog-titlebar">シリアルコード</div>
          <div className="majak-mobile-dialog-body majak-mobile-serial-body">
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
          <div className="majak-mobile-dialog-actions majak-mobile-serial-actions">
            <button type="button" onClick={onClose}>Cancel</button>
            <button type="button" onClick={handleOK}>Send</button>
          </div>
        </div>
      </div>
    )
  }

  // IDD_SERIAL_CODE_DLG: 141×91 DU → client 211×148px + titlebar 22px
  const DLG_W = SX(141)  // 211
  const DLG_H = SY(91)   // 148
  const TITLE_H = 22

  const btnStyle = (def: boolean): React.CSSProperties => ({
    position: 'absolute', fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#000',
    background: DLG_BG,
    borderTop: '2px solid #fff', borderLeft: '2px solid #fff',
    borderRight: '2px solid #808080', borderBottom: '2px solid #808080',
    cursor: 'pointer', outline: 'none',
    fontWeight: def ? 'bold' : 'normal',
  })

  return (
    <div style={{
      position: 'absolute', inset: 0, zIndex: 200,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'transparent',
    }}>
      {/* IDD_SERIAL_CODE_DLG ウィンドウ (DS_MODALFRAME|WS_CAPTION) */}
      <div style={{ position: 'relative', width: DLG_W, boxShadow: '3px 3px 8px rgba(0,0,0,0.6)' }}>

        {/* タイトルバー CAPTION "シリアルコード" */}
        <div style={{
          height: TITLE_H,
          background: 'linear-gradient(to right, #000080, #1060d0)',
          display: 'flex', alignItems: 'center', paddingLeft: 8,
        }}>
          <span style={{ fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#fff', fontWeight: 'bold' }}>
            シリアルコード
          </span>
        </div>

        {/* クライアントエリア: 211×148px */}
        <div style={{ position: 'relative', width: DLG_W, height: DLG_H, background: DLG_BG }}>

          {/* LTEXT "シリアルコードを入力してください" (13,17,95,8) */}
          <div style={{
            position: 'absolute', left: SX(13), top: SY(17), width: SX(95),
            fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#000',
          }}>
            シリアルコードを入力してください
          </div>

          {/* EDITTEXT IDC_EDTBOX_SERIAL_CODE (22,31,96,16) — m_cSerialCode.LimitText(16) */}
          <input
            type="text"
            value={code}
            onChange={e => setCode(e.target.value.slice(0, 16))}
            onKeyDown={e => { if (e.key === 'Enter') handleOK() }}
            maxLength={16}
            autoFocus
            style={{
              position: 'absolute', left: SX(22), top: SY(31),
              width: SX(96), height: SY(16),
              fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#000',
              background: '#fff',
              border: '1px solid #767676',
              outline: 'none', padding: '0 3px', boxSizing: 'border-box',
            }}
          />

          {/* DEFPUSHBUTTON "Send" IDOK (78,66,50,14) */}
          <button
            onClick={handleOK}
            style={{ ...btnStyle(true), left: SX(78), top: SY(66), width: SX(50), height: SY(14) }}
          >
            Send
          </button>

          {/* PUSHBUTTON "Cancel" IDCANCEL (11,66,50,14) */}
          <button
            onClick={onClose}
            style={{ ...btnStyle(false), left: SX(11), top: SY(66), width: SX(50), height: SY(14) }}
          >
            Cancel
          </button>

        </div>
      </div>
    </div>
  )
}
