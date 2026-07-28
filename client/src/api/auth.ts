/**
 * Google OAuth authentication and player registration API.
 *
 * Flow:
 *   1. Read the localStorage registration cache and reuse it when valid.
 *   2. If there is no valid cache, show the GoogleLogin button.
 *   3. Send the Google ID token to POST /auth/google-login.
 *      - Unregistered players receive requiresRegistration=true and continue to the registration form.
 *      - Registered players receive the player payload.
 *   4. After registration form completion, send POST /auth/google-register.
 *   5. Store successful authentication in localStorage.
 */
import { clearRememberedGameAccessToken } from './gameAuthToken'

export interface MajakPlayer {
  pix:       string
  accessToken?: string
  name:      string
  sex:       '' | 'M' | 'F'
  avatarId:  string
  password:  string       // Legacy Hangame compatibility field; empty for Google auth.
  isTestEnv: boolean
  requiresRegistration: boolean
  accountStatus?: number  // 0=pending approval, 1=playable, 2=suspended
  termsAgreed?:  boolean
}

export interface RegisteredPlayerCache {
  version:               1
  registrationCompleted: true
  pix:                   string
  name:                  string
  sex:                   'M' | 'F'
  avatarId:              string
  isTestEnv:             boolean
  accountStatus?:        number
  savedAt:               string
}

const REGISTERED_PLAYER_STORAGE_KEY = 'majak2.registeredPlayer.v1'

// Cache read/write

export function readRegisteredPlayerCache(): RegisteredPlayerCache | null {
  try {
    const raw = window.localStorage.getItem(REGISTERED_PLAYER_STORAGE_KEY)
    if (!raw) return null
    const cached = JSON.parse(raw) as Partial<RegisteredPlayerCache>
    const legacyPix = (cached as Record<string, unknown>)['member' + 'Id']
    if (!cached.pix && typeof legacyPix === 'string') cached.pix = legacyPix
    if (cached.version !== 1 || cached.registrationCompleted !== true || !cached.pix) {
      return null
    }
    return cached as RegisteredPlayerCache
  } catch {
    return null
  }
}

export function saveRegisteredPlayerCache(player: MajakPlayer): void {
  if (player.requiresRegistration) return
  if (player.sex !== 'M' && player.sex !== 'F') return

  const cached: RegisteredPlayerCache = {
    version:               1,
    registrationCompleted: true,
    pix:        player.pix,
    name:       player.name,
    sex:        player.sex,
    avatarId:   player.avatarId,
    isTestEnv:  player.isTestEnv,
    accountStatus: player.accountStatus,
    savedAt:    new Date().toISOString(),
  }

  try {
    window.localStorage.setItem(REGISTERED_PLAYER_STORAGE_KEY, JSON.stringify(cached))
  } catch {
    // Continue authentication even when storage is unavailable.
  }
}

/** Convert a cached registration record to a MajakPlayer. */
export function cachedToPlayer(cache: RegisteredPlayerCache): MajakPlayer {
  return {
    pix:                 cache.pix,
    name:                cache.name,
    sex:                 cache.sex,
    avatarId:            cache.avatarId,
    password:            '',
    isTestEnv:           cache.isTestEnv,
    requiresRegistration: false,
    accountStatus:       cache.accountStatus ?? 1,
    termsAgreed:         true,
  }
}

/** Persist the latest accountStatus value in the cache. */
export function updateCacheAccountStatus(accountStatus: number): void {
  try {
    const raw = window.localStorage.getItem(REGISTERED_PLAYER_STORAGE_KEY)
    if (!raw) return
    const cached = JSON.parse(raw) as Partial<RegisteredPlayerCache>
    if (cached.version !== 1) return
    cached.accountStatus = accountStatus
    window.localStorage.setItem(REGISTERED_PLAYER_STORAGE_KEY, JSON.stringify(cached))
  } catch {
    // ignore
  }
}

// Google authentication API

/**
 * Send a Google ID token to the server and return player information.
 * Unregistered players receive requiresRegistration=true.
 */
