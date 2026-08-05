/**
 * CMJCircleOptDlg 相当 — サークル (グループ対戦) オプション設定 (AP-09 §3-1-6)
 * レガシー: legacy/client/HgMajak2/MJCircleOptDlg.h/cpp
 *
 * ── 機能 ─────────────────────────────────────────────────────────────
 *   サークル対戦に使用するサークルを最大3つ選択する。
 *   「サークルを使用しない」チェックボックスで全サークル選択を無効化できる。
 *
 * ── コントロール ──────────────────────────────────────────────────────
 *   IDC_OPT_CIRCLE_DDWN1/2/3: コンボボックス (サークル選択) m_nCircle1/2/3
 *   IDC_OPT_CIRCLE_UNSEL:     チェックボックス m_bDisableCircleMode
 *   IDOK:    OK — 選択したサークルIDを返す (サークル未選択なら無効)
 *   IDCANCEL: キャンセル
 *
 * ── ロジック (OnUnselectCircle / OnOptChanged 相当) ─────────────────
 *   m_bDisableCircleMode=true  → コンボボックス無効 / OK有効
 *   m_bDisableCircleMode=false → コンボボックス有効 / サークル選択後OK有効
 * ─────────────────────────────────────────────────────────────────────
 */
import { useState } from 'react'

/** サークル情報 (CHgMJApp::CIRCLEINFO 相当) */
export interface CircleInfo {
  circleId:   string
  circleName: string
}

interface Props {
  circles:  CircleInfo[]
  onOK:     (circleIds: [string | null, string | null, string | null]) => void
  onCancel: () => void
}

export default function CircleOptDlg({ circles, onOK, onCancel }: Props) {
  const [disableMode, setDisableMode] = useState(false)
  const [sel1, setSel1] = useState<string | null>(null)
  const [sel2, setSel2] = useState<string | null>(null)
  const [sel3, setSel3] = useState<string | null>(null)

  /** OnOptChanged 相当: サークル選択 → OK 有効 */
  const circleSelected = sel1 !== null || sel2 !== null || sel3 !== null

  /** OK 有効条件: disableMode または サークル選択済み */
  const okEnabled = disableMode || circleSelected

  /** OnOK 相当 */
  const handleOK = () => {
    if (disableMode) {
      onOK([null, null, null])
    } else {
      onOK([sel1, sel2, sel3])
    }
  }

  // IDD_OPTION_CIRCLE_DLG: 180×180 DU → client 270×292px + titlebar 22px
  // DU→px: 1DU_x=1.5px, 1DU_y=1.625px
  const SX = (du: number) => Math.round(du * 1.5)
  const SY = (du: number) => Math.round(du * 1.625)
  const FONT   = 'var(--majak-font-family-ui)'
  const DLG_BG = '#d4d0c8'
  const DLG_W  = SX(180)  // 270
  const DLG_H  = SY(180)  // 292
  const TITLE_H = 22

  const btnStyle = (def: boolean, disabled?: boolean): React.CSSProperties => ({
    position: 'absolute', fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#000',
    background: DLG_BG,
    borderTop: '2px solid #fff', borderLeft: '2px solid #fff',
    borderRight: '2px solid #808080', borderBottom: '2px solid #808080',
    cursor: disabled ? 'not-allowed' : 'pointer', outline: 'none',
    fontWeight: def ? 'bold' : 'normal',
    opacity: disabled ? 0.5 : 1,
  })

  const GB = ({ x, y, w, h, label }: { x: number; y: number; w: number; h: number; label: string }) => (
    <fieldset style={{
      position: 'absolute', left: x, top: y, width: w, height: h,
      border: '1px solid #767676', margin: 0, padding: 0, minWidth: 0,
      pointerEvents: 'none',
    }}>
      <legend style={{ fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#000', padding: '0 3px', marginLeft: 4 }}>
        {label}
      </legend>
    </fieldset>
  )

  const makeSelect = (val: string | null, setter: (v: string | null) => void, yOffset: number) => (
    <select
      value={val ?? ''}
      disabled={disableMode}
      onChange={e => setter(e.target.value || null)}
      style={{
        position: 'absolute', left: SX(10), top: yOffset,
        width: SX(160), height: SY(14),
        fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#000',
        background: disableMode ? '#bbb' : '#fff',
        border: '1px solid #767676',
        opacity: disableMode ? 0.5 : 1,
      }}
    >
      <option value="">-- 未選択 --</option>
      {circles.map(c => (
        <option key={c.circleId} value={c.circleId}>{c.circleName}</option>
      ))}
    </select>
  )

  return (
    <div style={{
      position: 'absolute', inset: 0, zIndex: 200,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'rgba(0,0,0,0.4)',
    }}>
      {/* IDD_OPTION_CIRCLE_DLG ウィンドウ (DS_MODALFRAME|WS_CAPTION) */}
      <div style={{ position: 'relative', width: DLG_W, boxShadow: '3px 3px 8px rgba(0,0,0,0.6)' }}>

        {/* タイトルバー CAPTION "サークルの設定" */}
        <div style={{
          height: TITLE_H,
          background: 'linear-gradient(to right, #000080, #1060d0)',
          display: 'flex', alignItems: 'center', paddingLeft: 8,
        }}>
          <span style={{ fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#fff', fontWeight: 'bold' }}>
            サークルの設定
          </span>
        </div>

        {/* クライアントエリア: 270×292px */}
        <div style={{ position: 'relative', width: DLG_W, height: DLG_H, background: DLG_BG }}>

          {/* GROUPBOX "サークル1" (5,5,170,32) */}
          <GB x={SX(5)} y={SY(5)} w={SX(170)} h={SY(32)} label="サークル1" />
          {/* COMBOBOX IDC_OPT_CIRCLE_DDWN1 (10,17,160,90) — 展開高さ90DU, 制御高さ14DU */}
          {makeSelect(sel1, setSel1, SY(5) + 14)}

          {/* GROUPBOX "サークル2" (5,45,170,32) */}
          <GB x={SX(5)} y={SY(45)} w={SX(170)} h={SY(32)} label="サークル2" />
          {makeSelect(sel2, setSel2, SY(45) + 14)}

          {/* GROUPBOX "サークル3" (5,85,170,32) */}
          <GB x={SX(5)} y={SY(85)} w={SX(170)} h={SY(32)} label="サークル3" />
          {makeSelect(sel3, setSel3, SY(85) + 14)}

          {/* CHECKBOX "サークルを指定しない" IDC_OPT_CIRCLE_UNSEL (10,120,120,30) */}
          <label style={{
            position: 'absolute', left: SX(10), top: SY(120),
            display: 'flex', alignItems: 'center', gap: 4,
            fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#000',
            cursor: 'pointer', userSelect: 'none',
          }}>
            <input type="checkbox" checked={disableMode}
              onChange={e => setDisableMode(e.target.checked)} style={{ margin: 0 }} />
            サークルを指定しない
          </label>

          {/* DEFPUSHBUTTON "OK" (40,155,50,15) */}
          <button onClick={handleOK} disabled={!okEnabled}
            style={{ ...btnStyle(true, !okEnabled), left: SX(40), top: SY(155), width: SX(50), height: SY(15) }}>
            OK
          </button>

          {/* PUSHBUTTON "キャンセル" (95,155,50,15) */}
          <button onClick={onCancel}
            style={{ ...btnStyle(false), left: SX(95), top: SY(155), width: SX(50), height: SY(15) }}>
            キャンセル
          </button>

        </div>
      </div>
    </div>
  )
}
