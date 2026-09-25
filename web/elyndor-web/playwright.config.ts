import process from 'node:process'
import { defineConfig, devices } from '@playwright/test'

const usesExternalServer = Boolean(process.env.ELYNDOR_E2E_BASE_URL)
const usesDevelopmentPreview = process.env.ELYNDOR_E2E_UI_PREVIEW === 'true'
const usesRealBackend = process.env.ELYNDOR_E2E_REAL === 'true'
const localServerPort = usesDevelopmentPreview || !process.env.CI ? 5173 : 4173

/**
 * Read environment variables from file.
 * https://github.com/motdotla/dotenv
 */
// require('dotenv').config();

/**
 * See https://playwright.dev/docs/test-configuration.
 */
export default defineConfig({
  testDir: './e2e',
  testIgnore: usesRealBackend ? ['**/battle-screen.spec.ts'] : [],
  /* Maximum time one test can run for. */
  timeout: 30 * 1000,
  expect: {
    /**
     * Maximum time expect() should wait for the condition to be met.
     * For example in `await expect(locator).toHaveText();`
     */
    timeout: 5000,
  },
  /* Fail the build on CI if you accidentally left test.only in the source code. */
  forbidOnly: !!process.env.CI,
  /* Real database E2E is stateful; retries would reuse the same account after partial success. */
  retries: process.env.ELYNDOR_E2E_REAL === 'true' ? 0 : process.env.CI ? 2 : 0,
  /* Opt out of parallel tests on CI. */
  workers: process.env.CI ? 1 : undefined,
  /* Reporter to use. See https://playwright.dev/docs/test-reporters */
  reporter: 'html',
  /* Shared settings for all the projects below. See https://playwright.dev/docs/api/class-testoptions. */
  use: {
    /* Maximum time each action such as `click()` can take. Defaults to 0 (no limit). */
    actionTimeout: 0,
    /* Base URL to use in actions like `await page.goto('/')`. */
    baseURL:
      process.env.ELYNDOR_E2E_BASE_URL ??
      `http://localhost:${localServerPort}`,

    /* Collect trace when retrying the failed test. See https://playwright.dev/docs/trace-viewer */
    trace: 'on-first-retry',

    /* Only on CI systems run the tests headless */
    headless: true,
  },

  /* Telegram Mini App is mobile-first; expand the matrix when browser-specific UI appears. */
  projects: [
    {
      name: 'mobile-chromium',
      use: {
        ...devices['Pixel 5'],
      },
    },
  ],

  outputDir: '../../output/playwright',

  /* Folder for test artifacts such as screenshots, videos, traces, etc. */
  // outputDir: 'test-results/',

  /* Run your local dev server before starting the tests */
  webServer: usesExternalServer
    ? undefined
    : {
        /**
         * Use the dev server locally and for development-only UI previews.
         * Other CI browser tests use the production preview server.
         * Playwright will re-use the local server if there is already a dev-server running.
         */
        command: usesDevelopmentPreview || !process.env.CI ? 'npm run dev' : 'npm run preview',
        port: localServerPort,
        reuseExistingServer: !process.env.CI,
      },
})
