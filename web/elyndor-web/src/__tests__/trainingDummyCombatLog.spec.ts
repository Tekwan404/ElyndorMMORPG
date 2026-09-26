import { describe, expect, it } from 'vitest'

import type { CombatActorSnapshot, CombatSnapshot } from '@/api/contracts'
import { isTrainingDummyCombat } from '@/game/combat/trainingDummy'

function enemy(definitionId: string): CombatActorSnapshot {
  return { definitionId } as CombatActorSnapshot
}

function snapshot(
  primaryEnemyId: string,
  enemyIds?: string[],
): Pick<CombatSnapshot, 'enemy' | 'enemies'> {
  return {
    enemy: enemy(primaryEnemyId),
    enemies: enemyIds?.map(enemy),
  }
}

describe('training dummy combat log eligibility', () => {
  it('accepts a training dummy session', () => {
    expect(isTrainingDummyCombat(snapshot('TRAINING_DUMMY'))).toBe(true)
  })

  it('rejects normal and boss encounters', () => {
    expect(isTrainingDummyCombat(snapshot('WHISPERING_FOREST_WOLF'))).toBe(false)
    expect(isTrainingDummyCombat(snapshot('ARCHON_OF_THE_DEAD_STAR'))).toBe(false)
  })

  it('checks every enemy in a multi-enemy snapshot', () => {
    expect(
      isTrainingDummyCombat(
        snapshot('WHISPERING_FOREST_WOLF', ['WHISPERING_FOREST_WOLF', 'TRAINING_DUMMY']),
      ),
    ).toBe(true)
  })
})
