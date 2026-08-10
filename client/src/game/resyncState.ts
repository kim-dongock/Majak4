export interface VisiblePaiCode {
  code: number
  bipaiIndex?: number
}

export interface GameResyncGate {
  restorePending: boolean
  inFlight: boolean
  invokeResolved: boolean
  snapshotReceived: boolean
  historyReceived: boolean
  historyApplied: boolean
}

export function canCompleteGameResync(gate: GameResyncGate, isViewer: boolean): boolean {
  if (!gate.restorePending || !gate.inFlight || !gate.invokeResolved) return false
  if (isViewer) return gate.historyReceived && gate.historyApplied
  if (!gate.snapshotReceived) return false
  return !gate.historyReceived || gate.historyApplied
}

export function restoreVisiblePaiCodes(target: Map<number, number>, tiles: readonly VisiblePaiCode[]): void {
  tiles.forEach(tile => {
    if (tile.bipaiIndex !== undefined && tile.bipaiIndex >= 0) target.set(tile.bipaiIndex, tile.code)
  })
}