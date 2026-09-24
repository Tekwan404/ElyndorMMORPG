import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import type { CombatAbility, InventoryItem } from '@/api/contracts'
import CombatAbilityHotbar from '@/game/combat/CombatAbilityHotbar.vue'

const ability: CombatAbility = {
  id: 'TEST_STRIKE',
  displayName: 'Тестовый удар',
  description: 'Тестовая способность.',
  iconId: null,
  resourceCost: 10,
  cooldownSeconds: 20,
}

const consumable = {
  definitionId: 'TEST_POTION',
  name: 'Тестовое зелье',
  quantity: 3,
} as InventoryItem

function mountHotbar(cooldown = 0) {
  return mount(CombatAbilityHotbar, {
    props: {
      slots: [ability, null],
      auraAbilities: [],
      consumables: [consumable],
      queuedAbilityIds: [],
      fireballStreak: 0,
      heatActive: false,
      combustionActive: false,
      abilityState: () => cooldown > 0 ? 'cooldown' : 'ready',
      abilityIcon: () => undefined,
      abilityGlyph: () => 'sword',
      cooldownRemaining: () => cooldown,
      consumableCooldownRemaining: () => 0,
      consumableCanAffect: () => true,
      consumableGlyph: () => 'potion',
    },
    global: {
      stubs: {
        IconGenerator: { template: '<span data-icon />' },
      },
    },
  })
}

describe('CombatAbilityHotbar', () => {
  it('keeps consumables outside the ability grid without collapsing empty skill slots', () => {
    const wrapper = mountHotbar()

    expect(wrapper.find('[data-combat-abilities] [data-combat-consumable]').exists()).toBe(false)
    expect(wrapper.find('[data-combat-consumables] [data-combat-consumable="TEST_POTION"]').exists()).toBe(true)
    expect(wrapper.findAll('[data-combat-abilities] .combat-ability-hotbar__slot')).toHaveLength(2)
    expect(wrapper.get('[data-ability-slot="TEST_STRIKE"]').text()).toContain('Тестовый удар')
  })

  it('emits skill and consumable actions independently', async () => {
    const wrapper = mountHotbar()

    await wrapper.get('[data-ability-slot="TEST_STRIKE"]').trigger('click')
    await wrapper.get('[data-combat-consumable="TEST_POTION"]').trigger('click')

    expect(wrapper.emitted('use')).toEqual([[ability]])
    expect(wrapper.emitted('useConsumable')).toEqual([[consumable]])
  })

  it('renders bounded radial cooldown progress in seconds', () => {
    const wrapper = mountHotbar(10)
    const cooldown = wrapper.get('.combat-ability-hotbar__cooldown')

    expect(cooldown.text()).toBe('10с')
    expect(cooldown.attributes('style')).toContain('--cooldown-progress: 50%')
  })

  it('does not fire an ability while it is on cooldown', async () => {
    const wrapper = mountHotbar(10)
    const slot = wrapper.get('[data-ability-slot="TEST_STRIKE"]')

    await slot.trigger('click')

    expect(slot.attributes('aria-disabled')).toBe('true')
    expect(wrapper.emitted('use')).toBeUndefined()
  })
})