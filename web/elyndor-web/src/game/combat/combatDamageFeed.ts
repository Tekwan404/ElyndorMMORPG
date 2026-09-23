import type { CombatEvent } from '@/api/contracts'

export type CombatDamageFeedSide = 'player' | 'enemy'
export type CombatDamageFeedKind = 'damage' | 'healing'

export interface CombatDamageFeedHit {
  sequence: number
  side: CombatDamageFeedSide
  kind: CombatDamageFeedKind
  amount: number
  critical: boolean
}

export interface CombatDamageFeedBatch {
  hits: CombatDamageFeedHit[]
  latestSequence: number
}

function isCriticalDamage(events: CombatEvent[], index: number, event: CombatEvent): boolean {
  const previous = index > 0 ? events[index - 1] : undefined
  return previous?.type === 'CriticalHit'
    && previous.sourceActorId === event.sourceActorId
    && previous.targetActorId === event.targetActorId
    && previous.amount === event.amount
}

export function collectCombatDamageFeedHits(
  events: CombatEvent[],
  afterSequence: number,
  playerActorId: string,
  enemyActorId: string,
): CombatDamageFeedBatch {
  const hits: CombatDamageFeedHit[] = []
  let latestSequence = afterSequence

  for (let index = 0; index < events.length; index += 1) {
    const event = events[index]!
    latestSequence = Math.max(latestSequence, event.sequence)

    if (event.sequence <= afterSequence || event.amount <= 0) continue
    if (event.type !== 'DamageDealt' && event.type !== 'HealingApplied') continue

    const side: CombatDamageFeedSide | null = event.targetActorId === playerActorId
      ? 'player'
      : event.targetActorId === enemyActorId
        ? 'enemy'
        : null
    if (!side) continue

    const kind: CombatDamageFeedKind = event.type === 'HealingApplied' ? 'healing' : 'damage'
    hits.push({
      sequence: event.sequence,
      side,
      kind,
      amount: Math.max(1, Math.round(event.amount)),
      critical: kind === 'damage' && isCriticalDamage(events, index, event),
    })
  }

  return { hits, latestSequence }
}
