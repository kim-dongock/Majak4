import { describe, expect, it } from 'vitest'
import { reorderHanchanPlayers } from './hanchanOrder'

describe('reorderHanchanPlayers', () => {
  it('reorders room-position players for the first hanchan', () => {
    expect(reorderHanchanPlayers(
      ['room-0', 'room-1', 'room-2', 'room-3'],
      [0, 1, 2, 3],
      [2, 0, 3, 1],
    )).toEqual(['room-2', 'room-0', 'room-3', 'room-1'])
  })

  it('restores room positions before applying a consecutive hanchan shuffle', () => {
    const firstEngineOrder = ['room-2', 'room-0', 'room-3', 'room-1']
    const firstRoomPosToOdr = [1, 3, 0, 2]

    expect(reorderHanchanPlayers(
      firstEngineOrder,
      firstRoomPosToOdr,
      [1, 3, 0, 2],
    )).toEqual(['room-1', 'room-3', 'room-0', 'room-2'])
  })
})