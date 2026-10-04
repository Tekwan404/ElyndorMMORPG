import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import type { BootstrapSnapshot, InventoryItem } from '@/api/contracts'
import InventoryView from '@/game/character/views/InventoryView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

enableAutoUnmount(afterEach)

describe('InventoryView', () => {
  it('keeps saved sorting separate for different characters', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot([equipment('BLADE', 'Меч', 'Rare', 1, 3)], currentWeapon())
    const wrapper = mount(InventoryView)
    await wrapper.get('[data-inventory-sort]').setValue('rarity')
    store.snapshot = {
      ...store.snapshot,
      character: { ...store.snapshot.character!, id: 'OTHER_CHARACTER' },
    }
    await flushPromises()
    expect(wrapper.get<HTMLSelectElement>('[data-inventory-sort]').element.value).toBe('default')
    store.snapshot = snapshot([equipment('BLADE', 'Меч', 'Rare', 1, 3)], currentWeapon())
    await flushPromises()
    expect(wrapper.get<HTMLSelectElement>('[data-inventory-sort]').element.value).toBe('rarity')
  })

  it.each(['broken json', '{"sort":"unknown","view":"unknown"}'])(
    'keeps items usable when saved preferences are invalid: %s',
    (saved) => {
      const store = useGameSessionStore()
      store.snapshot = snapshot([equipment('BLADE', 'Меч', 'Rare', 1, 3)], currentWeapon())
      localStorage.setItem(`elyndor.inventory.preferences.${store.snapshot.character!.id}`, saved)
      const wrapper = mount(InventoryView)
      expect(itemIds(wrapper)).toEqual(['BLADE'])
      expect(wrapper.get<HTMLSelectElement>('[data-inventory-sort]').element.value).toBe('default')
    },
  )
  it('restores the chosen sort after reopening and resetting filters does not reset it', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot(
      [
        equipment('COMMON', 'Старый меч', 'Common', 1, 3),
        equipment('EPIC', 'Новый меч', 'Epic', 1, 4),
      ],
      currentWeapon(),
    )
    const first = mount(InventoryView)
    await first.get('[data-inventory-sort]').setValue('name')
    first.unmount()
    const wrapper = mount(InventoryView)
    expect(wrapper.get<HTMLSelectElement>('[data-inventory-sort]').element.value).toBe('name')
    await wrapper.get('[data-inventory-search]').setValue('старый')
    await wrapper.get('[data-inventory-active-filters] button').trigger('click')
    expect(wrapper.get<HTMLSelectElement>('[data-inventory-sort]').element.value).toBe('name')
    expect(itemIds(wrapper)).toEqual(['EPIC', 'COMMON'])
  })

  it('inserts newly granted equipment according to the saved sort rather than server insertion order', async () => {
    const store = useGameSessionStore()
    const common = equipment('COMMON', 'Старый меч', 'Common', 1, 3)
    store.snapshot = snapshot([common], currentWeapon())
    const wrapper = mount(InventoryView)
    await wrapper.get('[data-inventory-sort]').setValue('rarity')
    store.snapshot = snapshot(
      [common, equipment('EPIC', 'Новый меч', 'Epic', 1, 4)],
      currentWeapon(),
    )
    await flushPromises()
    expect(itemIds(wrapper)).toEqual(['EPIC', 'COMMON'])
    expect(wrapper.get('[data-item-id="EPIC"]').attributes('data-new')).toBe('true')
  })

  it('compares accuracy, critical damage and both penetration stats, including lost bonuses', async () => {
    const store = useGameSessionStore()
    const old = currentWeapon()
    old.stats.accuracy = 4
    old.stats.magicPenetration = 8
    const candidate = item({
      ...equipment('BLADE', 'Новый меч', 'Rare', 1, 3),
      stats: { criticalDamage: 15, armorPenetration: 6 },
    })
    store.snapshot = snapshot([candidate], old)
    const wrapper = mount(InventoryView)
    await wrapper.get('[data-item-id="BLADE"]').trigger('click')
    const comparison = document.querySelector('[aria-label="Сравнение с надетым предметом"]')!
    expect(comparison.textContent).toContain('Критический урон')
    expect(comparison.textContent).toContain('Пробивание брони')
    expect(comparison.textContent).toContain('Пробивание магии')
    expect(comparison.textContent).toContain('Точность')
    expect(
      [...comparison.querySelectorAll('[data-delta="down"]')].map((node) =>
        node.textContent?.trim(),
      ),
    ).toContain('-8')
  })
  it('switches icon density without losing category or selection and does not render captions', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot(
      [
        equipment('BLADE', 'Клинок сумеречного дозора', 'Rare', 1, 3),
        consumable('POTION', 'Зелье'),
      ],
      currentWeapon(),
    )
    const wrapper = mount(InventoryView)
    expect(wrapper.get('[data-inventory-layout]').attributes('data-inventory-layout')).toBe('grid')
    expect(wrapper.get('[data-item-id="BLADE"]').text()).not.toContain('Клинок сумеречного дозора')
    await wrapper.get('[data-inventory-category="equipment"]').trigger('click')
    await wrapper.get('[data-inventory-selection-mode]').trigger('click')
    await wrapper.get('[data-item-id="BLADE"]').trigger('click')
    await wrapper.get('[data-inventory-view="list"]').trigger('click')
    expect(wrapper.get('[data-inventory-layout]').attributes('data-inventory-layout')).toBe('list')
    expect(itemIds(wrapper)).toEqual(['BLADE'])
    expect(wrapper.get('[data-item-id="BLADE"]').attributes('aria-pressed')).toBe('true')
  })
  it('finds items by name without losing the selected category and can clear the search', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot(
      [equipment('BLADE', 'Клинок сумерек', 'Rare', 1, 3), consumable('POTION', 'Зелье сумерек')],
      currentWeapon(),
    )
    const wrapper = mount(InventoryView)
    await wrapper.get('[data-inventory-category="equipment"]').trigger('click')
    await wrapper.get('[data-inventory-search]').setValue('  СУМЕРЕК ')
    expect(itemIds(wrapper)).toEqual(['BLADE'])
    await wrapper.get('[data-inventory-search]').setValue('несуществующий')
    expect(itemIds(wrapper)).toEqual([])
    expect(wrapper.text()).toContain('Ничего не найдено')
    await wrapper.get('[data-clear-inventory-search]').trigger('click')
    expect(itemIds(wrapper)).toEqual(['BLADE'])
  })

  it('groups supplies together while keeping artifacts in equipment and materials separate', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot(
      [
        consumable('POTION', 'Зелье'),
        item({ id: 'CHEST', name: 'Сундук', type: 'LootContainer', rarity: 'Rare' }),
        item({
          id: 'ARTIFACT',
          name: 'Артефакт',
          type: 'SpatialArtifact' as InventoryItem['type'],
          rarity: 'Epic',
        }),
        item({ id: 'ORE', name: 'Руда', type: 'Material', rarity: 'Common' }),
      ],
      currentWeapon(),
    )
    const wrapper = mount(InventoryView)
    await wrapper.get('[data-inventory-category="supplies"]').trigger('click')
    expect(itemIds(wrapper)).toEqual(['POTION', 'CHEST'])
    await wrapper.get('[data-inventory-category="equipment"]').trigger('click')
    expect(itemIds(wrapper)).toEqual(['ARTIFACT'])
    await wrapper.get('[data-inventory-category="material"]').trigger('click')
    expect(itemIds(wrapper)).toEqual(['ORE'])
    expect(wrapper.get('[data-item-id="ORE"]').attributes('aria-label')).toBe('Руда')
  })

  it('organizes a mixed bag without changing server order and keeps explicit sorting available', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot(
      [consumable('POTION', 'Зелье'), equipment('BLADE', 'Клинок', 'Rare', 1, 3)],
      currentWeapon(),
    )
    const wrapper = mount(InventoryView)
    expect(itemIds(wrapper)).toEqual(['BLADE', 'POTION'])
    expect(store.snapshot.character!.inventory.items[0]!.id).toBe('POTION')
    await wrapper.get('[data-inventory-sort]').setValue('received')
    expect(itemIds(wrapper)).toEqual(['POTION', 'BLADE'])
  })
  it('offers pending-loot read retry without disabling the inventory', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot([equipment('BLADE', 'Клинок', 'Rare', 1, 3)], currentWeapon())
    const load = vi
      .spyOn(store, 'getPendingLoot')
      .mockRejectedValueOnce(new Error('network'))
      .mockResolvedValue({ items: [] })
    const wrapper = mount(InventoryView)
    await flushPromises()
    expect(wrapper.get('[data-pending-loot-read-error]').text()).toContain('Повторить')
    expect(wrapper.get('[data-item-id="BLADE"]').attributes('disabled')).toBeUndefined()
    await wrapper.get('[data-pending-loot-read-error] button').trigger('click')
    await flushPromises()
    expect(load).toHaveBeenCalledTimes(2)
    expect(wrapper.find('[data-pending-loot-read-error]').exists()).toBe(false)
  })
  it('does not use a consumable while its protection operation is pending', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot([consumable('POTION', 'Зелье')], currentWeapon())
    store.snapshot.character!.vitals.currentHp = 100
    vi.spyOn(store, 'isMutationPending').mockImplementation((key) => key === 'inventory:set-lock')
    const wrapper = mount(InventoryView)
    await wrapper.get('[data-item-id="POTION"]').trigger('click')
    const button = [...document.querySelectorAll<HTMLButtonElement>('[role="dialog"] button')].find(
      (button) => button.textContent?.includes('Использовать'),
    )!
    expect(button.disabled).toBe(true)
  })
  it('blocks protection changes while the same inventory equipment operation is pending', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot([equipment('BLADE', 'Клинок', 'Rare', 1, 3)], currentWeapon())
    vi.spyOn(store, 'isMutationPending').mockImplementation((key) => key === 'inventory:equip')
    const wrapper = mount(InventoryView)
    await wrapper.get('[data-item-id="BLADE"]').trigger('click')
    expect(document.querySelector<HTMLButtonElement>('[data-item-lock-action]')!.disabled).toBe(
      true,
    )
  })
  it('keeps a failed consumable action open with visible feedback', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot([consumable('POTION', 'Зелье')], currentWeapon())
    store.snapshot.character!.vitals.currentHp = 100
    const use = vi.spyOn(store, 'useConsumable').mockImplementation(async () => {
      store.errorCode = 'inventory_consumable_failed'
    })
    const wrapper = mount(InventoryView)
    await wrapper.get('[data-item-id="POTION"]').trigger('click')
    const button = [...document.querySelectorAll<HTMLButtonElement>('[role="dialog"] button')].find(
      (button) => button.textContent?.includes('Использовать'),
    )!
    button.click()
    await flushPromises()
    expect(use).toHaveBeenCalledWith('POTION')
    expect(document.querySelector('[role="dialog"]')).not.toBeNull()
    expect(document.body.textContent).toContain('Не удалось использовать')
    expect(wrapper.find('[data-inventory-feedback]').exists()).toBe(false)
  })
  it('reviews destructive actions in a game dialog before removing the item', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot(
      [equipment('RARE_BLADE', 'Редкий клинок', 'Rare', 1, 3)],
      currentWeapon(),
    )
    const discard = vi.spyOn(store, 'discardInventoryItems').mockResolvedValue(true)
    vi.spyOn(window, 'confirm').mockReturnValue(false)
    const wrapper = mount(InventoryView)
    await wrapper.get('[data-item-id="RARE_BLADE"]').trigger('click')
    await flushPromises()
    document.querySelector<HTMLButtonElement>('[data-item-discard-action]')!.click()
    await flushPromises()
    expect(document.querySelector('[data-confirmation]')?.textContent).toContain('Уничтожить')
    expect(discard).not.toHaveBeenCalled()
    document.querySelector<HTMLButtonElement>('[data-confirm-accept]')!.click()
    await flushPromises()
    expect(discard).toHaveBeenCalledWith([{ characterItemId: 'RARE_BLADE', quantity: 1 }])
    wrapper.unmount()
  })

  it('does not invent ranger bonuses for an unrelated set item', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot(
      [item({ ...equipment('SET_BLADE', 'Меч ордена', 'Rare', 1, 3), setId: 'ORDER_WARRIOR' })],
      currentWeapon(),
    )
    const wrapper = mount(InventoryView)
    await wrapper.get('[data-item-id="SET_BLADE"]').trigger('click')
    await flushPromises()
    const dialog = document.querySelector('[role="dialog"]')!
    expect(dialog.textContent).not.toContain('комплекта Следопыта')
    expect(dialog.querySelector('[data-item-set]')).not.toBeNull()
    wrapper.unmount()
  })
  beforeEach(() => {
    setActivePinia(createPinia())
    globalThis.localStorage.clear()
    document.body.innerHTML = ''
    vi.restoreAllMocks()
  })

  it('sorts bag items without mutating the authoritative inventory order', async () => {
    const store = useGameSessionStore()
    const common = equipment('COMMON_BLADE', 'Старый клинок', 'Common', 1, 3)
    const epic = equipment('EPIC_BLADE', 'Клинок сумерек', 'Epic', 5, 12)
    store.snapshot = snapshot([common, epic], currentWeapon())

    const wrapper = mount(InventoryView)
    await flushPromises()

    expect(itemIds(wrapper)).toEqual(['EPIC_BLADE', 'COMMON_BLADE'])
    await wrapper.get('[data-inventory-sort]').setValue('received')
    expect(itemIds(wrapper)).toEqual(['COMMON_BLADE', 'EPIC_BLADE'])

    await wrapper.get('[data-inventory-sort]').setValue('rarity')
    await flushPromises()
    expect(itemIds(wrapper)).toEqual(['EPIC_BLADE', 'COMMON_BLADE'])
    expect(store.snapshot.character?.inventory.items.map((item) => item.id)).toEqual([
      'COMMON_BLADE',
      'EPIC_BLADE',
      'CURRENT_WEAPON',
    ])
  })

  it('filters inventory down to equipment the current character can wear now', async () => {
    const store = useGameSessionStore()
    const wearable = item({
      id: 'WEARABLE_HELM',
      name: 'Тяжёлый шлем',
      type: 'Equipment',
      rarity: 'Rare',
      requiredLevel: 8,
      slot: 'Head',
      armorCategory: 'HEAVY',
    })
    const tooHigh = item({
      id: 'HIGH_LEVEL_HELM',
      name: 'Шлем ветерана',
      type: 'Equipment',
      rarity: 'Epic',
      requiredLevel: 14,
      slot: 'Head',
      armorCategory: 'HEAVY',
    })
    const wrongArmor = item({
      id: 'LEATHER_HOOD',
      name: 'Кожаный капюшон',
      type: 'Equipment',
      rarity: 'Rare',
      requiredLevel: 5,
      slot: 'Head',
      armorCategory: 'LEATHER',
    })
    const potion = consumable('TEST_POTION', 'Зелье')
    store.snapshot = snapshot([wearable, tooHigh, wrongArmor, potion], currentWeapon())

    const wrapper = mount(InventoryView)
    await wrapper.get('[data-open-inventory-filters]').trigger('click')
    document.querySelector<HTMLButtonElement>('[data-inventory-equipable-filter]')?.click()
    document.querySelector<HTMLButtonElement>('[data-apply-inventory-filters]')?.click()
    await flushPromises()

    expect(wrapper.find('[data-item-id="WEARABLE_HELM"]').exists()).toBe(true)
    expect(wrapper.find('[data-item-id="HIGH_LEVEL_HELM"]').exists()).toBe(false)
    expect(wrapper.find('[data-item-id="LEATHER_HOOD"]').exists()).toBe(false)
    expect(wrapper.find('[data-item-id="TEST_POTION"]').exists()).toBe(false)
  })

  it('labels randomized equipment instances in the item details', async () => {
    const store = useGameSessionStore()
    const rolled = item({
      id: 'ROLLED_SWORD',
      name: 'Случайный клинок',
      type: 'Equipment',
      rarity: 'Rare',
      slot: 'MainHand',
      weaponCategory: 'ONE_HAND_SWORD',
      hasRandomStats: true,
      stats: { strength: 7, stamina: 4 },
    })
    store.snapshot = snapshot([rolled], currentWeapon())

    const wrapper = mount(InventoryView)
    await wrapper.get('[data-item-id="ROLLED_SWORD"]').trigger('click')
    await flushPromises()

    expect(document.body.textContent).toContain(
      'Характеристики этого экземпляра определились при получении предмета.',
    )
    expect(document.body.textContent).toContain('Сила +7')
    expect(document.body.textContent).toContain('Выносливость +4')
  })

  it('shows generated equipment quality in the grid and its detail modal', async () => {
    const store = useGameSessionStore()
    const generated = item({
      id: 'PERFECT_SWORD',
      name: 'Совершенный клинок',
      type: 'Equipment',
      rarity: 'Epic',
      slot: 'MainHand',
      weaponCategory: 'ONE_HAND_SWORD',
      generatedItem: {
        itemLevel: 12,
        itemPower: 148.5,
        maxItemPower: 150,
        rollQuality: 99,
        stars: 5,
        isPerfect: true,
        perfectOrigin: 'DROP',
        generatedPrefixId: 'PREFIX_STRENGTH',
        generatedSuffixId: null,
        displayName: 'Совершенный клинок',
        affixes: [],
      },
    })
    store.snapshot = snapshot([generated], currentWeapon())

    const wrapper = mount(InventoryView)
    expect(wrapper.find('[data-item-id="PERFECT_SWORD"] [data-item-quality-stars]').exists()).toBe(
      true,
    )

    await wrapper.get('[data-item-id="PERFECT_SWORD"]').trigger('click')
    await flushPromises()

    const detailText = document.body.textContent ?? ''
    expect(detailText).toContain('МОЩЬ ПРЕДМЕТА')
    expect(detailText).toContain('148.50 / 150')
    expect(detailText).toContain('КАЧЕСТВО')
    expect(detailText).toContain('99%')
    expect(detailText).toContain('ИДЕАЛЬНОЕ КАЧЕСТВО')
    expect(detailText).not.toContain('ИДЕАЛЬНЫЙ РОЛЛ')
    expect(wrapper.findAll('[data-item-quality-stars]')).toHaveLength(1)
    expect(document.body.querySelectorAll('[data-item-quality-stars]')).toHaveLength(1)
  })

  it('does not show quality stars for materials or consumables', async () => {
    const store = useGameSessionStore()
    const material = item({
      id: 'IRON_ORE',
      name: 'Железная руда',
      type: 'Material',
      rarity: 'Common',
    })
    const potion = consumable('HEALING_POTION', 'Малое зелье лечения')
    store.snapshot = snapshot([material, potion], currentWeapon())

    const wrapper = mount(InventoryView)

    expect(wrapper.findAll('[data-item-quality-stars]')).toHaveLength(0)
  })

  it('compares candidate equipment with the currently equipped item', async () => {
    const store = useGameSessionStore()
    const candidate = equipment('EPIC_BLADE', 'Клинок сумерек', 'Epic', 5, 12)
    store.snapshot = snapshot([candidate], currentWeapon())

    const wrapper = mount(InventoryView)
    await wrapper.get('[data-item-id="EPIC_BLADE"]').trigger('click')
    await flushPromises()

    const modalText = document.body.textContent ?? ''
    expect(modalText).toContain('СРАВНЕНИЕ')
    expect(modalText).toContain('Сейчас: Учебный меч')
    expect(modalText).toContain('Сила атаки')
    expect(modalText).toContain('+7')
  })

  it('does not expose equip actions for class-incompatible equipment', async () => {
    const store = useGameSessionStore()
    const hood = item({
      id: 'RANGER_HOOD',
      name: 'Капюшон Следопыта',
      type: 'Equipment',
      rarity: 'Uncommon',
      slot: 'Head',
      armorCategory: 'LEATHER',
    })
    store.snapshot = snapshot([hood], currentWeapon())
    const equip = vi.spyOn(store, 'equip').mockResolvedValue(undefined)

    const wrapper = mount(InventoryView)
    await wrapper.get('[data-item-id="RANGER_HOOD"]').trigger('click')
    await flushPromises()

    expect(document.body.querySelector('[data-equip-action]')).toBeNull()
    expect(document.body.querySelector('[data-equip-target]')).toBeNull()
    expect(document.body.querySelector('[data-equip-restriction]')?.textContent).toContain(
      'Воин может носить только тяжёлую броню.',
    )
    expect(document.body.textContent).toContain('Капюшон Следопыта')
    expect(equip).not.toHaveBeenCalled()
  })

  it('keeps level-gated equipment visible but disables equip with a concrete reason', async () => {
    const store = useGameSessionStore()
    const helm = item({
      id: 'VETERAN_HELM',
      name: 'Шлем ветерана',
      type: 'Equipment',
      rarity: 'Epic',
      requiredLevel: 14,
      slot: 'Head',
      armorCategory: 'HEAVY',
    })
    store.snapshot = snapshot([helm], currentWeapon())
    const equip = vi.spyOn(store, 'equip').mockResolvedValue(undefined)

    const wrapper = mount(InventoryView)
    await wrapper.get('[data-item-id="VETERAN_HELM"]').trigger('click')
    await flushPromises()

    const equipAction = document.body.querySelector<HTMLButtonElement>('[data-equip-action]')
    expect(equipAction).not.toBeNull()
    expect(equipAction?.disabled).toBe(true)
    expect(document.body.querySelector('[data-equip-level-requirement]')?.textContent).toContain(
      'Требуется уровень 14. Текущий уровень: 10.',
    )
    expect(equip).not.toHaveBeenCalled()
  })

  it('lets a one-hand weapon choose main hand or off hand explicitly', async () => {
    const store = useGameSessionStore()
    const sword = item({
      id: 'BERSERKER_SWORD',
      name: 'Клинок Багровой Ярости',
      type: 'Equipment',
      rarity: 'Epic',
      slot: 'MainHand',
      weaponCategory: 'ONE_HAND_SWORD',
      weaponHandsRequired: 1,
    })
    store.snapshot = snapshot([sword], currentWeapon())
    const equip = vi.spyOn(store, 'equip').mockResolvedValue(undefined)

    const wrapper = mount(InventoryView)
    await wrapper.get('[data-item-id="BERSERKER_SWORD"]').trigger('click')
    await flushPromises()

    expect(document.body.querySelector('[data-equip-target="MainHand"]')).not.toBeNull()
    const offHand = document.body.querySelector<HTMLButtonElement>('[data-equip-target="OffHand"]')
    expect(offHand).not.toBeNull()
    offHand?.click()
    await flushPromises()

    expect(equip).toHaveBeenCalledWith('BERSERKER_SWORD', 'OffHand')
  })

  it('includes one-hand weapons when inventory is opened for the off-hand slot', async () => {
    const store = useGameSessionStore()
    const sword = item({
      id: 'OFFHAND_CANDIDATE',
      name: 'Запасной меч',
      type: 'Equipment',
      rarity: 'Rare',
      slot: 'MainHand',
      weaponCategory: 'ONE_HAND_SWORD',
      weaponHandsRequired: 1,
    })
    store.snapshot = snapshot([sword], currentWeapon())

    const wrapper = mount(InventoryView, { props: { slotFilter: 'OffHand' } })
    await flushPromises()

    expect(wrapper.find('[data-item-id="OFFHAND_CANDIDATE"]').exists()).toBe(true)
    await wrapper.get('[data-item-id="OFFHAND_CANDIDATE"]').trigger('click')
    await flushPromises()
    expect(document.body.querySelector('[data-equip-target="MainHand"]')).toBeNull()
    expect(document.body.querySelector('[data-equip-target="OffHand"]')).not.toBeNull()
  })

  it('marks only newly appeared server items as NEW and clears the mark after inspection', async () => {
    const store = useGameSessionStore()
    const existing = equipment('COMMON_BLADE', 'Старый клинок', 'Common', 1, 3)
    store.snapshot = snapshot([existing], currentWeapon())

    const wrapper = mount(InventoryView)
    await flushPromises()

    expect(wrapper.get('[data-item-id="COMMON_BLADE"]').attributes('data-new')).toBe('false')

    const fresh = consumable('FRESH_POTION', 'Свежее зелье')
    store.snapshot.character!.inventory.items = [existing, fresh]
    await flushPromises()

    expect(wrapper.get('[data-item-id="FRESH_POTION"]').attributes('data-new')).toBe('true')
    expect(wrapper.get('[data-item-id="FRESH_POTION"]').text()).toContain('НОВОЕ')

    await wrapper.get('[data-item-id="FRESH_POTION"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-item-id="FRESH_POTION"]').attributes('data-new')).toBe('false')
  })

  it('keeps NEW, lock and stars visible together for generated equipment', async () => {
    const store = useGameSessionStore()
    const existing = equipment('COMMON_BLADE', 'Existing sword', 'Common', 1, 3)
    store.snapshot = snapshot([existing], currentWeapon())

    const wrapper = mount(InventoryView)
    await flushPromises()

    const freshGenerated = item({
      id: 'FRESH_LOCKED_SWORD',
      name: 'New sword',
      type: 'Equipment',
      rarity: 'Rare',
      slot: 'MainHand',
      isLocked: true,
      generatedItem: {
        itemLevel: 10,
        itemPower: 100,
        maxItemPower: 150,
        rollQuality: 60,
        stars: 3,
        isPerfect: false,
        perfectOrigin: null,
        generatedPrefixId: null,
        generatedSuffixId: null,
        displayName: 'New sword',
        affixes: [],
      },
    })
    store.snapshot.character!.inventory.items = [existing, freshGenerated]
    await flushPromises()

    const cell = wrapper.get('[data-item-id="FRESH_LOCKED_SWORD"]')
    expect(cell.attributes('data-new')).toBe('true')
    expect(cell.attributes('data-locked')).toBe('true')
    expect(cell.text()).toContain('\u041d\u041e\u0412\u041e\u0415')
    expect(cell.find('[data-item-quality-stars]').exists()).toBe(true)
  })

  it('renders server lock state and toggles protection through the game store', async () => {
    const store = useGameSessionStore()
    const protectedItem = item({
      id: 'WOLF_FANG',
      name: 'Волчий клык',
      type: 'Material',
      rarity: 'Uncommon',
      isLocked: true,
      sellPriceGold: 4,
    })
    store.snapshot = snapshot([protectedItem], currentWeapon())
    const setItemLock = vi
      .spyOn(store, 'setItemLock')
      .mockImplementation(async (itemId, isLocked) => {
        const stored = store.snapshot?.character?.inventory.items.find(
          (candidate) => candidate.id === itemId,
        )
        if (stored) stored.isLocked = isLocked
      })

    const wrapper = mount(InventoryView)
    await flushPromises()

    expect(wrapper.get('[data-item-id="WOLF_FANG"]').attributes('data-locked')).toBe('true')
    await wrapper.get('[data-item-id="WOLF_FANG"]').trigger('click')
    await flushPromises()

    expect(document.body.textContent).toContain('Предмет защищён')
    expect(document.body.textContent).toContain('Предмет защищён от продажи')

    const action = document.body.querySelector<HTMLButtonElement>('[data-item-lock-action]')
    expect(action?.textContent).toContain('Снять защиту')
    action?.click()
    await flushPromises()

    expect(setItemLock).toHaveBeenCalledWith('WOLF_FANG', false)
    expect(document.body.textContent).toContain('Защитить')
  })

  it('keeps dismantling out of the inventory item actions', async () => {
    const store = useGameSessionStore()
    const sword = equipment('SALVAGE_SWORD', 'Salvage sword', 'Rare', 1, 3)
    store.snapshot = snapshot([sword], currentWeapon())

    const wrapper = mount(InventoryView)
    await wrapper.get('[data-item-id="SALVAGE_SWORD"]').trigger('click')
    await flushPromises()

    expect(document.querySelector('[data-item-salvage-action]')).toBeNull()
  })
})

