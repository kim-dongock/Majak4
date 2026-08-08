/**
 * CMJSelPaifuDlg 相当 — 牌譜選択ダイアログ (AP-09 §3-1-7)
 * レガシー: legacy/client/HgMajak2/mjselpaifu.h/cpp, HgMajak2.rc IDD_SPAIFU_DLG
 *
 * RC: IDD_SPAIFU_DLG DIALOG 0,0,451,325 / CAPTION "牌譜選択"
 * OnInitDialog(): 全フィルタON、OK/適用は無効、ScanPaifu() 後に OnPaifuSelected(-1)
 * OnApply(): フィルタ再適用、選択復元できなければ詳細を空に戻す
 * OnColumnclickPaifulist(): クリック列で昇順ソート
 * OnDblclkPaifulist(): OnOK()
 */
import { type CSSProperties, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { loadLastUsedPaifuFileName, saveLastUsedPaifuFileName } from '../../../game/paifuRecording'

const IMG = '/assets/images/game'
const FONT = 'var(--majak-font-family-ui)'
const DLG_BG = '#d4d0c8'
const SX = (du: number) => Math.round(du * 1.5)
const SY = (du: number) => Math.round(du * 1.625)
const DLG_W = SX(451)
const DLG_H = SY(325)

const OPTION_ICON = {
  set: `${IMG}/mj_opt_0.png`,
  kui: `${IMG}/mj_opt_3.png`,
  uma: `${IMG}/mj_opt_1.png`,
  ron: `${IMG}/mj_optron.png`,
  red: `${IMG}/mj_opt_5.png`,
  tor: `${IMG}/mj_opt_2.png`,
  war: `${IMG}/mj_optwar.png`,
  tip: `${IMG}/mj_opttip.png`,
  spd: `${IMG}/mj_opt_4.png`,
  opn: `${IMG}/mj_opt_6.png`,
  cht: `${IMG}/mj_opt_7.png`,
  ach: `${IMG}/mj_opt_8.png`,
} as const

export interface PaifuEntry {
  id: number
  date: string
  comment?: string
  fieldName: string
  roomName: string
  result: string
  option: string
  data?: unknown
  members?: Array<{ name?: string; title?: string; rate?: number; result?: string }>
}

interface OptFilter {
  con: [boolean, boolean]
  set: [boolean, boolean]
  kui: [boolean, boolean]
  uma: [boolean, boolean, boolean]
  red: [boolean, boolean, boolean]
  tor: [boolean, boolean]
  war: [boolean, boolean]
  tip: [boolean, boolean]
  ron: [boolean, boolean, boolean]
}

interface Props {
  entries?: PaifuEntry[]
  onSelect: (entry: PaifuEntry) => void
  onCancel: () => void
}

type SortKey = 'date' | 'comment'

const EMPTY_PAIFU_ENTRIES: PaifuEntry[] = []

const defaultOpt = (): OptFilter => ({
  con: [true, true],
  set: [true, true],
  kui: [true, true],
  uma: [true, true, true],
  red: [true, true, true],
  tor: [true, true],
  war: [true, true],
  tip: [true, true],
  ron: [true, true, true],
})

function readOptionDigit(option: string | undefined, index: number, fallback: number) {
  const char = option?.charAt(index) ?? ''
  return /^\d$/.test(char) ? Number(char) : fallback
}

function optionAllows(values: boolean[], value: number) {
  return values[Math.max(0, Math.min(values.length - 1, value))] !== false
}

function matchesOptionFilter(entry: PaifuEntry, opt: OptFilter) {
  const option = entry.option ?? ''
  const contest = readOptionDigit(option, 8, 0)
  if (!optionAllows(opt.con, contest ? 1 : 0)) return false
  if (contest !== 0) return true

  return optionAllows(opt.set, readOptionDigit(option, 0, 0))
    && optionAllows(opt.uma, readOptionDigit(option, 1, 0))
    && optionAllows(opt.kui, readOptionDigit(option, 3, 0))
    && optionAllows(opt.tor, readOptionDigit(option, 4, 0))
    && optionAllows(opt.red, readOptionDigit(option, 5, 0))
    && optionAllows(opt.war, readOptionDigit(option, 10, 0))
    && optionAllows(opt.tip, readOptionDigit(option, 11, 0))
    && optionAllows(opt.ron, readOptionDigit(option, 12, 0))
}

function normalizeDate(date: string) {
  if (/^\d{4}\/\d{2}\/\d{2}/.test(date)) return date.replace(/\//g, '-').slice(0, 10)
  return date.slice(0, 10)
}

function optionSprite(src: string, value: number, left: number, top: number, maxFrame = 8) {
  const frame = Math.max(0, Math.min(maxFrame, value))
  return (
    <div
      style={{
        position: 'absolute', left, top,
        width: 17, height: 17,
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-17 * frame}px 0`,
        backgroundRepeat: 'no-repeat',
        pointerEvents: 'none',
      }}
    />
  )
}

export default function SelPaifuDlg({ entries = EMPTY_PAIFU_ENTRIES, onSelect, onCancel }: Props) {
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [sortKey, setSortKey] = useState<SortKey>('date')
  const [applyEnabled, setApplyEnabled] = useState(false)
  const [fileName, setFileName] = useState(() => loadLastUsedPaifuFileName())
  const [dateFrom, setDateFrom] = useState('')
  const [dateTo, setDateTo] = useState('')
  const [memberFilter, setMemberFilter] = useState('')
  const [commentFilter, setCommentFilter] = useState('')
  const [opt, setOpt] = useState<OptFilter>(() => defaultOpt())
  const [localEntries, setLocalEntries] = useState(entries)

  useEffect(() => {
    setLocalEntries(entries)
    setSelectedId(null)
  }, [entries])

  const filtered = useMemo(() => {
    return localEntries.filter(entry => {
      const entryDate = normalizeDate(entry.date)
      if (dateFrom && entryDate < dateFrom) return false
      if (dateTo && entryDate > dateTo) return false
      if (commentFilter.trim() && !(entry.comment ?? '').includes(commentFilter.trim())) return false
      const members = entry.members?.map(member => member.name ?? '').join('\n') ?? ''
      if (memberFilter.trim() && !members.includes(memberFilter.trim())) return false
      return matchesOptionFilter(entry, opt)
    }).sort((a, b) => {
      const av = sortKey === 'date' ? a.date : (a.comment ?? '')
      const bv = sortKey === 'date' ? b.date : (b.comment ?? '')
      return av < bv ? -1 : av > bv ? 1 : 0
    })
  }, [localEntries, dateFrom, dateTo, memberFilter, commentFilter, opt, sortKey])

  const selected = filtered.find(entry => entry.id === selectedId) ?? null

  const markFilterChanged = () => setApplyEnabled(true)
  const applyFilter = useCallback(() => {
    setApplyEnabled(false)
    if (selectedId !== null && !filtered.some(entry => entry.id === selectedId)) {
      setSelectedId(null)
    }
  }, [filtered, selectedId])

  const toggleOpt = <K extends keyof OptFilter>(key: K, index: number, value: boolean) => {
    setOpt(prev => ({
      ...prev,
      [key]: prev[key].map((item, itemIndex) => itemIndex === index ? value : item) as OptFilter[K],
    }))
    markFilterChanged()
  }

  const handleOK = () => {
    if (selected) onSelect(selected)
  }

  const handleFileBrowse = () => {
    fileInputRef.current?.click()
  }

  const handleFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file) return
    setSelectedId(null)
    file.text().then(text => {
      const firstBreak = text.search(/\r?\n/)
      const rawComment = text.startsWith('<')
        ? text.slice(1, firstBreak === -1 ? undefined : firstBreak)
        : ''
      const body = text.startsWith('<') && firstBreak !== -1 ? text.slice(firstBreak).trimStart() : text
      let data: unknown = text
      try {
        const parsed = JSON.parse(body)
        data = parsed && typeof parsed === 'object' && 'paifu' in parsed ? (parsed as { paifu: unknown }).paifu : parsed
      } catch {
        data = text
      }
      const loadedEntry: PaifuEntry = {
        id: Date.now(),
        date: new Date(file.lastModified || Date.now()).toISOString().slice(0, 16).replace('T', ' '),
        comment: rawComment,
        fieldName: file.name,
        roomName: '',
        result: '',
        option: '',
        data,
      }
      setFileName(file.name)
      saveLastUsedPaifuFileName(file.name)
      setLocalEntries([loadedEntry])
      setApplyEnabled(false)
    }).catch(() => {
      setLocalEntries([])
      setApplyEnabled(false)
    })
  }

  const labelStyle: CSSProperties = { fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', color: '#000', lineHeight: '14px' }
  const inputStyle: CSSProperties = { fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', height: '100%', boxSizing: 'border-box', border: '1px solid #808080', background: '#fff', padding: '1px 3px' }
  const buttonStyle: CSSProperties = { fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', height: '100%', padding: 0, background: DLG_BG, border: '2px outset #d4d0c8', boxSizing: 'border-box' }
  const smallButtonStyle = (active: boolean): CSSProperties => ({
    position: 'absolute', width: SX(14), height: SY(16), padding: 0,
    border: active ? '2px inset #d4d0c8' : '2px outset #d4d0c8',
    background: active ? '#c0c0c0' : DLG_BG,
    boxSizing: 'border-box',
  })

  const optButton = (key: keyof OptFilter, index: number, x: number, y: number, label: string) => (
    <button
      aria-label={label}
      title={label}
      onClick={() => toggleOpt(key, index, !opt[key][index])}
      style={{ ...smallButtonStyle(opt[key][index]), left: SX(x), top: SY(y) }}
    />
  )

  const tableFont: CSSProperties = { fontFamily: FONT, fontSize: 'calc(12px * var(--majak-type-scale))', lineHeight: '16px' }

  return (
    <div style={{ position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', background: 'rgba(0,0,0,0.45)', zIndex: 400 }}>
      <div style={{ position: 'relative', width: DLG_W, height: DLG_H, background: DLG_BG, border: '2px outset #d4d0c8', boxSizing: 'border-box' }}>
        <button disabled={!selected} onClick={handleOK} style={{ ...buttonStyle, position: 'absolute', left: SX(339), top: SY(305), width: SX(50), height: SY(14), opacity: selected ? 1 : 0.55 }}>OK</button>
        <button onClick={onCancel} style={{ ...buttonStyle, position: 'absolute', left: SX(395), top: SY(305), width: SX(50), height: SY(14) }}>キャンセル</button>

        <span style={{ ...labelStyle, position: 'absolute', left: SX(7), top: SY(10), width: SX(24), height: SY(8) }}>ファイル</span>
        <input readOnly value={fileName} style={{ ...inputStyle, position: 'absolute', left: SX(34), top: SY(7), width: SX(181), height: SY(14) }} />
        <input ref={fileInputRef} type="file" accept=".txt,text/plain" onChange={handleFileChange} style={{ display: 'none' }} />
        <button onClick={handleFileBrowse} style={{ ...buttonStyle, position: 'absolute', left: SX(220), top: SY(7), width: SX(35), height: SY(14) }}>開く...</button>

        <fieldset style={{ position: 'absolute', left: SX(7), top: SY(27), width: SX(268), height: SY(118), margin: 0, padding: 0, border: '1px groove #fff', boxSizing: 'border-box' }}>
          <legend style={{ ...labelStyle, marginLeft: 6 }}>フィルタ</legend>
        </fieldset>

        {optButton('con', 0, 13, 41, '通常')}
        {optButton('con', 1, 27, 41, '大会')}
        {optButton('set', 0, 13, 60, '東風')}
        {optButton('set', 1, 27, 60, '半荘')}
        {optButton('kui', 0, 62, 60, 'クイタンなし')}
        {optButton('kui', 1, 76, 60, 'クイタンあり')}
        {optButton('uma', 0, 13, 79, 'ウマ5-10')}
        {optButton('uma', 1, 27, 79, 'ウマ10-20')}
        {optButton('uma', 2, 41, 79, 'ウマ10-30')}
        {optButton('tor', 0, 62, 79, '積みなし')}
        {optButton('tor', 1, 76, 79, '積みあり')}
        {optButton('red', 0, 13, 98, '赤なし')}
        {optButton('red', 1, 27, 98, '赤1')}
        {optButton('red', 2, 41, 98, '赤2')}
        {optButton('tip', 0, 62, 98, 'チップなし')}
        {optButton('tip', 1, 76, 98, 'チップあり')}
        {optButton('ron', 0, 13, 117, '頭ハネ')}
        {optButton('ron', 1, 27, 117, 'ダブロン')}
        {optButton('ron', 2, 41, 117, 'トリロン')}
        {optButton('war', 0, 62, 117, 'ワレメなし')}
        {optButton('war', 1, 76, 117, 'ワレメあり')}

        <span style={{ ...labelStyle, position: 'absolute', left: SX(100), top: SY(40), width: SX(75), height: SY(8) }}>以下の対局者を全て含む</span>
        <textarea value={memberFilter} onChange={event => { setMemberFilter(event.target.value); markFilterChanged() }} style={{ ...inputStyle, position: 'absolute', left: SX(100), top: SY(50), width: SX(77), height: SY(40), resize: 'none' }} />

        <fieldset style={{ position: 'absolute', left: SX(183), top: SY(40), width: SX(88), height: SY(50), margin: 0, padding: 0, border: '1px groove #fff', boxSizing: 'border-box' }}>
          <legend style={{ ...labelStyle, marginLeft: 6 }}>開始日時</legend>
        </fieldset>
        <input type="date" value={dateFrom} onChange={event => { setDateFrom(event.target.value); markFilterChanged() }} style={{ ...inputStyle, position: 'absolute', left: SX(189), top: SY(50), width: SX(61), height: SY(15) }} />
        <span style={{ ...labelStyle, position: 'absolute', left: SX(253), top: SY(53), width: SX(14), height: SY(8) }}>から</span>
        <input type="date" value={dateTo} onChange={event => { setDateTo(event.target.value); markFilterChanged() }} style={{ ...inputStyle, position: 'absolute', left: SX(189), top: SY(70), width: SX(61), height: SY(15) }} />
        <span style={{ ...labelStyle, position: 'absolute', left: SX(253), top: SY(73), width: SX(14), height: SY(8) }}>まで</span>

        <span style={{ ...labelStyle, position: 'absolute', left: SX(100), top: SY(95), width: SX(89), height: SY(8) }}>以下の文字列をコメントに含む</span>
        <input value={commentFilter} onChange={event => { setCommentFilter(event.target.value); markFilterChanged() }} style={{ ...inputStyle, position: 'absolute', left: SX(100), top: SY(105), width: SX(171), height: SY(14) }} />
        <button disabled={!applyEnabled} onClick={applyFilter} style={{ ...buttonStyle, position: 'absolute', left: SX(220), top: SY(125), width: SX(50), height: SY(14), opacity: applyEnabled ? 1 : 0.55 }}>適用</button>

        <div style={{ position: 'absolute', left: SX(7), top: SY(155), width: SX(437), height: SY(143), border: '1px solid #808080', background: '#fff', overflow: 'hidden', boxSizing: 'border-box' }}>
          <table style={{ ...tableFont, width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr>
                <th onClick={() => setSortKey('date')} style={{ width: SX(102), textAlign: 'left', background: DLG_BG, borderRight: '1px solid #808080', cursor: 'pointer' }}>開始日時</th>
                <th onClick={() => setSortKey('comment')} style={{ textAlign: 'left', background: DLG_BG, cursor: 'pointer' }}>コメント</th>
              </tr>
            </thead>
          </table>
          <div style={{ position: 'absolute', left: 0, right: 0, top: 18, bottom: 0, overflowY: 'auto' }}>
            <table style={{ ...tableFont, width: '100%', borderCollapse: 'collapse' }}>
              <tbody>
                {filtered.map(entry => (
                  <tr key={entry.id} onClick={() => setSelectedId(entry.id)} onDoubleClick={() => { setSelectedId(entry.id); onSelect(entry) }} style={{ background: selectedId === entry.id ? '#0a246a' : '#fff', color: selectedId === entry.id ? '#fff' : '#000', cursor: 'default' }}>
                    <td style={{ width: SX(102), padding: '0 3px', whiteSpace: 'nowrap' }}>{entry.date}</td>
                    <td style={{ padding: '0 3px', whiteSpace: 'nowrap' }}>{entry.comment ?? ''}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <fieldset style={{ position: 'absolute', left: SX(280), top: SY(7), width: SX(164), height: SY(138), margin: 0, padding: 0, border: '1px groove #fff', boxSizing: 'border-box' }}>
          <legend style={{ ...labelStyle, marginLeft: 6 }}>詳細</legend>
        </fieldset>
        <span style={{ ...labelStyle, position: 'absolute', left: SX(285), top: SY(21), width: SX(152), height: SY(8), overflow: 'hidden', whiteSpace: 'nowrap' }}>{selected?.fieldName ?? ''}</span>
        <span style={{ ...labelStyle, position: 'absolute', left: SX(285), top: SY(36), width: SX(152), height: SY(8), overflow: 'hidden', whiteSpace: 'nowrap' }}>{selected?.roomName ?? ''}</span>
        <span style={{ ...labelStyle, position: 'absolute', left: SX(285), top: SY(71), width: SX(152), height: SY(8), overflow: 'hidden', whiteSpace: 'nowrap' }}>{selected?.date ?? ''}</span>

        {selected && (
          <>
            {optionSprite(OPTION_ICON.set, readOptionDigit(selected.option, 0, 0), 496, 80, 1)}
            {optionSprite(OPTION_ICON.kui, readOptionDigit(selected.option, 3, 0), 496 + 17, 80, 1)}
            {optionSprite(OPTION_ICON.uma, readOptionDigit(selected.option, 1, 0), 496 + 17 * 2, 80, 3)}
            {optionSprite(OPTION_ICON.ron, readOptionDigit(selected.option, 12, 0), 496 + 17 * 3, 80, 2)}
            {optionSprite(OPTION_ICON.red, readOptionDigit(selected.option, 5, 0), 496 + 17 * 4, 80, 2)}
            {optionSprite(OPTION_ICON.tor, readOptionDigit(selected.option, 4, 0), 496 + 17 * 5, 80, 1)}
            {optionSprite(OPTION_ICON.war, readOptionDigit(selected.option, 10, 0), 496 + 17 * 6, 80, 1)}
            {optionSprite(OPTION_ICON.tip, readOptionDigit(selected.option, 11, 0), 496 + 17 * 7, 80, 1)}
            {optionSprite(OPTION_ICON.spd, readOptionDigit(selected.option, 2, 0), 496 + 17 * 8, 80, 3)}
            {optionSprite(OPTION_ICON.opn, readOptionDigit(selected.option, 6, 0), 496 + 17 * 9, 80, 1)}
            {optionSprite(readOptionDigit(selected.option, 7, 1) ? OPTION_ICON.cht : OPTION_ICON.ach, readOptionDigit(selected.option, 7, 0), 496 + 17 * 10, 80, 1)}
          </>
        )}

        <div style={{ position: 'absolute', left: SX(285), top: SY(85), width: SX(153), height: SY(50), border: '1px solid #808080', background: '#fff', overflow: 'hidden', boxSizing: 'border-box' }}>
          <table style={{ ...tableFont, width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr><th style={{ width: SX(60), textAlign: 'left', background: DLG_BG }}>ニックネーム</th><th style={{ textAlign: 'left', background: DLG_BG }}>称号</th><th style={{ width: SX(32), textAlign: 'right', background: DLG_BG }}>結果</th></tr>
            </thead>
            <tbody>
              {(selected?.members ?? []).map((member, index) => (
                <tr key={index}><td>{member.name ?? ''}</td><td>{member.title ?? ''}{typeof member.rate === 'number' ? `(${member.rate})` : ''}</td><td style={{ textAlign: 'right' }}>{member.result ?? ''}</td></tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}