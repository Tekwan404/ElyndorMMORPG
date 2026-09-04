import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('@microsoft/signalr', () => ({
  HubConnectionState: { Connected: 'Connected' },
  HttpTransportType: { LongPolling: 4 },
  LogLevel: { Warning: 3, Error: 4 },
  HubConnectionBuilder: class {
    withUrl() { return this }
    withAutomaticReconnect() { return this }
    configureLogging() { return this }
    build() {
      return {
        state: 'Connected',
        on: vi.fn<(...args: unknown[]) => void>(),
        onreconnecting: vi.fn<(...args: unknown[]) => void>(),
        onreconnected: vi.fn<(...args: unknown[]) => void>(),
        onclose: vi.fn<(...args: unknown[]) => void>(),
        start: vi.fn<() => Promise<void>>().mockResolvedValue(undefined),
        invoke: vi.fn<(...args: unknown[]) => Promise<unknown>>().mockResolvedValue({
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

  it('renders server-provided monster presentation directly from the combat snapshot', () => {
    const store = useCombatSessionStore()
    store.snapshot = {
      sessionId: crypto.randomUUID(), sequence: 4, status: 'Active',
      serverTimeUtc: '2026-09-01T12:00:00Z',
      player: actor('Player', 'WARRIOR', 'Warrior', 140, 180, 35, 100, [
        { id: 'STRIKE', resourceCost: 0, cooldownSeconds: 0 },
        { id: 'WILD_STRIKE', resourceCost: 25, cooldownSeconds: 6 },
      ]),
      enemy: actor('Monster', 'WOLF', 'Волк', 120, 180, 0, 0, [], 3, 'wolf'),
    }

    const wrapper = mount(CombatView)

    expect(wrapper.text()).toContain('Волк')
    expect(wrapper.text()).toContain('УР. 3')
    expect(wrapper.text()).toContain('Удар')
    expect(wrapper.text()).toContain('Дикий удар')
    expect(wrapper.text()).not.toContain('Вихрь')
    expect(wrapper.find('img[alt="Волк"]').exists()).toBe(true)
    expect(wrapper.findAll('[role="progressbar"]')).toHaveLength(3)
  })

  it('attributes monster damage to the server-provided monster name while player auto attack is disabled', () => {
    const store = useCombatSessionStore()
    const player = actor('Player', 'WARRIOR', 'Warrior', 128, 180, 5, 100, [
      { id: 'STRIKE', resourceCost: 0, cooldownSeconds: 0 },
    ])
    const enemy = actor('Monster', 'WOLF', 'Волк', 180, 180, 0, 0, [], 3, 'wolf')
    player.autoAttackEnabled = false
    store.snapshot = {
      sessionId: crypto.randomUUID(), sequence: 3, status: 'Active',
      serverTimeUtc: '2026-09-01T12:00:02Z', player, enemy,
    }
    store.events = [
      {
        sequence: 2,
        type: 'DamageDealt',
        actorId: player.actorId,
        sourceActorId: enemy.actorId,
        targetActorId: player.actorId,
        definitionId: 'AUTO_ATTACK',
        amount: 12,
        amountBeforeShields: 12,
        serverTimeUtc: '2026-09-01T12:00:02Z',
      },
    ]

    const wrapper = mount(CombatView)
    const row = wrapper.get('.combat-log li')
    expect(row.attributes('data-side')).toBe('enemy')
    expect(row.text()).toContain('ВОЛК')
    expect(row.text()).toContain('12 урона')
    expect(row.text()).not.toContain('ВЫ')
    expect(wrapper.text()).toContain('Включить автоатаку')
  })
})

function actor(
  kind: 'Player' | 'Monster', definitionId: string, name: string,
  hp: number, maxHp: number, resource: number, maxResource: number,
  abilities: {
    id: string
    resourceCost: number
    cooldownSeconds: number
    displayName?: string
  }[],
  level?: number,
  artId?: string | null,
) {
  return {
    actorId: crypto.randomUUID(), kind, definitionId, name, hp, maxHp,
    resourceType: kind === 'Player' ? 'RAGE' : 'NONE', resource, maxResource,
    autoAttackEnabled: false, cooldowns: {},
    knownAbilityIds: abilities.map((ability) => ability.id),
    abilities: abilities.map((ability) => ({
      ...ability,
      displayName: ability.displayName
        ?? (ability.id === 'STRIKE' ? 'Удар' : ability.id === 'WILD_STRIKE' ? 'Дикий удар' : ability.id),
      description: 'Server-provided ability presentation.',
      iconId: null,
    })),
    effects: [],
    level, artId,
  }
}
