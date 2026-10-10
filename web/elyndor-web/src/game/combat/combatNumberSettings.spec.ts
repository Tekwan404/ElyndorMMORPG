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
      showThreatTable: true,
    })).toEqual({
      density: 'minimal',
      emphasizeCrits: false,
      hitEffects: false,
      showPeriodicDamage: false,
      showThreatTable: true,
    })
  })

  it('falls back safely when local storage contains unsupported settings', () => {
    expect(normalizeCombatNumberSettings({
      density: 'ultra',
      emphasizeCrits: 'false',
      hitEffects: null,
      showThreatTable: 'true',
    })).toEqual(DEFAULT_COMBAT_NUMBER_SETTINGS)
  })
})
