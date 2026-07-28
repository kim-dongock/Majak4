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
// REEL_WIDTH=33 はレガシー定数として保持 (現在は slot_num.png の 25px を使用)
const NUMOF_FIGURE        = 10   /* 数字リール数 */

/**
 * レガシー OnInitDialog の RECT計算より:
 * リールは 3 グループに分かれて配置される
 *   グループ1 (i=9,8):  X=137, 170
 *   グループ2 (i=7−4): X=256, 289, 322, 355
 *   グループ3 (i=3−0): X=441, 474, 507, 540
 */
const REEL_X: number[] = (() => {
  const REEL_WIDTH = 33
  const X2 = 256, X3 = 441
  let left = REEL_CORNER_POS_X
  const pos = new Array(NUMOF_FIGURE)
  for (let i = NUMOF_FIGURE - 1; i >= 0; i--) {
    pos[i] = left
    if (i === 8)       left = X2
    else if (i === 4)  left = X3
    else               left = pos[i] + REEL_WIDTH
  }
  return pos
})()

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
  const sign = value < 0 ? '-' : ''
  const digits = String(Math.abs(Math.trunc(value)))
  const units = ['', '万', '億', '兆', '京']
  const parts: string[] = []
  for (let end = digits.length, unit = 0; end > 0; end -= 4, unit++) {
    const start = Math.max(0, end - 4)
    const part = Number(digits.slice(start, end))
    if (part > 0) parts.unshift(`${part}${units[unit] ?? ''}`)
  }
  return `${sign}${parts.length > 0 ? parts.join('') : '0'}`
}

