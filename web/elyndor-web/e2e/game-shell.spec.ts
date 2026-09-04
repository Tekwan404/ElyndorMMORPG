import { expect, test, type Page } from '@playwright/test'

test('creates a hero, travels, and restores the world on reload', async ({ page }) => {
  const browserErrors: string[] = []
  page.on('console', (message) => {
    if (
      message.type() === 'error'
      && !isExpectedMockRealtimeFailure(message.text())
    ) {
      browserErrors.push(message.text())
    }
  })
  page.on('pageerror', (error) => browserErrors.push(error.message))
  page.on('requestfailed', (request) => {
    if (isMockRealtimeRequest(request.url())) return
    browserErrors.push(`${request.method()} ${request.url()}: ${request.failure()?.errorText}`)
  })
  await installMockApiUnlessReal(page)
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Создание героя' })).toBeVisible()
  await page.screenshot({
    path: '../../output/playwright/session-2a-character-creation.png',
    fullPage: true,
  })
  await page.getByLabel('Имя').fill(characterName())
  await page.getByLabel('Лучник').check()
  await page.getByRole('button', { name: 'Войти в мир' }).click()
  await expect(page.getByRole('heading', { name: 'Стартовый город' })).toBeVisible()
  await page.getByRole('button', { name: /Шепчущий лес/ }).click()
  await expect(page.getByRole('heading', { name: 'Шепчущий лес' })).toBeVisible()
  await page.getByRole('button', { name: /Deep Forest/ }).click()
  await expect(page.getByRole('heading', { name: 'Deep Forest' })).toBeVisible()
  expect(
    await page.evaluate(() => document.documentElement.scrollHeight <= window.innerHeight),
  ).toBe(true)
  const navigationBox = await page
    .getByRole('navigation', { name: 'Основная навигация' })
    .boundingBox()
  expect(navigationBox).not.toBeNull()
  expect((navigationBox?.y ?? 0) + (navigationBox?.height ?? 0)).toBeLessThanOrEqual(
    page.viewportSize()?.height ?? 0,
  )
  await page.screenshot({ path: '../../output/playwright/session-2a-world.png', fullPage: true })
  await page.getByRole('button', { name: 'Герой' }).click()
  await expect(page.getByRole('heading', { name: 'Arthas' })).toBeVisible()
  await page.getByRole('button', { name: 'Инвентарь' }).click()
  await expect(page.getByRole('heading', { name: 'Рюкзак' })).toBeVisible()
  await expect(page.getByText('Рюкзак пуст')).toBeVisible()
  expect(
    await page.evaluate(() => document.documentElement.scrollHeight <= window.innerHeight),
  ).toBe(true)
  await page.screenshot({ path: '../../output/playwright/session-2a-hero.png', fullPage: true })
  await page.getByRole('button', { name: 'Мир' }).click()
  await page.reload()
  await expect(page.getByRole('heading', { name: 'Deep Forest' })).toBeVisible()
  expect(page.viewportSize()?.width).toBeLessThanOrEqual(430)
  expect(await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth)).toBe(
    false,
  )
  await page.setViewportSize({ width: 320, height: 568 })
  expect(await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth)).toBe(
    false,
  )
  await page.screenshot({
    path: '../../output/playwright/session-2a-world-320.png',
    fullPage: true,
  })
  expect(browserErrors).toEqual([])
})

async function installMockApiUnlessReal(page: Page): Promise<void> {
  if (process.env.ELYNDOR_E2E_REAL === 'true') return

  let hasCharacter = false
  let locationId: keyof typeof locations = 'STARTER_TOWN'
  await page.route('https://telegram.org/js/telegram-web-app.js?63', (route) =>
    route.fulfill({
      contentType: 'application/javascript',
      body: 'window.Telegram = { WebApp: { initData: "", ready() {}, expand() {} } };',
    }),
  )
  await page.route('**/api/v1/auth/development', async (route) => {
    if (process.env.ELYNDOR_E2E_LIVE_AUTH === 'true') {
      await route.fulfill({ response: await route.fetch() })
      return
    }

    await route.fulfill({
      json: { accessToken: 'test-token', expiresAtUtc: '2026-08-30T12:15:00Z' },
    })
  })
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
        primaryAttribute: 'AGILITY',
        classProfileVersion: '0.2.0',
        stats: archerStats,
        vitals: archerVitals,
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

function isMockRealtimeRequest(url: string): boolean {
  return process.env.ELYNDOR_E2E_REAL !== 'true' && url.includes('/hubs/combat')
}

function isExpectedMockRealtimeFailure(message: string): boolean {
  return process.env.ELYNDOR_E2E_REAL !== 'true'
    && message.includes('[combat-realtime]')
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
          experience: 35,
          xpToNextLevel: 100,
          gold: 0,
          primaryAttribute: 'AGILITY',
          classProfileVersion: '0.2.0',
          knownAbilityIds: [],
          knownAbilities: [],
          stats: archerStats,
          statBreakdown: emptyStatBreakdown,
          vitals: archerVitals,
          inventory: emptyInventory,
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

const archerStats = {
  strength: 5,
  agility: 9,
  intellect: 5,
  stamina: 7,
  maxHp: 120,
  attackPower: 19,
  spellPower: 10,
  criticalChance: 7.25,
  criticalDamage: 100,
  accuracy: 95,
  armorPenetration: 0,
  magicPenetration: 0,
  attackSpeed: 1,
  armor: 19,
  magicResistance: 12,
  dodge: 1.8,
}

const archerVitals = {
  currentHp: 120,
  maxHp: 120,
  resourceType: 'FOCUS',
  currentResource: 100,
  maxResource: 100,
  checkpointedAtUtc: '2026-08-30T12:00:00Z',
}

const emptyInventory = {
  items: [],
  equipped: {
    weapon: null,
    head: null,
    chest: null,
    legs: null,
    boots: null,
    accessory: null,
  },
}

const emptyStatBreakdown = Object.fromEntries(
  Object.keys(archerStats).map((key) => [
    key,
    { finalValue: archerStats[key as keyof typeof archerStats], contributions: [] },
  ]),
)
