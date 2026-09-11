import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { getPaifuArchives, getPaifuReplayPayload, type PaifuArchiveFilter, type PaifuArchiveSummary } from '../../api/paifu'
import GameReconnectLoading from '../../components/GameReconnectLoading'
import type { PaifuSource } from '../ingame/PaifWnd'

function formatPlayedAt(value: string): string {
  const normalized = value.trim()
  const utcValue = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(normalized) ? normalized : `${normalized}Z`
  const date = new Date(utcValue)
  if (Number.isNaN(date.getTime())) return value.replace('T', ' ').slice(0, 16)

  const parts = new Intl.DateTimeFormat('ja-JP', {
    timeZone: 'Asia/Tokyo',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(date)
  const values = Object.fromEntries(parts.map(part => [part.type, part.value]))
  return `${values.year}/${values.month}/${values.day} ${values.hour}:${values.minute}`
}

function formatDateInputValue(value: Date): string {
  const timezoneOffset = value.getTimezoneOffset() * 60_000
  return new Date(value.getTime() - timezoneOffset).toISOString().slice(0, 10)
}

function createDefaultFilter(): PaifuArchiveFilter {
  const to = new Date()
  const from = new Date(to)
  from.setDate(from.getDate() - 7)
  return { from: formatDateInputValue(from), to: formatDateInputValue(to) }
}

function rankClass(result: string | undefined): string {
  const rank = result?.match(/^([1-4])位/)?.[1]
  return rank ? `majak-paifu-archive__rank rank-${rank}` : 'majak-paifu-archive__rank'
}

export default function PaifuArchiveScreen() {
  const navigate = useNavigate()
  const [filter, setFilter] = useState<PaifuArchiveFilter>(createDefaultFilter)
  const [archives, setArchives] = useState<PaifuArchiveSummary[]>([])
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isStartingReplay, setIsStartingReplay] = useState(false)

  const loadArchives = async (nextFilter: PaifuArchiveFilter) => {
    setIsLoading(true)
    try {
      setArchives(await getPaifuArchives(nextFilter))
      setSelectedId(null)
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    void loadArchives(createDefaultFilter())
  }, [])

  const selected = archives.find(archive => archive.archiveId === selectedId)

  const startReplay = async () => {
    if (!selected || isStartingReplay) return
    setIsStartingReplay(true)
    try {
      const data = await getPaifuReplayPayload(selected.archiveId)
      const paifu: PaifuSource = { data, title: selected.roomName || String(selected.archiveId) }
      navigate(`/paifu/replay?archiveId=${selected.archiveId}`, { state: { paifu } })
    } finally {
      setIsStartingReplay(false)
    }
  }

  const updateFilter = <K extends keyof PaifuArchiveFilter>(key: K, value: PaifuArchiveFilter[K]) => {
    setFilter(current => ({ ...current, [key]: value || undefined }))
  }

  const applyFilter = () => void loadArchives(filter)

  return (
    <main className="majak-paifu-archive majak-screen-surface">
      <header className="majak-paifu-archive__header">
        <div><p>牌譜再生</p><h1>牌譜一覧</h1></div>
      </header>
      <section className="majak-paifu-archive__filters" aria-label="牌譜検索">
        <label>種別<select value={filter.matchKind ?? ''} onChange={event => updateFilter('matchKind', event.target.value as PaifuArchiveFilter['matchKind'])}><option value="">すべて</option><option value="normal">通常</option><option value="tournament">大会</option></select></label>
        <label>対局者<input value={filter.member ?? ''} onChange={event => updateFilter('member', event.target.value)} /></label>
        <label className="majak-paifu-archive__date-range">期間<span><input type="date" value={filter.from ?? ''} onChange={event => updateFilter('from', event.target.value)} /><input type="date" value={filter.to ?? ''} onChange={event => updateFilter('to', event.target.value)} /></span></label>
        <button type="button" className="majak-responsive-control-button" onClick={applyFilter} disabled={isLoading}>検索</button>
        <button type="button" className="majak-responsive-control-button" onClick={() => void startReplay()} disabled={!selected || isStartingReplay}>再生</button>
      </section>
      <section className="majak-paifu-archive__content">
        <div className="majak-paifu-archive__list" aria-live="polite">
          <div className="majak-paifu-archive__list-header"><time>対局日時</time><strong>ゲーム種類</strong><span>順位・参加者</span></div>
          {isLoading && <p>読み込み中...</p>}
          {!isLoading && archives.length === 0 && <p>再生できる牌譜はありません。</p>}
          {!isLoading && archives.map(archive => (
            <button key={archive.archiveId} type="button" className={archive.archiveId === selectedId ? 'is-selected' : undefined} onClick={() => setSelectedId(archive.archiveId)}>
              <time>{formatPlayedAt(archive.playedAt)}</time><strong>{archive.roomName || 'ゲーム情報なし'}</strong>
              <div className="majak-paifu-archive__members">{archive.members.map((member, index) => <span key={`${member.name}-${index}`}>{index > 0 && ' / '}{member.name || '-'}{member.result && <small className={rankClass(member.result)}> {member.result}</small>}</span>)}</div>
            </button>
          ))}
        </div>
      </section>
      <GameReconnectLoading visible={isStartingReplay} currentStep="server" complete={false} fixed />
    </main>
  )
}
