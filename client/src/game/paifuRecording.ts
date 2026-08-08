export type RecordedPaifuPacket = {
  cmd: 'playing' | 'smmc4e'
  data: Record<string, unknown>
}

export type RecordedPaifuEntry = {
  id: number
  date: string
  fieldName: string
  roomName: string
  result: string
  option: string
  data: { packets: RecordedPaifuPacket[] }
  members: Array<{ name?: string; result?: string }>
}

type ActivePaifuRecording = {
  id: number
  roomId: string
  roomName: string
  roomOption: string
  startedAt: number
  members: Array<{ name?: string }>
  enabled: boolean
  headerPacket?: RecordedPaifuPacket
  preludePackets: RecordedPaifuPacket[]
  currentKyokuPackets: RecordedPaifuPacket[]
  currentKyokuOpen: boolean
  currentKyokuEligible: boolean
  committedPackets: RecordedPaifuPacket[]
}

export const GAME_PAIFU_RECORDING_CONFIG_EVENT = 'majak:paifu-recording-config'
const STORAGE_KEY = 'majak2.recorded-paifu.v1'
const LAST_USED_FILE_NAME_KEY = 'majak2.paifu-last-file-name.v1'
const DEFAULT_FILE_NAME = 'Majak2Paifu.txt'
const MAX_RECORDED_PAIFU = 20
let activeRecording: ActivePaifuRecording | null = null

export function shouldRecordPaifu(mode: number, isViewer: boolean): boolean {
  return mode === 2 || (mode === 1 && !isViewer)
}

export function isRecordablePaifuPacket(cmd: string, data: Record<string, unknown>): cmd is RecordedPaifuPacket['cmd'] {
  if (cmd === 'smmc4e') return true
  if (cmd !== 'playing') return false
  const playType = String(data.playType ?? '')
  return playType !== 'MJPID_ACTIONS' && playType !== 'MJPID_TIME_BANK_EXTENDED'
}

export function beginPaifuRecording(options: {
  mode: number
  isViewer: boolean
  roomId: string
  roomName?: string
  roomOption: string
  members?: Array<{ name?: string }>
}): void {
  const startedAt = Date.now()
  const id = loadRecordedPaifuEntries().reduce((maxId, item) => Math.max(maxId, item.id + 1), startedAt)
  activeRecording = {
    id,
    roomId: options.roomId,
    roomName: options.roomName ?? options.roomId,
    roomOption: options.roomOption,
    startedAt,
    members: options.members ?? [],
    enabled: shouldRecordPaifu(options.mode, options.isViewer),
    preludePackets: [],
    currentKyokuPackets: [],
    currentKyokuOpen: false,
    currentKyokuEligible: false,
    committedPackets: [],
  }
}

export function setPaifuRecordingMode(mode: number, isViewer: boolean): void {
  if (!activeRecording) return
  activeRecording.enabled = shouldRecordPaifu(mode, isViewer)
  if (activeRecording.currentKyokuOpen) {
    activeRecording.currentKyokuEligible = activeRecording.enabled
  }
}

export function recordPaifuPacket(cmd: string, data: Record<string, unknown>): void {
  const recording = activeRecording
  if (!recording || !isRecordablePaifuPacket(cmd, data)) return
  const packet: RecordedPaifuPacket = { cmd, data: cloneRecord(data) }
  const playType = cmd === 'playing' ? String(data.playType ?? '') : ''

  if (playType === 'MJPID_INIHAN') {
    recording.headerPacket = packet
    return
  }

  if (!recording.currentKyokuOpen) {
    recording.preludePackets.push(packet)
    if (playType !== 'MJPID_INIKYO') return

    recording.currentKyokuPackets = [
      ...(recording.committedPackets.length === 0 && recording.headerPacket ? [recording.headerPacket] : []),
      ...recording.preludePackets,
    ]
    recording.preludePackets = []
    recording.currentKyokuOpen = true
    recording.currentKyokuEligible = recording.enabled
    return
  }

  recording.currentKyokuPackets.push(packet)
  if (playType !== 'MJPID_ENDKYO') return

  if (recording.currentKyokuEligible) {
    recording.committedPackets.push(...recording.currentKyokuPackets)
    storeRecordedPaifu(toRecordedPaifuEntry(recording))
  }
  recording.currentKyokuPackets = []
  recording.currentKyokuOpen = false
  recording.currentKyokuEligible = false
}

