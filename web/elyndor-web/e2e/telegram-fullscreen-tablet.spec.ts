import { expect, test } from '@playwright/test'

test.use({ viewport: { width: 1024, height: 1366 } })

test('uses the iPad canvas without forcing immersive Telegram fullscreen', async ({ page }) => {
  await page.route('https://telegram.org/js/telegram-web-app.js?63', (route) =>
    route.fulfill({
      contentType: 'application/javascript',
      body: `
        window.__elyndorExpandCalls = 0;
        window.Telegram = {
          WebApp: {
            initData: '',
            isFullscreen: false,
            viewportStableHeight: 1366,
            safeAreaInset: { top: 20, right: 0, bottom: 20, left: 0 },
            contentSafeAreaInset: { top: 54, right: 0, bottom: 28, left: 0 },
            ready() {},
            expand() { window.__elyndorExpandCalls += 1; },
            onEvent() {},
            offEvent() {},
          },
        };
      `,
    }),
  )

  await page.goto('/')
  const shell = page.locator('.game-shell')
  await expect(shell).toBeVisible()

  await expect.poll(() => page.evaluate(() => (window as typeof window & {
    __elyndorExpandCalls?: number
  }).__elyndorExpandCalls ?? 0)).toBe(1)

  expect(
    await page.evaluate(() =>
      document.documentElement.style.getPropertyValue('--elyndor-tg-content-safe-area-top'),
    ),
  ).toBe('54px')
  expect(await page.evaluate(() => document.documentElement.dataset.telegramFullscreen)).toBe('false')

  const tabletBox = await shell.boundingBox()
  expect(tabletBox).not.toBeNull()
  expect(tabletBox?.width ?? 0).toBeGreaterThan(900)
  expect(tabletBox?.width ?? 0).toBeLessThanOrEqual(1024)
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)

  await page.setViewportSize({ width: 390, height: 844 })
  const phoneBox = await shell.boundingBox()
  expect(phoneBox).not.toBeNull()
  expect(phoneBox?.width ?? 0).toBeLessThanOrEqual(390)
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
})
