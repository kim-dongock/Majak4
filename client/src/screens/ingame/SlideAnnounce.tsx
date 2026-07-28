/**
 * CMJSlideAnnounce 相当 — インゲーム スライドアナウンス (AP-09 §2-10)
 * レガシー: legacy/client/HgMajak2/MJSlideAnnounce.h/cpp
 *
 * ── 動作 (OnTimer 相当) ───────────────────────────────────────────────
 *   1. 画面右外(x=1024)から左へスライドイン (t/SPEED, SPEED=1)
 *   2. 2000ms 静止
 *   3. 右へスライドアウト (SPEED=1px/ms、MS_TIMER=20ms)
 *   4. 非表示
 *
 * ── タイプ (ANNOUNCE_GET_*) ───────────────────────────────────────────
 *   0=TRICKTITLE  → mj_pop_waza.png   + mj_skill_{code:03d}.png (fg)
 *   1=MAJAKTITLE  → mj_pop_syou.png   + mj_title_{code:03d}.png or mj_ctitle_{code-1000:03d}.png
 *   2=RYUTAMA     → mj_pop_ryu.png    + テキスト "{amount}個"
 *   3=YAKUMAN     → mj_pop_yakuman.png + 3行テキスト
 *
 * ── テキスト座標 (OnPaint DrawText より) ─────────────────────────────
 *   TRICKTITLE: rcText[0]=(9,193,206,204) DT_CENTER
 *   MAJAKTITLE: rcText[0]=(9,105,207,116) DT_CENTER
 *   RYUTAMA:    rcText[0]=(112,193,174,204) DT_CENTER
 *   YAKUMAN:    rcText[0-2] y=193/208/223 DT_LEFT
 *
 * ── 背景画像サイズ (レガシー実測) ─────────────────────────────────────
 *   m_rect.top=38, 各 .him の高さに合わせる
 *   幅は画像幅。right=1024 固定で、left を動かしてスライド/クリップする。
 * ─────────────────────────────────────────────────────────────────────
 */
import { useEffect, useRef, useState } from 'react'
import { playMajakSfx } from '../../utils/majakSound'

const IMG = '/assets/images/game'

// ANNOUNCE_GET_* 定数
export const ANNOUNCE_GET_TRICKTITLE = 0
export const ANNOUNCE_GET_MAJAKTITLE = 1
export const ANNOUNCE_GET_RYUTAMA    = 2
export const ANNOUNCE_GET_YAKUMAN    = 3

/** SlideAnnounce 表示データ */
export interface SlideAnnounceData {
  /** ANNOUNCE_GET_* */
  type:   0 | 1 | 2 | 3
  /** タイトルコード (mj_skill_NNN.png / mj_title_NNN.png のインデックス) */
  code:   number
  /** テキスト1行目 (pszName) */
  name:   string
  /** テキスト2行目 (pszName2 — YAKUMAN 時のみ) */
  name2?: string
}

interface Props {
  data:    SlideAnnounceData | null
  /** アニメーション完了コールバック */
  onDone?: () => void
  /** レガシー m_rect.top。MajakFrame 配下では title bar 31px を呼び出し側で補正する。 */
  top?: number
}

/** 背景画像ファイル名 */
function bgImage(type: number): string {
  switch (type) {
    case ANNOUNCE_GET_TRICKTITLE: return `${IMG}/mj_pop_waza.png`
    case ANNOUNCE_GET_MAJAKTITLE: return `${IMG}/mj_pop_syou.png`
    case ANNOUNCE_GET_RYUTAMA:    return `${IMG}/mj_pop_ryu.png`
    case ANNOUNCE_GET_YAKUMAN:    return `${IMG}/mj_pop_yakuman.png`
    default:                      return `${IMG}/mj_pop_waza.png`
  }
}

/** 前景画像ファイル名 (TRICKTITLE / MAJAKTITLE のみ) */
function fgImage(type: number, code: number): string | null {
  if (type === ANNOUNCE_GET_TRICKTITLE) {
    return `${IMG}/mj_skill_${String(code).padStart(3, '0')}.png`
  }
  if (type === ANNOUNCE_GET_MAJAKTITLE) {
    if (code < 1000) return `${IMG}/mj_title_${String(code).padStart(3, '0')}.png`
    return `${IMG}/mj_ctitle_${String(code - 1000).padStart(3, '0')}.png`
  }
  return null
}

function preloadImage(src: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const img = new Image()
    img.onload = () => resolve()
    img.onerror = () => reject(new Error(src))
    img.src = src
  })
}

/** ====================================================================
 * CMJSlideAnnounce 本体
 * ==================================================================== */
