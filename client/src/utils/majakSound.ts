const SOUND_BASE = '/assets/sounds/game/Sounds'

export interface MajakSoundConfig {
  bChkBGM: boolean
  bChkSND: boolean
  bChkPIO: boolean
  nVolBGM: number
  nVolSND: number
}

export const DEFAULT_MAJAK_SOUND_CONFIG: MajakSoundConfig = {
  bChkBGM: true,
  bChkSND: true,
  bChkPIO: true,
  nVolBGM: 255,
  nVolSND: 255,
}

const CUSTOM_DEFAULT_ID_COSTUME = 100011
const CUSTOM_ITEM_TYPE_CHARA = 30
const CUSTOM_ITEM_TYPE_CHARA_MALE = 31
const CUSTOM_ITEM_TYPE_CHARA_FEMALE = 32

export const SID_CHI = 0
export const SID_KAN = 1
export const SID_PON = 2
export const SID_RIC = 3
export const SID_RON = 4
export const SID_TSU = 5
export const SID_HUA = 6
const SID_CALLCNT = 7

export const SID_JOIN = SID_CALLCNT * 8
export const SID_EXIT = SID_JOIN + 1
export const SID_SIPAI = SID_JOIN + 2
export const SID_DICE = SID_JOIN + 3
export const SID_EXPOSE = SID_JOIN + 4
export const SID_FURO = SID_JOIN + 5
export const SID_TURN = SID_JOIN + 7
export const SID_TIME = SID_JOIN + 8
export const SID_THROW = SID_JOIN + 9
export const SID_DRAW = SID_JOIN + 10
export const SID_CHAT = SID_JOIN + 11
export const SID_RICSTK = SID_JOIN + 12
export const SID_THROW_DORA = SID_JOIN + 13
export const SID_THROW_RICH = SID_EXPOSE
export const SID_EFFECT_R_LV1 = SID_JOIN + 14
export const SID_EFFECT_R_LV2 = SID_JOIN + 15
export const SID_EFFECT_R_LV3 = SID_JOIN + 16
export const SID_EFFECT_T_LV1 = SID_JOIN + 17
export const SID_EFFECT_T_LV2 = SID_JOIN + 18
export const SID_EFFECT_T_LV3 = SID_JOIN + 19
export const SID_EFFECT_YAKUMAN = SID_JOIN + 20

export const MID_NORMAL = 0
export const MID_RICHI = 1
export const MID_BAD = 2
export const MID_GOOD = 3
export const MID_FESTA = 4
export const MID_FESRIC = 5
export const MID_TEN_TONBA = 6
export const MID_TEN_REACH1 = 7
export const MID_TEN_REACH2 = 8
export const MID_TEN_ALLLAST = 9
export const MID_TEN_NANBA = 10

const SFX_TABLE: Array<string | null> = [
  'mjkmchi', 'mjkmkang', 'mjkmpong', 'mjkmreach', 'mjkmrong', 'mjkmsumo', 'mjkhua',
  'mjkmchi2', 'mjkmkang2', 'mjkmpong2', 'mjkmreach2', 'mjkmrong2', 'mjkmsumo2', 'mjkhua',
  'mjkmchi3', 'mjkmkang3', 'mjkmpong3', 'mjkmreach3', 'mjkmrong3', 'mjkmsumo3', 'mjkhua',
  'mjkmchi4', 'mjkmkang4', 'mjkmpong4', 'mjkmreach4', 'mjkmrong4', 'mjkmsumo4', 'mjkhua',
  'mjkfchi', 'mjkfkang', 'mjkfpong', 'mjkfreach', 'mjkfrong', 'mjkfsumo', 'mjkhua',
  'mjkfchi2', 'mjkfkang2', 'mjkfpong2', 'mjkfreach2', 'mjkfrong2', 'mjkfsumo2', 'mjkhua',
  'mjkfchi3', 'mjkfkang3', 'mjkfpong3', 'mjkfreach3', 'mjkfrong3', 'mjkfsumo3', 'mjkhua',
  'mjkfchi4', 'mjkfkang4', 'mjkfpong4', 'mjkfreach4', 'mjkfrong4', 'mjkfsumo4', 'mjkhua',
  'mjkjoin', 'mjkexit', 'mjksiipai', 'mjkdice', 'mjkpais', 'mjkkui', null, 'mjkgetpae',
  'mjktime', 'mjkthrow', 'mjkdraw', 'mjkchat', 'mjkricstk', 'mjkdragiri',
  'sm_se013', 'sm_se014', 'sm_se036', 'sm_se015', 'sm_se016', 'sm_se037', 'sm_se020',
  'uniava001', 'uniava003', 'uniava004', 'uniava005', 'uniava006', 'uniava007',
  'uniava008', 'uniava009', 'uniava010', 'uniava012', 'uniava013', 'uniava014',
  'uniava015', 'uniava016', 'uniava017', 'uniava018', 'uniava019',
  'uniava_se_01', 'uniava_se_02', 'uniava_se_03', 'uniava_se_04', 'uniava_se_05',
  'uniava_se_06', 'uniava_se_07', 'uniava_se_08', 'uniava_se_09', 'uniava_se_10', 'uniava_se_11',
]

