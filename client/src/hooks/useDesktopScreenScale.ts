import { useEffect, useState } from 'react'

const BASE_VIEWPORT_HEIGHT = 768
const MAX_DESKTOP_SCALE = 1.3

function readDesktopScreenScale(): number {
  if (typeof window === 'undefined') return 1
  if (!document.fullscreenElement) return 1
  const viewportHeight = window.visualViewport?.height ?? window.innerHeight
  return Math.max(1, Math.min(MAX_DESKTOP_SCALE, viewportHeight / BASE_VIEWPORT_HEIGHT))
}

export function useDesktopScreenScale(enabled = true): number {
  const [scale, setScale] = useState(() => enabled ? readDesktopScreenScale() : 1)

  useEffect(() => {
    const visualViewport = window.visualViewport
    const update = () => setScale(enabled ? readDesktopScreenScale() : 1)
    update()
    window.addEventListener('resize', update)
    document.addEventListener('fullscreenchange', update)
    visualViewport?.addEventListener('resize', update)
    return () => {
      window.removeEventListener('resize', update)
      document.removeEventListener('fullscreenchange', update)
      visualViewport?.removeEventListener('resize', update)
    }
  }, [enabled])

  return scale
}