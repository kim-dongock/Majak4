/**
 * CMJWelcomeDlg 相当 — ウェルカムメッセージ (AP-09 §3-1-3)
 * レガシー: legacy/client/HgMajak2/MJWelcomeDlg.h/cpp
 *
 * ウィンドウサイズ: 495×470px (MoveWindow(rc.left+263, rc.top+136, 495, 470))
 * 背景: web_welcome_01.png (489×438) at (0,0)
 * 閉じるボタン: mj_shp_btn_close.png (352×32 → 4フレーム 88×32) at (200, 401)
 *
 * OnNcHitTest → HTCAPTION: ドラッグ移動可能、システム X ボタンなし
 * ゲーム妨害防止: × ボタン以外での閉じ操作不可
 */
import { useRef, useState, useEffect } from 'react'

const IMG = '/assets/images/game'

interface Props {
  onClose: () => void
}

export default function WelcomeDlg({ onClose }: Props) {
  /** ドラッグ移動 (OnNcHitTest → HTCAPTION 相当) */
  const [pos, setPos]     = useState({ x: 263, y: 136 })
  const dragging          = useRef(false)
  const dragOffset        = useRef({ dx: 0, dy: 0 })

  const onDragStart = (e: React.MouseEvent) => {
    dragging.current = true
    dragOffset.current = { dx: e.clientX - pos.x, dy: e.clientY - pos.y }
    e.preventDefault()
  }

  useEffect(() => {
    const onMove = (e: MouseEvent) => {
      if (!dragging.current) return
      setPos({ x: e.clientX - dragOffset.current.dx, y: e.clientY - dragOffset.current.dy })
    }
    const onUp = () => { dragging.current = false }
    window.addEventListener('mousemove', onMove)
    window.addEventListener('mouseup',   onUp)
    return () => {
      window.removeEventListener('mousemove', onMove)
      window.removeEventListener('mouseup',   onUp)
    }
  }, [])

  /** ボタンスプライト */
  const [closeFrame, setCloseFrame] = useState(0)

  return (
    /* モーダルオーバーレイ (ゲーム妨害防止: オーバーレイクリック無効) */
    <div
      style={{
        position: 'absolute', inset: 0,
        background: 'rgba(0,0,0,0.45)',
        zIndex: 100,
        pointerEvents: 'none',  // オーバーレイ自体はクリック透過
      }}
    >
      {/* CMJWelcomeDlg ウィンドウ: 495×470px */}
      <div
        style={{
          position: 'absolute',
          left: pos.x, top: pos.y,
          width: 495, height: 470,
          pointerEvents: 'auto',
        }}
      >
        {/* ── 背景 web_welcome_01.png (489×438) ── */}
        <img
          src={`${IMG}/web_welcome_01.png`}
          alt=""
          draggable={false}
          onMouseDown={onDragStart}   /* OnNcHitTest HTCAPTION: タイトル領域ドラッグ */
          style={{
            position: 'absolute', left: 0, top: 0,
            width: 489, height: 438,
            cursor: 'move', userSelect: 'none',
          }}
        />

        {/* ── 閉じるボタン mj_shp_btn_close.png 88×32 at (200,401) ──
               m_btnClose.Create(0, m_pCloseButtonImage, ..., 200, 401, ..., IDOK)
               OnOK() → CDialog::OnOK() */}
        <button
          onClick={onClose}
          onMouseEnter={() => setCloseFrame(2)}
          onMouseLeave={() => setCloseFrame(0)}
          onMouseDown={() => setCloseFrame(3)}
          onMouseUp={() => setCloseFrame(2)}
          style={{
            position: 'absolute', left: 200, top: 401,
            width: 88, height: 32,
            backgroundImage: `url(${IMG}/mj_shp_btn_close.png)`,
            backgroundPosition: `${-closeFrame * 88}px 0`,
            backgroundRepeat: 'no-repeat',
            border: 'none', padding: 0, cursor: 'pointer',
            outline: 'none', imageRendering: 'pixelated',
          }}
        />
      </div>
    </div>
  )
}
