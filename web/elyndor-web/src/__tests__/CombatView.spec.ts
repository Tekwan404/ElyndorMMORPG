import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('@microsoft/signalr', () => ({
  HubConnectionState: { Connected: 'Connected' },
  LogLevel: { Warning: 3, Error: 4 },
  HubConnectionBuilder: class {
    withUrl() { return this }
    withAutomaticReconnect() { return this }
    configureLogging() { return this }
    build() {
      return {
        state: 'Connected',
        on: vi.fn(),
        onreconnecting: vi.fn(),
        onreconnected: vi.fn(),
        onclose: vi.fn(),
        start: vi.fn().mockResolvedValue(undefined),
        invoke: vi.fn().mockResolvedValue({
          succeeded: false, errorCode: 'combat_not_found', snapshot: null, events: [],
        }),
      }
    }
  },
}))

import CombatView from '@/game/combat/views/CombatView.vue'
import { useCombatSessionStore } from '@/stores/combatSession'

describe('CombatView', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('renders only server-provided abilities and authoritative vitals', () => {
    const store = useCombatSessionStore()
    store.snapshot = {
      sessionId: crypto.randomUUID(), sequence: 4, status: 'Active',
      serverTimeUtc: '2026-09-01T12:00:00Z',
      player: actor('Player', 'WARRIOR', 'Warrior', 140, 180, 35, 100, [
        { id: 'STRIKE', resourceCost: 0, cooldownSeconds: 0 },
        { id: 'WILD_STRIKE', resourceCost: 25, cooldownSeconds: 6 },
      ]),
      enemy: actor('Monster', 'WOLF', 'Forest Wolf', 120, 180, 0, 0, []),
    }

    const wrapper = mount(CombatView)

    expect(wrapper.text()).toContain('Forest Wolf')
    expect(wrapper.text()).toContain('STRIKE')
    expect(wrapper.text()).toContain('WILD STRIKE')
    expect(wrapper.text()).not.toContain('WHIRLWIND')
    expect(wrapper.findAll('[role="progressbar"]')).toHaveLength(3)
  })
})

function actor(
  kind: 'Player' | 'Monster', definitionId: string, name: string,
  hp: number, maxHp: number, resource: number, maxResource: number,
  abilities: { id: string; resourceCost: number; cooldownSeconds: number }[],
) {
  return {
    actorId: crypto.randomUUID(), kind, definitionId, name, hp, maxHp,
    resourceType: kind === 'Player' ? 'RAGE' : 'NONE', resource, maxResource,
    autoAttackEnabled: false, cooldowns: {},
    knownAbilityIds: abilities.map((ability) => ability.id), abilities, effects: [],
  }
}
