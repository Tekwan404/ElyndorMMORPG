import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { apiClient } from '@/api/apiClient'
import type { BootstrapSnapshot, InventoryItem } from '@/api/contracts'
import ForgeView from '@/game/character/views/ForgeView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

enableAutoUnmount(afterEach)
function item(id: string, equipped = false): InventoryItem {
  return {
    id,
    definitionId: id,
    name: `Предмет ${id}`,
    type: 'Equipment',
    rarity: 'Epic',
    requiredLevel: 30,
    quantity: 1,
    slot: 'MainHand',
    equippedSlot: equipped ? 'MainHand' : null,
    weaponCategory: 'Sword',
    isLocked: false,
    transactionLocked: false,
    iconId: null,
    stats: {},
    generatedItem: {
      itemPower: 250,
      maxItemPower: 300,
      itemLevel: 30,
      stars: 4,
      rollQuality: 80,
      affixes: [
        { slotKey: 'A', statId: 'ATTACK_POWER', value: 15, min: 10, max: 20, step: 1, affixTier: 3, isGuaranteed: false, isReforgeSlot: false },
        { slotKey: 'B', statId: 'STAMINA', value: 5, min: 4, max: 8, step: 1, affixTier: 2, isGuaranteed: true, isReforgeSlot: false },
      ],
    },
  } as InventoryItem
}
const reward = {
  reward: {
    reforgeStoneItemId: 'REFORGE_STONE',
    reforgeStoneQuantity: 2,
    materialItemId: 'FORGE_SCRAP',
    materialQuantity: 4,
  },
  enhancementRefund: {
    enhancementMaterialItemId: 'ENHANCEMENT_ORE',
    enhancementMaterialQuantity: 1,
    catalystItemId: null,
    catalystQuantity: 0,
  },
}
function click(selector: string) {
  const button = document.body.querySelector<HTMLButtonElement>(selector)
  expect(button).not.toBeNull()
  button!.click()
  return flushPromises()
}
function cards() {
  return document.body.querySelectorAll('.forge-grid [data-forge-item]')
}
function text() {
  return document.body.textContent ?? ''
}
beforeEach(() => {
  Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
    configurable: true,
    value: vi.fn<() => void>(),
  })
  setActivePinia(createPinia())
  document.body.innerHTML = ''
  const store = useGameSessionStore()
  store.snapshot = {
    character: {
      id: 'test',
      gold: 100000,
      inventory: {
        items: [
          item('A'),
          item('B'),
          item('E', true),
          { ...item('LOCK'), isLocked: true },
          { ...item('STONE'), type: 'Material', definitionId: 'REFORGE_STONE', quantity: 50 },
          { ...item('ORE'), type: 'Material', definitionId: 'ENHANCEMENT_ORE', quantity: 50 },
        ],
      },
    },
  } as BootstrapSnapshot
  vi.spyOn(store, 'getReforgePreview').mockImplementation(async (id, key) => ({
    itemInstanceId: id,
    slotKey: key,
    current: item(id).generatedItem!,
    cost: {
      gold: 10,
      materialItemId: 'REFORGE_STONE',
      materialQuantity: 1,
      catalystItemId: '',
      catalystQuantity: 0,
      countMultiplier: 1,
    },
    possibleAffixes: [
      { statId: 'ATTACK_POWER', min: 10, max: 20, step: 1 },
      { statId: 'CRITICAL_DAMAGE', min: 12, max: 24, step: 0.1 },
    ],
  }))
  vi.spyOn(store, 'getSalvagePreview').mockImplementation(async (id) => ({
    ...reward,
    characterItemId: id,
    requiresConfirmation: true,
  }))
  vi.spyOn(apiClient, 'request').mockResolvedValue({
    currentEnhancementLevel: 2,
    targetEnhancementLevel: 3,
    isMaximumEnhancement: false,
    intrinsicItemPower: 250,
    finalItemPower: 280,
    cost: {
      gold: 20,
      enhancementMaterialItemId: 'ENHANCEMENT_ORE',
      enhancementMaterialQuantity: 10,
      catalystQuantity: 0,
      catalystItemId: null,
    },
  })
})
afterEach(() => vi.restoreAllMocks())

