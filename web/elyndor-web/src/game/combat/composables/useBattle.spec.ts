import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('@microsoft/signalr', () => ({
  HubConnectionState: { Connected: 'Connected' },
  LogLevel: { Warning: 3, Error: 4 },
  HubConnectionBuilder: class {},
}))

import type { CombatActorSnapshot, CombatSnapshot } from '@/api/contracts'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useBattle } from './useBattle'

function actor(actorId: string, kind: CombatActorSnapshot['kind'], hp = 100): CombatActorSnapshot {
  return {
    actorId,
    kind,
    definitionId: kind === 'Monster' ? 'WOLF' : 'WARRIOR',
    name: actorId,
    hp,
    maxHp: 100,
    resourceType: 'RAGE',
    resource: 0,
    maxResource: 100,
    autoAttackEnabled: false,
    cooldowns: {},
    knownAbilityIds: [],
    abilities: [],
    effects: [],
  }
}

function snapshot(): CombatSnapshot {
  const local = actor('local', 'Player')
  const ally = actor('ally', 'Player')
  const enemy = {
    ...actor('enemy', 'Monster'),
    currentAggroTargetActorId: 'local',
  }
  return {
    sessionId: 'session',
    sequence: 1,
    status: 'Active',
    serverTimeUtc: '2026-09-25T12:00:00Z',
    contentVersion: 'test',
    balanceVersion: 'test',
    player: local,
    players: [local, ally, local],
    enemy,
    enemies: [enemy],
    selectedTargetActorId: 'enemy',
    companion: actor('companion', 'Companion'),
  }
}

describe('useBattle', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('deduplicates party actors and keeps companions outside party formation', () => {
    const store = useCombatSessionStore()
    store.snapshot = snapshot()

    const battle = useBattle()

    expect(battle.allies.value.map((candidate) => candidate.actorId)).toEqual(['local', 'ally'])
    expect(battle.companion.value?.actorId).toBe('companion')
  })

  it('keeps friendly selection independent when enemy aggro changes', () => {
    const store = useCombatSessionStore()
    store.snapshot = snapshot()
    store.selectFriendlyTarget('ally')
    const battle = useBattle()

    store.snapshot = {
      ...store.snapshot!,
      sequence: 2,
      enemy: { ...store.snapshot!.enemy, currentAggroTargetActorId: 'ally' },
      enemies: [{ ...store.snapshot!.enemy, currentAggroTargetActorId: 'ally' }],
    }

    expect(battle.selectedFriendlyActorId.value).toBe('ally')
    expect(battle.aggroActorIds.value).toEqual(['ally'])
  })

  it('uses one selection intent for the roster and battlefield', () => {
    const store = useCombatSessionStore()
    store.snapshot = snapshot()
    const battle = useBattle()

    battle.selectFriendlyActor('ally')

    expect(store.selectedFriendlyTargetActorId).toBe('ally')
    expect(battle.selectedFriendlyActor.value?.actorId).toBe('ally')
  })
})
