import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { getPaifuArchives, getPaifuReplayPayload, type PaifuArchiveFilter, type PaifuArchiveSummary } from '../../api/paifu'
import type { PaifuSource } from '../ingame/PaifWnd'

function formatPlayedAt(value: string): string {
  return value.replace('T', ' ').slice(0, 16)
}

export default function PaifuArchiveScreen() {
  const navigate = useNavigate()
  const [filter, setFilter] = useState<PaifuArchiveFilter>({})
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
    void loadArchives({})
  }, [])

  const selected = archives.find(archive => archive.archiveId === selectedId)

  const startReplay = async () => {
    if (!selected || isStartingReplay) return
    setIsStartingReplay(true)
    try {
      const data = await getPaifuReplayPayload(selected.archiveId)
      const paifu: PaifuSource = { data, title: selected.roomName || String(selected.archiveId) }
      navigate('/paifu/replay', { state: { paifu } })
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
        <button type="button" className="majak-responsive-control-button" onClick={() => navigate('/channel')}>戻る</button>
      </header>
      <section className="majak-paifu-archive__filters" aria-label="牌譜検索">
        <label>種別<select value={filter.matchKind ?? ''} onChange={event => updateFilter('matchKind', event.target.value as PaifuArchiveFilter['matchKind'])}><option value="">すべて</option><option value="normal">通常</option><option value="tournament">大会</option></select></label>
        <label>対局者<input value={filter.member ?? ''} onChange={event => updateFilter('member', event.target.value)} /></label>
        <label>ルーム<input value={filter.room ?? ''} onChange={event => updateFilter('room', event.target.value)} /></label>
        <label>結果<input value={filter.result ?? ''} onChange={event => updateFilter('result', event.target.value)} /></label>
        <label>開始日<input type="date" value={filter.from ?? ''} onChange={event => updateFilter('from', event.target.value)} /></label>
        <label>終了日<input type="date" value={filter.to ?? ''} onChange={event => updateFilter('to', event.target.value)} /></label>
        <button type="button" className="majak-responsive-control-button" onClick={applyFilter} disabled={isLoading}>検索</button>
      </section>
      <section className="majak-paifu-archive__content">
        <div className="majak-paifu-archive__list" aria-live="polite">
          {isLoading && <p>読み込み中...</p>}
          {!isLoading && archives.length === 0 && <p>再生できる牌譜はありません。</p>}
          {!isLoading && archives.map(archive => (
            <button key={archive.archiveId} type="button" className={archive.archiveId === selectedId ? 'is-selected' : undefined} onClick={() => setSelectedId(archive.archiveId)}>
              <time>{formatPlayedAt(archive.playedAt)}</time><strong>{archive.roomName || 'ルーム'}</strong><span>{archive.result || '-'}</span>
            </button>
          ))}
        </div>
        <aside className="majak-paifu-archive__detail">
          {selected ? <>
            <h2>{selected.roomName || 'ルーム'}</h2>
            <dl><div><dt>開始日時</dt><dd>{formatPlayedAt(selected.playedAt)}</dd></div><div><dt>結果</dt><dd>{selected.result || '-'}</dd></div></dl>
            <table><thead><tr><th>ニックネーム</th><th>称号</th><th>結果</th></tr></thead><tbody>{selected.members.map((member, index) => <tr key={`${member.name}-${index}`}><td>{member.name || '-'}</td><td>{member.title || '-'}</td><td>{member.result || '-'}</td></tr>)}</tbody></table>
            <button type="button" className="majak-responsive-control-button is-primary" onClick={() => void startReplay()} disabled={isStartingReplay}>{isStartingReplay ? '準備中...' : '再生'}</button>
          </> : <p>牌譜を選択してください。</p>}
        </aside>
      </section>
    </main>
  )
}
