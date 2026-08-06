import { MemoryRouter, Routes, Route, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useEffect, useRef, useState, useCallback } from 'react'
import { GoogleLogin } from '@react-oauth/google'
import { Capacitor } from '@capacitor/core'
import ChannelGroupScreen from './screens/outgame/ChannelGroupScreen'
import LobbySelectScreen from './screens/outgame/LobbySelectScreen'
import LobbyScreen from './screens/outgame/LobbyScreen'
import RoomScreen from './screens/outgame/RoomScreen'
import GameScreen from './screens/ingame/GameScreen'
import PaifWnd from './screens/ingame/PaifWnd'
import MajakFrame from './components/MajakFrame'
import MessageBoxHost from './components/MessageBoxHost'
import RegistrationDlg from './screens/outgame/dialogs/RegistrationDlg'
import { googleLogin, refreshLogin, saveRegisteredPlayerCache, type MajakPlayer } from './api/auth'
import { getPlayerContinueRoom } from './api/channel'
import * as SignalR from './api/signalr'
import { useAuthStore } from './store/authStore'
import { useCustomSkinStore } from './store/customSkinStore'
import { forceDuplicateConnectionLogout } from './utils/msgbox'
import { signInWithNativeGoogle } from './utils/nativeGoogleAuth'

const ROUTER_STATE_STORAGE_KEY = 'majak:last-router-state'

type StoredRouterState = {
  pathname: string
  search?: string
  hash?: string
  state?: unknown
}

function readStoredRouterState(): StoredRouterState {
  try {
    const raw = window.sessionStorage.getItem(ROUTER_STATE_STORAGE_KEY)
    if (!raw) return { pathname: '/channel' }
    const value = JSON.parse(raw) as StoredRouterState
    return typeof value.pathname === 'string' && value.pathname.startsWith('/')
      ? value
      : { pathname: '/channel' }
  } catch {
    return { pathname: '/channel' }
  }
}

function RouterStatePersistence() {
  const location = useLocation()

  useEffect(() => {
    const state: StoredRouterState = {
      pathname: location.pathname,
      search: location.search,
      hash: location.hash,
      state: location.state,
    }
    try {
      window.sessionStorage.setItem(ROUTER_STATE_STORAGE_KEY, JSON.stringify(state))
    } catch {
      window.sessionStorage.setItem(ROUTER_STATE_STORAGE_KEY, JSON.stringify({ pathname: location.pathname }))
    }
    if (window.location.pathname !== '/') window.history.replaceState(window.history.state, '', '/')
  }, [location])

  return null
}

function SignalRRouteDisconnect() {
  const location = useLocation()

  useEffect(() => {
    const path = location.pathname
    const keepsSocket = path.includes('/lobby') || path.startsWith('/game/')
    if (!keepsSocket) {
      void SignalR.disconnect().catch(() => {})
    }
  }, [location.pathname])

  return null
}

function ForcedLogoutListener() {
  useEffect(() => {
    const handleForcedLogout = (data: Record<string, unknown>) => {
      console.error('[SignalR] server forced logout', { payload: data })
      forceDuplicateConnectionLogout()
    }

    SignalR.on('forcedLogout', handleForcedLogout)
    return () => SignalR.off('forcedLogout', handleForcedLogout)
  }, [])

  return null
}

function buildContinueAutoEnterPayload(room: {
  roomId: number
  pix: string
  title: string
  roomOption: string
}) {
  return {
    roomId: room.roomId,
    k42e: room.roomId,
    pix: room.pix,
    k3e: room.pix,
    connectFor: 'GameJoin',
    k82e: 'v16e',
    playerType: 'v4e',
    k57e: 'v4e',
    roomTitle: room.title,
    k45e: room.title,
    roomPwd: '',
    k67e: '',
    roomOption: room.roomOption,
    k46e: room.roomOption,
  }
}

function ContinueRoomBootstrap() {
  const location = useLocation()
  const navigate = useNavigate()
  const { status, player } = useAuthStore()
  const startedRef = useRef(false)

  useEffect(() => {
    if (startedRef.current || status !== 'ok' || !player?.pix) return
    startedRef.current = true

    let cancelled = false
    const wait = (ms: number) => new Promise(resolve => window.setTimeout(resolve, ms))

    async function restore() {
      for (let attempt = 0; attempt < 3 && !cancelled; attempt += 1) {
        if (attempt > 0) await wait(700)
        const room = await getPlayerContinueRoom(player!.pix).catch(() => null)
        const channelId = room?.channelId ?? room?.chanelId
        if (!room?.roomId || !channelId || !room.serverUrl) continue
        const customSkin = useCustomSkinStore.getState()

        navigate(`/channel/${encodeURIComponent(channelId)}/lobby/room/${room.roomId}`, {
          replace: true,
          state: {
            serverUrl: room.serverUrl,
            mode: 'auto',
            resumePlaying: true,
            roomTitle: room.title ?? '',
            roomOption: room.roomOption ?? '',
            customBgId: customSkin.bgId,
            customHaiId: customSkin.haiId,
            customBoardType: customSkin.bgType,
            autoEnterPayload: buildContinueAutoEnterPayload({
              roomId: room.roomId,
              pix: player!.pix,
              title: room.title ?? '',
              roomOption: room.roomOption ?? '',
            }),
          },
        })
        return
      }
    }

    void restore()
    return () => { cancelled = true }
  }, [location.pathname, navigate, player, status])

  return null
}

