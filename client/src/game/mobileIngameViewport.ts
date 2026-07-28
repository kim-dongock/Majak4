import { getIngameLayout, type IngameLayoutMode } from './ingameLayout'

export interface HudPoint { x: number; y: number }

export const MOBILE_PLAYFIELD_OFFSET_Y = -8

export interface MobileVisibleWorldBounds {
  left: number
  top: number
  right: number
  bottom: number
}

function mobileCenterInfoReference(): HudPoint {
  const layout = getIngameLayout('mobileLandscape')
  return {
    x: layout.board.x + layout.centerInfo.x + layout.centerInfo.width / 2,
    y: layout.board.y + layout.centerInfo.y + layout.centerInfo.height / 2,
  }
}

export function mobileVisibleWorldBounds(): MobileVisibleWorldBounds | null {
  if (typeof document === 'undefined') return null
  const shell = document.querySelector('.majak-mobile-ingame-shell')
  const canvas = shell?.querySelector('canvas')
  if (!(shell instanceof HTMLElement) || !(canvas instanceof HTMLCanvasElement)) return null
  const shellRect = shell.getBoundingClientRect()
  const canvasRect = canvas.getBoundingClientRect()
  const scaleX = canvasRect.width / canvas.width
  const scaleY = canvasRect.height / canvas.height
  if (!Number.isFinite(scaleX) || !Number.isFinite(scaleY) || scaleX <= 0 || scaleY <= 0) return null
  const left = (shellRect.left - canvasRect.left) / scaleX
  const top = (shellRect.top - canvasRect.top) / scaleY
  return {
    left,
    top,
    right: left + shellRect.width / scaleX,
    bottom: top + shellRect.height / scaleY,
  }
}

export function mobileCenterHudOffset(mode: IngameLayoutMode): HudPoint {
  if (mode !== 'mobileLandscape') return { x: 0, y: 0 }
  const bounds = mobileVisibleWorldBounds()
  if (!bounds) return { x: 0, y: 0 }
  const reference = mobileCenterInfoReference()
  return {
    x: (bounds.left + bounds.right) / 2 - reference.x,
    y: (bounds.top + bounds.bottom) / 2 - reference.y + MOBILE_PLAYFIELD_OFFSET_Y,
  }
}

export function mobileVisibleWorldLayoutKey(mode: IngameLayoutMode): string {
  if (mode !== 'mobileLandscape') return 'desktop'
  const bounds = mobileVisibleWorldBounds()
  if (!bounds) return 'mobile:none'
  return [bounds.left, bounds.top, bounds.right, bounds.bottom]
    .map(value => Math.round(value))
    .join(':')
}