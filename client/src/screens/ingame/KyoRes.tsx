/**
 * CMJKyoRes 相当 — 1局終了結果 (AP-09 §2-9)
 * Legacy: client/legacy/client/HgMajak2/MJKyoRes.h/cpp
 *
 * Main references:
 *   MJKyoRes.cpp:245-339  board/title/player button creation
 *   MJKyoRes.cpp:341-537  four-direction player balance placement
 *   MJKyoRes.cpp:555-741  selected winner yaku/total/dora display
 */
import { useEffect, useMemo, useRef, useState, type CSSProperties } from 'react'
import { getAvatarUrl, getDefaultAvatarUrl, getShortAvatarUrl } from '../../utils/resources'
import { ResponsiveKyoResult } from './ResponsiveResultOverlay'

const IMG = '/assets/images/game'
const RYUKYOKU_AVATAR_W = 52
const RYUKYOKU_AVATAR_H = 78
const RYUKYOKU_AVATAR_OFFSET_X = 7
const RYUKYOKU_AVATAR_OFFSET_Y = 10

export interface KyoYaku {
  name: string
  fan: number
  isFu?: boolean
  code?: number
  isYakuman?: boolean
  tip?: number
}

export interface KyoPlayer {
  pix: string
  name: string
  avatarId?: string
  seatPos: 0 | 1 | 2 | 3
  isOya: boolean
  tenBal: number
  tenBaseBal?: number
  paoBal?: number
  warBal?: number
  ribBal?: number
  renBal?: number
  tipBal?: number
  isHora?: boolean
  isHoju?: boolean
  isNagashiMangan?: boolean
  isTempai?: boolean
  isRichi?: boolean
}

export interface KyoResData {
  pinType: 0 | 1 | number
  players: KyoPlayer[]
  yaku?: KyoYaku[]
  yakuByPlayer?: Record<number, KyoYaku[]>
  totalsByPlayer?: Record<number, { totalFu?: number; totalFan?: number; totalTen?: number; tipBal?: number }>
  totalFu?: number
  totalFan?: number
  totalTen?: number
  tipBal?: number
  kyoNum?: number
  ribCnt?: number
  renCnt?: number
  dora?: number[]
  uraDora?: number[]
  selectedOdr?: number
  contest?: number
  waremeOdr?: number
  isDaniChannel?: boolean
}

interface Props {
  data: KyoResData
  myOdr?: number
  canContinue?: boolean
  onClose: () => void
}

const PIN_RON = 0
const PIN_TSU = 1
const PIN_NON = 2
const PIN_HOA = 5
const PIN_NAG = 9
const WND_W = 789
const WND_H = 704
const BOARD_X = 5
const BOARD_Y = 31

const btnPos = [
  { x: 303, y: 441, w: 291, h: 126, img: 'mj_resBtChgList_0My.png' },
  { x: 594, y: 323, w: 132, h: 244, img: 'mj_resBtChgList_1Next.png' },
  { x: 435, y: 197, w: 291, h: 126, img: 'mj_resBtChgList_2Opposite.png' },
  { x: 303, y: 197, w: 132, h: 244, img: 'mj_resBtChgList_3Before.png' },
] as const

const ptFon = [{ x: 499, y: 405 }, { x: 556, y: 365 }, { x: 499, y: 327 }, { x: 442, y: 365 }]
const ptAvt = [{ x: 313, y: 512 }, { x: 696, y: 512 }, { x: 696, y: 222 }, { x: 313, y: 222 }]
const ptNam = [{ x: 312, y: 548, w: 72 }, { x: 643, y: 548, w: 72 }, { x: 643, y: 206, w: 72 }, { x: 312, y: 206, w: 72 }]
const ptOya = [{ x: 340, y: 518 }, { x: 667, y: 518 }, { x: 665, y: 233 }, { x: 340, y: 224 }]
const ptTip = [{ x: 368, y: 518 }, { x: 639, y: 518 }, { x: 639, y: 226 }, { x: 368, y: 224 }]
const ptStatus = [{ x: 321, y: 450 }, { x: 600, y: 450 }, { x: 588, y: 263 }, { x: 304, y: 263 }]
const ptWarMrk = [{ x: 338, y: 488 }, { x: 620, y: 488 }, { x: 608, y: 254 }, { x: 329, y: 255 }]
const ptTen = [{ x: 574, y: 454 }, { x: 706, y: 336 }, { x: 574, y: 210 }, { x: 415, y: 328 }]
const ptWar = [{ x: 574, y: 473 }, { x: 706, y: 355 }, { x: 574, y: 229 }, { x: 415, y: 347 }]
const ptRib = [{ x: 574, y: 492 }, { x: 706, y: 374 }, { x: 574, y: 248 }, { x: 415, y: 366 }]
const ptRen = [{ x: 574, y: 511 }, { x: 706, y: 393 }, { x: 574, y: 267 }, { x: 415, y: 385 }]
const ptSum = [{ x: 569, y: 533 }, { x: 701, y: 415 }, { x: 569, y: 289 }, { x: 410, y: 407 }]