function AppLoadingScreen() {
  return (
    <div className="majak-boot-loading">
      <div className="majak-boot-loading__panel">
        <img className="majak-boot-loading__logo" src="/assets/images/common/ico_big_majak2.jpg" alt="" draggable={false} />
        <div className="majak-sync-spinner" aria-hidden="true" />
      </div>
    </div>
  )
}

// ── 認証ゲート ──────────────────────────────────────────────────────
/**
 * Google OAuth 認証ゲート
 *
 * フロー:
 *   1. Google サインインボタン表示
 *   3. Google redirect login → /auth/google-login-redirect
 *      a. 登録済み → refresh cookie を発行してトップへ戻る
 *      b. 未登録 → RegistrationDlg (利用規約→ニックネーム→性別/アバター)
 */
function AuthGate({ children }: { children: React.ReactNode }) {
  const { status, player, setLoading, setPlayer, setError } = useAuthStore()
  const [idToken, setIdToken] = useState<string | null>(null)
  const [refreshChecked, setRefreshChecked] = useState(false)
  const [registrationRequest, setRegistrationRequest] = useState<{ idToken: string; player: MajakPlayer } | null>(null)

  useEffect(() => {
    if (window.location.protocol !== 'http:' || window.location.hostname !== '127.0.0.1') return
    const localUrl = new URL(window.location.href)
    localUrl.hostname = 'localhost'
    window.location.replace(localUrl.toString())
  }, [])

  const consumeGoogleRedirectResult = useCallback(async (): Promise<boolean> => {
    const url = new URL(window.location.href)
    const marker = url.searchParams.get('googleAuth')
    if (!marker) return false

    url.searchParams.delete('googleAuth')
    const nextQuery = url.searchParams.toString()
    window.history.replaceState(window.history.state, '', `${url.pathname}${nextQuery ? `?${nextQuery}` : ''}${url.hash}`)

    if (marker === 'error') {
      setError('Google サインインに失敗しました。もう一度お試しください。')
      return true
    }

    if (marker !== 'register') return false

    // The API retains the verified Google token in an HttpOnly cookie until registration completes.
    setIdToken('')
    setRegistrationRequest({
      idToken: '',
      player: {
        pix: '', name: '', sex: '', avatarId: '', password: '', isTestEnv: false,
        requiresRegistration: true, accountStatus: 0, termsAgreed: false,
      },
    })
    return true
  }, [setError, setLoading, setPlayer])

  // 1. 初回マウント: HttpOnly refresh cookie があれば Google 画面なしで pix を再発行する
  useEffect(() => {
    const current = useAuthStore.getState()
    if (current.status === 'ok' && current.player?.accessToken) {
      setRefreshChecked(true)
      return
    }
    if (current.status === 'login_required') {
      setRefreshChecked(true)
      return
    }

    setLoading()
    let disposed = false
    void (async () => {
      const handledRedirect = await consumeGoogleRedirectResult()
      if (disposed || handledRedirect) {
        if (!disposed) setRefreshChecked(true)
        return
      }
      try {
        const p = await refreshLogin()
        if (disposed) return
        if (p) setPlayer(p)
        else setRefreshChecked(true)
      } catch {
        if (disposed) return
        const latest = useAuthStore.getState()
        if (latest.status === 'ok' && latest.player?.accessToken) return
        setRefreshChecked(true)
      }
    })()

    return () => { disposed = true }
  }, [consumeGoogleRedirectResult, setLoading, setPlayer])

  const handleGoogleCredential = useCallback(async (credential: string) => {
    setLoading()
    try {
      const authenticatedPlayer = await googleLogin(credential)
      if (authenticatedPlayer.requiresRegistration) {
        setIdToken(credential)
        setRegistrationRequest({ idToken: credential, player: authenticatedPlayer })
        return
      }
      setPlayer(authenticatedPlayer)
    } catch {
      setError('Google サインインに失敗しました。もう一度お試しください。')
    }
  }, [setError, setLoading, setPlayer])

  // 3. 承認待ち中は一定間隔で再照会して、承認後に自動でゲームへ進める。
  useEffect(() => {
    if (!player || player.accountStatus !== 0 || !player.termsAgreed) return

    let disposed = false
    const intervalId = window.setInterval(() => {
      if (disposed) return
      void (async () => {
        try {
          const refreshed = await refreshLogin()
          if (disposed) return
          if (refreshed) setPlayer(refreshed)
        } catch {
          // Keep pending screen; transient errors are retried on next tick.
        }
      })()
    }, 5000)

    return () => {
      disposed = true
      window.clearInterval(intervalId)
    }
  }, [idToken, player, setPlayer])

  // ── レンダリング ─────────────────────────────────────────────────

  // キャッシュ確認前 (idle)
  if (status === 'idle') {
    return <AppLoadingScreen />
  }

  if (registrationRequest) {
    return (
      <RegistrationDlg
        idToken={registrationRequest.idToken}
        googleInfo={registrationRequest.player}
        onComplete={(p) => {
          setRegistrationRequest(null)
          saveRegisteredPlayerCache(p)
          setPlayer(p)
        }}
      />
    )
  }

  // Google サインイン待ち (refresh cookie が使えなかった場合、またはゲーム認証が失効した場合のみ表示)
  if ((status === 'loading' && !player && !idToken && refreshChecked) || status === 'login_required') {
    return <GoogleSignInScreen onCredential={handleGoogleCredential} onError={() => setError('Google サインインに失敗しました。もう一度お試しください。')} />
  }

  // Google 認証中 / サーバー通信中
  if (status === 'loading') {
    return <AppLoadingScreen />
  }

  // エラー
  if (status === 'error') {
    return (
      <div style={{
        position: 'fixed', inset: 0,
        background: '#1a1a2e',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        flexDirection: 'column', gap: 12,
        fontFamily: 'var(--majak-font-family-ui)', color: '#fff',
      }}>
        <div style={{ fontSize: 'var(--majak-font-18)', color: '#f44' }}>認証エラー</div>
        <button
          type="button"
          onClick={() => { setIdToken(null); setLoading() }}
          style={{ padding: '8px 20px', cursor: 'pointer' }}
        >
          再試行
        </button>
      </div>
    )
  }

  // 未登録 → 会員登録フォーム
  if (player?.requiresRegistration && idToken) {
    return (
      <RegistrationDlg
        idToken={idToken}
        googleInfo={player}
        onComplete={(p) => { saveRegisteredPlayerCache(p); setPlayer(p) }}
      />
    )
  }

  // 承認待ち (利用規約同意済み・管理者未承認)
  if (player && player.accountStatus === 0 && player.termsAgreed) {
    return <PendingApprovalScreen />
  }

  // アカウント停止
  if (player?.accountStatus === 2) {
    return <SuspendedScreen />
  }

  return <>{children}</>
}

