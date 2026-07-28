/**
 * CMJHanRes 相当 — 半荘/東風 最終結果画面 (AP-09 §2-8)
 * レガシー: legacy/client/HgMajak2/MJHanRes.h/cpp
 *
 * ── 座標定義 (__SETPOS__ マクロ: raw_x-5, raw_y-31) ───────────────────
 *   mj_endResultBoard.png       (背景ボード)       at (174, 75)
 *   mj_endResultBoard_yen.png   (点数詳細背景)      at (174, 297)
 *   mj_endResultBoard_blind.png (観戦者ブラインド) at (174, 465)
 *
 *   アバター (mj_aiAvtrL 64×64 half)    x = 215 + mem*108,  y = 138
 *   メンバーID テキスト (w=105, h=14)   x = 185 + mem*108,  y = 206
 *   順位 mj_ranking_L.png (4フレーム)   x = 248 + mem*108,  y = 230
 *   合計点 mj_ptResult_num_pls.png      x = 273 + mem*108,  y = 261
 *
 *   点数詳細エリア (mj_endResultBoard_yen 内):
 *     バランス (setBal) at (277+mem*108, 310)
 *     点数    (setTen) at (277+mem*108, 344)
 *     ウマ   (setUma)  at (277+mem*108, 374)
 *     飛び   (setTor)  at (277+mem*108, 404)  ← rule.nTor > 0 の場合
 *     チップ (setTip)  at (277+mem*108, 434)  ← rule.bTip の場合
 *
 *   コイン獲得 mj_endResult_ptYen.png   at (558, 502)
 *   次LVまで  mj_endResultNum_w.png     at (575, 545)
 *   レベル表示エリア                    at (190, 476)
 *   W_COLUMN = 108,  MAX_PLAYER = 4
 * ────────────────────────────────────────────────────────────────────
 */
import { useEffect, useState, type CSSProperties } from 'react'
import { getAvatarUrl, getDefaultAvatarUrl } from '../../utils/resources'

const IMG = '/assets/images/game'
const CUSTOM_DEFAULT_ID_COSTUME = 100011
const SCREEN_W = 800
const SCREEN_H = 600
const W_COLUMN = 108

const X_BOARD = 174
const Y_BOARD = 75
const X_DETAIL = 174
const Y_DETAIL = 297
const RESULT_BOARD_W = 451
const RESULT_DETAIL_H = 277
const OK_FRAME_W = 116
const OK_FRAME_H = 40
const X_RESULT_OK = X_BOARD + Math.round((RESULT_BOARD_W - OK_FRAME_W) / 2)
const Y_RESULT_OK = Y_DETAIL + RESULT_DETAIL_H + 10
const RESULT_STAGE_H = Y_RESULT_OK + OK_FRAME_H + 12
const X_BLIND = 174
const Y_BLIND = 465
const X_AVATAR = 215
const Y_AVATAR = 138
const AVATAR_RESULT_SIZE = 52
const AVATAR_RESULT_OFFSET = 6
const X_PIX = 185
const Y_PIX = 206
const X_SETPOINT = 273
const Y_SETPOINT = 261
const X_SETRANK = 248
const Y_SETRANK = 230
const X_SETBAL = 277
const Y_SETBAL = 310
const X_SETTEN = 277
const Y_SETTEN = 344
const X_SETUMA = 277
const Y_SETUMA = 374
const X_SETTOR = 277
const Y_SETTOR = 404
const X_SETCHP = 277
const Y_SETCHP = 434
const X_MONGAIN = 558
const Y_MONGAIN = 502
const X_MONNEED = 575
const Y_MONNEED = 545
const X_LEVEL = 190
const Y_LEVEL = 476
const X_LVLDN = 301
const Y_LVLDN = 493
const RESULT_COUNT_DURATION_MS = 1800
const RESULT_PLAYER_STAGGER_MS = 130
const RESULT_DETAIL_DELAY_MS = 320
const RESULT_RANK_DELAY_MS = 1150

/** 1プレイヤー分の結果データ */
export interface HanResPlayer {
  pix:       string
  name:      string
  avatarId?: string
  sex?:      string
  charaId?:  number
  /** 席順 0-3 */
  seatPos:   0 | 1 | 2 | 3
  /** 순위 0=1位 … 3=4位 */
  rank:      0 | 1 | 2 | 3
  /** 合計点 */
  point:     number
  /** 清算バランス */
  setBal:    number
  /** 点数 */
  setTen:    number
  /** ウマ */
  setUma:    number
  /** 飛び (tobi) */
  setTor?:   number
  /** チップ */
  setTip?:   number
  /** コイン獲得額 */
  coinGain?: number
  /** 次レベルまでの必要コイン */
  coinNeed?: number
  prevNlevel?: number
  nlevel?: number
  levelName?: string
  /** 自分自身フラグ */
  isMe?:     boolean
}

