/**
 * CMJCfgDlgEx 相当 — クライアント設定ダイアログ (タブ付き) (AP-09 §3-1-4/5)
 * レガシー: legacy/client/HgMajak2/MJCfgDlg.h/cpp
 *
 * IDD_CONFIG_DLG_EX: DIALOGEX 0,0,246,250  CAPTION "設定"
 *   DS_SETFONT|DS_MODALFRAME|WS_POPUP|WS_CAPTION  FONT 9,"MS UI Gothic"
 *   DEFPUSHBUTTON  "OK"        IDOK      (135,230,50,14)
 *   PUSHBUTTON     "キャンセル" IDCANCEL  (190,230,50,14)
 *   Tab1 IDC_TAB SysTabControl32          (5,5,235,220)
 *     Tab 0 "環境設定" = IDD_CONFIG_DLG   (230×195 DU, DS_CONTROL|WS_CHILD)
 *     Tab 1 "アシスト" = IDD_CONFIG_DLG3  (230×169 DU, DS_CONTROL|WS_CHILD)
 *   ※ CMJCfgDlg2 "マッチに対戦" は 2014/10/31 に削除 (#if 0)
 *
 * AP-11 §8: カスタム .him 不使用 → 全コントロール HTML 標準
 *   DU→px: 9pt "MS UI Gothic" @96dpi  1DU_x=1.5px, 1DU_y=1.625px
 *   GROUPBOX           → <fieldset><legend>
 *   BS_AUTOCHECKBOX    → <input type="checkbox">
 *   BS_AUTORADIOBUTTON → <input type="radio">
 *   msctls_trackbar32  → <input type="range">
 *   LTEXT              → <div>
 *   DEFPUSHBUTTON / PUSHBUTTON → <button>  (スプライト不使用)
 *
 * IDD_CONFIG_DLG (Tab 0, 230×195 DU):
 *   GROUPBOX "サウンド"                    (5,6,85,60)
 *     IDC_CHKBGM "BGM"                    (10,19,30,10)
 *     IDC_CHKSND "効果音"                  (10,34,34,10)
 *     IDC_CHKPIO "チャット音"              (15,49,40,10)  ← SND OFF 時 disabled
 *     IDC_VOLBGM msctls_trackbar32         (43,20,45,10)
 *     IDC_VOLSND msctls_trackbar32         (43,35,45,10)
 *   GROUPBOX "牌譜の記録"                 (5,70,85,60)
 *     IDC_SELREC0 "記録しない"             (10,84,47,10)
 *     IDC_SELREC1 "自分の対局のみ記録"     (10,99,78,10)
 *     IDC_SELREC2 "観戦した対局も記録"     (10,114,74,10)
 *   GROUPBOX "オートパス"                  (95,6,130,60)
 *     IDC_SELPAS0 "毎局解除する"           (100,19,56,10)
 *     IDC_SELPAS1 "毎局設定する（超光速では解除）" (100,34,110,10)
 *     IDC_SELPAS2 "毎局設定する（超光速でも設定）" (100,48,120,10)
 *   GROUPBOX "ツモ切り"                    (95,70,130,30)
 *     IDC_CHKAUT "立直時に設定する"        (100,84,69,10)
 *   GROUPBOX "パスに使用するキー"          (95,105,130,45)
 *     IDC_SELPASKEY0 "[Enter]・[Space]・[Num 0]を使用する" (100,118,121,10)
 *     IDC_SELPASKEY1 "[↑]を使用する"                      (100,133,121,10)
 *
 * IDD_CONFIG_DLG3 (Tab 1, 230×169 DU):
 *   IDC_CHKTAP "手出し/自摸切り表示"      (10,15,78,10)
 *     LTEXT "手出しと自摸切りの区別を手牌に残します" (20,30,122,8)
 *   IDC_CHKPAI "隣接牌表示"               (10,50,50,10)
 *     LTEXT "マウスでポイントしている牌と隣接牌を強調表示します" (20,65,156,8)
 *   IDC_CHKTNP "聴牌表示"                 (10,85,43,10)
 *     LTEXT "捨てたときに聴牌になる牌にマークを表示します" (20,101,139,8)
 *   IDC_CHKHOR "和了表示"                 (10,120,43,10)
 *     LTEXT "カーソルが合っている牌を捨てたときの\n各待ち牌の残り枚数と確定翻数を表示します" (20,135,196,19)
 */
