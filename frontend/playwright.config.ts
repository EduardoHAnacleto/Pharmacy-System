import process from 'node:process'
import { defineConfig, devices } from '@playwright/test'

/**
 * https://playwright.dev/docs/test-configuration
 */

/**
 * Escape hatch for environments that ship a preinstalled Chromium.
 *
 * Playwright resolves its browser by an exact build number, so a container with a
 * slightly older Chromium than this @playwright/test expects fails to launch even
 * though a perfectly usable binary is present. Setting
 * `PLAYWRIGHT_CHROMIUM_PATH=/path/to/chrome` points it at that binary instead.
 *
 * CI never sets it and downloads its own browsers, so CI behaviour is unchanged.
 */
const chromiumPath = process.env.PLAYWRIGHT_CHROMIUM_PATH

export default defineConfig({
  testDir: './e2e',

  timeout: 30 * 1000,
  expect: {
    timeout: 5000,
  },

  /* Fail the build on CI if a test.only was left in the source. */
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: process.env.CI ? [['line'], ['html', { open: 'never' }]] : 'html',

  use: {
    actionTimeout: 0,

    /* Dev server locally for a fast loop; the built bundle on CI, which is what ships. */
    baseURL: process.env.CI ? 'http://localhost:4173' : 'http://localhost:5173',

    trace: 'on-first-retry',
    headless: !!process.env.CI,
  },

  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        ...(chromiumPath ? { launchOptions: { executablePath: chromiumPath } } : {}),
      },
    },
    {
      name: 'firefox',
      use: { ...devices['Desktop Firefox'] },
    },
    {
      name: 'webkit',
      use: { ...devices['Desktop Safari'] },
    },
  ],

  webServer: {
    command: process.env.CI ? 'npm run preview' : 'npm run dev',
    port: process.env.CI ? 4173 : 5173,
    reuseExistingServer: !process.env.CI,
  },
})
