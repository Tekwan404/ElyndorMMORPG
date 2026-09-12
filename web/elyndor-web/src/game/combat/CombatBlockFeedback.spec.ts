import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'
import { afterEach, describe, expect, it, vi } from 'vitest'

import type { CombatEvent } from '@/api/contracts'
import CombatBlockFeedback from '@/game/combat/CombatBlockFeedback.vue'
import { useCombatSessionStore } from '@/stores/combatSession'

function event(
  sequence: number,
  type: string,
  amount: number,
  amountBeforeShields = 0,
): CombatEvent {
  return {
    sequence,
    type,
    actorId: 'actor-player',
    sourceActorId: 'actor-enemy',
    targetActorId: 'actor-player',
    definitionId: null,
    amount,
    amountBeforeShields,
    serverTimeUtc: '2026-09-12T12:00:00Z',
  }
}

afterEach(() => {
  vi.useRealTimers()
})

describe('CombatBlockFeedback', () => {
  it('shows the blocked amount for DamageBlocked and hides it after the feedback window', async () => {
    vi.useFakeTimers()
    const pinia = createPinia()
    setActivePinia(pinia)
    const combat = useCombatSessionStore(pinia)
    const wrapper = mount(CombatBlockFeedback, { global: { plugins: [pinia] } })

    combat.events = [
      event(1, 'DamageDealt', 53),
      event(2, 'DamageBlocked', 27.6, 53),
    ]
    await nextTick()

    const feedback = wrapper.get('[data-combat-block-feedback]')
    expect(feedback.text()).toContain('БЛОК −28')
    expect(feedback.classes()).not.toContain('combat-block-feedback--full')

    vi.advanceTimersByTime(1_250)
    await nextTick()
    expect(wrapper.find('[data-combat-block-feedback]').exists()).toBe(false)
  })

  it('shows full block when no damage remains after shield block', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const combat = useCombatSessionStore(pinia)
    const wrapper = mount(CombatBlockFeedback, { global: { plugins: [pinia] } })

    combat.events = [event(1, 'DamageBlocked', 42, 0)]
    await nextTick()

    const feedback = wrapper.get('[data-combat-block-feedback]')
    expect(feedback.text()).toContain('ПОЛНЫЙ БЛОК')
    expect(feedback.classes()).toContain('combat-block-feedback--full')
  })

  it('does not confuse effect shield absorption with equipment block', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const combat = useCombatSessionStore(pinia)
    combat.events = [
      event(1, 'DamageDealt', 42),
      event(2, 'ShieldAbsorbed', 20),
    ]

    const wrapper = mount(CombatBlockFeedback, { global: { plugins: [pinia] } })
    await nextTick()

    expect(wrapper.find('[data-combat-block-feedback]').exists()).toBe(false)
  })
})
