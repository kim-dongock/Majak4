import type { KyoResData } from './KyoRes'

export const FORCE_KYO_RESULT_FOR_TEST = false

export const FORCED_KYO_RESULT: KyoResData = {
  pinType: 9,
  kyoNum: 0,
  ribCnt: 0,
  renCnt: 1,
  players: [
    { pix: 'nagashi-p0', name: '流し満貫成立者', sex: 'male', seatPos: 0, isOya: true, isNagashiMangan: true, tenBal: 12_000 },
    { pix: 'nagashi-p1', name: 'テスト対面', sex: 'female', seatPos: 1, isOya: false, tenBal: -4_000 },
    { pix: 'nagashi-p2', name: 'テスト下家', sex: 'male', seatPos: 2, isOya: false, tenBal: -4_000 },
    { pix: 'nagashi-p3', name: 'テスト上家', sex: 'female', seatPos: 3, isOya: false, tenBal: -4_000 },
  ],
}