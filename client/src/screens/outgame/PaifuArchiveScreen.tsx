import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { getPaifuArchives, getPaifuReplayPayload, type PaifuArchiveFilter, type PaifuArchiveSummary } from '../../api/paifu'
import type { PaifuSource } from '../ingame/PaifWnd'

function formatPlayedAt(value: string): string {
  return value.replace('T', ' ').slice(0, 16)
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
        <button type="button" className="majak-responsive-control-button" onClick={() => void startReplay()} disabled={!selected || isStartingReplay}>{isStartingReplay ? '準備中...' : '再生'}</button>
      </section>
      <section className="majak-paifu-archive__content">
        <div className="majak-paifu-archive__list" aria-live="polite">
          <div className="majak-paifu-archive__list-header"><time>対局日時</time><strong>ルーム</strong><span>結果</span><span>参加者</span></div>
          {isLoading && <p>読み込み中...</p>}
          {!isLoading && archives.length === 0 && <p>再生できる牌譜はありません。</p>}
          {!isLoading && archives.map(archive => (
            <button key={archive.archiveId} type="button" className={archive.archiveId === selectedId ? 'is-selected' : undefined} onClick={() => setSelectedId(archive.archiveId)}>
              <time>{formatPlayedAt(archive.playedAt)}</time><strong>{archive.roomName || 'ルーム'}</strong><span>{archive.result || '-'}</span>
              <div className="majak-paifu-archive__members">{archive.members.map((member, index) => <span key={`${member.name}-${index}`}>{index > 0 && ' / '}{member.name || '-'}</span>)}</div>
            </button>
          ))}
        </div>
      </section>
    </main>
  )
}
