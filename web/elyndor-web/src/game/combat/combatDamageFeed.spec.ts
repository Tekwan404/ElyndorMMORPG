import { describe, expect, it } from 'vitest'

import type { CombatEvent } from '@/api/contracts'
import { collectCombatDamageFeedHits } from '@/game/combat/combatDamageFeed'

function event(
  sequence: number,
  type: string,
  targetActorId: string | null,
  amount: number,
  sourceActorId = 'actor-source',
): CombatEvent {
  return {
    sequence,
    type,
    actorId: sourceActorId,
    sourceActorId,
    targetActorId,
    definitionId: null,
    amount,
    amountBeforeShields: amount,
    serverTimeUtc: '2026-09-19T16:28:00Z',
  }
}

describe('collectCombatDamageFeedHits', () => {
  it('separates damage received by the player from damage received by the selected enemy', () => {
    const result = collectCombatDamageFeedHits([
      event(1, 'DamageDealt', 'player', 37),
      event(2, 'DamageDealt', 'enemy', 91),
      event(3, 'DamageDealt', 'other-enemy', 14),
    ], 0, 'player', 'enemy')

    expect(result.hits).toEqual([
      { sequence: 1, side: 'player', kind: 'damage', amount: 37, critical: false },
      { sequence: 2, side: 'enemy', kind: 'damage', amount: 91, critical: false },
    ])
    expect(result.latestSequence).toBe(3)
  })

  it('marks a damage event as critical when it follows the matching CriticalHit event', () => {
    const result = collectCombatDamageFeedHits([
      event(10, 'CriticalHit', 'enemy', 173, 'player'),
      event(11, 'DamageDealt', 'enemy', 173, 'player'),
    ], 0, 'player', 'enemy')

    expect(result.hits).toEqual([
      { sequence: 11, side: 'enemy', kind: 'damage', amount: 173, critical: true },
    ])
  })

  it('does not mark unrelated damage as critical', () => {
    const result = collectCombatDamageFeedHits([
      event(12, 'CriticalHit', 'enemy', 173, 'player'),
      event(13, 'DamageDealt', 'player', 42, 'enemy'),
    ], 0, 'player', 'enemy')

    expect(result.hits).toEqual([
      { sequence: 13, side: 'player', kind: 'damage', amount: 42, critical: false },
    ])
  })

  it('uses the same floating feedback stream for healing', () => {
    const result = collectCombatDamageFeedHits([
      event(20, 'DamageDealt', 'player', 12),
      event(21, 'HealingApplied', 'player', 50, 'player'),
      event(22, 'HealingApplied', 'enemy', 18, 'enemy'),
    ], 20, 'player', 'enemy')

    expect(result.hits).toEqual([
      { sequence: 21, side: 'player', kind: 'healing', amount: 50, critical: false },
      { sequence: 22, side: 'enemy', kind: 'healing', amount: 18, critical: false },
    ])
    expect(result.latestSequence).toBe(22)
  })

  it('only returns supported events newer than the processed sequence cursor', () => {
    const result = collectCombatDamageFeedHits([
      event(30, 'DamageDealt', 'player', 12),
      event(31, 'DamageDealt', 'enemy', 28),
      event(32, 'ResourceChanged', 'player', 50),
    ], 30, 'player', 'enemy')

    expect(result.hits).toEqual([
      { sequence: 31, side: 'enemy', kind: 'damage', amount: 28, critical: false },
    ])
    expect(result.latestSequence).toBe(32)
  })
})
