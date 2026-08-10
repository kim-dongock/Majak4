export function isOwnGpReplenishmentResponse(data: Record<string, unknown>, currentPix: string): boolean {
  const responsePix = String(data.pix ?? data.k3e ?? '')
  return responsePix === '' || responsePix === currentPix
}

export interface GpAssetUpdate {
  gamMoney?: number
  slevel?: string
  nlevel?: number
}

export function readGpAssetUpdate(data: Record<string, unknown>): GpAssetUpdate {
  const gamMoney = Number(data.gammoney ?? data.gamMoney ?? data.k34e)
  const nlevel = Number(data.nlevel ?? data.nLevel ?? data.k33e)
  const rawSlevel = data.slevel ?? data.k32e

  return {
    ...(Number.isFinite(gamMoney) ? { gamMoney } : {}),
    ...(typeof rawSlevel === 'string' ? { slevel: rawSlevel } : {}),
    ...(Number.isFinite(nlevel) ? { nlevel } : {}),
  }
}

export function gpReplenishmentFailureMessage(data: Record<string, unknown>): string {
  const replenishmentType = Number(data.mjkk42e ?? data.replenishmentType ?? 0)
  if (replenishmentType === 3) {
    return '本日の無料GP補充は使用済みです。\n利用回数は午前6時に回復します。'
  }
  if (Number(data.gammoney ?? data.k34e ?? 0) >= 1000) {
    return '所持GPが1,000以上のため、無料補充は利用できません。'
  }
  return 'GP補充に失敗しました'
}