import { Capacitor } from '@capacitor/core'
import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import MobileWebDownloadScreen from '../../components/MobileWebDownloadScreen'
import { useOutgameLayoutMode } from '../../hooks/useOutgameLayoutMode'
import { useAuthStore } from '../../store/authStore'

type TopPageProps = {
  onUnauthenticatedStart: () => void
}

type LockableScreenOrientation = ScreenOrientation & {
  lock?: (orientation: 'landscape') => Promise<void>
}

const gameModes = [
  { title: '交流戦', description: 'ルールや卓を選んで、気軽に対戦。' },
  { title: '段位戦', description: '一局ごとの読みと決断で、さらなる高みへ。' },
  { title: '大会', description: '緊張感あふれる勝負で、頂点を目指そう。' },
]

const showcaseTiles = [
  { id: 'one-man', frame: 0, label: '一萬' },
  { id: 'three-man', frame: 2, label: '三萬' },
  { id: 'five-man', frame: 4, label: '五萬' },
  { id: 'one-bamboo', frame: 10, label: '一索' },
  { id: 'three-bamboo', frame: 12, label: '三索' },
  { id: 'five-bamboo', frame: 14, label: '五索' },
  { id: 'one-circle', frame: 20, label: '一筒' },
  { id: 'three-circle', frame: 22, label: '三筒' },
  { id: 'five-circle', frame: 24, label: '五筒' },
  { id: 'red-dragon', frame: 35, label: '中' },
  { id: 'green-dragon', frame: 34, label: '發' },
  { id: 'east', frame: 30, label: '東' },
]

const tileReactions = [
  { image: '/assets/images/characters/thumbnail_01f.png', name: 'ミユ', line: 'その一打、いいね！' },
  { image: '/assets/images/characters/thumbnail_08m.png', name: 'タクミ', line: 'ここからが勝負だ。' },
  { image: '/assets/images/characters/thumbnail_14f.png', name: 'ユナ', line: '次のツモに期待しよう！' },
]