const ptRyuBall = [{ x: 39, y: 442 }, { x: 507, y: 442 }, { x: 507, y: 75 }, { x: 39, y: 75 }]
const ptRyuCall = [{ x: 157, y: 486 }, { x: 607, y: 486 }, { x: 607, y: 146 }, { x: 157, y: 146 }]
const ptRyuBal = [{ x: 243, y: 534 }, { x: 693, y: 534 }, { x: 693, y: 194 }, { x: 243, y: 194 }]
const ptRyuAvt = [{ x: 70, y: 459 }, { x: 520, y: 459 }, { x: 520, y: 119 }, { x: 70, y: 119 }]

function frameStyle(src: string, w: number, h: number, frame: number): CSSProperties {
  return {
    width: w,
    height: h,
    backgroundImage: `url(${IMG}/${src})`,
    backgroundPosition: `${-w * frame}px 0`,
    backgroundRepeat: 'no-repeat',
    imageRendering: 'pixelated',
  }
}

function Sprite({ src, x, y, w, h, frame = 0, z = 1 }: { src: string; x: number; y: number; w: number; h: number; frame?: number; z?: number }) {
  return <div style={{ position: 'absolute', left: x, top: y, zIndex: z, ...frameStyle(src, w, h, frame) }} />
}

function KyoResultButton({ btn, selected, enabled, onClick, title }: {
  btn: typeof btnPos[number]
  selected: boolean
  enabled: boolean
  onClick: () => void
  title: string
}) {
  const [hover, setHover] = useState(false)
  const [active, setActive] = useState(false)
  const frame = !enabled
    ? 1
    : active
      ? 3
      : selected
        ? (hover ? 4 : 5)
        : (hover ? 2 : 0)

  return (
    <button
      type="button"
      onClick={enabled ? onClick : undefined}
      disabled={!enabled}
      onMouseEnter={() => enabled && setHover(true)}
      onMouseLeave={() => { setHover(false); setActive(false) }}
      onMouseDown={() => enabled && setActive(true)}
      onMouseUp={() => enabled && setActive(false)}
      style={{
        position: 'absolute', left: btn.x, top: btn.y, zIndex: 10,
        border: 'none', padding: 0, backgroundColor: 'transparent',
        cursor: enabled ? 'pointer' : 'default',
        ...frameStyle(btn.img, btn.w, btn.h, frame),
      }}
      title={title}
    />
  )
}

function KyoCommandButton({ src, x, y, disabled = false, checked = false, onClick }: { src: string; x: number; y: number; disabled?: boolean; checked?: boolean; onClick: () => void }) {
  const [hover, setHover] = useState(false)
  const [active, setActive] = useState(false)
  const frame = disabled ? 1 : active ? 3 : checked ? (hover ? 4 : 5) : hover ? 2 : 0
  return (
    <button
      type="button"
      onClick={disabled ? undefined : onClick}
      disabled={disabled}
      onMouseEnter={() => !disabled && setHover(true)}
      onMouseLeave={() => { setHover(false); setActive(false) }}
      onMouseDown={() => !disabled && setActive(true)}
      onMouseUp={() => setActive(false)}
      style={{
        position: 'absolute', left: x, top: y, zIndex: 60,
        border: 'none', padding: 0, backgroundColor: 'transparent', cursor: disabled ? 'default' : 'pointer',
        ...frameStyle(src, 116, 40, frame),
      }}
    />
  )
}