export interface HanResProps {
  players:   HanResPlayer[]
  /** ルールフラグ */
  hasTor?:   boolean
  hasTip?:   boolean
  /** 観戦モード — blind 表示 */
  isViewer?: boolean
  isTournament?: boolean
  displayScale?: number
  displayOffsetY?: number
  backdrop?: boolean
  onClose:   () => void
}

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

function getSexFallback(sex: string | undefined): 'male' | 'female' {
  return sex === 'F' || sex === 'female' ? 'female' : 'male'
}

function isDummyPlayer(player: HanResPlayer): boolean {
  return !player.pix || player.pix === '*AI*'
}

function getHanResAvatarUrl(player: HanResPlayer): string {
  if (isDummyPlayer(player)) return `${IMG}/mj_aiAvtrL.png`
  const charaId = player.charaId ?? 0
  if (charaId !== 0 && charaId !== CUSTOM_DEFAULT_ID_COSTUME) {
    const skinId = String(charaId).padStart(2, '0')
    return `${IMG}/skin/${charaId}/mj_costume_mj_aiAvtrL_${skinId}.png`
  }
  return getAvatarUrl(player.avatarId ?? null)
}

function getHanResDisplayName(player: HanResPlayer): string {
  return player.name || player.pix || '<トントン>'
}

function SpriteNumber({ src, frameW, frameH, value, x, y, digits, sign = false, gap = 0 }: {
  src: string
  frameW: number
  frameH: number
  value: number
  x: number
  y: number
  digits: number
  sign?: boolean
  gap?: number
}) {
  const step = gap || frameW
  const frames: number[] = []
  const text = String(Math.trunc(Math.abs(value))).slice(-digits)
  if (value < 0) {
    frames.push(11)
  } else if (sign) {
    frames.push(10)
  }
  frames.push(...text.split('').map(Number))
  const left = x - step * (frames.length - 1)
  return (
    <div style={{ position: 'absolute', left, top: y, display: 'flex', columnGap: Math.max(0, step - frameW), zIndex: 20 }}>
      {frames.map((frame, idx) => <span key={`${frame}-${idx}`} style={frameStyle(src, frameW, frameH, frame)} />)}
    </div>
  )
}

function easeOutCubic(t: number): number {
  return 1 - Math.pow(1 - t, 3)
}

function AnimatedSpriteNumber({ value, delay = 0, duration = RESULT_COUNT_DURATION_MS, ...props }: {
  src: string
  frameW: number
  frameH: number
  value: number
  x: number
  y: number
  digits: number
  sign?: boolean
  gap?: number
  delay?: number
  duration?: number
}) {
  const [displayValue, setDisplayValue] = useState(0)

  useEffect(() => {
    const reduceMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
    if (reduceMotion || duration <= 0) {
      setDisplayValue(value)
      return
    }

    let frameId = 0
    let timeoutId = 0
    setDisplayValue(0)

    timeoutId = window.setTimeout(() => {
      const startTime = performance.now()
      const tick = (now: number) => {
        const progress = Math.min(1, (now - startTime) / duration)
        const nextValue = Math.round(value * easeOutCubic(progress))
        setDisplayValue(progress >= 1 ? value : nextValue)
        if (progress < 1) frameId = window.requestAnimationFrame(tick)
      }
      frameId = window.requestAnimationFrame(tick)
    }, delay)

    return () => {
      window.clearTimeout(timeoutId)
      if (frameId) window.cancelAnimationFrame(frameId)
    }
  }, [delay, duration, value])

  return <SpriteNumber {...props} value={displayValue} />
}

function RankingSprite({ rank, x, y, delay = RESULT_RANK_DELAY_MS }: {
  rank: 0 | 1 | 2 | 3
  x: number
  y: number
  delay?: number
}) {
  const [visible, setVisible] = useState(false)

  useEffect(() => {
    setVisible(false)
    const timer = window.setTimeout(() => setVisible(true), delay)
    return () => window.clearTimeout(timer)
  }, [delay, rank])

  return (
    <div style={{
      position: 'absolute',
      left: x,
      top: y,
      backgroundImage: `url(${IMG}/mj_ranking_L.png)`,
      backgroundPosition: `${-rank * 39}px 0`,
      backgroundRepeat: 'no-repeat',
      width: 39,
      height: 25,
      imageRendering: 'pixelated',
      opacity: visible ? 1 : 0,
      transform: visible ? 'translateY(0) scale(1)' : 'translateY(-5px) scale(1.18)',
      transition: 'opacity 140ms ease-out, transform 180ms ease-out',
      zIndex: 22,
    }} />
  )
}

