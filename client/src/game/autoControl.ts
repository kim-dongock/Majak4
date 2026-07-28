export type AutoControlState = {
  prox: boolean
  autoTap: boolean
  autoPass: boolean
  autoHora: boolean
}

export type AutoControlAction = 'Tap' | 'Pass' | 'Ron' | 'Tsumo'

export function resolveAutoControlAction(autoControl: AutoControlState, acts: string[]): AutoControlAction | null {
  const visibleActs = new Set(acts)

  if (autoControl.autoHora && (visibleActs.has('Ron') || visibleActs.has('Tsumo'))) {
    return visibleActs.has('Tsumo') ? 'Tsumo' : 'Ron'
  }

  if (autoControl.prox) {
    if (visibleActs.has('Tap')) return 'Tap'
    if (visibleActs.has('Pass')) return 'Pass'
  }

  if (autoControl.autoPass && visibleActs.has('Pass') && !visibleActs.has('Ron') && acts.length > 1) {
    return 'Pass'
  }

  if (
    autoControl.autoTap &&
    visibleActs.has('Tap') &&
    !visibleActs.has('Tsumo') &&
    !visibleActs.has('Kan') &&
    !visibleActs.has('Hua') &&
    !visibleActs.has('Tao')
  ) {
    return 'Tap'
  }

  return null
}