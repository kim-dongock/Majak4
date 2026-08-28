import { expect, test } from '@playwright/test'

test('downloads the equipped skin into canonical Phaser texture keys', async ({ page }) => {
  const skinResponses: Array<{ url: string; status: number }> = []
  const browserErrors: string[] = []
  page.on('pageerror', error => browserErrors.push(error.message))
  page.on('console', message => {
    if (message.type() === 'error') browserErrors.push(message.text())
  })
  page.on('requestfailed', request => browserErrors.push(`${request.url()}: ${request.failure()?.errorText}`))
  page.on('response', response => {
    if (response.url().includes('/assets/images/game/skin/')) {
      skinResponses.push({ url: response.url(), status: response.status() })
    }
  })

  await page.goto('/e2e/game-fixture.html?bg=100001&bgType=11&hai=100003', { waitUntil: 'domcontentloaded' })
  await expect.poll(() => page.evaluate(() => window.__majakGameFixture?.ready()), {
    timeout: 15_000,
    message: () => `Phaser did not start: ${browserErrors.join('\n')}`,
  }).toBe(true)

  const requiredUrls = [
    '/skin/100001/mj_board_100001.png',
    '/skin/100001/mj_h_bg_100001.png',
  ]
  for (const requiredUrl of requiredUrls) {
    expect(skinResponses.some(response => response.url.includes(requiredUrl) && response.status === 200), requiredUrl).toBe(true)
  }
  for (const textureKey of ['mj_board', 'mj_h_bg']) {
    expect(await page.evaluate(key => window.__majakGameFixture.textureExists(key), textureKey), textureKey).toBe(true)
  }
  expect(await page.evaluate(() => window.__majakGameFixture.boardTexture())).toMatchObject({
    exists: true,
    key: 'mj_board',
    scaleX: 1.5,
    scaleY: 1.5,
  })
  expect((await page.evaluate(() => window.__majakGameFixture.boardTexture())).source)
    .toContain('/skin/100001/mj_board_100001.png')
})