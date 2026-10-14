import { expect, test, type Page } from '@playwright/test'

test.describe.configure({ timeout: 60_000 })

async function openPreview(page: Page, party: number, width = 390, height = 844): Promise<void> {
  await page.setViewportSize({ width, height })
  await page.goto(`/dev/battle?party=${party}`)
  await expect(page.locator('[data-battle-screen]')).toBeVisible({ timeout: 15_000 })
  await expect(page.locator('[data-character-figure]')).toHaveCount(party)
  await expect
    .poll(
      () =>
        page
          .locator('[data-character-figure] img, [data-allies-strip] img')
          .evaluateAll(
            (images) =>
              images.length > 0 &&
              images.every(
                (image) =>
                  image instanceof HTMLImageElement && image.complete && image.naturalWidth > 0,
              ),
          ),
      { timeout: 15_000 },
    )
    .toBe(true)
}

test('three-player baseline fits without scroll and protects the arena', async ({ page }) => {
  await openPreview(page, 3)

  const metrics = await page.locator('[data-battle-screen]').evaluate((screen) => {
    const arena = screen.querySelector<HTMLElement>('[data-battle-arena]')!
    const abilities = [...screen.querySelectorAll<HTMLElement>('[data-ability-slot]')]
    const arenaRect = arena.getBoundingClientRect()
    return {
      pageScrollHeight: document.documentElement.scrollHeight,
      viewportHeight: window.innerHeight,
      pageScrollWidth: document.documentElement.scrollWidth,
      viewportWidth: window.innerWidth,
      arenaRatio: arenaRect.height / window.innerHeight,
      smallestAbility: Math.min(
        ...abilities.map((ability) =>
          Math.min(ability.getBoundingClientRect().width, ability.getBoundingClientRect().height),
        ),
      ),
    }
  })

  expect(metrics.pageScrollHeight).toBeLessThanOrEqual(metrics.viewportHeight)
  expect(metrics.pageScrollWidth).toBeLessThanOrEqual(metrics.viewportWidth)
  expect(metrics.arenaRatio).toBeGreaterThanOrEqual(0.58)
  expect(metrics.arenaRatio).toBeLessThanOrEqual(0.62)
  expect(metrics.smallestAbility).toBeGreaterThanOrEqual(44)
  expect(metrics.smallestAbility).toBeLessThanOrEqual(54)
  await page.screenshot({
    path: '../../output/playwright/battle-screen-party-3-390x844.png',
    fullPage: true,
  })
})

test('solo character remains prominent on mobile and desktop', async ({ page }) => {
  for (const viewport of [
    { name: 'mobile', width: 390, height: 844 },
    { name: 'desktop', width: 820, height: 900 },
  ]) {
    await openPreview(page, 1, viewport.width, viewport.height)

    const geometry = await page.locator('[data-battle-arena]').evaluate((arena) => {
      const actor = arena.querySelector<HTMLElement>('[data-slot-id="solo-frontline"]')!
      const enemy = arena.querySelector<HTMLElement>('.battle-arena__enemy')!
      const arenaRect = arena.getBoundingClientRect()
      const actorRect = actor.getBoundingClientRect()
      const enemyRect = enemy.getBoundingClientRect()
      return {
        actorWidthRatio: actorRect.width / arenaRect.width,
        actorHeightRatio: actorRect.height / arenaRect.height,
        enemyWidthRatio: enemyRect.width / arenaRect.width,
        enemyHeightRatio: enemyRect.height / arenaRect.height,
        enemyCenterOffset: Math.abs(
          enemyRect.x + enemyRect.width / 2 - (arenaRect.x + arenaRect.width / 2),
        ),
        depthOrdered: actorRect.top > enemyRect.top && actorRect.bottom > enemyRect.bottom,
      }
    })

    expect(geometry.actorWidthRatio).toBeGreaterThanOrEqual(0.37)
    expect(geometry.actorHeightRatio).toBeGreaterThanOrEqual(0.5)
    expect(geometry.enemyWidthRatio).toBeGreaterThanOrEqual(0.44)
    expect(geometry.enemyHeightRatio).toBeLessThanOrEqual(0.5)
    expect(geometry.enemyCenterOffset).toBeLessThan(2)
    expect(geometry.depthOrdered).toBe(true)
    await page.screenshot({
      path: `../../output/playwright/battle-screen-solo-${viewport.name}.png`,
      fullPage: true,
    })
  }
})

test('five-player formation stays readable and selection does not follow aggro', async ({
  page,
}) => {
  await openPreview(page, 5)

  await expect(page.locator('[data-battle-arena]')).toHaveAttribute(
    'data-formation-mode',
    'all-visible',
  )
  await page.locator('[data-ally-status="ally-2"]').click()
  await page.locator('[data-preview-transfer-aggro]').dispatchEvent('click')

  await expect(page.locator('[data-preview-aggro-target]')).toHaveAttribute(
    'data-preview-aggro-target',
    'ally-4',
  )
  await expect(page.locator('[data-character-figure="ally-2"]')).toHaveAttribute(
    'data-selected',
    'true',
  )
  await expect(page.locator('[data-character-figure="ally-2"]')).toHaveAttribute(
    'data-aggro',
    'false',
  )
  await expect(page.locator('[data-character-figure="ally-4"]')).toHaveAttribute(
    'data-aggro',
    'true',
  )
  await expect(page.locator('[data-character-figure="ally-4"]')).toHaveAttribute(
    'data-selected',
    'false',
  )
  await expect(page.locator('[data-character-figure="ally-4"]')).toHaveCount(1)
  await page.screenshot({
    path: '../../output/playwright/battle-screen-party-5-390x844.png',
    fullPage: true,
  })
})

