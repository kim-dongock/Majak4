/**
 * プレイヤー情報ダイアログ
 */
import { useEffect, useState } from 'react'
import * as SignalR from '../../../api/signalr'
import { getAvatarUrl, getDefaultAvatarUrl } from '../../../utils/resources'
import { gradeLevelName } from '../../../utils/grade'

const IMG = '/assets/images/game'

export interface PlayerInfo {
  pix: string
  name: string
  avatarId?: string
  sex?: 'male' | 'female' | 'M' | 'F'
  rating?: number
  slevel?: string
  location?: string
  winCount?: number
  loseCount?: number
  drawCount?: number
  lastLogin?: string
  titleId?: number
  nLevel?: number
  gradeLevel?: number
  /** mjkk46e: トリックタイトル */
  trickTitle?: number
  /** mjkk47e: マジャクタイトル */
  majakTitle?: number
  /** mjkk54e: リーチ演出 */
  richiEffect?: number
  /** mjkk41e: コイン貸付額 */
  lentMoney?: number
  /** mjkk136e: キャラクターID */
  charaId?: number
  /** mjkk137e: キャラクタータイプ */
  charaType?: number
}

interface Props {
  player: PlayerInfo
  onClose: () => void
  /** 1対1対戦申込ボタン表示 */
  showOneToOne?: boolean
  onOneToOne?: () => void
  /** OnBtnName 相当 — 名前ブラックリスト等 */
  onBtnName?: () => void
  /** TCN_SELCHANGE 相当 — タブ切替時のデータ取得コールバック */
  onTabChange?: (tabIndex: number) => void
}

type DetailRecord = {
  name?: string
  rating: number
  matchCnt: number
  winCnt: number
  defeatCnt: number
  drawCnt: number
  grade1: number
  grade2: number
  grade3: number
  grade4: number
  pointSum: number
  kyokuCnt: number
  horaCnt: number
  horaPoint: number
  hojuCnt: number
  hojuPoint: number
  richiCnt: number
  furoCnt: number
  tipPoint: number
  tipMatchCnt: number
  tobiCnt: number
  tobashiCnt: number
  doraCnt: number
  uraDoraCnt: number
  richiHoraCnt: number
}

type DetailInfo = {
  pix: string
  name?: string
  avatarId?: string
  regular: DetailRecord
  hiClass: DetailRecord
  gradeMode: DetailRecord
  gradeLevel?: number
  gradePoint?: number
  gradeMaxPoint?: number
  trickTitle: number
  majakTitle: number
}

const TABS = [
  { label: '全体戦績' },
  { label: '交流広場' },
  { label: '段位戦' },
]

function padTitleId(id: number) {
  return String(Math.trunc(id)).padStart(3, '0')
}

function titleImageName(id: number) {
  return id >= 1000 ? `mj_ctitle_${padTitleId(id - 1000)}.png` : `mj_title_${padTitleId(id)}.png`
}

function toNumber(value: unknown) {
  return Number(value ?? 0) || 0
}

function readRecord(value: unknown): DetailRecord {
  const record = (value && typeof value === 'object') ? value as Record<string, unknown> : {}
  return {
    rating: toNumber(record.rating),
    matchCnt: toNumber(record.matchCnt),
    winCnt: toNumber(record.winCnt),
    defeatCnt: toNumber(record.defeatCnt),
    drawCnt: toNumber(record.drawCnt),
    grade1: toNumber(record.grade1),
    grade2: toNumber(record.grade2),
    grade3: toNumber(record.grade3),
    grade4: toNumber(record.grade4),
    pointSum: toNumber(record.pointSum),
    kyokuCnt: toNumber(record.kyokuCnt),
    horaCnt: toNumber(record.horaCnt),
    horaPoint: toNumber(record.horaPoint),
    hojuCnt: toNumber(record.hojuCnt),
    hojuPoint: toNumber(record.hojuPoint),
    richiCnt: toNumber(record.richiCnt),
    furoCnt: toNumber(record.furoCnt),
    tipPoint: toNumber(record.tipPoint),
    tipMatchCnt: toNumber(record.tipMatchCnt),
    tobiCnt: toNumber(record.tobiCnt),
    tobashiCnt: toNumber(record.tobashiCnt),
    doraCnt: toNumber(record.doraCnt),
    uraDoraCnt: toNumber(record.uraDoraCnt),
    richiHoraCnt: toNumber(record.richiHoraCnt),
  }
}