describe('Forge workbench', () => {
  it('ignores a late preview after another item has been selected', async () => {
    const preview = vi.mocked(useGameSessionStore().getReforgePreview)
    const result = await preview('A', 'A')
    let resolve!: (value: typeof result) => void
    preview.mockImplementationOnce(
      () =>
        new Promise((done) => {
          resolve = done
        }),
    )
    mount(ForgeView)
    await click('.forge-grid [data-forge-item="A"]')
    await click('.forge-grid [data-forge-item="B"]')
    resolve({ ...result!, cost: { ...result!.cost, materialQuantity: 999 } })
    await flushPromises()
    expect(text()).not.toContain('999')
    expect(
      document.body
        .querySelector('.forge-grid [data-forge-item="B"]')
        ?.getAttribute('aria-pressed'),
    ).toBe('true')
    expect(document.body.querySelector<HTMLButtonElement>('[data-forge-roll]')!.disabled).toBe(
      false,
    )
  })
  it('shows item quality, affix quality and the real reforge pool', async () => {
    mount(ForgeView)
    await click('.forge-grid [data-forge-item="A"]')
    expect(text()).toContain('Качество предмета')
    expect(text()).toContain('80%')
    expect(text()).toContain('50% · T3')
    expect(text()).toContain('Может выпасть')
    expect(text()).toContain('Крит. урон')
    expect(text()).toContain('12–24%')
  })

  it('restores a pending paid reforge rather than charging for a new roll', async () => {
    const store = useGameSessionStore()
    store.snapshot!.character!.inventory.items[0]!.transactionLocked = true
    const restored = vi.spyOn(store, 'getPendingReforge').mockResolvedValue({
      operationId: 'restored',
      slotKey: 'A',
      current: item('A').generatedItem!,
      proposed: item('A').generatedItem!,
    } as never)
    mount(ForgeView)
    await click('.forge-grid [data-forge-item="A"]')
    expect(restored).toHaveBeenCalledExactlyOnceWith('A')
    expect(text()).toContain('Выберите, что оставить')
    expect(document.body.querySelector('[data-forge-roll]')).toBeNull()
  })
  it('disables enhancement when the required materials are missing', async () => {
    useGameSessionStore().snapshot!.character!.inventory.items = [item('A')]
    mount(ForgeView)
    await click('[data-forge-mode="upgrade"]')
    await click('.forge-grid [data-forge-item="A"]')
    expect(
      document.body.querySelector<HTMLButtonElement>('[data-forge-enhancement]')!.disabled,
    ).toBe(true)
  })
  it('keeps mode navigation, sorting and cards visible after selection, with no All source', async () => {
    mount(ForgeView)
    await click('.forge-grid [data-forge-item="A"]')
    expect(cards()).toHaveLength(3)
    expect(document.body.querySelectorAll('[data-forge-mode]')).toHaveLength(3)
    expect(document.body.querySelectorAll('[data-forge-sort]')).toHaveLength(4)
    expect(document.body.querySelector('[data-forge-filter="all"]')).toBeNull()
    expect(text()).toContain('Текущие характеристики')
    expect(document.body.querySelector<HTMLButtonElement>('[data-forge-affix="B"]')!.disabled).toBe(
      true,
    )
    expect(document.body.querySelector('[data-forge-affix="B"]')?.textContent).toContain('🔒')
    expect(document.body.querySelector('[data-forge-roll]')?.textContent).toContain('Перековать за')
  })
  it('reforges an equipped item without unequipping and handles the paid decision', async () => {
    const store = useGameSessionStore()
    const response = {
      operationId: 'op',
      slotKey: 'A',
      current: item('E').generatedItem!,
      proposed: item('E').generatedItem!,
    } as never
    const roll = vi.spyOn(store, 'rollReforge').mockResolvedValue(response)
    const decide = vi.spyOn(store, 'decideReforge').mockResolvedValue(response)
    mount(ForgeView)
    await click('[data-forge-filter="equipped"]')
    await click('.forge-grid [data-forge-item="E"]')
    await click('[data-forge-roll]')
    expect(roll).toHaveBeenCalledWith('E', 'A')
    expect(text()).toContain('Выберите, что оставить')
    const button = [...document.body.querySelectorAll<HTMLButtonElement>('button')].find((b) =>
      b.textContent?.includes('Применить новую'),
    )!
    button.click()
    await flushPromises()
    expect(decide).toHaveBeenCalledWith('op', true)
    expect(text()).toContain('Новая характеристика применена')
    expect(document.body.querySelector('[data-forge-detail]')).not.toBeNull()
    expect(store.getReforgePreview).toHaveBeenCalledTimes(2)
  })
  it('scrolls once on the first selection and switches later items in place', async () => {
    mount(ForgeView)
    await click('.forge-grid [data-forge-item="A"]')
    const detail = document.body.querySelector<HTMLElement>('[data-forge-detail]')!
    expect(detail.scrollIntoView).toHaveBeenCalledWith({ behavior: 'smooth', block: 'start' })

    await click('[data-forge-change-item]')
    expect(document.body.querySelector('[data-forge-item-picker]')).not.toBeNull()
    await click('[data-forge-item-picker] [data-forge-item="B"]')

    expect(detail.scrollIntoView).toHaveBeenCalledTimes(1)
    expect(document.body.querySelector('[data-forge-item-picker]')).toBeNull()
    expect(document.body.querySelector('[data-forge-detail]')?.textContent).toContain('Предмет B')
  })
  it('selecting a card does not switch out of enhancement and uses its server preview', async () => {
    const enhance = vi.spyOn(useGameSessionStore(), 'enhanceItem').mockResolvedValue({
      itemInstanceId: 'A',
      enhancementLevel: 3,
      enhancementBonusPercent: 0.06,
      finalItemPower: 280,
    })
    mount(ForgeView)
    await click('[data-forge-mode="upgrade"]')
    await click('.forge-grid [data-forge-item="A"]')
    expect(text()).toContain('Текущее усиление +2')
    expect(text()).toContain('Звёзды и качество предмета не меняются')
    expect(text()).toContain('280')
    await click('[data-forge-enhancement]')
    expect(enhance).toHaveBeenCalledExactlyOnceWith('A')
  })
  it('selects all eligible backpack items, sums server rewards and requires confirmation', async () => {
    const salvage = vi.spyOn(useGameSessionStore(), 'salvageItemDetailed').mockResolvedValue(reward)
    mount(ForgeView)
    await click('[data-forge-mode="salvage"]')
    await click('[data-forge-select-all]')
    expect(text()).toContain('Выбрано предметов: 2')
    expect(text()).toContain('+8')
    await click('[data-forge-salvage-submit]')
    expect(salvage).not.toHaveBeenCalled()
    await click('[data-confirm-accept]')
    expect(salvage.mock.calls).toEqual([
      ['A', true],
      ['B', true],
    ])
    expect(text()).toContain('Разобрано предметов: 2')
  })
  it('prevents selecting equipped and protected items for salvage', async () => {
    mount(ForgeView)
    await click('[data-forge-mode="salvage"]')
    expect(
      document.body.querySelector<HTMLButtonElement>('.forge-grid [data-forge-item="LOCK"]')!
        .disabled,
    ).toBe(true)
    await click('[data-forge-filter="equipped"]')
    expect(
      document.body.querySelector<HTMLButtonElement>('.forge-grid [data-forge-item="E"]')!.disabled,
    ).toBe(true)
    expect(text()).toContain('Надетую вещь нельзя разобрать')
  })
  it('preserves salvage selection across filters and clears explicitly', async () => {
    mount(ForgeView)
    await click('[data-forge-mode="salvage"]')
    await click('.forge-grid [data-forge-item="A"]')
    await click('[data-forge-category="armor"]')
    expect(cards()).toHaveLength(0)
    expect(text()).toContain('Выбрано предметов: 1')
    await click('[data-forge-clear]')
    expect(text()).toContain('Выбрано предметов: 0')
  })
  it('reports partial batch failure without pretending all items were dismantled', async () => {
    vi.spyOn(useGameSessionStore(), 'salvageItemDetailed')
      .mockResolvedValueOnce(reward)
      .mockResolvedValueOnce(null)
    mount(ForgeView)
    await click('[data-forge-mode="salvage"]')
    await click('[data-forge-select-all]')
    await click('[data-forge-salvage-submit]')
    await click('[data-confirm-accept]')
    expect(text()).toContain('Разобрано 1 из 2')
    expect(text()).not.toContain('Разобрано предметов: 2')
  })
})
