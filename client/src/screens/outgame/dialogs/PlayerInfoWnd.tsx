/**
 * CMJPlayerInfo 相当 — プレイヤー情報ウィンドウ (AP-09 §1-8)
 * レガシー: legacy/client/HgMajak2/MJPlayerInfo.h/cpp
 *
 * ウィンドウ: mj_sen_win_base.png 353×595px
 * タブ: COwnerCheckBox, 113×27×4f at (7,213), (120,213), (233,213)
 *   MAJAK3_TAB01=mj_sen_tab01, TAB02=mj_sen_tab02, TAB03=mj_sen_tab04
 * OKボタン: mj_sen_btn_ok.png 85×29×4f at (134,555)
 *
 * データフィールド (CMJPlayerInfo / CHgPlayerInfo):
 *   m_nTrickTitle, m_nMajakTitle: タイトル
 *   m_nRichiEffect: リーチ演出
 *   m_llLentMoney: コイン貸付
 *   レガシーのプロトコルキー: mjkk41e/46e/47e/54e/136e/137e
 */
import { useEffect, useState } from 'react'
import * as SignalR from '../../../api/signalr'
import { getAvatarUrl, getDefaultAvatarUrl } from '../../../utils/resources'

const IMG = '/assets/images/game'
const DETAIL_RECORD_URL = 'http://redirect.hangame.co.jp/majak2/collection/dummy/'
const DIALOG_W = 353
const DIALOG_H = 595

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
  gradePoint?: number
  gradeMaxPoint?: number
  trickTitle: number
  majakTitle: number
}

/** タブ定義 — レガシー一般ユーザー情報は3タブのみ */
const TABS = [
  { img: 'mj_sen_tab01.png', label: '全体戦績', x: 7 },
  { img: 'mj_sen_tab02.png', label: '交流広場', x: 120 },
  { img: 'mj_sen_tab04.png', label: '段位戦',   x: 233 },
]

const FONT = "'MS PGothic', 'MS UI Gothic', 'Meiryo', sans-serif"

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
function SpriteButton({
  src, frameW, frameH, x, y, onClick, title,
}: {
  src: string; frameW: number; frameH: number
  x: number; y: number; onClick: () => void; title?: string
}) {
  const [fi, setFi] = useState(0)
  return (
    <button
      title={title}
      onClick={onClick}
      onMouseEnter={() => setFi(2)}
      onMouseLeave={() => setFi(0)}
      onMouseDown={() => setFi(3)}
      onMouseUp={() => setFi(2)}
      style={{
        position: 'absolute', left: x, top: y,
        width: frameW, height: frameH,
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-fi * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        border: 'none', padding: 0, cursor: 'pointer',
        outline: 'none', imageRendering: 'pixelated',
      }}
    />
  )
}

