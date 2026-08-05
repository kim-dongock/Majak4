import { Capacitor } from '@capacitor/core'
import { SocialLogin } from '@capgo/capacitor-social-login'

let initialization: Promise<void> | null = null

function initializeNativeGoogle(): Promise<void> {
  if (initialization) return initialization

  const webClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID ?? ''
  if (!webClientId) return Promise.reject(new Error('Google Web Client ID is not configured'))

  const platform = Capacitor.getPlatform()
  const iOSClientId = import.meta.env.VITE_GOOGLE_IOS_CLIENT_ID ?? ''
  if (platform === 'ios' && !iOSClientId) {
    return Promise.reject(new Error('Google iOS Client ID is not configured'))
  }

  initialization = SocialLogin.initialize({
    google: {
      webClientId,
      iOSClientId: iOSClientId || undefined,
      iOSServerClientId: platform === 'ios' ? webClientId : undefined,
      mode: 'online',
    },
  }).catch(error => {
    initialization = null
    throw error
  })
  return initialization
}

export async function signInWithNativeGoogle(): Promise<string> {
  if (!Capacitor.isNativePlatform()) throw new Error('Native Google login is unavailable on web')

  await initializeNativeGoogle()
  const login = await SocialLogin.login({
    provider: 'google',
    options: {
      style: 'bottom',
      filterByAuthorizedAccounts: false,
      autoSelectEnabled: false,
      forcePrompt: true,
    },
  })

  if (login.result.responseType !== 'online' || !login.result.idToken) {
    throw new Error('Google login did not return an ID token')
  }
  return login.result.idToken
}