const BGM_TABLE = [
  'mjkbgm02', 'mjkbgm03', 'mjkbgm01', 'mjkbgm04', 'mjkbgm_festa0', 'mjkbgm_festa1',
  'sm_bgm001', 'sm_bgm002', 'sm_bgm003', 'sm_bgm008', 'sm_bgm011',
]

const OGG_NAMES = new Set([
  'mjkbgm01', 'mjkbgm02', 'mjkbgm03', 'mjkbgm04', 'mjkbgm_festa1',
  'sm_bgm001', 'sm_bgm002', 'sm_bgm003', 'sm_bgm008', 'sm_bgm011',
  'sm_se013', 'sm_se014', 'sm_se015', 'sm_se016', 'sm_se020', 'sm_se036', 'sm_se037',
  'mjkearthskill1', 'mjkearthskill2', 'mjkfireskill1', 'mjkfireskill2',
  'mjkwaterskill1', 'mjkwaterskill2', 'mjkwindskill1', 'mjkwindskill2',
  'mjkgettitle', 'mjklevelup1', 'mjkhojyu', 'mjkhistart', 'mjkhiend1', 'mjkhiendwin', 'mjkhiendlost',
  'mjkreach01', 'mjkreach02', 'mjkryutama01', 'mjkryutama02',
])

const HSO_NAMES = new Set(['mjkbgm_festa0'])

let config: MajakSoundConfig = { ...DEFAULT_MAJAK_SOUND_CONFIG }
let bgmAudio: HTMLAudioElement | null = null
let bgmId = -1
let bgmRequest: { mid: number; skinId?: number } | null = null
const activeSfx = new Set<HTMLAudioElement>()

function clampVolume(value: number): number {
  if (!Number.isFinite(value)) return 1
  return Math.min(1, Math.max(0, value / 255))
}

function defaultSoundExtension(name: string): string {
  return HSO_NAMES.has(name) ? 'hso' : OGG_NAMES.has(name) ? 'ogg' : 'wav'
}

function skinSoundExtensions(name: string): string[] {
  if (name.startsWith('mjkvoice_')) return ['wav', 'ogg', 'hso']
  if (HSO_NAMES.has(name)) return ['ogg', 'hso', 'wav']
  return ['ogg', 'wav', 'hso']
}

function uniqueUrls(urls: string[]): string[] {
  return Array.from(new Set(urls))
}

function soundUrl(name: string): string {
  return `${SOUND_BASE}/${name}.${defaultSoundExtension(name)}`
}

function skinSoundUrl(name: string, skinId: number, ext: string): string {
  const suffix = String(skinId).padStart(2, '0')
  return `${SOUND_BASE}/skin/${skinId}/${name}_${suffix}.${ext}`
}

function soundUrlCandidates(name: string, skinId?: number): string[] {
  if (skinId === undefined) return [soundUrl(name)]
  return uniqueUrls([
    ...skinSoundExtensions(name).map(ext => skinSoundUrl(name, skinId, ext)),
    soundUrl(name),
  ])
}

function disposeAudio(audio: HTMLAudioElement): void {
  audio.pause()
  audio.removeAttribute('src')
}

function bgmRequestId(mid: number, skinId?: number): number {
  return skinId !== undefined ? mid * 1000000 + skinId : mid
}

function stopCurrentBgm(): void {
  if (bgmAudio) disposeAudio(bgmAudio)
  bgmAudio = null
  bgmId = -1
}

function playSfxCandidate(urls: string[], index: number, loop: boolean): HTMLAudioElement | null {
  const url = urls[index]
  if (!url) return null
  const audio = new Audio(url)
  audio.volume = clampVolume(config.nVolSND)
  audio.loop = loop
  activeSfx.add(audio)
  let finished = false
  const finish = (tryFallback: boolean) => {
    if (finished) return
    finished = true
    activeSfx.delete(audio)
    disposeAudio(audio)
    if (tryFallback) playSfxCandidate(urls, index + 1, loop)
  }
  audio.addEventListener('ended', () => finish(false), { once: true })
  audio.addEventListener('error', () => finish(true), { once: true })
  audio.play().catch(() => finish(true))
  return audio
}

function playBgmCandidate(urls: string[], index: number): void {
  const url = urls[index]
  if (!url) {
    bgmAudio = null
    bgmId = -1
    return
  }
  const audio = new Audio(url)
  audio.volume = clampVolume(config.nVolBGM)
  audio.loop = true
  bgmAudio = audio
  let finished = false
  const fallback = () => {
    if (finished || bgmAudio !== audio) return
    finished = true
    disposeAudio(audio)
    bgmAudio = null
    playBgmCandidate(urls, index + 1)
  }
  audio.addEventListener('error', fallback, { once: true })
  audio.play().catch(fallback)
}

