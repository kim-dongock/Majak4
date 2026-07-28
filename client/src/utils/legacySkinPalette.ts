const CUSTOM_BOARD_TENGOKU = 100002
const CUSTOM_BOARD_DEFAULT = 100000
const CUSTOM_BGM_ID_EXTRA = 100008
const CUSTOM_BGM_ID_TENGOKU = 100009
const CUSTOM_ITEM_TYPE_BG_EXTRA = 11
const CUSTOM_ITEM_TYPE_BG_TENGOKU = 12

export interface LegacyRoomPalette {
  normal: string
  notice: string
  error: string
  roomJoin: string
  roomExit: string
  roomDrop: string
  chatEditBack: string
  chatEditText: string
  chatSelf: string
  chatOther: string
  chatViewer: string
  chatAbuse: string
  roomTitle: string
  info: string
}

const DEFAULT_ROOM_PALETTE: LegacyRoomPalette = {
  normal: '#0000d7',
  notice: '#008000',
  error: '#e00000',
  roomJoin: '#004000',
  roomExit: '#800000',
  roomDrop: '#e00000',
  chatEditBack: 'rgb(195,193,156)',
  chatEditText: '#000000',
  chatSelf: '#003060',
  chatOther: '#000000',
  chatViewer: '#606060',
  chatAbuse: '#ff0000',
  roomTitle: '#000000',
  info: '#000000',
}

const TENGOKU_ROOM_PALETTE: LegacyRoomPalette = {
  normal: '#1ee61e',
  notice: '#58d3ff',
  error: '#ff4646',
  roomJoin: '#58d3ff',
  roomExit: '#ff0000',
  roomDrop: '#ff4646',
  chatEditBack: 'rgb(195,193,156)',
  chatEditText: '#ffffff',
  chatSelf: '#ffffff',
  chatOther: '#58d3ff',
  chatViewer: '#c8c8c8',
  chatAbuse: '#ff4646',
  roomTitle: '#ffffff',
  info: '#ffff9c',
}

function asFiniteNumber(value: unknown): number | undefined {
  const number = Number(value)
  return Number.isFinite(number) ? number : undefined
}

function toHex(value: number): string {
  return Math.max(0, Math.min(255, value)).toString(16).padStart(2, '0')
}

function invertRgb(red: number, green: number, blue: number): string {
  return `#${toHex(255 - red)}${toHex(255 - green)}${toHex(255 - blue)}`
}

export function isTengokuBoardSkin(customBgId: unknown, customBoardType?: unknown): boolean {
  return asFiniteNumber(customBgId) === CUSTOM_BOARD_TENGOKU || asFiniteNumber(customBoardType) === CUSTOM_ITEM_TYPE_BG_TENGOKU
}

export function getLegacyRoomPalette(tengoku: boolean): LegacyRoomPalette {
  return tengoku ? TENGOKU_ROOM_PALETTE : DEFAULT_ROOM_PALETTE
}

export function getLegacyBoardSoundSkinId(customBgId: unknown, customBoardType?: unknown): number | undefined {
  const board = asFiniteNumber(customBgId)
  const type = asFiniteNumber(customBoardType)
  if (board === CUSTOM_BGM_ID_EXTRA || board === CUSTOM_BGM_ID_TENGOKU) return board
  if (type === CUSTOM_ITEM_TYPE_BG_EXTRA || board === 100001) return CUSTOM_BGM_ID_EXTRA
  if (type === CUSTOM_ITEM_TYPE_BG_TENGOKU || board === CUSTOM_BOARD_TENGOKU) return CUSTOM_BGM_ID_TENGOKU
  return undefined
}

export function getLegacyFullUiSkinId(customBgId: unknown, customBoardType?: unknown): number | undefined {
  const board = asFiniteNumber(customBgId)
  const type = asFiniteNumber(customBoardType)
  if (type === CUSTOM_ITEM_TYPE_BG_TENGOKU || board === CUSTOM_BOARD_TENGOKU) return CUSTOM_BOARD_TENGOKU
  if (board != null && board > CUSTOM_BOARD_DEFAULT) return board
  return undefined
}

export function applyTengokuTextColor(color: string | undefined, tengoku: boolean, fallback = '#000000'): string {
  const source = color || fallback
  if (!tengoku) return source

  const hex = source.trim().match(/^#([0-9a-f]{3}|[0-9a-f]{6})$/i)?.[1]
  if (hex) {
    const expanded = hex.length === 3 ? hex.split('').map(char => char + char).join('') : hex
    return invertRgb(
      Number.parseInt(expanded.slice(0, 2), 16),
      Number.parseInt(expanded.slice(2, 4), 16),
      Number.parseInt(expanded.slice(4, 6), 16),
    )
  }

  const rgb = source.trim().match(/^rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)/i)
  if (rgb) {
    return invertRgb(Number(rgb[1]), Number(rgb[2]), Number(rgb[3]))
  }

  return source
}