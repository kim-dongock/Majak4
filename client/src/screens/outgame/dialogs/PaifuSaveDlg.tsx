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

  // IDD_PAIFUSAVE_DLG: 283×58 DU (DS_CONTROL|WS_CHILD) → 424×94px
  // Web版では CFileDialog の代わりに独自モーダルとしてラップする
  // DU→px: 1DU_x=1.5px, 1DU_y=1.625px
  const SX = (du: number) => Math.round(du * 1.5)
  const SY = (du: number) => Math.round(du * 1.625)
  const FONT   = "'MS PGothic', 'ＭＳ Ｐゴシック', 'MS UI Gothic', sans-serif"
  const DLG_BG = '#d4d0c8'
  const CHILD_W = SX(283)  // 424 (RC child dialog width)
  const CHILD_H = SY(58)   // 94  (RC child dialog height)
  const TITLE_H = 22

  const btnStyle: React.CSSProperties = {
    fontFamily: FONT, fontSize: 12, color: '#000', background: DLG_BG,
    borderTop: '2px solid #fff', borderLeft: '2px solid #fff',
    borderRight: '2px solid #808080', borderBottom: '2px solid #808080',
    cursor: 'pointer', outline: 'none', padding: '3px 12px',
  }

  return (
    <div style={{
      position: 'absolute', inset: 0, zIndex: 200,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'rgba(0,0,0,0.4)',
    }}>
      <div style={{ position: 'relative', width: CHILD_W + 20, boxShadow: '3px 3px 8px rgba(0,0,0,0.6)' }}>

        {/* タイトルバー (Web版: CFileDialogのキャプション相当) */}
        <div style={{
          height: TITLE_H,
          background: 'linear-gradient(to right, #000080, #1060d0)',
          display: 'flex', alignItems: 'center', paddingLeft: 8,
        }}>
          <span style={{ fontFamily: FONT, fontSize: 12, color: '#fff', fontWeight: 'bold' }}>
            牌譜の保存
          </span>
        </div>

        <div style={{ background: DLG_BG, padding: '8px 10px', width: CHILD_W + 20, boxSizing: 'border-box' }}>

          {/* ファイル名 (Web版: CFileDialog の EDITTEXT IDC_FILENAME 相当) */}
          <div style={{ marginBottom: 6 }}>
            <div style={{ fontFamily: FONT, fontSize: 12, color: '#000', marginBottom: 2 }}>ファイル名</div>
            <input
              type="text"
              value={fileName}
              onChange={e => setFileName(e.target.value)}
              style={{
                width: '100%', fontFamily: FONT, fontSize: 12, color: '#000',
                background: '#fff', border: '1px solid #767676',
                outline: 'none', padding: '2px 4px', boxSizing: 'border-box',
              }}
              placeholder="Majak2Paifu.txt"
            />
          </div>

          {/* IDD_PAIFUSAVE_DLG 子ダイアログ領域 (283×58 DU) */}
          <div style={{ position: 'relative', width: CHILD_W, height: CHILD_H, marginLeft: -10 }}>

            {/* IDC_RADIO_WHOLE "読み込まれた牌譜全体を保存" (7,7,103,10) */}
            <label style={{
              position: 'absolute', left: SX(7), top: SY(7),
              display: 'flex', alignItems: 'center', gap: 3,
              fontFamily: FONT, fontSize: 12, color: '#000',
              cursor: 'pointer', userSelect: 'none', whiteSpace: 'nowrap',
            }}>
              <input type="radio" name="kyoku" checked={!bKyoku}
                onChange={() => setBKyoku(false)} style={{ margin: 0 }} />
              読み込まれた牌譜全体を保存
            </label>

            {/* IDC_RADIO_KYOKU "再生中の局の牌譜のみ保存" (7,22,98,10) */}
            <label style={{
              position: 'absolute', left: SX(7), top: SY(22),
              display: 'flex', alignItems: 'center', gap: 3,
              fontFamily: FONT, fontSize: 12, color: '#000',
              cursor: 'pointer', userSelect: 'none', whiteSpace: 'nowrap',
            }}>
              <input type="radio" name="kyoku" checked={bKyoku}
                onChange={() => setBKyoku(true)} style={{ margin: 0 }} />
              再生中の局の牌譜のみ保存
            </label>

            {/* LTEXT "コメント" (7,40,22,8) */}
            <div style={{
              position: 'absolute', left: SX(7), top: SY(40),
              fontFamily: FONT, fontSize: 12, color: '#000',
            }}>コメント</div>

            {/* EDITTEXT IDC_COMMENT (30,37,244,14) */}
            <input
              type="text"
              value={comment}
              onChange={e => setComment(e.target.value)}
              style={{
                position: 'absolute', left: SX(30), top: SY(37),
                width: SX(244), height: SY(14),
                fontFamily: FONT, fontSize: 12, color: '#000',
                background: '#fff', border: '1px solid #767676',
                outline: 'none', padding: '0 3px',
              }}
            />
          </div>

          {/* ボタン行 (Web版: CFileDialogのIDOK/IDCANCELボタン相当) */}
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 6, marginTop: 8 }}>
            <button onClick={onCancel} style={btnStyle}>キャンセル</button>
            <button onClick={handleOK} style={{ ...btnStyle, fontWeight: 'bold' }}>保存(&S)</button>
          </div>
        </div>
      </div>
    </div>
  )
}
