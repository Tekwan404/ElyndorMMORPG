import { describe, expect, it } from 'vitest'

import {
  DEFAULT_COMBAT_NUMBER_SETTINGS,
  normalizeCombatNumberSettings,
} from './combatNumberSettings'

describe('combat number settings', () => {
  it('defaults to lightweight balanced mobile visuals with critical and periodic damage', () => {
    expect(normalizeCombatNumberSettings(null)).toEqual(DEFAULT_COMBAT_NUMBER_SETTINGS)
  })

  it('accepts an explicit compact preference profile', () => {
    expect(normalizeCombatNumberSettings({
      density: 'minimal',
      emphasizeCrits: false,
      hitEffects: false,
      showPeriodicDamage: false,
    })).toEqual({
      density: 'minimal',
      emphasizeCrits: false,
      hitEffects: false,
      showPeriodicDamage: false,
    })
  })

  it('falls back safely when local storage contains unsupported settings', () => {
    expect(normalizeCombatNumberSettings({
      density: 'ultra',
      emphasizeCrits: 'false',
      hitEffects: null,
    })).toEqual(DEFAULT_COMBAT_NUMBER_SETTINGS)
  })
})
