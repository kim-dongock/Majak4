import { useEffect, useState } from 'react'
import { Capacitor } from '@capacitor/core'

export type OutgameLayoutMode = 'desktop' | 'mobileLandscape' | 'mobilePortrait'

const MOBILE_OUTGAME_QUERY = '(max-width: 768px), (orientation: landscape) and (max-height: 540px)'

function readOutgameLayoutMode(): OutgameLayoutMode {
  if (typeof window === 'undefined') return 'desktop'
  if (Capacitor.isNativePlatform()) {
    return window.matchMedia('(orientation: portrait)').matches ? 'mobilePortrait' : 'mobileLandscape'
  }
  if (!window.matchMedia(MOBILE_OUTGAME_QUERY).matches) return 'desktop'
  return window.matchMedia('(orientation: portrait)').matches ? 'mobilePortrait' : 'mobileLandscape'
}

export function useOutgameLayoutMode(): OutgameLayoutMode {
  const [mode, setMode] = useState<OutgameLayoutMode>(() => readOutgameLayoutMode())

  useEffect(() => {
    const media = window.matchMedia(MOBILE_OUTGAME_QUERY)
    const orientation = window.matchMedia('(orientation: portrait)')
    const update = () => setMode(readOutgameLayoutMode())
    update()
    media.addEventListener('change', update)
    orientation.addEventListener('change', update)
    return () => {
      media.removeEventListener('change', update)
      orientation.removeEventListener('change', update)
    }
  }, [])

  return mode
}