import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'

import type { CombatAbility } from '@/api/contracts'
import CombatAbilityHotbar from '@/game/combat/CombatAbilityHotbar.vue'

const ability: CombatAbility = {
  id: 'HUNTER_MARK',
  displayName: 'Метка охотника',
  description: 'Помечает врага и усиливает последующие атаки.',
  iconId: null,
  resourceCost: 10,
  cooldownSeconds: 6,
}

function mountHotbar(abilityState: 'cooldown' | 'resource' | 'ready' = 'ready') {
  return mount(CombatAbilityHotbar, {
    props: {
      slots: [ability],
      auraAbilities: [],
      consumables: [],
      queuedAbilityIds: [],
      fireballStreak: 0,
      heatActive: false,
      combustionActive: false,
      abilityState: () => abilityState,
      abilityIcon: () => undefined,
      abilityGlyph: () => 'bow',
      cooldownRemaining: () => 0,
      consumableCooldownRemaining: () => 0,
      consumableCanAffect: () => false,
      consumableGlyph: () => 'potion',
    },
  })
}

describe('CombatAbilityHotbar ability inspection', () => {
  afterEach(() => vi.useRealTimers())

  it('keeps a short press as ability activation', async () => {
    vi.useFakeTimers()
    const wrapper = mountHotbar()
    const button = wrapper.get('[data-ability-slot="HUNTER_MARK"]')

    await button.trigger('pointerdown')
    await vi.advanceTimersByTimeAsync(200)
    await button.trigger('pointerup')
    await button.trigger('click')

    expect(wrapper.emitted('use')).toEqual([[ability]])
    expect(wrapper.text()).not.toContain(ability.description)
  })

  it('shows ability details on long press without activating it', async () => {
    vi.useFakeTimers()
    const wrapper = mountHotbar('cooldown')
    const button = wrapper.get('[data-ability-slot="HUNTER_MARK"]')

    expect(button.attributes('aria-disabled')).toBe('true')
    await button.trigger('pointerdown', { pointerType: 'touch' })
    await vi.advanceTimersByTimeAsync(500)
    await button.trigger('pointerup', { pointerType: 'touch' })
    await button.trigger('click')

    expect(wrapper.text()).toContain(ability.description)
    expect(wrapper.emitted('use')).toBeUndefined()
  })
})
