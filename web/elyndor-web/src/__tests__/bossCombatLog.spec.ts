import { describe, expect, it } from 'vitest'

import type { CombatActorSnapshot, CombatSnapshot } from '@/api/contracts'
import { isBossCombatLogEligible } from '@/game/combat/bossCombatLog'

function enemy(definitionId: string, monsterRank: string | null): CombatActorSnapshot {
  return { definitionId, monsterRank } as CombatActorSnapshot
}

function snapshot(
  definitionId: string,
  monsterRank: string | null,
): Pick<CombatSnapshot, 'enemy' | 'enemies'> {
  return {
    enemy: enemy(definitionId, monsterRank),
    enemies: [enemy(definitionId, monsterRank)],
  }
}

describe('boss combat log eligibility', () => {
  it('accepts ordinary bosses', () => {
    expect(isBossCombatLogEligible(snapshot('ARCHON_OF_THE_DEAD_STAR', 'Boss'))).toBe(true)
  })

  it('accepts world bosses', () => {
    expect(isBossCombatLogEligible(snapshot('WORLD_BOSS_ASH_ARCHON_L30', 'Boss'))).toBe(true)
  })

  it('keeps training dummy diagnostics', () => {
    expect(isBossCombatLogEligible(snapshot('TRAINING_DUMMY', null))).toBe(true)
  })

  it('rejects normal monsters', () => {
    expect(isBossCombatLogEligible(snapshot('WHISPERING_FOREST_WOLF', 'Normal'))).toBe(false)
  })
})
