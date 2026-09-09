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
})
