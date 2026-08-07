import { describe, expect, it } from 'vitest'
import { assistTileMask, decideDiscardSource, decideTouchTileAction, waitGuideWorldY } from './assistLogic'

describe('waitGuideWorldY', () => {
  it('places the full guide above the hand and action buttons', () => {
    const y = waitGuideWorldY(589, 214, 661)

    expect(y).toBe(448)
    expect(y + 85).toBeLessThanOrEqual(589 - 40 - 8)
  })

  it('keeps the guide inside shallow mobile viewports', () => {
    expect(waitGuideWorldY(475, 169, 536)).toBe(334)
    expect(waitGuideWorldY(260, 220, 320)).toBe(220)
  })
})

describe('assistTileMask', () => {
  it('uses the strong mask for the same tile', () => {
    expect(assistTileMask(0x15, 0x15)).toBe(2)
  })

  it('uses the weak mask only for adjacent numbered tiles in the same suit', () => {
    expect(assistTileMask(0x15, 0x14)).toBe(1)
    expect(assistTileMask(0x15, 0x16)).toBe(1)
    expect(assistTileMask(0x15, 0x13)).toBe(0)
    expect(assistTileMask(0x15, 0x25)).toBe(0)
  })

  it('does not treat neighboring honor codes as adjacent tiles', () => {
    expect(assistTileMask(0x31, 0x31)).toBe(2)
    expect(assistTileMask(0x31, 0x32)).toBe(0)
  })
})

describe('decideTouchTileAction', () => {
  it('selects a tile on the first touch without discarding it', () => {
    expect(decideTouchTileAction(-1, 4)).toEqual({ selectedIdx: 4, confirmDiscard: false })
  })

  it('confirms discard only when the selected tile is touched again', () => {
    expect(decideTouchTileAction(4, 4)).toEqual({ selectedIdx: 4, confirmDiscard: true })
    expect(decideTouchTileAction(4, 7)).toEqual({ selectedIdx: 7, confirmDiscard: false })
  })
})

describe('decideDiscardSource', () => {
  it('distinguishes tedashi from tsumogiri for a normal discard', () => {
    expect(decideDiscardSource(14, 5, false, false, 100)).toEqual({ isTedashi: true, displayIdx: 5 })
    expect(decideDiscardSource(14, 13, false, false, 100)).toEqual({ isTedashi: false, displayIdx: 13 })
  })

  it('treats a discard after a call as tedashi even from the final slot', () => {
    expect(decideDiscardSource(14, 13, true, false, 100)).toEqual({ isTedashi: true, displayIdx: 13 })
  })

  it('conceals an opponent tedashi position while preserving its source type', () => {
    const first = decideDiscardSource(14, 13, true, true, 713)
    const repeated = decideDiscardSource(14, 13, true, true, 713)

    expect(first).toEqual(repeated)
    expect(first.isTedashi).toBe(true)
    expect(first.displayIdx).toBeGreaterThanOrEqual(0)
    expect(first.displayIdx).toBeLessThan(13)
  })

  it('does not obfuscate a tsumogiri marker', () => {
    expect(decideDiscardSource(14, 13, false, true, 713)).toEqual({ isTedashi: false, displayIdx: 13 })
  })
})