export async function googleLogin(idToken: string): Promise<MajakPlayer> {
  let res: Response
  try {
    res = await fetch('/auth/google-login', {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body:    JSON.stringify({ idToken }),
    })
  } catch (err) {
    throw new AuthError('network', String(err))
  }

  if (res.status === 401) throw new AuthError('server', 'Invalid Google token')
  if (!res.ok) throw new AuthError('server', `HTTP ${res.status}`)

  const data = await res.json() as MajakPlayer
  if (!data.requiresRegistration) saveRegisteredPlayerCache(data)
  return data
}

let refreshLoginInFlight: Promise<MajakPlayer> | null = null

async function requestRefreshLogin(): Promise<MajakPlayer> {
  let res: Response
  try {
    res = await fetch('/auth/refresh', {
      method: 'POST',
      credentials: 'include',
    })
  } catch (err) {
    throw new AuthError('network', String(err))
  }

  if (res.status === 401) throw new AuthError('server', 'Refresh token expired')
  if (!res.ok) throw new AuthError('server', `HTTP ${res.status}`)

  const data = await res.json() as MajakPlayer
  saveRegisteredPlayerCache(data)
  return data
}

export async function refreshLogin(): Promise<MajakPlayer> {
  refreshLoginInFlight ??= requestRefreshLogin().finally(() => {
    refreshLoginInFlight = null
  })
  return refreshLoginInFlight
}

export async function logout(): Promise<void> {
  clearRememberedGameAccessToken()
  await fetch('/auth/logout', {
    method: 'POST',
    credentials: 'include',
  }).catch(() => {})
}

/**
 * Register nickname, sex, and avatar after the user accepts the terms.
 * The server records terms_agreed_at with NOW().
 */
export async function googleRegister(
  idToken:     string,
  displayName: string,
  sex:         'M' | 'F',
  avatarId:    string,
): Promise<MajakPlayer> {
  let res: Response
  try {
    res = await fetch('/auth/google-register', {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body:    JSON.stringify({ idToken, displayName, sex, avatarId }),
    })
  } catch (err) {
    throw new AuthError('network', String(err))
  }

  if (res.status === 401) throw new AuthError('server', 'Unauthorized')
  if (res.status === 400) {
    const err = await res.json() as { error?: string }
    throw new AuthError('server', err.error ?? 'BAD_REQUEST')
  }
  if (!res.ok) throw new AuthError('server', `HTTP ${res.status}`)

  const data = await res.json() as MajakPlayer
  saveRegisteredPlayerCache(data)
  return data
}

/**
 * Ask the server whether the nickname can be used.
 * Names shorter than 4 characters are rejected immediately by the server.
 */
export async function checkNickname(name: string): Promise<{ available: boolean; reason: string }> {
  try {
    const res = await fetch(`/auth/check-nickname?name=${encodeURIComponent(name)}`)
    if (!res.ok) return { available: false, reason: 'ERROR' }
    return await res.json() as { available: boolean; reason: string }
  } catch {
    return { available: false, reason: 'ERROR' }
  }
}

// Error class

export class AuthError extends Error {
  constructor(
    public readonly kind: 'network' | 'server' | 'cookie',
    message: string,
  ) {
    super(message)
    this.name = 'AuthError'
  }
}

// Legacy Hangame authentication stubs kept for compatibility. New Google auth flow does not use them.

/** @deprecated Migrated to the Google auth flow. */
export function detectTestEnv(cookieValue: string): boolean {
  return cookieValue.trimStart().toLowerCase().startsWith('hangametest=')
}

/** @deprecated Migrated to the Google auth flow. AuthGate handles Google authentication. */
export async function login(): Promise<MajakPlayer> {
  throw new AuthError('cookie', 'Hangame auth is no longer supported. Use Google OAuth.')
}

/** @deprecated Migrated to the Google auth flow. */
export async function registerPlayer(
  _pendingPlayer: MajakPlayer,
  _sex: 'M' | 'F',
  _avatarId: string,
): Promise<MajakPlayer> {
  throw new AuthError('cookie', 'Hangame register is no longer supported. Use googleRegister().')
}

/** @deprecated Migrated to the Google auth flow. */
export function redirectToLogin(): never {
  throw new AuthError('cookie', 'Hangame redirectToLogin is no longer used.')
}