import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'

import type { CombatActorSnapshot } from '@/api/contracts'
import BattleHeader from './BattleHeader.vue'

function actor(id: string, kind: CombatActorSnapshot['kind']): CombatActorSnapshot {
  return {
    actorId: id,
    kind,
    definitionId: kind === 'Monster' ? 'WOLF' : 'WARRIOR',
    name: id,
    hp: 80,
    maxHp: 100,
    resourceType: 'RAGE',
    resource: 35,
    maxResource: 100,
    autoAttackEnabled: false,
    cooldowns: {},
    knownAbilityIds: [],
    abilities: [],
    effects: [],
    level: 30,
  }
}

describe('BattleHeader', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('emits the same friendly actor identity selected by the roster', async () => {
    const local = actor('local', 'Player')
    const ally = actor('ally', 'Player')
    const wrapper = mount(BattleHeader, {
      props: {
        localActor: local,
        enemy: actor('enemy', 'Monster'),
        allies: [local, ally],
        selectedFriendlyActorId: 'local',
        aggroActorIds: ['ally'],
        disabled: false,
      },
    })

    await wrapper.get('[data-ally-strip-actor="ally"]').trigger('click')

    expect(wrapper.emitted('selectFriendly')).toEqual([['ally']])
  })

  it('shows selected and aggro states independently in the strip', () => {
    const local = actor('local', 'Player')
    const ally = actor('ally', 'Player')
    const wrapper = mount(BattleHeader, {
      props: {
        localActor: local,
        enemy: actor('enemy', 'Monster'),
        allies: [local, ally],
        selectedFriendlyActorId: 'local',
        aggroActorIds: ['ally'],
        disabled: false,
      },
    })

    expect(wrapper.get('[data-ally-strip-actor="local"]').attributes('data-selected')).toBe('true')
    expect(wrapper.get('[data-ally-strip-actor="local"]').attributes('data-aggro')).toBe('false')
    expect(wrapper.get('[data-ally-strip-actor="ally"]').attributes('data-selected')).toBe('false')
    expect(wrapper.get('[data-ally-strip-actor="ally"]').attributes('data-aggro')).toBe('true')
  })
})
