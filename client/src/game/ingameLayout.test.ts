import { describe, expect, it } from 'vitest'
import {
  DESKTOP_INGAME_LAYOUT,
  getIngameLayout,
  isCenteredIngameLayout,
  isMobileIngameLayout,
  MOBILE_DEAD_WALL_SHIFT_X,
  MOBILE_DISCARD_CENTER_INFO_OFFSETS,
  MOBILE_INGAME_LAYOUT,
  MOBILE_REACH_POSITIONS,
  MOBILE_TOP_MELD_CENTER_INFO_OFFSET,
} from './ingameLayout'

describe('responsive desktop layout mode', () => {
  it('centers the original desktop geometry without using mobile coordinates', () => {
    expect(isCenteredIngameLayout('responsiveDesktop')).toBe(true)
    expect(isCenteredIngameLayout('mobileLandscape')).toBe(true)
    expect(isCenteredIngameLayout('desktop')).toBe(false)
    expect(isMobileIngameLayout('responsiveDesktop')).toBe(false)
    expect(isMobileIngameLayout('mobileLandscape')).toBe(true)
    expect(getIngameLayout('responsiveDesktop')).toBe(DESKTOP_INGAME_LAYOUT)
  })
})

describe('mobile reach-stick ring', () => {
  it('centers all discard rivers around the panel without overlapping rows', () => {
    const center = MOBILE_INGAME_LAYOUT.centerInfo
    const right = center.x + center.width
    const bottom = center.y + center.height
    const discardStarts = MOBILE_DISCARD_CENTER_INFO_OFFSETS.map(offset => ({
      x: center.x + offset.x,
      y: center.y + offset.y,
    }))

    expect(MOBILE_REACH_POSITIONS[0].y + 10).toBeLessThanOrEqual(bottom)
    expect(MOBILE_REACH_POSITIONS[1].x + 12).toBeLessThanOrEqual(right)
    expect(MOBILE_REACH_POSITIONS[2].y).toBeGreaterThanOrEqual(center.y)
    expect(MOBILE_REACH_POSITIONS[3].x).toBeGreaterThanOrEqual(center.x)

    expect(discardStarts[0].y).toBeLessThan(bottom)
    expect(discardStarts[0].x - center.x).toBe(24.5)
    expect(discardStarts[1].x - right).toBe(15)
    expect(discardStarts[2].y).toBeLessThan(center.y)
    expect(right - discardStarts[2].x).toBe(46)
    expect(center.x - discardStarts[3].x).toBe(46)

    const layoutScale = 1.2
    const tileScale = 0.7
    const horizontalTileWidth = 31 * tileScale
    const horizontalStep = MOBILE_INGAME_LAYOUT.discardStep[0].x * layoutScale
    const bottomRiverCenter = discardStarts[0].x + (9 * horizontalStep + horizontalTileWidth) / 2
    const topRiverCenter = discardStarts[2].x - 9 * horizontalStep + (9 * horizontalStep + horizontalTileWidth) / 2
    expect(bottomRiverCenter).toBeCloseTo(center.x + center.width / 2, 0)
    expect(topRiverCenter).toBeCloseTo(center.x + center.width / 2, 0)

    const verticalTileHeight = 43 * tileScale
    const verticalStep = MOBILE_INGAME_LAYOUT.discardStep[3].y * layoutScale
    const rightRiverCenter = discardStarts[1].y - 9 * verticalStep + (9 * verticalStep + verticalTileHeight) / 2
    const leftRiverCenter = discardStarts[3].y + (9 * verticalStep + verticalTileHeight) / 2
    expect(rightRiverCenter).toBeCloseTo(center.y + center.height / 2, 0)
    expect(leftRiverCenter).toBeCloseTo(center.y + center.height / 2, 0)

    expect(MOBILE_INGAME_LAYOUT.discardRowStep[0].y * layoutScale).toBe(32.4)
    expect(MOBILE_INGAME_LAYOUT.discardRowStep[1].x * layoutScale).toBeGreaterThan(45 * tileScale)

    expect(MOBILE_TOP_MELD_CENTER_INFO_OFFSET.x - center.width).toBe(-14)
    expect(MOBILE_TOP_MELD_CENTER_INFO_OFFSET.y).toBe(-48)
    expect(MOBILE_DEAD_WALL_SHIFT_X).toBe(24)
  })
})
