import React from 'react'
import ReactDOM from 'react-dom/client'
import { GoogleOAuthProvider } from '@react-oauth/google'
import App from './App'
import { showError } from './utils/msgbox'
import './index.css'

let lastGlobalErrorMessage = ''
let lastGlobalErrorAt = 0

function shouldIgnoreGlobalError(message: string): boolean {
  return /Cannot (?:suspend|resume) a closed AudioContext/i.test(message)
}

function showGlobalError(message: string): void {
  if (shouldIgnoreGlobalError(message)) return
  const now = Date.now()
  if (message === lastGlobalErrorMessage && now - lastGlobalErrorAt < 1000) return
  lastGlobalErrorMessage = message
  lastGlobalErrorAt = now
  void showError(message)
}

window.addEventListener('error', event => {
  const message = event.message || event.error?.message || 'JavaScript エラーが発生しました'
  showGlobalError(message)
  event.preventDefault()
})

window.addEventListener('unhandledrejection', event => {
  const reason = event.reason
  const message = reason instanceof Error
    ? reason.message
    : typeof reason === 'string'
      ? reason
      : 'JavaScript エラーが発生しました'
  showGlobalError(message)
  event.preventDefault()
})

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <GoogleOAuthProvider clientId={import.meta.env.VITE_GOOGLE_CLIENT_ID ?? ''}>
      <App />
    </GoogleOAuthProvider>
  </React.StrictMode>,
)
