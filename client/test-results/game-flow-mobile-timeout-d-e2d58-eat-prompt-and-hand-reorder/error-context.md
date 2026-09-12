# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: game-flow.spec.ts >> mobile timeout discards the preselected tile after another-seat prompt and hand reorder
- Location: e2e\game-flow.spec.ts:32:1

# Error details

```
Error: expect(received).toBe(expected) // Object.is equality

Expected: true
Received: false

Call Log:
- Timeout 3000ms exceeded while waiting on the predicate
```

# Page snapshot

```yaml
- generic [ref=e3]:
  - generic:
    - img "Virtual User" [ref=e5] [cursor=pointer]
    - img "NPC West" [ref=e6] [cursor=pointer]
    - img "NPC North" [ref=e7] [cursor=pointer]
    - img "NPC East" [ref=e8] [cursor=pointer]
```

# Test source

```ts
  1  | import { expect, test } from '@playwright/test'
  2  | 
  3  | test.beforeEach(async ({ page }) => {
  4  |   page.on('pageerror', error => console.error('[browser pageerror]', error.message))
  5  |   page.on('console', message => {
  6  |     if (message.type() === 'error') console.error('[browser console]', message.text())
  7  |   })
  8  |   await page.goto('/e2e/game-fixture.html', { waitUntil: 'domcontentloaded' })
  9  |   await expect.poll(() => page.evaluate(() => window.__majakGameFixture?.ready()), { timeout: 60_000 }).toBe(true)
  10 | })
  11 | 
  12 | test('fresh game acknowledges presentation only after the legacy seat, dice, and deal sequence', async ({ page }) => {
  13 |   await page.evaluate(() => window.__majakGameFixture.emitFreshGameStart())
  14 | 
  15 |   await expect.poll(() => page.evaluate(() => window.__majakGameFixture.seatRevealCount())).toBe(4)
  16 |   await page.waitForTimeout(1150)
  17 |   await expect.poll(() => page.evaluate(() => window.__majakGameFixture.seatRevealTextures().length)).toBe(4)
  18 | 
  19 |   const earlyPresentationReady = await page.evaluate(() => window.__majakGameFixture.sent()
  20 |     .filter(message => message.kind === 'invoke' && message.name === 'NotifyGamePresentationReady'))
  21 |   expect(earlyPresentationReady).toHaveLength(0)
  22 | 
  23 |   await expect.poll(() => page.evaluate(() => window.__majakGameFixture.sent()
  24 |     .find(message => message.kind === 'invoke' && message.name === 'NotifyGamePresentationReady')?.payload), { timeout: 12_000 }).toEqual([901, 1])
  25 |   expect(await page.evaluate(() => window.__majakGameFixture.visibleHandCount())).toBe(52)
  26 | 
  27 |   const reconnectInvocations = await page.evaluate(() => window.__majakGameFixture.sent()
  28 |     .filter(message => message.kind === 'invoke' && message.name === 'RequestGameResync'))
  29 |   expect(reconnectInvocations).toHaveLength(0)
  30 | })
  31 | 
  32 | test('mobile timeout discards the preselected tile after another-seat prompt and hand reorder', async ({ page }) => {
  33 |   await page.evaluate(() => window.__majakGameFixture.setTimeScale(10))
  34 |   await page.evaluate(() => window.__majakGameFixture.emitFreshGameStart())
  35 |   await expect.poll(() => page.evaluate(() => window.__majakGameFixture.sent()
> 36 |     .some(message => message.kind === 'invoke' && message.name === 'NotifyGamePresentationReady')), { timeout: 3000 }).toBe(true)
     |                                                                                                                        ^ Error: expect(received).toBe(expected) // Object.is equality
  37 | 
  38 |   const selectedTile = await page.evaluate(() => window.__majakGameFixture.tileCenter(4))
  39 |   expect(selectedTile.bipaiIndex).toBeDefined()
  40 |   await page.touchscreen.tap(selectedTile.x, selectedTile.y)
  41 |   await expect.poll(() => page.evaluate(() => window.__majakGameFixture.selectedBipaiIndex())).toBe(selectedTile.bipaiIndex)
  42 | 
  43 |   await page.evaluate(() => window.__majakGameFixture.emitOtherSeatPrompt())
  44 |   expect(await page.evaluate(() => window.__majakGameFixture.selectedBipaiIndex())).toBe(selectedTile.bipaiIndex)
  45 | 
  46 |   await page.evaluate(() => window.__majakGameFixture.emitLocalTurnPrompt())
  47 |   await expect.poll(() => page.evaluate(() => window.__majakGameFixture.sent().find(message => {
  48 |     if (message.kind !== 'send' || message.name !== 'playing') return false
  49 |     const payload = message.payload as { playType?: string }
  50 |     return payload.playType === 'MJPID_ACTION'
  51 |   })?.payload)).toMatchObject({
  52 |     playType: 'MJPID_ACTION',
  53 |     bipaiIndex: [selectedTile.bipaiIndex],
  54 |     actionSeq: 11,
  55 |   })
  56 | })
  57 | 
```