// ── Google サインイン画面 ────────────────────────────────────────────
function GoogleSignInScreen({
  onCredential,
  onError,
}: {
  onCredential: (credential: string) => void
  onError: () => void
}) {
  const isNativeApp = Capacitor.isNativePlatform()
  const [nativeLoginPending, setNativeLoginPending] = useState(false)

  const handleNativeGoogleLogin = async () => {
    if (nativeLoginPending) return
    setNativeLoginPending(true)
    try {
      onCredential(await signInWithNativeGoogle())
    } catch (error) {
      if (error instanceof Error && /cancel/i.test(error.message)) return
      onError()
    } finally {
      setNativeLoginPending(false)
    }
  }

  return (
    <div style={{
      position: 'fixed', inset: 0,
      background: 'linear-gradient(135deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%)',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontFamily: 'var(--majak-font-family-ui)',
    }}>
      <div style={{
        background: 'rgba(255,255,255,0.05)',
        border: '1px solid rgba(255,255,255,0.15)',
        borderRadius: 12,
        padding: '40px 48px',
        display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 24,
        boxShadow: '0 8px 32px rgba(0,0,0,0.4)',
      }}>
        <img
          src="/assets/images/common/ico_big_majak2.jpg"
          alt="麻雀4"
          draggable={false}
          style={{ width: 80, height: 80, borderRadius: 8 }}
          onError={(e) => { (e.target as HTMLImageElement).style.display = 'none' }}
        />
        <div style={{ color: '#fff', fontSize: 'var(--majak-font-22)', fontWeight: 700, letterSpacing: 2 }}>
          麻雀4
        </div>
        <div style={{ color: 'rgba(255,255,255,0.7)', fontSize: 'var(--majak-font-13)', textAlign: 'center' }}>
          Google アカウントでサインインしてください
        </div>
        <div style={{ position: 'relative', width: 280, height: 40 }}>
          {isNativeApp ? (
            <button
              type="button"
              disabled={nativeLoginPending}
              onClick={() => { void handleNativeGoogleLogin() }}
              style={{
                position: 'absolute', inset: 0,
                display: 'flex', alignItems: 'center', justifyContent: 'center',
                boxSizing: 'border-box',
                border: '1px solid #747775', borderRadius: 4,
                background: '#fff', color: '#1f1f1f',
                fontSize: 14, fontWeight: 500, cursor: nativeLoginPending ? 'default' : 'pointer',
              }}
            >
              <img src="/assets/images/common/google-g.svg" alt="" draggable={false} style={{ position: 'absolute', left: 12, width: 18, height: 18 }} />
              {nativeLoginPending ? 'ログイン中...' : 'Google でログイン'}
            </button>
          ) : (
            <>
              <GoogleLogin
                onSuccess={response => {
                  if (response.credential) onCredential(response.credential)
                  else onError()
                }}
                onError={onError}
                useOneTap={false}
                width="280"
                containerProps={{ style: { position: 'absolute', inset: 0, width: '100%' } }}
              />
              <img src="/assets/images/common/google-g.svg" alt="" draggable={false} style={{ position: 'absolute', left: 12, top: 11, zIndex: 1, width: 18, height: 18, pointerEvents: 'none' }} />
            </>
          )}
        </div>
      </div>
    </div>
  )
}

