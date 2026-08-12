import type { HanResPlayer } from './HanRes'

export const FORCE_HAN_RESULT_FOR_TEST = false

export const FORCED_HAN_RESULT: HanResPlayer[] = [
  { pix: 'final-p0', name: 'テスト一位', sex: 'male', seatPos: 0, rank: 0, point: 42_000, setBal: 120, setTen: 72, setUma: 40, setTip: 8, coinGain: 1_200, coinNeed: 800, isMe: true },
  { pix: 'final-p1', name: 'テスト二位', sex: 'female', seatPos: 1, rank: 1, point: 30_000, setBal: 20, setTen: 0, setUma: 10, setTip: 10 },
  { pix: 'final-p2', name: 'テスト三位', sex: 'male', seatPos: 2, rank: 2, point: 20_000, setBal: -40, setTen: -30, setUma: -10, setTip: 0 },
  { pix: 'final-p3', name: 'テスト四位', sex: 'female', seatPos: 3, rank: 3, point: 8_000, setBal: -100, setTen: -42, setUma: -40, setTip: -18 },
]