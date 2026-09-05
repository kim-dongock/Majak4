/**
 * CMajakFrame 相当 — ゲームウィンドウ共通タイトルバー (AP-09 §1-2)
 * レガシー:
 *   背景   : MJDrawFrame.cpp::Init() → m_Title.Create("mj_ttlhgc1024.him")
 *            Draw() → m_Title.Draw(pDC, 0, 0)  (1024×31px, ロゴ・麻雀4タイトル込み)
 *   最小化 : MajakChannelWnd.cpp/MJRoomWnd1.cpp MINBOX_CREATE("mj_minbox.him", x=947, y=4)
 *   最大化 : MajakChannelWnd.cpp/MJRoomWnd1.cpp MAXBOX_CREATE("mj_maxbox.him", "mj_rstbox.him", x=970, y=4)
 *   閉じる : MajakChannelWnd.cpp/MJRoomWnd1.cpp EXITBOX_CREATE("mj_clsbox.him", x=993, y=4)
 *   設定   : MajakChannelWnd.cpp SNDBOX_CREATE("mj_sndbox.him", x=901, y=4)
 *   通報   : MajakChannelWnd.cpp ACCBOX_CREATE("mj_accbox.him", x=817, y=4)
 *            MJRoomWnd1.cpp       ACCBOX_CREATE("mj_accbox.him", x=733, y=4)
 *   ルーム : MJRoomWnd1.cpp CAPBOX_CREATE("mj_capbox.him", x=775, y=4)
 *            MJRoomWnd1.cpp BNSBOX_CREATE("mj_banbox.him", x=817, y=4)
 *
 * 設定ボタン: onOpenSettings コールバック (省略可)
 *   省略時は CMJCfgDlg (CfgDlg) を内部で開く
 *   レガシー: CMajakChannelWnd::OnSoundOptionIconBoxClicked()
 *     → m_MajakGame.OnGameConf() → CMJCfgDlg::DoModal()
 */
import { useEffect, useState } from 'react'
import { Capacitor } from '@capacitor/core'
import { useLocation, useNavigate } from 'react-router-dom'
import CfgDlg, { loadMajakConfig, saveMajakConfig, type MJConfig } from '../screens/outgame/dialogs/CfgDlg'
import { configureMajakSound } from '../utils/majakSound'
import { useCustomSkinStore } from '../store/customSkinStore'
import { getLegacyFullUiSkinId } from '../utils/legacySkinPalette'
import { useOutgameLayoutMode } from '../hooks/useOutgameLayoutMode'
import { useDesktopScreenScale } from '../hooks/useDesktopScreenScale'
import { showMessage } from '../utils/msgbox'
import { saveLargestCanvasScreenshot } from '../utils/screenshot'
import { isHangeClientHost, logout, saveRegisteredPlayerCache } from '../api/auth'
import * as SignalR from '../api/signalr'
import { useAuthStore } from '../store/authStore'
import { useGamePlayerStore } from '../store/gamePlayerStore'
import EndingPopupWnd from '../screens/outgame/dialogs/EndingPopupWnd'
import ProfileEditDlg from '../screens/outgame/dialogs/ProfileEditDlg'

const MAJAK3 = '/assets/images/game'
export const MAJAK_ACCUSE_EVENT = 'majak:accuse-click'
export const MAJAK_EXIT_REQUEST_EVENT = 'majak:exit-request'
export const MAJAK_LOBBY_CHANGE_REQUEST_EVENT = 'majak:lobby-change-request'
const IS_NATIVE_APP = Capacitor.isNativePlatform()

type LockableScreenOrientation = ScreenOrientation & {
  lock?: (orientation: 'landscape') => Promise<void>
}

