import { useEffect, useRef, useState } from 'react'
import { getAnnouncements, type GameAnnouncement } from '../../api/announcements'

const PAGE_SIZE = 20

function formatPublishedDate(value: string | null): string {
  if (!value) return '公開日未設定'
  return new Intl.DateTimeFormat('ja-JP', { year: 'numeric', month: 'short', day: 'numeric' }).format(new Date(value))
}

export default function AnnouncementListScreen() {
  const [articles, setArticles] = useState<GameAnnouncement[]>([])
  const [selected, setSelected] = useState<GameAnnouncement | null>(null)
  const [hasMore, setHasMore] = useState(true)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const sentinelRef = useRef<HTMLDivElement | null>(null)
  const loadingRef = useRef(false)
  const hasMoreRef = useRef(true)

  const loadMore = async () => {
    if (loadingRef.current || !hasMoreRef.current) return
    loadingRef.current = true
    setLoading(true)
    try {
      const page = await getAnnouncements(articles.length, PAGE_SIZE)
      setArticles(current => {
        const knownIds = new Set(current.map(article => article.announcementId))
        return [...current, ...page.items.filter(article => !knownIds.has(article.announcementId))]
      })
      hasMoreRef.current = page.hasMore
      setHasMore(page.hasMore)
    } catch (requestError) {
      setError((requestError as Error).message)
    } finally {
      loadingRef.current = false
      setLoading(false)
    }
  }

  useEffect(() => { void loadMore() }, [])

  useEffect(() => {
    const sentinel = sentinelRef.current
    if (!sentinel) return
    const observer = new IntersectionObserver(entries => {
      if (entries[0]?.isIntersecting) void loadMore()
    }, { rootMargin: '320px' })
    observer.observe(sentinel)
    return () => observer.disconnect()
  }, [articles.length, hasMore])

  return <main className="majak-announcement-list majak-screen-surface">
    <section className="majak-announcement-list__intro">
      <h1>お知らせ</h1>
      <p>最新のお知らせから順に表示しています。</p>
    </section>
    <section className="majak-announcement-list__items" aria-label="お知らせ一覧">
      {articles.map(article => <button
        type="button"
        key={article.announcementId}
        className={selected?.announcementId === article.announcementId ? 'is-selected' : ''}
        onClick={() => setSelected(article)}
      >
        <time>{formatPublishedDate(article.publishedAt)}</time>
        <strong>{article.title}</strong>
        {article.isStartup && <span>開始時表示</span>}
      </button>)}
      {!loading && articles.length === 0 && !error && <p className="majak-announcement-list__empty">公開中のお知らせはありません。</p>}
      {error && <div className="majak-announcement-list__error"><p>{error}</p><button type="button" onClick={() => void loadMore()}>再読み込み</button></div>}
      <div ref={sentinelRef} className="majak-announcement-list__sentinel" aria-hidden="true" />
      {loading && <p className="majak-announcement-list__loading">お知らせを読み込んでいます...</p>}
      {!hasMore && articles.length > 0 && <p className="majak-announcement-list__complete">すべてのお知らせを表示しました。</p>}
    </section>
    {selected && <section className="majak-announcement-list__detail">
      <details open>
        <summary><span>記事の詳細</span><strong>{selected.title}</strong></summary>
        <article>
          <time>{formatPublishedDate(selected.publishedAt)}</time>
          <h2>{selected.title}</h2>
          <p>{selected.body}</p>
        </article>
      </details>
    </section>}
    <style>{`
      .majak-announcement-list { min-height: 100dvh; color: #182b29; background: #eef1e9; font-family: var(--majak-font-family-ui); }
      .majak-announcement-list__intro, .majak-announcement-list__items, .majak-announcement-list__detail { width: min(940px, calc(100% - 32px)); margin-inline: auto; }
      .majak-announcement-list__intro { padding: 24px 0 12px; color: #607069; font-size: var(--majak-font-13); }
      .majak-announcement-list__intro h1 { margin: 0 0 8px; color: #1b413a; font-size: var(--majak-font-25); }
      .majak-announcement-list__intro p { margin: 0; }
      .majak-announcement-list__items { display: grid; border-top: 1px solid #c7d0c7; }
      .majak-announcement-list__items > button { display: grid; grid-template-columns: 120px minmax(0, 1fr) auto; gap: 16px; align-items: center; min-height: 62px; padding: 12px 16px; border: 0; border-bottom: 1px solid #c7d0c7; color: #263936; background: #fffdf8; text-align: left; cursor: pointer; }
      .majak-announcement-list__items > button:hover, .majak-announcement-list__items > button.is-selected { background: #e4efe7; box-shadow: inset 4px 0 #c3962e; }
      .majak-announcement-list__items time { color: #64736a; font-size: var(--majak-font-12); }
      .majak-announcement-list__items strong { overflow: hidden; font-size: var(--majak-font-15); text-overflow: ellipsis; white-space: nowrap; }
      .majak-announcement-list__items span { padding: 4px 7px; border: 1px solid #d1a846; color: #7c5c12; background: #fff5cf; font-size: var(--majak-font-10); white-space: nowrap; }
      .majak-announcement-list__sentinel { height: 1px; }
      .majak-announcement-list__loading, .majak-announcement-list__complete, .majak-announcement-list__empty { margin: 18px 0; color: #68776f; text-align: center; font-size: var(--majak-font-13); }
      .majak-announcement-list__error { display: flex; justify-content: center; align-items: center; gap: 12px; padding: 18px; color: #a33b2c; }
      .majak-announcement-list__error p { margin: 0; }.majak-announcement-list__error button { border: 1px solid #a33b2c; background: #fff; color: #8b3023; cursor: pointer; }
      .majak-announcement-list__detail { margin-top: 24px; padding-bottom: 42px; }
      .majak-announcement-list__detail details { border: 1px solid #b7c5b8; background: #fffdf8; }
      .majak-announcement-list__detail summary { display: grid; grid-template-columns: 118px minmax(0, 1fr); gap: 14px; padding: 15px 18px; color: #1e4f45; cursor: pointer; list-style: none; }.majak-announcement-list__detail summary::-webkit-details-marker { display: none; }
      .majak-announcement-list__detail summary span { color: #8a681d; font-size: var(--majak-font-12); font-weight: 700; }.majak-announcement-list__detail summary strong { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
      .majak-announcement-list__detail article { padding: 22px; border-top: 1px solid #cfd7cd; }.majak-announcement-list__detail article time { color: #68776f; font-size: var(--majak-font-12); }.majak-announcement-list__detail h2 { margin: 10px 0 16px; color: #1b413a; font-size: var(--majak-font-20); }.majak-announcement-list__detail article p { margin: 0; white-space: pre-wrap; color: #34453f; font-size: var(--majak-font-14); line-height: 1.8; }
      @media (max-width: 640px) { .majak-announcement-list__intro, .majak-announcement-list__items, .majak-announcement-list__detail { width: min(100% - 20px, 940px); }.majak-announcement-list__intro { padding-top: 15px; }.majak-announcement-list__intro h1 { font-size: var(--majak-font-19); }.majak-announcement-list__items > button { grid-template-columns: 1fr auto; gap: 5px 8px; min-height: 58px; padding: 10px; }.majak-announcement-list__items time { grid-column: 1 / -1; font-size: var(--majak-font-11); }.majak-announcement-list__items strong { font-size: var(--majak-font-13); }.majak-announcement-list__detail { margin-top: 16px; }.majak-announcement-list__detail summary { grid-template-columns: 1fr; gap: 5px; padding: 12px; }.majak-announcement-list__detail article { padding: 16px 12px; }.majak-announcement-list__detail h2 { font-size: var(--majak-font-17); }.majak-announcement-list__detail article p { font-size: var(--majak-font-13); line-height: 1.7; } }
    `}</style>
  </main>
}