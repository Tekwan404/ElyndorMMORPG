import type { CombatEvent } from '@/api/contracts'

export type CombatNumberKind = 'damage' | 'crit' | 'heal' | 'miss'
export type BattleLogSide = 'player' | 'ally' | 'enemy' | 'system'

export interface CombatNumberPresentation {
  key: number
  targetActorId: string
  kind: CombatNumberKind
  value: number | null
  periodic?: boolean
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
      const critical =
        previous?.type === 'CriticalHit' &&
        previous.sourceActorId === event.sourceActorId &&
        previous.targetActorId === event.targetActorId &&
        previous.amount === event.amount
      numbers.push({
        key: event.sequence,
        targetActorId: event.targetActorId,
        kind: critical ? 'crit' : 'damage',
        value: event.amount,
        ...(event.isPeriodic === true ? { periodic: true } : {}),
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
      text: eventText(event, context, previous),
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

const effectNames: Record<string, string> = {
  'BERSERKER_DEATHS_EMBRACE_READY': 'Объятия смерти',
  'BERSERKER_RECKLESSNESS_INCOMING': 'Безрассудство: получаемый урон',
  'BERSERKER_RECKLESSNESS_OUTGOING': 'Безрассудство: наносимый урон',
  'BERSERKER_BLOOD_RAGE_ATTACK_SPEED': 'Кровавая ярость: скорость атаки',
  'BERSERKER_BLOOD_RAGE_ATTACK_POWER': 'Кровавая ярость: сила атаки',
  'BERSERKER_DEVASTATING_VULNERABILITY': 'Сокрушительная уязвимость',
  'WARLORD_PARTY_SHIELD': 'Групповой щит полководца',
  'WARLORD_INSPIRATION': 'Вдохновение полководца',
  'MAGE_MANA_SHIELD_EFFECT': 'Щит маны',
}

function effectName(definitionId: string | null, names: ReadonlyMap<string, string>): string {
  if (!definitionId?.trim()) return 'Неизвестный эффект'
  const named = names.get(definitionId)
  if (named?.trim()) return named
  return effectNames[definitionId.trim().toUpperCase().replace(/[^A-Z0-9]+/g, '_')]
    ?? 'Неизвестный эффект'
}

function eventText(
  event: CombatEvent,
  context: BattleEventProjectionContext,
  previous?: CombatEvent,
): string {
  const sourceName = actorName(event.sourceActorId ?? event.actorId, context)
  const targetName = actorName(event.targetActorId ?? event.actorId, context)
  const abilityName = event.definitionId
    ? (context.abilityNames.get(event.definitionId) ?? readableIdentifier(event.definitionId))
    : 'способность'

  switch (event.type) {
    case 'DamageDealt': {
      const isCrit = previous?.type === 'CriticalHit'
        && previous.sourceActorId === event.sourceActorId
        && previous.targetActorId === event.targetActorId
        && previous.amount === event.amount
      const skill = event.definitionId
        ? context.abilityNames.get(event.definitionId)
          ?? (event.definitionId === 'AUTO_ATTACK' ? 'Автоатака' : null)
        : null
      return `${sourceName} → ${targetName}: ${Math.round(event.amount)} урона${isCrit ? ' (КРИТ)' : ''}${event.isPeriodic ? ' (периодический)' : ''}${skill ? ` · ${skill}` : ''}`
    }
    case 'AutoAttackStarted':
      return `${sourceName} включает автоатаку`
    case 'AutoAttackStopped':
      return `${sourceName} останавливает автоатаку`
    case 'UnblockableHit':
      return `${sourceName}: неблокируемый удар`
    case 'DamageBlocked':
      return event.amountBeforeShields <= 0
        ? `${targetName}: полный блок`
        : `${targetName} блокирует ${Math.round(event.amount)} урона`
    case 'HealingApplied':
      return `${sourceName} восстанавливает ${targetName}: ${Math.round(event.amount)} здоровья`
    case 'Dodge':
      return `${targetName} уклоняется`
    case 'AbilityUsed':
      return `${sourceName} использует «${abilityName}»`
    case 'EffectApplied':
      return `${targetName}: наложен эффект «${effectName(event.definitionId, context.abilityNames)}»`
    case 'EffectRefreshed':
      return `${targetName}: обновлён эффект «${effectName(event.definitionId, context.abilityNames)}»`
    case 'EffectRemoved':
      return `${targetName}: завершён эффект «${effectName(event.definitionId, context.abilityNames)}»`
    case 'ShieldAbsorbed':
      return `${targetName}: щит поглощает ${Math.round(event.amount)} урона`
    case 'ConsumableUsed':
      return `${sourceName} использует «${abilityName}»`
    case 'TauntApplied':
      return `${sourceName} провоцирует ${targetName}`
    case 'ActorJoined':
      return `${targetName} вступает в бой`
    case 'ActorLeft':
      return `${targetName} покидает бой`
    case 'ActorSummoned':
      return `${sourceName} призывает ${targetName}`
    case 'TargetChanged':
      return `${sourceName} выбирает цель: ${targetName}`
    case 'ActorDied':
      return `${targetName} погибает`
    case 'ActorResurrected':
      return `${sourceName} воскрешает ${targetName}`
    case 'EnemyKilled':
      return `${targetName} повержен`
    case 'CombatStarted':
      return 'Бой начался'
    case 'CombatEnded':
      return event.definitionId === 'Victory'
        ? 'Победа'
        : event.definitionId === 'Defeat'
          ? 'Поражение'
          : event.definitionId === 'FLED'
            ? 'Вы покинули бой'
            : 'Бой завершён'
    case 'AbilityInterrupted':
      return `«${abilityName}» прервана`
    default:
      return 'Событие боя'
  }
}

function readableIdentifier(identifier: string): string {
  return identifier
    .toLocaleLowerCase('ru-RU')
    .replace(/_/g, ' ')
    .replace(/^./u, (first) => first.toLocaleUpperCase('ru-RU'))
}

function actorName(actorId: string | null, context: BattleEventProjectionContext): string {
  if (!actorId) return 'Система'
  return context.actorNames.get(actorId) ?? 'Неизвестный участник'
}
