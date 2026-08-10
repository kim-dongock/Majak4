import { describe, expect, it } from 'vitest'
import { readGpAssetUpdate } from './gpReplenishment'

describe('readGpAssetUpdate', () => {
  it('reads the current GP asset title from modern protocol keys', () => {
    expect(readGpAssetUpdate({ gammoney: 100_000, slevel: '金持ち', nlevel: 7 })).toEqual({
      gamMoney: 100_000,
      slevel: '金持ち',
      nlevel: 7,
    })
  })

  it('reads the current GP asset title from legacy protocol keys', () => {
    expect(readGpAssetUpdate({ k34e: 500, k32e: '庶民', k33e: 2 })).toEqual({
      gamMoney: 500,
      slevel: '庶民',
      nlevel: 2,
    })
  })

  it('ignores missing asset values instead of replacing the current title', () => {
    expect(readGpAssetUpdate({ result: 1 })).toEqual({})
  })
})