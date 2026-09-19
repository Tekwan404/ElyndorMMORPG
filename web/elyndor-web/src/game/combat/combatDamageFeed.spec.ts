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
      { sequence: 1, side: 'player', amount: 37, critical: false },
      { sequence: 2, side: 'enemy', amount: 91, critical: false },
    ])
    expect(result.latestSequence).toBe(3)
  })

  it('marks a damage event as critical when it follows the matching CriticalHit event', () => {
    const result = collectCombatDamageFeedHits([
      event(10, 'CriticalHit', 'enemy', 173, 'player'),
      event(11, 'DamageDealt', 'enemy', 173, 'player'),
    ], 0, 'player', 'enemy')

    expect(result.hits).toEqual([
      { sequence: 11, side: 'enemy', amount: 173, critical: true },
    ])
  })

  it('does not mark unrelated damage as critical', () => {
    const result = collectCombatDamageFeedHits([
      event(12, 'CriticalHit', 'enemy', 173, 'player'),
      event(13, 'DamageDealt', 'player', 42, 'enemy'),
    ], 0, 'player', 'enemy')

    expect(result.hits).toEqual([
      { sequence: 13, side: 'player', amount: 42, critical: false },
    ])
  })

  it('only returns events newer than the processed sequence cursor', () => {
    const result = collectCombatDamageFeedHits([
      event(20, 'DamageDealt', 'player', 12),
      event(21, 'DamageDealt', 'enemy', 28),
      event(22, 'HealingApplied', 'player', 50),
    ], 20, 'player', 'enemy')

    expect(result.hits).toEqual([
      { sequence: 21, side: 'enemy', amount: 28, critical: false },
    ])
    expect(result.latestSequence).toBe(22)
  })
})