function createLotValues(totalAmount: number, count: number): number[] {
  if (count <= 0) return []
  if (count === 1) return [Math.max(0, Math.trunc(totalAmount))]
  const total = Math.max(0, Math.trunc(totalAmount))
  if (total < count) return Array(count).fill(0)
  const values = Array.from({ length: count }, () => Math.random())
  const sum = values.reduce((acc, value) => acc + value, 0) || 1
  const out = values.map(value => Math.max(1, Math.floor(total * value / sum)))
  let adjust = total - out.reduce((acc, value) => acc + value, 0)
  while (adjust > 0) {
    out[Math.floor(Math.random() * out.length)]++
    adjust--
  }
  return out
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
  useEffect(() => { setFi(disabled ? 1 : 0) }, [disabled])
  return (
    <button
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
  return (
    <div style={{
      width: 25, height: REEL_HEIGHT,
      overflow: 'hidden',
      position: 'relative',
    }}>
      <div
        style={{
          position: 'absolute', left: 0, top: 0,
          width: 25, height: REEL_HEIGHT,
          backgroundImage: `url(${IMG_LOT}/${isRotating ? 'lot_slot1.png' : 'lot_slot_num.png'})`,
          backgroundPosition: `${-(isRotating ? spinFrame : digit) * 25}px 0`,
          backgroundRepeat: 'no-repeat',
          imageRendering: 'pixelated',
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
  const [phase,   setPhase]   = useState<Phase>('idle')
  const [digits,  setDigits]  = useState<number[]>(Array(NUMOF_FIGURE).fill(0))
  const [spinFrame, setSpinFrame] = useState(0)
  const [amount,  setAmount]  = useState(0)
  const [lotCnt,  setLotCnt]  = useState(0)
  const [showResultDlg, setShowResultDlg] = useState(false)
  const valuesRef = useRef<number[]>(lotValues?.slice(0, lotteryCount) ?? createLotValues(totalAmount, lotteryCount))

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
    const resultDigits = String(result).padStart(NUMOF_FIGURE, '0').slice(-NUMOF_FIGURE).split('').map(Number)
    setDigits(Array(NUMOF_FIGURE).fill(-1))

    /* CMJSound::LoadSFX("mjkslotstart") + PlaySFX 相当 */
    playMajakSfx('mjkslotstart')

    /* TIMER_LOT_SLOT_ROTATION 相当: lot_slot1 4フレーム回転 */
    spinTimer.current = setInterval(() => {
      setSpinFrame(frame => (frame + 1) % 4)
    }, 30)

    stopTimer.current = setTimeout(() => {
      let stopIndex = 0
      stopTimer.current = setInterval(() => {
        playMajakSfx('mjkslotstop')
        setDigits(current => current.map((digit, index) => (
          index === stopIndex ? resultDigits[index] : digit
        )))
        stopIndex++
        if (stopIndex >= NUMOF_FIGURE) {
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
  }, [lotCnt, lotteryCount, phase])

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
    if (remaining > 0) {
      const ok = await showConfirm(
        '残りカウント分は自動的に全回転STARTされます。\nよろしいですか？'
      )
      if (!ok) return
    } else {
      await showMessage('残りカウントがありませんので\n結果画面を表 示します。')
    }
    setShowResultDlg(true)
  }, [isSpinning, remaining])

  /** OnBtnResultClicked → CloseDlg(TRUE) 相当 */
  const handleResult = useCallback(() => {
    setPhase('done')
    setShowResultDlg(true)
  }, [])

  return (
    <div style={{
      position: 'absolute', inset: 0,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'rgba(0,0,0,0.45)', zIndex: 300,
    }}>
      {/* CMJLotSlotDlg クライアント領域: 624×222px */}
      <div style={{ position: 'relative', width: 624, height: 222 }}>

        {/* ================================================================
            背景: lot/lot_base1.png (624×222) at (0,0)
            Create(..., 1, ...) = 1フレーム単一画像
            ================================================================ */}
        <img
          src={`${IMG_LOT}/lot_base1.png`}
          alt=""
          draggable={false}
          style={{ position: 'absolute', left: 0, top: 0, width: 624, height: 222 }}
        />

        {/* アイテム画像: m_pItemImage->Draw(..., 33, 69, 0) */}
        {imageUrl && (
          <img
            src={imageUrl}
            alt={itemName}
            draggable={false}
            style={{
              position: 'absolute',
              left: 33, top: 69,
              objectFit: 'contain', pointerEvents: 'none',
            }}
          />
        )}

        <div style={{ position: 'absolute', left: 41, top: 53, width: 51, height: 13,
          fontFamily: "'MS PGothic', 'Noto Sans JP', 'MS UI Gothic', sans-serif", fontSize: 13,
          fontWeight: 'bold', color: 'rgb(6,65,2)', lineHeight: '13px', textAlign: 'center', overflow: 'hidden', pointerEvents: 'none' }}>
          {itemName}
        </div>
        <div style={{ position: 'absolute', left: 220, top: 28, width: 51, height: 13,
          fontFamily: "'MS PGothic', 'Noto Sans JP', 'MS UI Gothic', sans-serif", fontSize: 13,
          fontWeight: 'bold', color: 'rgb(6,65,2)', lineHeight: '13px', textAlign: 'right', pointerEvents: 'none' }}>
          {moneyString(totalAmount)}
        </div>
        <div style={{ position: 'absolute', left: 53, top: 116, width: 43, height: 15,
          fontFamily: "'MS PGothic', 'Noto Sans JP', 'MS UI Gothic', sans-serif", fontSize: 13,
          fontWeight: 'bold', color: '#fff', lineHeight: '15px', textAlign: 'right', pointerEvents: 'none' }}>
          {lotteryCount}回
        </div>
        <div style={{ position: 'absolute', left: 35, top: 184, width: 51, height: 13,
          fontFamily: "'MS PGothic', 'Noto Sans JP', 'MS UI Gothic', sans-serif", fontSize: 13,
          fontWeight: 'bold', color: 'rgb(6,65,2)', lineHeight: '13px', textAlign: 'right', pointerEvents: 'none' }}>
          {remaining}
        </div>
        <div style={{ position: 'absolute', left: 221, top: 184, width: 142, height: 13,
          fontFamily: "'MS PGothic', 'Noto Sans JP', 'MS UI Gothic', sans-serif", fontSize: 13,
          fontWeight: 'bold', color: 'rgb(6,65,2)', lineHeight: '13px', textAlign: 'right', pointerEvents: 'none' }}>
          {moneyString(amount)}
        </div>

        {/* ================================================================
            1回ボタン: lot/lot_t_btn_1.png (288×42, 4フレーム 72×42)
            m_btnOnce.Create(0, ..., 430, 161, ..., IDC_BTN_START)
            ================================================================ */}
        <SpriteButton
          src={`${IMG_LOT}/lot_t_btn_1.png`}
          frameW={72} frameH={42}
          x={430} y={161}
          onClick={handleOnce}
          disabled={isSpinning || isDone || remaining <= 0}
          title="1回"
        />

        {/* ================================================================
            全回 / 結果ボタン: 同座標 (510, 161) で状態により切り替え
            停止前: lot/lot_t_btn_2.png (全回)
            残り0回後: lot/lot_t_btn_5.png (結果表示)
            m_btnAll / m_btnResult.Create(0, ..., 510, 161, ..., IDC_BTN_RESULT)
            ================================================================ */}
        {showResult ? (
          <SpriteButton
            src={`${IMG_LOT}/lot_t_btn_5.png`}
            frameW={72} frameH={42}
            x={510} y={161}
            onClick={handleResult}
            title="結果"
          />
        ) : (
          <SpriteButton
            src={`${IMG_LOT}/lot_t_btn_2.png`}
            frameW={72} frameH={42}
            x={510} y={161}
            onClick={handleResult}
            disabled={isSpinning || isDone}
            title="全回"
          />
        )}

        {/* ================================================================
            数字リール: 3グループ配置 (REEL_X[] より絶対位置)
            グループ1: i=9,8  → X=137,170
            グループ2: i=7−4 → X=256,289,322,355
            グループ3: i=3−0 → X=441,474,507,540
            ================================================================ */}
        {digits.map((d, i) => (
          <div key={i} style={{ position: 'absolute', left: REEL_X[i], top: REEL_CORNER_POS_Y }}>
            <NumberReel digit={d} spinFrame={spinFrame} />
          </div>
        ))}
        {/* ================================================================
            閉じるボタン: lot/lot_btn_close.png (72×18, 4フレーム 18×18) at (599, 7)
            m_btnClose.Create(0, ..., 599, 7, ..., IDC_BTN_CLOSE)
            OnBtnCloseClicked → CloseDlg() 相当: 残りあれば確認ダイアログ
            ================================================================ */}
        <SpriteButton
          src={`${IMG_LOT}/lot_btn_close.png`}
          frameW={18} frameH={18}
          x={599} y={7}
          onClick={handleClose}
          disabled={isSpinning}
          title="閉じる"
        />
        {showResultDlg && (
          <LotResultDlg
            itemName={itemName}
            lotteryCount={lotteryCount}
            entries={resultEntries}
            totalAmount={resultTotal}
            nextLotteryCount={nextLotteryCount ?? lotteryCount}
            onBuyAgain={() => {
              setShowResultDlg(false)
              _onClose()
            }}
            onClose={() => onResult(resultTotal)}
          />
        )}
      </div>
    </div>
  )
}