export function replaceRecordedPaifuPackets(packets: RecordedPaifuPacket[]): void {
  const recording = activeRecording
  if (!recording) return
  recording.headerPacket = undefined
  recording.preludePackets = []
  recording.currentKyokuPackets = []
  recording.currentKyokuOpen = false
  recording.currentKyokuEligible = false

  const headerPacket = [...packets].reverse().find(packet => packet.cmd === 'playing' && packet.data.playType === 'MJPID_INIHAN')
  if (headerPacket) recordPaifuPacket(headerPacket.cmd, headerPacket.data)

  let lastInitIndex = -1
  let lastEndIndex = -1
  packets.forEach((packet, index) => {
    if (packet.cmd !== 'playing') return
    if (packet.data.playType === 'MJPID_INIKYO') lastInitIndex = index
    if (packet.data.playType === 'MJPID_ENDKYO') lastEndIndex = index
  })
  if (lastInitIndex < 0 || lastEndIndex > lastInitIndex) return

  let previousEndIndex = -1
  for (let index = 0; index < lastInitIndex; index += 1) {
    const packet = packets[index]
    if (packet.cmd === 'playing' && packet.data.playType === 'MJPID_ENDKYO') previousEndIndex = index
  }
  packets.slice(previousEndIndex + 1).forEach(packet => {
    if (packet.cmd === 'playing' && packet.data.playType === 'MJPID_INIHAN') return
    recordPaifuPacket(packet.cmd, packet.data)
  })
}

export function finalizePaifuRecording(options: {
  roomName?: string
  result?: string
  members?: Array<{ name?: string; result?: string }>
} = {}): RecordedPaifuEntry | null {
  const recording = activeRecording
  activeRecording = null
  if (!recording || recording.committedPackets.length === 0) return null

  const entry = toRecordedPaifuEntry(recording, options)
  storeRecordedPaifu(entry)
  return entry
}

export function interruptPaifuRecording(): void {
  activeRecording = null
}

export function cancelPaifuRecording(): void {
  activeRecording = null
}

export function loadRecordedPaifuEntries(): RecordedPaifuEntry[] {
  if (typeof window === 'undefined') return []
  try {
    const parsed = JSON.parse(window.localStorage.getItem(STORAGE_KEY) ?? '[]')
    return Array.isArray(parsed) ? parsed.filter(isRecordedPaifuEntry) : []
  } catch {
    return []
  }
}

function toRecordedPaifuEntry(
  recording: ActivePaifuRecording,
  options: {
    roomName?: string
    result?: string
    members?: Array<{ name?: string; result?: string }>
  } = {},
): RecordedPaifuEntry {
  return {
    id: recording.id,
    date: new Date(recording.startedAt).toISOString().slice(0, 16).replace('T', ' '),
    fieldName: recording.roomId,
    roomName: options.roomName ?? recording.roomName,
    result: options.result ?? '',
    option: recording.roomOption,
    data: { packets: recording.committedPackets },
    members: options.members ?? recording.members,
  }
}

function storeRecordedPaifu(entry: RecordedPaifuEntry): void {
  if (typeof window === 'undefined') return
  const entries = [entry, ...loadRecordedPaifuEntries().filter(item => item.id !== entry.id)]
    .slice(0, MAX_RECORDED_PAIFU)
  while (entries.length > 0) {
    try {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(entries))
      return
    } catch {
      entries.pop()
    }
  }
}

function cloneRecord(data: Record<string, unknown>): Record<string, unknown> {
  try {
    return JSON.parse(JSON.stringify(data)) as Record<string, unknown>
  } catch {
    return { ...data }
  }
}

function isRecordedPaifuEntry(value: unknown): value is RecordedPaifuEntry {
  if (!value || typeof value !== 'object') return false
  const entry = value as Partial<RecordedPaifuEntry>
  return typeof entry.id === 'number'
    && typeof entry.date === 'string'
    && typeof entry.option === 'string'
    && Boolean(entry.data)
}

export function loadLastUsedPaifuFileName(): string {
  if (typeof window === 'undefined') return DEFAULT_FILE_NAME
  try {
    return window.localStorage.getItem(LAST_USED_FILE_NAME_KEY)?.trim() || DEFAULT_FILE_NAME
  } catch {
    return DEFAULT_FILE_NAME
  }
}

export function saveLastUsedPaifuFileName(fileName: string): void {
  if (typeof window === 'undefined' || !fileName.trim()) return
  try {
    window.localStorage.setItem(LAST_USED_FILE_NAME_KEY, fileName.trim())
  } catch {
    // localStorage can be unavailable in private or embedded contexts.
  }
}