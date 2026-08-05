/**
 * CMJOptDlg 相当 — ルームオプション設定ダイアログ (AP-09 §3-1-4)
 * レガシー: legacy/client/HgMajak2/MJOptDlg.h/cpp
 *
 * IDD_OPTION_DLG: DIALOG 0,0,183,239  CAPTION "部屋の設定"
 *   DS_SETFONT|DS_MODALFRAME|WS_POPUP|WS_CAPTION  FONT 9,"MS UI Gothic"
 *   COMBOBOX  IDC_OPT_CONTEST      (5,5,170,30)   WS_DISABLED
 *   GROUPBOX  "対局戦"             (5,20,120,25)
 *     IDC_OPTSET0 "東南戦"         (10,30,35,10)
 *     IDC_OPTSET1 "東風戦"         (47,30,38,10)
 *   GROUPBOX  "ウマ(順位ボーナス)" (5,50,120,25)
 *     IDC_OPTUMA0 "5-10"           (10,60,34,10)
 *     IDC_OPTUMA1 "10-20"          (47,60,34,10)
 *     IDC_OPTUMA2 "10-30"          (84,60,34,10)
 *   GROUPBOX  "槓"                 (5,80,120,25)
 *     IDC_OPTRON0 "喰い"           (10,90,35,10)
 *     IDC_OPTRON1 "ダブル槓"       (45,90,38,10)
 *     IDC_OPTRON2 "延長槓"         (85,90,38,10)
 *   IDC_OPTKUI "クイタン有り"      (130,30,50,10)
 *   IDC_OPTTOR "手を鳴らす有効"    (130,50,50,10)
 *   IDC_OPTTIP "チップ有り"        (130,70,47,10)
 *   IDC_OPTWAR "鳴き有り"          (130,90,47,10)
 *   GROUPBOX  "赤牌"               (5,110,170,25)
 *     IDC_OPTRED0 "無し"           (10,120,29,10)
 *     IDC_OPTRED1 "各１枚"         (47,120,36,10)
 *     IDC_OPTRED2 "五筒２枚"       (84,120,43,10)
 *   GROUPBOX  "スピード"           (5,139,170,25)
 *     IDC_OPTSPD0 "ゆっくり"       (10,150,37,10)
 *     IDC_OPTSPD1 "サクサク"       (47,150,37,10)
 *     IDC_OPTSPD2 "普通"           (84,150,31,10)
 *     IDC_OPTSPD3 "高速"           (121,150,37,10)
 *   IDC_OPT_ENABLECHAT "チャット音を出す"             (10,174,94,10)
 *   IDC_OPT_OPENHAND   "観戦者に手牌を公開する"       (10,190,94,10)
 *   IDC_OPT_VIEWCHAT   "観戦者に対局者とのチャットを許可する" (10,206,137,10)
 *   DEFPUSHBUTTON "OK"        IDOK     (40,221,50,14)
 *   PUSHBUTTON    "キャンセル" IDCANCEL (95,221,50,14)
 *
 * AP-11 §8: カスタム .him 不使用 → 全コントロール HTML 標準
 * DU→px: 1DU_x=1.5px, 1DU_y=1.625px
 *   GROUPBOX    → <fieldset><legend>
 *   BS_AUTORADIOBUTTON / BS_AUTOCHECKBOX → <input type="radio|checkbox">
 *   DEFPUSHBUTTON / PUSHBUTTON → <button>
 *
 * mask フィールドが >=0 の場合、対応項目を無効化 (GetMask 相当)
 */
import { useState } from 'react'

// DU→px (9pt "MS UI Gothic" @96dpi)
const SX = (du: number) => Math.round(du * 1.5)
const SY = (du: number) => Math.round(du * 1.625)
const FONT   = 'var(--majak-font-family-ui)'
const DLG_BG = '#d4d0c8'
const OPT_TO_DLG_RED = [0, 2, 1] as const
const DLG_TO_OPT_RED = [0, 2, 1] as const

