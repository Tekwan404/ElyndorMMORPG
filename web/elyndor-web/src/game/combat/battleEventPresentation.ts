import type { CombatEvent } from '@/api/contracts'

export type CombatNumberKind = 'damage' | 'crit' | 'heal' | 'miss'
export type BattleLogSide = 'player' | 'ally' | 'enemy' | 'system'

export interface CombatNumberPresentation {
  key: number
  targetActorId: string
  kind: CombatNumberKind
  value: number | null
}

export interface BattleLogEntry {
  key: number
  side: BattleLogSide
  text: string
  occurredAtUtc: string
  eventType: string
}

export interface BattleEventProjectionContext {
  actorNames: ReadonlyMap<string, string>
  abilityNames: ReadonlyMap<string, string>
  enemyActorIds: ReadonlySet<string>
  localActorId: string
}

export interface BattleEventProjection {
  numbers: CombatNumberPresentation[]
  logEntries: BattleLogEntry[]
}

export function projectBattleEvents(
  events: readonly CombatEvent[],
  context: BattleEventProjectionContext,
): BattleEventProjection {
  const numbers: CombatNumberPresentation[] = []
  const logEntries: BattleLogEntry[] = []

  for (let index = 0; index < events.length; index += 1) {
    const event = events[index]!
    const previous = events[index - 1]

    if (event.type === 'DamageDealt' && event.targetActorId) {
      const critical = previous?.type === 'CriticalHit'
        && previous.sourceActorId === event.sourceActorId
        && previous.targetActorId === event.targetActorId
        && previous.amount === event.amount
      numbers.push({
        key: event.sequence,
        targetActorId: event.targetActorId,
        kind: critical ? 'crit' : 'damage',
        value: event.amount,
      })
    } else if (event.type === 'HealingApplied' && event.targetActorId) {
      numbers.push({
        key: event.sequence,
        targetActorId: event.targetActorId,
        kind: 'heal',
        value: event.amount,
      })
    } else if (event.type === 'Dodge' && event.targetActorId) {
      numbers.push({
        key: event.sequence,
        targetActorId: event.targetActorId,
        kind: 'miss',
        value: null,
      })
    }

    if (event.type === 'CriticalHit' || event.type === 'ResourceChanged') continue

    logEntries.push({
      key: event.sequence,
      side: resolveSide(event, context),
      text: eventText(event, context),
      occurredAtUtc: event.serverTimeUtc,
      eventType: event.type,
    })
  }

  return { numbers, logEntries }
}

function resolveSide(event: CombatEvent, context: BattleEventProjectionContext): BattleLogSide {
  const sourceActorId = event.sourceActorId ?? event.actorId
  if (sourceActorId === context.localActorId) return 'player'
  if (context.enemyActorIds.has(sourceActorId)) return 'enemy'
  if (context.actorNames.has(sourceActorId)) return 'ally'
  return 'system'
}

function eventText(event: CombatEvent, context: BattleEventProjectionContext): string {
  const sourceName = actorName(event.sourceActorId ?? event.actorId, context)
  const targetName = actorName(event.targetActorId ?? event.actorId, context)
  const abilityName = event.definitionId
    ? context.abilityNames.get(event.definitionId) ?? event.definitionId
    : 'способность'

  switch (event.type) {
    case 'DamageDealt': return `${sourceName}: ${Math.round(event.amount)} урона`
    case 'HealingApplied': return `${sourceName} восстанавливает ${targetName}: ${Math.round(event.amount)} здоровья`
    case 'Dodge': return `${targetName} уклоняется`
    case 'AbilityUsed': return `${sourceName} использует «${abilityName}»`
    case 'ActorJoined': return `${targetName} вступает в бой`
    case 'ActorLeft': return `${targetName} покидает бой`
    case 'TargetChanged': return `${sourceName} выбирает цель: ${targetName}`
    case 'ActorDied': return `${targetName} погибает`
    case 'EnemyKilled': return `${targetName} повержен`
    case 'CombatStarted': return 'Бой начался'
    case 'CombatEnded': return 'Бой завершён'
    case 'AbilityInterrupted': return `«${abilityName}» прервана`
    default: return event.definitionId ? `${sourceName}: ${abilityName}` : 'Событие боя'
  }
}

function actorName(actorId: string | null, context: BattleEventProjectionContext): string {
  if (!actorId) return 'Система'
  return context.actorNames.get(actorId) ?? 'Неизвестный участник'
}
