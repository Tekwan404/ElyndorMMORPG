import type { CombatSnapshot } from '@/api/contracts'
import { isTrainingDummyCombat } from '@/game/combat/trainingDummy'

export function isBossCombatLogEligible(
  snapshot: Pick<CombatSnapshot, 'enemy' | 'enemies'>,
): boolean {
  if (isTrainingDummyCombat(snapshot)) return true
  const enemies = snapshot.enemies ?? [snapshot.enemy]
  return enemies.some((enemy) => enemy.monsterRank === 'Boss')
}