export default function TopPage({
  onUnauthenticatedStart,
}: TopPageProps) {
  const navigate = useNavigate()
  const { status, player } = useAuthStore()
  const layoutMode = useOutgameLayoutMode()
  const [selectedTileId, setSelectedTileId] = useState<string | null>(null)
  const [reaction, setReaction] = useState<{ character: (typeof tileReactions)[number]; id: number } | null>(null)
  const [showMobileDownload, setShowMobileDownload] = useState(false)
  const isLoggedIn = status === 'ok' && Boolean(player && !player.requiresRegistration && player.accountStatus === 1)
  const selectedTile = showcaseTiles.find(tile => tile.id === selectedTileId)
  useEffect(() => {
    if (!reaction) return
    const timer = window.setTimeout(() => setReaction(null), 3000)
    return () => window.clearTimeout(timer)
  }, [reaction])

  const selectTile = (tile: (typeof showcaseTiles)[number]) => {
    setSelectedTileId(tile.id)
    setReaction({ character: tileReactions[Math.floor(Math.random() * tileReactions.length)], id: Date.now() })
  }
  const startGame = async () => {
    if (!Capacitor.isNativePlatform() && layoutMode !== 'desktop') {
      setShowMobileDownload(true)
      return
    }
    if (!isLoggedIn) {
      onUnauthenticatedStart()
      return
    }
    if (Capacitor.isNativePlatform()) {
      await (window.screen.orientation as LockableScreenOrientation | undefined)?.lock?.('landscape').catch(() => {})
    }
    navigate('/channel')
  }

  if (showMobileDownload) return <MobileWebDownloadScreen onBackToTop={() => setShowMobileDownload(false)} />

  return (
    <main className="top-page">
      <header className="top-page__header">
        <a className="top-page__brand" href="#top" aria-label="麻雀4 トップへ"><img className="top-page__brand-logo" src="/assets/images/common/ico_big_majak4.jpg" alt="" /><span>麻雀4</span></a>
        <nav className="top-page__nav" aria-label="ページ内ナビゲーション"><a href="#about">麻雀4とは</a><a href="#play">遊び方</a></nav>
        <button type="button" className="top-page__header-action" onClick={() => { void startGame() }}><span className="top-page__start-tile" aria-hidden="true">一</span>ゲームスタート</button>
      </header>

      <section id="top" className="top-page__hero">
        <div className="top-page__hero-copy">
          <p className="top-page__eyebrow">REALTIME ONLINE RIICHI MAHJONG</p>
          <h1>その一打で、<br />流れを変えろ。</h1>
          <p className="top-page__lead">すぐに集まって、じっくり読み合う。<br />麻雀4で始める、熱くて軽やかなオンライン麻雀。</p>
          <p className="top-page__tile-action" aria-live="polite">{selectedTile ? `${selectedTile.label}を切る。勝負はここから。` : '牌を選んで、一局を動かそう。'}</p>
        </div>
        <div className="top-page__hero-table" aria-label="麻雀牌を選ぶ"><div className="top-page__tile-board">{showcaseTiles.map(tile => <button key={tile.id} type="button" className={`top-page__showcase-tile${selectedTileId === tile.id ? ' is-selected' : ''}`} style={{ backgroundPosition: `${-tile.frame * 37}px 0` }} onClick={() => selectTile(tile)} aria-label={`${tile.label}を切る`} />)}</div>{reaction && <div key={reaction.id} className="top-page__tile-reaction" role="status"><img src={reaction.character.image} alt="" /><p><strong>{reaction.character.name}</strong>{reaction.character.line}</p></div>}</div>
      </section>

      <section id="play" className="top-page__play"><div className="top-page__play-heading"><h2>今日は、どの卓で打つ？</h2></div><div className="top-page__modes">{gameModes.map(mode => <article key={mode.title} className="top-page__mode"><div><h3>{mode.title}</h3><p>{mode.description}</p></div></article>)}</div></section>

      <section id="about" className="top-page__intro"><h2>気軽に集まり、<br />何度でも勝負する。</h2><p>交流戦でわいわい楽しむ日も、段位戦で一手を磨く夜も。<br />牌を切るたびに生まれる駆け引きが、次の一局をもっと面白くします。</p><dl className="top-page__rule-summary"><div><dt>4人対戦</dt><dd>リアルタイムで集まる</dd></div><div><dt>136枚</dt><dd>34種の牌で役を作る</dd></div><div><dt>東風・半荘</dt><dd>時間に合わせて選べる</dd></div></dl></section>

      <section className="top-page__manual-summary" aria-label="公式マニュアル要約">
        <h2>はじめてでも、すぐ打てる。</h2>
        <div className="top-page__manual-summary-grid">
          <p><strong>基本無料</strong>プレイは無料。GPが不足したら、1日1回の無料補充を利用できます。</p>
          <p><strong>ゲームの流れ</strong>13枚の手牌に1枚をツモり、不要な牌を捨てて14枚の和了形を目指します。</p>
          <p><strong>3つのモード</strong>ルールを選ぶ交流戦、実力を競う段位戦、条件戦の大会を用意。</p>
        </div>
      </section>

      <section id="top-page-play" className="top-page__cta"><div><h2>{isLoggedIn ? `${player?.name ?? 'プレイヤー'}さん、卓が待っています。` : 'さあ、最初の一局へ。'}</h2><p>{isLoggedIn ? '対戦メニューから、今の気分にぴったりの卓を選びましょう。' : 'ゲームスタートからログインして、すぐに対局へ参加できます。'}</p></div><button type="button" className="top-page__primary-action" onClick={() => { void startGame() }}><span className="top-page__start-tile" aria-hidden="true">一</span><span>ゲームスタート</span><strong>GAME START</strong></button></section>

      <footer className="top-page__footer">MAHJONG 4 <span>ONLINE RIICHI MAHJONG</span></footer>
    </main>
  )
}