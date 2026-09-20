import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { apiClient, ApiRequestError } from '@/api/apiClient'
import type { BootstrapSnapshot, InventoryItem, SpatialInventorySnapshot } from '@/api/contracts'
import InventoryView from '@/game/character/views/InventoryView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

describe('InventoryView spatial artifacts', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    globalThis.localStorage.clear()
    document.body.innerHTML = ''
    vi.restoreAllMocks()
  })

  it('shows a purchased spatial artifact in inventory and equips it from the item menu', async () => {
    const artifact = spatialArtifact('ARTIFACT_10', 'Малое пространственное кольцо')
    const request = vi.spyOn(apiClient, 'request')
      .mockResolvedValueOnce(spatialState())
      .mockResolvedValueOnce(spatialState({
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
          usedSlots: 0,
          freeSlots: 40,
          isOverflow: false,
        },
      }))
    const session = useGameSessionStore()
    session.snapshot = snapshot([artifact])

    const wrapper = mount(InventoryView)
    await flushPromises()

    expect(wrapper.find('[data-item-id="ARTIFACT_10"]').exists()).toBe(true)
    await wrapper.get('[data-item-id="ARTIFACT_10"]').trigger('click')
    await flushPromises()

    const equipAction = document.body.querySelector<HTMLButtonElement>('[data-equip-spatial-artifact]')
    expect(equipAction).not.toBeNull()
    expect(equipAction?.textContent?.trim()).toBe('Надеть')
    expect(document.body.textContent).toContain('Пространственный артефакт')
    expect(document.body.textContent).toContain('отдельном слоте пространственного артефакта')

    equipAction!.click()
    await flushPromises()

    expect(request).toHaveBeenNthCalledWith(
      2,
      '/api/v1/inventory/spatial-artifact/equip',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ characterItemId: 'ARTIFACT_10' }),
      }),
    )
    expect(wrapper.find('[data-item-id="ARTIFACT_10"]').exists()).toBe(false)
    expect(wrapper.get('[data-equipped-spatial-artifact]').text()).toContain('Малое пространственное кольцо')
    expect(wrapper.get('[data-spatial-capacity]').text()).toContain('30 + 10 = 40')
  })

  it('offers replacement when another spatial artifact is already equipped', async () => {
    const equipped = spatialArtifact('ARTIFACT_5', 'Треснувшее пространственное кольцо')
    const candidate = spatialArtifact('ARTIFACT_20', 'Пространственная печать')
    vi.spyOn(apiClient, 'request').mockResolvedValue(spatialState({
      equippedArtifact: {
        characterItemId: equipped.id,
        definitionId: equipped.definitionId,
        name: equipped.name,
        rarity: equipped.rarity,
        capacityBonus: 5,
        iconId: equipped.iconId,
      },
      capacity: {
        baseCapacity: 30,
        artifactCapacityBonus: 5,
        capacity: 35,
        usedSlots: 1,
        freeSlots: 34,
        isOverflow: false,
      },
    }))
    const session = useGameSessionStore()
    session.snapshot = snapshot([equipped, candidate])

    const wrapper = mount(InventoryView)
    await flushPromises()

    expect(wrapper.find('[data-item-id="ARTIFACT_5"]').exists()).toBe(false)
    expect(wrapper.find('[data-item-id="ARTIFACT_20"]').exists()).toBe(true)
    await wrapper.get('[data-item-id="ARTIFACT_20"]').trigger('click')
    await flushPromises()

    const replaceAction = document.body.querySelector<HTMLButtonElement>('[data-equip-spatial-artifact]')
    expect(replaceAction).not.toBeNull()
    expect(replaceAction?.textContent?.trim()).toBe('Заменить')
    expect(document.body.textContent).toContain('Сейчас надето: Треснувшее пространственное кольцо (+5)')
  })

  it('unequips from the dedicated spatial capacity block and returns the artifact to the grid', async () => {
    const artifact = spatialArtifact('ARTIFACT_15', 'Кольцо расширенного пространства')
    const request = vi.spyOn(apiClient, 'request')
      .mockResolvedValueOnce(spatialState({
        equippedArtifact: {
          characterItemId: artifact.id,
          definitionId: artifact.definitionId,
          name: artifact.name,
          rarity: artifact.rarity,
          capacityBonus: 15,
          iconId: artifact.iconId,
        },
        capacity: {
          baseCapacity: 30,
          artifactCapacityBonus: 15,
          capacity: 45,
          usedSlots: 0,
          freeSlots: 45,
          isOverflow: false,
        },
      }))
      .mockResolvedValueOnce(spatialState())
    const session = useGameSessionStore()
    session.snapshot = snapshot([artifact])

    const wrapper = mount(InventoryView)
    await flushPromises()

    expect(wrapper.find('[data-item-id="ARTIFACT_15"]').exists()).toBe(false)
    expect(wrapper.get('[data-unequip-spatial-artifact]').text()).toBe('Снять')

    await wrapper.get('[data-unequip-spatial-artifact]').trigger('click')
    await flushPromises()

    expect(request).toHaveBeenNthCalledWith(
      2,
      '/api/v1/inventory/spatial-artifact/unequip',
      { method: 'POST' },
    )
    expect(wrapper.find('[data-item-id="ARTIFACT_15"]').exists()).toBe(true)
    expect(wrapper.find('[data-unequip-spatial-artifact]').exists()).toBe(false)
    expect(wrapper.get('[data-spatial-capacity]').text()).toContain('30 + 0 = 30')
  })

  it('keeps an equipped artifact and explains when it cannot be removed because inventory would overflow', async () => {
    const artifact = spatialArtifact('ARTIFACT_40', 'Печать Бездны')
    vi.spyOn(apiClient, 'request')
      .mockResolvedValueOnce(spatialState({
        equippedArtifact: {
          characterItemId: artifact.id,
          definitionId: artifact.definitionId,
          name: artifact.name,
          rarity: artifact.rarity,
          capacityBonus: 40,
          iconId: artifact.iconId,
        },
        capacity: {
          baseCapacity: 30,
          artifactCapacityBonus: 40,
          capacity: 70,
          usedSlots: 50,
          freeSlots: 20,
          isOverflow: false,
        },
      }))
      .mockRejectedValueOnce(new ApiRequestError(409, 'inventory_full'))
    const session = useGameSessionStore()
    session.snapshot = snapshot([artifact])

    const wrapper = mount(InventoryView)
    await flushPromises()
    await wrapper.get('[data-unequip-spatial-artifact]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-spatial-action-error]').text()).toContain('освободите достаточно ячеек')
    expect(wrapper.get('[data-equipped-spatial-artifact]').text()).toContain('Печать Бездны')
    expect(wrapper.find('[data-item-id="ARTIFACT_40"]').exists()).toBe(false)
  })
})

function spatialState(overrides: Partial<SpatialInventorySnapshot> = {}): SpatialInventorySnapshot {
  return {
    equippedArtifact: null,
    capacity: {
      baseCapacity: 30,
      artifactCapacityBonus: 0,
      capacity: 30,
      usedSlots: 1,
      freeSlots: 29,
      isOverflow: false,
    },
    ...overrides,
  }
}

function spatialArtifact(id: string, name: string): InventoryItem {
  return {
    id,
    definitionId: id,
    name,
    type: 'SpatialArtifact',
    rarity: 'Rare',
    requiredLevel: 1,
    quantity: 1,
    slot: null,
    equippedSlot: null,
    stats: {
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
    },
    description: 'Пространственный артефакт для расширения инвентаря.',
    setId: null,
    weaponCategory: null,
    armorCategory: null,
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
  } as unknown as InventoryItem
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
        checkpointedAtUtc: '2026-09-20T00:00:00Z',
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
    serverTimeUtc: '2026-09-20T00:00:00Z',
  }
}