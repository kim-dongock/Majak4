import { useEffect, useState } from 'react'
import { getAvatarUrl, getDefaultAvatarUrl } from '../../utils/resources'
import type { HanResPlayer } from './HanRes'
import type { KyoPlayer, KyoResData, KyoYaku } from './KyoRes'

type KyoProps = {
  data: KyoResData
  myOdr?: number
  canContinue: boolean
  waitingForOtherPlayers?: boolean
  playerProgress?: Record<number, { durationMs: number; localDeadlineAt: number; submitted: boolean }>
  onClose: () => void
}

type HanProps = {
  players: HanResPlayer[]
  hasTor?: boolean
  hasTip?: boolean
  isViewer?: boolean
  isTournament?: boolean
  onClose: () => void
}

function formatNumber(value: number | undefined): string {
  return new Intl.NumberFormat('ja-JP', { signDisplay: 'always' }).format(value ?? 0)
}

function AnimatedNumber({ value, signed = false, delay = 0, suffix }: { value: number; signed?: boolean; delay?: number; suffix?: string }) {
  const [displayValue, setDisplayValue] = useState(0)

  useEffect(() => {
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    if (reduceMotion) {
      setDisplayValue(value)
      return
    }

    let frameId = 0
    let startTime: number | undefined
    const timerId = window.setTimeout(() => {
      const animate = (time: number) => {
        startTime ??= time
        const progress = Math.min((time - startTime) / 760, 1)
        const easedProgress = 1 - (1 - progress) ** 3
        setDisplayValue(Math.round(value * easedProgress))
        if (progress < 1) frameId = window.requestAnimationFrame(animate)
      }
      frameId = window.requestAnimationFrame(animate)
    }, delay)

    return () => {
      window.clearTimeout(timerId)
      window.cancelAnimationFrame(frameId)
    }
  }, [delay, value])

  const number = signed
    ? formatNumber(displayValue)
    : new Intl.NumberFormat('ja-JP').format(displayValue)
  return <>{number}{suffix ? ` ${suffix}` : ''}</>
}

function playerAvatar(player: { avatarId?: string; sex?: string }) {
  const sex = player.sex === 'F' || player.sex === 'female' ? 'female' : 'male'
  return getAvatarUrl(player.avatarId ?? null) || getDefaultAvatarUrl(sex)
}

function resultLabel(data: KyoResData): string {
  if (data.pinType === 0) return 'RON'
  if (data.pinType === 1) return 'TSUMO'
  if (data.pinType === 9) return '流し満貫'
  return '流局'
}

function playerResultLabel(data: KyoResData, player: KyoPlayer): string {
  if (player.isHora) return '和了'
  if (player.isHoju) return '放銃'
  if (data.pinType === 5) return player.isTempai ? '聴牌' : 'ノーテン'
  if (data.pinType === 9) return player.isNagashiMangan ? '流し満貫' : '流局'
  return '流局'
}

function AnimatedLegacyStatus({ status }: { status: 'tsumo' | 'ron' | 'hoju' }) {
  const type = status === 'hoju' ? 'hoju' : 'hora'
  const frameCount = type === 'hora' ? 2 : 4
  const [frame, setFrame] = useState(0)

  useEffect(() => {
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return
    const timer = window.setInterval(() => setFrame(current => (current + 1) % frameCount), 200)
    return () => window.clearInterval(timer)
  }, [frameCount])

  return (
    <span className={`majak-result-player__animated-status is-${status}`} role="img" aria-label={status === 'tsumo' ? '自摸和了' : status === 'ron' ? '栄和了' : '放銃'}>
      <span className="majak-result-player__status-label" aria-hidden="true" />
      <img
        className="majak-result-player__status-image"
        src={`/assets/images/game/mj_ef_status_${type}_${String(frame).padStart(2, '0')}.png`}
        width={122}
        height={48}
        alt=""
        aria-hidden="true"
      />
    </span>
  )
}

function playerChange(player: KyoPlayer): number {
  return (player.tenBaseBal ?? player.tenBal)
    + (player.paoBal ?? 0)
    + (player.warBal ?? 0)
    + (player.ribBal ?? 0)
    + (player.renBal ?? 0)
}

