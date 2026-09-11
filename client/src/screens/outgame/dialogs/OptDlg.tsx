import { useState } from 'react'
import { useOutgameLayoutMode } from '../../../hooks/useOutgameLayoutMode'

const OPT_TO_DLG_RED = [0, 2, 1] as const
const DLG_TO_OPT_RED = [0, 2, 1] as const

export interface MJOption {
  nSet: number
  nUma: number
  bKui: boolean
  bTor: boolean
  bWar: boolean
  bTip: boolean
  nRon: number
  nSpd: number
  bEnableChat: boolean
  bOpenHand: boolean
  bViewChat: boolean
  nRed: number
  nContest: number
}

export const DEFAULT_OPTION: MJOption = { nSet: 1, nUma: 2, bKui: false, bTor: false, bWar: false, bTip: false, nRon: 0, nSpd: 2, bEnableChat: false, bOpenHand: false, bViewChat: false, nRed: 2, nContest: 0 }

export function optionToString(opt: MJOption): string {
  return [opt.nSet, opt.nUma, opt.nSpd, opt.bKui ? 1 : 0, opt.bTor ? 1 : 0, opt.nRed, opt.bOpenHand ? 1 : 0, opt.bViewChat ? 1 : 0, opt.nContest, 0, opt.bWar ? 1 : 0, opt.bTip ? 1 : 0, opt.nRon, 0, opt.bEnableChat ? 1 : 0].join('')
}

export interface MJOptionMask {
  nSet?: number; nUma?: number; bKui?: number; bTor?: number; bWar?: number; bTip?: number; nRon?: number; nSpd?: number; bEnableChat?: number; bOpenHand?: number; bViewChat?: number; nRed?: number; nContest?: number
}

interface Props { initial: MJOption; mask?: MJOptionMask; viewerEnable?: boolean; onOK: (opt: MJOption) => void; onCancel: () => void }

function applyOptionMask(option: MJOption, mask: MJOptionMask, viewerEnable: boolean): MJOption {
  const next = { ...option }
  for (const key of ['nSet', 'nUma', 'nSpd', 'nRed', 'nRon', 'nContest'] as const) if (mask[key] !== undefined && mask[key]! >= 0) next[key] = mask[key]!
  for (const key of ['bKui', 'bTor', 'bWar', 'bTip', 'bEnableChat'] as const) if (mask[key] !== undefined && mask[key]! >= 0) next[key] = mask[key]! !== 0
  if (viewerEnable) {
    for (const key of ['bOpenHand', 'bViewChat'] as const) if (mask[key] !== undefined && mask[key]! >= 0) next[key] = mask[key]! !== 0
    if (!next.bEnableChat) next.bViewChat = false
  } else { next.bOpenHand = false; next.bViewChat = false }
  return next
}

