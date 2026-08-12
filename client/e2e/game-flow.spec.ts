import { expect, test } from '@playwright/test'

test.beforeEach(async ({ page }) => {
  page.on('pageerror', error => console.error('[browser pageerror]', error.message))
  page.on('console', message => {
    if (message.type() === 'error') console.error('[browser console]', message.text())
  })
  await page.goto('/e2e/game-fixture.html', { waitUntil: 'domcontentloaded' })
  await expect.poll(() => page.evaluate(() => window.__majakGameFixture?.ready()), { timeout: 60_000 }).toBe(true)
})

test('fresh game acknowledges presentation only after the legacy seat, dice, and deal sequence', async ({ page }) => {
  await page.evaluate(() => window.__majakGameFixture.emitFreshGameStart())

  await expect.poll(() => page.evaluate(() => window.__majakGameFixture.seatRevealCount())).toBe(4)
  await page.waitForTimeout(1150)
  await expect.poll(() => page.evaluate(() => window.__majakGameFixture.seatRevealTextures().length)).toBe(4)

  const earlyPresentationReady = await page.evaluate(() => window.__majakGameFixture.sent()
    .filter(message => message.kind === 'invoke' && message.name === 'NotifyGamePresentationReady'))
  expect(earlyPresentationReady).toHaveLength(0)

  await expect.poll(() => page.evaluate(() => window.__majakGameFixture.sent()
    .find(message => message.kind === 'invoke' && message.name === 'NotifyGamePresentationReady')?.payload), { timeout: 12_000 }).toEqual([901, 1])
  expect(await page.evaluate(() => window.__majakGameFixture.visibleHandCount())).toBe(52)

  const reconnectInvocations = await page.evaluate(() => window.__majakGameFixture.sent()
    .filter(message => message.kind === 'invoke' && message.name === 'RequestGameResync'))
  expect(reconnectInvocations).toHaveLength(0)
})

test('mobile timeout discards the preselected tile after another-seat prompt and hand reorder', async ({ page }) => {
  await page.evaluate(() => window.__majakGameFixture.setTimeScale(10))
  await page.evaluate(() => window.__majakGameFixture.emitFreshGameStart())
  await expect.poll(() => page.evaluate(() => window.__majakGameFixture.sent()
    .some(message => message.kind === 'invoke' && message.name === 'NotifyGamePresentationReady')), { timeout: 3000 }).toBe(true)

  const selectedTile = await page.evaluate(() => window.__majakGameFixture.tileCenter(4))
  expect(selectedTile.bipaiIndex).toBeDefined()
  await page.touchscreen.tap(selectedTile.x, selectedTile.y)
  await expect.poll(() => page.evaluate(() => window.__majakGameFixture.selectedBipaiIndex())).toBe(selectedTile.bipaiIndex)

  await page.evaluate(() => window.__majakGameFixture.emitOtherSeatPrompt())
  expect(await page.evaluate(() => window.__majakGameFixture.selectedBipaiIndex())).toBe(selectedTile.bipaiIndex)

  await page.evaluate(() => window.__majakGameFixture.emitLocalTurnPrompt())
  await expect.poll(() => page.evaluate(() => window.__majakGameFixture.sent().find(message => {
    if (message.kind !== 'send' || message.name !== 'playing') return false
    const payload = message.payload as { playType?: string }
    return payload.playType === 'MJPID_ACTION'
  })?.payload)).toMatchObject({
    playType: 'MJPID_ACTION',
    bipaiIndex: [selectedTile.bipaiIndex],
    actionSeq: 11,
  })
})
