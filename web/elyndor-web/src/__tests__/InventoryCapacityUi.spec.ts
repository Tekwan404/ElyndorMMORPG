import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { apiClient } from '@/api/apiClient'
import type { BootstrapSnapshot, InventoryItem, SpatialInventorySnapshot } from '@/api/contracts'
import InventoryView from '@/game/character/views/InventoryView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

describe('InventoryView capacity', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    globalThis.localStorage.clear()
    vi.restoreAllMocks()
  })

  it('renders capacity from the canonical spatial inventory response', async () => {
    const request = vi.spyOn(apiClient, 'request').mockResolvedValue(spatialState({
      baseCapacity: 30,
      artifactCapacityBonus: 15,
      capacity: 45,
      usedSlots: 1,
      freeSlots: 44,
      isOverflow: false,
    }))
    const session = useGameSessionStore()
    session.snapshot = snapshot([equipment('TEST_HELMET')])

    const wrapper = mount(InventoryView)
    await flushPromises()

    expect(request).toHaveBeenCalledWith('/api/v1/inventory/spatial-artifact/')
    expect(wrapper.findAll('.bag-cell')).toHaveLength(45)
    expect(wrapper.get('[data-inventory-capacity]').text()).toContain('1/ 45')
    expect(wrapper.get('[data-spatial-capacity]').text()).toContain('30 + 15 = 45')
    expect(wrapper.get('[data-spatial-capacity]').text()).toContain('44 свободно')
  })

  it('excludes the equipped spatial artifact from the normal inventory grid', async () => {
    const artifact = equipment('ARTIFACT_INSTANCE', 'Пространственное кольцо')
    vi.spyOn(apiClient, 'request').mockResolvedValue({
      equippedArtifact: {
        characterItemId: artifact.id,
        definitionId: artifact.definitionId,
        name: artifact.name,
        rarity: artifact.rarity,
        capacityBonus: 10,
        iconId: artifact.iconId,
      },
      capacity: {
        baseCapacity: 30,
        artifactCapacityBonus: 10,
        capacity: 40,
        usedSlots: 1,
        freeSlots: 39,
        isOverflow: false,
      },
    } satisfies SpatialInventorySnapshot)
    const session = useGameSessionStore()
    session.snapshot = snapshot([equipment('TEST_HELMET'), artifact])

    const wrapper = mount(InventoryView)
    await flushPromises()

    expect(wrapper.find('[data-item-id="ARTIFACT_INSTANCE"]').exists()).toBe(false)
    expect(wrapper.find('[data-item-id="TEST_HELMET"]').exists()).toBe(true)
    expect(wrapper.get('[data-equipped-spatial-artifact]').text()).toContain('+10')
  })

  it('never hides overflow items and expands the grid to the actual item count', async () => {
    vi.spyOn(apiClient, 'request').mockResolvedValue(spatialState({
      baseCapacity: 2,
      artifactCapacityBonus: 0,
      capacity: 2,
      usedSlots: 3,
      freeSlots: 0,
      isOverflow: true,
    }))
    const session = useGameSessionStore()
    session.snapshot = snapshot([
      equipment('ITEM_1'),
      equipment('ITEM_2'),
      equipment('ITEM_3'),
    ])

    const wrapper = mount(InventoryView)
    await flushPromises()

    expect(wrapper.findAll('.bag-cell')).toHaveLength(3)
    expect(wrapper.find('[data-item-id="ITEM_1"]').exists()).toBe(true)
    expect(wrapper.find('[data-item-id="ITEM_2"]').exists()).toBe(true)
    expect(wrapper.find('[data-item-id="ITEM_3"]').exists()).toBe(true)
    expect(wrapper.get('[data-inventory-overflow]').text()).toContain('Переполнение')
  })

  it('refreshes spatial state when the inventory snapshot changes', async () => {
    const request = vi.spyOn(apiClient, 'request')
      .mockResolvedValueOnce(spatialState({ capacity: 30, usedSlots: 1, freeSlots: 29 }))
      .mockResolvedValueOnce(spatialState({ capacity: 40, usedSlots: 2, freeSlots: 38, artifactCapacityBonus: 10 }))
    const session = useGameSessionStore()
    session.snapshot = snapshot([equipment('ITEM_1')])

    const wrapper = mount(InventoryView)
    await flushPromises()
    expect(wrapper.get('[data-inventory-capacity]').text()).toContain('/ 30')

    session.snapshot.character!.inventory.items.push(equipment('ITEM_2'))
    await flushPromises()

    expect(request).toHaveBeenCalledTimes(2)
    expect(wrapper.get('[data-inventory-capacity]').text()).toContain('/ 40')
  })
})

function spatialState(capacity: Partial<SpatialInventorySnapshot['capacity']>): SpatialInventorySnapshot {
  return {
    equippedArtifact: null,
    capacity: {
      baseCapacity: 30,
      artifactCapacityBonus: 0,
      capacity: 30,
      usedSlots: 0,
      freeSlots: 30,
      isOverflow: false,
      ...capacity,
    },
  }
}

function equipment(id: string, name = 'Тестовый шлем'): InventoryItem {
  return {
    id,
    definitionId: id,
    name,
    type: 'Equipment',
    rarity: 'Rare',
    requiredLevel: 1,
    quantity: 1,
    slot: 'Head',
    equippedSlot: null,
    stats: {
      strength: 1,
      agility: 0,
      intellect: 0,
      stamina: 1,
      maxHp: 0,
      attackPower: 0,
      spellPower: 0,
      criticalChance: 0,
      criticalDamage: 0,
      accuracy: 0,
      armor: 2,
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
    armorCategory: 'HEAVY',
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

function snapshot(items: InventoryItem[]): BootstrapSnapshot {
  return {
    accountId: crypto.randomUUID(),
    character: {
      id: crypto.randomUUID(),
      name: 'Arthas',
      raceId: 'HUMAN',
      genderId: 'MALE',
      classId: 'WARRIOR',
      level: 10,
      experience: 0,
      xpToNextLevel: 1000,
      gold: 0,
      primaryAttribute: 'STRENGTH',
      classProfileVersion: 'test',
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
        checkpointedAtUtc: '2026-09-19T00:00:00Z',
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
    contentVersion: 'test',
    balanceVersion: 'test',
    serverTimeUtc: '2026-09-19T00:00:00Z',
  }
}
