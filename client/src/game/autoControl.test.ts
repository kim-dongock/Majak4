import { describe, expect, it } from 'vitest'
import { calculateTimeBankSegments, getLegacyTimerColors, resolveLegacyTimerMode } from './autoControl'

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

describe('legacy timer presentation', () => {
  const manual = { prox: false, autoTap: false, autoPass: false, autoHora: false }

  it('uses the legacy input colors for a normal turn', () => {
    expect(resolveLegacyTimerMode('Turn', ['Tap'], true, manual)).toBe('input')
    expect(getLegacyTimerColors('input')).toEqual({ bank: 0x0000ff, turn: 0x0080ff, keep: 0x00ffff })
  })

  it('uses a red bank and light-blue response segment before extension', () => {
    expect(resolveLegacyTimerMode('Furo', ['Pass', 'Pon'], false, manual)).toBe('extend')
    expect(getLegacyTimerColors('extend')).toEqual({ bank: 0xff0000, turn: 0x0080ff, keep: 0x00ffff })
  })

  it('uses the legacy automatic colors for auto pass', () => {
    const autoPass = { ...manual, autoPass: true }
    expect(resolveLegacyTimerMode('Furo', ['Pass', 'Pon'], false, autoPass)).toBe('auto')
    expect(getLegacyTimerColors('auto')).toEqual({ bank: 0xff0000, turn: 0xff8080, keep: 0xffffff })
    expect(resolveLegacyTimerMode('Furo', ['Pass', 'Pon'], true, autoPass)).toBe('input')
  })

  it('keeps ron prompts in input mode', () => {
    expect(resolveLegacyTimerMode('Furo', ['Pass', 'Ron'], false, manual)).toBe('input')
  })
})
