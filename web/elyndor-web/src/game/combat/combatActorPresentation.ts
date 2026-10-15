import type { CombatActorSnapshot } from '@/api/contracts'
import { resourceLabel } from '@/game/character/characterPresentation'

export function combatActorVitalsLabel(actor: CombatActorSnapshot): string {
  const hp = `Здоровье ${Math.ceil(actor.hp)} / ${Math.ceil(actor.maxHp)}`
  const resource =
    actor.resourceType !== 'NONE' && actor.maxResource > 0
      ? `, ${resourceLabel(actor.resourceType)} ${Math.ceil(actor.resource)} / ${Math.ceil(actor.maxResource)}`
      : ''
  return `${actor.name}, ${hp}${resource}`
}
