import { describe, expect, it } from 'vitest'
import {
  centerMobileEffectPoint,
  mobileDiscardScale,
  mobileEffectPointFromAnchor,
  type MobileVisibleWorldBounds,
} from './mobileIngameViewport'

function bounds(width: number, height: number): MobileVisibleWorldBounds {
  return { left: 0, top: 0, right: width, bottom: height }
}

describe('mobileDiscardScale', () => {
  it('keeps the current discard size at 16:9', () => {
    expect(mobileDiscardScale(0.7, bounds(667, 375))).toBe(0.7)
  })

  it('uses 90 percent of the current size on wider mobile screens', () => {
    expect(mobileDiscardScale(0.7, bounds(932, 430))).toBeCloseTo(0.63)
  })

  it('uses the same 90 percent ratio for discard spacing', () => {
    expect(mobileDiscardScale(1.2, bounds(932, 430))).toBeCloseTo(1.08)
  })
})

describe('mobile effect positioning', () => {
  it('moves a legacy effect by the mobile anchor delta', () => {
    expect(mobileEffectPointFromAnchor(
      { x: 10, y: 20 },
      { x: 100, y: 200 },
      { x: 130, y: 260 },
    )).toEqual({ x: 40, y: 80 })
  })

  it('centers an effect in the visible world', () => {
    expect(centerMobileEffectPoint(
      { width: 900, height: 435 },
      { left: 0, top: 153, right: 794, bottom: 519 },
    )).toEqual({ x: -53, y: 118.5 })
  })
})