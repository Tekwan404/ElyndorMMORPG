import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { BootstrapSnapshot, InventoryItem } from '@/api/contracts'
import CharacterOverviewV2 from '@/game/character/views/CharacterOverviewV2.vue'
import { useGameSessionStore } from '@/stores/gameSession'

describe('CharacterOverviewV2', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    vi.restoreAllMocks()
  })

  it('shows the complete current combat sheet including effective mitigation', () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot({})
    const wrapper = mount(CharacterOverviewV2)

    expect(wrapper.text()).toContain('Сила')
    expect(wrapper.text()).toContain('Ловкость')
    expect(wrapper.text()).toContain('Интеллект')
    expect(wrapper.text()).toContain('Выносливость')
    expect(wrapper.text()).toContain('Сила атаки')
    expect(wrapper.text()).toContain('Сила заклинаний')
    expect(wrapper.text()).toContain('Крит. шанс')
    expect(wrapper.text()).toContain('Крит. урон')
    expect(wrapper.text()).toContain('Меткость')
    expect(wrapper.text()).toContain('Пробивание брони')
    expect(wrapper.text()).toContain('Пробивание магии')
    expect(wrapper.text()).toContain('Скорость атаки')
    expect(wrapper.text()).toContain('Броня')
    expect(wrapper.text()).toContain('38.25% физ. снижения')
    expect(wrapper.text()).toContain('Сопротивление магии')
    expect(wrapper.text()).toContain('17.5% маг. снижения')
    expect(wrapper.text()).toContain('Шанс блока')
    expect(wrapper.text()).toContain('Сила блока')
  })

  it('shows rich authoritative shield details including item level and block profile', async () => {
    const store = useGameSessionStore()
    const shield = equipment('GUARDIAN_SHIELD', 'Оплот Хранителя Глубин', 'OffHand')
    Object.assign(shield, {
      rarity: 'Legendary',
      requiredLevel: 16,
      armorCategory: 'HEAVY',
      blockChancePercent: 12.5,
      blockValueMin: 38,
      blockValueMax: 65,
      generatedItem: {
        itemLevel: 24,
        itemPower: 188,
        maxItemPower: 200,
        rollQuality: 94,
        stars: 4,
        isPerfect: false,
        perfectOrigin: null,
        generatedPrefixId: 'PREFIX_STAMINA',
        generatedSuffixId: null,
        displayName: 'Оплот Хранителя Глубин',
        affixes: [
          { slotKey: 'A1', statId: 'STAMINA', value: 18, min: 10, max: 20, step: 1, affixTier: 2, isGuaranteed: true, isReforgeSlot: false },
        ],
      },
    })
    shield.stats.armor = 148
    shield.stats.stamina = 18
    store.snapshot = snapshot({ offHand: shield })

    const wrapper = mount(CharacterOverviewV2)
    await wrapper.get('[data-equipment-slot="offHand"]').trigger('click')
    await flushPromises()

    const text = document.body.textContent ?? ''
    expect(text).toContain('Оплот Хранителя Глубин')
    expect(text).toContain('Легендарный')
    expect(text).toContain('Уровень предмета')
    expect(text).toContain('24')
    expect(text).toContain('Требуется уровень')
    expect(text).toContain('16')
    expect(text).toContain('Броня')
    expect(text).toContain('+148')
    expect(text).toContain('Шанс блока')
    expect(text).toContain('12.5%')
    expect(text).toContain('Сила блока')
    expect(text).toContain('38–65')
    expect(text).toContain('Качество ролла')
    expect(text).toContain('94%')
    expect(text).toContain('Выносливость')
  })

  it('keeps empty-slot navigation and the full-stats shortcut intact', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot({})
    const wrapper = mount(CharacterOverviewV2)

    await wrapper.get('[data-equipment-slot="head"]').trigger('click')
    expect(wrapper.emitted('select-empty-slot')?.[0]).toEqual(['Head'])

    await wrapper.get('[data-open-full-stats]').trigger('click')
    expect(wrapper.emitted('open-stats')).toHaveLength(1)
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
    equippedSlot: slot,
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
    description: 'Тестовый предмет.',
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
      level: 25,
      experience: 340,
      xpToNextLevel: 1000,
      gold: 55,
      primaryAttribute: 'STRENGTH',
      classProfileVersion: '0.2.0',
      knownAbilityIds: [],
      knownAbilities: [],
      stats: {
        strength: 176,
        agility: 72,
        intellect: 18,
        stamina: 921,
        maxHp: 2840,
        attackPower: 310,
        spellPower: 22,
        criticalChance: 12.25,
        criticalDamage: 150,
        accuracy: 96,
        armorPenetration: 8,
        magicPenetration: 0,
        attackSpeed: 1.12,
        armor: 291.5,
        magicResistance: 84,
        dodge: 4.5,
      },
      statBreakdown: {
        armorDamageReductionPercent: { finalValue: 38.25, contributions: [] },
        magicDamageReductionPercent: { finalValue: 17.5, contributions: [] },
        blockChance: { finalValue: 18.4, contributions: [] },
        blockValueMin: { finalValue: 38, contributions: [] },
        blockValueMax: { finalValue: 65, contributions: [] },
      } as never,
      vitals: {
        currentHp: 2715,
        maxHp: 2840,
        resourceType: 'RAGE',
        currentResource: 35,
        maxResource: 100,
        checkpointedAtUtc: '2026-09-14T08:00:00Z',
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
    contentVersion: '0.18.0',
    balanceVersion: '0.14.0',
    serverTimeUtc: '2026-09-14T08:00:00Z',
  }
}
