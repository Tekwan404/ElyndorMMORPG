import { describe, expect, it } from 'vitest'

import { resolveAbilityArt, resolveTalentArt } from '@/game/talents/talentArt'

const berserkerTalentArt = {
  BERSERKER_BLADE_GUARD: 'berserker-blade-guard.webp',
  BERSERKER_BLOOD_BLADES: 'berserker-blood-blades.webp',
  BERSERKER_BLOOD_RENEWAL: 'berserker-blood-renewal.webp',
  BERSERKER_CRUSHING_BLOW: 'berserker-crushing-blow.webp',
  BERSERKER_IRON_WILL: 'berserker-iron-will.webp',
  BERSERKER_KEEN_EYE: 'berserker-keen-eye.webp',
  BERSERKER_RAGE_SLASH: 'berserker-rage-slash.webp',
  BERSERKER_SHATTER_GUARD: 'berserker-shatter-guard.webp',
  BERSERKER_SUNDERING_BLADE: 'berserker-sundering-blade.webp',
  BERSERKER_WAR_MASK: 'berserker-war-mask.webp',
} as const

describe('talent art registry', () => {
  it('resolves every Berserker artwork asset', () => {
    for (const [iconId, filename] of Object.entries(berserkerTalentArt)) {
      expect(resolveTalentArt(iconId)).toMatch(new RegExp(`${filename.replace('.', '\\.')}$$`))
    }
  })

  it('preserves generated fallback for branches without raster talent art', () => {
    expect(resolveTalentArt(null)).toBeNull()
    expect(resolveTalentArt('GUARDIAN_IRON_SKIN')).toBeNull()
  })

  it('reuses Berserker talent art for unlocked abilities', () => {
    expect(resolveAbilityArt('WILD_STRIKE')).toBe(resolveTalentArt('BERSERKER_RAGE_SLASH'))
    expect(resolveAbilityArt('WHIRLWIND')).toBe(resolveTalentArt('BERSERKER_BLOOD_BLADES'))
    expect(resolveAbilityArt('BERSERK')).toBe(resolveTalentArt('BERSERKER_WAR_MASK'))
  })
})
