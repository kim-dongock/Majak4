/**
 * CMJSelGroupWnd 相当 — チャンネルグループ選択画面 (AP-09 §1-3)
 * レガシー: legacy/client/HgMajak2/MJSelGroupWnd.h/cpp
 *
 * ウィンドウサイズ: CRect(5,31,1019,735) → 1014×704px
 * 背景: mj_gs_bk.png (1014×704)
 * ボタン配置は MJSelGroupWnd::OnCreate() の Create() 呼び出しに準拠
 * テキスト配置は MJSelGroupWnd::OnPaint() の TextOut() 呼び出しに準拠
 *  (18px Bold MS UI Gothic, 白, 透過背景)
 *
 * OnPaint() の m_bShowStartPopup=TRUE → ShowStartPopupDialog() 相当:
 *  マウント時に StartPopupWnd.NeedsToDisplayToday() を確認して表示
 */
import { useNavigate } from 'react-router-dom'
import { useState, useEffect } from 'react'
import StartPopupWnd, { needsToDisplayToday } from './dialogs/StartPopupWnd'
import DrawMemberInfo from '../../components/DrawMemberInfo'
import * as SignalR from '../../api/signalr'
import { MAJAK_EXIT_REQUEST_EVENT } from '../../components/MajakFrame'
import { useOutgameLayoutMode } from '../../hooks/useOutgameLayoutMode'
import { useAuthStore } from '../../store/authStore'
import { useGamePlayerStore } from '../../store/gamePlayerStore'

const IMG = '/assets/images/game'
const MOBILE_MAIN_VISUAL_SRC = 'https://images.hange.jp/hangame/easy/majak4/client/bnr/top_majak_20220329.png'

/** ====================================================================
 * CMJBmpButton 相当 — AP-06 §2 4フレームスプライトボタン
 *   フレーム1=normal / フレーム2=disabled / フレーム3=hover / フレーム4=pressed
 * ==================================================================== */
