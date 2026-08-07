export type AssistTileMask = 0 | 1 | 2

export interface TouchTileDecision {
  selectedIdx: number
  confirmDiscard: boolean
}

export interface DiscardSourceDecision {
  isTedashi: boolean
  displayIdx: number
}

export function waitGuideWorldY(
  handTop: number,
  visibleTop: number,
  visibleBottom: number,
  guideHeight = 85,
  actionButtonHeight = 40,
  gap = 8,
): number {
  const paddedMinY = visibleTop + gap
  const paddedMaxY = visibleBottom - guideHeight - gap
  const minY = paddedMaxY >= paddedMinY ? paddedMinY : visibleTop
  const maxY = paddedMaxY >= paddedMinY ? paddedMaxY : Math.max(visibleTop, visibleBottom - guideHeight)
  const desiredY = handTop - guideHeight - actionButtonHeight - gap * 2
  return Math.min(Math.max(desiredY, minY), maxY)
}

export function assistTileMask(sourceCode: number, targetCode: number): AssistTileMask {
  if (sourceCode <= 0 || targetCode <= 0) return 0
  const sourceKind = (sourceCode >> 4) & 0xF
  const sourceNumber = sourceCode & 0xF
  const targetKind = (targetCode >> 4) & 0xF
  const targetNumber = targetCode & 0xF
  if (sourceKind !== targetKind) return 0
  if (sourceNumber === targetNumber) return 2
  if (sourceKind < 3 && Math.abs(sourceNumber - targetNumber) === 1) return 1
  return 0
}

export function decideTouchTileAction(selectedIdx: number, tappedIdx: number): TouchTileDecision {
  return {
    selectedIdx: tappedIdx,
    confirmDiscard: selectedIdx === tappedIdx,
  }
}

export function decideDiscardSource(
  handCount: number,
  handIdx: number,
  afterCall: boolean,
  concealOpponentPosition: boolean,
  seed: number,
): DiscardSourceDecision {
  const lastIdx = Math.max(0, handCount - 1)
  const isTedashi = afterCall || handIdx !== lastIdx
  if (!isTedashi || !concealOpponentPosition) return { isTedashi, displayIdx: Math.max(0, handIdx) }
  const concealedSlots = Math.max(1, handCount - 1)
  const mixedSeed = Math.abs(Math.imul(seed || 1, 1103515245) + 12345)
  return { isTedashi, displayIdx: mixedSeed % concealedSlots }
}