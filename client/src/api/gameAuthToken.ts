let cachedGameAccessToken: { pix: string; value: string } | null = null

export function rememberGameAccessToken(pix?: string, accessToken?: string): void {
  if (pix && accessToken) cachedGameAccessToken = { pix, value: accessToken }
}

export function getRememberedGameAccessToken(pix?: string): string {
  return pix && cachedGameAccessToken?.pix === pix ? cachedGameAccessToken.value : ''
}

export function clearRememberedGameAccessToken(): void {
  cachedGameAccessToken = null
}