import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import type { CombatActorSnapshot, CombatEvent, CombatSnapshot } from '@/api/contracts'
import CombatPartyDamageFeed from '@/game/combat/CombatPartyDamageFeed.vue'
import { useCombatSessionStore } from '@/stores/combatSession'

function actor(actorId: string, kind: CombatActorSnapshot['kind'], overrides: Partial<CombatActorSnapshot> = {}): CombatActorSnapshot {
  return {
    actorId,
    kind,
    definitionId: kind === 'Monster' ? 'TEST_ENEMY' : 'PALADIN',
    name: actorId,
    hp: 100,
    maxHp: 100,
    resourceType: kind === 'Monster' ? 'NONE' : 'MANA',
    resource: kind === 'Monster' ? 0 : 100,
    maxResource: kind === 'Monster' ? 0 : 100,
    autoAttackEnabled: false,
    cooldowns: {},
    knownAbilityIds: [],
    abilities: [],
    effects: [],
    ...overrides,
  }
}

function snapshot(party = true): CombatSnapshot {
  const player = actor('player', 'Player')
  const ally = actor('ally', 'Player', { definitionId: 'MAGE' })
  const enemy = actor('enemy-1', 'Monster', { hp: 200, maxHp: 200 })
  return {
    sessionId: 'session-1',
    sequence: 0,
    status: 'Active',
    serverTimeUtc: '2026-09-24T05:00:00Z',
    contentVersion: 'test',
    balanceVersion: 'test',
    player,
    enemy,
    enemies: [enemy, actor('enemy-2', 'Monster', { hp: 150, maxHp: 150 })],
    selectedTargetActorId: enemy.actorId,
    players: party ? [player, ally] : [player],
  }
}

function damage(sequence: number, sourceActorId: string, targetActorId: string, amount: number): CombatEvent {
  return {
    sequence,
    type: 'DamageDealt',
    actorId: sourceActorId,
    sourceActorId,
    targetActorId,
    definitionId: 'TEST_ATTACK',
    amount,
    amountBeforeShields: amount,
    serverTimeUtc: `2026-09-24T05:00:0${sequence}Z`,
  }
}

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = '<div data-combat-battlefield></div>'
  vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => {
    callback(0)
    return 1
  })
  vi.stubGlobal('cancelAnimationFrame', () => undefined)
})

afterEach(() => {
  vi.useRealTimers()
  document.body.innerHTML = ''
  vi.unstubAllGlobals()
})

