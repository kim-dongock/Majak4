import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  workers: 1,
  timeout: 90_000,
  expect: { timeout: 5_000 },
  use: {
    ...devices['iPhone 13 landscape'],
    baseURL: 'http://127.0.0.1:4175',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    defaultBrowserType: 'chromium',
    channel: 'chrome',
  },
  webServer: {
    command: 'npm run dev -- --host 127.0.0.1 --port 4175',
    url: 'http://127.0.0.1:4175/e2e/game-fixture.html',
    reuseExistingServer: true,
    timeout: 120_000,
  },
})