export interface MJOption {
  nSet: number       // IDC_OPTSET0/1: 0=東南戦 1=東風戦
  nUma: number       // IDC_OPTUMA0-2: 0=5-10 1=10-20 2=10-30
  bKui: boolean      // IDC_OPTKUI: クイタン有り
  bTor: boolean      // IDC_OPTTOR: 手を鳴らす有効
  bWar: boolean      // IDC_OPTWAR: 鳴き有り
  bTip: boolean      // IDC_OPTTIP: チップ有り
  nRon: number       // IDC_OPTRON0-2: 0=喰い 1=ダブル槓 2=延長槓
  nSpd: number       // IDC_OPTSPD0-3: 0=ゆっくり 1=サクサク 2=普通 3=高速
  bEnableChat: boolean  // IDC_OPT_ENABLECHAT
  bOpenHand: boolean    // IDC_OPT_OPENHAND
  bViewChat: boolean    // IDC_OPT_VIEWCHAT
  nRed: number       // IDC_OPTRED0-2: 0=無し 1=1枚 2=独自2枚
  nContest: number   // IDC_OPT_CONTEST
}

export const DEFAULT_OPTION: MJOption = {
  nSet: 1, nUma: 2, bKui: false, bTor: false, bWar: false,
  bTip: false, nRon: 0, nSpd: 2, bEnableChat: false,
  bOpenHand: false, bViewChat: false, nRed: 2, nContest: 0,
}

export function optionToString(opt: MJOption): string {
  return [
    opt.nSet,
    opt.nUma,
    opt.nSpd,
    opt.bKui ? 1 : 0,
    opt.bTor ? 1 : 0,
    opt.nRed,
    opt.bOpenHand ? 1 : 0,
    opt.bViewChat ? 1 : 0,
    opt.nContest,
    0,
    opt.bWar ? 1 : 0,
    opt.bTip ? 1 : 0,
    opt.nRon,
    0,
    opt.bEnableChat ? 1 : 0,
  ].join('')
}

export interface MJOptionMask {
  nSet?: number; nUma?: number; bKui?: number; bTor?: number; bWar?: number
  bTip?: number; nRon?: number; nSpd?: number; bEnableChat?: number
  bOpenHand?: number; bViewChat?: number; nRed?: number; nContest?: number
}

interface Props {
  initial: MJOption
  mask?: MJOptionMask
  /** 観戦者機能有無 (SetViewerEnable 相当) */
  viewerEnable?: boolean
  onOK: (opt: MJOption) => void
  onCancel: () => void
}