export default function OptDlg({ initial, mask = {}, viewerEnable = true, onOK, onCancel }: Props) {
  const layoutMode = useOutgameLayoutMode()
  const [opt, setOpt] = useState<MJOption>(() => applyOptionMask(initial, mask, viewerEnable))
  const set = <K extends keyof MJOption>(key: K, value: MJOption[K]) => setOpt(current => ({ ...current, [key]: value, ...(key === 'bEnableChat' && value === false ? { bViewChat: false } : {}) }))
  const disabled = (key: keyof MJOptionMask) => mask[key] !== undefined && mask[key]! >= 0
  const dialogRed = OPT_TO_DLG_RED[opt.nRed as 0 | 1 | 2] ?? 0
  const setDialogRed = (value: number) => set('nRed', (DLG_TO_OPT_RED[value as 0 | 1 | 2] ?? 0) as number)

  return <div className={`majak-mobile-dialog-overlay majak-room-setup-overlay majak-room-setup-overlay--${layoutMode} majak-popup-overlay`} role="presentation">
    <div className="majak-mobile-option-dialog majak-room-setup-dialog majak-mobile-dialog-panel majak-popup-panel" role="dialog" aria-modal="true" aria-labelledby="room-option-dialog-title">
      <header id="room-option-dialog-title" className="majak-mobile-dialog-titlebar majak-popup-titlebar"><span>部屋の設定</span><button className="majak-popup-titlebar__close" type="button" onClick={onCancel} aria-label="閉じる">×</button></header>
      <div className="majak-popup-body majak-mobile-dialog-body majak-mobile-option-body">
        <fieldset className="majak-mobile-dialog-section"><legend>対戦種別</legend><div className="majak-mobile-choice-grid majak-mobile-choice-grid--two"><label className="majak-mobile-choice"><input type="radio" name="nSet" checked={opt.nSet === 0} disabled={disabled('nSet')} onChange={() => set('nSet', 0)} />東風戦</label><label className="majak-mobile-choice"><input type="radio" name="nSet" checked={opt.nSet === 1} disabled={disabled('nSet')} onChange={() => set('nSet', 1)} />半荘戦</label></div></fieldset>
        <fieldset className="majak-mobile-dialog-section"><legend>ウマ(順位ボーナス)</legend><div className="majak-mobile-choice-grid majak-mobile-choice-grid--three">{([['5-10', 0], ['10-20', 1], ['10-30', 2]] as const).map(([label, value]) => <label className="majak-mobile-choice" key={value}><input type="radio" name="nUma" checked={opt.nUma === value} disabled={disabled('nUma')} onChange={() => set('nUma', value)} />{label}</label>)}</div></fieldset>
        <fieldset className="majak-mobile-dialog-section"><legend>あがり</legend><div className="majak-mobile-choice-grid majak-mobile-choice-grid--three">{(['頭ハネ', 'ダブロン', 'トリロン'] as const).map((label, value) => <label className="majak-mobile-choice" key={label}><input type="radio" name="nRon" checked={opt.nRon === value} disabled={disabled('nRon')} onChange={() => set('nRon', value)} />{label}</label>)}</div></fieldset>
        <fieldset className="majak-mobile-dialog-section"><legend>赤牌</legend><div className="majak-mobile-choice-grid majak-mobile-choice-grid--three"><label className="majak-mobile-choice"><input type="radio" name="nRed" checked={opt.nRed === 0} disabled={disabled('nRed')} onChange={() => set('nRed', 0)} />無し</label><label className="majak-mobile-choice"><input type="radio" name="nRed" checked={dialogRed === 1} disabled={disabled('nRed')} onChange={() => setDialogRed(1)} />各１枚</label><label className="majak-mobile-choice"><input type="radio" name="nRed" checked={dialogRed === 2} disabled={disabled('nRed')} onChange={() => setDialogRed(2)} />五筒２枚</label></div></fieldset>
        <fieldset className="majak-mobile-dialog-section"><legend>スピード</legend><div className="majak-mobile-choice-grid majak-mobile-choice-grid--four">{(['超光速', 'サクサク', '標準', 'ゆったり'] as const).map((label, value) => <label className="majak-mobile-choice" key={label}><input type="radio" name="nSpd" checked={opt.nSpd === value} disabled={disabled('nSpd')} onChange={() => set('nSpd', value)} />{label}</label>)}</div></fieldset>
        <fieldset className="majak-mobile-dialog-section"><legend>その他</legend><div className="majak-mobile-choice-grid majak-mobile-choice-grid--three">{([['bKui', 'クイタン無し'], ['bTor', '焼き鳥有り'], ['bTip', 'チップ有り'], ['bWar', 'ワレメ有り'], ['bEnableChat', 'チャットを許可する']] as const).map(([key, label]) => <label className="majak-mobile-choice" key={key}><input type="checkbox" checked={opt[key]} disabled={disabled(key)} onChange={event => set(key, event.target.checked)} />{label}</label>)}<label className="majak-mobile-choice"><input type="checkbox" checked={opt.bOpenHand} disabled={!viewerEnable || disabled('bOpenHand')} onChange={event => set('bOpenHand', event.target.checked)} />観戦者に手牌を公開する</label><label className="majak-mobile-choice majak-mobile-choice--wide"><input type="checkbox" checked={opt.bViewChat} disabled={!viewerEnable || disabled('bViewChat') || !opt.bEnableChat} onChange={event => set('bViewChat', event.target.checked)} />観戦者に対局者とのチャットを許可する</label></div></fieldset>
      </div>
      <footer className="majak-mobile-dialog-actions majak-popup-actions"><button type="button" className="majak-standard-dialog__secondary" onClick={onCancel}>キャンセル</button><button type="button" className="majak-standard-dialog__primary is-primary" onClick={() => onOK(applyOptionMask(opt, mask, viewerEnable))}>確認</button></footer>
    </div>
  </div>
}
