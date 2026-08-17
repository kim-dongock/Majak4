/**
 * CMJLotSlotDlg 相当 — 抽選スロットダイアログ (AP-09 §3-2-11)
 * レガシー: legacy/client/HgMajak2/MJLotSlotDlg.h/cpp
 *
 * ウィンドウ: 624×222px (CRect rcMemDC(0,0,624,222) より)
 * ※ MoveWindow 呼び出しなし → .rc リソースサイズ使用; lot_base1.png と同サイズ
 *
 * ── 画像配置 (OnCreate / OnPaint より — 座標すべてレガシー準拠) ─────────────
 *
 * 全画像は lot/ サブフォルダに格納
 *
 * 背景 (1フレーム 624×222):
 *   lot/lot_base1.png  at (0, 0)
 *
 * 閉じるボタン (4フレーム 18×18):
 *   lot/lot_btn_close.png  at (599, 7)  IDC_BTN_CLOSE
 *
 * 1回 ボタン (4フレーム 72×42):
 *   lot/lot_t_btn_1.png  at (430, 161)  IDC_BTN_START → OnBtnStartClicked
 *
 * 全回 → 結果ボタン (4フレーム 72×42, 同座標で切り替え):
 *   lot/lot_t_btn_2.png (全回)   at (510, 161)  IDC_BTN_RESULT
 *   lot/lot_t_btn_5.png (結果表示) at (510, 161)  IDC_BTN_RESULT → OnBtnResultClicked
 *
 * リール (スロット回転表示エリア):
 *   REEL_CORNER_POS_X=137, Y=61, WIDTH=33, HEIGHT=78
 *   REEL_CORNER_POS_X2=256 / X3=441
 *   lot/lot_slot1.png   (100×78, 4フレーム 25×78)   — リール背景
 *   lot/lot_slot_num.png (250×78, 10フレーム 25×78)  — 数字 0-9
 *
 * ── タイマーシーケンス (OnTimer — setInterval/setTimeout 相当) ────────────
 *   TIMER_LOT_SLOT_START       → 回転開始
 *   TIMER_LOT_SLOT_ROTATION    → リール回転アニメーション
 *   TIMER_LOT_SLOT_NUMREEL_STOP → 数字リール停止
 *   TIMER_LOT_SLOT_UNITREEL_STOP → 単位リール停止
 *   TIMER_LOT_SLOT_ENABLEBTN   → ボタン再有効化
 * ────────────────────────────────────────────────────────────────────────
 */
import { useState, useEffect, useRef, useCallback } from 'react'
import { showConfirm, showMessage } from '../../../utils/msgbox'
import { playMajakSfx } from '../../../utils/majakSound'
import LotResultDlg, { type LotEntry } from './LotResultDlg'

const IMG     = '/assets/images/game'
const IMG_LOT = `${IMG}/lot`

/** REEL 定数 (レガシーより) */
const REEL_CORNER_POS_X  = 137
const REEL_CORNER_POS_Y  = 61
const REEL_HEIGHT         = 78
const REEL_SCALE          = 1.25
const REEL_WIDTH          = 25
const DISPLAY_REEL_WIDTH  = REEL_WIDTH * REEL_SCALE
const DISPLAY_REEL_HEIGHT = REEL_HEIGHT * REEL_SCALE
const REEL_FRAME_WIDTH    = DISPLAY_REEL_WIDTH
// REEL_WIDTH=33 はレガシー定数として保持 (現在は slot_num.png の 25px を使用)
const MIN_REEL_COUNT      = 4

interface Props {
  itemName: string
  lotteryCount: number   // m_pShopItemData->m_nLotteryCount
  totalAmount?: number   // CRandomDiv::RndDiv の元金額
  lotValues?: number[]   // CRandomDiv::GetRndValue(i) 相当。指定時はこの値をそのまま使う
  nextLotteryCount?: number
  imageUrl?: string
  onResult: (amount: number) => void
  onClose: () => void
}

function moneyString(value: number): string {
  return Math.trunc(value).toLocaleString('ja-JP')
}

