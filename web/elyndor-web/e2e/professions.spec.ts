import { expect, test, type Page } from '@playwright/test'

test('learns and restores Skinning and Leatherworking against the real database', async ({ page }) => {
  test.skip(process.env.ELYNDOR_E2E_REAL !== 'true', 'requires the real ASP.NET/PostgreSQL E2E server')
  // This suite shares its test account with the world-travel smoke test.
  // Returning from Deep Forest uses real timed travel, not an instant teleport.
  test.setTimeout(180_000)

  await installTelegramBridge(page)
  await page.goto('/')

  const creationHeading = page.getByRole('heading', { name: 'Создание героя' })
  if (await creationHeading.isVisible().catch(() => false)) {
    await page.getByLabel('Имя').fill(`Prof${Date.now().toString().slice(-8)}`)
    await page.getByLabel('Лучник').check()
    await page.getByRole('button', { name: 'Войти в мир' }).click()
    await expect(page.getByRole('button', { name: 'Меню' })).toBeVisible()
  }

  await openProfessions(page)

  const skinningCard = page.locator('.profession-card').filter({ hasText: 'Снятие шкур' })
  const leatherworkingCard = page.locator('.profession-card').filter({ hasText: 'Кожевничество' })

  // The profession list is loaded asynchronously. Wait for the real cards before
  // deciding whether a profession has already been learned; Locator.isVisible()
  // itself does not wait and can otherwise race the initial API response on CI.
  await expect(skinningCard).toBeVisible()
  await expect(leatherworkingCard).toBeVisible()

  if (await skinningCard.getByRole('button', { name: 'Изучить профессию' }).isVisible()) {
    await expect(skinningCard.getByRole('button', { name: 'Изучить профессию' })).toBeEnabled()
    const learnResponse = page.waitForResponse(response =>
      response.url().endsWith('/api/v1/professions/learn')
      && response.request().method() === 'POST')
    await skinningCard.getByRole('button', { name: 'Изучить профессию' }).click()
    const response = await learnResponse
    expect(response.status()).toBe(200)
    await expectLearnedProfession(response, 'SKINNING')
  }
  await expect(skinningCard).toContainText('Навык 1 / 300')

  if (await leatherworkingCard.getByRole('button', { name: 'Изучить профессию' }).isVisible()) {
    await expect(leatherworkingCard.getByRole('button', { name: 'Изучить профессию' })).toBeEnabled()
    const learnResponse = page.waitForResponse(response =>
      response.url().endsWith('/api/v1/professions/learn')
      && response.request().method() === 'POST')
    await leatherworkingCard.getByRole('button', { name: 'Изучить профессию' }).click()
    const response = await learnResponse
    expect(response.status()).toBe(200)
    await expectLearnedProfession(response, 'LEATHERWORKING')
  }
  await expect(leatherworkingCard).toContainText('Навык 1 / 300')
  await expect(page.locator('.professions-hero > span')).toHaveText('2 / 2')

  await page.reload()
  await openProfessions(page)

  await expect(page.locator('.profession-card').filter({ hasText: 'Снятие шкур' })).toContainText('Навык 1 / 300')
  await expect(page.locator('.profession-card').filter({ hasText: 'Кожевничество' })).toContainText('Навык 1 / 300')
  await expect(page.locator('.professions-hero > span')).toHaveText('2 / 2')
})

async function openProfessions(page: Page): Promise<void> {
  await page.locator('[data-nav="location"]').click()

  // The preceding real-world journey can leave this same E2E character in Deep Forest.
  // Walk the actual world routes back to town instead of bypassing location authority.
  if (!(await page.locator('[data-city-hub]').isVisible())) {
    await page.locator('[data-nav="world"]').click()
    for (const locationId of ['WHISPERING_FOREST', 'STARTER_TOWN']) {
      const marker = page.locator(`[data-location-id="${locationId}"]`)
      if ((await marker.getAttribute('data-state')) === 'current') continue
      await marker.click()
      const travel = page.locator('[data-map-travel-inline]')
      // A second trip cannot be issued until the previous travel has fully settled.
      await expect(travel).toBeEnabled({ timeout: 15_000 })
      await travel.click()
      await expect(marker).toHaveAttribute('data-state', 'current', { timeout: 90_000 })
    }
    await page.locator('[data-nav="location"]').click()
  }

  await expect(page.locator('[data-city-hub]')).toBeVisible()
  await page.locator('[data-city-marker="craft"]').click()
  await page.locator('[data-city-professions]').click()
  await expect(page.getByRole('heading', { name: 'Профессии' })).toBeVisible()
}

async function expectLearnedProfession(
  response: Awaited<ReturnType<Page['waitForResponse']>>,
  professionId: 'SKINNING' | 'LEATHERWORKING',
): Promise<void> {
  const payload = (await response.json()) as {
    isSuccess?: boolean
    state?: { learned?: Array<{ id?: string; skill?: number; maxSkill?: number }> }
  }

  expect(payload.isSuccess).toBe(true)
  expect(payload.state?.learned).toEqual(expect.arrayContaining([
    expect.objectContaining({ id: professionId, skill: 1, maxSkill: 300 }),
  ]))
}

async function installTelegramBridge(page: Page): Promise<void> {
  await page.route('https://telegram.org/js/telegram-web-app.js?63', route =>
    route.fulfill({
      contentType: 'application/javascript',
      body: 'window.Telegram = { WebApp: { initData: "", ready() {}, expand() {} } };',
    }),
  )
}
