import { describe, expect, it } from 'vitest'

import { resolveAbilityArt, resolveTalentArt } from '@/game/talents/talentArt'

describe('talent art registry', () => {
  it('resolves Berserker art and preserves generated fallback for other branches', () => {
    expect(resolveTalentArt('BERSERKER_WAR_MASK')).toMatch(/berserker-war-mask\.webp$/)
    expect(resolveTalentArt(null)).toBeNull()
    expect(resolveTalentArt('GUARDIAN_01')).toMatch(/guardian-01\.webp$/)
    expect(resolveTalentArt('FIRE_01')).toMatch(/fire-01\.webp$/)
    expect(resolveTalentArt('MARKSMAN_01')).toMatch(/marksman-01\.webp$/)
    expect(resolveAbilityArt('WHIRLWIND')).toBe(resolveTalentArt('BERSERKER_BLOOD_BLADES'))
    expect(resolveAbilityArt('BATTLE_CRY')).toMatch(/warlord-05\.webp$/)
  })

  it('resolves V2 branch art for every playable class', () => {
    for (const iconId of [
      'MARKSMAN_01',
      'BEAST_MASTERY_01',
      'SURVIVAL_01',
      'FIRE_01',
      'ARCANE_01',
      'FROST_01',
      'PALADIN_HOLY_01',
      'PALADIN_PROTECTION_01',
      'PALADIN_RETRIBUTION_01',
      'BERSERKER_01',
      'GUARDIAN_01',
      'WARLORD_01',
    ]) {
      expect(resolveTalentArt(iconId)).toBeTruthy()
    }
  })

  it('prefers a dedicated spell icon over the talent art fallback', () => {
    expect(resolveAbilityArt('MAGE_FIREBALL', 'mage-fireball')).toMatch(/mage-fireball\.webp$/)
    expect(resolveAbilityArt('HOLY_LIGHT', 'paladin-holy-light')).toMatch(/paladin-holy-light\.webp$/)
  })
})
