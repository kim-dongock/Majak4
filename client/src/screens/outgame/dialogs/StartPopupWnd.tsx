/**
 * CMJStartPopupWnd 相当 — スタートポップアップ (AP-09 §3-1-1)
 * レガシー: legacy/client/HgMajak2/MJStartPopupWnd.h/cpp
 *
 * ウィンドウサイズ: CLIENT_W=682, CLIENT_H2=394 px (MoveWindow)
 * 背景: mj_start_popup_background.png (682×394)
 * 閉じるボタン: mj_start_popup_close.png (96×24 → 4フレーム 24×24) at (656, 2)
 * チェックボックス: check.png (56×14 → 14×14 /frame) at (262, 370)
 *   CMJChkBtn.DrawItem 方式: チェック枠(14px) + テキスト "当日はこれ以上表示しない"
 *
 * NeedsToDisplayToday() → localStorage "mj_startPopupSkipDate" で判定
 * _MarkAsToSkipDisplayingToday() → OnDestroy 時に localStorage へ書込
 */
import { useState } from 'react'

const IMG = '/assets/images/game'
const LS_KEY = 'mj_startPopupSkipDate'
const BANNER_IMG_URL = import.meta.env.VITE_BANNER_IMG_URL as string | undefined

/** NeedsToDisplayToday() 相当 */
function needsToDisplayToday(): boolean {
  const stored = localStorage.getItem(LS_KEY)
  if (!stored) return true
  const today = new Date()
  const yyyymmdd = `${today.getFullYear()}${String(today.getMonth() + 1).padStart(2, '0')}${String(today.getDate()).padStart(2, '0')}`
  return stored !== yyyymmdd
}

/** _MarkAsToSkipDisplayingToday() 相当 */
function markSkipToday() {
  const today = new Date()
  const yyyymmdd = `${today.getFullYear()}${String(today.getMonth() + 1).padStart(2, '0')}${String(today.getDate()).padStart(2, '0')}`
  localStorage.setItem(LS_KEY, yyyymmdd)
}

/** ====================================================================
 * CMJBmpButton 相当 — AP-06 §2 4フレームスプライトボタン
 * ==================================================================== */
function SpriteButton({
  src, frameW, frameH, x, y, onClick,
}: {
  src: string; frameW: number; frameH: number
  x: number; y: number; onClick: () => void
}) {
  const [fi, setFi] = useState(0)
  return (
    <button
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

/** ====================================================================
 * CMJChkBtn 相当 — check.png (14×14 /frame) + テキスト
 * ==================================================================== */
function ChkBtn({
  x, y, w, h, label, checked, onToggle,
}: {
  x: number; y: number; w: number; h: number
  label: string; checked: boolean; onToggle: () => void
}) {
  const [pressed, setPressed] = useState(false)
  const nState = (pressed ? 1 : 0) | (checked ? 2 : 0)
  return (
    <div
      onClick={onToggle}
      onMouseDown={() => setPressed(true)}
      onMouseUp={() => setPressed(false)}
      onMouseLeave={() => setPressed(false)}
      style={{
        position: 'absolute', left: x, top: y,
        width: w, height: h,
        display: 'flex', alignItems: 'center',
        cursor: 'pointer', userSelect: 'none',
        /* 背景画像の古いテキスト ("当日はこれ以上表示しない") を緑背景で上書き
           OnCtlColor: CreateSolidBrush(RGB(74, 165, 57)) に準拠 */
        background: 'rgb(74,165,57)',
      }}
    >
      <div style={{
        width: 14, height: 14,
        backgroundImage: `url(${IMG}/check.png)`,
        backgroundPosition: `${-nState * 14}px 0`,
        backgroundRepeat: 'no-repeat', imageRendering: 'pixelated',
        flexShrink: 0,
      }} />
      <span style={{
        marginLeft: 4,
        fontFamily: 'var(--majak-font-family-ui)', fontSize: 'calc(11px * var(--majak-type-scale))',
        color: '#000', whiteSpace: 'nowrap',
      }}>
        {label}
      </span>
    </div>
  )
}

/** ====================================================================
 * CMJStartPopupWnd 本体
 * ==================================================================== */
interface Props {
  /** 閉じる時のコールバック */
  onClose: () => void
}

export default function StartPopupWnd({ onClose }: Props) {
  const [skipToday, setSkipToday] = useState(false)

  /** OnDestroy() 相当: 閉じる時にチェック状態で localStorage 書込 */
  const handleClose = () => {
    if (skipToday) markSkipToday()
    onClose()
  }



  return (
    /* モーダルオーバーレイ */
    <div
      className="majak-start-popup-overlay"
      style={{
        position: 'absolute', inset: 0,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        background: 'rgba(0,0,0,0.45)',
        zIndex: 100,
      }}
      onMouseDown={e => { if (e.target === e.currentTarget) handleClose() }}
    >
      {/* CMJStartPopupWnd クライアント領域: 682×394px */}
      <div className="majak-start-popup" style={{ position: 'relative', width: 682, height: 394, flexShrink: 0 }}>

        {/* ── 背景 mj_start_popup_background.png ── */}
        <img
          className="majak-start-popup__background"
          src={`${IMG}/mj_start_popup_background.png`}
          alt=""
          draggable={false}
          style={{ position: 'absolute', left: 0, top: 0, width: 682, height: 394 }}
        />

        {/* ── バナーエリア (12,31) 658×334 ──
             レガシー: CMJStartPopupWnd の BANNER_URL 相当
             HTML ページをそのまま iframe で表示する */}
        {BANNER_IMG_URL && (
          <iframe
            className="majak-start-popup__banner"
            src={BANNER_IMG_URL}
            title="お知らせ"
            style={{
              position: 'absolute', left: 12, top: 31,
              width: 658, height: 334,
              border: 'none',
            }}
            sandbox="allow-scripts allow-same-origin allow-popups"
          />
        )}

        {/* ── 当日は表示しないチェックボックス
               CMJChkBtn at CRect(262,370, 422,385) = 160×15
               テキスト: "今日はこれを表示しない" (MJStartPopupWnd.cpp 原典)
               背景色 RGB(74,165,57) で背景画像の古いテキストを上書き */}
        <ChkBtn
          x={262} y={370} w={160} h={15}
          label="今日はこれを表示しない"
          checked={skipToday}
          onToggle={() => setSkipToday(v => !v)}
        />

        {/* ── 閉じるボタン mj_start_popup_close.png 24×24 at (656,2) ── */}
        <SpriteButton
          src={`${IMG}/mj_start_popup_close.png`}
          frameW={24} frameH={24}
          x={656} y={2}
          onClick={handleClose}
        />
      </div>
    </div>
  )
}

/** needsToDisplayToday を外部からも参照できるようにエクスポート */
export { needsToDisplayToday }
