import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'

import type { CombatActorSnapshot } from '@/api/contracts'
import type { CombatNumberPresentation } from '@/game/combat/battleEventPresentation'
import BattleArena from './BattleArena.vue'

function actor(id: string, kind: CombatActorSnapshot['kind']): CombatActorSnapshot {
  return {
    actorId: id,
    kind,
    definitionId: kind === 'Monster' ? 'WOLF' : 'WARRIOR',
    name: id,
    hp: 80,
    maxHp: 100,
    resourceType: 'RAGE',
    resource: 20,
    maxResource: 100,
    autoAttackEnabled: false,
    cooldowns: {},
    knownAbilityIds: [],
    abilities: [],
    effects: [],
    monsterRank: kind === 'Monster' ? 'Normal' : null,
  }
}

function mountArena(allies: CombatActorSnapshot[], numbers: CombatNumberPresentation[] = []) {
  const enemy = actor('enemy', 'Monster')
  return mount(BattleArena, {
    props: {
      allies,
      enemies: [enemy],
      localActorId: 'local',
      selectedFriendlyActorId: 'ally-1',
      selectedEnemyActorId: 'enemy',
      aggroActorIds: ['ally-1'],
      numbers,
      battlefieldArt: '/battlefield.webp',
      disabled: false,
      layoutWidthPx: 390,
    },
  })
}

describe('BattleArena', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it.each([1, 3, 5])('renders %i unique friendly actors in one formation', (count) => {
    const allies = [actor('local', 'Player')]
    for (let index = 1; index < count; index += 1) allies.push(actor(`ally-${index}`, 'Player'))

    const wrapper = mountArena(allies)

    const renderedIds = wrapper.findAll('[data-character-figure]').map((node) => node.attributes('data-character-figure'))
    expect(renderedIds).toHaveLength(count)
    expect(new Set(renderedIds).size).toBe(count)
  })

  it('renders one actor when aggro and selected target are the same', () => {
    const wrapper = mountArena([actor('local', 'Player'), actor('ally-1', 'Player')])

    expect(wrapper.findAll('[data-character-figure="ally-1"]')).toHaveLength(1)
    expect(wrapper.get('[data-character-figure="ally-1"]').attributes('data-selected')).toBe('true')
    expect(wrapper.get('[data-character-figure="ally-1"]').attributes('data-aggro')).toBe('true')
  })

  it('anchors server-provided numbers to their target actors', () => {
    const wrapper = mountArena(
      [actor('local', 'Player'), actor('ally-1', 'Player')],
      [
        { key: 1, targetActorId: 'ally-1', kind: 'heal', value: 44 },
        { key: 2, targetActorId: 'enemy', kind: 'crit', value: 91 },
      ],
    )

    expect(wrapper.get('[data-combat-numbers-for="ally-1"]').text()).toContain('+44')
    expect(wrapper.get('[data-combat-numbers-for="enemy"]').text()).toContain('-91')
  })
})
