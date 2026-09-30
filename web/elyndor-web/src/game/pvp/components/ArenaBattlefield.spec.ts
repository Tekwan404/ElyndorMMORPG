import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'

import type { CombatActorSnapshot, CombatSnapshot } from '@/api/contracts'
import ArenaBattlefield from './ArenaBattlefield.vue'

function actor(
  actorId: string,
  name: string,
  definitionId: string,
  genderId: 'MALE' | 'FEMALE',
): CombatActorSnapshot {
  return {
    actorId,
    kind: 'Player',
    definitionId,
    name,
    hp: 900,
    maxHp: 1_000,
    resourceType: definitionId === 'MAGE' ? 'MANA' : 'RAGE',
    resource: 80,
    maxResource: 100,
    autoAttackEnabled: true,
    cooldowns: {},
    knownAbilityIds: ['STRIKE', 'BASTION'],
    abilities: [
      {
        id: 'STRIKE',
        displayName: 'Удар',
        description: 'Удар по противнику.',
        iconId: null,
        resourceCost: 0,
        cooldownSeconds: 0,
        targetType: 'SingleEnemy',
      },
      {
        id: 'BASTION',
        displayName: 'Бастион',
        description: 'Защитный эффект на себя.',
        iconId: null,
        resourceCost: 0,
        cooldownSeconds: 0,
        targetType: 'Self',
      },
    ],
    effects: [],
    level: 30,
    genderId,
    skinId: null,
  }
}

function battle(): CombatSnapshot {
  return {
    sessionId: 'arena-match',
    sequence: 1,
    status: 'Active',
    serverTimeUtc: '2026-09-30T18:00:00Z',
    contentVersion: 'test',
    balanceVersion: 'test',
    player: actor('player-a', 'Tekwan', 'WARRIOR', 'MALE'),
    enemy: actor('player-b', 'Mini tekwan', 'MAGE', 'FEMALE'),
  }
}

describe('ArenaBattlefield', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('renders both real players on the battlefield', () => {
    const wrapper = mount(ArenaBattlefield, {
      props: { battle: battle(), events: [], active: true, resultLabel: null },
    })

    expect(wrapper.get('[data-character-figure="player-a"]').text()).toContain('Tekwan')
    expect(wrapper.get('[data-character-figure="player-b"]').text()).toContain('Mini tekwan')
    expect(wrapper.find('[data-arena-battlefield]').exists()).toBe(true)

    wrapper.unmount()
  })

  it('targets the opponent for attacks and the local player for self abilities', async () => {
    const wrapper = mount(ArenaBattlefield, {
      props: { battle: battle(), events: [], active: true, resultLabel: null },
    })

    await wrapper.get('[data-ability-slot="STRIKE"]').trigger('click')
    await wrapper.get('[data-ability-slot="BASTION"]').trigger('click')

    expect(wrapper.emitted('useAbility')).toEqual([
      ['STRIKE', 'player-b'],
      ['BASTION', 'player-a'],
    ])

    wrapper.unmount()
  })
})
