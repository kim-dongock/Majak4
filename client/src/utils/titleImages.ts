const TITLE_IMAGE_ROOT = '/assets/images/game'

function padTitleCode(code: number) {
  return String(Math.trunc(code)).padStart(3, '0')
}

export function getMajakTitleImageUrl(titleId?: string | number) {
  if (typeof titleId === 'number') {
    if (!Number.isFinite(titleId) || titleId <= 0) return ''
    return titleId >= 1000
      ? `${TITLE_IMAGE_ROOT}/mj_ctitle_${padTitleCode(titleId - 1000)}.png`
      : `${TITLE_IMAGE_ROOT}/mj_title_${padTitleCode(titleId)}.png`
  }

  const normalized = titleId?.trim().toLowerCase() ?? ''
  if (normalized.startsWith('mjkc')) {
    const code = Number(normalized.slice(4))
    return code > 0 ? `${TITLE_IMAGE_ROOT}/mj_ctitle_${padTitleCode(code)}.png` : ''
  }
  if (normalized.startsWith('mjkt')) {
    const code = Number(normalized.slice(4))
    return code > 0 ? `${TITLE_IMAGE_ROOT}/mj_title_${padTitleCode(code)}.png` : ''
  }
  return ''
}

export function getTrickTitleImageUrl(titleId?: string | number) {
  const code = typeof titleId === 'number'
    ? titleId
    : titleId?.trim().toLowerCase().startsWith('mjks')
      ? Number(titleId.trim().slice(4))
      : 0
  return Number.isFinite(code) && code > 0
    ? `${TITLE_IMAGE_ROOT}/mj_skill_${padTitleCode(code)}.png`
    : ''
}