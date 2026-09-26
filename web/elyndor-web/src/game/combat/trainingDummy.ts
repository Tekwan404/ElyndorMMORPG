import type { CombatSnapshot } from '@/api/contracts'

export const TRAINING_DUMMY_ID = 'TRAINING_DUMMY'

export function isTrainingDummyCombat(
  snapshot: Pick<CombatSnapshot, 'enemy' | 'enemies'>,
): boolean {
  const enemies = snapshot.enemies ?? [snapshot.enemy]
  return enemies.some(enemy => enemy.definitionId === TRAINING_DUMMY_ID)
}
