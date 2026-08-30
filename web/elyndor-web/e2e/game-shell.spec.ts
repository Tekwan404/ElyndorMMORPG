import { expect, test, type Page } from '@playwright/test'

test('creates a hero, travels, and restores the world on reload', async ({ page }) => {
  const browserErrors: string[] = []
  page.on('console', (message) => {
    if (message.type() === 'error') browserErrors.push(message.text())
  })
  page.on('pageerror', (error) => browserErrors.push(error.message))
  page.on('requestfailed', (request) =>
    browserErrors.push(`${request.method()} ${request.url()}: ${request.failure()?.errorText}`),
  )
  await installMockApiUnlessReal(page)
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Создание героя' })).toBeVisible()
  await page.getByLabel('Имя').fill(characterName())
  await page.getByLabel('Лучник').check()
  await page.getByRole('button', { name: 'Войти в мир' }).click()
  await expect(page.getByRole('heading', { name: 'Starter Town' })).toBeVisible()
  await page.getByRole('button', { name: /Whispering Forest/ }).click()
  await expect(page.getByRole('heading', { name: 'Whispering Forest' })).toBeVisible()
  await page.getByRole('button', { name: /Deep Forest/ }).click()
  await expect(page.getByRole('heading', { name: 'Deep Forest' })).toBeVisible()
  await page.reload()
  await expect(page.getByRole('heading', { name: 'Deep Forest' })).toBeVisible()
  expect(page.viewportSize()?.width).toBeLessThanOrEqual(430)
  expect(await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth)).toBe(
    false,
  )
  expect(browserErrors).toEqual([])
})

async function installMockApiUnlessReal(page: Page): Promise<void> {
  if (process.env.ELYNDOR_E2E_REAL === 'true') return

  let hasCharacter = false
  let locationId: keyof typeof locations = 'STARTER_TOWN'
  await page.route('**/api/v1/auth/development', (route) =>
    route.fulfill({ json: { accessToken: 'test-token', expiresAtUtc: '2026-08-30T12:15:00Z' } }),
  )
  await page.route('**/api/v1/bootstrap', (route) =>
    route.fulfill({ json: snapshot(hasCharacter, locationId) }),
  )
  await page.route('**/api/v1/character', async (route) => {
    hasCharacter = true
    await route.fulfill({
      json: {
        id: '00000000-0000-0000-0000-000000000001',
        name: 'Arthas',
        raceId: 'HUMAN',
        genderId: 'MALE',
        classId: 'ARCHER',
        level: 1,
        createdAtUtc: '2026-08-30T12:00:00Z',
      },
    })
  })
  await page.route('**/api/v1/world/travel', async (route) => {
    locationId = (route.request().postDataJSON() as { targetLocationId: keyof typeof locations })
      .targetLocationId
    await route.fulfill({ json: { locationId, version: 2 } })
  })
}

function characterName(): string {
  return process.env.ELYNDOR_E2E_REAL === 'true' ? `Hero${asLetters(Date.now())}` : 'Arthas'
}

function asLetters(value: number): string {
  let remaining = value
  let result = ''
  while (remaining > 0 && result.length < 10) {
    result += String.fromCharCode(97 + (remaining % 26))
    remaining = Math.floor(remaining / 26)
  }
  return result
}

const locations = {
  STARTER_TOWN: {
    id: 'STARTER_TOWN',
    displayName: 'Starter Town',
    dangerLevel: 'SAFE',
    recommendedLevel: 1,
  },
  WHISPERING_FOREST: {
    id: 'WHISPERING_FOREST',
    displayName: 'Whispering Forest',
    dangerLevel: 'ADVENTURE',
    recommendedLevel: 1,
  },
  DEEP_FOREST: {
    id: 'DEEP_FOREST',
    displayName: 'Deep Forest',
    dangerLevel: 'DANGEROUS',
    recommendedLevel: 3,
  },
} as const

function snapshot(hasCharacter: boolean, locationId: keyof typeof locations) {
  const transitions =
    locationId === 'STARTER_TOWN'
      ? [locations.WHISPERING_FOREST]
      : locationId === 'WHISPERING_FOREST'
        ? [locations.STARTER_TOWN, locations.DEEP_FOREST]
        : [locations.WHISPERING_FOREST]
  return {
    accountId: '00000000-0000-0000-0000-000000000002',
    character: hasCharacter
      ? {
          id: '00000000-0000-0000-0000-000000000001',
          name: 'Arthas',
          raceId: 'HUMAN',
          genderId: 'MALE',
          classId: 'ARCHER',
          level: 1,
        }
      : null,
    world: hasCharacter
      ? { currentLocation: locations[locationId], version: 1, outgoingTransitions: transitions }
      : null,
    contentVersion: '0.1.0',
    balanceVersion: '0.1.0',
    serverTimeUtc: '2026-08-30T12:00:00Z',
  }
}
