/** Majak 共通キャラクター画像。全画面で同じ選択 URL を使用する。 */
export const LOCAL_AVATAR_BASE = '/assets/images/characters'
const HANGE_AVATAR_BASE = 'https://avatar.hange.jp/IMG_AVTR'
const HANGE_SHORT_AVATAR_BASE = 'https://alpha-avatar.hange.jp/IMG_AVTR'
const THUMBNAIL_FILE_RE = /thumbnail_\d{2}[mf]\.png$/i
export const MALE_AVATARS = Array.from(
  { length: 16 },
  (_, index) => `${LOCAL_AVATAR_BASE}/thumbnail_${String(index + 1).padStart(2, '0')}m.png`,
)
export const FEMALE_AVATARS = Array.from(
  { length: 16 },
  (_, index) => `${LOCAL_AVATAR_BASE}/thumbnail_${String(index + 1).padStart(2, '0')}f.png`,
)

type AvatarSex = 'male' | 'female'

function extractThumbnailFileName(sourceUrl: string): string | null {
  const tail = sourceUrl.split('?')[0] ?? sourceUrl
  const fileName = tail.split('/').pop() ?? ''
  return THUMBNAIL_FILE_RE.test(fileName) ? fileName.toLowerCase() : null
}

export function normalizeAvatarUrl(sourceUrl: string): string {
  if (!sourceUrl) return sourceUrl
  if (sourceUrl.startsWith(`${LOCAL_AVATAR_BASE}/`)) return sourceUrl
  const fileName = extractThumbnailFileName(sourceUrl)
  if (!fileName) return sourceUrl
  return `${LOCAL_AVATAR_BASE}/${fileName}`
}

function toAvatarProxyUrl(sourceUrl: string): string {
  return normalizeAvatarUrl(sourceUrl)
}

type HangeAvatarVersion = 1 | 2 | 3

function getAvatarVersion(avatarId: string): HangeAvatarVersion {
  const first = avatarId.charAt(0).toUpperCase()
  if (first === '2') return 3
  if (first === '1') return 2
  return 1
}

function resolveLocalAvatarUrl(avatarId: string | null | undefined): string | undefined {
  if (avatarId) {
    const normalized = toAvatarProxyUrl(avatarId)
    if (normalized !== avatarId || normalized.startsWith(`${LOCAL_AVATAR_BASE}/`)) return normalized
    if (/^https?:\/\//i.test(avatarId)) return avatarId
  }
  return undefined
}

function hangeAvatarUrl(avatarId: string | null | undefined, size: 'web' | 'game' | 'short'): string {
  const localUrl = resolveLocalAvatarUrl(avatarId)
  if (localUrl) return localUrl
  const normalizedId = avatarId?.trim()
  if (!normalizedId) return getDefaultAvatarUrl('male')

  const version = getAvatarVersion(normalizedId)
  const animation = version === 3 ? 'S' : version === 2 ? 'A' : ''
  const prefix = size === 'web'
    ? `AWF${animation}`
    : size === 'game'
      ? version === 3 ? 'AGBS' : `AGF${animation}`
      : `ACH${animation}`
  const base = size === 'short' ? HANGE_SHORT_AVATAR_BASE : HANGE_AVATAR_BASE
  return `${base}/${prefix}_${normalizedId}.GIF`
}

export function getAvatarUrl(avatarId: string | null | undefined): string {
  return hangeAvatarUrl(avatarId, 'web')
}

export function getGameAvatarUrl(avatarId: string | null | undefined): string {
  return hangeAvatarUrl(avatarId, 'game')
}

export function getHighResolutionGameAvatarUrl(avatarId: string | null | undefined): string {
  return hangeAvatarUrl(avatarId, 'web')
}

export function getWebHalfAvatarUrl(avatarId: string | null | undefined): string {
  const localUrl = resolveLocalAvatarUrl(avatarId)
  if (localUrl) return localUrl
  const normalizedId = avatarId?.trim()
  if (!normalizedId) return getDefaultAvatarUrl('male')
  const version = getAvatarVersion(normalizedId)
  const animation = version === 3 ? 'S' : version === 2 ? 'A' : ''
  return `${HANGE_AVATAR_BASE}/AWH${animation}_${normalizedId}.GIF`
}

export function getShortAvatarUrl(avatarId: string | null | undefined): string {
  return hangeAvatarUrl(avatarId, 'short')
}

export function handleShortAvatarError(
  image: HTMLImageElement,
  avatarId: string | null | undefined,
  sex: AvatarSex,
): void {
  const fallbackStage = image.dataset.avatarFallback
  if (avatarId && fallbackStage == null) {
    image.dataset.avatarFallback = 'web-half'
    image.src = getWebHalfAvatarUrl(avatarId)
    return
  }
  if (fallbackStage !== 'default') {
    image.dataset.avatarFallback = 'default'
    image.src = getDefaultAvatarUrl(sex)
  }
}

/** フォールバック用デフォルトアバター */
export function getDefaultAvatarUrl(sex: AvatarSex): string {
  return toAvatarProxyUrl(sex === 'female' ? FEMALE_AVATARS[0] : MALE_AVATARS[0])
}
