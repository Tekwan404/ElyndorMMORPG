import { mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it } from 'vitest'

import type { KnownAbility } from '@/api/contracts'
import CombatHotbarSettings from '@/game/combat/CombatHotbarSettings.vue'

import {
  loadCombatHotbarOrder,
  normalizeCombatHotbarOrder,
  orderCombatAbilities,
  resetCombatHotbarOrder,
  saveCombatHotbarOrder,
} from '@/game/combat/combatHotbarSettings'

describe('combatHotbarSettings', () => {
  beforeEach(() => {
    globalThis.localStorage.clear()
  })

  it('keeps the preferred order, removes stale ids and appends newly unlocked abilities', () => {
    expect(normalizeCombatHotbarOrder(
      ['A', 'B', 'C', 'D'],
      ['C', 'STALE', 'A', 'C'],
    )).toEqual(['C', 'A', 'B', 'D'])
  })

  it('stores layouts separately for each character', () => {
    saveCombatHotbarOrder('hero-a', ['B', 'A'])
    saveCombatHotbarOrder('hero-b', ['A', 'B'])

    expect(loadCombatHotbarOrder('hero-a', ['A', 'B'])).toEqual(['B', 'A'])
    expect(loadCombatHotbarOrder('hero-b', ['A', 'B'])).toEqual(['A', 'B'])
  })

  it('orders combat abilities using the saved layout', () => {
    saveCombatHotbarOrder('hero', ['C', 'A', 'B'])
    const abilities = [{ id: 'A' }, { id: 'B' }, { id: 'C' }]

    expect(orderCombatAbilities('hero', abilities).map(ability => ability.id)).toEqual(['C', 'A', 'B'])
  })

  it('restores the server/default order after reset', () => {
    saveCombatHotbarOrder('hero', ['B', 'A'])
    resetCombatHotbarOrder('hero')

    expect(loadCombatHotbarOrder('hero', ['A', 'B'])).toEqual(['A', 'B'])
  })

  it('renders exactly twelve active slots and swaps a reserve ability with two taps', async () => {
    const abilities = Array.from({ length: 13 }, (_, index) => ability(`ABILITY_${index + 1}`))
    const wrapper = mount(CombatHotbarSettings, {
      props: { characterId: 'hero', abilities },
    })

    expect(wrapper.findAll('[data-hotbar-slot]')).toHaveLength(12)
    expect(wrapper.get('[data-hotbar-slot="12"]').text()).toContain('ABILITY_12')
    expect(wrapper.get('.hotbar-settings__reserve').text()).toContain('ABILITY_13')

    await wrapper.get('[data-hotbar-slot="12"]').trigger('click')
    await wrapper.get('.hotbar-slot--reserve').trigger('click')

    expect(wrapper.get('[data-hotbar-slot="12"]').text()).toContain('ABILITY_13')
    expect(wrapper.get('.hotbar-settings__reserve').text()).toContain('ABILITY_12')
    expect(loadCombatHotbarOrder('hero', abilities.map(item => item.id)).slice(11, 13))
      .toEqual(['ABILITY_13', 'ABILITY_12'])
  })
})

function ability(id: string): KnownAbility {
  return {
    id,
    displayName: id,
    description: 'Test ability.',
    iconId: null,
    resourceCost: 0,
    cooldownSeconds: 0,
    type: 'Active',
    targetType: 'SingleEnemy',
    sourceTalentId: null,
    sourceTalentName: null,
  }
}
