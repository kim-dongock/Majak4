export const LEGACY_EFFECT_FRAME_MS = 30
export const LEGACY_HORA_FIRE_DURATION_MS = 650
export const LEGACY_YAKUMAN_FINISH_DURATION_MS = 1950

export const LEGACY_REACH_FRAME_DELAYS = {
  1: [50, 50, 50, 50, 50, 120, 120, 120, 120],
  2: [50, 50, 50, 50, 50, 120, 120, 120, 120, 120, 70, 70, 70],
} as const

export const LEGACY_COSTUME_FRAME_COUNTS = {
  9: { default: 14, chi: 25, kan: 25, pon: 25, reach: 15, ron: 20, tsumo: 20 },
  10: { default: 11, chi: 15, kan: 15, pon: 15, reach: 17, ron: 25, tsumo: 25 },
  11: { default: 2, chi: 11, kan: 11, pon: 11, reach: 18, ron: 23, tsumo: 23 },
} as const

export type LegacyCostumeId = keyof typeof LEGACY_COSTUME_FRAME_COUNTS
export type LegacyCostumeAction = keyof (typeof LEGACY_COSTUME_FRAME_COUNTS)[LegacyCostumeId]

export interface LegacyHoraPresentation {
  odr: number
  pinType: 0 | 1
  totalTen: number
  isYakuman: boolean
  level: 1 | 2 | 3
  scoreEffectDurationMs: number
  level2SkillDurationMs: number
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value != null && typeof value === 'object'
}

function asBoolean(value: unknown): boolean {
  if (typeof value === 'boolean') return value
  if (typeof value === 'number') return Number.isFinite(value) && value !== 0
  return typeof value === 'string' && (value === '1' || value.toLowerCase() === 'true')
}

export function numberedLegacyKeys(prefix: string, count: number, start = 0, separator = '_'): string[] {
  return Array.from({ length: count }, (_, index) => `${prefix}${separator}${String(start + index).padStart(2, '0')}`)
}

function level2SkillDurationMs(trickTitle: unknown): number {
  const title = Number(trickTitle ?? 0)
  const attribute = Math.trunc((title - 1) / 3) - 1
  if (attribute < 0 || attribute >= 4 || title % 3 < 2) return 0
  return [920, 1000, 1000, 1000][attribute] + 500
}

export function getLegacyHoraPresentations(data: Record<string, unknown>): LegacyHoraPresentation[] {
  const pinType = Number(data.pinType ?? -1)
  if (pinType !== 0 && pinType !== 1) return []
  const resultPlayers = Array.isArray(data.players) ? data.players : []
  const totalsByPlayer = isRecord(data.totalsByPlayer) ? data.totalsByPlayer : {}
  const yakuByPlayer = isRecord(data.yakuByPlayer) ? data.yakuByPlayer : {}

  return resultPlayers.flatMap((value, odr) => {
    const player = isRecord(value) ? value : {}
    const yakuValue = yakuByPlayer[String(odr)]
    const yaku: unknown[] = Array.isArray(yakuValue) ? yakuValue : []
    if (!asBoolean(player.isHora) && yaku.length === 0) return []
    const totalsValue = totalsByPlayer[String(odr)]
    const totals: Record<string, unknown> = isRecord(totalsValue) ? totalsValue : {}
    const totalTen = Number(totals.totalTen ?? 0)
    const isYakuman = yaku.some(item => isRecord(item) && asBoolean(item.isYakuman))
    const level: 1 | 2 | 3 = isYakuman || totalTen >= 4000 ? 3 : totalTen >= 3000 ? 2 : 1
    const scoreEffectDurationMs = pinType === 0
      ? isYakuman
        ? 69 * LEGACY_EFFECT_FRAME_MS
        : totalTen >= 6000
          ? 39 * LEGACY_EFFECT_FRAME_MS
          : (level === 3 ? 27 : level === 2 ? 20 : 11) * LEGACY_EFFECT_FRAME_MS
      : (isYakuman ? 30 : level === 3 ? 15 : level === 2 ? 13 : 12) * LEGACY_EFFECT_FRAME_MS
    return [{
      odr,
      pinType,
      totalTen,
      isYakuman,
      level,
      scoreEffectDurationMs,
      level2SkillDurationMs: isYakuman || totalTen >= 3000
        ? level2SkillDurationMs(player.trickTitle ?? player.mjkk46e)
        : 0,
    }]
  })
}

export function getLegacyKyoResultDelayMs(data: Record<string, unknown>): number {
  const presentations = getLegacyHoraPresentations(data)
  if (presentations.length === 0) return 0
  const scoreDuration = Math.max(...presentations.map(item => item.scoreEffectDurationMs))
  const hasHighHora = presentations.some(item => item.isYakuman || item.totalTen >= 3000)
  const hasYakuman = presentations.some(item => item.isYakuman)
  const skillDuration = Math.max(...presentations.map(item => item.level2SkillDurationMs))
  return scoreDuration
    + (skillDuration || (hasHighHora ? LEGACY_HORA_FIRE_DURATION_MS : 0))
    + (hasYakuman ? LEGACY_YAKUMAN_FINISH_DURATION_MS : 0)
}