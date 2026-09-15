import { describe, expect, it } from 'vitest'

import { isAuraAbility } from './combatAbilityGroups'

describe('isAuraAbility', () => {
  it('keeps paladin auras out of the active ability hotbar', () => {
    expect(isAuraAbility('DEVOTION_AURA')).toBe(true)
    expect(isAuraAbility('CONCENTRATION_AURA')).toBe(true)
    expect(isAuraAbility('SANCTITY_AURA')).toBe(true)
    expect(isAuraAbility('HOLY_LIGHT')).toBe(false)
  })
})
