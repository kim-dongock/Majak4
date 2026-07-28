import { create } from 'zustand'

const STORAGE_KEY = 'majak:custom-skin-equip'

export type CustomSkinEquip = {
  charaId: number
  charaType: number
  haiId: number
  haiType: number
  bgId: number
  bgType: number
}

const DEFAULT_EQUIP: CustomSkinEquip = {
  charaId: 0,
  charaType: 0,
  haiId: 0,
  haiType: 0,
  bgId: 0,
  bgType: 0,
}

function readStoredEquip(): CustomSkinEquip {
  try {
    const raw = window.sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return DEFAULT_EQUIP
    const value = JSON.parse(raw) as Partial<CustomSkinEquip>
    return {
      charaId: Number(value.charaId ?? 0),
      charaType: Number(value.charaType ?? 0),
      haiId: Number(value.haiId ?? 0),
      haiType: Number(value.haiType ?? 0),
      bgId: Number(value.bgId ?? 0),
      bgType: Number(value.bgType ?? 0),
    }
  } catch {
    return DEFAULT_EQUIP
  }
}

function storeEquip(equip: CustomSkinEquip) {
  try {
    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(equip))
  } catch {
    // sessionStorage can be unavailable in private or embedded contexts.
  }
}

function applyEquip(prev: CustomSkinEquip, customId: number, customType: number): CustomSkinEquip {
  if (customId <= 0) return prev
  if (customType >= 30 && customType < 40) return { ...prev, charaId: customId, charaType: customType }
  if (customType >= 20 && customType < 30) return { ...prev, haiId: customId, haiType: customType }
  if (customType >= 10 && customType < 20) return { ...prev, bgId: customId, bgType: customType }
  return prev
}

type CustomSkinStore = CustomSkinEquip & {
  setEquip: (customId: number, customType: number) => void
  setEquips: (entries: Array<Record<string, unknown>>) => CustomSkinEquip
}

export const useCustomSkinStore = create<CustomSkinStore>(set => ({
  ...readStoredEquip(),

  setEquip: (customId, customType) => set(prev => {
    const next = applyEquip(prev, customId, customType)
    storeEquip(next)
    return next
  }),

  setEquips: entries => {
    let next = DEFAULT_EQUIP
    for (const entry of entries) {
      next = applyEquip(next, Number(entry.customId ?? 0), Number(entry.customType ?? 0))
    }
    storeEquip(next)
    set(next)
    return next
  },
}))