import { describe, expect, it } from 'vitest'
import { buildInitialHandIndices, inferInitialDealStart } from './replayInitialDeal'

describe('replay initial deal reconstruction', () => {
  it('infers the deal start from dealt tiles and the dora indicator', () => {
    const start = 118
    const indices = [
      ...Array.from({ length: 53 }, (_, offset) => (start + offset) % 136),
      (start + 130) % 136,
    ]

    expect(inferInitialDealStart(indices)).toBe(start)
  })

  it('reconstructs every seat even when recorded dice point at another wall break', () => {
    const actualStart = 50
    const available = new Set([
      ...Array.from({ length: 53 }, (_, offset) => (actualStart + offset) % 136),
      (actualStart + 130) % 136,
    ])

    const inferredStart = inferInitialDealStart([...available])
    expect(inferredStart).toBe(actualStart)
    expect([0, 1, 2, 3].map(seat => buildInitialHandIndices(seat, 0, inferredStart!).filter(index => available.has(index)).length))
      .toEqual([14, 13, 13, 13])
  })

  it('does not infer from a player-specific partial snapshot', () => {
    expect(inferInitialDealStart(Array.from({ length: 14 }, (_, index) => index))).toBeUndefined()
  })
})