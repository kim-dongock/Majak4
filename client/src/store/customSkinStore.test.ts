import { beforeEach, describe, expect, it } from 'vitest'
import { useCustomSkinStore } from './customSkinStore'

describe('customSkinStore', () => {
  beforeEach(() => {
    useCustomSkinStore.getState().setEquips([])
  })

  it('hydrates equipped board and tile skins from the channel response', () => {
    const equip = useCustomSkinStore.getState().setEquips([
      { customType: 12, customId: 16 },
      { customType: 20, customId: 20 },
    ])

    expect(equip).toMatchObject({
      bgId: 16,
      bgType: 12,
      haiId: 20,
      haiType: 20,
    })
    expect(useCustomSkinStore.getState()).toMatchObject(equip)
  })
})