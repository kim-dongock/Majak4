export type AutoControlState = {
  prox: boolean
  autoTap: boolean
  autoPass: boolean
  autoHora: boolean
}

export type AutoControlAction = 'Tap' | 'Pass' | 'Ron' | 'Tsumo'

export type TimeBankSegments = {
  bankMs: number
  turnMs: number
  keepMs: number
}

export const GAME_AUTO_CONTROL_EVENT = 'majak:auto-control'
export const GAME_KYOKU_STARTED_EVENT = 'majak:kyoku-started'
export const GAME_AUTO_PASS_HOLD_EVENT = 'majak:auto-pass-hold'
export const GAME_LOCAL_REACH_EVENT = 'majak:local-reach'

const LEGACY_KEEP_MS = [100, 500, 1000, 1200] as const

export function shouldEnableAutoPassAtKyokuStart(mode: number, roomOption: string): boolean {
  if (mode <= 0) return false
  if (mode >= 2) return true
  const speedNo = Number(roomOption[2])
  const keepMs = LEGACY_KEEP_MS[Number.isInteger(speedNo) && speedNo >= 0 && speedNo < LEGACY_KEEP_MS.length ? speedNo : 2]
  return keepMs > 500
}

export function resetAutoControlForNewKyoku(
  autoControl: AutoControlState,
  autoPassAtKyokuStart = false,
): AutoControlState {
  if (autoControl.prox) return autoControl

  return {
    ...autoControl,
    autoTap: false,
    autoPass: autoPassAtKyokuStart,
    autoHora: false,
  }
}

export function enableAutoTapAfterReach(autoControl: AutoControlState, enabled: boolean): AutoControlState {
  if (!enabled || autoControl.prox || autoControl.autoTap) return autoControl
  return { ...autoControl, autoTap: true }
}

export function getAutoControlDelayMs(action: AutoControlAction, roomOption: string): number {
  if (action === 'Ron' || action === 'Tsumo') return 50

  const speedNo = Number(roomOption[2])
  return LEGACY_KEEP_MS[Number.isInteger(speedNo) && speedNo >= 0 && speedNo < LEGACY_KEEP_MS.length ? speedNo : 2]
}

export function shouldSuspendAutoPassForPrompt(
  autoControl: AutoControlState,
  acts: string[],
  isTurnMode: boolean,
): boolean {
  return !autoControl.prox
    && !isTurnMode
    && resolveAutoControlAction(autoControl, acts) === 'Pass'
}

export function calculateTimeBankSegments(
  remainingMs: number,
  baseTimeMs: number,
  keepTimeMs: number,
  availableBankMs: number,
  timeBankEnabled: boolean,
): TimeBankSegments {
  const baseMs = Math.max(0, baseTimeMs)
  const remaining = Math.max(0, remainingMs)
  const bankMs = Math.max(0, availableBankMs)
  const issuedTotalMs = baseMs + (timeBankEnabled ? bankMs : 0)
  const elapsedMs = Math.max(0, issuedTotalMs - remaining)
  const baseRemainingMs = Math.max(0, baseMs - elapsedMs)
  const keepMs = Math.max(0, Math.min(baseRemainingMs, keepTimeMs - elapsedMs))

  return {
    bankMs: timeBankEnabled ? Math.max(0, bankMs - Math.max(0, elapsedMs - baseMs)) : bankMs,
    turnMs: Math.max(0, baseRemainingMs - keepMs),
    keepMs,
  }
}

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