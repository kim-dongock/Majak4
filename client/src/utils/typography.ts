const UI_FONT_FAMILY_FALLBACK = "'Hiragino Kaku Gothic ProN', 'Yu Gothic UI', Meiryo, 'Noto Sans JP', sans-serif"

export function getUiFontFamily(): string {
  if (typeof document === 'undefined') return UI_FONT_FAMILY_FALLBACK

  return getComputedStyle(document.documentElement)
    .getPropertyValue('--majak-font-family-ui')
    .trim() || UI_FONT_FAMILY_FALLBACK
}

export function getUiTypeScale(): number {
  if (typeof document === 'undefined') return 1

  const scale = Number(getComputedStyle(document.documentElement)
    .getPropertyValue('--majak-type-scale')
    .trim())
  return Number.isFinite(scale) && scale > 0 ? scale : 1
}

export function getUiFontSizePx(basePx: number): number {
  return basePx * getUiTypeScale()
}

export function getUiFontSize(basePx: number): string {
  return `${getUiFontSizePx(basePx)}px`
}