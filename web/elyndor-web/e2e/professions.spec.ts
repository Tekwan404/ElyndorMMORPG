import { expect, test, type Page } from '@playwright/test'

test('learns and restores Skinning and Leatherworking against the real database', async ({ page }) => {
  test.skip(process.env.ELYNDOR_E2E_REAL !== 'true', 'requires the real ASP.NET/PostgreSQL E2E server')

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

  if (await skinningCard.getByRole('button', { name: 'Изучить профессию' }).isVisible().catch(() => false)) {
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

  if (await leatherworkingCard.getByRole('button', { name: 'Изучить профессию' }).isVisible().catch(() => false)) {
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
  await page.getByRole('button', { name: 'Меню' }).click()
  await expect(page.getByRole('heading', { name: 'Меню' })).toBeVisible()
  await page.getByRole('button', { name: 'Профессии Сбор и ремесло' }).click()
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
