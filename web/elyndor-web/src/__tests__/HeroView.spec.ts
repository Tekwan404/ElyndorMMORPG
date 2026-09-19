import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { BootstrapSnapshot, InventoryItem } from '@/api/contracts'
import HeroView from '@/game/character/views/HeroView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

describe('HeroView', () => {
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

  it('enables and opens the talents tab for Paladin', async () => {
    const session = useGameSessionStore()
    session.snapshot = snapshot([], 'PALADIN')

    const wrapper = mount(HeroView, {
      global: {
        stubs: {
          CharacterOverviewView: true,
          CharacterStatsView: true,
          InventoryView: true,
          TalentTreeView: { template: '<div data-paladin-talent-tree />' },
        },
      },
    })

    const talentsTab = wrapper.get('[data-hero-tab="talents"]')
    expect(talentsTab.attributes('disabled')).toBeUndefined()

    await talentsTab.trigger('click')

    expect(talentsTab.attributes('aria-current')).toBe('page')
    expect(wrapper.find('[data-paladin-talent-tree]').exists()).toBe(true)
  })

  it('shows the companion tab only for Archer', async () => {
    const session = useGameSessionStore()
    session.snapshot = snapshot([], 'ARCHER')

    const wrapper = mount(HeroView, {
      global: {
        stubs: {
          CharacterOverviewView: true,
          CharacterStatsView: true,
          InventoryView: true,
          TalentTreeView: true,
          CompanionView: { template: '<div data-companion-view />' },
        },
      },
    })

    await wrapper.get('[data-hero-tab="companion"]').trigger('click')

    expect(wrapper.find('[data-companion-view]').exists()).toBe(true)
  })

  it('opens empty equipment slots in a contextual picker with an explicit back action', async () => {
    const session = useGameSessionStore()
    const helmet = equipment('TEST_HELMET', 'Шлем стража', 'Head')
    const chest = equipment('TEST_CHEST', 'Кираса стража', 'Chest')
    session.snapshot = snapshot([helmet, chest])

    const wrapper = mount(HeroView)

    await wrapper.get('[data-equipment-slot="head"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-hero-tab="inventory"]').attributes('aria-current')).toBe('page')
    expect(wrapper.get('[data-close-slot-inventory]').text()).toContain('Назад')
    expect(wrapper.text()).toContain('Выберите: шлем')
    expect(wrapper.find('[data-item-id="TEST_HELMET"]').exists()).toBe(true)
    expect(wrapper.find('[data-item-id="TEST_CHEST"]').exists()).toBe(false)

    await wrapper.get('[data-close-slot-inventory]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-hero-tab="character"]').attributes('aria-current')).toBe('page')
    expect(wrapper.find('[data-equipment-slot="head"]').exists()).toBe(true)
  })

  it('opens Change for an equipped slot and compares a replacement with the currently equipped item', async () => {
    const session = useGameSessionStore()
    const currentHelmet = equipment('CURRENT_HELMET', 'Шлем ветерана', 'Head')
    currentHelmet.equippedSlot = 'Head'
    currentHelmet.stats.stamina = 4
    const candidate = equipment('NEW_HELMET', 'Шлем бастиона', 'Head')
    candidate.stats.stamina = 8
    session.snapshot = snapshot([currentHelmet, candidate])
    session.snapshot.character!.inventory.equipped.head = currentHelmet

    const wrapper = mount(HeroView)

    await wrapper.get('[data-equipment-slot="head"]').trigger('click')
    await flushPromises()

    const changeAction = document.body.querySelector<HTMLButtonElement>('[data-change-equipment]')
    expect(changeAction).not.toBeNull()
    expect(changeAction?.textContent).toContain('Сменить')
    changeAction?.click()
    await flushPromises()

    expect(wrapper.get('[data-hero-tab="inventory"]').attributes('aria-current')).toBe('page')
    expect(wrapper.get('[data-close-slot-inventory]').exists()).toBe(true)
    expect(wrapper.find('[data-item-id="NEW_HELMET"]').exists()).toBe(true)
    expect(wrapper.find('[data-item-id="CURRENT_HELMET"]').exists()).toBe(false)

    await wrapper.get('[data-item-id="NEW_HELMET"]').trigger('click')
    await flushPromises()

    expect(document.body.textContent).toContain('СРАВНЕНИЕ')
    expect(document.body.textContent).toContain('Сейчас: Шлем ветерана')
  })

  it('returns to the character tab after a successful equip from a contextual slot picker', async () => {
    const session = useGameSessionStore()
    const helmet = equipment('TEST_HELMET', 'Шлем стража', 'Head')
    session.snapshot = snapshot([helmet])
    const equip = vi.spyOn(session, 'equip').mockImplementation(async (itemId, targetSlot) => {
      const item = session.snapshot?.character?.inventory.items.find(candidate => candidate.id === itemId)
      if (!item || !session.snapshot?.character) return
      item.equippedSlot = targetSlot ?? item.slot
      session.snapshot.character.inventory.equipped.head = item
    })

    const wrapper = mount(HeroView)

    await wrapper.get('[data-equipment-slot="head"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-item-id="TEST_HELMET"]').trigger('click')
    await flushPromises()

    const equipAction = document.body.querySelector<HTMLButtonElement>('[data-equip-action]')
    expect(equipAction).not.toBeNull()
    equipAction?.click()
    await flushPromises()

    expect(equip).toHaveBeenCalledWith('TEST_HELMET', undefined)
    expect(wrapper.get('[data-hero-tab="character"]').attributes('aria-current')).toBe('page')
    expect(wrapper.get('[data-equipment-slot="head"]').attributes('data-filled')).toBe('true')
  })

  it('does not offer class-incompatible items in a contextual equipment picker', async () => {
    const session = useGameSessionStore()
    const leatherHood = equipment('LEATHER_HOOD', 'Кожаный капюшон', 'Head')
    leatherHood.armorCategory = 'LEATHER'
    session.snapshot = snapshot([leatherHood], 'WARRIOR')
    const equip = vi.spyOn(session, 'equip').mockResolvedValue(undefined)

    const wrapper = mount(HeroView)

    await wrapper.get('[data-equipment-slot="head"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-hero-tab="inventory"]').attributes('aria-current')).toBe('page')
    expect(wrapper.find('[data-item-id="LEATHER_HOOD"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('В рюкзаке нет предметов для выбранного слота.')
    expect(equip).not.toHaveBeenCalled()
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
