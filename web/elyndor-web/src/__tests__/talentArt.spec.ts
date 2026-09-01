import { describe, expect, it } from 'vitest'

import { resolveAbilityArt, resolveTalentArt } from '@/game/talents/talentArt'

describe('talent art registry', () => {
  it('resolves Berserker art and preserves generated fallback for other branches', () => {
    expect(resolveTalentArt('BERSERKER_WAR_MASK')).toMatch(/berserker-war-mask\.webp$/)
    expect(resolveTalentArt(null)).toBeNull()
    expect(resolveTalentArt('GUARDIAN_IRON_SKIN')).toBeNull()
    expect(resolveAbilityArt('WHIRLWIND')).toBe(resolveTalentArt('BERSERKER_BLOOD_BLADES'))
  })
})
