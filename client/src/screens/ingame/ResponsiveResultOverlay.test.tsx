import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it, vi } from 'vitest'
import { ResponsiveHanResult, ResponsiveKyoResult } from './ResponsiveResultOverlay'
import { FORCED_HAN_RESULT } from './forcedHanResult'
import type { KyoResData } from './KyoRes'

const DRAW_RESULT: KyoResData = {
  pinType: 5,
  kyoNum: 0,
  ribCnt: 1,
  renCnt: 2,
  players: [
    { pix: 'p0', name: 'P0', seatPos: 0, isOya: true, tenBal: 1500, isTempai: true },
    { pix: 'p1', name: 'P1', seatPos: 1, isOya: false, tenBal: -1500, isTempai: false },
    { pix: 'p2', name: 'P2', seatPos: 2, isOya: false, tenBal: 1500, isTempai: true },
    { pix: 'p3', name: 'P3', seatPos: 3, isOya: false, tenBal: -1500, isTempai: false },
  ],
}

const TSUMO_RESULT: KyoResData = {
  pinType: 1,
  totalFu: 30,
  totalFan: 4,
  totalTen: 8000,
  players: [
    { pix: 'p0', name: 'P0', seatPos: 0, isOya: true, tenBal: 8000, isHora: true },
    { pix: 'p1', name: 'P1', seatPos: 1, isOya: false, tenBal: -4000 },
    { pix: 'p2', name: 'P2', seatPos: 2, isOya: false, tenBal: -2000 },
    { pix: 'p3', name: 'P3', seatPos: 3, isOya: false, tenBal: -2000 },
  ],
  yaku: [{ name: '門前清自摸和', fan: 1 }],
}

const RON_RESULT: KyoResData = {
  pinType: 0,
  players: [
    { pix: 'p0', name: 'P0', seatPos: 0, isOya: true, tenBal: 8000, isHora: true },
    { pix: 'p1', name: 'P1', seatPos: 1, isOya: false, tenBal: 0 },
    { pix: 'p2', name: 'P2', seatPos: 2, isOya: false, tenBal: -8000, isHoju: true },
    { pix: 'p3', name: 'P3', seatPos: 3, isOya: false, tenBal: 0 },
  ],
  yaku: [{ name: '立直', fan: 1 }],
}

describe('ResponsiveKyoResult', () => {
  it('renders a compact shared draw result with tenpai and noten states', () => {
    const html = renderToStaticMarkup(
      <ResponsiveKyoResult data={DRAW_RESULT} canContinue onClose={vi.fn()} />,
    )

    expect(html).toContain('majak-kyo-result-panel is-draw')
    expect(html).toContain('流局')
    expect(html).toContain('聴牌')
    expect(html).toContain('ノーテン')
    expect(html).toContain('本場 2 / 供託 1')
    expect(html).not.toContain('aria-label="和了詳細"')
  })

  it('keeps the result visible after local confirmation while waiting for other players', () => {
    const html = renderToStaticMarkup(
      <ResponsiveKyoResult data={DRAW_RESULT} canContinue waitingForOtherPlayers onClose={vi.fn()} />,
    )

    expect(html).toContain('他のプレイヤーの確認を待っています')
    expect(html).toContain('確認済み')
    expect(html).toContain('disabled=""')
  })

  it('shows all players and marks only confirmed continuations as complete', () => {
    const html = renderToStaticMarkup(
      <ResponsiveKyoResult
        data={DRAW_RESULT}
        myOdr={0}
        canContinue
        playerProgress={{
          0: { durationMs: 8_000, localDeadlineAt: performance.now() + 8_000, submitted: false },
          1: { durationMs: 8_000, localDeadlineAt: performance.now(), submitted: true },
        }}
        onClose={vi.fn()}
      />,
    )

    expect(html).toContain('プレイヤーの確認状況')
    expect(html).toContain('確認済み')
    expect(html).toContain('確認待ち')
    expect(html).toContain('P0')
    expect(html).toContain('P1')
    expect(html).toContain('aria-label="P0の確認時間"')
    expect(html).not.toContain('aria-label="P1の確認時間"')
    expect(html).not.toContain('P0</span><strong>確認済み')
  })

  it('marks a tsumo result for the compact mobile hora layout without dropping details', () => {
    const html = renderToStaticMarkup(
      <ResponsiveKyoResult data={TSUMO_RESULT} canContinue onClose={vi.fn()} />,
    )

    expect(html).toContain('majak-kyo-result-panel is-hora')
    expect(html).toContain('TSUMO')
    expect(html).toContain('aria-label="和了詳細"')
    expect(html).toContain('門前清自摸和')
    expect(html).toContain('majak-result-player__animated-status is-tsumo')
    expect(html).toContain('aria-label="自摸和了"')
    expect(html).toContain('majak-result-player__status-image')
    expect(html).toContain('aria-hidden="true"')
    expect(html).toContain('role="table"')
    expect(html).toContain('プレイヤー')
    expect(html).toContain('チップ')
  })

  it('renders ron with one winner detail and the dealer-in player status', () => {
    const html = renderToStaticMarkup(
      <ResponsiveKyoResult data={RON_RESULT} canContinue onClose={vi.fn()} />,
    )

    expect(html).toContain('RON')
    expect(html).toContain('majak-result-player__animated-status is-ron')
    expect(html).toContain('aria-label="栄和了"')
    expect(html).toContain('majak-result-player__animated-status is-hoju')
    expect(html).toContain('aria-label="放銃"')
    expect(html).toContain('majak-result-player__status-label')
    expect(html).toContain('立直')
    expect(html.match(/>表示<\/button>/g)).toHaveLength(1)
  })
})

describe('ResponsiveHanResult', () => {
  it('renders four final ranks with score, uma, chip, and player reward', () => {
    const html = renderToStaticMarkup(
      <ResponsiveHanResult players={FORCED_HAN_RESULT} hasTip onClose={vi.fn()} />,
    )

    expect(html).toContain('対局結果')
    expect(html).toContain('テスト一位')
    expect(html).toContain('テスト四位')
    expect(html).toContain('ウマ')
    expect(html).toContain('チップ')
    expect(html).toContain('今回の収支')
    expect(html.match(/majak-han-result-row/g)).toHaveLength(4)
  })
})
