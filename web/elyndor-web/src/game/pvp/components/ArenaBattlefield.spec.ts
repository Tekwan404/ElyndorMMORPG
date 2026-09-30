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

function battle(status: CombatSnapshot['status'] = 'Active'): CombatSnapshot {
  return {
    sessionId: 'arena-match',
    sequence: 1,
    status,
    serverTimeUtc: '2026-09-30T18:00:00Z',
    contentVersion: 'test',
    balanceVersion: 'test',
    player: actor('player-a', 'Tekwan', 'WARRIOR', 'MALE'),
    enemy: actor('player-b', 'Mini tekwan', 'MAGE', 'FEMALE'),
  }
}

function mountBattlefield(active = true) {
  return mount(ArenaBattlefield, {
    props: {
      battle: battle(active ? 'Active' : 'Victory'),
      events: [],
      active,
      resultLabel: active ? null : 'Победа',
      rating: active ? null : 1016,
      ratingDelta: active ? null : 16,
      honorDelta: active ? null : 8,
    },
    global: { stubs: { Teleport: true } },
  })
}

describe('ArenaBattlefield', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('renders both real players in the pve-sized combat structure', () => {
    const wrapper = mountBattlefield()

    expect(wrapper.get('[data-character-figure="player-a"]').text()).toContain('Tekwan')
    expect(wrapper.get('[data-character-figure="player-b"]').text()).toContain('Mini tekwan')
    expect(wrapper.find('[data-arena-battlefield]').exists()).toBe(true)
    expect(wrapper.find('[data-battle-screen]').exists()).toBe(true)
    expect(wrapper.find('[data-enemy-resource]').exists()).toBe(true)
    expect(wrapper.find('[data-autoattack-toggle]').exists()).toBe(false)
    expect(wrapper.get('[data-combat-exit]').text()).toContain('Сдаться')
    expect(wrapper.find('.arena-battle-screen__log').exists()).toBe(true)
    expect(wrapper.text()).not.toContain('1 × 1')

    wrapper.unmount()
  })

  it('targets the opponent for attacks and the local player for self abilities', async () => {
    const wrapper = mountBattlefield()

    await wrapper.get('[data-ability-slot="STRIKE"]').trigger('click')
    await wrapper.get('[data-ability-slot="BASTION"]').trigger('click')

    expect(wrapper.emitted('useAbility')).toEqual([
      ['STRIKE', 'player-b'],
      ['BASTION', 'player-a'],
    ])

    wrapper.unmount()
  })

  it('shows progression and can queue the next opponent from the result', async () => {
    const wrapper = mountBattlefield(false)

    expect(wrapper.text()).toContain('Победа')
    expect(wrapper.text()).toContain('1016')
    expect(wrapper.text()).toContain('+16')
    expect(wrapper.text()).toContain('+8')

    const buttons = wrapper.findAll('button')
    const next = buttons.find(button => button.text().includes('Следующий соперник'))
    expect(next).toBeDefined()
    await next!.trigger('click')

    expect(wrapper.emitted('nextOpponent')).toEqual([[]])
    wrapper.unmount()
  })
})
