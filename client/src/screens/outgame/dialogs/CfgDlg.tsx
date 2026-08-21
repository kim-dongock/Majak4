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
import { useOutgameLayoutMode } from '../../../hooks/useOutgameLayoutMode'
import { GAME_ASSIST_CONFIG_EVENT, toGameAssistConfig } from '../../../game/assistConfig'
import { GAME_PAIFU_RECORDING_CONFIG_EVENT } from '../../../game/paifuRecording'

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
  window.dispatchEvent(new CustomEvent(GAME_PAIFU_RECORDING_CONFIG_EVENT, {
    detail: normalized.nChkREC,
  }))
}

interface Props {
  initial: MJConfig
  onOK: (cfg: MJConfig) => void
  onCancel: () => void
  /** OnModify 相当 — BGM/SE/Vol 変更時の即時反映 (任意) */
  onModify?: (cfg: MJConfig) => void
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

  return (
      <div className={`majak-mobile-dialog-overlay majak-room-setup-overlay majak-room-setup-overlay--${layoutMode} majak-mobile-config-overlay--${layoutMode} majak-popup-overlay`}>
        <div className="majak-mobile-config-dialog majak-room-setup-dialog majak-mobile-dialog-panel majak-popup-panel" role="dialog" aria-modal="true" aria-labelledby="config-dialog-title">
          <header id="config-dialog-title" className="majak-mobile-dialog-titlebar majak-popup-titlebar">
            <span>設定</span>
            <button className="majak-popup-titlebar__close" type="button" onClick={onCancel} aria-label="閉じる">×</button>
          </header>
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
          <div className="majak-popup-body majak-mobile-dialog-body majak-mobile-config-body">
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
          <div className="majak-mobile-dialog-actions majak-popup-actions">
            <button type="button" className="is-primary" onClick={() => onOK(cfg)}>OK</button>
            <button type="button" onClick={() => { onModify?.(initial); onCancel() }}>キャンセル</button>
          </div>
        </div>
      </div>
  )
}