function SpriteNumber({ value, x, y, kind = 'balance', sign = true, z = 30 }: {
  value: number; x: number; y: number; kind?: 'balance' | 'small' | 'total' | 'chip'; sign?: boolean; z?: number
}) {
  const cfg = kind === 'total'
    ? { src: 'mj_ptResult_num_total.png', w: 28, h: 28, max: 8 }
    : kind === 'small'
      ? { src: 'mj_ptResult_num_yaku.png', w: 20, h: 21, max: 4 }
      : kind === 'chip'
        ? { src: 'mj_resChipNum.png', w: 10, h: 14, max: 3 }
        : { src: value < 0 ? 'mj_ptResult_num_mns.png' : 'mj_ptResult_num_pls.png', w: 14, h: 25, max: 7 }
  const digits = String(Math.abs(value)).slice(-cfg.max)
  if (!digits) return null
  return (
    <div style={{ position: 'absolute', left: x, top: y, display: 'flex', zIndex: z }}>
      {digits.split('').map((d, i) => <span key={`${d}-${i}`} style={frameStyle(cfg.src, cfg.w, cfg.h, Number(d))} />)}
      {sign && value === 0 && <span style={{ width: cfg.w, height: cfg.h }} />}
    </div>
  )
}

function Tip({ value, x, y }: { value?: number; x: number; y: number }) {
  if (!value) return null
  return (
    <>
      <img src={`${IMG}/mj_ptResult_chip.png`} alt="" draggable={false} style={{ position: 'absolute', left: x, top: y, zIndex: 30 }} />
      <SpriteNumber value={value} x={x + 14} y={y + 7} kind="chip" />
    </>
  )
}

function yakuImageCode(yaku: KyoYaku): number | null {
  if (typeof yaku.code === 'number') return yaku.code
  const m = yaku.name.match(/(\d{1,2})$/)
  return m ? Number(m[1]) : null
}

function paiToFrame(code: number): number {
  const kind = (code >> 4) & 0x0f
  const num = code & 0x0f
  if (kind === 0) return Math.max(0, num - 1)
  if (kind === 1) return 9 + Math.max(0, num - 1)
  if (kind === 2) return 18 + Math.max(0, num - 1)
  return 27 + Math.max(0, num - 1)
}

function odrToLoc(odr: number, viewOdr: number): 0 | 1 | 2 | 3 {
  return ((4 + odr - viewOdr) % 4) as 0 | 1 | 2 | 3
}

function HoraPlayerPanel({ player, selected, onSelect, pinType, isWareme, displayLoc }: {
  player: KyoPlayer; selected: boolean; onSelect: () => void; pinType: number; isWareme: boolean; displayLoc: 0 | 1 | 2 | 3
}) {
  const loc = displayLoc
  const btn = btnPos[loc]
  const statusFrame = player.isHora ? (pinType === PIN_TSU ? 0 : 1) : player.isHoju ? 2 : -1
  const ten = (player.tenBaseBal ?? player.tenBal) + (player.paoBal ?? 0)
  const sum = ten + (player.warBal ?? 0) + (player.ribBal ?? 0) + (player.renBal ?? 0)
  const enabled = statusFrame === 0 || statusFrame === 1
  return (
    <>
      <KyoResultButton btn={btn} selected={selected} enabled={enabled} onClick={onSelect} title={player.name || player.pix} />
      <Sprite src="mj_myfan_0.png" x={ptFon[loc].x} y={ptFon[loc].y} w={25} h={25} frame={4 + loc} z={31} />
      {isWareme && <img src={`${IMG}/mj_wareme00.png`} alt="" draggable={false} style={{ position: 'absolute', left: ptWarMrk[loc].x, top: ptWarMrk[loc].y, zIndex: 31, imageRendering: 'pixelated' }} />}
      <img src={getShortAvatarUrl(player.avatarId ?? null)} alt="" draggable={false} style={{ position: 'absolute', left: ptAvt[loc].x, top: ptAvt[loc].y, width: 22, height: 32, objectFit: 'cover', imageRendering: 'auto', zIndex: 31 }} onError={e => { e.currentTarget.src = getDefaultAvatarUrl('male') }} />
      <div style={{ position: 'absolute', left: ptNam[loc].x, top: ptNam[loc].y, width: 60, height: 12, overflow: 'hidden', whiteSpace: 'nowrap', fontFamily: 'var(--majak-font-family-ui)', fontSize: 'calc(12px * var(--majak-type-scale))', color: '#000', zIndex: 32 }}>{player.name}</div>
      <Sprite src="mj_ptResult_oyako.png" x={ptOya[loc].x} y={ptOya[loc].y} w={24} h={24} frame={player.isOya ? 0 : 1} z={32} />
      <Tip value={player.tipBal} x={ptTip[loc].x} y={ptTip[loc].y} />
      {statusFrame >= 0 && <Sprite src="mj_ptResult_status.png" x={ptStatus[loc].x} y={ptStatus[loc].y} w={122} h={48} frame={statusFrame} z={33} />}
      {ten !== 0 && <SpriteNumber value={ten} x={ptTen[loc].x} y={ptTen[loc].y} />}
      {!!player.warBal && <SpriteNumber value={player.warBal} x={ptWar[loc].x} y={ptWar[loc].y} />}
      {!!player.ribBal && <SpriteNumber value={player.ribBal} x={ptRib[loc].x} y={ptRib[loc].y} />}
      {!!player.renBal && <SpriteNumber value={player.renBal} x={ptRen[loc].x} y={ptRen[loc].y} />}
      {sum !== 0 && <SpriteNumber value={sum} x={ptSum[loc].x} y={ptSum[loc].y} />}
    </>
  )
}