function itemIds(wrapper: ReturnType<typeof mount>): string[] {
  return wrapper
    .findAll('[data-item-id]')
    .map((node) => node.attributes('data-item-id'))
    .filter((value): value is string => Boolean(value))
}

function equipment(
  id: string,
  name: string,
  rarity: InventoryItem['rarity'],
  requiredLevel: number,
  attackPower: number,
): InventoryItem {
  return item({
    id,
    name,
    type: 'Equipment',
    rarity,
    requiredLevel,
    slot: 'Weapon',
    stats: { attackPower },
  })
}

function currentWeapon(): InventoryItem {
  return {
    ...equipment('CURRENT_WEAPON', 'Учебный меч', 'Rare', 1, 5),
    equippedSlot: 'Weapon',
  }
}

function consumable(id: string, name: string): InventoryItem {
  return item({
    id,
    name,
    type: 'Consumable',
    rarity: 'Common',
    requiredLevel: 1,
    slot: null,
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
  })
}

type InventoryItemOverrides = Omit<Partial<InventoryItem>, 'stats'> &
  Pick<InventoryItem, 'id' | 'name' | 'type' | 'rarity'> & {
    stats?: Partial<InventoryItem['stats']>
  }

function item(overrides: InventoryItemOverrides): InventoryItem {
  const zeroStats: InventoryItem['stats'] = {
    strength: 0,
    agility: 0,
    intellect: 0,
    stamina: 0,
    maxHp: 0,
    attackPower: 0,
    spellPower: 0,
    criticalChance: 0,
    criticalDamage: 0,
    accuracy: 0,
    armor: 0,
    magicResistance: 0,
    dodge: 0,
    armorPenetration: 0,
    magicPenetration: 0,
    attackSpeed: 0,
    maxResource: 0,
  }

  return {
    id: overrides.id,
    definitionId: overrides.definitionId ?? overrides.id,
    name: overrides.name,
    type: overrides.type,
    rarity: overrides.rarity,
    requiredLevel: overrides.requiredLevel ?? 1,
    quantity: overrides.quantity ?? 1,
    slot: overrides.slot ?? null,
    equippedSlot: overrides.equippedSlot ?? null,
    stats: { ...zeroStats, ...overrides.stats },
    description: overrides.description ?? 'Test item',
    setId: overrides.setId ?? null,
    weaponCategory: overrides.weaponCategory ?? null,
    armorCategory: overrides.armorCategory ?? null,
    weaponBaseAttackIntervalSeconds: overrides.weaponBaseAttackIntervalSeconds ?? null,
    attackSpeedPercent: overrides.attackSpeedPercent ?? 0,
    dodgePercent: overrides.dodgePercent ?? 0,
    consumableActions: overrides.consumableActions ?? [],
    consumableCooldownCategoryId: overrides.consumableCooldownCategoryId ?? null,
    consumableCooldownSeconds: overrides.consumableCooldownSeconds ?? 0,
    buyPriceGold: overrides.buyPriceGold ?? 0,
    sellPriceGold: overrides.sellPriceGold ?? 0,
    isLocked: overrides.isLocked ?? false,
    iconId: overrides.iconId ?? null,
    appearanceProfileId: overrides.appearanceProfileId ?? null,
    weaponHandsRequired: overrides.weaponHandsRequired ?? null,
    hasRandomStats: overrides.hasRandomStats ?? false,
    generatedItem: overrides.generatedItem ?? null,
  }
}

