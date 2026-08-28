# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: skin-resource.spec.ts >> downloads equipped board and tile resources before starting the game scene
- Location: e2e\skin-resource.spec.ts:3:1

# Error details

```
Error: expect(received).toBe(expected) // Object.is equality

Expected: true
Received: false

Call Log:
- Timeout 60000ms exceeded while waiting on the predicate
```

# Test source

```ts
  1  | import { expect, test } from '@playwright/test'
  2  | 
  3  | test('downloads equipped board and tile resources before starting the game scene', async ({ page }) => {
  4  |   const skinResponses: Array<{ url: string; status: number }> = []
  5  |   page.on('response', response => {
  6  |     if (response.url().includes('/assets/images/game/skin/')) {
  7  |       skinResponses.push({ url: response.url(), status: response.status() })
  8  |     }
  9  |   })
  10 | 
  11 |   await page.goto('/e2e/game-fixture.html?bg=16&bgType=12&hai=20', { waitUntil: 'domcontentloaded' })
> 12 |   await expect.poll(() => page.evaluate(() => window.__majakGameFixture?.ready()), { timeout: 60_000 }).toBe(true)
     |                                                                                                         ^ Error: expect(received).toBe(expected) // Object.is equality
  13 | 
  14 |   const requiredUrls = [
  15 |     '/skin/16/mj_board_16.png',
  16 |     '/skin/16/mj_h_bg_16.png',
  17 |     '/skin/20/mj_hai_omote_0_20.png',
  18 |     '/skin/20/mj_hai_sutehai_0_20.png',
  19 |     '/skin/20/mj_hai_dora_20.png',
  20 |   ]
  21 |   for (const requiredUrl of requiredUrls) {
  22 |     expect(skinResponses.some(response => response.url.includes(requiredUrl) && response.status === 200), requiredUrl).toBe(true)
  23 |   }
  24 |   for (const textureKey of ['mj_board', 'mj_h_bg', 'hai_omote', 'hai_sute', 'hai_dora']) {
  25 |     expect(await page.evaluate(key => window.__majakGameFixture.textureExists(key), textureKey), textureKey).toBe(true)
  26 |   }
  27 | })
```