describe('CombatPartyDamageFeed', () => {
  it('renders allied damage to the selected enemy during party combat', async () => {
    const combat = useCombatSessionStore()
    combat.snapshot = snapshot(true)
    const wrapper = mount(CombatPartyDamageFeed, { attachTo: document.body })
    await flushPromises()

    combat.events = [damage(1, 'ally', 'enemy-1', 64)]
    await flushPromises()

    const battlefield = document.querySelector('[data-combat-battlefield]')!
    expect(battlefield.querySelector('[data-combat-party-enemy-damage-feed]')?.textContent).toContain('−64')
    wrapper.unmount()
  })

  it('renders damage received by the local player from any enemy', async () => {
    const combat = useCombatSessionStore()
    combat.snapshot = snapshot(true)
    const wrapper = mount(CombatPartyDamageFeed, { attachTo: document.body })
    await flushPromises()

    combat.events = [damage(1, 'enemy-2', 'player', 31)]
    await flushPromises()

    const battlefield = document.querySelector('[data-combat-battlefield]')!
    expect(battlefield.querySelector('[data-combat-party-player-damage-feed]')?.textContent).toContain('−31')
    wrapper.unmount()
  })

  it('does not mount a duplicate party feed in solo combat', async () => {
    const combat = useCombatSessionStore()
    combat.snapshot = snapshot(false)
    const wrapper = mount(CombatPartyDamageFeed, { attachTo: document.body })
    await flushPromises()

    combat.events = [damage(1, 'player', 'enemy-1', 44)]
    await flushPromises()

    const battlefield = document.querySelector('[data-combat-battlefield]')!
    expect(battlefield.querySelector('[data-combat-party-enemy-damage-feed]')).toBeNull()
    wrapper.unmount()
  })

  it('does not replay old selected-target hits after switching enemies', async () => {
    const combat = useCombatSessionStore()
    combat.snapshot = snapshot(true)
    const wrapper = mount(CombatPartyDamageFeed, { attachTo: document.body })
    await flushPromises()

    combat.events = [damage(1, 'ally', 'enemy-1', 64)]
    await flushPromises()

    combat.snapshot = { ...combat.snapshot!, selectedTargetActorId: 'enemy-2', sequence: 1 }
    await flushPromises()

    const battlefield = document.querySelector('[data-combat-battlefield]')!
    expect(battlefield.querySelector('[data-combat-party-enemy-damage-feed]')?.textContent).not.toContain('−64')

    combat.events = [...combat.events, damage(2, 'ally', 'enemy-2', 52)]
    await flushPromises()
    expect(battlefield.querySelector('[data-combat-party-enemy-damage-feed]')?.textContent).toContain('−52')
    wrapper.unmount()
  })

  it('keeps a fresh hit when target snapshot and events arrive in the same update turn', async () => {
    const combat = useCombatSessionStore()
    combat.snapshot = snapshot(true)
    const wrapper = mount(CombatPartyDamageFeed, { attachTo: document.body })
    await flushPromises()

    combat.events = [damage(1, 'ally', 'enemy-1', 64)]
    await flushPromises()

    combat.snapshot = { ...combat.snapshot!, selectedTargetActorId: 'enemy-2', sequence: 2 }
    combat.events = [...combat.events, damage(2, 'ally', 'enemy-2', 52)]
    await flushPromises()

    const battlefield = document.querySelector('[data-combat-battlefield]')!
    const copy = battlefield.querySelector('[data-combat-party-enemy-damage-feed]')?.textContent ?? ''
    expect(copy).not.toContain('−64')
    expect(copy).toContain('−52')
    wrapper.unmount()
  })

  it('clears transient hits when party combat collapses to solo and does not replay them', async () => {
    const combat = useCombatSessionStore()
    combat.snapshot = snapshot(true)
    const wrapper = mount(CombatPartyDamageFeed, { attachTo: document.body })
    await flushPromises()

    combat.events = [damage(1, 'ally', 'enemy-1', 64)]
    await flushPromises()
    const battlefield = document.querySelector('[data-combat-battlefield]')!
    expect(battlefield.querySelector('[data-combat-party-enemy-damage-feed]')?.textContent).toContain('−64')

    const solo = snapshot(false)
    combat.snapshot = { ...solo, sequence: 1 }
    await flushPromises()
    expect(battlefield.querySelector('[data-combat-party-enemy-damage-feed]')).toBeNull()

    combat.snapshot = { ...snapshot(true), sequence: 1 }
    await flushPromises()
    expect(battlefield.querySelector('[data-combat-party-enemy-damage-feed]')?.textContent ?? '').not.toContain('−64')
    wrapper.unmount()
  })

  it('removes transient damage numbers after their presentation lifetime', async () => {
    vi.useFakeTimers()
    const combat = useCombatSessionStore()
    combat.snapshot = snapshot(true)
    const wrapper = mount(CombatPartyDamageFeed, { attachTo: document.body })
    await flushPromises()

    combat.events = [damage(1, 'ally', 'enemy-1', 64)]
    await flushPromises()
    const battlefield = document.querySelector('[data-combat-battlefield]')!
    expect(battlefield.querySelector('[data-combat-party-enemy-damage-feed]')?.textContent).toContain('−64')

    vi.advanceTimersByTime(1_700)
    await wrapper.vm.$nextTick()
    expect(battlefield.querySelector('[data-combat-party-enemy-damage-feed]')?.textContent ?? '').not.toContain('−64')
    wrapper.unmount()
  })
})
