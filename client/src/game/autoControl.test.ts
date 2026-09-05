import { describe, expect, it } from 'vitest'
import { calculateTimeBankSegments } from './autoControl'

describe('calculateTimeBankSegments', () => {
  const baseTimeMs = 2_000
  const keepTimeMs = 100
  const bankTimeMs = 20_000

  it('starts with the full keep, turn, and bank segments', () => {
    expect(calculateTimeBankSegments(22_000, baseTimeMs, keepTimeMs, bankTimeMs, true)).toEqual({
      bankMs: 20_000,
      turnMs: 1_900,
      keepMs: 100,
    })
  })

  it('consumes keep time before turn time', () => {
    expect(calculateTimeBankSegments(21_950, baseTimeMs, keepTimeMs, bankTimeMs, true)).toEqual({
      bankMs: 20_000,
      turnMs: 1_900,
      keepMs: 50,
    })
    expect(calculateTimeBankSegments(20_000, baseTimeMs, keepTimeMs, bankTimeMs, true)).toEqual({
      bankMs: 20_000,
      turnMs: 0,
      keepMs: 0,
    })
  })

  it('consumes the dark time-bank segment only after base time', () => {
    expect(calculateTimeBankSegments(19_000, baseTimeMs, keepTimeMs, bankTimeMs, true)).toEqual({
      bankMs: 19_000,
      turnMs: 0,
      keepMs: 0,
    })
  })
})
