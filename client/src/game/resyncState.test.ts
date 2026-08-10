import { describe, expect, it } from 'vitest'
import { canCompleteGameResync, restoreVisiblePaiCodes, type GameResyncGate } from './resyncState'

const COMPLETE_GATE: GameResyncGate = {
  restorePending: true,
  inFlight: true,
  invokeResolved: true,
  snapshotReceived: true,
  historyReceived: true,
  historyApplied: true,
}

describe('restoreVisiblePaiCodes', () => {
  it('restores every visible physical tile after round state is cleared', () => {
    const knownPai = new Map<number, number>([[75, 0x11]])
    knownPai.clear()

    restoreVisiblePaiCodes(knownPai, [
      { bipaiIndex: 75, code: 0x25 },
      { bipaiIndex: 88, code: 0x31 },
      { code: 0x01 },
    ])

    expect([...knownPai.entries()]).toEqual([[75, 0x25], [88, 0x31]])
  })
})

describe('canCompleteGameResync', () => {
  it('requires an authoritative hand snapshot for a player', () => {
    expect(canCompleteGameResync({ ...COMPLETE_GATE, snapshotReceived: false }, false)).toBe(false)
    expect(canCompleteGameResync(COMPLETE_GATE, false)).toBe(true)
  })

  it('completes viewer resync from applied history without a player snapshot', () => {
    const viewerGate = { ...COMPLETE_GATE, snapshotReceived: false }
    expect(canCompleteGameResync({ ...viewerGate, historyApplied: false }, true)).toBe(false)
    expect(canCompleteGameResync(viewerGate, true)).toBe(true)
  })

  it('does not complete a viewer before history arrives', () => {
    expect(canCompleteGameResync({
      ...COMPLETE_GATE,
      snapshotReceived: false,
      historyReceived: false,
      historyApplied: false,
    }, true)).toBe(false)
  })
})