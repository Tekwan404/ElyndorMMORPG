import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import type { InventoryItem } from '@/api/contracts'
import ConsumableBar from './ConsumableBar.vue'

function consumable(id: string, quantity: number, name = 'Лечебное зелье'): InventoryItem {
  return {
    id: `${id}-${quantity}`,
    definitionId: id,
    name,
    type: 'Consumable',
    rarity: 'Common',
    requiredLevel: 1,
    quantity,
    slot: null,
    equippedSlot: null,
    stats: {
      stamina: 0,
      strength: 0,
      agility: 0,
      intellect: 0,
      spirit: 0,
      armor: 0,
      attackPower: 0,
      spellPower: 0,
      critRating: 0,
      hasteRating: 0,
    },
    description: '',
    setId: null,
    weaponCategory: null,
    armorCategory: null,
    weaponBaseAttackIntervalSeconds: null,
    attackSpeedPercent: 0,
    dodgePercent: 0,
    consumableActions: [{ type: 'RestoreHp', amount: 100 }],
    consumableCooldownCategoryId: 'HEALING_POTION',
    consumableCooldownSeconds: 30,
    buyPriceGold: 0,
    sellPriceGold: 0,
    isLocked: false,
    iconId: null,
    appearanceProfileId: null,
  } as InventoryItem
}

describe('ConsumableBar', () => {
  it('renders one compact slot per definition and sums physical inventory stacks', async () => {
    const wrapper = mount(ConsumableBar, {
      props: {
        items: [
          consumable('HEALING_POTION', 20),
          consumable('HEALING_POTION', 1),
          consumable('MAJOR_HEALING_POTION', 8, 'Большое лечебное зелье'),
        ],
        cooldownRemaining: () => 0,
        canUse: () => true,
        isPending: () => false,
      },
      global: {
        stubs: {
          ItemIcon: true,
        },
      },
    })

    const slots = wrapper.findAll('[data-combat-consumable]')
    expect(slots).toHaveLength(2)

    const healing = wrapper.get('[data-combat-consumable="HEALING_POTION"]')
    expect(healing.text()).toBe('×21')
    expect(healing.attributes('aria-label')).toBe('Лечебное зелье, 21 шт.')
    expect(wrapper.text()).not.toContain('Большое лечебное зелье')

    await healing.trigger('click')
    expect(wrapper.emitted('use')).toEqual([
      [expect.objectContaining({ definitionId: 'HEALING_POTION', quantity: 21 })],
    ])
  })

  it('keeps cooldown and availability behavior for the aggregated slot', () => {
    const cooldownRemaining = vi.fn(() => 4_000)
    const wrapper = mount(ConsumableBar, {
      props: {
        items: [consumable('HEALING_POTION', 20), consumable('HEALING_POTION', 1)],
        cooldownRemaining,
        canUse: () => true,
        isPending: () => false,
      },
      global: {
        stubs: {
          ItemIcon: true,
        },
      },
    })

    const healing = wrapper.get('[data-combat-consumable="HEALING_POTION"]')
    expect(healing.attributes()).toHaveProperty('disabled')
    expect(healing.text()).toContain('4с')
    expect(healing.text()).toContain('×21')
  })
})