function mergeRecord(a: DetailRecord, b: DetailRecord): DetailRecord {
  return {
    rating: a.rating,
    matchCnt: a.matchCnt + b.matchCnt,
    winCnt: a.winCnt + b.winCnt,
    defeatCnt: a.defeatCnt + b.defeatCnt,
    drawCnt: a.drawCnt + b.drawCnt,
    grade1: a.grade1 + b.grade1,
    grade2: a.grade2 + b.grade2,
    grade3: a.grade3 + b.grade3,
    grade4: a.grade4 + b.grade4,
    pointSum: a.pointSum + b.pointSum,
    kyokuCnt: a.kyokuCnt + b.kyokuCnt,
    horaCnt: a.horaCnt + b.horaCnt,
    horaPoint: a.horaPoint + b.horaPoint,
    hojuCnt: a.hojuCnt + b.hojuCnt,
    hojuPoint: a.hojuPoint + b.hojuPoint,
    richiCnt: a.richiCnt + b.richiCnt,
    furoCnt: a.furoCnt + b.furoCnt,
    tipPoint: a.tipPoint + b.tipPoint,
    tipMatchCnt: a.tipMatchCnt + b.tipMatchCnt,
    tobiCnt: a.tobiCnt + b.tobiCnt,
    tobashiCnt: a.tobashiCnt + b.tobashiCnt,
    doraCnt: a.doraCnt + b.doraCnt,
    uraDoraCnt: a.uraDoraCnt + b.uraDoraCnt,
    richiHoraCnt: a.richiHoraCnt + b.richiHoraCnt,
  }
}

function emptyRecordFromPlayer(player: PlayerInfo): DetailRecord {
  return {
    rating: player.rating ?? 0,
    matchCnt: (player.winCount ?? 0) + (player.loseCount ?? 0) + (player.drawCount ?? 0),
    winCnt: player.winCount ?? 0,
    defeatCnt: player.loseCount ?? 0,
    drawCnt: player.drawCount ?? 0,
    grade1: 0,
    grade2: 0,
    grade3: 0,
    grade4: 0,
    pointSum: 0,
    kyokuCnt: 0,
    horaCnt: 0,
    horaPoint: 0,
    hojuCnt: 0,
    hojuPoint: 0,
    richiCnt: 0,
    furoCnt: 0,
    tipPoint: 0,
    tipMatchCnt: 0,
    tobiCnt: 0,
    tobashiCnt: 0,
    doraCnt: 0,
    uraDoraCnt: 0,
    richiHoraCnt: 0,
  }
}

function percent(numerator: number, denominator: number) {
  return denominator > 0 ? `${((numerator * 100) / denominator).toFixed(2)}%` : '---.--%'
}

/** ====================================================================
 * CMJBmpButton 相当 — 4フレームスプライトボタン (AP-06 §2)
 * ==================================================================== */
