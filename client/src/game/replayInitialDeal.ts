const TILE_COUNT = 136
const INITIAL_DEAL_TILE_COUNT = 53
const DORA_OFFSET = 130

function mod(value: number): number {
  return ((value % TILE_COUNT) + TILE_COUNT) % TILE_COUNT
}

export function buildInitialHandIndices(openPos: number, oyaOrder: number, haipaiPos: number): number[] {
  const indices: number[] = []
  let seatOrder = oyaOrder
  let offset = 0
  for (let round = 0; round < 3; round++) {
    for (let player = 0; player < 4; player++) {
      for (let count = 0; count < 4; count++) {
        if (seatOrder === openPos) indices.push(mod(haipaiPos + offset))
        offset++
      }
      seatOrder = (seatOrder + 1) % 4
    }
  }
  for (let player = 0; player < 4; player++) {
    if (seatOrder === openPos) indices.push(mod(haipaiPos + offset))
    offset++
    seatOrder = (seatOrder + 1) % 4
  }
  if (openPos === oyaOrder) indices.push(mod(haipaiPos + offset))
  return indices
}

export function inferInitialDealStart(bipaiIndices: readonly number[]): number | undefined {
  const available = new Set(bipaiIndices.filter(index => Number.isInteger(index) && index >= 0 && index < TILE_COUNT))
  if (available.size < INITIAL_DEAL_TILE_COUNT + 1) return undefined

  for (let candidate = 0; candidate < TILE_COUNT; candidate++) {
    let complete = true
    for (let offset = 0; offset < INITIAL_DEAL_TILE_COUNT; offset++) {
      if (!available.has(mod(candidate + offset))) {
        complete = false
        break
      }
    }
    if (complete && available.has(mod(candidate + DORA_OFFSET))) return candidate
  }
  return undefined
}