export default function SlideAnnounce({ data, onDone, top = 38 }: Props) {
  /** 現在の X オフセット (右端からの距離) — 0=完全表示 */
  const [offsetX, setOffsetX] = useState(0)
  const [visible,  setVisible]  = useState(false)

  const startTimeRef = useRef<number>(0)
  const rafRef       = useRef<number>(0)
  const IMG_W        = 217  // 背景画像幅 (mj_pop_*.png 実測: 217px)
  const LEGACY_RIGHT = 1024 // MFC: m_rect.right=1024
  const SPEED        = 1    // px/ms (レガシー: rc.left = rc.right - t / SPEED)
  const HOLD_MS      = 2000

  useEffect(() => {
    if (!data) {
      setVisible(false)
      return
    }

    let cancelled = false
    const bg = bgImage(data.type)
    const fg = fgImage(data.type, data.code)

    const tick = (now: number) => {
      const t = now - startTimeRef.current
      const slideDuration = IMG_W / SPEED  // IMG_W ms でスライドイン完了

      let x: number
      if (t < slideDuration) {
        /* フェーズ1: スライドイン — 右端から左へ */
        x = IMG_W - t * SPEED
      } else if (t < slideDuration + HOLD_MS) {
        /* フェーズ2: 静止 */
        x = 0
      } else if (t < slideDuration * 2 + HOLD_MS) {
        /* フェーズ3: スライドアウト — 左から右へ */
        x = (t - slideDuration - HOLD_MS) * SPEED
      } else {
        /* フェーズ4: 非表示 */
        setVisible(false)
        onDone?.()
        return
      }
      setOffsetX(x)
      rafRef.current = requestAnimationFrame(tick)
    }

    Promise.all([bg, fg].filter((src): src is string => Boolean(src)).map(preloadImage))
      .then(() => {
        if (cancelled) return
        /* サウンド再生 (CMJSound::PlaySFX "mjkgettitle") */
        playMajakSfx('mjkgettitle')
        setOffsetX(IMG_W)
        setVisible(true)
        startTimeRef.current = performance.now()
        rafRef.current = requestAnimationFrame(tick)
      })
      .catch(() => {
        if (cancelled) return
        setVisible(false)
        onDone?.()
      })

    return () => {
      cancelled = true
      cancelAnimationFrame(rafRef.current)
    }
  }, [data])  // eslint-disable-line react-hooks/exhaustive-deps

  if (!visible || !data) return null

  const fg = fgImage(data.type, data.code)
  const font = "'MS PGothic', 'MS UI Gothic', sans-serif"
  const textStyle = {
    fontFamily: font,
    fontSize: 12, fontWeight: 'bold' as const,
    lineHeight: '12px',
    color: '#000',
    background: 'transparent',
    pointerEvents: 'none' as const,
  }

  return (
    /* CMJSlideAnnounce: top=38, right=1024, left を動かして表示幅をクリップ */
    <div style={{
      position: 'absolute',
      top,
      left: LEGACY_RIGHT - IMG_W + offsetX,
      width: IMG_W - offsetX,
      zIndex: 500,
      overflow: 'hidden',
      pointerEvents: 'none',
    }}>
      {/* ── 背景画像 ── */}
      <div style={{ position: 'relative' }}>
        <img
          src={bgImage(data.type)}
          alt=""
          draggable={false}
          style={{ display: 'block', imageRendering: 'pixelated' }}
          onError={() => {
            setVisible(false)
            onDone?.()
          }}
        />

        {/* ── 前景画像 (TRICKTITLE / MAJAKTITLE) ── */}
        {fg && (
          <img
            src={fg}
            alt=""
            draggable={false}
            style={{
              position: 'absolute',
              left: data.type === ANNOUNCE_GET_TRICKTITLE ? 58 : 84,
              top:  data.type === ANNOUNCE_GET_TRICKTITLE ? 53 : 54,
              imageRendering: 'pixelated',
              pointerEvents: 'none',
            }}
            onError={() => {
              setVisible(false)
              onDone?.()
            }}
          />
        )}

        {/* ── テキスト ── */}
        {data.type === ANNOUNCE_GET_YAKUMAN ? (
          /* YAKUMAN: 3行テキスト DT_LEFT */
          <>
            <div style={{
              ...textStyle,
              position: 'absolute', left: 15, top: 193, width: 197,
              textAlign: 'left',
            }}>
              {data.name}さんの
            </div>
            <div style={{
              ...textStyle,
              position: 'absolute', left: 15, top: 208, width: 197,
              textAlign: 'left',
            }}>
              {data.name2}達成が
            </div>
            <div style={{
              ...textStyle,
              position: 'absolute', left: 15, top: 223, width: 197,
              textAlign: 'left',
            }}>
              見事に役満ボーナスになりました。
            </div>
          </>
        ) : data.type === ANNOUNCE_GET_RYUTAMA ? (
          /* RYUTAMA: "{amount}個" DT_CENTER */
          <div style={{
            ...textStyle,
            position: 'absolute', left: 112, top: 193, width: 62,
            textAlign: 'center',
          }}>
            {data.code}個
          </div>
        ) : data.type === ANNOUNCE_GET_TRICKTITLE ? (
          /* TRICKTITLE: テキスト1行 DT_CENTER */
          <div style={{
            ...textStyle,
            position: 'absolute', left: 9, top: 193, width: 197,
            textAlign: 'center',
          }}>
            {data.name}
          </div>
        ) : (
          /* MAJAKTITLE: テキスト1行 DT_CENTER */
          <div style={{
            ...textStyle,
            position: 'absolute', left: 9, top: 105, width: 198,
            textAlign: 'center',
          }}>
            {data.name}
          </div>
        )}
      </div>
    </div>
  )
}