function createLotValues(totalAmount: number, count: number): number[] {
  if (count <= 0) return []
  const total = Math.max(0, Math.trunc(totalAmount))
  if (count === 1) return [total]
  if (total < count) return Array(count).fill(0)

  // CRandomDiv::NormalAllotment port: minimum 1 GP per draw, then random dispersion and adjustment.
  const values = Array.from({ length: count }, () => Math.random())
  const randomSum = values.reduce((sum, value) => sum + value, 0) || 1
  let dispersion = 1
  if (count >= 70) dispersion = Math.max(1, Math.floor(total / 3_000_000))
  else if (count >= 6) dispersion = Math.max(1, Math.floor(total / 1_000_000))

  const weightedSum = randomSum * dispersion
  const result = values.map(value => Math.max(1, Math.floor(total * value / weightedSum)))
  let adjustment = total - result.reduce((sum, value) => sum + value, 0)
  let adjustmentPart = adjustment
  let iteration = 0

  while (dispersion > 1 && adjustment > 0) {
    let numerator = Math.floor(Math.random() * 10) + 1
    let denominator = numerator + Math.floor(Math.random() * 10) + 1
    if (iteration === 0) denominator += Math.floor(dispersion / 50)
    else if (iteration === 1) {
      numerator += Math.floor(dispersion / 50)
      denominator += Math.floor(dispersion / 50)
    }
    adjustmentPart -= Math.floor(adjustmentPart * numerator / denominator)
    dispersion -= Math.floor(dispersion * numerator / denominator)
    result[Math.floor(Math.random() * count)] += adjustmentPart
    adjustment -= adjustmentPart
    iteration++
  }

  if (adjustment > 0) result[Math.floor(Math.random() * count)] += adjustment
  else if (adjustment < 0) {
    const largestIndex = result.reduce((largest, value, index, array) => value > array[largest] ? index : largest, 0)
    result[largestIndex] += adjustment
  }
  return result
}

/** ====================================================================
 * CMJBmpButton 相当 — AP-06 §2 4フレームスプライトボタン
 * ==================================================================== */
function SpriteButton({
  src, frameW, frameH, x, y, onClick, disabled = false, title,
}: {
  src: string; frameW: number; frameH: number
  x: number; y: number; onClick: () => void
  disabled?: boolean; title?: string
}) {
  const [fi, setFi] = useState(disabled ? 1 : 0)
  const buttonRef = useRef<HTMLButtonElement>(null)
  useEffect(() => { setFi(disabled ? 1 : 0) }, [disabled])
  useEffect(() => {
    const button = buttonRef.current
    if (!button) return
    button.style.setProperty('width', `${frameW}px`, 'important')
    button.style.setProperty('height', `${frameH}px`, 'important')
    button.style.setProperty('background-image', `url(${src})`, 'important')
    button.style.setProperty('background-position', `${-fi * frameW}px 0`, 'important')
    button.style.setProperty('background-repeat', 'no-repeat', 'important')
  }, [fi, frameH, frameW, src])
  return (
    <button
      ref={buttonRef}
      title={title}
      disabled={disabled}
      onClick={disabled ? undefined : onClick}
      onMouseEnter={() => !disabled && setFi(2)}
      onMouseLeave={() => setFi(disabled ? 1 : 0)}
      onMouseDown={() => !disabled && setFi(3)}
      onMouseUp={() => !disabled && setFi(2)}
      style={{
        position: 'absolute', left: x, top: y,
        width: frameW, height: frameH,
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-fi * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        border: 'none', padding: 0,
        cursor: disabled ? 'not-allowed' : 'pointer',
        outline: 'none', imageRendering: 'pixelated',
      }}
    />
  )
}

/** ====================================================================
 * 数字リール (TIMER_LOT_SLOT_ROTATION 相当)
 * lot_slot_num.png: 250×78, 10フレーム 25×78 (数字 0〜9)
 * ==================================================================== */