function KyoTitle({ data }: { data: KyoResData }) {
  const fon = data.kyoNum !== undefined ? Math.floor(data.kyoNum / 4) : 0
  const kyo = data.kyoNum !== undefined ? data.kyoNum % 4 : 0
  return (
    <>
      <Sprite src="mj_ptResult_ttlkaze.png" x={329} y={80} w={49} h={43} frame={fon} />
      <Sprite src="mj_ptResult_ttlkyoku.png" x={372} y={80} w={87} h={43} frame={kyo} />
      <SpriteNumber value={data.ribCnt ?? 0} x={532} y={361} kind="small" sign={false} />
      <SpriteNumber value={data.renCnt ?? 0} x={532} y={384} kind="small" sign={false} />
    </>
  )
}

function YakuPane({ data, selectedOdr }: { data: KyoResData; selectedOdr: number }) {
  const list = data.yakuByPlayer?.[selectedOdr] ?? data.yaku ?? []
  const totals = data.totalsByPlayer?.[selectedOdr]
  const yakuman = list.some(y => y.isYakuman)
  const showUraDora = data.contest !== 1 && Boolean(data.players[selectedOdr]?.isRichi)
  return (
    <>
      {list.map((yaku, idx) => {
        const code = yakuImageCode(yaku)
        const top = 140 + idx * (yakuman ? 60 : 28)
        return (
          <div key={`${yaku.name}-${idx}`} style={{ position: 'absolute', left: 76, top, zIndex: 40 }}>
            {code != null ? (
              <img src={`${IMG}/${yaku.isYakuman ? 'mj_yakuman' : 'mj_yaku'}_${String(code).padStart(2, '0')}.png`} alt={yaku.name} draggable={false} style={{ imageRendering: 'pixelated' }} />
            ) : (
              <span style={{ fontFamily: 'var(--majak-font-family-ui)', fontSize: 'calc(12px * var(--majak-type-scale))', color: '#111', whiteSpace: 'nowrap' }}>{yaku.name}</span>
            )}
            {yaku.isYakuman ? (
              !data.isDaniChannel && <Sprite src="mj_fntYakuman.png" x={124} y={32} w={69} h={23} frame={Math.max(0, yaku.fan - 1)} z={41} />
            ) : (
              <>
                <SpriteNumber value={yaku.fan} x={150} y={1} kind="small" sign={false} />
                <Sprite src="mj_ptResult_huhan.png" x={172} y={1} w={22} h={21} frame={1} z={41} />
              </>
            )}
            {!!yaku.tip && <Tip value={yaku.tip} x={194} y={yaku.isYakuman ? 32 : -1} />}
          </div>
        )
      })}
      {!yakuman && (totals?.totalFu ?? data.totalFu) !== undefined && <SpriteNumber value={totals?.totalFu ?? data.totalFu ?? 0} x={127} y={456} kind="small" sign={false} />}
      {!yakuman && (totals?.totalFu ?? data.totalFu) !== undefined && <Sprite src="mj_ptResult_huhan.png" x={149} y={456} w={22} h={21} frame={0} />}
      {!yakuman && (totals?.totalFan ?? data.totalFan) !== undefined && <SpriteNumber value={totals?.totalFan ?? data.totalFan ?? 0} x={225} y={456} kind="small" sign={false} />}
      {!yakuman && (totals?.totalFan ?? data.totalFan) !== undefined && <Sprite src="mj_ptResult_huhan.png" x={247} y={456} w={22} h={21} frame={1} />}
      {yakuman && <Sprite src="mj_ptResult_mangan.png" x={69} y={491} w={92} h={24} frame={4 + (totals?.totalFan ?? data.totalFan ?? 0)} />}
      <Sprite src="mj_ptResult_oyako.png" x={69} y={455} w={24} h={24} frame={data.players[selectedOdr]?.isOya ? 0 : 1} />
      <Tip value={totals?.tipBal ?? data.tipBal} x={270} y={454} />
      <Sprite src="mj_ptResult_status_s.png" x={190} y={491} w={91} h={24} frame={data.pinType === PIN_RON ? 1 : 0} />
      {(totals?.totalTen ?? data.totalTen) !== undefined && <SpriteNumber value={totals?.totalTen ?? data.totalTen ?? 0} x={213} y={536} kind="total" sign={false} />}
      <Tip value={data.players[selectedOdr]?.tipBal ?? totals?.tipBal ?? data.tipBal} x={270} y={539} />
      {data.dora?.map((code, i) => <Sprite key={`d-${i}`} src="mj_hai_resultdora.png" x={357 + 30 * i} y={142} w={26} h={44} frame={paiToFrame(code)} />)}
      {showUraDora && data.uraDora?.map((code, i) => <Sprite key={`u-${i}`} src="mj_hai_resultdora.png" x={541 + 30 * i} y={142} w={26} h={44} frame={paiToFrame(code)} />)}
    </>
  )
}