function FramePlayerBalances({ name, mp, gp }: { name: string; mp?: number; gp?: number }) {
  const [expanded, setExpanded] = useState(false)
  const amount = (value?: number) => typeof value === 'number' && Number.isFinite(value)
    ? value.toLocaleString('ja-JP')
    : '-'

  return (
    <div className={`majak-frame-player-balances${expanded ? ' is-expanded' : ''}`} aria-label="プレイヤー残高">
      <span className="majak-frame-player-balances__name"><b>ニックネーム</b><em title={name}>{name}</em></span>
      <span className="majak-frame-player-balances__detail majak-frame-player-balances__mp"><b>MP</b><em>{amount(mp)}</em></span>
      <span className="majak-frame-player-balances__detail majak-frame-player-balances__gp"><b>GP</b><em>{amount(gp)}</em></span>
      <button
        type="button"
        className="majak-frame-player-balances__toggle"
        aria-label={expanded ? '残高情報を閉じる' : '残高情報を開く'}
        aria-expanded={expanded}
        onClick={() => setExpanded(value => !value)}
      >
        <span aria-hidden="true" />
      </button>
    </div>
  )
}

// ── スプライトボタン (4フレーム: normal/disabled/hover/pressed) ──────
function TitleBtn({
  src, fallbackSrc, frameW, frameH, x, y, onClick, title,
}: {
  src: string; fallbackSrc?: string; frameW: number; frameH: number
  x: number; y: number; onClick: () => void; title?: string
}) {
  const [fi, setFi] = useState(0)
  const backgroundImage = fallbackSrc ? `url(${src}), url(${fallbackSrc})` : `url(${src})`
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
        backgroundImage,
        backgroundPosition: `${-fi * frameW}px 0`,
        backgroundRepeat: 'no-repeat',
        backgroundColor: 'transparent',   // shorthand "background" は NG (backgroundImage を上書きするため)
        border: 'none', padding: 0, cursor: 'pointer', outline: 'none',
        imageRendering: 'pixelated',
      }}
    />
  )
}

interface MajakFrameProps {
  /** 設定ボタンクリック時のコールバック (省略可) */
  onOpenSettings?: () => void
  /** お知らせ画面を開くコールバック */
  onOpenAnnouncements?: () => void
  /** タイトル画面へ戻るコールバック */
  onGoHome?: () => void
  /** 通報ボタン表示位置。レガシーで ACCBOX_CREATE がある画面だけ指定する */
  accBox?: 'channel' | 'room'
  children: React.ReactNode
}

