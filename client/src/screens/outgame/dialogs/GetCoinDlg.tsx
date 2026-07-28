/**
 * CMJGetCoinDlg 相当 — コイン獲得通知 (AP-09 §3-2-13)
 * レガシー: legacy/client/HgMajak2/MJGetCoinDlg.h/cpp
 *
 * ウィンドウ: MoveWindow(rc.left+146, rc.top+192, 508, 366) → 508×366px
 * (親ウィンドウの左上 + オフセット)
 *
 * 表示制御: 前回表示から7日以内は非表示 (registry → localStorage)
 *
 * ── 画像配置 (OnCreate / OnPaint より — 座標すべてレガシー準拠) ─────────────
 *
 * 背景 (1フレーム 508×366):
 *   mj_ive_window_07.png  at (0, 0)
 *   "無料麻雀コイン獲得！" タイトル, コイン画像, 説明テキスト,
 *   "一週間このウィンドウを開かない" テキスト は背景に焼き込み済み
 *
 * ×とじる ボタン (4フレーム 88×32):
 *   mj_shp_btn_close.png  at (210, 297)  IDOK → OnOK
 *   背景の "×とじる" 表示位置に重ねて配置
 *
 * 「一週間このウィンドウを開かない」チェックボックス (4フレーム 14×14):
 *   mj_pop_check.png  at (349, 338)  IDC_CHECK_NOOPEN
 *   背景のチェック欄位置に重ねて配置
 *   ※ チェックボックスラベルは背景に焼き込み済み
 *
 * ── 動作ロジック (OnInitDialog / OnOK より) ──────────────────────────────
 * NeedsToShowToday(): localStorage "mj_getCoinDlg_{key}_ymd" で判定
 *   前回表示日から7日未満なら表示しない (GetDays 相当)
 * OnOK():
 *   チェックありの場合 → 今日の日付を localStorage に保存
 *   チェックなしの場合 → localStorage のキーを削除
 * ────────────────────────────────────────────────────────────────────────
 */
import { useState, useEffect, useRef } from 'react'

const IMG = '/assets/images/game'

interface Props {
  /** m_szKey — localStorage のキー識別子 */
  storageKey?: string
  onClose: () => void
}

const LS_PREFIX = 'mj_getCoinDlg_'

/** GetDays 相当: 日付から通算日数を計算 (1年1月1日からの経過日) */
function getDays(y: number, m: number, d: number): number {
  if (m <= 2) { --y; m += 12 }
  const dy = 365 * (y - 1)
  const c  = Math.floor(y / 100)
  const dl = Math.floor(y / 4) - c + Math.floor(c / 4)
  const dm = Math.floor((m * 979 - 1033) / 32)
  return dy + dl + dm + d - 1
}

/** 前回表示から 7 日以内かチェック */
function isWithin7Days(storageKey: string): boolean {
  const lsKey = LS_PREFIX + storageKey + '_ymd'
  const stored = localStorage.getItem(lsKey)
  if (!stored) return false
  const prev = Number(stored)
  const py = Math.floor(prev / 10000)
  const pm = Math.floor((prev % 10000) / 100)
  const pd = prev % 100
  const now = new Date()
  const today = getDays(now.getFullYear(), now.getMonth() + 1, now.getDate())
  const past  = getDays(py, pm, pd)
  return (today - past) < 7
}

function needsToShow(storageKey: string): boolean {
  return !isWithin7Days(storageKey)
}

/** ====================================================================
 * CMJBmpButton 相当 — AP-06 §2 4フレームスプライトボタン
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
        border: 'none', padding: 0,
        cursor: 'pointer', outline: 'none',
        imageRendering: 'pixelated',
      }}
    />
  )
}

/** ====================================================================
 * CMJChkBtn 相当 — mj_pop_check.png (56×14, 4フレーム 14×14)
 * CMJGetCoinDlg では COwnerCheckBox (m_ckNoOpen) を使用
 * ==================================================================== */