function snapshot(items: InventoryItem[], weapon: InventoryItem): BootstrapSnapshot {
  return {
    accountId: crypto.randomUUID(),
    character: {
      id: '0199-character-inventory-test',
      name: 'Arthas',
      raceId: 'HUMAN',
      genderId: 'MALE',
      classId: 'WARRIOR',
      level: 10,
      experience: 0,
      xpToNextLevel: 1000,
      gold: 120,
      primaryAttribute: 'STRENGTH',
      classProfileVersion: '0.2.0',
      knownAbilityIds: [],
      knownAbilities: [],
      stats: {
        strength: 20,
        agility: 8,
        intellect: 5,
        stamina: 16,
        maxHp: 240,
        attackPower: 45,
        spellPower: 10,
        criticalChance: 5,
        criticalDamage: 100,
        accuracy: 95,
        armorPenetration: 0,
        magicPenetration: 0,
        attackSpeed: 1,
        armor: 30,
        magicResistance: 15,
        dodge: 2,
      },
      statBreakdown: {} as never,
      vitals: {
        currentHp: 240,
        maxHp: 240,
        resourceType: 'RAGE',
        currentResource: 0,
        maxResource: 100,
        checkpointedAtUtc: '2026-09-06T05:00:00Z',
      },
      inventory: {
        items: [...items, weapon],
        equipped: {
          weapon,
          head: null,
          chest: null,
          legs: null,
          boots: null,
          accessory: null,
        },
      },
    },
    world: {
      currentLocation: {
        id: 'STARTER_TOWN',
        displayName: 'Starter Town',
        dangerLevel: 'SAFE',
        recommendedLevel: 1,
        minimumLevel: 1,
        maximumLevel: 60,
        requiredContractId: null,
        artId: null,
        description: 'Test location',
      },
      version: 1,
      outgoingTransitions: [],
      contracts: [],
    },
    contentVersion: '0.9.4',
    balanceVersion: '0.9.1',
    serverTimeUtc: '2026-09-06T05:00:00Z',
  }
}