import { useState } from 'react'
import { GAME_ASSIST_CONFIG_EVENT, toGameAssistConfig } from '../../../game/assistConfig'
import { useOutgameLayoutMode } from '../../../hooks/useOutgameLayoutMode'

// DU→px 変換 (9pt "MS UI Gothic" @96dpi: baseX=6px → 6/4=1.5, baseY=13px → 13/8=1.625)
const SX = (du: number) => Math.round(du * 1.5)
const SY = (du: number) => Math.round(du * 1.625)

const FONT = 'var(--majak-font-family-ui)'
const DLG_BG = '#d4d0c8'   // Windows classic dialog gray
const TITLE_BG = '#f0f0f0'

export interface MJConfig {
  bChkBGM: boolean   // IDC_CHKBGM
  bChkSND: boolean   // IDC_CHKSND
  bChkPIO: boolean   // IDC_CHKPIO  (SND OFF 時 disabled)
  nVolBGM: number    // IDC_VOLBGM 0–255
  nVolSND: number    // IDC_VOLSND 0–255
  nChkPAS: number    // IDC_SELPAS0 0=なし 1=簡易 2=毎回
  bChkAUT: boolean   // IDC_CHKAUT
  nSelPasKey: number // IDC_SELPASKEY0 0=標準 1=テンキー
  nChkREC: number    // IDC_SELREC0 0=しない 1=自動 2=常時
  bChkPai: boolean   // IDC_CHKPAI (ver=0 default: true)
  bChkTap: boolean   // IDC_CHKTAP
  bChkTnp: boolean   // IDC_CHKTNP
  bChkHor: boolean   // IDC_CHKHOR (ver=0 default: true)
}

export const DEFAULT_CONFIG: MJConfig = {
  bChkBGM: true, bChkSND: true, bChkPIO: true,
  nVolBGM: 255, nVolSND: 255,
  nChkPAS: 0, bChkAUT: false, nSelPasKey: 0, nChkREC: 0,
  bChkPai: true, bChkTap: true, bChkTnp: true, bChkHor: true,
}

const CONFIG_STORAGE_KEY = 'majak2.config'
const BOOLEAN_CONFIG_KEYS = ['bChkBGM', 'bChkSND', 'bChkPIO', 'bChkAUT', 'bChkPai', 'bChkTap', 'bChkTnp', 'bChkHor'] as const
const NUMBER_CONFIG_KEYS = ['nVolBGM', 'nVolSND', 'nChkPAS', 'nSelPasKey', 'nChkREC'] as const

function clampInt(value: unknown, min: number, max: number, fallback: number): number {
  const number = Number(value)
  if (!Number.isFinite(number)) return fallback
  return Math.min(max, Math.max(min, Math.trunc(number)))
}

function normalizeConfig(value: unknown): MJConfig {
  const source = value && typeof value === 'object' ? value as Partial<MJConfig> : {}
  const next: MJConfig = { ...DEFAULT_CONFIG }
  for (const key of BOOLEAN_CONFIG_KEYS) {
    if (typeof source[key] === 'boolean') next[key] = source[key]
  }
  for (const key of NUMBER_CONFIG_KEYS) {
    const max = key === 'nVolBGM' || key === 'nVolSND' ? 255 : key === 'nSelPasKey' ? 1 : 2
    next[key] = clampInt(source[key], 0, max, DEFAULT_CONFIG[key])
  }
  return next
}

export function loadMajakConfig(): MJConfig {
  if (typeof window === 'undefined') return { ...DEFAULT_CONFIG }
  try {
    const raw = window.localStorage.getItem(CONFIG_STORAGE_KEY)
    return raw ? normalizeConfig(JSON.parse(raw)) : { ...DEFAULT_CONFIG }
  } catch {
    return { ...DEFAULT_CONFIG }
  }
}

