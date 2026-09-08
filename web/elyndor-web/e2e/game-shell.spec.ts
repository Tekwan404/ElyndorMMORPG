import { expect, test, type Page } from '@playwright/test'

test('creates a hero, travels, and restores the world on reload', async ({ page }) => {
  const browserErrors: string[] = []
  page.on('console', (message) => {
    if (
      message.type() === 'error'
      && !isExpectedMockRealtimeFailure(message.text(), message.location().url)
    ) {
      browserErrors.push(message.text())
    }
  })
  page.on('pageerror', (error) => browserErrors.push(error.message))
  page.on('requestfailed', (request) => {
    if (isMockRealtimeRequest(request.url()) || isExpectedNavigationAbort(request)) return
    browserErrors.push(`${request.method()} ${request.url()}: ${request.failure()?.errorText}`)
  })
  await installMockApiUnlessReal(page)
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Создание героя' })).toBeVisible()
  await page.screenshot({
    path: '../../output/playwright/session-2a-character-creation.png',
    fullPage: true,
  })
  const heroName = characterName()
  await page.getByLabel('Имя').fill(heroName)
  await page.getByLabel('Лучник').check()
  await page.getByRole('button', { name: 'Войти в мир' }).click()
  await expect(page.getByRole('heading', { name: 'Стартовый город' })).toBeVisible()

  await page.getByRole('button', { name: 'Торговать' }).click()
  const merchantDialog = page.getByRole('dialog', { name: 'Торговец' })
  await expect(merchantDialog).toBeVisible()
  await expect(merchantDialog.getByText('Маркус', { exact: true })).toBeVisible()
  await expect(merchantDialog.locator('[data-merchant-offer]').first()).toBeVisible()
  await merchantDialog.getByRole('button', { name: 'Close' }).click()
  await expect(merchantDialog).toBeHidden()

  await page.getByRole('button', { name: 'Мир' }).click()
  await expect(page.getByRole('heading', { name: 'Карта мира' })).toBeVisible()
  await page.locator('[data-location-id="WHISPERING_FOREST"]').click()
  await page.locator('[data-map-travel]').click()
  await page.getByRole('button', { name: 'Локация' }).click()
  await expect(page.getByRole('heading', { name: 'Шепчущий лес' })).toBeVisible()
  await expect(page.locator('[data-location-travel]')).toHaveCount(0)

  await page.getByRole('button', { name: 'Мир' }).click()
  await page.locator('[data-location-id="DEEP_FOREST"]').click()
  await expect(page.getByText('Требуется 6 уровень.')).toBeVisible()
  await expect(page.locator('[data-map-travel]')).toBeDisabled()
  await page.getByRole('button', { name: 'Локация' }).click()
  await expect(page.getByRole('heading', { name: 'Шепчущий лес' })).toBeVisible()
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
  await expect(page.getByRole('heading', { name: 'Снаряжение' })).toBeVisible()
  await expect(page.locator('[data-hero-name]')).toHaveText(heroName)
  await page.getByRole('button', { name: 'Инвентарь' }).click()
  await expect(page.getByRole('heading', { name: 'Рюкзак' })).toBeVisible()
  await expect(page.getByText('Рюкзак пуст')).toBeVisible()
  expect(
    await page.evaluate(() => document.documentElement.scrollHeight <= window.innerHeight),
  ).toBe(true)
  await page.screenshot({ path: '../../output/playwright/session-2a-hero.png', fullPage: true })
  await page.getByRole('button', { name: 'Локация' }).click()
  await page.reload()
  await expect(page.getByRole('heading', { name: 'Шепчущий лес' })).toBeVisible()
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
  // Keep CI independent from telegram.org availability. This only stubs the host
  // platform bridge; in real mode every Elyndor API call still reaches ASP.NET/PostgreSQL.
  await page.route('https://telegram.org/js/telegram-web-app.js?63', (route) =>
    route.fulfill({
      contentType: 'application/javascript',
      body: 'window.Telegram = { WebApp: { initData: "", ready() {}, expand() {} } };',
    }),
  )

  if (process.env.ELYNDOR_E2E_REAL === 'true') return

  await page.route('**/api/v1/inventory/merchant/MARCUS_SUPPLIES', (route) =>
    route.fulfill({
      json: {
        id: 'MARCUS_SUPPLIES',
        name: 'Маркус',
        description: 'Торговец припасами.',
        gold: 0,
        items: [
          {
            definitionId: 'SMALL_HEALING_POTION',
            name: 'Малое зелье лечения',
            type: 'Consumable',
            rarity: 'Common',
            description: 'Мгновенно восстанавливает здоровье.',
            buyPriceGold: 20,
            sellPriceGold: 0,
            consumableActions: [
              {
                type: 'RestoreHp',
                amount: 50,
                resourceType: null,
                effectId: null,
                dispelCategory: null,
              },
            ],
            consumableCooldownCategoryId: 'HEALING_POTION',
            consumableCooldownSeconds: 30,
            iconId: null,
          },
        ],
      },
    }),
  )

  let hasCharacter = false
  let locationId: keyof typeof locations = 'STARTER_TOWN'
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
  await page.route('**/api/v1/world/locations', (route) =>
    route.fulfill({ json: Object.values(locations) }),
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
  await page.route('**/api/v1/party', (route) =>
    route.fulfill({ json: null }),
  )
  await page.route('**/api/v1/party/invites', (route) =>
    route.fulfill({ json: [] }),
  )
  await page.route('**/api/v1/dungeons', (route) =>
    route.fulfill({ json: [] }),
  )
  await page.route('**/api/v1/dungeons/current', (route) =>
    route.fulfill({ json: null }),
  )
}

function isMockRealtimeRequest(url: string): boolean {
  return process.env.ELYNDOR_E2E_REAL !== 'true' && url.includes('/hubs/combat')
}

function isExpectedNavigationAbort(request: { failure(): { errorText?: string } | null }): boolean {
  return request.failure()?.errorText === 'net::ERR_ABORTED'
}

function isExpectedMockRealtimeFailure(message: string, sourceUrl = ''): boolean {
  if (process.env.ELYNDOR_E2E_REAL === 'true') return false
  return sourceUrl.includes('/hubs/combat')
    || message.includes('[combat-realtime]')
    || message.includes('Failed to complete negotiation with the server')
    || message.includes('Failed to start the connection')
    || message.includes('Failed to start the transport')
    || message === 'Failed to load resource: the server responded with a status of 502 (Bad Gateway)'
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
    displayName: 'Стартовый город',
    dangerLevel: 'SAFE',
    recommendedLevel: 1,
    minimumLevel: 1,
    maximumLevel: 60,
    requiredContractId: null,
    artId: null,
    description: 'Безопасная стартовая локация.',
  },
  WHISPERING_FOREST: {
    id: 'WHISPERING_FOREST',
    displayName: 'Шепчущий лес',
    dangerLevel: 'ADVENTURE',
    recommendedLevel: 3,
    minimumLevel: 1,
    maximumLevel: 5,
    requiredContractId: null,
    artId: null,
    description: 'Лесная зона 1–5 уровня.',
  },
  DEEP_FOREST: {
    id: 'DEEP_FOREST',
    displayName: 'Глубокий лес',
    dangerLevel: 'DANGEROUS',
    recommendedLevel: 9,
    minimumLevel: 6,
    maximumLevel: 11,
    requiredContractId: null,
    artId: null,
    description: 'Опасная лесная зона 6–11 уровня.',
  },
} as const

function snapshot(hasCharacter: boolean, locationId: keyof typeof locations) {
  const transitions =
    locationId === 'STARTER_TOWN'
      ? [locations.WHISPERING_FOREST]
      : locationId === 'WHISPERING_FOREST'
        ? [locations.STARTER_TOWN]
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
      ? {
          currentLocation: locations[locationId],
          version: 1,
          outgoingTransitions: transitions,
          contracts: [],
        }
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