/** COwnerCheckBox bitmap tab: active = SetCheck(TRUE)+EnableWindow(FALSE) → frame3 */
function TabButton({
  src, x, active, onClick, title,
}: {
  src: string; x: number; active: boolean; onClick: () => void; title: string
}) {
  const [hover, setHover] = useState(false)
  const frame = active ? 3 : hover ? 2 : 0
  return (
    <button
      title={title}
      aria-disabled={active}
      onClick={active ? undefined : onClick}
      onMouseEnter={() => !active && setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        position: 'absolute', left: x, top: 213,
        width: 113, height: 27,
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-frame * 113}px 0`,
        backgroundRepeat: 'no-repeat',
        border: 'none', padding: 0,
        cursor: active ? 'default' : 'pointer',
        outline: 'none', imageRendering: 'pixelated',
      }}
    />
  )
}

function StatLine({ label, value, x, y, w = 140 }: { label: string; value: string; x: number; y: number; w?: number }) {
  return (
    <div style={{ position: 'absolute', left: x, top: y, width: w, height: 12, display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 4, fontFamily: FONT, fontSize: 12, lineHeight: '12px', color: '#000' }}>
      <span style={{ flex: '0 1 auto', minWidth: 0, overflow: 'hidden', whiteSpace: 'nowrap' }}>{label}</span>
      <span style={{ flex: '0 0 auto', whiteSpace: 'nowrap', textAlign: 'right' }}>{value}</span>
    </div>
  )
}

/** ====================================================================
 * CMJPlayerInfo 本体
 * ==================================================================== */
export default function PlayerInfoWnd({ player, onClose, onTabChange }: Props) {
  const [activeTab, setActiveTab] = useState(0)
  const [detail, setDetail] = useState<DetailInfo | null>(null)
  const [dialogScale, setDialogScale] = useState(1)

  useEffect(() => {
    const updateScale = () => {
      const margin = 16
      setDialogScale(Math.min(1, (window.innerWidth - margin) / DIALOG_W, (window.innerHeight - margin) / DIALOG_H))
    }
    updateScale()
    window.addEventListener('resize', updateScale)
    return () => window.removeEventListener('resize', updateScale)
  }, [])

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

  const handleDetailRecord = () => {
    window.open(DETAIL_RECORD_URL, '_blank', 'noopener,noreferrer')
  }

  const fallbackRecord = emptyRecordFromPlayer(player)
  const tabRecord = detail
    ? activeTab === 0
      ? mergeRecord(detail.regular, detail.hiClass)
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

  return (
    /* モーダルオーバーレイ */
    <div
      style={{
        position: dialogScale < 1 ? 'fixed' : 'absolute', inset: 0,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        background: 'rgba(0,0,0,0.5)', zIndex: 150,
      }}
    >
      <div style={{ width: DIALOG_W * dialogScale, height: DIALOG_H * dialogScale }}>
      {/* CMJPlayerInfo ウィンドウ: 353×595px */}
      <div style={{ position: 'relative', width: DIALOG_W, height: DIALOG_H, transform: `scale(${dialogScale})`, transformOrigin: 'top left' }}>

        {/* ── 背景 mj_sen_win_base.png (353×595) ── */}
        <img
          src={`${IMG}/mj_sen_win_base.png`}
          alt=""
          draggable={false}
          style={{ position: 'absolute', left: 0, top: 0, width: 353, height: 595 }}
        />

        {/* アバター: レガシー OnPaint pt_avatar(16,49) */}
        <div
          style={{
            position: 'absolute', left: 16, top: 49,
            width: 66, height: 150,
            background: '#fff',
            overflow: 'hidden',
          }}
        >
          <img
            src={getAvatarUrl(avatarId ?? null)}
            alt={player.name}
            style={{ width: '100%', height: '100%', objectFit: 'cover' }}
            onError={e => {
              (e.currentTarget as HTMLImageElement).src = getDefaultAvatarUrl(avatarSex)
            }}
          />
        </div>

        {trickTitle > 0 && (
          <img
            src={`${IMG}/mj_skill_${padTitleId(trickTitle)}.png`}
            alt=""
            draggable={false}
            style={{ position: 'absolute', left: 85, top: 79, width: 100, height: 122, imageRendering: 'pixelated' }}
          />
        )}

        {majakTitle > 0 && (
          <>
            <img
              src={`${IMG}/mj_title_base.png`}
              alt=""
              draggable={false}
              style={{ position: 'absolute', left: 85, top: 47, width: 100, height: 52, imageRendering: 'pixelated' }}
            />
            <img
              src={`${IMG}/${titleImageName(majakTitle)}`}
              alt=""
              draggable={false}
              style={{ position: 'absolute', left: 110, top: 54, width: 50, height: 38, imageRendering: 'pixelated' }}
            />
          </>
        )}

        {/* 基本情報: レガシー OnPaint pt_member/pt_gender/pt_region/pt_rating */}
        <div style={{ position: 'absolute', left: 193, top: 53, width: 144, height: 12, fontFamily: FONT, fontSize: 12, fontWeight: 'bold', color: 'rgb(0,114,188)', textAlign: 'center' }}>
          {detail?.name ?? player.name}
        </div>
        <div style={{ position: 'absolute', left: 193, top: 74, fontFamily: FONT, fontSize: 12, color: '#000' }}>性別</div>
        <div style={{ position: 'absolute', left: 240, top: 74, fontFamily: FONT, fontSize: 12, color: '#000' }}>{` : ${sexText}`}</div>
        <div style={{ position: 'absolute', left: 193, top: 90, fontFamily: FONT, fontSize: 12, color: '#000' }}>地域</div>
        <div style={{ position: 'absolute', left: 240, top: 90, fontFamily: FONT, fontSize: 12, color: '#000' }}>{` : ${player.location ?? '-'}`}</div>
        <div style={{ position: 'absolute', left: 193, top: 106, fontFamily: FONT, fontSize: 12, color: '#000' }}>称号</div>
        <div style={{ position: 'absolute', left: 240, top: 106, fontFamily: FONT, fontSize: 12, color: '#000' }}>{` : ${player.slevel ?? '-'}`}</div>
        <div style={{ position: 'absolute', left: 193, top: 122, fontFamily: FONT, fontSize: 12, color: '#000' }}>戦績</div>
        <div style={{ position: 'absolute', left: 240, top: 122, fontFamily: FONT, fontSize: 12, color: '#000' }}>{` : R${player.rating ?? '-'}`}</div>

        {TABS.map((tab, i) => (
          <TabButton
            key={tab.img}
            src={`${IMG}/${tab.img}`}
            x={tab.x}
            active={activeTab === i}
            title={tab.label}
            onClick={() => handleTabChange(i)}
          />
        ))}

        {/* 戦績概要: CRect(15,251,337,265) DT_CENTER */}
        <div style={{ position: 'absolute', left: 15, top: 251, width: 322, height: 14, fontFamily: FONT, fontSize: 12, lineHeight: '14px', color: '#000', textAlign: 'center' }}>
          {`戦績 : ${matchCnt}戦 ${tabRecord.winCnt}勝 ${tabRecord.defeatCnt}敗 ${tabRecord.drawCnt}分`}
        </div>

        <StatLine label="平均順位" value={averageRank} x={21} y={276} />
        <StatLine label="平均収支" value={averageSet} x={21} y={296} />
        <StatLine label="1位" value={percent(tabRecord.grade1, rankCnt)} x={21} y={336} />
        <StatLine label="2位" value={percent(tabRecord.grade2, rankCnt)} x={21} y={356} />
        <StatLine label="3位" value={percent(tabRecord.grade3, rankCnt)} x={21} y={376} />
        <StatLine label="4位" value={percent(tabRecord.grade4, rankCnt)} x={21} y={396} />
        <StatLine label="飛び率" value={percent(tabRecord.tobiCnt, rankCnt)} x={21} y={436} />
        <StatLine label="飛ばし率" value={percent(tabRecord.tobashiCnt, rankCnt)} x={21} y={456} />
        {activeTab !== 2 && (
          <StatLine label="平均チップ収支" value={tabRecord.tipMatchCnt > 0 ? `${(tabRecord.tipPoint / tabRecord.tipMatchCnt).toFixed(2)}` : '---.--'} x={21} y={476} />
        )}

        <StatLine label="和了率" value={percent(tabRecord.horaCnt, tabRecord.kyokuCnt)} x={185} y={276} />
        <StatLine label="放銃率" value={percent(tabRecord.hojuCnt, tabRecord.kyokuCnt)} x={185} y={296} />
        <StatLine label="平均和了点" value={tabRecord.horaCnt > 0 ? `${Math.trunc(tabRecord.horaPoint / tabRecord.horaCnt)}点` : '---点'} x={185} y={316} />
        <StatLine label="平均放銃点" value={tabRecord.hojuCnt > 0 ? `${Math.trunc(tabRecord.hojuPoint / tabRecord.hojuCnt)}点` : '---点'} x={185} y={336} />
        <StatLine label="立直率" value={percent(tabRecord.richiCnt, tabRecord.kyokuCnt)} x={185} y={376} />
        <StatLine label="副露率" value={percent(tabRecord.furoCnt, tabRecord.kyokuCnt)} x={185} y={396} />
        <StatLine label="平均ドラ枚数" value={tabRecord.horaCnt > 0 ? `${(tabRecord.doraCnt / tabRecord.horaCnt).toFixed(2)}枚` : '--.--枚'} x={185} y={436} />
        <StatLine label="平均裏ドラ枚数" value={tabRecord.richiHoraCnt > 0 ? `${(tabRecord.uraDoraCnt / tabRecord.richiHoraCnt).toFixed(2)}枚` : '--.--枚'} x={185} y={456} />

        <div style={{ position: 'absolute', left: 20, top: 522, width: 314, height: 23, fontFamily: FONT, fontSize: 12, lineHeight: '12px', color: '#000', whiteSpace: 'pre-line' }}>
          {activeTab === 0
            ? '段位戦・練習広場以外の全ての戦績です。\n最近の一般広場の戦績を含みます。'
            : activeTab === 1
              ? '交流広場の戦績です。\n最近の対戦の戦績を含みます。'
              : '段位戦だけの戦績です。'}
        </div>

        {/* 詳細戦績ボタン: legacy m_btnDatailRec at (79,533) */}
        <SpriteButton
          src={`${IMG}/mj_sen_btn_deteilrecord.png`}
          frameW={31} frameH={15}
          x={79} y={533}
          onClick={handleDetailRecord}
          title="詳細戦績"
        />

        {/* OKボタン: レガシー at (134,555) */}
        <SpriteButton
          src={`${IMG}/mj_sen_btn_ok.png`}
          frameW={85} frameH={29}
          x={134} y={555}
          onClick={onClose}
          title="閉じる"
        />
      </div>
      </div>
    </div>
  )
}