function SpriteButton({
  src,
  frameW,
  frameH,
  x,
  y,
  onClick,
  title,
}: {
  src: string
  frameW: number
  frameH: number
  x: number
  y: number
  onClick: () => void
  title?: string
}) {
  const [frameIdx, setFrameIdx] = useState(0) // 0=normal,1=disabled,2=hover,3=pressed

  return (
    <button
      title={title}
      onClick={onClick}
      onMouseEnter={() => setFrameIdx(2)}
      onMouseLeave={() => setFrameIdx(0)}
      onMouseDown={() => setFrameIdx(3)}
      onMouseUp={() => setFrameIdx(2)}
      style={{
        position: 'absolute',
        left: x,
        top: y,
        width: frameW,
        height: frameH,
        backgroundImage: `url(${src})`,
        backgroundPosition: `${-frameIdx * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        border: 'none',
        padding: 0,
        cursor: 'pointer',
        outline: 'none',
        imageRendering: 'pixelated',
      }}
    />
  )
}

function MobileSpriteButton({
  src,
  label,
  frameW,
  frameH,
  onClick,
}: {
  src: string
  label: string
  frameW: number
  frameH: number
  onClick: () => void
}) {
  const [frameIdx, setFrameIdx] = useState(0)
  return (
    <button
      type="button"
      className="majak-mobile-sprite-button"
      onClick={onClick}
      onMouseEnter={() => setFrameIdx(2)}
      onMouseLeave={() => setFrameIdx(0)}
      onMouseDown={() => setFrameIdx(3)}
      onMouseUp={() => setFrameIdx(2)}
      aria-label={label}
      style={{
        width: `var(--majak-mobile-sprite-w, ${frameW}px)`,
        height: `var(--majak-mobile-sprite-h, ${frameH}px)`,
        backgroundImage: `url(${src})`,
        backgroundPosition: `calc(var(--majak-mobile-sprite-w, ${frameW}px) * ${-frameIdx}) 0`,
        backgroundSize: `calc(var(--majak-mobile-sprite-w, ${frameW}px) * 4) var(--majak-mobile-sprite-h, ${frameH}px)`,
      }}
    />
  )
}

/** ====================================================================
 * CMJSelGroupWnd 本体
 * ==================================================================== */

/** OnPaint() の tblStrSelGrpBtnInfo[] テキスト (SJIS デコード済み) */
const descriptions: [number, number, string][] = [
  [370, 210, 'ルール、対戦相手、レートを選んで対戦！'],    // 交流戦
  [370, 300, '基本ルールで実力を競え！'],                  // 段位戦
  [370, 390, '公式大会の参加はこちら！詳細は公式お知らせにて'], // 大会
  [370, 480, '牌譜を見直して雀力を高めよう！'],            // 牌譜
]

export default function ChannelGroupScreen() {
  const navigate = useNavigate()
  const layoutMode = useOutgameLayoutMode()
  const player = useAuthStore(state => state.player)
  const { data: gpData, fetchProfile } = useGamePlayerStore()
  const gameMoneyText = typeof gpData?.gamMoney === 'number' && Number.isFinite(gpData.gamMoney)
    ? gpData.gamMoney.toLocaleString('ja-JP')
    : ''

  /** OnPaint(): m_bShowStartPopup=TRUE → ShowStartPopupDialog() 相当 */
  const [showStartPopup, setShowStartPopup] = useState(false)

  useEffect(() => {
    SignalR.disconnect().catch(() => {})
    if (needsToDisplayToday()) setShowStartPopup(true)
  }, [])

  useEffect(() => {
    if (player?.pix) {
      fetchProfile(player.pix)
    }
  }, [player?.pix, fetchProfile])

  useEffect(() => {
    if (!player?.pix) return

    const refreshProfile = () => fetchProfile(player.pix)
    const refreshWhenVisible = () => {
      if (document.visibilityState === 'visible') refreshProfile()
    }

    window.addEventListener('focus', refreshProfile)
    document.addEventListener('visibilitychange', refreshWhenVisible)
    return () => {
      window.removeEventListener('focus', refreshProfile)
      document.removeEventListener('visibilitychange', refreshWhenVisible)
    }
  }, [player?.pix, fetchProfile])

  /** OnCommand() → GetParent()->SendMessage(WM_COMMAND) に相当するナビゲーション */
  const onKouryu  = () => navigate('/channel/select/kouryu')   // IDC_BTN_CATEGORY_KOURYU → EnterCustom(IDC_CHK_STAND)
  const onDani    = () => navigate('/channel/select/dani')     // IDC_BTN_CATEGORY_DANI   → EnterCustom(IDC_CHK_DANI)
  const onTaikai  = () => navigate('/channel/00H8A/lobby')     // IDC_BTN_CATEGORY_TAIKAI → EnterQuick(TOURNAMENT_SUBID)
  const onPaifu   = () => navigate('/channel/00V0A/lobby')     // IDC_BTN_CATEGORY_PAIFU  → EnterQuick("00V0A")
  const onExit    = () => window.dispatchEvent(new Event(MAJAK_EXIT_REQUEST_EVENT)) // IDC_SETTING_BTN_EXT

  if (layoutMode === 'mobileLandscape') {
    const items = [
      { title: '交流戦', src: `${IMG}/mj_top_btn_01.png`, description: descriptions[0][2], onClick: onKouryu },
      { title: '段位戦', src: `${IMG}/mj_top_btn_02.png`, description: descriptions[1][2], onClick: onDani },
      { title: '大会', src: `${IMG}/mj_top_btn_03.png`, description: descriptions[2][2], onClick: onTaikai },
      { title: '牌譜再生', src: `${IMG}/mj_top_btn_05.png`, description: descriptions[3][2], onClick: onPaifu },
    ]

    return (
      <div className="majak-mobile-screen majak-mobile-channel-group">
        <section className="majak-mobile-hero">
          <div className="majak-mobile-hero__visual">
            <img className="majak-mobile-logo" src={MOBILE_MAIN_VISUAL_SRC} alt="麻雀4" draggable={false} />
          </div>
          {player && (
            <div className="majak-mobile-member-info">
              <span className="majak-mobile-member-info__id">{player.name}</span>
              <span>GP : {gameMoneyText} GP</span>
              <span>資産 : {gpData ? gpData.slevel : ''}</span>
            </div>
          )}
        </section>
        <div className="majak-mobile-card-list majak-mobile-sprite-list">
          {items.map(item => (
            <div key={item.title} className="majak-mobile-sprite-entry">
              <MobileSpriteButton src={item.src} label={item.title} frameW={244} frameH={65} onClick={item.onClick} />
              <span className="majak-mobile-sprite-description">{item.description}</span>
            </div>
          ))}
        </div>
        {showStartPopup && (
          <StartPopupWnd onClose={() => setShowStartPopup(false)} />
        )}
      </div>
    )
  }

  return (
    /* CMJSelGroupWnd クライアント領域: 1014×704px */
    <div style={{ position: 'relative', width: 1014, height: 704, overflow: 'hidden' }}>

      {/* ── 背景 BitBlt(0,0, m_dibBack) ── */}
      <img
        src={`${IMG}/mj_gs_bk.png`}
        alt=""
        draggable={false}
        style={{ position: 'absolute', left: 0, top: 0, width: 1014, height: 704 }}
      />

      {/* ── ロビー説明テキスト TextOut(x,y, ...) / FW_BOLD 18px MS UI Gothic 白 ── */}
      {descriptions.map(([tx, ty, text]) => (
        <span
          key={ty}
          style={{
            position: 'absolute',
            left: tx,
            top: ty,
            fontFamily: 'var(--majak-font-family-ui)',
            fontSize: 'calc(18px * var(--majak-type-scale))',
            fontWeight: 'bold',
            color: '#ffffff',
            whiteSpace: 'nowrap',
            pointerEvents: 'none',
          }}
        >
          {text}
        </span>
      ))}

      {/* ── 交流戦ボタン — IDC_BTN_CATEGORY_KOURYU (86,191) mj_top_btn_01.png 244×65 ── */}
      <SpriteButton
        src={`${IMG}/mj_top_btn_01.png`}
        frameW={244} frameH={65}
        x={86} y={191}
        onClick={onKouryu}
        title="交流戦"
      />

      {/* ── 段位戦ボタン — IDC_BTN_CATEGORY_DANI (86,281) mj_top_btn_02.png 244×65 ── */}
      <SpriteButton
        src={`${IMG}/mj_top_btn_02.png`}
        frameW={244} frameH={65}
        x={86} y={281}
        onClick={onDani}
        title="段位戦"
      />

      {/* ── 大会ボタン — IDC_BTN_CATEGORY_TAIKAI (86,371) mj_top_btn_03.png 244×65 ── */}
      <SpriteButton
        src={`${IMG}/mj_top_btn_03.png`}
        frameW={244} frameH={65}
        x={86} y={371}
        onClick={onTaikai}
        title="大会"
      />

      {/* ── 牌譜ボタン — IDC_BTN_CATEGORY_PAIFU (86,461) mj_top_btn_05.png 244×65 ── */}
      <SpriteButton
        src={`${IMG}/mj_top_btn_05.png`}
        frameW={244} frameH={65}
        x={86} y={461}
        onClick={onPaifu}
        title="牌譜再生"
      />

      {/* ── 終了ボタン — IDC_SETTING_BTN_EXT (821,625) mj_btn_exit.png 164×55 ── */}
      <SpriteButton
        src={`${IMG}/mj_btn_exit.png`}
        frameW={164} frameH={55}
        x={821} y={625}
        onClick={onExit}
        title="終了"
      />

      {/* ── DrawMemberInfo 相当: CMJSelGroupWnd::OnPaint() → DrawMemberInfo() ── */}
      <DrawMemberInfo />

      {/* ── CMJStartPopupWnd: ログイン直後に1回表示 (m_bShowStartPopup) ── */}
      {showStartPopup && (
        <StartPopupWnd onClose={() => setShowStartPopup(false)} />
      )}
    </div>
  )
}