test('narrow and reduced-motion layouts keep controls inside the viewport', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await openPreview(page, 3, 360, 800)

  const overflow = await page.evaluate(() => ({
    horizontal: document.documentElement.scrollWidth > window.innerWidth,
    abilityOverlap: (() => {
      const arena = document
        .querySelector<HTMLElement>('[data-battle-arena]')!
        .getBoundingClientRect()
      const skills = document
        .querySelector<HTMLElement>('[data-skill-panel]')!
        .getBoundingClientRect()
      return arena.bottom > skills.top
    })(),
  }))

  expect(overflow.horizontal).toBe(false)
  expect(overflow.abilityOverlap).toBe(false)
  await page.screenshot({
    path: '../../output/playwright/battle-screen-party-3-360x800.png',
    fullPage: true,
  })
})

test('mobile hotbar uses five columns and pages large ability kits without layout shift', async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await page.goto('/dev/battle?party=1&abilities=16&error=true')

  const screen = page.locator('[data-battle-screen]')
  const grid = page.locator('[data-skill-grid]')
  const slots = page.locator('[data-ability-slot]')
  const toast = page.locator('[data-combat-error-toast]')
  await expect(screen).toBeVisible({ timeout: 15_000 })
  await expect(slots).toHaveCount(10)
  await expect(slots.nth(9)).toBeVisible()
  await expect(toast).toBeVisible()

  const collapsed = await grid.evaluate((element) => ({
    columns: getComputedStyle(element).gridTemplateColumns.split(' ').length,
    horizontalOverflow: element.scrollWidth > element.clientWidth,
    position: getComputedStyle(document.querySelector('[data-combat-error-toast]')!).position,
  }))
  expect(collapsed.columns).toBe(5)
  expect(collapsed.horizontalOverflow).toBe(false)
  expect(collapsed.position).toBe('fixed')
  await page.screenshot({
    path: '../../output/playwright/battle-screen-16-abilities-collapsed-390x844.png',
    fullPage: true,
  })

  await page.locator('[data-skill-next]').click()
  await expect(slots).toHaveCount(6)
  await expect(page.locator('[data-ability-slot="PREVIEW_ABILITY_16"]')).toBeVisible()
  expect(await grid.evaluate((element) => element.scrollWidth > element.clientWidth)).toBe(false)
  await page.setViewportSize({ width: 820, height: 900 })
  await expect(slots).toHaveCount(6)
  await page.locator('[data-skill-prev]').click()
  await expect(slots).toHaveCount(10)
})

test('party, companion and multiple enemies keep distinct readable targets', async ({ page }) => {
  for (const viewport of [
    { width: 320, height: 740 },
    { width: 390, height: 844 },
  ]) {
    await page.setViewportSize(viewport)
    await page.goto('/dev/battle?party=5&abilities=10&enemies=3&companion=true')
    await expect(page.locator('[data-ally-status]')).toHaveCount(5, { timeout: 15_000 })
    const geometry = await page.locator('[data-battle-arena]').evaluate((arena) => {
      const enemy = arena.querySelector<HTMLElement>('.battle-arena__enemy')!
      const caption = arena.querySelector<HTMLElement>('.battle-arena__enemy-caption')!
      const strip = arena.querySelector<HTMLElement>('.battle-arena__enemy-strip')!
      const companion = arena.querySelector<HTMLElement>('[data-combat-companion]')!
      const status = [...arena.querySelectorAll<HTMLElement>('[data-ally-status]')]
      const rects = [caption, strip, companion, ...status].map((element) =>
        element.getBoundingClientRect(),
      )
      const overlap = rects.some((a, i) =>
        rects
          .slice(i + 1)
          .some(
            (b) => a.left < b.right && a.right > b.left && a.top < b.bottom && a.bottom > b.top,
          ),
      )
      const rect = caption.getBoundingClientRect()
      const hit = document.elementFromPoint(rect.x + rect.width / 2, rect.y + rect.height / 2)
      return {
        overlap,
        enemyClickable: hit !== null && enemy.contains(hit),
        overflow: document.documentElement.scrollWidth > window.innerWidth,
      }
    })
    expect(geometry).toEqual({ overlap: false, enemyClickable: true, overflow: false })
    for (const target of ['local', 'ally-1', 'ally-2', 'ally-3', 'ally-4']) {
      await page.locator(`[data-ally-status="${target}"]`).click()
      await expect(page.locator(`[data-ally-status="${target}"]`)).toHaveAttribute(
        'aria-pressed',
        'true',
      )
    }
  }
})
