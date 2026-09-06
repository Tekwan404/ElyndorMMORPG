import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { BootstrapSnapshot, InventoryItem } from '@/api/contracts'
import InventoryView from '@/game/character/views/InventoryView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

describe('InventoryView', () => {
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

    expect(itemIds(wrapper)).toEqual(['COMMON_BLADE', 'EPIC_BLADE'])

    await wrapper.get('[data-inventory-sort]').setValue('rarity')
    expect(itemIds(wrapper)).toEqual(['EPIC_BLADE', 'COMMON_BLADE'])
    expect(store.snapshot.character?.inventory.items.map(item => item.id)).toEqual([
      'COMMON_BLADE',
      'EPIC_BLADE',
      'CURRENT_WEAPON',
    ])
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

  it('keeps an equipment modal open and explains server equip restrictions', async () => {
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
    vi.spyOn(store, 'equip').mockImplementation(async () => {
      store.errorCode = 'inventory_armor_category_restricted'
    })

    const wrapper = mount(InventoryView)
    await wrapper.get('[data-item-id="RANGER_HOOD"]').trigger('click')
    await flushPromises()

    const equipAction = Array.from(document.body.querySelectorAll<HTMLButtonElement>('button'))
      .find(button => button.textContent?.trim() === 'Надеть')
    expect(equipAction).toBeDefined()
    equipAction?.click()
    await flushPromises()

    expect(document.body.textContent).toContain('Этот тип брони недоступен вашему классу.')
    expect(document.body.textContent).toContain('Капюшон Следопыта')
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
    expect(wrapper.get('[data-item-id="FRESH_POTION"]').text()).toContain('NEW')

    await wrapper.get('[data-item-id="FRESH_POTION"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-item-id="FRESH_POTION"]').attributes('data-new')).toBe('false')
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
    const setItemLock = vi.spyOn(store, 'setItemLock').mockImplementation(async (itemId, isLocked) => {
      const stored = store.snapshot?.character?.inventory.items.find(candidate => candidate.id === itemId)
      if (stored) stored.isLocked = isLocked
    })

    const wrapper = mount(InventoryView)
    await flushPromises()

    expect(wrapper.get('[data-item-id="WOLF_FANG"]').attributes('data-locked')).toBe('true')
    await wrapper.get('[data-item-id="WOLF_FANG"]').trigger('click')
    await flushPromises()

    expect(document.body.textContent).toContain('ЗАЩИЩЕНО')
    expect(document.body.textContent).toContain('защищён от продажи')

    const action = document.body.querySelector<HTMLButtonElement>('[data-item-lock-action]')
    expect(action?.textContent).toContain('Снять защиту')
    action?.click()
    await flushPromises()

    expect(setItemLock).toHaveBeenCalledWith('WOLF_FANG', false)
    expect(document.body.textContent).toContain('Защитить')
  })
})

function itemIds(wrapper: ReturnType<typeof mount>): string[] {
  return wrapper.findAll('[data-item-id]')
    .map(node => node.attributes('data-item-id'))
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
    healAmount: 50,
    consumableCooldownSeconds: 30,
  })
}

type InventoryItemOverrides =
  Omit<Partial<InventoryItem>, 'stats'>
  & Pick<InventoryItem, 'id' | 'name' | 'type' | 'rarity'>
  & { stats?: Partial<InventoryItem['stats']> }

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
    allowedClassIds: overrides.allowedClassIds ?? [],
    weaponBaseAttackIntervalSeconds: overrides.weaponBaseAttackIntervalSeconds ?? null,
    attackSpeedPercent: overrides.attackSpeedPercent ?? 0,
    dodgePercent: overrides.dodgePercent ?? 0,
    healAmount: overrides.healAmount ?? 0,
    consumableCooldownSeconds: overrides.consumableCooldownSeconds ?? 0,
    buyPriceGold: overrides.buyPriceGold ?? 0,
    sellPriceGold: overrides.sellPriceGold ?? 0,
    isLocked: overrides.isLocked ?? false,
    iconId: overrides.iconId ?? null,
    appearanceProfileId: overrides.appearanceProfileId ?? null,
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
      },
      version: 1,
      outgoingTransitions: [],
    },
    contentVersion: '0.9.4',
    balanceVersion: '0.9.1',
    serverTimeUtc: '2026-09-06T05:00:00Z',
  }
}