function RyukyokuView({ data, myOdr }: { data: KyoResData; myOdr: number }) {
  const fon = data.kyoNum !== undefined ? Math.floor(data.kyoNum / 4) : 0
  const kyo = data.kyoNum !== undefined ? data.kyoNum % 4 : 0
  const showPlayerResults = data.pinType === PIN_HOA || data.pinType === PIN_NAG
  return (
    <>
      <img src={`${IMG}/mj_ryukyoku.png`} alt="" draggable={false} style={{ position: 'absolute', left: 249, top: 235, imageRendering: 'pixelated' }} />
      {data.pinType !== PIN_NAG && <Sprite src="mj_ryukyokuKind.png" x={307} y={395} w={175} h={29} frame={Math.max(0, data.pinType - PIN_NON - 1)} />}
      <Sprite src="mj_ryukyoku_kaze.png" x={346} y={242} w={34} h={33} frame={fon} />
      <Sprite src="mj_ryukyoku_kyokuNum.png" x={378} y={242} w={64} h={33} frame={kyo} />
      {showPlayerResults && data.players.map((p, odr) => {
        const loc = odrToLoc(p.seatPos, myOdr)
        const call = data.pinType === PIN_NAG ? (p.isNagashiMangan ? 2 : -1) : p.isTempai ? 0 : 1
        return (
          <div key={p.pix || odr}>
            <Sprite src="mj_ryukyoku_hukidasi.png" x={ptRyuBall[loc].x} y={ptRyuBall[loc].y} w={243} h={158} frame={loc} />
            <img src={getAvatarUrl(p.avatarId ?? null)} alt="" draggable={false} style={{ position: 'absolute', left: ptRyuAvt[loc].x + RYUKYOKU_AVATAR_OFFSET_X, top: ptRyuAvt[loc].y + RYUKYOKU_AVATAR_OFFSET_Y, width: RYUKYOKU_AVATAR_W, height: RYUKYOKU_AVATAR_H, objectFit: 'contain', imageRendering: 'auto', zIndex: 25 }} onError={e => { e.currentTarget.src = getDefaultAvatarUrl('male') }} />
            {call >= 0 && <Sprite src="mj_ryukyoku_hukidasiIn.png" x={ptRyuCall[loc].x} y={ptRyuCall[loc].y} w={104} h={29} frame={call} />}
            <SpriteNumber value={p.tenBal} x={ptRyuBal[loc].x} y={ptRyuBal[loc].y} />
          </div>
        )
      })}
    </>
  )
}

