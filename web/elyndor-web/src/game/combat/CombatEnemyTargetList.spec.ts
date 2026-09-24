import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import type { CombatActorSnapshot } from '@/api/contracts'
import CombatEnemyTargetList from '@/game/combat/CombatEnemyTargetList.vue'

function enemy(overrides: Partial<CombatActorSnapshot> = {}): CombatActorSnapshot {
  return {
    actorId: 'enemy-1',
    kind: 'Monster',
    definitionId: 'TEST_WOLF',
    name: 'Серый волк',
    hp: 700,
    maxHp: 1000,
    resourceType: 'NONE',
    resource: 0,
    maxResource: 0,
    autoAttackEnabled: true,
    cooldowns: {},
    knownAbilityIds: [],
    abilities: [],
    effects: [],
    currentAggroTargetActorId: 'player-1',
    ...overrides,
  }
}

function mountTargets(options: {
  enemies?: CombatActorSnapshot[]
  selected?: string
  disabled?: boolean
} = {}) {
  const enemies = options.enemies ?? [
    enemy(),
    enemy({ actorId: 'enemy-2', definitionId: 'TEST_MAGE', name: 'Культист', hp: 320, maxHp: 500, resourceType: 'MANA', resource: 25, maxResource: 100, currentAggroTargetActorId: 'ally-2' }),
  ]

  return mount(CombatEnemyTargetList, {
    props: {
      enemies,
      selectedTargetActorId: options.selected ?? 'enemy-1',
      disabled: options.disabled ?? false,
      healthRatio: (value: CombatActorSnapshot) => value.maxHp > 0 ? value.hp / value.maxHp * 100 : 0,
      aggroName: (value: CombatActorSnapshot) => value.currentAggroTargetActorId === 'ally-2' ? 'Lina' : 'tekwan',
    },
    global: {
      stubs: {
        IconGenerator: { template: '<span data-icon />' },
      },
    },
  })
}

describe('CombatEnemyTargetList', () => {
  it('exposes the selected enemy as a pressed target button', () => {
    const wrapper = mountTargets()
    const selected = wrapper.get('[data-target-actor-id="enemy-1"]')
    const other = wrapper.get('[data-target-actor-id="enemy-2"]')

    expect(selected.attributes('aria-pressed')).toBe('true')
    expect(selected.attributes('data-selected')).toBe('true')
    expect(selected.text()).toContain('ЦЕЛЬ')
    expect(other.attributes('aria-pressed')).toBe('false')
  })

  it('announces and exposes the current aggro target', () => {
    const wrapper = mountTargets({ selected: 'enemy-2' })
    const target = wrapper.get('[data-target-actor-id="enemy-2"]')

    expect(target.attributes('data-aggro-target-actor-id')).toBe('ally-2')
    expect(target.attributes('aria-label')).toContain('атакует: Lina')
    expect(target.text()).toContain('Атакует: Lina')
  })

  it('shows a visible resource for enemies that use one', () => {
    const wrapper = mountTargets()
    const target = wrapper.get('[data-target-actor-id="enemy-2"]')

    expect(target.text()).toContain('Мана · 25 / 100')
    expect(target.attributes('aria-label')).toContain('мана 25 из 100')
  })

  it('emits the selected enemy actor id', async () => {
    const wrapper = mountTargets()

    await wrapper.get('[data-target-actor-id="enemy-2"]').trigger('click')

    expect(wrapper.emitted('select')).toEqual([['enemy-2']])
  })

  it('disables target switching while a combat command is pending', () => {
    const wrapper = mountTargets({ disabled: true })

    for (const button of wrapper.findAll('[data-target-actor-id]')) {
      expect(button.attributes()).toHaveProperty('disabled')
    }
  })

  it('still renders a single enemy when its resource state needs to stay visible', () => {
    const wrapper = mountTargets({
      enemies: [enemy({ resourceType: 'RAGE', resource: 40, maxResource: 100 })],
    })

    expect(wrapper.find('[data-combat-targets]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Ярость · 40 / 100')
  })
})
