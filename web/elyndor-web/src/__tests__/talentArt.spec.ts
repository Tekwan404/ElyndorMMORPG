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
    expect(resolveAbilityArt('HOLY_LIGHT', 'paladin-holy-light')).toMatch(
      /paladin-holy-light\.webp$/,
    )
  })

  it('uses matching class art for Archer abilities without dedicated spell-sheet icons', () => {
    const abilityIcons = {
      HUNTER_MARK: 'marksman-05',
      PIERCING_ARROW: 'marksman-07',
      AIMED_SHOT: 'marksman-09',
      SHOCKING_SHOT: 'marksman-04',
      MULTI_SHOT: 'marksman-26',
      DISORIENTING_SHOT: 'marksman-06',
      SNIPER_FOCUS: 'marksman-02',
      HEAVY_ARROW: 'marksman-08',
      VOLLEY: 'marksman-12',
      COMMAND_ATTACK: 'beast-mastery-13',
      MEND_PET: 'beast-mastery-14',
      INTIMIDATION: 'beast-mastery-02',
      BESTIAL_WRATH: 'beast-mastery-28',
      RETURN_TO_OWNER: 'beast-mastery-26',
      SERPENT_STING: 'survival-03',
      FREEZING_TRAP: 'survival-05',
      IMMOLATION_TRAP: 'survival-07',
      EXPLOSIVE_TRAP: 'survival-09',
      DETERRENCE: 'survival-28',
      WYVERN_STING: 'survival-04',
      PREPARATION: 'survival-25',
    } as const

    for (const [abilityId, iconName] of Object.entries(abilityIcons)) {
      expect(resolveAbilityArt(abilityId)).toMatch(new RegExp(`${iconName}\\.webp$`))
    }
  })
})
