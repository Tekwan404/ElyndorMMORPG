import { expect, test } from '@playwright/test'

test('shows the mobile game shell', async ({ page }) => {
  const browserErrors: string[] = []

  page.on('console', (message) => {
    if (message.type() === 'error') {
      browserErrors.push(`console: ${message.text()}`)
    }
  })
  page.on('pageerror', (error) => browserErrors.push(`page: ${error.message}`))

  await page.route('**/api/v1/status', async (route) => {
    await route.fulfill({
      contentType: 'application/json',
      json: {
        service: 'Elyndor.Server',
        status: 'ready',
        utcNow: '2026-08-29T00:00:00Z',
      },
    })
  })

  await page.goto('/')

  await expect(page.getByRole('heading', { name: 'Северные земли' })).toBeVisible()
  await expect(page.getByRole('navigation', { name: 'Основная навигация' })).toBeVisible()
  await expect(page.getByText('Сервер доступен')).toBeVisible()
  await expect(page.getByRole('main')).toBeVisible()
  await expect(page.getByRole('link', { name: 'Мир' })).toHaveAttribute('aria-current', 'page')
  await expect(page.getByRole('button', { name: 'Герой' })).toBeDisabled()
  await expect(page.getByRole('button', { name: 'Локация' })).toBeDisabled()
  await expect(page.getByRole('button', { name: 'Квесты' })).toBeDisabled()
  await expect(page.getByRole('button', { name: 'Меню' })).toBeDisabled()

  expect(page.viewportSize()?.width).toBeLessThanOrEqual(430)
  expect(
    await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth),
  ).toBe(false)
  expect(browserErrors).toEqual([])
})