export function LegacyKyoRes({ data, myOdr = 0, canContinue = true, onClose }: Props) {
  const rootRef = useRef<HTMLDivElement>(null)
  const winnerIndexes = useMemo(() => data.players.map((p, i) => p.isHora ? i : -1).filter(i => i >= 0), [data.players])
  const initialSelectedOdr = data.selectedOdr !== undefined && winnerIndexes.includes(data.selectedOdr)
    ? data.selectedOdr
    : winnerIndexes[0] ?? 0
  const [selectedOdr, setSelectedOdr] = useState(initialSelectedOdr)
  const [unseenWinnerIndexes, setUnseenWinnerIndexes] = useState(() => winnerIndexes.filter(i => i !== initialSelectedOdr))
  const [resultVisible, setResultVisible] = useState(true)
  const isHora = data.pinType === PIN_RON || data.pinType === PIN_TSU
  const selectWinner = (odr: number) => {
    setSelectedOdr(odr)
    setUnseenWinnerIndexes(indexes => indexes.filter(i => i !== odr))
  }
  const continueKyoResult = () => {
    if (!isHora || unseenWinnerIndexes.length === 0) {
      onClose()
      return
    }
    for (let offset = 1; offset <= 4; offset++) {
      const next = (selectedOdr + offset) % 4
      if (unseenWinnerIndexes.includes(next)) {
        selectWinner(next)
        return
      }
    }
    onClose()
  }
  useEffect(() => {
    rootRef.current?.focus()
  }, [])
  const handleKeyDown = (event: React.KeyboardEvent<HTMLDivElement>) => {
    if (!canContinue) return
    if (event.key === 'Enter' || event.key === ' ' || event.code === 'Numpad0') {
      event.preventDefault()
      continueKyoResult()
    }
  }

  return (
    <div
      ref={rootRef}
      tabIndex={-1}
      onKeyDown={handleKeyDown}
      style={{ position: 'absolute', inset: 0, zIndex: 350, pointerEvents: 'none' }}
      onContextMenu={e => e.preventDefault()}
    >
      {resultVisible && (
        <div style={{ position: 'absolute', left: BOARD_X, top: BOARD_Y, width: WND_W, height: WND_H, imageRendering: 'pixelated', pointerEvents: 'auto' }}>
          {isHora ? (
            <>
              <img src={`${IMG}/mj_ptResultBoard.png`} alt="" draggable={false} style={{ position: 'absolute', left: 50, top: 72, imageRendering: 'pixelated' }} />
              <KyoTitle data={data} />
              {data.players.map((p, odr) => (
                <HoraPlayerPanel key={p.pix || odr} player={p} selected={odr === selectedOdr} onSelect={() => p.isHora && selectWinner(odr)} pinType={data.pinType} isWareme={data.waremeOdr === p.seatPos} displayLoc={odrToLoc(p.seatPos, myOdr)} />
              ))}
              <YakuPane data={data} selectedOdr={selectedOdr} />
            </>
          ) : <RyukyokuView data={data} myOdr={myOdr} />}
        </div>
      )}
      <div style={{ pointerEvents: 'auto' }}>
        <img src={`${IMG}/mj_resBtBoard.png`} alt="" draggable={false} style={{ position: 'absolute', left: 102, top: 644, width: 580, height: 60, imageRendering: 'pixelated', pointerEvents: 'none' }} />
        <KyoCommandButton src="mj_btLookSutehai.png" x={435} y={647} checked={!resultVisible} onClick={() => setResultVisible(v => !v)} />
        <KyoCommandButton src="mj_btOk.png" x={558} y={647} disabled={!canContinue} onClick={continueKyoResult} />
      </div>
    </div>
  )
}

export default function KyoRes({ data, canContinue = true, onClose }: Props) {
  return <ResponsiveKyoResult data={data} canContinue={canContinue} onClose={onClose} />
}