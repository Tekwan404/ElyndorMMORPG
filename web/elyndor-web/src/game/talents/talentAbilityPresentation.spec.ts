import { describe, expect, it } from 'vitest'

import { unlockedAbilityLabel } from './talentAbilityPresentation'

describe('unlockedAbilityLabel', () => {
  it('uses the server-provided localized ability name', () => {
    expect(unlockedAbilityLabel({
      unlockedAbilityId: 'PALADIN_DIVINE_STORM',
      unlockedAbilityName: 'Божественная буря',
    })).toBe('Божественная буря')
  })

  it('falls back to the ability id instead of a generic placeholder', () => {
    expect(unlockedAbilityLabel({
      unlockedAbilityId: 'FUTURE_ABILITY',
      unlockedAbilityName: null,
    })).toBe('FUTURE_ABILITY')
  })

  it('does not render an unlock label when no ability is linked', () => {
    expect(unlockedAbilityLabel({ unlockedAbilityId: null })).toBeNull()
    expect(unlockedAbilityLabel({ unlockedAbilityId: '   ' })).toBeNull()
  })
})
