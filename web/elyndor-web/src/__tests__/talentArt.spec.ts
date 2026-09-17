import { readFileSync, readdirSync } from 'node:fs'
import { resolve } from 'node:path'

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

  it('matches Archer spell art to its unlocking talent and keeps talent text readable', () => {
    const contentRoot = resolve(process.cwd(), '../../content')
    const talentTree = JSON.parse(
      readFileSync(resolve(contentRoot, 'talents/archer.json'), 'utf8').replace(/^\uFEFF/, ''),
    ) as { talentTrees: Array<{ nodes: Array<{ iconId: string; name: string; description: string; modifiers: Array<{ key: string; targetId?: string }> }> }> }
    const abilities = JSON.parse(
      readFileSync(resolve(contentRoot, 'abilities/archer.json'), 'utf8').replace(/^\uFEFF/, ''),
    ) as { abilities: Array<{ id: string }> }
    const iconByAbility = new Map<string, string>()

    for (const node of talentTree.talentTrees.flatMap(tree => tree.nodes)) {
      for (const modifier of node.modifiers) {
        if (modifier.key === 'UNLOCK_ABILITY' && modifier.targetId) {
          iconByAbility.set(modifier.targetId, node.iconId)
        }
      }
    }

    for (const ability of abilities.abilities) {
      expect(resolveAbilityArt(ability.id)).toBe(resolveTalentArt(iconByAbility.get(ability.id)))
    }

    const archerNodes = talentTree.talentTrees.flatMap(tree => tree.nodes)
    expect(archerNodes.every(node => !/[�]/.test(node.name + node.description))).toBe(true)

    const abilityRoot = resolve(contentRoot, 'abilities')
    const allAbilities = readdirSync(abilityRoot)
      .filter(fileName => fileName.endsWith('.json'))
      .flatMap((fileName) => {
        const raw = readFileSync(resolve(abilityRoot, fileName), 'utf8').replace(/^\uFEFF/, '')
        return (JSON.parse(raw) as { abilities: Array<{ id: string; iconId?: string | null }> }).abilities
      })
    expect(allAbilities.filter(ability => !resolveAbilityArt(ability.id, ability.iconId)).map(ability => ability.id)).toEqual([])

    const markIcon = resolveTalentArt('MARKSMAN_06')

    expect(markIcon).toMatch(/marksman-06\.webp$/)
    expect(resolveAbilityArt('HUNTER_MARK')).toBe(markIcon)
    expect(resolveTalentArt('MARKSMAN_03')).toMatch(/marksman-03\.webp$/)
    expect(resolveTalentArt('MARKSMAN_13')).toMatch(/marksman-13\.webp$/)
    expect(resolveAbilityArt('PIERCING_ARROW')).toMatch(/marksman-05\.webp$/)
    expect(resolveAbilityArt('AIMED_SHOT')).toMatch(/marksman-07\.webp$/)
    expect(resolveAbilityArt('MULTI_SHOT')).toMatch(/marksman-12\.webp$/)
    expect(resolveAbilityArt('DISORIENTING_SHOT')).toMatch(/marksman-09\.webp$/)
    expect(resolveAbilityArt('SNIPER_FOCUS')).toMatch(/marksman-16\.webp$/)
    expect(resolveAbilityArt('MEND_PET')).toMatch(/beast-mastery-07\.webp$/)
    expect(resolveAbilityArt('INTIMIDATION')).toMatch(/beast-mastery-02\.webp$/)
    expect(resolveAbilityArt('BESTIAL_WRATH')).toMatch(/beast-mastery-06\.webp$/)
    expect(resolveAbilityArt('DETERRENCE')).toMatch(/survival-22\.webp$/)
    expect(resolveAbilityArt('WYVERN_STING')).toMatch(/survival-04\.webp$/)
  })
})
