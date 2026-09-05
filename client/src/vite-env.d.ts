/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string
  readonly VITE_DEBUG_SIGNALR?: string
  readonly VITE_ENV: string
  readonly VITE_AUTH_PROVIDER?: 'google' | 'hange'
  readonly VITE_LOGIN_URL?: string
  readonly VITE_BANNER_IMG_URL?: string
  readonly VITE_GOOGLE_CLIENT_ID?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