function NumberReel({ digit, spinFrame }: { digit: number; spinFrame: number }) {
  const isRotating = digit < 0
  const displayedDigit = isRotating ? spinFrame % 10 : digit
  return (
    <div style={{
      width: DISPLAY_REEL_WIDTH, height: DISPLAY_REEL_HEIGHT,
      overflow: 'hidden',
      position: 'relative',
    }}>
      <div
        style={{
          position: 'absolute', left: 0, top: 0,
          width: REEL_WIDTH, height: REEL_HEIGHT,
          backgroundImage: `url(${IMG_LOT}/lot_slot_num.png)`,
          backgroundPosition: `${-displayedDigit * 25}px 0`,
          backgroundRepeat: 'no-repeat',
          imageRendering: 'pixelated',
          transform: `scale(${REEL_SCALE})`,
          transformOrigin: 'top left',
        }}
      />
    </div>
  )
}

/** ====================================================================
 * CMJLotSlotDlg 本体
 * ==================================================================== */
type Phase = 'idle' | 'spinning' | 'stopped' | 'done'

export default function LotSlotDlg({
  itemName, lotteryCount, totalAmount = 0, lotValues, nextLotteryCount, imageUrl, onResult, onClose: _onClose,
}: Props) {
  const valuesRef = useRef<number[]>(lotValues?.slice(0, lotteryCount) ?? createLotValues(totalAmount, lotteryCount))
  const reelCount = Math.max(
    MIN_REEL_COUNT,
    String(Math.max(0, totalAmount, ...valuesRef.current)).length,
  )
  const [phase,   setPhase]   = useState<Phase>('idle')
  const [digits,  setDigits]  = useState<number[]>(Array(reelCount).fill(0))
  const [spinFrame, setSpinFrame] = useState(0)
  const [amount,  setAmount]  = useState(0)
  const [lotCnt,  setLotCnt]  = useState(0)
  const [showResultDlg, setShowResultDlg] = useState(false)
  const spinTimer  = useRef<ReturnType<typeof setInterval>  | null>(null)
  const stopTimer  = useRef<ReturnType<typeof setTimeout>   | null>(null)

  useEffect(() => {
    return () => {
      if (spinTimer.current) clearInterval(spinTimer.current)
      if (stopTimer.current) clearTimeout(stopTimer.current)
    }
  }, [])

  /** OnBtnStartClicked — 1回抜符・サウンド (mjkslotstart) */
  const handleOnce = useCallback(async () => {
    if (phase !== 'idle' && phase !== 'stopped') return
    if (lotteryCount - lotCnt <= 0) return
    setPhase('spinning')
    const result = valuesRef.current[lotCnt] ?? 0
    const resultDigits = String(result).padStart(reelCount, '0').slice(-reelCount).split('').map(Number)
    setDigits(Array(reelCount).fill(-1))

    /* CMJSound::LoadSFX("mjkslotstart") + PlaySFX 相当 */
    playMajakSfx('mjkslotstart')

    /* TIMER_LOT_SLOT_ROTATION 相当: lot_slot1 4フレーム回転 */
    spinTimer.current = setInterval(() => {
      setSpinFrame(frame => frame + 1)
    }, 30)

    stopTimer.current = setTimeout(() => {
      let stopIndex = 0
      stopTimer.current = setInterval(() => {
        playMajakSfx('mjkslotstop')
        const reelIndex = reelCount - 1 - stopIndex
        setDigits(current => current.map((digit, index) => (
          index === reelIndex ? resultDigits[index] : digit
        )))
        stopIndex++
        if (stopIndex >= reelCount) {
          if (spinTimer.current) clearInterval(spinTimer.current)
          if (stopTimer.current) clearInterval(stopTimer.current)
          stopTimer.current = setTimeout(() => {
            setAmount(current => current + result)
            setLotCnt(current => current + 1)
            setPhase('stopped')
          }, 500)
        }
      }, 300)
    }, 900)
  }, [lotCnt, lotteryCount, phase, reelCount])

  const remaining  = lotteryCount - lotCnt
  const isDone     = phase === 'done'
  const isSpinning = phase === 'spinning'
  const showResult = remaining <= 0
  const resultTotal = valuesRef.current.reduce((sum, value) => sum + value, 0)
  const resultEntries: LotEntry[] = valuesRef.current.map((value, index) => ({ seq: index + 1, amount: value }))

  /**
   * OnBtnCloseClicked → CloseDlg(FALSE) 相当
   * LOTCNT_REMAINING > 0 の場合は確認ダイアログを表示する
   */
  const handleClose = useCallback(async () => {
    if (isSpinning) return  // 回転中は閉じない
    if (remaining <= 0) {
      _onClose()
      return
    }
    if (remaining > 0) {
      const ok = await showConfirm(
        '残りカウント分は自動的に全回転STARTされます。\nよろしいですか？'
      )
      if (!ok) return
    } else {
      await showMessage('残りカウントがありませんので\n結果画面を表 示します。')
    }
    setShowResultDlg(true)
  }, [_onClose, isSpinning, remaining])

  /** OnBtnResultClicked → CloseDlg(TRUE) 相当 */
  const handleResult = useCallback(() => {
    setPhase('done')
    setShowResultDlg(true)
  }, [])

  return (
    <div className="majak-popup-overlay majak-lottery-slot-overlay" role="presentation">
      <section
        role="dialog"
        aria-modal="true"
        aria-label={`${itemName} 抽選`}
        className="majak-lottery-game-panel"
      >
        <header className="majak-lottery-game-panel__header">
          <div>
            <strong>{itemName}</strong>
          </div>
          <dl>
            <div><dt>残り</dt><dd>{remaining}回</dd></div>
            <div><dt>賞金総額</dt><dd>{moneyString(totalAmount)} GP</dd></div>
          </dl>
          <button type="button" className="majak-lottery-game-panel__close" onClick={() => { void handleClose() }} disabled={isSpinning || remaining > 0} aria-label="閉じる" title={remaining > 0 ? '残りの抽選後に閉じられます' : undefined}>×</button>
        </header>
        <div className="majak-lottery-game-panel__stage" aria-label="抽選金額">
          <div className="majak-lottery-game-panel__reels">
            {digits.map((digit, index) => (
              <div key={index} className="majak-lottery-game-panel__reel">
                <NumberReel digit={digit} spinFrame={spinFrame + index} />
              </div>
            ))}
          </div>
          <p>{isSpinning ? '抽選中...' : `現在の獲得合計 ${moneyString(amount)} GP`}</p>
          <div className="majak-lottery-game-panel__guide">
            <span>停止した数字が、その回の獲得GPです。</span>
            <span>すべての抽選結果の合計を受け取れます。</span>
          </div>
        </div>
        <footer className="majak-lottery-game-panel__actions">
          <button type="button" className="is-draw" onClick={() => { void handleOnce() }} disabled={isSpinning || isDone || remaining <= 0}>1回抽選</button>
          <button type="button" className="is-result" onClick={handleResult} disabled={isSpinning || isDone}>{showResult ? '結果を見る' : '結果へ'}</button>
        </footer>
      </section>
      {showResultDlg && <LotResultDlg itemName={itemName} lotteryCount={lotteryCount} entries={resultEntries} totalAmount={resultTotal} nextLotteryCount={nextLotteryCount ?? lotteryCount} onBuyAgain={() => { setShowResultDlg(false); _onClose() }} onClose={() => { setShowResultDlg(false); onResult(resultTotal) }} />}
      <style>{`
        .majak-lottery-game-panel { width: min(760px, calc(100dvw - 32px)); max-height: calc(100dvh - 32px); display: flex; flex-direction: column; color: #f8f6e9; overflow: hidden; border: 2px solid #d9bc62; border-radius: 7px; background: repeating-linear-gradient(135deg, #123d31 0 12px, #0e3429 12px 24px); box-shadow: 0 22px 55px rgba(0, 0, 0, .55), inset 0 0 0 4px rgba(255,255,255,.07); }
        .majak-lottery-game-panel__header { display: flex; min-height: 76px; align-items: center; gap: 24px; padding: 13px 19px; background: linear-gradient(90deg, #1b5a4b, #24705b 48%, #1b5a4b); border-bottom: 2px solid #d9bc62; }
        .majak-lottery-game-panel__header > div { min-width: 190px; }.majak-lottery-game-panel__header strong { display: block; color: #f8f6e9; font: 700 var(--majak-popup-font-title)/var(--majak-popup-leading-title) var(--majak-font-family-ui); }
        .majak-lottery-game-panel__header dl { display: flex; gap: 25px; margin: 0 0 0 auto; }.majak-lottery-game-panel__header dt { color: #d7e3d7; font-size: var(--majak-popup-font-body); line-height: var(--majak-popup-leading-body); }.majak-lottery-game-panel__header dd { margin: 4px 0 0; color: #f8f6e9; font: 700 var(--majak-popup-font-emphasis)/var(--majak-popup-leading-emphasis) var(--majak-font-family-ui); white-space: nowrap; }
        .majak-lottery-game-panel__close { width: 28px; height: 28px; border: 1px solid #d9bc62; border-radius: 4px; color: #f8f6e9; background: #1c6b58; font-size: 20px; cursor: pointer; }.majak-lottery-game-panel__close:hover, .majak-lottery-game-panel__close:focus-visible { background: #247c67; }.majak-lottery-game-panel__close:disabled { opacity: .45; }
        .majak-lottery-game-panel__stage { min-height: 0; padding: 28px 30px 23px; overflow: auto; text-align: center; background: radial-gradient(ellipse at center, rgba(217,188,98,.15), transparent 60%); }.majak-lottery-game-panel__reels { display: inline-flex; align-items:center; gap: 10px; padding: 13px 16px; border: 2px solid #d9bc62; border-radius: 6px; background: #173d31; box-shadow: inset 0 0 18px #071d16, 0 8px 18px rgba(0,0,0,.3); }.majak-lottery-game-panel__reel { display:flex; justify-content:center; width: ${REEL_FRAME_WIDTH}px; height: ${DISPLAY_REEL_HEIGHT}px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,.65); }
        .majak-lottery-game-panel__stage p { margin: 14px 0 0; color: #d9bc62; font: 700 var(--majak-popup-font-emphasis)/var(--majak-popup-leading-emphasis) var(--majak-font-family-ui); }.majak-lottery-game-panel__guide { display:flex; justify-content:center; gap:16px; margin-top:11px; color:#d7e3d7; font-size:var(--majak-popup-font-body); line-height:var(--majak-popup-leading-body); }.majak-lottery-game-panel__guide span::before { content:'●'; margin-right:5px; color:#d9bc62; }.majak-lottery-game-panel__actions { display: flex; justify-content: flex-end; gap: 10px; padding: 13px 18px; background: rgba(5,31,23,.62); border-top: 1px solid rgba(217,188,98,.55); }.majak-lottery-game-panel__actions button { min-width: 122px; height: 38px; border: 1px solid #255d4e; border-radius: 4px; color: #fff; font: 700 var(--majak-popup-font-emphasis)/1 var(--majak-font-family-ui); cursor: pointer; }.majak-lottery-game-panel__actions .is-draw { background: #1c6b58; }.majak-lottery-game-panel__actions .is-result { border-color: #698674; background: #315f4d; }.majak-lottery-game-panel__actions button:disabled { cursor: default; opacity: .42; }
        @media (max-width: 600px) { .majak-lottery-game-panel__header { gap: 12px; padding: 11px 13px; }.majak-lottery-game-panel__header > div { min-width: 0; }.majak-lottery-game-panel__header strong { font-size: var(--majak-popup-font-title); }.majak-lottery-game-panel__header dl { gap: 11px; }.majak-lottery-game-panel__header dd { font-size: var(--majak-popup-font-emphasis); }.majak-lottery-game-panel__stage { padding: 18px 8px; }.majak-lottery-game-panel__reels { gap: 4px; padding: 8px; }.majak-lottery-game-panel__reel { width: ${DISPLAY_REEL_WIDTH + 4}px; }.majak-lottery-game-panel__guide { display:grid; gap:4px; text-align:left; }.majak-lottery-game-panel__actions { padding: 10px; }.majak-lottery-game-panel__actions button { min-width: 108px; } }
      `}</style>
    </div>
  )
}
