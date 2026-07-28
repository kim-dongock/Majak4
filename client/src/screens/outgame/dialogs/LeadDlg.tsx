/**
 * CMJLeadDlg 相当 — リード表示 (点数1位達成演出) (AP-09 §3-3-3)
 * レガシー: legacy/client/HgMajak2/MJLeadDlg.h/cpp
 *
 * ウィンドウ: MoveWindow(0,0,419,516) → 419×516px, CenterWindow(GetParent())
 *
 * CMJGetCoinDlg と同一パターン (7日間表示抑制 / チェックボックス付き)
 *
 * ── 画像配置 (OnCreate / OnPaint より — 座標すべてレガシー準拠) ─────────────
 *
 * 背景 (1フレーム 419×516):
 *   mj_pop_high_base.png  at (0, 0)
 *   内容テキスト / "一週間このウィンドウを開かない" ラベルは背景に焼き込み済み
 *
 * ×とじる ボタン (4フレーム 88×32):
 *   mj_shp_btn_close.png  at (166, 468)  IDOK → OnOK
 *
 * 「一週間このウィンドウを開かない」チェックボックス (4フレーム 14×14):
 *   mj_pop_check.png  at (300, 447)  IDC_CHECK_NOOPEN
 *   ラベルテキストは背景に焼き込み済み
 *
 * ── 動作ロジック (OnInitDialog / OnOK より) ──────────────────────────────
 * NeedsToShow(): localStorage "mj_leadDlg_{key}_ymd" で判定
 *   前回表示日から7日未満なら表示しない (GetDays 相当)
 * OnOK():
 *   チェックありの場合 → 今日の日付を localStorage に保存
 *   チェックなしの場合 → localStorage のキーを削除
 * ────────────────────────────────────────────────────────────────────────
 */
import { useRef, useState, useEffect } from 'react'

const IMG = '/assets/images/game'
const LS_PREFIX = 'mj_leadDlg_'

/** GetDays 相当: 日付から通算日数 */
function getDays(y: number, m: number, d: number): number {
  if (m <= 2) { --y; m += 12 }
  const c = Math.floor(y / 100)
  return 365 * (y - 1) + Math.floor(y / 4) - c + Math.floor(c / 4) +
         Math.floor((m * 979 - 1033) / 32) + d - 1
}

/** 前回表示から7日以内かチェック */
function isWithin7Days(storageKey: string): boolean {
  const stored = localStorage.getItem(LS_PREFIX + storageKey + '_ymd')
  if (!stored) return false
  const prev = Number(stored)
  const now = new Date()
  const today = getDays(now.getFullYear(), now.getMonth() + 1, now.getDate())
  const past  = getDays(Math.floor(prev / 10000), Math.floor((prev % 10000) / 100), prev % 100)
  return (today - past) < 7
}

function needsToShow(storageKey: string): boolean {
  return !isWithin7Days(storageKey)
}

interface Props {
  storageKey?: string
  onClose: () => void
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
      onMouseDown={(e) => { e.stopPropagation(); setFi(3) }}
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
 * mj_pop_check.png (56×14, 4フレーム 14×14) チェックボックス
 * ==================================================================== */
function CheckSprite({
  x, y, checked, onToggle,
}: {
  x: number; y: number; checked: boolean; onToggle: () => void
}) {
  const [pressed, setPressed] = useState(false)
  const frame = checked ? (pressed ? 3 : 1) : (pressed ? 2 : 0)
  return (
    <div
      onClick={onToggle}
      onMouseDown={(e) => { e.stopPropagation(); setPressed(true) }}
      onMouseUp={() => setPressed(false)}
      onMouseLeave={() => setPressed(false)}
      style={{
        position: 'absolute', left: x, top: y,
        width: 14, height: 14,
        backgroundImage: `url(${IMG}/mj_pop_check.png)`,
        backgroundPosition: `${-frame * 14}px 0`,
        backgroundRepeat: 'no-repeat',
        imageRendering: 'pixelated',
        cursor: 'pointer',
      }}
    />
  )
}

/** ====================================================================
 * CMJLeadDlg 本体
 * ==================================================================== */
export default function LeadDlg({ storageKey = 'default', onClose }: Props) {
  const [noOpen,  setNoOpen]  = useState(false)
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
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'transparent', zIndex: 400,
    }}>
      {/* CMJLeadDlg クライアント領域: 419×516px */}
      <div
        onMouseDown={onDragStart}
        style={{ position: 'relative', width: 419, height: 516, left: pos.x, top: pos.y }}
      >

        {/* ================================================================
            背景: mj_pop_high_base.png (419×516) at (0,0)
            Create(..., 1, ...) = 1フレーム単一画像
            内容テキスト / チェックラベルは焼き込み済み
            ================================================================ */}
        <img
          src={`${IMG}/mj_pop_high_base.png`}
          alt=""
          draggable={false}
          style={{
            position: 'absolute', left: 0, top: 0,
            width: 419, height: 516,
          }}
        />

        {/* ================================================================
            「一週間このウィンドウを開かない」チェック
            mj_pop_check.png (56×14, 4フレーム 14×14) at (300, 447)
            COwnerCheckBox m_ckNoOpen at CPoint(300, 447)
            ラベルは背景に焼き込み済み
            ================================================================ */}
        <CheckSprite
          x={300} y={447}
          checked={noOpen}
          onToggle={() => setNoOpen(v => !v)}
        />

        {/* ================================================================
            ×とじる: mj_shp_btn_close.png (352×32, 4フレーム 88×32) at (166,468)
            m_btnClose.Create(0, ..., 166, 468, ..., IDOK)
            ================================================================ */}
        <SpriteButton
          src={`${IMG}/mj_shp_btn_close.png`}
          frameW={88} frameH={32}
          x={166} y={468}
          onClick={handleClose}
          title="×とじる"
        />
      </div>
    </div>
  )
}

/** 外部参照用エクスポート */
export { needsToShow as leadDlgNeedsShow }