export function configureMajakSound(next: Partial<MajakSoundConfig>): void {
  const wasBgmEnabled = config.bChkBGM
  config = { ...config, ...next }
  if (bgmAudio) bgmAudio.volume = clampVolume(config.nVolBGM)
  activeSfx.forEach(audio => { audio.volume = clampVolume(config.nVolSND) })
  if (!config.bChkBGM) {
    stopCurrentBgm()
  } else if (!wasBgmEnabled && bgmRequest && !bgmAudio) {
    playMajakBgm(bgmRequest.mid, bgmRequest.skinId !== undefined ? { skinId: bgmRequest.skinId } : {})
  }
}

export function playMajakSfx(name: string, options: { loop?: boolean; force?: boolean; skinId?: number } = {}): HTMLAudioElement | null {
  if (!options.force && !config.bChkSND) return null
  return playSfxCandidate(soundUrlCandidates(name, options.skinId), 0, Boolean(options.loop))
}

export function stopMajakSfx(): void {
  activeSfx.forEach(disposeAudio)
  activeSfx.clear()
}

export function playMajakSid(sid: number, options: { loop?: boolean; force?: boolean; skinId?: number } = {}): HTMLAudioElement | null {
  const name = SFX_TABLE[sid]
  if (!name) return null
  return playMajakSfx(name, options)
}

export function playMajakChat(): HTMLAudioElement | null {
  if (!config.bChkPIO) return null
  return playMajakSid(SID_CHAT)
}

export function playMajakBgm(mid: number, options: { skinId?: number } = {}): void {
  const name = BGM_TABLE[mid]
  if (!name) {
    bgmRequest = null
    stopCurrentBgm()
    return
  }
  bgmRequest = options.skinId !== undefined ? { mid, skinId: options.skinId } : { mid }
  if (!config.bChkBGM) {
    stopCurrentBgm()
    return
  }
  const nextBgmId = bgmRequestId(mid, options.skinId)
  if (bgmAudio && bgmId === nextBgmId && !bgmAudio.paused) return
  stopCurrentBgm()
  bgmId = nextBgmId
  playBgmCandidate(soundUrlCandidates(name, options.skinId), 0)
}

export function stopMajakBgm(): void {
  bgmRequest = null
  stopCurrentBgm()
}

type CallAction = 'chi' | 'kan' | 'pon' | 'reach' | 'ron' | 'tsumo' | 'hua'

const CALL_SID: Record<CallAction, number> = {
  chi: SID_CHI,
  kan: SID_KAN,
  pon: SID_PON,
  reach: SID_RIC,
  ron: SID_RON,
  tsumo: SID_TSU,
  hua: SID_HUA,
}

const CUSTOM_VOICE_NAME: Record<Exclude<CallAction, 'hua'>, string> = {
  chi: 'mjkvoice_chi',
  kan: 'mjkvoice_kan',
  pon: 'mjkvoice_pon',
  reach: 'mjkvoice_reach',
  ron: 'mjkvoice_ron',
  tsumo: 'mjkvoice_tsumo',
}

export function playMajakCallVoice(action: CallAction, options: {
  odr: number
  sex?: string
  customCostume?: number
  customCostumeType?: number
  soundSkinId?: number
}): HTMLAudioElement | null {
  const sid = CALL_SID[action]
  const odr = Number.isInteger(options.odr) ? Math.min(3, Math.max(0, options.odr)) : 0
  const customCostume = options.customCostume ?? 0
  const customCostumeType = options.customCostumeType ?? 0

  if (action !== 'hua' && customCostume !== 0 && customCostume !== CUSTOM_DEFAULT_ID_COSTUME && customCostumeType === CUSTOM_ITEM_TYPE_CHARA) {
    return playMajakSfx(CUSTOM_VOICE_NAME[action], { skinId: customCostume })
  }

  if (customCostume !== 0 && customCostume !== CUSTOM_DEFAULT_ID_COSTUME) {
    if (customCostumeType === CUSTOM_ITEM_TYPE_CHARA_MALE) return playMajakSid(sid + SID_CALLCNT * odr, { skinId: options.soundSkinId })
    if (customCostumeType === CUSTOM_ITEM_TYPE_CHARA_FEMALE) return playMajakSid(sid + SID_CALLCNT * (4 + odr), { skinId: options.soundSkinId })
  }

  const sex = String(options.sex ?? '').toUpperCase()
  const isFemale = sex === 'F' || sex === 'FEMALE'
  return playMajakSid(sid + SID_CALLCNT * (isFemale ? 4 + odr : odr), { skinId: options.soundSkinId })
}