import { describe, expect, it } from 'vitest'

import type { CombatEvent } from '@/api/contracts'
import { projectBattleEvents } from './battleEventPresentation'

function event(sequence: number, type: string, overrides: Partial<CombatEvent> = {}): CombatEvent {
  return {
    sequence,
    type,
    actorId: overrides.targetActorId ?? 'enemy',
    sourceActorId: 'local',
    targetActorId: 'enemy',
    definitionId: 'STRIKE',
    amount: 42,
    amountBeforeShields: 42,
    serverTimeUtc: `2026-09-25T12:00:0${sequence}Z`,
    ...overrides,
  }
}

describe('projectBattleEvents', () => {
  it('projects server outcomes into actor-bound combat numbers without recalculation', () => {
    const events = [
      event(1, 'DamageDealt'),
      event(2, 'CriticalHit', { amount: 91 }),
      event(3, 'DamageDealt', { amount: 91 }),
      event(4, 'HealingApplied', { targetActorId: 'ally', actorId: 'ally', amount: 35 }),
      event(5, 'Dodge', { amount: 0 }),
    ]

    const result = projectBattleEvents(events, {
      actorNames: new Map([
        ['local', 'Текван'],
        ['ally', 'Мира'],
        ['enemy', 'Жрец отражений'],
      ]),
      abilityNames: new Map([['STRIKE', 'Удар']]),
      enemyActorIds: new Set(['enemy']),
      localActorId: 'local',
    })

    expect(result.numbers).toEqual([
      { key: 1, targetActorId: 'enemy', kind: 'damage', value: 42 },
      { key: 3, targetActorId: 'enemy', kind: 'crit', value: 91 },
      { key: 4, targetActorId: 'ally', kind: 'heal', value: 35 },
      { key: 5, targetActorId: 'enemy', kind: 'miss', value: null },
    ])
  })

  it('keeps join, leave, skill and aggro events in the readable log', () => {
    const result = projectBattleEvents(
      [
        event(1, 'AbilityUsed'),
        event(2, 'ActorJoined', { actorId: 'ally', targetActorId: 'ally' }),
        event(3, 'TargetChanged', { sourceActorId: 'enemy', targetActorId: 'ally' }),
        event(4, 'ActorLeft', { actorId: 'ally', targetActorId: 'ally' }),
      ],
      {
        actorNames: new Map([
          ['local', 'Текван'],
          ['ally', 'Мира'],
          ['enemy', 'Жрец отражений'],
        ]),
        abilityNames: new Map([['STRIKE', 'Удар']]),
        enemyActorIds: new Set(['enemy']),
        localActorId: 'local',
      },
    )

    expect(result.logEntries.map((entry) => entry.text)).toEqual([
      'Текван использует «Удар»',
      'Мира вступает в бой',
      'Жрец отражений выбирает цель: Мира',
      'Мира покидает бой',
    ])
  })
})