// ── 承認待ち画面 ─────────────────────────────────────────────────────
function PendingApprovalScreen() {
  return (
    <div style={{
      position: 'fixed', inset: 0,
      background: '#1a1a2e',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontFamily: 'var(--majak-font-family-ui)', color: '#fff',
    }}>
      <div style={{
        background: 'rgba(255,255,255,0.05)',
        border: '1px solid rgba(255,255,255,0.2)',
        borderRadius: 10,
        padding: '36px 48px',
        textAlign: 'center', maxWidth: 400,
      }}>
        <div style={{ fontSize: 'var(--majak-font-20)', marginBottom: 16 }}>⏳ 承認待ち</div>
        <div style={{ fontSize: 'var(--majak-font-14)', color: 'rgba(255,255,255,0.75)', lineHeight: 1.8 }}>
          会員登録が完了しました。<br />
          管理者によるアカウント承認をお待ちください。<br />
          承認後にゲームをプレイできます。
        </div>
      </div>
    </div>
  )
}

// ── アカウント停止画面 ───────────────────────────────────────────────
function SuspendedScreen() {
  return (
    <div style={{
      position: 'fixed', inset: 0,
      background: '#1a1a2e',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontFamily: 'var(--majak-font-family-ui)', color: '#fff',
    }}>
      <div style={{
        background: 'rgba(255,0,0,0.1)',
        border: '1px solid rgba(255,60,60,0.4)',
        borderRadius: 10,
        padding: '36px 48px',
        textAlign: 'center', maxWidth: 400,
      }}>
        <div style={{ fontSize: 'var(--majak-font-20)', marginBottom: 16, color: '#f66' }}>🚫 アカウント停止</div>
        <div style={{ fontSize: 'var(--majak-font-14)', color: 'rgba(255,255,255,0.75)', lineHeight: 1.8 }}>
          このアカウントは停止されています。<br />
          詳細については運営までお問い合わせください。
        </div>
      </div>
    </div>
  )
}


export default function App() {
  const initialRoute = readStoredRouterState()

  return (
    <>
      <AuthGate>
        <MemoryRouter initialEntries={[initialRoute]}>
          <RouterStatePersistence />
          <SignalRRouteDisconnect />
          <ForcedLogoutListener />
          <ContinueRoomBootstrap />
          <Routes>
            {/* アウトゲーム (CMajakFrame タイトルバー付き) */}
            <Route path="/" element={<Navigate to="/channel" replace />} />
            <Route path="/channel" element={<MajakFrame><ChannelGroupScreen /></MajakFrame>} />
            <Route path="/channel/select/:group" element={<MajakFrame><LobbySelectScreen /></MajakFrame>} />
            <Route path="/channel/:channelId" element={<MajakFrame><LobbySelectScreen /></MajakFrame>} />
            <Route path="/channel/:channelId/lobby" element={<MajakFrame accBox="channel"><LobbyScreen /></MajakFrame>} />
            <Route path="/channel/:channelId/lobby/room/:roomId" element={<MajakFrame accBox="room"><RoomScreen /></MajakFrame>} />
            <Route path="/channel/:channelId/lobby/:lobbyId/room/:roomId" element={<MajakFrame accBox="room"><RoomScreen /></MajakFrame>} />
            {/* インゲーム (Phaser) — タイトルバーなし */}
            <Route path="/game/:roomId" element={<GameScreen />} />
            {/* 牏譜再生 (CMJPaifWnd) */}
            <Route path="/paifu" element={<PaifWnd />} />
            <Route path="/paifu/:roomId" element={<PaifWnd />} />
          </Routes>
        </MemoryRouter>
      </AuthGate>
      <MessageBoxHost />
    </>
  )
}
