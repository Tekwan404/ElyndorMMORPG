import { beforeEach, describe, expect, it } from 'vitest'

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
})
