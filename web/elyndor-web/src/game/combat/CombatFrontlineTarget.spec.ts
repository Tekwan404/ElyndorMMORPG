import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import type { CombatActorSnapshot, CombatSnapshot } from '@/api/contracts'
import CombatFrontlineTarget from '@/game/combat/CombatFrontlineTarget.vue'
import { useCombatSessionStore } from '@/stores/combatSession'

function actor(actorId: string, name: string, kind: CombatActorSnapshot['kind']): CombatActorSnapshot {
  return {
    actorId,
    kind,
    definitionId: actorId,
    name,
    hp: 100,
    maxHp: 100,
    resourceType: 'NONE',
    resource: 0,
    maxResource: 0,
    autoAttackEnabled: false,
    cooldowns: {},
    knownAbilityIds: [],
    abilities: [],
    effects: [],
  }
}

const ally = actor('player-1', 'tekwan', 'Player')

function activeSnapshot(): CombatSnapshot {
  return {
    sessionId: 'session-1',
    sequence: 1,
    status: 'Active',
    serverTimeUtc: '2026-09-23T15:00:00Z',
    contentVersion: 'test-content',
    balanceVersion: 'test-balance',
    player: ally,
    enemy: actor('enemy-1', 'Враг', 'Monster'),
    selectedTargetActorId: 'enemy-1',
  }
}

function createTarget(): HTMLElement {
  const target = document.createElement('section')
  target.className = 'combat-hud__actor--enemy'
  document.body.appendChild(target)
  return target
}

describe('CombatFrontlineTarget', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
    document.body.innerHTML = ''
  })

  it('shows current aggro target and relative threat beside the enemy HUD', () => {
    const combat = useCombatSessionStore()
    vi.spyOn(combat, 'refreshCombatTelemetry').mockResolvedValue()
    combat.snapshot = activeSnapshot()
    combat.threat = {
      enemyActorId: 'enemy-1',
      enemyName: 'Враг',
      currentTargetActorId: 'player-1',
      forcedTargetActorId: null,
      entries: [
        { actorId: 'player-1', name: 'tekwan', threat: 240, isCurrentTarget: true },
        { actorId: 'player-2', name: 'ally', threat: 120, isCurrentTarget: false },
      ],
    }
    const target = createTarget()

    const wrapper = mount(CombatFrontlineTarget, {
      props: { ally },
      global: {
        stubs: {
          IconGenerator: { template: '<span data-icon />' },
        },
      },
    })

    expect(target.textContent).toContain('ЦЕЛЬ')
    expect(target.textContent).toContain('tekwan')
    expect(target.textContent).toContain('100%')
    wrapper.unmount()
  })

  it('does not show a stale threat percentage from another enemy', () => {
    const combat = useCombatSessionStore()
    vi.spyOn(combat, 'refreshCombatTelemetry').mockResolvedValue()
    combat.snapshot = activeSnapshot()
    combat.threat = {
      enemyActorId: 'enemy-2',
      enemyName: 'Другой враг',
      currentTargetActorId: 'player-1',
      forcedTargetActorId: null,
      entries: [
        { actorId: 'player-1', name: 'tekwan', threat: 240, isCurrentTarget: true },
      ],
    }
    const target = createTarget()

    const wrapper = mount(CombatFrontlineTarget, {
      props: { ally },
      global: {
        stubs: {
          IconGenerator: { template: '<span data-icon />' },
        },
      },
    })

    expect(target.textContent).toContain('tekwan')
    expect(target.textContent).not.toContain('%')
    wrapper.unmount()
  })
})
