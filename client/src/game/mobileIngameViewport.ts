import { getIngameLayout, isCenteredIngameLayout, isMobileIngameLayout, type IngameLayoutMode } from './ingameLayout'

export interface HudPoint { x: number; y: number }

export interface EffectSize { width: number; height: number }

export const MOBILE_PLAYFIELD_OFFSET_Y = -8

export interface MobileVisibleWorldBounds {
  left: number
  top: number
  right: number
  bottom: number
}

const STANDARD_MOBILE_LANDSCAPE_ASPECT = 667 / 375

export function mobileDiscardScale(baseScale: number, bounds = mobileVisibleWorldBounds()): number {
  if (!bounds) return baseScale
  const width = bounds.right - bounds.left
  const height = bounds.bottom - bounds.top
  if (width <= 0 || height <= 0) return baseScale
  return width / height > STANDARD_MOBILE_LANDSCAPE_ASPECT ? baseScale * 0.9 : baseScale
}

function centeredLayoutReference(mode: IngameLayoutMode): HudPoint {
  const layout = getIngameLayout(mode)
  return {
    x: layout.board.x + layout.centerInfo.x + layout.centerInfo.width / 2,
    y: layout.board.y + layout.centerInfo.y + layout.centerInfo.height / 2,
  }
}

export function mobileVisibleWorldBounds(): MobileVisibleWorldBounds | null {
  if (typeof document === 'undefined') return null
  const shell = document.querySelector('.majak-mobile-ingame-shell, .majak-responsive-ingame-playfield')
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
  if (!isCenteredIngameLayout(mode)) return { x: 0, y: 0 }
  const bounds = mobileVisibleWorldBounds()
  if (!bounds) return { x: 0, y: 0 }
  const reference = centeredLayoutReference(mode)
  return {
    x: (bounds.left + bounds.right) / 2 - reference.x,
    y: (bounds.top + bounds.bottom) / 2 - reference.y + (isMobileIngameLayout(mode) ? MOBILE_PLAYFIELD_OFFSET_Y : 0),
  }
}

export function responsiveDesktopCenterOffset(mode: IngameLayoutMode): HudPoint {
  if (mode !== 'responsiveDesktop') return { x: 0, y: 0 }
  const bounds = responsiveDesktopVisibleWorldBounds()
  if (!bounds) return { x: 0, y: 0 }
  const layout = getIngameLayout(mode)
  const boardCenterX = layout.board.x + layout.board.width / 2
  const boardCenterY = layout.board.y + layout.board.height / 2
  return {
    x: (bounds.left + bounds.right) / 2 - boardCenterX,
    y: (bounds.top + bounds.bottom) / 2 - boardCenterY,
  }
}

export function responsiveDesktopEdgeOffset(mode: IngameLayoutMode, loc: number): HudPoint {
  if (mode !== 'responsiveDesktop') return { x: 0, y: 0 }
  const bounds = responsiveDesktopVisibleWorldBounds()
  if (!bounds) return { x: 0, y: 0 }
  const board = getIngameLayout(mode).board
  if (loc === 0) return { x: 0, y: bounds.bottom - (board.y + board.height) }
  if (loc === 1) return { x: bounds.right - (board.x + board.width), y: 0 }
  if (loc === 2) return { x: 0, y: bounds.top - board.y }
  return { x: bounds.left - board.x, y: 0 }
}

export function responsiveDesktopSeatOffset(mode: IngameLayoutMode, loc: number): HudPoint {
  const center = responsiveDesktopCenterOffset(mode)
  const edge = responsiveDesktopEdgeOffset(mode, loc)
  return loc === 0 || loc === 2
    ? { x: center.x, y: edge.y }
    : { x: edge.x, y: center.y }
}

export function responsiveDesktopCornerOffset(mode: IngameLayoutMode, loc: number): HudPoint {
  if (mode !== 'responsiveDesktop') return { x: 0, y: 0 }
  const bounds = responsiveDesktopVisibleWorldBounds()
  if (!bounds) return { x: 0, y: 0 }
  const board = getIngameLayout(mode).board
  return {
    x: loc === 1 || loc === 2 ? bounds.right - (board.x + board.width) : bounds.left - board.x,
    y: loc === 0 || loc === 1 ? bounds.bottom - (board.y + board.height) : bounds.top - board.y,
  }
}

export function responsiveDesktopVisibleWorldBounds(): MobileVisibleWorldBounds | null {
  const bounds = mobileVisibleWorldBounds()
  if (!bounds || typeof document === 'undefined') return bounds
  const shell = document.querySelector('.majak-responsive-ingame-playfield')
  const canvas = shell?.querySelector('canvas')
  if (!(canvas instanceof HTMLCanvasElement)) return bounds
  return {
    left: Math.max(0, bounds.left),
    top: Math.max(0, bounds.top),
    right: Math.min(canvas.width, bounds.right),
    bottom: Math.min(canvas.height, bounds.bottom),
  }
}

export function mobileVisibleWorldLayoutKey(mode: IngameLayoutMode): string {
  if (!isCenteredIngameLayout(mode) && mode !== 'responsiveDesktop') return 'desktop'
  const bounds = mode === 'responsiveDesktop' ? responsiveDesktopVisibleWorldBounds() : mobileVisibleWorldBounds()
  if (!bounds) return 'mobile:none'
  return [bounds.left, bounds.top, bounds.right, bounds.bottom]
    .map(value => Math.round(value))
    .join(':')
}

export function mobileEffectPointFromAnchor(point: HudPoint, desktopAnchor: HudPoint, mobileAnchor: HudPoint): HudPoint {
  return {
    x: point.x + mobileAnchor.x - desktopAnchor.x,
    y: point.y + mobileAnchor.y - desktopAnchor.y,
  }
}

export function centerMobileEffectPoint(size: EffectSize, bounds: MobileVisibleWorldBounds): HudPoint {
  return {
    x: bounds.left + (bounds.right - bounds.left - size.width) / 2,
    y: bounds.top + (bounds.bottom - bounds.top - size.height) / 2,
  }
}