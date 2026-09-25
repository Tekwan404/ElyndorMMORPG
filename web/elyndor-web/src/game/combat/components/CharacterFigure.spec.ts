import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'

import type { CombatActorSnapshot } from '@/api/contracts'
import CharacterFigure from './CharacterFigure.vue'

const actor: CombatActorSnapshot = {
  actorId: 'ally',
  kind: 'Player',
  definitionId: 'MAGE',
  name: 'Мира',
  hp: 75,
  maxHp: 100,
  resourceType: 'MANA',
  resource: 60,
  maxResource: 100,
  autoAttackEnabled: false,
  cooldowns: {},
  knownAbilityIds: [],
  abilities: [],
  effects: [],
  genderId: 'FEMALE',
}

describe('CharacterFigure', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('renders selected and aggro as two states on one actor', () => {
    const wrapper = mount(CharacterFigure, {
      props: {
        actor,
        selected: true,
        aggro: true,
        local: false,
        disabled: false,
        frontline: true,
      },
    })

    expect(wrapper.findAll('[data-character-figure="ally"]')).toHaveLength(1)
    expect(wrapper.get('[data-character-figure="ally"]').attributes('data-selected')).toBe('true')
    expect(wrapper.get('[data-character-figure="ally"]').attributes('data-aggro')).toBe('true')
    expect(wrapper.findAll('[data-target-selection]')).toHaveLength(1)
    expect(wrapper.findAll('[data-aggro-indicator]')).toHaveLength(1)
  })

  it('emits the actor id from a touch-sized button', async () => {
    const wrapper = mount(CharacterFigure, {
      props: {
        actor,
        selected: false,
        aggro: false,
        local: false,
        disabled: false,
        frontline: false,
      },
    })

    await wrapper.get('button').trigger('click')

    expect(wrapper.emitted('select')).toEqual([['ally']])
    expect(wrapper.get('button').attributes('aria-label')).toContain('Мира')
  })

  it('loads every visible actor eagerly while prioritizing frontline responsive art', async () => {
    const wrapper = mount(CharacterFigure, {
      props: {
        actor,
        selected: false,
        aggro: false,
        local: false,
        disabled: false,
        frontline: true,
      },
    })

    expect(wrapper.get('img').attributes('loading')).toBe('eager')
    expect(wrapper.get('img').attributes('fetchpriority')).toBe('high')
    expect(wrapper.get('img').attributes('sizes')).toContain('390px')

    await wrapper.setProps({ frontline: false })
    expect(wrapper.get('img').attributes('loading')).toBe('eager')
    expect(wrapper.get('img').attributes('fetchpriority')).toBe('low')
  })

  it('marks a dead actor unavailable without hiding its identity', () => {
    const wrapper = mount(CharacterFigure, {
      props: {
        actor: { ...actor, hp: 0 },
        selected: false,
        aggro: false,
        local: false,
        disabled: true,
        frontline: false,
      },
    })

    expect(wrapper.get('button').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-character-figure="ally"]').attributes('data-dead')).toBe('true')
    expect(wrapper.text()).toContain('Мира')
  })
})
