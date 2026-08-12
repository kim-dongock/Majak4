import { describe, expect, it } from 'vitest'
import { assistTileMask, decideAutoDiscardDelayMs, decideDiscardSource, decideTimedDiscardIndex, decideTouchTileAction, DISCARD_SOURCE_MARKER_DEPTH, offsetDiscardSourceMarker, waitGuideWorldY } from './assistLogic'

describe('DISCARD_SOURCE_MARKER_DEPTH', () => {
  it('keeps the marker behind hand tiles like legacy z=-1', () => {
    expect(DISCARD_SOURCE_MARKER_DEPTH).toBe(-1)
    expect(DISCARD_SOURCE_MARKER_DEPTH).toBeLessThan(10)
  })
})

describe('offsetDiscardSourceMarker', () => {
  it('applies the legacy right-hand open offset at the rendered scale', () => {
    expect(offsetDiscardSourceMarker({ x: 800, y: 200 }, { x: -15, y: 18 }, 0.75))
      .toEqual({ x: 788.75, y: 213.5 })
  })
})

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

describe('decideTimedDiscardIndex', () => {
  it('prioritizes the tile selected by the first mobile touch', () => {
    expect(decideTimedDiscardIndex(Array.from({ length: 14 }, (_, index) => index), 5)).toBe(5)
  })

  it('keeps an off-turn selection after the hand order changes', () => {
    expect(decideTimedDiscardIndex([101, 103, 102, 104], 1, 102)).toBe(2)
  })

  it('falls back to the final tile when there is no valid selection', () => {
    const hand = Array.from({ length: 14 }, (_, index) => index)
    expect(decideTimedDiscardIndex(hand, -1)).toBe(13)
    expect(decideTimedDiscardIndex(hand, 14)).toBe(13)
  })
})

describe('mobile preselected timeout discard', () => {
  it('sends the preselected tile before the server deadline after the hand is reordered', () => {
    const selectedBipaiIndex = 102
    const reorderedHand = [101, 103, 104, 105, selectedBipaiIndex]
    const localNow = 5_000
    const localDeadlineAt = 9_400

    expect(decideAutoDiscardDelayMs(localDeadlineAt, localNow, 5)).toBe(4_150)
    expect(localNow + decideAutoDiscardDelayMs(localDeadlineAt, localNow, 5)).toBeLessThan(localDeadlineAt)
    expect(decideTimedDiscardIndex(reorderedHand, 1, selectedBipaiIndex)).toBe(4)
  })

  it('runs immediately when less than the send lead remains', () => {
    expect(decideAutoDiscardDelayMs(5_100, 5_000, 1)).toBe(0)
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