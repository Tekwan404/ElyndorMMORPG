import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { describe, expect, it } from 'vitest'

import type { CombatActorSnapshot } from '@/api/contracts'
import CombatAllyRoster from '@/game/combat/CombatAllyRoster.vue'

function actor(overrides: Partial<CombatActorSnapshot> = {}): CombatActorSnapshot {
  return {
    actorId: 'player-1',
    kind: 'Player',
    definitionId: 'PALADIN',
    name: 'tekwan',
    hp: 900,
    maxHp: 1000,
    resourceType: 'MANA',
    resource: 80,
    maxResource: 100,
    autoAttackEnabled: false,
    cooldowns: {},
    knownAbilityIds: [],
    abilities: [],
    effects: [],
    level: 30,
    genderId: 'MALE',
    ...overrides,
  }
}

function mountRoster(options: {
  allies?: CombatActorSnapshot[]
  selected?: string | null
  aggroed?: string[]
  status?: Record<string, string>
  selectable?: Record<string, boolean>
} = {}) {
  const allies = options.allies ?? [
    actor(),
    actor({ actorId: 'ally-2', definitionId: 'MAGE', name: 'Lina', genderId: 'FEMALE', hp: 420, maxHp: 700 }),
  ]

  return mount(CombatAllyRoster, {
    props: {
      allies,
      playerActorId: 'player-1',
      aggroedActorIds: options.aggroed ?? [],
      selectedFriendlyTargetActorId: options.selected ?? null,
      participantStatus: (actorId: string) => options.status?.[actorId] ?? 'В бою',
      canSelect: (actorId: string) => options.selectable?.[actorId] ?? true,
      participantGlyph: () => 'star',
      roleLabel: (value: CombatActorSnapshot) => value.definitionId === 'MAGE' ? 'Маг' : 'Паладин',
      healthRatio: (value: CombatActorSnapshot) => value.maxHp > 0 ? value.hp / value.maxHp * 100 : 0,
    },
    global: {
      plugins: [createPinia()],
      stubs: {
        IconGenerator: { template: '<span data-icon />' },
      },
    },
  })
}

describe('CombatAllyRoster', () => {
  it('marks the selected ally and the ally currently holding aggro', () => {
    const wrapper = mountRoster({ selected: 'ally-2', aggroed: ['ally-2'] })
    const ally = wrapper.get('button[aria-label^="Lina,"]')

    expect(ally.classes()).toContain('combat-ally-roster__member--selected')
    expect(ally.classes()).toContain('combat-ally-roster__member--aggro')
    expect(ally.attributes('aria-pressed')).toBe('true')
    expect(ally.attributes('aria-label')).toContain('выбранная дружеская цель')
    expect(ally.attributes('aria-label')).toContain('противник атакует этого союзника')
    expect(ally.text()).toContain('ЦЕЛЬ')
    expect(ally.text()).toContain('ПОД АТАКОЙ')
  })

  it('emits the ally actor id when a support target is selected', async () => {
    const wrapper = mountRoster()
    const ally = wrapper.get('button[aria-label^="Lina,"]')

    await ally.trigger('click')

    expect(wrapper.emitted('select')).toEqual([['ally-2']])
  })

  it('disables a dead ally even if the participant is otherwise selectable', () => {
    const dead = actor({ actorId: 'ally-dead', name: 'Fallen', hp: 0 })
    const wrapper = mountRoster({
      allies: [actor(), dead],
      status: { 'ally-dead': 'Пал' },
    })
    const ally = wrapper.get('button[aria-label^="Fallen,"]')

    expect(ally.attributes()).toHaveProperty('disabled')
    expect(ally.attributes('data-selectable')).toBe('false')
    expect(ally.attributes('aria-label')).toContain('состояние: Пал')
  })

  it('disables a living ally whose combat participant state is no longer active', () => {
    const wrapper = mountRoster({
      selectable: { 'ally-2': false },
      status: { 'ally-2': 'Сбежал' },
    })
    const ally = wrapper.get('button[aria-label^="Lina,"]')

    expect(ally.attributes()).toHaveProperty('disabled')
    expect(ally.attributes('data-selectable')).toBe('false')
    expect(ally.attributes('aria-label')).toContain('состояние: Сбежал')
  })
})
