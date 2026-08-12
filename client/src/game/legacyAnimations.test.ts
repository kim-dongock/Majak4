import { describe, expect, it } from 'vitest'
import { getLegacyKyoResultDelayMs } from './legacyAnimations'

function result(totalTen: number, options: { pinType?: number; yakuman?: boolean; trickTitle?: number } = {}) {
  return {
    pinType: options.pinType ?? 0,
    players: [{ isHora: true, trickTitle: options.trickTitle }],
    totalsByPlayer: { 0: { totalTen } },
    yakuByPlayer: { 0: options.yakuman ? [{ isYakuman: true }] : [{ isYakuman: false }] },
  }
}

describe('getLegacyKyoResultDelayMs', () => {
  it('uses the 11 frame duration for a basic ron', () => {
    expect(getLegacyKyoResultDelayMs(result(2000))).toBe(330)
  })

  it('adds the hora-fire sequence after a mangan-class tsumo', () => {
    expect(getLegacyKyoResultDelayMs(result(3000, { pinType: 1 }))).toBe(1040)
  })

  it('includes staggered ron, fire, and fan sequences for yakuman', () => {
    expect(getLegacyKyoResultDelayMs(result(8000, { yakuman: true }))).toBe(4670)
  })

  it('uses L2 plus its final wait instead of hora fire', () => {
    expect(getLegacyKyoResultDelayMs(result(3000, { pinType: 1, trickTitle: 5 }))).toBe(1810)
  })
})