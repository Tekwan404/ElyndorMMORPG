import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'

import type { BootstrapSnapshot, InventoryItem } from '@/api/contracts'
import HeroView from '@/game/character/views/HeroView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

describe('HeroView contextual equipment flow', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('enables and opens the talents tab for Archer', async () => {
    const session = useGameSessionStore()
    session.snapshot = snapshot([], 'ARCHER')

    const wrapper = mount(HeroView, {
      global: {
        stubs: {
          CharacterOverviewView: true,
          CharacterStatsView: true,
          InventoryView: true,
          TalentTreeView: { template: '<div data-archer-talent-tree />' },
        },
      },
    })

    const talentsTab = wrapper.get('[data-hero-tab="talents"]')
    expect(talentsTab.attributes('disabled')).toBeUndefined()

    await talentsTab.trigger('click')

    expect(talentsTab.attributes('aria-current')).toBe('page')
    expect(wrapper.find('[data-archer-talent-tree]').exists()).toBe(true)
  })

  it('opens empty equipment slots into inventory filtered for that slot', async () => {
    const session = useGameSessionStore()
    const helmet = equipment('TEST_HELMET', 'Шлем стража', 'Head')
    const chest = equipment('TEST_CHEST', 'Кираса стража', 'Chest')
    session.snapshot = snapshot([helmet, chest])

    const wrapper = mount(HeroView)

    await wrapper.get('[data-equipment-slot="head"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-hero-tab="inventory"]').exists()).toBe(false)
    expect(wrapper.find('[data-close-slot-inventory]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Выберите: шлем')
    expect(wrapper.find('[data-item-id="TEST_HELMET"]').exists()).toBe(true)
    expect(wrapper.find('[data-item-id="TEST_CHEST"]').exists()).toBe(false)
  })
})

function equipment(id: string, name: string, slot: InventoryItem['slot']): InventoryItem {
  return {
    id,
    definitionId: id,
    name,
    type: 'Equipment',
    rarity: 'Rare',
    requiredLevel: 1,
    quantity: 1,
    slot,
    equippedSlot: null,
    stats: {
      strength: 1,
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
    },
    description: 'Test equipment',
    setId: null,
    weaponCategory: null,
    armorCategory: null,
    allowedClassIds: [],
    weaponBaseAttackIntervalSeconds: null,
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
  }
}

function snapshot(
  items: InventoryItem[],
  classId: NonNullable<BootstrapSnapshot['character']>['classId'] = 'WARRIOR',
): BootstrapSnapshot {
  return {
    accountId: crypto.randomUUID(),
    character: {
      id: crypto.randomUUID(),
      name: 'Arthas',
      raceId: 'HUMAN',
      genderId: 'MALE',
      classId,
      level: 10,
      experience: 340,
      xpToNextLevel: 1000,
      gold: 55,
      primaryAttribute: 'STRENGTH',
      classProfileVersion: '0.10.0',
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
        currentHp: 220,
        maxHp: 240,
        resourceType: 'RAGE',
        currentResource: 35,
        maxResource: 100,
        checkpointedAtUtc: '2026-09-06T09:00:00Z',
      },
      inventory: {
        items,
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
    contentVersion: '0.10.0',
    balanceVersion: '0.8.0',
    serverTimeUtc: '2026-09-06T09:00:00Z',
  }
}
