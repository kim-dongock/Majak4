import { describe, expect, it } from 'vitest'
import { mobileDiscardScale, type MobileVisibleWorldBounds } from './mobileIngameViewport'

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