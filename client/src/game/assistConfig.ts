export interface GameAssistConfig {
  bChkTap: boolean
  bChkPai: boolean
  bChkTnp: boolean
  bChkHor: boolean
}

export const DEFAULT_GAME_ASSIST_CONFIG: GameAssistConfig = {
  bChkTap: true,
  bChkPai: true,
  bChkTnp: true,
  bChkHor: true,
}

export const GAME_ASSIST_CONFIG_EVENT = 'majak:assist-config'

export function toGameAssistConfig(value?: Partial<GameAssistConfig>): GameAssistConfig {
  return {
    bChkTap: value?.bChkTap ?? DEFAULT_GAME_ASSIST_CONFIG.bChkTap,
    bChkPai: value?.bChkPai ?? DEFAULT_GAME_ASSIST_CONFIG.bChkPai,
    bChkTnp: value?.bChkTnp ?? DEFAULT_GAME_ASSIST_CONFIG.bChkTnp,
    bChkHor: value?.bChkHor ?? DEFAULT_GAME_ASSIST_CONFIG.bChkHor,
  }
}