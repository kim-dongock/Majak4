import { describe, expect, it } from 'vitest'
import { getLegacyBoardImageUrl, getLegacyBoardSkinId, getLegacyFullUiSkinId, getLegacyHaiSkinId } from './legacySkinPalette'

describe('board skin selection', () => {
  it('uses the base legacy board when no background skin is equipped', () => {
    expect(getLegacyBoardSkinId(0, 0)).toBeUndefined()
    expect(getLegacyBoardImageUrl(0, 0)).toBe('/assets/images/game/mj_board.png')
  })

  it('preserves a standard board ID even when its master kind is 12', () => {
    expect(getLegacyBoardSkinId(16, 12)).toBe(16)
    expect(getLegacyBoardImageUrl(16, 12)).toBe('/assets/images/game/skin/16/mj_board_16.png')
    expect(getLegacyFullUiSkinId(16, 12)).toBeUndefined()
  })

  it('uses full UI resources only for the explicit premium board IDs', () => {
    expect(getLegacyFullUiSkinId(100001, 11)).toBe(100001)
    expect(getLegacyFullUiSkinId(100002, 12)).toBe(100002)
  })
})

describe('getLegacyHaiSkinId', () => {
  it.each([1, 2, 3, 4, 20, 21, 22, 23, 27, 28, 100004, 100005])(
    'selects equipped tile skin %i',
    customHaiId => {
      expect(getLegacyHaiSkinId(customHaiId)).toBe(customHaiId)
    },
  )

  it.each([0, 100003, 999999, undefined])(
    'falls back for unsupported tile skin %s',
    customHaiId => {
      expect(getLegacyHaiSkinId(customHaiId)).toBeUndefined()
    },
  )
})