function totalFor(data: KyoResData, player: KyoPlayer, index: number): number | undefined {
  return data.totalsByPlayer?.[index]?.totalTen ?? data.totalTen ?? player.tenBal
}

function yakuList(data: KyoResData, index: number): KyoYaku[] {
  return data.yakuByPlayer?.[index] ?? data.yaku ?? []
}

function tileName(code: number): string {
  const suit = ['萬', '筒', '索']
  const kind = (code >> 4) & 0x0f
  const number = code & 0x0f
  return kind < 3 ? `${number}${suit[kind]}` : ['東', '南', '西', '北', '白', '發', '中'][number - 1] ?? String(code)
}

function HandSettlementCells({ player, delay }: { player: KyoPlayer; delay: number }) {
  const pointChange = (player.tenBaseBal ?? player.tenBal) + (player.paoBal ?? 0)
  const values = [pointChange, player.warBal ?? 0, player.ribBal ?? 0, player.renBal ?? 0, player.tipBal ?? 0]
  return (
    <>{values.map((value, index) => (
      <span key={index} className={`majak-result-player__value${value >= 0 ? ' is-plus' : ''}`}>
        <AnimatedNumber value={value} signed delay={delay + index * 45} />
      </span>
    ))}</>
  )
}

export function ResponsiveKyoResult({ data, myOdr, canContinue, waitingForOtherPlayers = false, playerProgress = {}, onClose }: KyoProps) {
  const winners = data.players
    .map((player, index) => ({ player, index }))
    .filter(({ player }) => player.isHora)
  const [selectedIndex, setSelectedIndex] = useState(winners[0]?.index ?? 0)
  const selected = data.players[selectedIndex] ?? data.players[0]
  const yaku = yakuList(data, selectedIndex)
  const totals = data.totalsByPlayer?.[selectedIndex]
  const isHora = data.pinType === 0 || data.pinType === 1
  const [now, setNow] = useState(() => performance.now())
  const localProgress = myOdr == null ? undefined : playerProgress[myOdr]
  const hasPendingLocalPlayer = Boolean(localProgress && !localProgress.submitted)

  useEffect(() => {
    setSelectedIndex(winners[0]?.index ?? 0)
  }, [data, winners.length])

  useEffect(() => {
    if (!hasPendingLocalPlayer) return
    const timer = window.setInterval(() => setNow(performance.now()), 200)
    return () => window.clearInterval(timer)
  }, [hasPendingLocalPlayer])

  const advance = () => {
    const winnerPosition = winners.findIndex(item => item.index === selectedIndex)
    if (winnerPosition >= 0 && winnerPosition < winners.length - 1) {
      setSelectedIndex(winners[winnerPosition + 1].index)
      return
    }
    onClose()
  }

  return (
    <div className="majak-result-overlay" role="dialog" aria-modal="true" aria-label="局結果">
      <section className={`majak-result-panel majak-kyo-result-panel${isHora ? ' is-hora' : ' is-draw'}`}>
        <header className="majak-result-header">
          <div>
            <span className="majak-result-kicker">HAND RESULT</span>
            <h2>{resultLabel(data)}</h2>
          </div>
          <div className="majak-result-round">{data.kyoNum != null ? `${Math.floor(data.kyoNum / 4) + 1}場 ${data.kyoNum % 4 + 1}局` : '局結果'}</div>
        </header>

        <div className="majak-kyo-result-body">
          <section className="majak-result-players" role="table" aria-label="点数移動">
            <div className="majak-result-players__head" role="row">
              <span>プレイヤー</span>
              <span>結果</span>
              <span>収支</span>
              <span>点数</span>
              <span>割れ目</span>
              <span>供託</span>
              <span>本場</span>
              <span>チップ</span>
              <span>詳細</span>
            </div>
            {data.players.map((player, index) => {
              const selectedWinner = index === selectedIndex && player.isHora
              return (
                <div
                  key={`${player.pix}-${player.seatPos}`}
                  className={`majak-result-player${player.isHora ? ' is-winner' : ''}${player.isHoju ? ' is-dealer-in' : ''}${selectedWinner ? ' is-selected' : ''}`}
                  role="row"
                >
                  <span className="majak-result-player__identity">
                    <img src={playerAvatar(player)} alt="" onError={event => { event.currentTarget.src = getDefaultAvatarUrl('male') }} />
                    <span><strong>{player.name || player.pix}</strong><small>{player.isOya ? '親' : '子'}{data.waremeOdr === player.seatPos ? ' / 割れ目' : ''}</small></span>
                  </span>
                  <span className="majak-result-player__status">
                    {player.isHora
                      ? <AnimatedLegacyStatus status={data.pinType === 1 ? 'tsumo' : 'ron'} />
                      : player.isHoju
                        ? <AnimatedLegacyStatus status="hoju" />
                        : playerResultLabel(data, player)}
                  </span>
                  <span className={`majak-result-player__change${playerChange(player) >= 0 ? ' is-plus' : ''}`}><AnimatedNumber value={playerChange(player)} signed delay={index * 100} /></span>
                  <HandSettlementCells player={player} delay={index * 100 + 120} />
                  <span className="majak-result-player__detail-cell">{player.isHora && <button type="button" className="majak-result-player__select" onClick={() => setSelectedIndex(index)}>表示</button>}</span>
                </div>
              )
            })}
          </section>

          {selected && isHora && (
            <section className="majak-result-detail" aria-label="和了詳細">
              <>
                <div className="majak-result-yaku">
                  {yaku.length > 0 ? yaku.map((item, index) => (
                    <div key={`${item.name}-${index}`}><span>{item.name}</span><b>{item.isYakuman ? `${item.fan}倍役満` : `${item.fan}翻`}{item.tip ? ` / チップ ${item.tip}` : ''}</b></div>
                  )) : <p>役情報はありません</p>}
                </div>
                <div className="majak-result-total">
                  <span>{totals?.totalFu ?? data.totalFu ?? 0}符 {totals?.totalFan ?? data.totalFan ?? 0}翻</span>
                  <strong><AnimatedNumber value={totalFor(data, selected, selectedIndex) ?? 0} suffix="点" /></strong>
                </div>
                {(data.dora?.length || data.uraDora?.length) ? <div className="majak-result-dora">ドラ {data.dora?.map(tileName).join(' / ') || '-'} {selected.isRichi && data.contest !== 1 && data.uraDora?.length ? ` 裏ドラ ${data.uraDora.map(tileName).join(' / ')}` : ''}</div> : null}
              </>
            </section>
          )}
        </div>

        {data.players.length > 0 && (
          <section className="majak-kyo-confirmation-progress" aria-label="プレイヤーの確認状況">
            {data.players.map((player, index) => {
              const progress = playerProgress[player.seatPos ?? index]
              const confirmed = Boolean(progress?.submitted)
              const isLocalPlayer = player.seatPos === myOdr
              const remainingMs = confirmed ? 0 : Math.max(0, (progress?.localDeadlineAt ?? now) - now)
              const percent = progress && progress.durationMs > 0
                ? Math.round(Math.min(1, remainingMs / progress.durationMs) * 100)
                : 0
              return (
              <div key={`${player.pix}-${player.seatPos}`} className={`majak-kyo-confirmation-progress__player${confirmed ? ' is-confirmed' : ''}${isLocalPlayer && progress && !confirmed ? ' is-timed' : ''}`}>
                <span>{player.name || player.pix}</span>
                {confirmed
                  ? <strong>確認済み</strong>
                  : isLocalPlayer && progress
                    ? <><div role="progressbar" aria-label={`${player.name || player.pix}の確認時間`} aria-valuemin={0} aria-valuemax={100} aria-valuenow={percent}><i style={{ width: `${percent}%` }} /></div><strong>{Math.ceil(remainingMs / 1000)}秒</strong></>
                    : <strong>確認待ち</strong>}
              </div>
              )
            })}
          </section>
        )}

        <footer className="majak-result-actions">
          <span>{waitingForOtherPlayers
            ? '他のプレイヤーの確認を待っています'
            : `本場 ${data.renCnt ?? 0} / 供託 ${data.ribCnt ?? 0}${(totals?.tipBal ?? data.tipBal) ? ` / チップ ${totals?.tipBal ?? data.tipBal}` : ''}`}</span>
          <button type="button" onClick={advance} disabled={!canContinue || waitingForOtherPlayers}>{waitingForOtherPlayers ? '確認済み' : winners.length > 1 && selectedIndex !== winners[winners.length - 1]?.index ? '次の和了者' : '続ける'}</button>
        </footer>
      </section>
    </div>
  )
}

