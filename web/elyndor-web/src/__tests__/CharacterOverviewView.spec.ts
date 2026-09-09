import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { BootstrapSnapshot, InventoryItem } from '@/api/contracts'
import CharacterOverviewView from '@/game/character/views/CharacterOverviewView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

describe('CharacterOverviewView equipment paperdoll', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    vi.restoreAllMocks()
  })

  it('renders canonical MMORPG slots while preserving legacy equipment fallbacks', () => {
    const session = useGameSessionStore()
    const legacyWeapon = equipment('LEGACY_WEAPON', 'Старый меч', 'Weapon')
    const legacyBoots = equipment('LEGACY_BOOTS', 'Старые сапоги', 'Boots')
    const legacyAccessory = equipment('LEGACY_AMULET', 'Старый талисман', 'Accessory')
    const shield = equipment('SHIELD', 'Щит стража', 'OffHand')

    session.snapshot = snapshot({
      weapon: legacyWeapon,
      boots: legacyBoots,
      accessory: legacyAccessory,
      offHand: shield,
    })

    const wrapper = mount(CharacterOverviewView)

    expect(wrapper.findAll('[data-equipment-slot]')).toHaveLength(12)
    expect(wrapper.get('[data-equipment-slot="mainHand"]').attributes('aria-label')).toContain('Старый меч')
    expect(wrapper.get('[data-equipment-slot="feet"]').attributes('aria-label')).toContain('Старые сапоги')
    expect(wrapper.get('[data-equipment-slot="amulet"]').attributes('aria-label')).toContain('Старый талисман')
    expect(wrapper.get('[data-equipment-slot="offHand"]').attributes('aria-label')).toContain('Щит стража')
    expect(wrapper.get('[data-equipment-slot="hands"]').attributes('data-filled')).toBe('false')
    expect(wrapper.get('[data-equipment-slot="shoulders"]').attributes('data-filled')).toBe('false')
    expect(wrapper.text()).toContain('4 / 12 слотов')
    expect(wrapper.get('[data-equipment-slot="mainHand"]').text()).not.toContain('Старый меч')
    expect(wrapper.find('.paperdoll__vitals').exists()).toBe(false)
  })

  it('renders item artwork in both paperdoll columns and shows exact dual-wield cadence', () => {
    const session = useGameSessionStore()
    const mainHand = {
      ...equipment('MAIN_HAND_ART', 'Клинок', 'MainHand'),
      iconId: 'item-warrior-sword',
      weaponCategory: 'ONE_HAND_SWORD',
      weaponBaseAttackIntervalSeconds: 2,
    }
    const offHand = {
      ...equipment('OFF_HAND_ART', 'Второй клинок', 'OffHand'),
      iconId: 'item-warrior-sword',
      weaponCategory: 'ONE_HAND_SWORD',
      weaponBaseAttackIntervalSeconds: 2.5,
    }
    const chest = {
      ...equipment('CHEST_ART', 'Кираса', 'Chest'),
      iconId: 'item-warrior-chestplate',
    }
    session.snapshot = snapshot({ mainHand, offHand, chest })
    session.snapshot.character!.stats.attackSpeed = 1.25

    const wrapper = mount(CharacterOverviewView)

    expect(wrapper.get('[data-equipment-slot="mainHand"]').find('img').exists()).toBe(true)
    expect(wrapper.get('[data-equipment-slot="offHand"]').find('img').exists()).toBe(true)
    expect(wrapper.get('[data-equipment-slot="chest"]').find('img').exists()).toBe(true)
    expect(wrapper.text()).toContain('Основная: 1.6 сек. · 0.63 уд/с')
    expect(wrapper.text()).toContain('Вторая: 2 сек. · 0.5 уд/с')
    expect(wrapper.text()).toContain('Итого: 1.13 уд/с')
  })

  it('unequips an equipped legacy accessory through its canonical amulet slot', async () => {
    const session = useGameSessionStore()
    const legacyAccessory = equipment('LEGACY_AMULET', 'Амулет Следопыта', 'Accessory')
    session.snapshot = snapshot({ accessory: legacyAccessory })
    const unequip = vi.spyOn(session, 'unequip').mockResolvedValue(undefined)

    const wrapper = mount(CharacterOverviewView)
    await wrapper.get('[data-equipment-slot="amulet"]').trigger('click')

    const action = document.body.querySelector<HTMLButtonElement>('[data-unequip-selected]')
    expect(action).not.toBeNull()
    action?.click()
    await Promise.resolve()

    expect(unequip).toHaveBeenCalledWith('Amulet')
  })

  it('prefers canonical slots over legacy aliases when both are present', () => {
    const session = useGameSessionStore()
    const legacyWeapon = equipment('LEGACY_WEAPON', 'Старый меч', 'Weapon')
    const mainHand = equipment('MAIN_HAND', 'Клинок героя', 'MainHand')
    const legacyBoots = equipment('LEGACY_BOOTS', 'Старые сапоги', 'Boots')
    const feet = equipment('FEET', 'Сапоги героя', 'Feet')

    session.snapshot = snapshot({
      weapon: legacyWeapon,
      mainHand,
      boots: legacyBoots,
      feet,
    })

    const wrapper = mount(CharacterOverviewView)

    expect(wrapper.get('[data-equipment-slot="mainHand"]').attributes('aria-label')).toContain('Клинок героя')
    expect(wrapper.get('[data-equipment-slot="mainHand"]').attributes('aria-label')).not.toContain('Старый меч')
    expect(wrapper.get('[data-equipment-slot="feet"]').attributes('aria-label')).toContain('Сапоги героя')
    expect(wrapper.get('[data-equipment-slot="feet"]').attributes('aria-label')).not.toContain('Старые сапоги')
  })
})

function equipment(
  id: string,
  name: string,
  slot: InventoryItem['slot'],
): InventoryItem {
  return {
    id,
    definitionId: id,
    name,
    type: 'Equipment',
    rarity: 'Rare',
    requiredLevel: 1,
    quantity: 1,
    slot,
    equippedSlot: slot,
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
  equipped: Partial<NonNullable<BootstrapSnapshot['character']>['inventory']['equipped']>,
): BootstrapSnapshot {
  const equippedItems = Object.values(equipped).filter(
    (item): item is InventoryItem => item !== null && item !== undefined,
  )

  return {
    accountId: crypto.randomUUID(),
    character: {
      id: crypto.randomUUID(),
      name: 'Arthas',
      raceId: 'HUMAN',
      genderId: 'MALE',
      classId: 'WARRIOR',
      level: 10,
      experience: 340,
      xpToNextLevel: 1000,
      gold: 55,
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
        currentHp: 220,
        maxHp: 240,
        resourceType: 'RAGE',
        currentResource: 35,
        maxResource: 100,
        checkpointedAtUtc: '2026-09-06T05:00:00Z',
      },
      inventory: {
        items: equippedItems,
        equipped: {
          weapon: null,
          head: null,
          chest: null,
          legs: null,
          boots: null,
          accessory: null,
          ...equipped,
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