function CheckSprite({
  x, y, checked, onToggle,
}: {
  x: number; y: number; checked: boolean; onToggle: () => void
}) {
  const [pressed, setPressed] = useState(false)
  const nState = (pressed ? 1 : 0) | (checked ? 2 : 0)
  return (
    <div
      onClick={onToggle}
      onMouseDown={() => setPressed(true)}
      onMouseUp={()   => setPressed(false)}
      onMouseLeave={() => setPressed(false)}
      style={{
        position: 'absolute', left: x, top: y,
        width: 14, height: 14,
        backgroundImage: `url(${IMG}/mj_pop_check.png)`,
        backgroundPosition: `${-nState * 14}px 0`,
        backgroundRepeat: 'no-repeat',
        imageRendering: 'pixelated',
        cursor: 'pointer',
      }}
    />
  )
}

/** ====================================================================
 * CMJGetCoinDlg 本体
 * ==================================================================== */
export default function GetCoinDlg({ storageKey = 'default', onClose }: Props) {
  const [noOpen, setNoOpen] = useState(false)
  const [visible, setVisible] = useState(false)
  const [pos, setPos] = useState({ x: 0, y: 0 })
  const dragging = useRef(false)
  const dragOffset = useRef({ dx: 0, dy: 0 })

  /* OnInitDialog 相当: 7日以内に表示済みなら即閉じる */
  useEffect(() => {
    if (!needsToShow(storageKey)) {
      onClose()
    } else {
      setVisible(true)
    }
  }, [storageKey, onClose])

  useEffect(() => {
    const onMove = (e: MouseEvent) => {
      if (!dragging.current) return
      setPos({ x: e.clientX - dragOffset.current.dx, y: e.clientY - dragOffset.current.dy })
    }
    const onUp = () => { dragging.current = false }
    window.addEventListener('mousemove', onMove)
    window.addEventListener('mouseup', onUp)
    return () => {
      window.removeEventListener('mousemove', onMove)
      window.removeEventListener('mouseup', onUp)
    }
  }, [])

  const onDragStart = (e: React.MouseEvent) => {
    dragging.current = true
    dragOffset.current = { dx: e.clientX - pos.x, dy: e.clientY - pos.y }
    e.preventDefault()
  }

  /** OnOK() 相当: チェック状態を localStorage に保存 */
  const handleClose = () => {
    const lsKey = LS_PREFIX + storageKey + '_ymd'
    if (noOpen) {
      const now = new Date()
      const ymd = now.getFullYear() * 10000 + (now.getMonth() + 1) * 100 + now.getDate()
      localStorage.setItem(lsKey, String(ymd))
    } else {
      localStorage.removeItem(lsKey)
    }
    onClose()
  }

  if (!visible) return null

  return (
    /* モーダルオーバーレイ */
    <div style={{
      position: 'absolute', inset: 0,
      background: 'transparent', zIndex: 300,
    }}>
      {/* CMJGetCoinDlg クライアント領域: 508×366px */}
      <div
        style={{ position: 'absolute', width: 508, height: 366, left: 146 + pos.x, top: 192 + pos.y }}
        onMouseDown={onDragStart}
      >

        {/* ================================================================
            背景: mj_ive_window_07.png (508×366) at (0,0)
            Create(..., 1, ...) = 1フレーム単一画像
            タイトル / コイン画像 / 説明テキスト / チェックラベルは焼き込み済み
            ================================================================ */}
        <img
          src={`${IMG}/mj_ive_window_07.png`}
          alt=""
          draggable={false}
          style={{
            position: 'absolute', left: 0, top: 0,
            width: 508, height: 366,
          }}
        />

        {/* ================================================================
            ×とじる: mj_shp_btn_close.png (352×32, 4フレーム 88×32) at (210,297)
            m_btnClose.Create(0, ..., 210, 297, ..., IDOK)
            背景の "×とじる" 表示位置に重ねて配置
            ================================================================ */}
        <SpriteButton
          src={`${IMG}/mj_shp_btn_close.png`}
          frameW={88} frameH={32}
          x={210} y={297}
          onClick={handleClose}
          title="×とじる"
        />

        {/* ================================================================
            「一週間このウィンドウを開かない」チェック
            mj_pop_check.png (56×14, 4フレーム 14×14) at (349, 338)
            COwnerCheckBox m_ckNoOpen at CPoint(349, 338)
            ラベルテキストは背景に焼き込み済み
            ================================================================ */}
        <CheckSprite
          x={349} y={338}
          checked={noOpen}
          onToggle={() => setNoOpen(v => !v)}
        />
      </div>
    </div>
  )
}

/** needsToShow を外部から参照できるようにエクスポート */
export { needsToShow as coinDlgNeedsShow }
