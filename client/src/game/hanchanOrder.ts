export function reorderHanchanPlayers<T>(
  players: readonly T[],
  previousRoomPosToOdr: readonly number[],
  engineToRoom: readonly number[],
): Array<T | undefined> {
  const roomPositionPlayers = previousRoomPosToOdr.map(odr => players[odr])
  const engineOrderPlayers = Array<T | undefined>(players.length).fill(undefined)

  engineToRoom.forEach((roomPos, odr) => {
    if (Number.isInteger(roomPos) && roomPos >= 0 && roomPos < players.length && odr >= 0 && odr < players.length) {
      engineOrderPlayers[odr] = roomPositionPlayers[roomPos]
    }
  })

  return engineOrderPlayers
}