export default function PlayerInfoWnd({ player, onClose, onTabChange }: Props) {
  const [activeTab, setActiveTab] = useState(0)
  const [detail, setDetail] = useState<DetailInfo | null>(null)

  useEffect(() => {
    const handler = (data: Record<string, unknown>) => {
      if (Number(data.result) !== 1) return
      const responsePix = String(data.k3e ?? data.pix ?? '')
      if (responsePix !== player.pix) return
      setDetail({
        pix: responsePix,
        avatarId: data.avatarId != null ? String(data.avatarId) : undefined,
        regular: readRecord(data.regular),
        hiClass: readRecord(data.hiClass),
        gradeMode: readRecord(data.gradeMode),
        gradeLevel: data.gradeLevel != null ? Number(data.gradeLevel) : undefined,
        gradePoint: data.gradePoint != null ? Number(data.gradePoint) : undefined,
        gradeMaxPoint: data.gradeMaxPoint != null ? Number(data.gradeMaxPoint) : undefined,
        trickTitle: toNumber(data.trickTitle),
        majakTitle: toNumber(data.majakTitle),
      })
    }

    SignalR.on('mjkc1e', handler)
    SignalR.send('mjkc1e', { pix: player.pix, k3e: player.pix }).catch(() => {})
    return () => SignalR.off('mjkc1e', handler)
  }, [player.pix])

  /** TCN_SELCHANGE 相当 */
  const handleTabChange = (i: number) => {
    setActiveTab(i)
    onTabChange?.(i)
  }

  const fallbackRecord = emptyRecordFromPlayer(player)
  const tabRecord = detail
    ? activeTab === 0
      ? mergeRecord(mergeRecord(detail.regular, detail.hiClass), detail.gradeMode)
      : activeTab === 1
        ? detail.hiClass
        : detail.gradeMode
    : fallbackRecord
  const rankCnt = tabRecord.grade1 + tabRecord.grade2 + tabRecord.grade3 + tabRecord.grade4
  const averageRank = rankCnt > 0
    ? `${((tabRecord.grade1 * 1 + tabRecord.grade2 * 2 + tabRecord.grade3 * 3 + tabRecord.grade4 * 4) / rankCnt).toFixed(2)}位`
    : '-.--位'
  const averageSet = rankCnt > 0 ? `${tabRecord.pointSum >= 0 ? '+' : ''}${(tabRecord.pointSum / rankCnt).toFixed(2)}` : '-.--'
  const matchCnt = tabRecord.matchCnt
  const sexText = player.sex === 'female' || player.sex === 'F' ? '女' : player.sex === 'male' || player.sex === 'M' ? '男' : '-'
  const avatarSex = player.sex === 'female' || player.sex === 'F' ? 'female' : 'male'
  const trickTitle = detail?.trickTitle ?? player.trickTitle ?? 0
  const majakTitle = detail?.majakTitle ?? player.majakTitle ?? 0
  const avatarId = detail?.avatarId ?? player.avatarId
  const titleText = player.slevel || (majakTitle > 0 ? `実績称号 ${majakTitle}` : '資産なし')
  const gradeLevel = detail?.gradeLevel ?? player.gradeLevel
  const gradeText = gradeLevelName(gradeLevel)
  const averageTip = tabRecord.tipMatchCnt > 0 ? `${(tabRecord.tipPoint / tabRecord.tipMatchCnt).toFixed(2)}` : '---.--'

  return (
    <div className="majak-popup-overlay majak-player-profile-overlay" role="presentation">
      <section className="majak-popup-panel majak-player-profile" role="dialog" aria-modal="true" aria-labelledby="majak-player-profile-name">
        <header className="majak-popup-titlebar majak-player-profile__titlebar">
          <span>プレイヤー情報</span>
          <button type="button" className="majak-popup-titlebar__close" onClick={onClose} aria-label="閉じる">×</button>
        </header>
        <header className="majak-player-profile__hero">
          <div className="majak-player-profile__avatar">
            <img
              src={getAvatarUrl(avatarId ?? null)}
              alt=""
              onError={event => { event.currentTarget.src = getDefaultAvatarUrl(avatarSex) }}
            />
          </div>
          <div className="majak-player-profile__title-art" aria-hidden="true">
            {trickTitle > 0 && (
              <img
                className="majak-player-profile__trick-title-image"
                src={`${IMG}/mj_skill_${padTitleId(trickTitle)}.png`}
                alt=""
              />
            )}
            {majakTitle > 0 && (
              <>
                <img className="majak-player-profile__title-base" src={`${IMG}/mj_title_base.png`} alt="" />
                <img className="majak-player-profile__majak-title-image" src={`${IMG}/${titleImageName(majakTitle)}`} alt="" />
              </>
            )}
          </div>
          <div className="majak-player-profile__identity">
            <h2 id="majak-player-profile-name">{detail?.name ?? player.name}</h2>
            <div className="majak-player-profile__badges">
              <span>{titleText}</span>
              <span>段位 {gradeText}{detail?.gradePoint != null && ` ${detail.gradePoint}/${detail.gradeMaxPoint ?? '-'}`}</span>
              {trickTitle > 0 && <span>演出 {trickTitle}</span>}
              {player.location && <span>{player.location}</span>}
            </div>
          </div>
          <div className="majak-player-profile__rating">
            <span>レーティング</span>
            <strong>{(player.rating ?? 0).toLocaleString()}</strong>
            <small>{sexText}</small>
          </div>
        </header>

        <div className="majak-player-profile__tabs" role="tablist" aria-label="戦績区分">
          {TABS.map((tab, index) => (
            <button
              key={tab.label}
              type="button"
              role="tab"
              aria-selected={activeTab === index}
              className={activeTab === index ? 'is-active' : ''}
              onClick={() => handleTabChange(index)}
            >
              {tab.label}
            </button>
          ))}
        </div>

        <div className="majak-popup-body majak-player-profile__summary">
          <div><span>対局数</span><strong>{matchCnt.toLocaleString()}</strong></div>
          <div><span>平均順位</span><strong>{averageRank}</strong></div>
          <div><span>平均収支</span><strong>{averageSet}</strong></div>
          <div><span>順位構成</span><strong>{tabRecord.grade1} / {tabRecord.grade2} / {tabRecord.grade3} / {tabRecord.grade4}</strong></div>
        </div>

        <div className="majak-popup-body majak-player-profile__metrics">
          <section>
            <h3>順位・収支</h3>
            <dl>
              <div><dt>1位率</dt><dd>{percent(tabRecord.grade1, rankCnt)}</dd></div>
              <div><dt>2位率</dt><dd>{percent(tabRecord.grade2, rankCnt)}</dd></div>
              <div><dt>3位率</dt><dd>{percent(tabRecord.grade3, rankCnt)}</dd></div>
              <div><dt>4位率</dt><dd>{percent(tabRecord.grade4, rankCnt)}</dd></div>
              <div><dt>飛び率</dt><dd>{percent(tabRecord.tobiCnt, rankCnt)}</dd></div>
              <div><dt>飛ばし率</dt><dd>{percent(tabRecord.tobashiCnt, rankCnt)}</dd></div>
            </dl>
          </section>
          <section>
            <h3>対局内容</h3>
            <dl>
              <div><dt>和了率</dt><dd>{percent(tabRecord.horaCnt, tabRecord.kyokuCnt)}</dd></div>
              <div><dt>放銃率</dt><dd>{percent(tabRecord.hojuCnt, tabRecord.kyokuCnt)}</dd></div>
              <div><dt>平均和了点</dt><dd>{tabRecord.horaCnt > 0 ? `${Math.trunc(tabRecord.horaPoint / tabRecord.horaCnt)}点` : '---点'}</dd></div>
              <div><dt>平均放銃点</dt><dd>{tabRecord.hojuCnt > 0 ? `${Math.trunc(tabRecord.hojuPoint / tabRecord.hojuCnt)}点` : '---点'}</dd></div>
              <div><dt>立直率</dt><dd>{percent(tabRecord.richiCnt, tabRecord.kyokuCnt)}</dd></div>
              <div><dt>副露率</dt><dd>{percent(tabRecord.furoCnt, tabRecord.kyokuCnt)}</dd></div>
            </dl>
          </section>
          <section>
            <h3>牌・チップ</h3>
            <dl>
              <div><dt>平均ドラ</dt><dd>{tabRecord.horaCnt > 0 ? `${(tabRecord.doraCnt / tabRecord.horaCnt).toFixed(2)}枚` : '--.--枚'}</dd></div>
              <div><dt>平均裏ドラ</dt><dd>{tabRecord.richiHoraCnt > 0 ? `${(tabRecord.uraDoraCnt / tabRecord.richiHoraCnt).toFixed(2)}枚` : '--.--枚'}</dd></div>
              <div><dt>平均チップ</dt><dd>{activeTab === 2 ? '--.--' : averageTip}</dd></div>
              <div><dt>勝</dt><dd>{tabRecord.winCnt.toLocaleString()}</dd></div>
              <div><dt>敗</dt><dd>{tabRecord.defeatCnt.toLocaleString()}</dd></div>
              <div><dt>分</dt><dd>{tabRecord.drawCnt.toLocaleString()}</dd></div>
            </dl>
          </section>
        </div>

        <footer className="majak-popup-actions majak-player-profile__footer">
          <p>{activeTab === 0 ? '全ての対局戦績を表示しています。' : activeTab === 1 ? '交流広場の対局戦績を表示しています。' : '段位戦の対局戦績を表示しています。'}</p>
          <div>
            <button type="button" className="majak-player-profile__done" onClick={onClose}>閉じる</button>
          </div>
        </footer>
      </section>
    </div>
  )
}
