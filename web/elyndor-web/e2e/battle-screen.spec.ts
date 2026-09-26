import { expect, test, type Page } from '@playwright/test'

test.describe.configure({ timeout: 60_000 })

async function openPreview(page: Page, party: number, width = 390, height = 844): Promise<void> {
  await page.setViewportSize({ width, height })
  await page.goto(`/dev/battle?party=${party}`)
  await expect(page.locator('[data-battle-screen]')).toBeVisible({ timeout: 15_000 })
  await expect(page.locator('[data-character-figure]')).toHaveCount(party)
  await expect
    .poll(() =>
      page
        .locator('[data-character-figure] img, [data-allies-strip] img')
        .evaluateAll(
          (images) =>
            images.length > 0 && images.every((image) => image.complete && image.naturalWidth > 0),
        ),
    )
    .toBe(true, { timeout: 15_000 })
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
  expect(metrics.smallestAbility).toBeGreaterThanOrEqual(64)
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
        actorsOverlap: actorRect.right > enemyRect.left,
      }
    })

    expect(geometry.actorWidthRatio).toBeGreaterThanOrEqual(0.43)
    expect(geometry.actorHeightRatio).toBeGreaterThanOrEqual(0.83)
    expect(geometry.enemyWidthRatio).toBeGreaterThanOrEqual(0.42)
    expect(geometry.enemyHeightRatio).toBeGreaterThanOrEqual(0.81)
    expect(geometry.actorsOverlap).toBe(false)
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
  await page.locator('[data-ally-strip-actor="ally-2"]').click()
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
