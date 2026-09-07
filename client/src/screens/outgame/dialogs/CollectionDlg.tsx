import { useEffect, useState } from 'react'
import { equipCollectionTitle, getPlayerCollection, type CollectionTitle, type PlayerCollection } from '../../../api/collection'

type CollectionCategory = 'majak' | 'title' | 'trick'

interface Props {
  onClose: () => void
  onEquipChange: (collection: PlayerCollection) => void
}

function titleImageUrl(title: CollectionTitle, category: CollectionCategory): string {
  const code = title.titleId.slice(4).padStart(3, '0')
  if (category === 'trick') return `/assets/images/game/mj_skill_${code}.png`
  return title.titleId.startsWith('mjkc')
    ? `/assets/images/game/mj_ctitle_${code}.png`
    : `/assets/images/game/mj_title_${code}.png`
}

export default function CollectionDlg({ onClose, onEquipChange }: Props) {
  const [category, setCategory] = useState<CollectionCategory>('majak')
  const [collection, setCollection] = useState<PlayerCollection | null>(null)
  const [pendingTitleId, setPendingTitleId] = useState<string | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    getPlayerCollection()
      .then(value => { if (active) setCollection(value) })
      .catch(() => { if (active) setError('コレクションを読み込めませんでした。') })
    return () => { active = false }
  }, [])

  const items = category === 'majak'
    ? collection?.majakTitles ?? []
    : category === 'title'
      ? collection?.titleTitles ?? []
      : collection?.trickTitles ?? []
  const equip = async (titleId: string | null) => {
    if (pendingTitleId !== null) return
    setPendingTitleId(titleId ?? '__unequip__')
    setError('')
    try {
      const next = await equipCollectionTitle(category, titleId)
      setCollection(next)
      onEquipChange(next)
    } catch {
      setError('装着状態を更新できませんでした。')
    } finally {
      setPendingTitleId(null)
    }
  }

  return (
    <div className="majak-popup-overlay majak-collection-overlay" role="presentation">
      <section className="majak-popup-panel majak-collection-dialog" role="dialog" aria-modal="true" aria-labelledby="majak-collection-title">
        <header className="majak-popup-titlebar majak-collection-header">
          <div><h2 id="majak-collection-title">コレクション</h2></div>
          <button className="majak-popup-titlebar__close" type="button" onClick={onClose} aria-label="閉じる">×</button>
        </header>
        <div className="majak-collection-tabs" role="tablist" aria-label="コレクション種別">
          <button type="button" role="tab" aria-selected={category === 'majak'} className={category === 'majak' ? 'is-active' : ''} onClick={() => setCategory('majak')}>麻雀称号</button>
          <button type="button" role="tab" aria-selected={category === 'title'} className={category === 'title' ? 'is-active' : ''} onClick={() => setCategory('title')}>タイトル</button>
          <button type="button" role="tab" aria-selected={category === 'trick'} className={category === 'trick' ? 'is-active' : ''} onClick={() => setCategory('trick')}>技</button>
        </div>
        <div className="majak-popup-body majak-collection-content">
          {!collection && !error && <div className="majak-collection-status">読み込み中...</div>}
          {error && <div className="majak-collection-status is-error">{error}</div>}
          {collection && items.length === 0 && <div className="majak-collection-status">獲得済みの{category === 'majak' ? '麻雀称号' : category === 'title' ? 'タイトル' : '技'}はありません。</div>}
          <div className="majak-collection-grid">
            {items.map(title => (
              <article key={title.titleId} className={`majak-collection-item${title.isEquipped ? ' is-equipped' : ''}${title.isOwned ? '' : ' is-unowned'}`}>
                <div className="majak-collection-image"><img src={titleImageUrl(title, category)} alt="" onError={event => { event.currentTarget.style.display = 'none' }} /></div>
                <div className="majak-collection-item-copy"><strong>{title.titleName}</strong></div>
                <button
                  type="button"
                  className={title.isEquipped ? 'is-unequipable' : category !== 'trick' && title.isOwned ? 'is-equipable' : undefined}
                  disabled={!title.isOwned || pendingTitleId !== null}
                  onClick={() => void equip(title.isEquipped ? null : title.titleId)}
                >
                  {title.isEquipped ? '解除' : title.isOwned ? '装着' : '未獲得'}
                </button>
              </article>
            ))}
          </div>
        </div>
        <footer className="majak-popup-actions majak-collection-footer">
          <button type="button" onClick={onClose}>閉じる</button>
        </footer>
      </section>
    </div>
  )
}