// GROUPBOX → <fieldset><legend>
function GB({ x, y, w, h, label }: { x: number; y: number; w: number; h: number; label: string }) {
  return (
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
}

// BS_AUTORADIOBUTTON
function Rad({ x, y, name, val, label, checked, disabled, onChange }: {
  x: number; y: number; name: string; val: number; label: string
  checked: boolean; disabled?: boolean; onChange: (v: number) => void
}) {
  return (
    <label style={{
      position: 'absolute', left: x, top: y,
      display: 'flex', alignItems: 'center', gap: 3,
      fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#000',
      cursor: disabled ? 'default' : 'pointer',
      opacity: disabled ? 0.5 : 1, userSelect: 'none', whiteSpace: 'nowrap',
    }}>
      <input type="radio" name={name} value={val} checked={checked} disabled={disabled}
        onChange={() => !disabled && onChange(val)} style={{ margin: 0 }} />
      {label}
    </label>
  )
}

// BS_AUTOCHECKBOX
function Chk({ x, y, label, checked, disabled, onChange }: {
  x: number; y: number; label: string
  checked: boolean; disabled?: boolean; onChange: (v: boolean) => void
}) {
  return (
    <label style={{
      position: 'absolute', left: x, top: y,
      display: 'flex', alignItems: 'center', gap: 3,
      fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#000',
      cursor: disabled ? 'default' : 'pointer',
      opacity: disabled ? 0.5 : 1, userSelect: 'none', whiteSpace: 'nowrap',
    }}>
      <input type="checkbox" checked={checked} disabled={disabled}
        onChange={e => !disabled && onChange(e.target.checked)} style={{ margin: 0 }} />
      {label}
    </label>
  )
}

function applyOptionMask(option: MJOption, mask: MJOptionMask, viewerEnable: boolean): MJOption {
  const next = { ...option }
  const applyNumber = <K extends keyof MJOption>(key: K) => {
    const value = mask[key as keyof MJOptionMask]
    if (value !== undefined && value >= 0) next[key] = value as MJOption[K]
  }
  const applyBoolean = <K extends keyof MJOption>(key: K) => {
    const value = mask[key as keyof MJOptionMask]
    if (value !== undefined && value >= 0) next[key] = (value !== 0) as MJOption[K]
  }

  applyNumber('nSet')
  applyNumber('nUma')
  applyNumber('nSpd')
  applyNumber('nRed')
  applyNumber('nRon')
  applyNumber('nContest')
  applyBoolean('bKui')
  applyBoolean('bTor')
  applyBoolean('bWar')
  applyBoolean('bTip')
  applyBoolean('bEnableChat')

  if (viewerEnable) {
    applyBoolean('bOpenHand')
    applyBoolean('bViewChat')
    if (!next.bEnableChat) next.bViewChat = false
  } else {
    next.bOpenHand = false
    next.bViewChat = false
  }

  return next
}

export default function OptDlg({ initial, mask = {}, viewerEnable = true, onOK, onCancel }: Props) {
  const [opt, setOpt] = useState<MJOption>(() => applyOptionMask(initial, mask, viewerEnable))

  const set = <K extends keyof MJOption>(k: K, v: MJOption[K]) =>
    setOpt(o => {
      const next = { ...o, [k]: v }
      if (k === 'bEnableChat' && v === false)
        next.bViewChat = false
      return next
    })

  /** GetMask 相当: mask[key] >= 0 → disabled */
  const dis = (key: keyof MJOptionMask) =>
    mask[key] !== undefined && (mask[key] as number) >= 0

  const dlgRed = OPT_TO_DLG_RED[opt.nRed as 0 | 1 | 2] ?? 0
  const setDlgRed = (v: number) => set('nRed', (DLG_TO_OPT_RED[v as 0 | 1 | 2] ?? 0) as MJOption['nRed'])

  {
    return (
      <div className="majak-mobile-dialog-overlay">
        <div className="majak-mobile-option-dialog majak-mobile-dialog-panel">
          <div className="majak-mobile-dialog-titlebar">部屋の設定</div>
          <div className="majak-mobile-dialog-body majak-mobile-option-body">
            <fieldset className="majak-mobile-dialog-section">
              <legend>対戦種別</legend>
              <div className="majak-mobile-choice-grid majak-mobile-choice-grid--two">
                <label className="majak-mobile-choice"><input type="radio" name="nSet-mobile" checked={opt.nSet === 0} disabled={dis('nSet')} onChange={() => set('nSet', 0)} />東風戦</label>
                <label className="majak-mobile-choice"><input type="radio" name="nSet-mobile" checked={opt.nSet === 1} disabled={dis('nSet')} onChange={() => set('nSet', 1)} />半荘戦</label>
              </div>
            </fieldset>

            <fieldset className="majak-mobile-dialog-section">
              <legend>ウマ(順位ボーナス)</legend>
              <div className="majak-mobile-choice-grid majak-mobile-choice-grid--three">
                <label className="majak-mobile-choice"><input type="radio" name="nUma-mobile" checked={opt.nUma === 0} disabled={dis('nUma')} onChange={() => set('nUma', 0)} />5-10</label>
                <label className="majak-mobile-choice"><input type="radio" name="nUma-mobile" checked={opt.nUma === 1} disabled={dis('nUma')} onChange={() => set('nUma', 1)} />10-20</label>
                <label className="majak-mobile-choice"><input type="radio" name="nUma-mobile" checked={opt.nUma === 2} disabled={dis('nUma')} onChange={() => set('nUma', 2)} />10-30</label>
              </div>
            </fieldset>

            <fieldset className="majak-mobile-dialog-section">
              <legend>あがり</legend>
              <div className="majak-mobile-choice-grid majak-mobile-choice-grid--three">
                <label className="majak-mobile-choice"><input type="radio" name="nRon-mobile" checked={opt.nRon === 0} disabled={dis('nRon')} onChange={() => set('nRon', 0)} />頭ハネ</label>
                <label className="majak-mobile-choice"><input type="radio" name="nRon-mobile" checked={opt.nRon === 1} disabled={dis('nRon')} onChange={() => set('nRon', 1)} />ダブロン</label>
                <label className="majak-mobile-choice"><input type="radio" name="nRon-mobile" checked={opt.nRon === 2} disabled={dis('nRon')} onChange={() => set('nRon', 2)} />トリロン</label>
              </div>
            </fieldset>

            <fieldset className="majak-mobile-dialog-section">
              <legend>赤牌</legend>
              <div className="majak-mobile-choice-grid majak-mobile-choice-grid--three">
                <label className="majak-mobile-choice"><input type="radio" name="nRed-mobile" checked={opt.nRed === 0} disabled={dis('nRed')} onChange={() => set('nRed', 0)} />無し</label>
                <label className="majak-mobile-choice"><input type="radio" name="nRed-mobile" checked={dlgRed === 1} disabled={dis('nRed')} onChange={() => setDlgRed(1)} />各１枚</label>
                <label className="majak-mobile-choice"><input type="radio" name="nRed-mobile" checked={dlgRed === 2} disabled={dis('nRed')} onChange={() => setDlgRed(2)} />五筒２枚</label>
              </div>
            </fieldset>

            <fieldset className="majak-mobile-dialog-section">
              <legend>スピード</legend>
              <div className="majak-mobile-choice-grid majak-mobile-choice-grid--four">
                <label className="majak-mobile-choice"><input type="radio" name="nSpd-mobile" checked={opt.nSpd === 0} disabled={dis('nSpd')} onChange={() => set('nSpd', 0)} />超光速</label>
                <label className="majak-mobile-choice"><input type="radio" name="nSpd-mobile" checked={opt.nSpd === 1} disabled={dis('nSpd')} onChange={() => set('nSpd', 1)} />サクサク</label>
                <label className="majak-mobile-choice"><input type="radio" name="nSpd-mobile" checked={opt.nSpd === 2} disabled={dis('nSpd')} onChange={() => set('nSpd', 2)} />標準</label>
                <label className="majak-mobile-choice"><input type="radio" name="nSpd-mobile" checked={opt.nSpd === 3} disabled={dis('nSpd')} onChange={() => set('nSpd', 3)} />ゆったり</label>
              </div>
            </fieldset>

            <fieldset className="majak-mobile-dialog-section majak-mobile-dialog-section--checks">
              <div className="majak-mobile-choice-grid majak-mobile-choice-grid--three">
                <label className="majak-mobile-choice"><input type="checkbox" checked={opt.bKui} disabled={dis('bKui')} onChange={event => set('bKui', event.target.checked)} />クイタン無し</label>
                <label className="majak-mobile-choice"><input type="checkbox" checked={opt.bTor} disabled={dis('bTor')} onChange={event => set('bTor', event.target.checked)} />焼き鳥有り</label>
                <label className="majak-mobile-choice"><input type="checkbox" checked={opt.bTip} disabled={dis('bTip')} onChange={event => set('bTip', event.target.checked)} />チップ有り</label>
                <label className="majak-mobile-choice"><input type="checkbox" checked={opt.bWar} disabled={dis('bWar')} onChange={event => set('bWar', event.target.checked)} />ワレメ有り</label>
                <label className="majak-mobile-choice"><input type="checkbox" checked={opt.bEnableChat} disabled={dis('bEnableChat')} onChange={event => set('bEnableChat', event.target.checked)} />チャットを許可する</label>
                <label className="majak-mobile-choice"><input type="checkbox" checked={opt.bOpenHand} disabled={!viewerEnable || dis('bOpenHand')} onChange={event => set('bOpenHand', event.target.checked)} />観戦者に手牌を公開する</label>
                <label className="majak-mobile-choice majak-mobile-choice--wide"><input type="checkbox" checked={opt.bViewChat} disabled={!viewerEnable || dis('bViewChat') || !opt.bEnableChat} onChange={event => set('bViewChat', event.target.checked)} />観戦者に対局者とのチャットを許可する</label>
              </div>
            </fieldset>
          </div>
          <div className="majak-mobile-dialog-actions">
            <button type="button" onClick={() => onOK(applyOptionMask(opt, mask, viewerEnable))}>OK</button>
            <button type="button" onClick={onCancel}>キャンセル</button>
          </div>
        </div>
      </div>
    )
  }

  // IDD_OPTION_DLG: 183×239 DU client area; caption is rendered separately.
  const DLG_W = SX(183)  // 274
  const DLG_H = SY(239)  // 388
  const TITLE_H = 20

  const btnStyle: React.CSSProperties = {
    position: 'absolute', fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#000',
    background: DLG_BG,
    borderTop: '2px solid #fff', borderLeft: '2px solid #fff',
    borderRight: '2px solid #808080', borderBottom: '2px solid #808080',
    cursor: 'pointer', outline: 'none',
  }

  return (
    <div style={{
      position: 'absolute', inset: 0, zIndex: 200,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'rgba(0,0,0,0.4)',
    }}>
      {/* IDD_OPTION_DLG ウィンドウ (DS_MODALFRAME|WS_CAPTION) */}
      <div style={{ position: 'relative', width: DLG_W, height: TITLE_H + DLG_H, background: DLG_BG, border: '1px solid #808080', boxShadow: '3px 3px 8px rgba(0,0,0,0.6)' }}>

        {/* タイトルバー CAPTION "部屋の設定" */}
        <div style={{
          position: 'absolute', left: 0, top: 0, width: DLG_W, boxSizing: 'border-box',
          height: TITLE_H,
          background: '#f0f0f0',
          display: 'flex', alignItems: 'center', paddingLeft: 8,
        }}>
          <span style={{ fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#111' }}>
            部屋の設定
          </span>
        </div>

        {/* クライアントエリア: 274×388px */}
        <div style={{ position: 'absolute', left: 0, top: TITLE_H, width: DLG_W, height: DLG_H, background: DLG_BG }}>

          {/* COMBOBOX IDC_OPT_CONTEST (5,5,170,30) — disabled */}
          <select
            value={opt.nContest}
            disabled
            onChange={() => undefined}
            style={{
              position: 'absolute', left: SX(5), top: SY(5), width: DLG_W - SX(10), height: SY(14),
              fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', boxSizing: 'border-box',
              border: '1px inset #d4d0c8', background: '#d4d0c8', color: '#808080',
            }}
          >
            <option value={opt.nContest}>ハンゲ荘ルール</option>
          </select>

          {/* GROUPBOX "対戦種別" (5,20,120,25) */}
          <GB x={SX(5)} y={SY(20)} w={SX(120)} h={SY(25)} label="対戦種別" />
          {/* IDC_OPTSET0 "東風戦" (10,30) */}
          <Rad x={SX(10)} y={SY(30)} name="nSet" val={0} label="東風戦"
            checked={opt.nSet === 0} disabled={dis('nSet')} onChange={v => set('nSet', v)} />
          {/* IDC_OPTSET1 "半荘戦" (47,30) */}
          <Rad x={SX(47)} y={SY(30)} name="nSet" val={1} label="半荘戦"
            checked={opt.nSet === 1} disabled={dis('nSet')} onChange={v => set('nSet', v)} />

          {/* GROUPBOX "ウマ(順位ボーナス)" (5,50,120,25) */}
          <GB x={SX(5)} y={SY(50)} w={SX(120)} h={SY(25)} label="ウマ(順位ボーナス)" />
          {/* IDC_OPTUMA0 "5-10" (10,60) */}
          <Rad x={SX(10)} y={SY(60)} name="nUma" val={0} label="5-10"
            checked={opt.nUma === 0} disabled={dis('nUma')} onChange={v => set('nUma', v)} />
          {/* IDC_OPTUMA1 "10-20" (47,60) */}
          <Rad x={SX(47)} y={SY(60)} name="nUma" val={1} label="10-20"
            checked={opt.nUma === 1} disabled={dis('nUma')} onChange={v => set('nUma', v)} />
          {/* IDC_OPTUMA2 "10-30" (84,60) */}
          <Rad x={SX(84)} y={SY(60)} name="nUma" val={2} label="10-30"
            checked={opt.nUma === 2} disabled={dis('nUma')} onChange={v => set('nUma', v)} />

          {/* GROUPBOX "あがり" (5,80,120,25) */}
          <GB x={SX(5)} y={SY(80)} w={SX(120)} h={SY(25)} label="あがり" />
          {/* IDC_OPTRON0 "頭ハネ" (10,90) */}
          <Rad x={SX(10)} y={SY(90)} name="nRon" val={0} label="頭ハネ"
            checked={opt.nRon === 0} disabled={dis('nRon')} onChange={v => set('nRon', v)} />
          {/* IDC_OPTRON1 "ダブロン" (45,90) */}
          <Rad x={SX(45)} y={SY(90)} name="nRon" val={1} label="ダブロン"
            checked={opt.nRon === 1} disabled={dis('nRon')} onChange={v => set('nRon', v)} />
          {/* IDC_OPTRON2 "トリロン" (85,90) */}
          <Rad x={SX(85)} y={SY(90)} name="nRon" val={2} label="トリロン"
            checked={opt.nRon === 2} disabled={dis('nRon')} onChange={v => set('nRon', v)} />

          {/* IDC_OPTKUI "クイタン無し" (130,30) ← 右列: GroupBox 外 */}
          <Chk x={SX(130)} y={SY(30)} label="クイタン無し"
            checked={opt.bKui} disabled={dis('bKui')} onChange={v => set('bKui', v)} />
          {/* IDC_OPTTOR "焼き鳥有り" (130,50) */}
          <Chk x={SX(130)} y={SY(50)} label="焼き鳥有り"
            checked={opt.bTor} disabled={dis('bTor')} onChange={v => set('bTor', v)} />
          {/* IDC_OPTTIP "チップ有り" (130,70) */}
          <Chk x={SX(130)} y={SY(70)} label="チップ有り"
            checked={opt.bTip} disabled={dis('bTip')} onChange={v => set('bTip', v)} />
          {/* IDC_OPTWAR "ワレメ有り" (130,90) */}
          <Chk x={SX(130)} y={SY(90)} label="ワレメ有り"
            checked={opt.bWar} disabled={dis('bWar')} onChange={v => set('bWar', v)} />

          {/* GROUPBOX "赤牌" (5,110,170,25) */}
          <GB x={SX(5)} y={SY(110)} w={SX(170)} h={SY(25)} label="赤牌" />
          {/* IDC_OPTRED0 "無し" (10,120) */}
          <Rad x={SX(10)} y={SY(120)} name="nRed" val={0} label="無し"
            checked={opt.nRed === 0} disabled={dis('nRed')} onChange={v => set('nRed', v)} />
          {/* IDC_OPTRED1 "各１枚" (47,120) */}
          <Rad x={SX(47)} y={SY(120)} name="nRed" val={1} label="各１枚"
            checked={dlgRed === 1} disabled={dis('nRed')} onChange={setDlgRed} />
          {/* IDC_OPTRED2 "五筒２枚" (84,120) */}
          <Rad x={SX(84)} y={SY(120)} name="nRed" val={2} label="五筒２枚"
            checked={dlgRed === 2} disabled={dis('nRed')} onChange={setDlgRed} />

          {/* GROUPBOX "スピード" (5,139,170,25) */}
          <GB x={SX(5)} y={SY(139)} w={SX(170)} h={SY(25)} label="スピード" />
          {/* IDC_OPTSPD0 "超光速" (10,150) */}
          <Rad x={SX(10)} y={SY(150)} name="nSpd" val={0} label="超光速"
            checked={opt.nSpd === 0} disabled={dis('nSpd')} onChange={v => set('nSpd', v)} />
          {/* IDC_OPTSPD1 "サクサク" (47,150) */}
          <Rad x={SX(47)} y={SY(150)} name="nSpd" val={1} label="サクサク"
            checked={opt.nSpd === 1} disabled={dis('nSpd')} onChange={v => set('nSpd', v)} />
          {/* IDC_OPTSPD2 "標準" (84,150) */}
          <Rad x={SX(84)} y={SY(150)} name="nSpd" val={2} label="標準"
            checked={opt.nSpd === 2} disabled={dis('nSpd')} onChange={v => set('nSpd', v)} />
          {/* IDC_OPTSPD3 "ゆったり" (121,150) */}
          <Rad x={SX(121)} y={SY(150)} name="nSpd" val={3} label="ゆったり"
            checked={opt.nSpd === 3} disabled={dis('nSpd')} onChange={v => set('nSpd', v)} />

          {/* IDC_OPT_ENABLECHAT "チャットを許可する" (10,174,94,10) */}
          <Chk x={SX(10)} y={SY(174)} label="チャットを許可する"
            checked={opt.bEnableChat} disabled={dis('bEnableChat')} onChange={v => set('bEnableChat', v)} />

          {/* IDC_OPT_OPENHAND "観戦者に手牌を公開する" (10,190,94,10) */}
          <Chk x={SX(10)} y={SY(190)} label="観戦者に手牌を公開する"
            checked={opt.bOpenHand} disabled={!viewerEnable || dis('bOpenHand')} onChange={v => set('bOpenHand', v)} />

          {/* IDC_OPT_VIEWCHAT "観戦者に対局者とのチャットを許可する" (10,206,137,10) */}
          <Chk x={SX(10)} y={SY(206)} label="観戦者に対局者とのチャットを許可する"
            checked={opt.bViewChat} disabled={!viewerEnable || dis('bViewChat') || !opt.bEnableChat} onChange={v => set('bViewChat', v)} />

          {/* DEFPUSHBUTTON "OK" IDOK (40,221,50,14) */}
          <button onClick={() => onOK(applyOptionMask(opt, mask, viewerEnable))}
            style={{ ...btnStyle, left: SX(40), top: SY(221), width: SX(50), height: SY(14), fontWeight: 'bold' }}>
            OK
          </button>

          {/* PUSHBUTTON "キャンセル" IDCANCEL (95,221,50,14) */}
          <button onClick={onCancel}
            style={{ ...btnStyle, left: SX(95), top: SY(221), width: SX(50), height: SY(14) }}>
            キャンセル
          </button>

        </div>
      </div>
    </div>
  )
}
