export interface NoticeDisplay {
  text: string
  color: string
  durationMs: number
}

function colorRefToCss(value: number): string {
  const r = value & 0xff
  const g = (value >> 8) & 0xff
  const b = (value >> 16) & 0xff
  return `rgb(${r},${g},${b})`
}

function readNoticeColor(data: Record<string, unknown>): string {
  const rawColor = data.k40e ?? data.color
  if (typeof rawColor === 'string' && rawColor.trim().startsWith('#')) return rawColor
  const color = Number(rawColor ?? 0)
  if (Number.isFinite(color) && color > 0) return colorRefToCss(color)

  const level = String(data.k83e ?? data.noticeLevel ?? '')
  if (level === 'v26e') return 'rgb(225,225,254)'
  return level === 'v24e' ? 'rgb(254,254,254)' : 'rgb(254,225,225)'
}

export function readNoticePayload(data: Record<string, unknown>): NoticeDisplay | null {
  const text = String(data.k41e ?? data.message ?? data.text ?? '')
  if (!text) return null
  const sec = Number(data.k86e ?? data.noticeSec ?? 10)
  return {
    text,
    color: readNoticeColor(data),
    durationMs: Math.max(1000, (Number.isFinite(sec) && sec > 0 ? sec : 10) * 1000),
  }
}