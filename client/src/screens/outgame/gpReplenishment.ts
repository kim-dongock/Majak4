export function isOwnGpReplenishmentResponse(data: Record<string, unknown>, currentPix: string): boolean {
  const responsePix = String(data.pix ?? data.k3e ?? '')
  return responsePix === '' || responsePix === currentPix
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