export function saveMajakConfig(cfg: MJConfig): void {
  if (typeof window === 'undefined') return
  const normalized = normalizeConfig(cfg)
  try {
    window.localStorage.setItem(CONFIG_STORAGE_KEY, JSON.stringify(normalized))
  } catch {
    // localStorage can be unavailable in private or embedded contexts.
  }
  window.dispatchEvent(new CustomEvent(GAME_ASSIST_CONFIG_EVENT, {
    detail: toGameAssistConfig(normalized),
  }))
}

interface Props {
  initial: MJConfig
  onOK: (cfg: MJConfig) => void
  onCancel: () => void
  /** OnModify 相当 — BGM/SE/Vol 変更時の即時反映 (任意) */
  onModify?: (cfg: MJConfig) => void
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

// BS_AUTOCHECKBOX → <label><input type="checkbox">
function Chk({ x, y, label, checked, disabled, onChange }: {
  x: number; y: number; label: string
  checked: boolean; disabled?: boolean; onChange: (v: boolean) => void
}) {
  return (
    <label style={{
      position: 'absolute', left: x, top: y,
      display: 'flex', alignItems: 'center', gap: 4,
      fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#000',
      cursor: disabled ? 'default' : 'pointer',
      opacity: disabled ? 0.5 : 1,
      userSelect: 'none', whiteSpace: 'nowrap',
    }}>
      <input type="checkbox" checked={checked} disabled={disabled}
        onChange={e => onChange(e.target.checked)}
        style={{ margin: 0 }} />
      {label}
    </label>
  )
}

// BS_AUTORADIOBUTTON → <label><input type="radio">
function Rad({ x, y, name, val, label, checked, onChange }: {
  x: number; y: number; name: string; val: number; label: string
  checked: boolean; onChange: (v: number) => void
}) {
  return (
    <label style={{
      position: 'absolute', left: x, top: y,
      display: 'flex', alignItems: 'center', gap: 4,
      fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#000',
      cursor: 'pointer', userSelect: 'none', whiteSpace: 'nowrap',
    }}>
      <input type="radio" name={name} value={val} checked={checked}
        onChange={() => onChange(val)} style={{ margin: 0 }} />
      {label}
    </label>
  )
}

// LTEXT → <div>
function LTxt({ x, y, w, text }: { x: number; y: number; w: number; text: string }) {
  return (
    <div style={{
      position: 'absolute', left: x, top: y, width: w,
      fontFamily: FONT, fontSize: 'calc(11px * var(--majak-type-scale))', color: '#000',
      lineHeight: '1.3', whiteSpace: 'pre-wrap',
    }}>
      {text}
    </div>
  )
}

export default function CfgDlg({ initial, onOK, onCancel, onModify }: Props) {
  const layoutMode = useOutgameLayoutMode()
  const [cfg, setCfg] = useState<MJConfig>({ ...initial })
  const [activeTab, setActiveTab] = useState(0)

  const set = <K extends keyof MJConfig>(k: K, v: MJConfig[K]) => {
    const next = { ...cfg, [k]: v }
    setCfg(next)
    onModify?.(next)
  }

  if (layoutMode === 'mobileLandscape') {
    return (
      <div className="majak-mobile-dialog-overlay">
        <div className="majak-mobile-config-dialog majak-mobile-dialog-panel">
          <div className="majak-mobile-dialog-titlebar">設定</div>
          <div className="majak-mobile-config-tabs" role="tablist" aria-label="設定">
            {(['環境設定', 'アシスト'] as const).map((label, index) => (
              <button
                key={label}
                type="button"
                className={activeTab === index ? 'is-active' : undefined}
                onClick={() => setActiveTab(index)}
              >{label}</button>
            ))}
          </div>
          <div className="majak-mobile-dialog-body majak-mobile-config-body">
            {activeTab === 0 ? (
              <>
                <fieldset className="majak-mobile-dialog-section majak-mobile-config-section">
                  <legend>サウンド</legend>
                  <label className="majak-mobile-choice"><input type="checkbox" checked={cfg.bChkBGM} onChange={event => set('bChkBGM', event.target.checked)} />BGM</label>
                  <input className="majak-mobile-config-range" type="range" min={0} max={255} value={cfg.nVolBGM} onChange={event => set('nVolBGM', +event.target.value)} />
                  <label className="majak-mobile-choice"><input type="checkbox" checked={cfg.bChkSND} onChange={event => set('bChkSND', event.target.checked)} />効果音</label>
                  <input className="majak-mobile-config-range" type="range" min={0} max={255} value={cfg.nVolSND} onChange={event => set('nVolSND', +event.target.value)} />
                  <label className="majak-mobile-choice"><input type="checkbox" checked={cfg.bChkPIO} disabled={!cfg.bChkSND} onChange={event => set('bChkPIO', event.target.checked)} />チャット音</label>
                </fieldset>

                <fieldset className="majak-mobile-dialog-section majak-mobile-config-section">
                  <legend>牌譜の記録</legend>
                  <div className="majak-mobile-choice-grid majak-mobile-choice-grid--one">
                    <label className="majak-mobile-choice"><input type="radio" name="nChkREC-mobile" checked={cfg.nChkREC === 0} onChange={() => set('nChkREC', 0)} />記録しない</label>
                    <label className="majak-mobile-choice"><input type="radio" name="nChkREC-mobile" checked={cfg.nChkREC === 1} onChange={() => set('nChkREC', 1)} />自分の対局のみ記録</label>
                    <label className="majak-mobile-choice"><input type="radio" name="nChkREC-mobile" checked={cfg.nChkREC === 2} onChange={() => set('nChkREC', 2)} />観戦した対局も記録</label>
                  </div>
                </fieldset>

                <fieldset className="majak-mobile-dialog-section majak-mobile-config-section">
                  <legend>オートパス</legend>
                  <div className="majak-mobile-choice-grid majak-mobile-choice-grid--one">
                    <label className="majak-mobile-choice"><input type="radio" name="nChkPAS-mobile" checked={cfg.nChkPAS === 0} onChange={() => set('nChkPAS', 0)} />毎局解除する</label>
                    <label className="majak-mobile-choice"><input type="radio" name="nChkPAS-mobile" checked={cfg.nChkPAS === 1} onChange={() => set('nChkPAS', 1)} />毎局設定する（超光速では解除）</label>
                    <label className="majak-mobile-choice"><input type="radio" name="nChkPAS-mobile" checked={cfg.nChkPAS === 2} onChange={() => set('nChkPAS', 2)} />毎局設定する（超光速でも設定）</label>
                  </div>
                </fieldset>

                <fieldset className="majak-mobile-dialog-section majak-mobile-config-section">
                  <legend>ツモ切り</legend>
                  <label className="majak-mobile-choice"><input type="checkbox" checked={cfg.bChkAUT} onChange={event => set('bChkAUT', event.target.checked)} />立直時に設定する</label>
                </fieldset>

                <fieldset className="majak-mobile-dialog-section majak-mobile-config-section majak-mobile-config-section--wide">
                  <legend>パスに使用するキー</legend>
                  <div className="majak-mobile-choice-grid majak-mobile-choice-grid--two">
                    <label className="majak-mobile-choice"><input type="radio" name="nSelPasKey-mobile" checked={cfg.nSelPasKey === 0} onChange={() => set('nSelPasKey', 0)} />[Enter]・[Space]・[Num 0]を使用する</label>
                    <label className="majak-mobile-choice"><input type="radio" name="nSelPasKey-mobile" checked={cfg.nSelPasKey === 1} onChange={() => set('nSelPasKey', 1)} />[↑]を使用する</label>
                  </div>
                </fieldset>
              </>
            ) : (
              <div className="majak-mobile-config-assist">
                <label className="majak-mobile-config-assist-row"><span><input type="checkbox" checked={cfg.bChkTap} onChange={event => set('bChkTap', event.target.checked)} />手出し/自摸切り表示</span><small>手出しと自摸切りの区別を手牌に残します</small></label>
                <label className="majak-mobile-config-assist-row"><span><input type="checkbox" checked={cfg.bChkPai} onChange={event => set('bChkPai', event.target.checked)} />隣接牌表示</span><small>マウスでポイントしている牌と隣接牌を強調表示します</small></label>
                <label className="majak-mobile-config-assist-row"><span><input type="checkbox" checked={cfg.bChkTnp} onChange={event => set('bChkTnp', event.target.checked)} />聴牌表示</span><small>捨てたときに聴牌になる牌にマークを表示します</small></label>
                <label className="majak-mobile-config-assist-row"><span><input type="checkbox" checked={cfg.bChkHor} onChange={event => set('bChkHor', event.target.checked)} />和了表示</span><small>カーソルが合っている牌を捨てたときの各待ち牌の残り枚数と確定翻数を表示します</small></label>
              </div>
            )}
          </div>
          <div className="majak-mobile-dialog-actions">
            <button type="button" onClick={() => onOK(cfg)}>OK</button>
            <button type="button" onClick={() => { onModify?.(initial); onCancel() }}>キャンセル</button>
          </div>
        </div>
      </div>
    )
  }

  // IDD_CONFIG_DLG_EX: 246×250 DU → client 369×406px + titlebar 22px
  const DLG_W = SX(246)  // 369
  const DLG_H = SY(250)  // 406
  const TITLE_H = 22
  // Tab content area: SY(220)=358 - tab header 24px = 334px
  const TAB_CONTENT_H = SY(220) - 24

  return (
    <div style={{
      position: 'absolute', inset: 0, zIndex: 210,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'rgba(0,0,0,0.25)',
    }}>
      {/* IDD_CONFIG_DLG_EX ウィンドウ (DS_MODALFRAME|WS_CAPTION) */}
      <div style={{ position: 'relative', width: DLG_W, height: TITLE_H + DLG_H, background: DLG_BG, border: '1px solid #808080', boxShadow: '3px 3px 8px rgba(0,0,0,0.45)' }}>

        {/* ── タイトルバー (CAPTION "設定") ── */}
        <div style={{
          position: 'absolute', left: 0, top: 0, width: DLG_W, boxSizing: 'border-box',
          height: TITLE_H,
          background: TITLE_BG,
          display: 'flex', alignItems: 'center', paddingLeft: 8,
        }}>
          <span style={{ fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#111' }}>設定</span>
        </div>

        {/* ── クライアントエリア: 369×406px ── */}
        <div style={{ position: 'absolute', left: 0, top: TITLE_H, width: DLG_W, height: DLG_H, background: DLG_BG }}>

          {/* IDC_TAB SysTabControl32 (5,5,235,220) */}
          <div style={{
            position: 'absolute',
            left: SX(5), top: SY(5),
            width: SX(235), height: SY(220),
          }}>
            {/* タブヘッダー (TCN_SELCHANGE 相当) */}
            <div style={{ display: 'flex', alignItems: 'flex-end', paddingLeft: 4 }}>
              {(['環境設定', 'アシスト'] as const).map((label, i) => (
                <button key={i} onClick={() => setActiveTab(i)} style={{
                  padding: '2px 10px',
                  fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#000',
                  background: activeTab === i ? DLG_BG : '#bbb',
                  border: '1px solid #808080',
                  borderBottom: activeTab === i ? `1px solid ${DLG_BG}` : '1px solid #808080',
                  marginRight: 2,
                  cursor: 'pointer', outline: 'none',
                  position: 'relative', bottom: activeTab === i ? -1 : 0,
                  zIndex: activeTab === i ? 2 : 1,
                }}>{label}</button>
              ))}
            </div>

            {/* タブコンテンツ枠 */}
            <div style={{
              position: 'relative',
              border: '1px solid #808080',
              background: DLG_BG,
              height: TAB_CONTENT_H,
              overflow: 'hidden',
            }}>

              {/* ============================================================
                  Tab 0: IDD_CONFIG_DLG (230×195 DU)
                  ============================================================ */}
              {activeTab === 0 && (
                <div style={{ position: 'relative', width: SX(230), height: SY(195) }}>

                  {/* GROUPBOX "サウンド" (5,6,85,60) */}
                  <GB x={SX(5)} y={SY(6)} w={SX(85)} h={SY(60)} label="サウンド" />
                  {/* IDC_CHKBGM "BGM" (10,19) — OnChkBGM→OnModify 相当 */}
                  <Chk x={SX(10)} y={SY(19)} label="BGM"
                    checked={cfg.bChkBGM}
                    onChange={v => set('bChkBGM', v)} />
                  {/* IDC_CHKSND "効果音" (10,34) — OnChkSND: EnableWindow(CHKPIO) のみ */}
                  <Chk x={SX(10)} y={SY(34)} label="効果音"
                    checked={cfg.bChkSND} onChange={v => set('bChkSND', v)} />
                  {/* IDC_CHKPIO "チャット音" (15,49) — SND OFF 時 disabled */}
                  <Chk x={SX(15)} y={SY(49)} label="チャット音"
                    checked={cfg.bChkPIO} disabled={!cfg.bChkSND}
                    onChange={v => set('bChkPIO', v)} />
                  {/* IDC_VOLBGM (43,20,45,10) — OnHScroll→OnModify 相当 */}
                  <input type="range" min={0} max={255} value={cfg.nVolBGM}
                    onChange={e => set('nVolBGM', +e.target.value)}
                    style={{ position: 'absolute', left: SX(43), top: SY(20), width: SX(45), height: SY(10), margin: 0 }} />
                  {/* IDC_VOLSND (43,35,45,10) */}
                  <input type="range" min={0} max={255} value={cfg.nVolSND}
                    onChange={e => set('nVolSND', +e.target.value)}
                    style={{ position: 'absolute', left: SX(43), top: SY(35), width: SX(45), height: SY(10), margin: 0 }} />

                  {/* GROUPBOX "牌譜の記録" (5,70,85,60) */}
                  <GB x={SX(5)} y={SY(70)} w={SX(85)} h={SY(60)} label="牌譜の記録" />
                  {/* IDC_SELREC0 "記録しない" (10,84) */}
                  <Rad x={SX(10)} y={SY(84)} name="nChkREC" val={0} label="記録しない"
                    checked={cfg.nChkREC === 0} onChange={v => set('nChkREC', v)} />
                  {/* IDC_SELREC1 "自分の対局のみ記録" (10,99) */}
                  <Rad x={SX(10)} y={SY(99)} name="nChkREC" val={1} label="自分の対局のみ記録"
                    checked={cfg.nChkREC === 1} onChange={v => set('nChkREC', v)} />
                  {/* IDC_SELREC2 "観戦した対局も記録" (10,114) */}
                  <Rad x={SX(10)} y={SY(114)} name="nChkREC" val={2} label="観戦した対局も記録"
                    checked={cfg.nChkREC === 2} onChange={v => set('nChkREC', v)} />

                  {/* GROUPBOX "オートパス" (95,6,130,60) */}
                  <GB x={SX(95)} y={SY(6)} w={SX(130)} h={SY(60)} label="オートパス" />
                  {/* IDC_SELPAS0 "毎局解除する" (100,19) */}
                  <Rad x={SX(100)} y={SY(19)} name="nChkPAS" val={0} label="毎局解除する"
                    checked={cfg.nChkPAS === 0} onChange={v => set('nChkPAS', v)} />
                  {/* IDC_SELPAS1 "毎局設定する（超光速では解除）" (100,34) */}
                  <Rad x={SX(100)} y={SY(34)} name="nChkPAS" val={1} label="毎局設定する（超光速では解除）"
                    checked={cfg.nChkPAS === 1} onChange={v => set('nChkPAS', v)} />
                  {/* IDC_SELPAS2 "毎局設定する（超光速でも設定）" (100,48) */}
                  <Rad x={SX(100)} y={SY(48)} name="nChkPAS" val={2} label="毎局設定する（超光速でも設定）"
                    checked={cfg.nChkPAS === 2} onChange={v => set('nChkPAS', v)} />

                  {/* GROUPBOX "ツモ切り" (95,70,130,30) */}
                  <GB x={SX(95)} y={SY(70)} w={SX(130)} h={SY(30)} label="ツモ切り" />
                  {/* IDC_CHKAUT "立直時に設定する" (100,84) */}
                  <Chk x={SX(100)} y={SY(84)} label="立直時に設定する"
                    checked={cfg.bChkAUT} onChange={v => set('bChkAUT', v)} />

                  {/* GROUPBOX "パスに使用するキー" (95,105,130,45) */}
                  <GB x={SX(95)} y={SY(105)} w={SX(130)} h={SY(45)} label="パスに使用するキー" />
                  {/* IDC_SELPASKEY0 "[Enter]・[Space]・[Num 0]を使用する" (100,118) */}
                  <Rad x={SX(100)} y={SY(118)} name="nSelPasKey" val={0}
                    label="[Enter]・[Space]・[Num 0]を使用する"
                    checked={cfg.nSelPasKey === 0} onChange={v => set('nSelPasKey', v)} />
                  {/* IDC_SELPASKEY1 "[↑]を使用する" (100,133) */}
                  <Rad x={SX(100)} y={SY(133)} name="nSelPasKey" val={1}
                    label="[↑]を使用する"
                    checked={cfg.nSelPasKey === 1} onChange={v => set('nSelPasKey', v)} />

                </div>
              )}

              {/* ============================================================
                  Tab 1: IDD_CONFIG_DLG3 (230×169 DU)
                  ============================================================ */}
              {activeTab === 1 && (
                <div style={{ position: 'relative', width: SX(230), height: SY(169) }}>

                  {/* IDC_CHKTAP "手出し/自摸切り表示" (10,15,78,10) */}
                  <Chk x={SX(10)} y={SY(15)} label="手出し/自摸切り表示"
                    checked={cfg.bChkTap} onChange={v => set('bChkTap', v)} />
                  {/* LTEXT (20,30,122,8) */}
                  <LTxt x={SX(20)} y={SY(30)} w={SX(122)}
                    text="手出しと自摸切りの区別を手牌に残します" />

                  {/* IDC_CHKPAI "隣接牌表示" (10,50,50,10) */}
                  <Chk x={SX(10)} y={SY(50)} label="隣接牌表示"
                    checked={cfg.bChkPai} onChange={v => set('bChkPai', v)} />
                  {/* LTEXT (20,65,156,8) */}
                  <LTxt x={SX(20)} y={SY(65)} w={SX(156)}
                    text="マウスでポイントしている牌と隣接牌を強調表示します" />

                  {/* IDC_CHKTNP "聴牌表示" (10,85,43,10) */}
                  <Chk x={SX(10)} y={SY(85)} label="聴牌表示"
                    checked={cfg.bChkTnp} onChange={v => set('bChkTnp', v)} />
                  {/* LTEXT (20,101,139,8) */}
                  <LTxt x={SX(20)} y={SY(101)} w={SX(139)}
                    text="捨てたときに聴牌になる牌にマークを表示します" />

                  {/* IDC_CHKHOR "和了表示" (10,120,43,10) */}
                  <Chk x={SX(10)} y={SY(120)} label="和了表示"
                    checked={cfg.bChkHor} onChange={v => set('bChkHor', v)} />
                  {/* LTEXT (20,135,196,19) */}
                  <LTxt x={SX(20)} y={SY(135)} w={SX(196)}
                    text={"カーソルが合っている牌を捨てたときの\n各待ち牌の残り枚数と確定翻数を表示します"} />

                </div>
              )}
            </div>
          </div>

          {/* DEFPUSHBUTTON "OK" (135,230,50,14) */}
          <button onClick={() => onOK(cfg)} style={{
            position: 'absolute', left: SX(135), top: SY(230),
            width: SX(50), height: SY(14),
            fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#000', background: DLG_BG,
            borderTop: '2px solid #fff', borderLeft: '2px solid #fff',
            borderRight: '2px solid #808080', borderBottom: '2px solid #808080',
            cursor: 'pointer', outline: 'none',
          }}>OK</button>

          {/* PUSHBUTTON "キャンセル" (190,230,50,14) */}
          <button onClick={() => { onModify?.(initial); onCancel() }} style={{
            position: 'absolute', left: SX(190), top: SY(230),
            width: SX(50), height: SY(14),
            fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#000', background: DLG_BG,
            borderTop: '2px solid #fff', borderLeft: '2px solid #fff',
            borderRight: '2px solid #808080', borderBottom: '2px solid #808080',
            cursor: 'pointer', outline: 'none',
          }}>キャンセル</button>

        </div>
      </div>
    </div>
  )
}
