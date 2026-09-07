import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { BootstrapSnapshot, InventoryItem, MerchantSnapshot } from '@/api/contracts'
import MerchantShop from '@/game/world/components/MerchantShop.vue'
import { useGameSessionStore } from '@/stores/gameSession'

describe('MerchantShop', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('renders a selectable storefront and buys only through the merchant store action', async () => {
    const session = useGameSessionStore()
    session.snapshot = snapshot([])
    const getMerchant = vi.spyOn(session, 'getMerchant').mockResolvedValue(merchantSnapshot())
    const buy = vi.spyOn(session, 'buyMerchantItem').mockResolvedValue({
      ...merchantSnapshot(),
      gold: 80,
    })

    const wrapper = mount(MerchantShop, {
      props: { open: false },
      global: { stubs: { Teleport: true } },
    })

    await wrapper.setProps({ open: true })
    await flushPromises()

    expect(getMerchant).toHaveBeenCalledWith('MARCUS_SUPPLIES')
    expect(wrapper.get('[data-merchant-offer="SMALL_HEALING_POTION"]').attributes('aria-pressed')).toBe('true')
    expect(wrapper.get('[data-merchant-detail]').text()).toContain('Малое зелье лечения')
    expect(wrapper.get('[data-merchant-detail]').text()).toContain('+50 здоровья')

    await wrapper.get('[data-buy-selected]').trigger('click')
    await flushPromises()

    expect(buy).toHaveBeenCalledWith('MARCUS_SUPPLIES', 'SMALL_HEALING_POTION', 1)
    expect(wrapper.get('.merchant__wallet').text()).toContain('80')
  })

  it('excludes protected items from sell actions and sells through the generic merchant action', async () => {
    const session = useGameSessionStore()
    session.snapshot = snapshot([
      material('OPEN_HIDE', 'Шкура волка', false),
      material('LOCKED_HIDE', 'Защищённая шкура', true),
    ])
    vi.spyOn(session, 'getMerchant').mockResolvedValue(merchantSnapshot())
    const sell = vi.spyOn(session, 'sellMerchantItem').mockResolvedValue(merchantSnapshot())

    const wrapper = mount(MerchantShop, {
      props: { open: false },
      global: { stubs: { Teleport: true } },
    })

    await wrapper.setProps({ open: true })
    await flushPromises()
    await wrapper.get('[data-merchant-tab="sell"]').trigger('click')

    expect(wrapper.find('[data-sell-item="OPEN_HIDE"]').exists()).toBe(true)
    expect(wrapper.find('[data-sell-item="LOCKED_HIDE"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Защищённые предметы скрыты из продажи: 1')

    const buttons = wrapper.get('[data-sell-item="OPEN_HIDE"]').findAll('button')
    await buttons[0]!.trigger('click')
    await flushPromises()

    expect(sell).toHaveBeenCalledWith('MARCUS_SUPPLIES', 'OPEN_HIDE', 1)
  })
})

function merchantSnapshot(): MerchantSnapshot {
  return {
    id: 'MARCUS_SUPPLIES',
    name: 'Маркус',
    description: 'Торговец припасами.',
    gold: 100,
    items: [
      {
        definitionId: 'SMALL_HEALING_POTION',
        name: 'Малое зелье лечения',
        type: 'Consumable',
        rarity: 'Common',
        description: 'Мгновенно восстанавливает здоровье.',
        buyPriceGold: 20,
        sellPriceGold: 0,
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
        iconId: null,
      },
      {
        definitionId: 'FIELD_TONIC',
        name: 'Походный тоник',
        type: 'Consumable',
        rarity: 'Uncommon',
        description: 'Редкий запас.',
        buyPriceGold: 35,
        sellPriceGold: 0,
        consumableActions: [
          {
            type: 'ApplyEffect',
            amount: 0,
            resourceType: null,
            effectId: 'FIELD_TONIC_BUFF',
            dispelCategory: null,
          },
        ],
        consumableCooldownCategoryId: 'UTILITY_POTION',
        consumableCooldownSeconds: 60,
        iconId: null,
      },
    ],
  }
}

function material(id: string, name: string, isLocked: boolean): InventoryItem {
  return {
    id,
    definitionId: id,
    name,
    type: 'Material',
    rarity: 'Common',
    requiredLevel: 1,
    quantity: 3,
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
    description: name,
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
    sellPriceGold: 2,
    isLocked,
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
      level: 1,
      experience: 0,
      xpToNextLevel: 100,
      gold: 100,
      primaryAttribute: 'STRENGTH',
      classProfileVersion: '0.10.0',
      knownAbilityIds: [],
      knownAbilities: [],
      stats: {
        strength: 12,
        agility: 6,
        intellect: 4,
        stamina: 10,
        maxHp: 150,
        attackPower: 30,
        spellPower: 8,
        criticalChance: 5,
        criticalDamage: 100,
        accuracy: 95,
        armorPenetration: 0,
        magicPenetration: 0,
        attackSpeed: 1,
        armor: 32,
        magicResistance: 14,
        dodge: 1,
      },
      statBreakdown: {} as never,
      vitals: {
        currentHp: 150,
        maxHp: 150,
        resourceType: 'RAGE',
        currentResource: 0,
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