export default function MajakFrame({ onOpenSettings, onOpenAnnouncements, onGoHome, accBox, children }: MajakFrameProps) {
  const location = useLocation()
  const navigate = useNavigate()
  /** CMJCfgDlg 内部管理 — onOpenSettings 未指定時に使用 */
  const [showCfg, setShowCfg] = useState(false)
  const [cfg, setCfg]         = useState<MJConfig>(() => loadMajakConfig())
  const [isFullScreen, setIsFullScreen] = useState(() => Boolean(document.fullscreenElement))
  const [showExitConfirm, setShowExitConfirm] = useState(false)
  const [showProfile, setShowProfile] = useState(false)
  const player = useAuthStore(state => state.player)
  const setPlayer = useAuthStore(state => state.setPlayer)
  const gamePlayer = useGamePlayerStore(state => state.data)
  const fetchGamePlayer = useGamePlayerStore(state => state.fetchProfile)
  const isHangeClient = isHangeClientHost()
  const isTitleScreen = location.pathname === '/channel'
  const isAnnouncementScreen = location.pathname === '/announcements'
  const isLobbySelectScreen = location.pathname.startsWith('/channel/select/')
    || /^\/channel\/[^/]+$/.test(location.pathname)
  const showFramePlayerBalances = isTitleScreen || isLobbySelectScreen
  const isLobbyListScreen = /\/channel\/[^/]+\/lobby$/.test(location.pathname)
  const isPaifuArchiveScreen = location.pathname === '/paifu'
  const screenTitle = isAnnouncementScreen
    ? 'お知らせ'
    : isLobbySelectScreen
      ? 'ロビー選択'
      : isPaifuArchiveScreen
        ? '牌譜検索'
        : '麻雀4'
  const frameBack = onGoHome ?? (isLobbySelectScreen || isPaifuArchiveScreen ? () => navigate('/channel') : undefined)
  const frameWidth = accBox === 'room' ? 1024 : 1014
  const frameHeight = accBox === 'room' ? 735 : undefined
  const isResponsiveDesktopScreen = location.pathname === '/channel'
    || location.pathname === '/announcements'
    || location.pathname === '/paifu'
    || location.pathname.startsWith('/channel/select/')
    || /\/channel\/[^/]+\/lobby$/.test(location.pathname)
    || /\/channel\/[^/]+\/lobby\/room\/[^/]+$/.test(location.pathname)
  const desktopFrameWidth = isResponsiveDesktopScreen
    ? (isFullScreen ? '100vw' : 'min(1320px, calc(100vw - 48px))')
    : frameWidth
  const desktopFrameHeight = isResponsiveDesktopScreen
    ? '100dvh'
    : frameHeight
  const routeState = (location.state ?? {}) as { customBgId?: number; customBoardType?: number }
  const fallbackSkin = useCustomSkinStore()
  const routeCustomBoardId = Number(routeState.customBgId ?? 0)
  const routeCustomBoardType = Number(routeState.customBoardType ?? 0)
  const customBoardId = routeCustomBoardId > 0 ? routeCustomBoardId : fallbackSkin.bgId
  const customBoardType = routeCustomBoardType > 0 ? routeCustomBoardType : fallbackSkin.bgType
  const roomFullUiSkinId = accBox === 'room' ? getLegacyFullUiSkinId(customBoardId, customBoardType) : undefined
  const useRoomBoardSkin = roomFullUiSkinId != null
  const roomFullUiSkinSuffix = String(roomFullUiSkinId ?? customBoardId).padStart(2, '0')
  const roomSkinSrc = (key: string) => useRoomBoardSkin
    ? `${MAJAK3}/skin/${roomFullUiSkinId}/${key}_${roomFullUiSkinSuffix}.png`
    : `${MAJAK3}/${key}.png`
  const roomSkinFallbackSrc = (key: string) => useRoomBoardSkin ? `${MAJAK3}/${key}.png` : undefined
  const titleSrc = useRoomBoardSkin ? roomSkinSrc('mj_ttlhgc1024') : `${MAJAK3}/mj_ttlhgc1024.png`
  const borderSrc = roomSkinSrc('mj_border')
  const borderFallbackSrc = roomSkinFallbackSrc('mj_border')
  const borderBackgroundImage = borderFallbackSrc ? `url(${borderSrc}), url(${borderFallbackSrc})` : `url(${borderSrc})`
  const frameChromeColor = useRoomBoardSkin ? '#151515' : '#2c9827'
  const frameChromeShadow = useRoomBoardSkin ? '#333333' : '#147d1f'
  const frameBottomColor = useRoomBoardSkin ? '#e8e8e8' : frameChromeColor
  const layoutMode = useOutgameLayoutMode()
  const desktopScale = useDesktopScreenScale(layoutMode === 'desktop' && !isResponsiveDesktopScreen)

  /** OnSoundOptionIconBoxClicked 相当 */
  const handleOpenSettings = onOpenSettings ?? (() => setShowCfg(true))

  useEffect(() => {
    configureMajakSound(cfg)
  }, [cfg])

  useEffect(() => {
    if (!showFramePlayerBalances || !player?.pix) return
    void fetchGamePlayer(player.pix)
  }, [fetchGamePlayer, player?.pix, showFramePlayerBalances])

  useEffect(() => {
    const onFullScreenChange = () => setIsFullScreen(Boolean(document.fullscreenElement))
    document.addEventListener('fullscreenchange', onFullScreenChange)
    return () => document.removeEventListener('fullscreenchange', onFullScreenChange)
  }, [])

  useEffect(() => {
    const requestExit = () => setShowExitConfirm(true)
    window.addEventListener(MAJAK_EXIT_REQUEST_EVENT, requestExit)
    return () => window.removeEventListener(MAJAK_EXIT_REQUEST_EVENT, requestExit)
  }, [])

  const handleMinimize = () => {
    window.blur()
  }

  const enterFullscreen = () => {
    if (document.fullscreenElement || !document.fullscreenEnabled) return
    void document.documentElement.requestFullscreen()
      .then(() => (window.screen.orientation as LockableScreenOrientation | undefined)?.lock?.('landscape'))
      .catch(() => {})
  }

  const handleMaximize = () => {
    if (document.fullscreenElement) {
      document.exitFullscreen().catch(() => {})
      return
    }
    enterFullscreen()
  }

  const handleClose = () => {
    setShowExitConfirm(true)
  }

  const handleChangeLobby = () => {
    window.dispatchEvent(new Event(MAJAK_LOBBY_CHANGE_REQUEST_EVENT))
  }

  const handleExitConfirm = async () => {
    setShowExitConfirm(false)
    await logout()
    await SignalR.disconnect().catch(() => {})
    useGamePlayerStore.getState().clearData()
    useAuthStore.getState().requireLogin()
  }

  const handleAccuse = () => {
    window.dispatchEvent(new CustomEvent(MAJAK_ACCUSE_EVENT))
  }

  const handleBanish = () => {}

  const handleCapture = () => {
    void saveLargestCanvasScreenshot({ filenamePrefix: 'majak-room' }).catch(() => {
      void showMessage('保存できるゲーム画面がありません。', '画面')
    })
  }

  const profileDialog = showProfile && player ? (
    <ProfileEditDlg
      player={player}
      onClose={() => setShowProfile(false)}
      onComplete={updatedPlayer => {
        setPlayer(updatedPlayer)
        saveRegisteredPlayerCache(updatedPlayer)
        setShowProfile(false)
      }}
    />
  ) : null

  if (layoutMode !== 'desktop') {
    const isLobbyScreen = /\/channel\/[^/]+\/lobby$/.test(location.pathname)
    const showMobileHeader = accBox !== 'room' && !isLobbyScreen
    const usesScreenHeader = isAnnouncementScreen || isLobbySelectScreen || isPaifuArchiveScreen
    const showMobileExit = location.pathname === '/channel'
      || location.pathname.startsWith('/channel/select/')
      || isLobbyScreen

    if (layoutMode === 'mobilePortrait') {
      return (
        <main className="majak-mobile-portrait-notice majak-screen-surface" aria-live="polite">
          <img
            className="majak-mobile-portrait-notice__logo"
            src="/assets/images/common/ico_big_majak4.jpg"
            alt="麻雀4"
            draggable={false}
          />
          <div className="majak-mobile-portrait-notice__device" aria-hidden="true">
            <span />
          </div>
          <h1>端末を横向きにしてください</h1>
          <p>麻雀4は横向きの画面に対応しています。<br />端末を横向きにすると、そのままゲームを続けられます。</p>
        </main>
      )
    }

    return (
      <div className={`majak-mobile-frame${accBox === 'room' ? ' majak-mobile-frame--room' : ''}`} style={{ position: 'relative' }}>
        {showMobileHeader && (
          <header className="majak-mobile-frame__bar">
            <div className="majak-mobile-frame__brand">
              <span className="majak-mobile-frame__brand-screen">{screenTitle}</span>
              <span className="majak-mobile-frame__brand-compact">麻雀4</span>
            </div>
            {showFramePlayerBalances && player && (
              <FramePlayerBalances name={player.name} mp={gamePlayer?.cashCount} gp={gamePlayer?.gamMoney} />
            )}
            <div className="majak-mobile-frame__tools">
              {isTitleScreen && !isHangeClient && <button type="button" onClick={() => setShowProfile(true)}>プロフィール</button>}
              {usesScreenHeader ? (
                frameBack && <button type="button" onClick={frameBack}>{isAnnouncementScreen ? '閉じる' : '戻る'}</button>
              ) : <>
                {onGoHome && <button type="button" onClick={onGoHome}>閉じる</button>}
                {onOpenAnnouncements && <button type="button" onClick={onOpenAnnouncements}>お知らせ</button>}
                {!IS_NATIVE_APP && <button type="button" onClick={enterFullscreen} title="全画面表示">全画面</button>}
                <button type="button" onClick={handleOpenSettings}>設定</button>
                {showMobileExit && <button type="button" onClick={handleClose}>ログアウト</button>}
                {accBox && <button type="button" onClick={handleAccuse}>通報</button>}
              </>}
            </div>
          </header>
        )}
        <main className="majak-mobile-frame__content">
          {children}
        </main>
        {showCfg && <CfgDlg initial={cfg} onOK={value => { setCfg(value); saveMajakConfig(value); setShowCfg(false) }} onCancel={() => setShowCfg(false)} />}
        {showExitConfirm && <EndingPopupWnd onOK={() => { void handleExitConfirm() }} onCancel={() => setShowExitConfirm(false)} />}
        {profileDialog}
      </div>
    )
  }

  return (
    <div className={isResponsiveDesktopScreen ? `majak-responsive-desktop-frame${isFullScreen ? ' is-fullscreen' : ''}` : undefined} style={{
      position: 'relative',
      width: desktopFrameWidth,
      height: desktopFrameHeight,
      display: 'flex',
      flexDirection: 'column',
      backgroundColor: frameChromeColor,
      border: isResponsiveDesktopScreen ? '2px solid #0b552a' : undefined,
      boxSizing: isResponsiveDesktopScreen ? 'border-box' : undefined,
      outline: isResponsiveDesktopScreen ? undefined : `2px solid ${frameChromeColor}`,
      boxShadow: isResponsiveDesktopScreen
        ? 'inset 0 0 0 2px #063618, 0 0 0 1px rgba(84, 168, 91, 0.48)'
        : `inset 0 0 0 2px ${frameChromeShadow}`,
      transform: desktopScale === 1 ? undefined : `scale(${desktopScale})`,
      transformOrigin: 'center center',
    }}>

      {isResponsiveDesktopScreen ? (
        <header className="majak-responsive-desktop-frame__bar">
          <strong className="majak-type-lg">{screenTitle}</strong>
          {showFramePlayerBalances && player && (
            <FramePlayerBalances name={player.name} mp={gamePlayer?.cashCount} gp={gamePlayer?.gamMoney} />
          )}
          <div>
            {(isTitleScreen || isLobbySelectScreen) && !isHangeClient && <button type="button" className="majak-responsive-control-button" onClick={() => setShowProfile(true)}>プロフィール</button>}
            {isAnnouncementScreen ? (
              onGoHome && <button type="button" className="majak-responsive-control-button" onClick={onGoHome}>閉じる</button>
            ) : isLobbySelectScreen ? (
              <>
                {onOpenAnnouncements && <button type="button" className="majak-responsive-control-button" onClick={onOpenAnnouncements}>お知らせ</button>}
                <button type="button" className="majak-responsive-control-button" onClick={handleOpenSettings}>設定</button>
                {!IS_NATIVE_APP && <button type="button" className="majak-responsive-control-button" onClick={handleMaximize}>{isFullScreen ? '元に戻す' : '全画面'}</button>}
                {frameBack && <button type="button" className="majak-responsive-control-button" onClick={frameBack}>戻る</button>}
              </>
            ) : isPaifuArchiveScreen ? (
              frameBack && <button type="button" className="majak-responsive-control-button" onClick={frameBack}>戻る</button>
            ) : <>
              {onGoHome && <button type="button" className="majak-responsive-control-button" onClick={onGoHome}>閉じる</button>}
              {onOpenAnnouncements && <button type="button" className="majak-responsive-control-button" onClick={onOpenAnnouncements}>お知らせ</button>}
              <button type="button" className="majak-responsive-control-button" onClick={handleOpenSettings}>設定</button>
              {isLobbyListScreen && <button type="button" className="majak-responsive-control-button" onClick={handleChangeLobby}>ロビー変更</button>}
              {!isLobbyListScreen && accBox !== 'room' && !IS_NATIVE_APP && <button type="button" className="majak-responsive-control-button" onClick={handleMaximize}>{isFullScreen ? '元に戻す' : '全画面'}</button>}
              {!isLobbyListScreen && accBox !== 'room' && <button type="button" className="majak-responsive-control-button" onClick={handleClose}>ログアウト</button>}
            </>}
          </div>
        </header>
      ) : (
        <div style={{ position: 'relative', width: frameWidth, height: 31, flexShrink: 0, overflow: 'hidden' }}>

        {/* 背景: mj_ttlhgc1024.png — ロゴ・タイトル文字すべて込み */}
        <img
          src={titleSrc}
          alt=""
          draggable={false}
          onError={event => { event.currentTarget.src = `${MAJAK3}/mj_ttlhgc1024.png` }}
          style={{ position: 'absolute', left: 0, top: 0, width: 1024, height: 31, imageRendering: 'pixelated', pointerEvents: 'none' }}
        />

        {/* 設定ボタン — mj_sndbox.png (40×23, 4フレーム) SNDBOX_CREATE x=901 y=4
             OnSoundOptionIconBoxClicked → m_MajakGame.OnGameConf() → CMJCfgDlg 相当 */}
        <TitleBtn
          src={roomSkinSrc('mj_sndbox')}
          fallbackSrc={roomSkinFallbackSrc('mj_sndbox')}
          frameW={40} frameH={23}
          x={901} y={4}
          onClick={handleOpenSettings}
          title="設定"
        />

        <TitleBtn
          src={roomSkinSrc('mj_minbox')}
          fallbackSrc={roomSkinFallbackSrc('mj_minbox')}
          frameW={21} frameH={23}
          x={947} y={4}
          onClick={handleMinimize}
          title="最小化"
        />

        {!IS_NATIVE_APP && (
          <TitleBtn
            src={roomSkinSrc(isFullScreen ? 'mj_rstbox' : 'mj_maxbox')}
            fallbackSrc={roomSkinFallbackSrc(isFullScreen ? 'mj_rstbox' : 'mj_maxbox')}
            frameW={21} frameH={23}
            x={970} y={4}
            onClick={handleMaximize}
            title="最大化"
          />
        )}

        <TitleBtn
          src={roomSkinSrc('mj_clsbox')}
          fallbackSrc={roomSkinFallbackSrc('mj_clsbox')}
          frameW={21} frameH={23}
          x={993} y={4}
          onClick={handleClose}
          title="閉じる"
        />

        {accBox && (
          <TitleBtn
            src={roomSkinSrc('mj_accbox')}
            fallbackSrc={roomSkinFallbackSrc('mj_accbox')}
            frameW={40} frameH={23}
            x={accBox === 'room' ? 733 : 817} y={4}
            onClick={handleAccuse}
            title="通報"
          />
        )}

        {accBox === 'room' && (
          <>
            <TitleBtn
              src={roomSkinSrc('mj_capbox')}
              fallbackSrc={roomSkinFallbackSrc('mj_capbox')}
              frameW={40} frameH={23}
              x={775} y={4}
              onClick={handleCapture}
              title="キャプチャ"
            />
            <TitleBtn
              src={roomSkinSrc('mj_banbox')}
              fallbackSrc={roomSkinFallbackSrc('mj_banbox')}
              frameW={40} frameH={23}
              x={817} y={4}
              onClick={handleBanish}
              title="追放"
            />
          </>
        )}

        </div>
      )}

      {/* ── コンテンツ領域 ────────────────────────────────────────── */}
      {children}

      {[
        { left: 0, top: 31, bottom: 0, width: 5, backgroundImage: borderBackgroundImage, backgroundPosition: '0 0', backgroundRepeat: 'repeat-y' },
        { right: 0, top: 31, bottom: 0, width: 5, backgroundImage: borderBackgroundImage, backgroundPosition: '0 0', backgroundRepeat: 'repeat-y' },
        ...(accBox === 'room' ? [] : [
          { left: 5, right: 5, bottom: 0, height: 5, backgroundColor: frameBottomColor },
          { left: 0, bottom: 0, width: 5, height: 5, backgroundColor: frameBottomColor },
          { right: 0, bottom: 0, width: 5, height: 5, backgroundColor: frameBottomColor },
        ]),
      ].map((edge, index) => (
        <div
          key={index}
          aria-hidden="true"
          style={{
            position: 'absolute',
            ...edge,
            backgroundColor: edge.backgroundColor ?? frameChromeColor,
            pointerEvents: 'none',
            zIndex: 10,
          }}
        />
      ))}

      {/* ── CMJCfgDlg — onOpenSettings 未指定時の内部設定ダイアログ ── */}
      {showCfg && (
        <CfgDlg
          initial={cfg}
          onOK={c => { saveMajakConfig(c); setCfg(c); setShowCfg(false) }}
          onCancel={() => setShowCfg(false)}
          onModify={configureMajakSound}
        />
      )}
      {showExitConfirm && <EndingPopupWnd onOK={() => { void handleExitConfirm() }} onCancel={() => setShowExitConfirm(false)} />}
      {profileDialog}
    </div>
  )
}
