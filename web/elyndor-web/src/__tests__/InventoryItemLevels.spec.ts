import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'

import type { BootstrapSnapshot, InventoryItem } from '@/api/contracts'
import InventoryView from '@/game/character/views/InventoryView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

describe('Inventory item level presentation', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    globalThis.localStorage.clear()
    document.body.innerHTML = ''
  })

  it('shows generated Item Level separately from Required Level', async () => {
    const store = useGameSessionStore()
    const item = equipment({
      id: 'LEVELLED_SWORD',
      name: 'Клинок испытаний',
      requiredLevel: 8,
      generatedItem: {
        itemLevel: 12,
        itemPower: 100,
        maxItemPower: 120,
        rollQuality: 80,
        stars: 4,
        isPerfect: false,
        perfectOrigin: null,
        generatedPrefixId: null,
        generatedSuffixId: null,
        displayName: 'Клинок испытаний',
        affixes: [],
      },
    })
    store.snapshot = snapshot(item)

    const wrapper = mount(InventoryView)
    await wrapper.get('[data-item-id="LEVELLED_SWORD"]').trigger('click')
    await flushPromises()

    const itemLevel = document.body.querySelector('[data-item-level]')
    const requiredLevel = document.body.querySelector('[data-required-level]')
    expect(itemLevel?.textContent).toContain('12')
    expect(itemLevel?.textContent).toContain('ilvl')
    expect(requiredLevel?.textContent).toContain('8')
    expect(requiredLevel?.textContent).toContain('для экипировки')
  })

  it('does not substitute Required Level when legacy equipment has no resolved Item Level', async () => {
    const store = useGameSessionStore()
    const item = equipment({
      id: 'LEGACY_SWORD',
      name: 'Старый клинок',
      requiredLevel: 9,
      generatedItem: null,
    })
    store.snapshot = snapshot(item)

    const wrapper = mount(InventoryView)
    await wrapper.get('[data-item-id="LEGACY_SWORD"]').trigger('click')
    await flushPromises()

    const itemLevel = document.body.querySelector('[data-item-level]')
    const requiredLevel = document.body.querySelector('[data-required-level]')
    expect(itemLevel?.textContent).toContain('—')
    expect(itemLevel?.textContent).not.toContain('9')
    expect(requiredLevel?.textContent).toContain('9')
  })
})

function equipment(overrides: {
  id: string
  name: string
  requiredLevel: number
  generatedItem: InventoryItem['generatedItem']
}): InventoryItem {
  return {
    id: overrides.id,
    definitionId: overrides.id,
    name: overrides.name,
    type: 'Equipment',
    rarity: 'Rare',
    requiredLevel: overrides.requiredLevel,
    quantity: 1,
    slot: 'MainHand',
    equippedSlot: null,
    stats: {
      strength: 0,
      agility: 0,
      intellect: 0,
      stamina: 0,
      maxHp: 0,
      attackPower: 5,
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
    },
    description: 'Предмет для проверки уровней.',
    setId: null,
    weaponCategory: 'ONE_HAND_SWORD',
    armorCategory: null,
    allowedClassIds: ['WARRIOR'],
    weaponBaseAttackIntervalSeconds: 2,
    attackSpeedPercent: 0,
    dodgePercent: 0,
    consumableActions: [],
    consumableCooldownCategoryId: null,
    consumableCooldownSeconds: 0,
    buyPriceGold: 0,
    sellPriceGold: 0,
    isLocked: false,
    iconId: null,
    appearanceProfileId: null,
    weaponHandsRequired: 1,
    hasRandomStats: false,
    generatedItem: overrides.generatedItem,
  }
}

function snapshot(item: InventoryItem): BootstrapSnapshot {
  return {
    accountId: crypto.randomUUID(),
    character: {
      id: '0199-inventory-levels-test',
      name: 'Arthas',
      raceId: 'HUMAN',
      genderId: 'MALE',
      classId: 'WARRIOR',
      level: 10,
      experience: 0,
      xpToNextLevel: 100,
      gold: 0,
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
        checkpointedAtUtc: '2026-09-14T09:55:00Z',
      },
      inventory: {
        items: [item],
        equipped: {
          weapon: null,
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
    contentVersion: '0.21.0',
    balanceVersion: '0.16.1',
    serverTimeUtc: '2026-09-14T09:55:00Z',
  }
}
