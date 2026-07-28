/** Majak 共通キャラクター画像。全画面で同じ選択 URL を使用する。 */
export const LOCAL_AVATAR_BASE = '/assets/images/characters'
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

function resolveAvatarUrl(avatarId: string | null | undefined): string {
  if (avatarId) {
    const normalized = toAvatarProxyUrl(avatarId)
    if (normalized !== avatarId || normalized.startsWith(`${LOCAL_AVATAR_BASE}/`)) return normalized
  }
  return getDefaultAvatarUrl('male')
}

export function getAvatarUrl(avatarId: string | null | undefined): string {
  return resolveAvatarUrl(avatarId)
}

export function getGameAvatarUrl(avatarId: string | null | undefined): string {
  return resolveAvatarUrl(avatarId)
}

export function getWebHalfAvatarUrl(avatarId: string | null | undefined): string {
  return resolveAvatarUrl(avatarId)
}

export function getShortAvatarUrl(avatarId: string | null | undefined): string {
  return resolveAvatarUrl(avatarId)
}

/** フォールバック用デフォルトアバター */
export function getDefaultAvatarUrl(sex: AvatarSex): string {
  return toAvatarProxyUrl(sex === 'female' ? FEMALE_AVATARS[0] : MALE_AVATARS[0])
}