function HanCommandButton({ src, x, y, frameW, frameH, onClick }: {
  src: string
  x: number
  y: number
  frameW: number
  frameH: number
  onClick: () => void
}) {
  const [frame, setFrame] = useState(0)
  return (
    <button
      type="button"
      onClick={onClick}
      onMouseEnter={() => setFrame(2)}
      onMouseLeave={() => setFrame(0)}
      onMouseDown={() => setFrame(3)}
      onMouseUp={() => setFrame(2)}
      title={src === 'mj_btOk.png' ? 'OK' : '退室'}
      style={{
        position: 'absolute', left: x, top: y, zIndex: 1220,
        width: frameW, height: frameH, border: 'none', padding: 0,
        backgroundColor: 'transparent', cursor: 'pointer',
        ...frameStyle(src, frameW, frameH, frame),
      }}
    />
  )
}

/** ====================================================================
 * CMJHanRes 本体
 * ==================================================================== */
export default function HanRes({ players, hasTor, hasTip, isViewer, isTournament, displayScale = 1, displayOffsetY = 0, backdrop = false, onClose }: HanResProps) {
  const me = players.find(p => p.isMe)
  const shouldShowLevelDown = !isViewer && !isTournament && !!me && me.nlevel !== undefined && me.prevNlevel !== undefined && me.nlevel < me.prevNlevel
  const [showLevelDown, setShowLevelDown] = useState(false)

  useEffect(() => {
    setShowLevelDown(false)
    if (!shouldShowLevelDown) return
    const timer = window.setTimeout(() => setShowLevelDown(true), 500)
    return () => window.clearTimeout(timer)
  }, [me?.pix, me?.nlevel, me?.prevNlevel, shouldShowLevelDown])

  return (
    <div
      style={{
        position: 'absolute', inset: 0,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        zIndex: 1200,
        background: backdrop ? 'rgba(0, 0, 0, 0.76)' : undefined,
        pointerEvents: 'auto',
      }}
      onContextMenu={e => e.preventDefault()}
    >
      <div style={{ position: 'relative', top: displayOffsetY, width: SCREEN_W, height: RESULT_STAGE_H, imageRendering: 'pixelated', transform: displayScale !== 1 ? `scale(${displayScale})` : undefined, transformOrigin: 'center center' }}>
        <div
          style={{ position: 'relative', width: SCREEN_W, height: SCREEN_H, imageRendering: 'pixelated' }}
        >

        {/* ── 背景ボード mj_endResultBoard.png at (174,75) ── */}
        <img
          src={`${IMG}/mj_endResultBoard.png`}
          alt=""
          draggable={false}
          style={{ position: 'absolute', left: X_BOARD, top: Y_BOARD, imageRendering: 'pixelated' }}
        />

        {/* ── 点数詳細背景 mj_endResultBoard_yen.png at (174,297) ── */}
        <img
          src={`${IMG}/mj_endResultBoard_yen.png`}
          alt=""
          draggable={false}
          style={{ position: 'absolute', left: X_DETAIL, top: Y_DETAIL, imageRendering: 'pixelated' }}
        />

        {/* ── 4人分のプレイヤー情報 ── */}
        {players.map((p) => {
          const mem = p.seatPos
          const offX = mem * W_COLUMN
          const playerDelay = mem * RESULT_PLAYER_STAGGER_MS

          return (
            <div key={`${mem}-${p.pix}`}>
              {/* アバター at (215+mem*108, 138) */}
              <div style={{
                position: 'absolute',
                left: X_AVATAR + offX + AVATAR_RESULT_OFFSET,
                top: Y_AVATAR + AVATAR_RESULT_OFFSET,
                width: AVATAR_RESULT_SIZE,
                height: AVATAR_RESULT_SIZE,
                overflow: 'hidden',
              }}>
                <img
                  src={getHanResAvatarUrl(p)}
                  alt={getHanResDisplayName(p)}
                  style={{ width: '100%', height: '100%', objectFit: 'contain' }}
                  onError={e => {
                    if (isDummyPlayer(p)) return
                    (e.currentTarget as HTMLImageElement).src =
                      getDefaultAvatarUrl(getSexFallback(p.sex))
                  }}
                />
              </div>

              {/* ニックネーム at (185+mem*108, 206) */}
              <div style={{
                position: 'absolute',
                left: X_PIX + offX,
                top: Y_PIX,
                width: 105,
                height: 14,
                fontFamily: "'Noto Sans JP', 'Noto Sans JP', 'MS Gothic', monospace",
                fontSize: 14,
                color: isDummyPlayer(p) ? '#e00000' : '#fff',
                fontWeight: 'bold',
                lineHeight: '14px',
                textAlign: 'center',
                overflow: 'hidden',
                whiteSpace: 'nowrap',
                textOverflow: 'ellipsis',
              }}>
                {getHanResDisplayName(p)}
              </div>

              {/* 순위 mj_ranking_L.png (4フレーム 39×25) at (248+mem*108, 230) */}
              <RankingSprite rank={p.rank} x={X_SETRANK + offX} y={Y_SETRANK} delay={RESULT_RANK_DELAY_MS + playerDelay} />

              {/* 合計点 at (273+mem*108, 261) */}
              <AnimatedSpriteNumber src="mj_ptResult_num_pls.png" frameW={14} frameH={25} value={p.point} x={X_SETPOINT + offX} y={Y_SETPOINT} digits={7} delay={playerDelay} />

              {/* バランス at (277+mem*108, 310) */}
              <AnimatedSpriteNumber src="mj_num_game00.png" frameW={9} frameH={17} value={p.setBal} x={X_SETBAL + offX} y={Y_SETBAL} digits={7} sign delay={RESULT_DETAIL_DELAY_MS + playerDelay} />

              {/* 点数 at (277+mem*108, 344) */}
              <AnimatedSpriteNumber src="mj_num_game00.png" frameW={9} frameH={17} value={p.setTen} x={X_SETTEN + offX} y={Y_SETTEN} digits={7} sign delay={RESULT_DETAIL_DELAY_MS + playerDelay} />

              {/* ウマ at (277+mem*108, 374) */}
              <AnimatedSpriteNumber src="mj_num_game00.png" frameW={9} frameH={17} value={p.setUma} x={X_SETUMA + offX} y={Y_SETUMA} digits={7} sign delay={RESULT_DETAIL_DELAY_MS + playerDelay} />

              {/* 飛び at (277+mem*108, 404) — rule.nTor > 0 の場合のみ */}
              {hasTor && p.setTor !== undefined && (
                <AnimatedSpriteNumber src="mj_num_game00.png" frameW={9} frameH={17} value={p.setTor} x={X_SETTOR + offX} y={Y_SETTOR} digits={7} sign delay={RESULT_DETAIL_DELAY_MS + playerDelay} />
              )}

              {/* チップ at (277+mem*108, 434) — rule.bTip の場合のみ */}
              {hasTip && p.setTip !== undefined && (
                <AnimatedSpriteNumber src="mj_num_game00.png" frameW={9} frameH={17} value={p.setTip} x={X_SETCHP + offX} y={Y_SETCHP} digits={7} sign delay={RESULT_DETAIL_DELAY_MS + playerDelay} />
              )}
            </div>
          )
        })}

        {/* ── 観戦者ブラインド mj_endResultBoard_blind.png at (174,465) ── */}
        {isViewer && (
          <img
            src={`${IMG}/mj_endResultBoard_blind.png`}
            alt=""
            draggable={false}
            style={{ position: 'absolute', left: X_BLIND, top: Y_BLIND, imageRendering: 'pixelated' }}
          />
        )}

        {/* ── 自分のコイン獲得 mj_endResult_ptYen.png at (558,502) ── */}
        {!isViewer && !isTournament && (() => {
          if (!me || me.coinGain === undefined) return null
          return <AnimatedSpriteNumber src="mj_endResult_ptYen.png" frameW={19} frameH={33} value={me.coinGain} x={X_MONGAIN} y={Y_MONGAIN} digits={11} delay={RESULT_DETAIL_DELAY_MS + RESULT_PLAYER_STAGGER_MS * 4} />
        })()}

        {/* ── 次レベルまで mj_endResultNum_w.png at (575,545) ── */}
        {!isViewer && !isTournament && (() => {
          if (!me || me.coinNeed === undefined || me.coinNeed <= 0) return null
          return <AnimatedSpriteNumber src="mj_endResultNum_w.png" frameW={9} frameH={17} value={me.coinNeed} x={X_MONNEED} y={Y_MONNEED} digits={11} delay={RESULT_DETAIL_DELAY_MS + RESULT_PLAYER_STAGGER_MS * 4} />
        })()}

        {showLevelDown && (() => {
          if (!me) return null
          return (
            <>
              <img src={`${IMG}/mj_endResultBoard_dn.png`} alt="" draggable={false} style={{ position: 'absolute', left: X_LEVEL, top: Y_LEVEL, imageRendering: 'pixelated', zIndex: 30 }} />
              <div style={{ position: 'absolute', left: X_LVLDN, top: Y_LVLDN, width: 90, height: 30, zIndex: 31, color: '#fff', font: 'bold 30px MS Gothic, monospace', lineHeight: '30px', textAlign: 'center', overflow: 'hidden', whiteSpace: 'nowrap', textOverflow: 'ellipsis' }}>
                {me.levelName ?? ''}
              </div>
            </>
          )
        })()}

        </div>
        <HanCommandButton src="mj_btOk.png" x={X_RESULT_OK} y={Y_RESULT_OK} frameW={OK_FRAME_W} frameH={OK_FRAME_H} onClick={onClose} />
      </div>
    </div>
  )
}