export function ResponsiveHanResult({ players, hasTor, hasTip, isViewer, isTournament, onClose }: HanProps) {
  const rankedPlayers = [...players].sort((left, right) => left.rank - right.rank)
  const me = players.find(player => player.isMe)
  const showYakitori = hasTor || players.some(player => player.setTor !== undefined)
  const showTip = hasTip || players.some(player => player.setTip !== undefined)
  const showRoomCharge = players.some(player => (player.dealerFee ?? 0) > 0)

  return (
    <div className="majak-result-overlay" role="dialog" aria-modal="true" aria-label="最終結果">
      <section className="majak-result-panel majak-han-result-panel">
        <header className="majak-result-header">
          <div><span className="majak-result-kicker">FINAL RESULT</span><h2>対局結果</h2></div>
          <span className="majak-result-round">{isTournament ? 'TOURNAMENT' : 'MATCH COMPLETE'}</span>
        </header>

        <div className="majak-han-result-table" role="table" aria-label="最終順位">
          <div className="majak-han-result-table__head" role="row">
            <span>順位</span><span>プレイヤー</span><span>最終点</span><span>合計</span>
            <span className="majak-han-result-table__detail-head">
              <span>点数</span><span>ウマ</span>{showYakitori && <span>焼き鳥</span>}{showTip && <span>チップ</span>}{showRoomCharge && <span>場代</span>}
            </span>
          </div>
          {rankedPlayers.map((player, index) => (
            <div key={`${player.pix}-${player.seatPos}`} className={`majak-han-result-row${player.isMe ? ' is-me' : ''}`} role="row">
              <strong className={`majak-result-rank rank-${player.rank + 1}`}>{player.rank + 1}</strong>
              <span className="majak-han-result-player"><img src={playerAvatar(player)} alt="" onError={event => { event.currentTarget.src = getDefaultAvatarUrl('male') }} />{player.name || player.pix}</span>
              <b><AnimatedNumber value={player.point} delay={index * 110} /></b>
              <b className={player.setBal >= 0 ? 'is-plus' : ''}><AnimatedNumber value={player.setBal} signed delay={index * 110 + 100} /></b>
              <dl className="majak-han-result-detail">
                <div><dt>点数</dt><dd><AnimatedNumber value={player.setTen} signed delay={index * 110 + 180} /></dd></div>
                <div><dt>ウマ</dt><dd><AnimatedNumber value={player.setUma} signed delay={index * 110 + 220} /></dd></div>
                {showYakitori && <div><dt>焼き鳥</dt><dd><AnimatedNumber value={player.setTor ?? 0} signed delay={index * 110 + 260} /></dd></div>}
                {showTip && <div><dt>チップ</dt><dd><AnimatedNumber value={player.setTip ?? 0} signed delay={index * 110 + 300} /></dd></div>}
                {showRoomCharge && <div><dt>場代</dt><dd><AnimatedNumber value={-(player.dealerFee ?? 0)} signed delay={index * 110 + 340} suffix="GP" /></dd></div>}
              </dl>
            </div>
          ))}
        </div>

        {!isViewer && !isTournament && me && <div className="majak-result-reward"><span>今回の収支</span><strong><AnimatedNumber value={me.coinGain ?? 0} delay={560} suffix="GP" /></strong>{me.coinNeed != null && <small>次の資産ランクまで <AnimatedNumber value={me.coinNeed} delay={620} suffix="GP" /></small>}</div>}
        <footer className="majak-result-actions"><span>{isViewer ? '観戦モード' : '最終順位が確定しました'}</span><button type="button" onClick={onClose}>OK</button></footer>
      </section>
    </div>
  )
}
