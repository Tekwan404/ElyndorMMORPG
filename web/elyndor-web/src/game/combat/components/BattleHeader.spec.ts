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

  it('allows selecting a corpse only when the local actor has resurrection', async () => {
    const local = actor('local', 'Player')
    const ally = { ...actor('ally', 'Player'), hp: 0 }
    const wrapper = mount(BattleHeader, { props: {
      localActor: local, enemy: actor('enemy', 'Monster'), allies: [local, ally],
      selectedFriendlyActorId: 'local', aggroActorIds: [], disabled: false,
    } })
    expect(wrapper.get('[data-ally-strip-actor="ally"]').attributes('disabled')).toBeDefined()
    const paladin = { ...local, abilities: [{ id: 'RESURRECTION', targetType: 'SingleDeadAlly' }] } as CombatActorSnapshot
    await wrapper.setProps({ localActor: paladin, allies: [paladin, ally] })
    expect(wrapper.get('[data-ally-strip-actor="ally"]').attributes('disabled')).toBeUndefined()
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

  it('shows the opposing player resource without a monster portrait', () => {
    const local = actor('local', 'Player')
    const enemy = { ...actor('enemy', 'Player'), resourceType: 'MANA', resource: 42, maxResource: 120 }
    const wrapper = mount(BattleHeader, {
      props: {
        localActor: local,
        enemy,
        allies: [local],
        selectedFriendlyActorId: 'local',
        aggroActorIds: [],
        disabled: false,
      },
    })

    expect(wrapper.get('[data-enemy-resource]').attributes('data-resource')).toBe('MANA')
    expect(wrapper.text()).toContain('42 / 120')
    expect(wrapper.find('.battle-header__enemy-portrait').exists()).toBe(false)
  })

  it('marks low and critical health bars for urgency styling', () => {
    const local = { ...actor('local', 'Player'), hp: 20 }
    const enemy = { ...actor('enemy', 'Monster'), hp: 45 }
    const wrapper = mount(BattleHeader, {
      props: {
        localActor: local,
        enemy,
        allies: [local],
        selectedFriendlyActorId: 'local',
        aggroActorIds: [],
        disabled: false,
      },
    })

    const healthBars = wrapper.findAll('.battle-header__bar--health')
    expect(healthBars[0]?.attributes('data-health-state')).toBe('critical')
    expect(healthBars[1]?.attributes('data-health-state')).toBe('low')
  })
})
