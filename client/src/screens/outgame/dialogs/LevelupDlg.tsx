/**
 * CMJLevelupDlg 相当 — 称号上昇ダイアログ (AP-09 §3-3-2)
 * レガシー: legacy/client/HgMajak2/MJLevelupDlg.h/cpp
 *
 * ウィンドウ: MoveWindow(0,0,383,321) → 383×321px, CenterWindow(GetParent())
 * OnNcHitTest: 常に HTCAPTION (全体ドラッグ可能)
 *
 * ── 画像配置 (OnCreate / OnPaint より — 座標すべてレガシー準拠) ─────────────
 *
 * 背景 (1フレーム 383×321):
 *   mj_sho_window.png  at (0, 0)
 *   以下のテキストは背景に焼き込み済み:
 *     "称号上昇！", "おめでとうございます！", "に上昇しました。",
 *     "以下のマネーが保険金として積み立てられました。"
 *     "麻雀マネー：" / "円"
 *     "積み立てられた保険金は、麻雀マネーが0円になった時、自動補充されます。"
 *
 * 称号文字 (12フレーム 90×30):
 *   mj_sho_moji.png  at (147, 112)  frame = m_nLevel (0〜11)
 *   m_p_imgStrShogo->Draw(&dc, 147, 112, m_nLevel)
 *   フレーム対応: 0=無一文 / 1=ぴよぴよ / 2=金欠 / 3=庶民 / 4=中流 /
 *                5=上流   / 6=富豪     / 7=大富豪 / 8=貴族 / 9=大臣 /
 *                10=王様  / 11=大王様
 *
 * OK ボタン (4フレーム 88×32):
 *   mj_shp_btn_ok.png  at (148, 277)  IDOK
 *
 * ── テキスト (OnPaint — 14px bold MS Pゴシック) ─────────────────────────
 *   m_strLentMoney (積立保険金額):
 *     CRect(161, 205, 309, 219) DT_RIGHT|DT_SINGLELINE|DT_VCENTER
 *     "{amount}円" 相当
 * ────────────────────────────────────────────────────────────────────────
 */
import { useRef, useEffect, useState } from 'react'
import { playMajakSfx } from '../../../utils/majakSound'

const IMG = '/assets/images/game'
const FONT = 'var(--majak-font-family-ui)'

/** mj_sho_moji.png 12フレーム対応称号名 */
const LEVEL_NAMES = [
  '無一文',   // frame 0
  'ぴよぴよ', // frame 1
  '金欠',     // frame 2
  '庶民',     // frame 3
  '中流',     // frame 4
  '上流',     // frame 5
  '富豪',     // frame 6
  '大富豪',   // frame 7
  '貴族',     // frame 8
  '大臣',     // frame 9
  '王様',     // frame 10
  '大王様',   // frame 11
] as const

interface Props {
  /** m_nLevel: 0〜11 (mj_sho_moji.png フレーム番号) */
  level: number
  /** m_strLentMoney: 積立保険金額 (円) */
  lentMoney: number
  onClose: () => void
}

function formatLentMoney(value: number): string {
  return new Intl.NumberFormat('ja-JP', {
    useGrouping: true,
    maximumFractionDigits: 0,
  }).format(Math.trunc(value))
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
 * CMJLevelupDlg 本体
 * ==================================================================== */
export default function LevelupDlg({ level, lentMoney, onClose }: Props) {
  /* OnNcHitTest: 常に HTCAPTION → 全体ドラッグ */
  const [pos, setPos]   = useState({ x: 0, y: 0 })
  const dragging        = useRef(false)
  const dragOffset      = useRef({ dx: 0, dy: 0 })

  /** CMJSound::LoadSFX + PlaySFX(SID_ALLCNT) 相当 — レベルアップ SE 再生 */
  useEffect(() => {
    const audio = playMajakSfx('mjklevelup1')
    return () => { audio?.pause() }
  }, [])

  const onDragStart = (e: React.MouseEvent) => {
    dragging.current   = true
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

  /** nLevel クランプ (0〜11) */
  const nLevel = Math.min(Math.max(0, level), LEVEL_NAMES.length - 1)

  return (
    /* モーダルオーバーレイ */
    <div style={{
      position: 'absolute', inset: 0,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'rgba(0,0,0,0.45)', zIndex: 400,
    }}>
      {/* CMJLevelupDlg クライアント領域: 383×321px */}
      <div style={{
        position: 'relative',
        width: 383, height: 321,
        left: pos.x, top: pos.y,
      }}>

        {/* ================================================================
            背景: mj_sho_window.png (383×321) at (0,0)
            Create(..., 1, ...) = 1フレーム単一画像
            全テキスト / レイアウトは背景に焼き込み済み
            OnNcHitTest = HTCAPTION → 全体でドラッグ可
            ================================================================ */}
        <img
          src={`${IMG}/mj_sho_window.png`}
          alt=""
          draggable={false}
          onMouseDown={onDragStart}
          style={{
            position: 'absolute', left: 0, top: 0,
            width: 383, height: 321,
            cursor: 'move', userSelect: 'none',
          }}
        />

        {/* ================================================================
            称号文字: mj_sho_moji.png (1080×30, 12フレーム 90×30)
            m_p_imgStrShogo->Draw(&dc, 147, 112, m_nLevel)
            frame = m_nLevel (0〜11)
            ================================================================ */}
        <div
          title={LEVEL_NAMES[nLevel]}
          style={{
            position: 'absolute',
            left: 147, top: 112,
            width: 90, height: 30,
            backgroundImage: `url(${IMG}/mj_sho_moji.png)`,
            backgroundPosition: `${-nLevel * 90}px 0`,
            backgroundRepeat: 'no-repeat',
            imageRendering: 'pixelated',
            pointerEvents: 'none',
          }}
        />

        {/* ================================================================
            積立保険金額テキスト (OnPaint)
            CRect(161, 205, 309, 219) DT_RIGHT|DT_SINGLELINE|DT_VCENTER
            14px bold MS Pゴシック 黒
            ================================================================ */}
        <div style={{
          position: 'absolute',
          left: 161, top: 205,
          width: 148,   /* 309 - 161 = 148 */
          height: 14,   /* 219 - 205 = 14 */
          fontFamily: FONT,
          fontSize: 'calc(14px * var(--majak-type-scale))', fontWeight: 'bold', color: '#000',
          textAlign: 'right',
          lineHeight: '14px',
          overflow: 'hidden', whiteSpace: 'nowrap',
          pointerEvents: 'none',
        }}>
          {formatLentMoney(lentMoney)}
        </div>

        {/* ================================================================
            OK: mj_shp_btn_ok.png (352×32, 4フレーム 88×32) at (148,277)
            m_btnClose.Create(0, ..., 148, 277, ..., IDOK)
            ================================================================ */}
        <SpriteButton
          src={`${IMG}/mj_shp_btn_ok.png`}
          frameW={88} frameH={32}
          x={148} y={277}
          onClick={onClose}
          title="OK"
        />
      </div>
    </div>
  )
}
