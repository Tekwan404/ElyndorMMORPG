import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'
import { afterEach, describe, expect, it, vi } from 'vitest'

import type { CombatEvent } from '@/api/contracts'
import CombatBlockFeedback from '@/game/combat/CombatBlockFeedback.vue'
import { useCombatSessionStore } from '@/stores/combatSession'

function event(sequence: number, type: string, amount: number): CombatEvent {
  return {
    sequence,
    type,
    actorId: 'actor-player',
    sourceActorId: 'actor-enemy',
    targetActorId: 'actor-player',
    definitionId: null,
    amount,
    amountBeforeShields: 0,
    serverTimeUtc: '2026-09-12T12:00:00Z',
  }
}

afterEach(() => {
  vi.useRealTimers()
})

describe('CombatBlockFeedback', () => {
  it('shows the absorbed amount for ShieldAbsorbed and hides it after the float animation window', async () => {
    vi.useFakeTimers()
    const pinia = createPinia()
    setActivePinia(pinia)
    const combat = useCombatSessionStore(pinia)
    const wrapper = mount(CombatBlockFeedback, { global: { plugins: [pinia] } })

    combat.events = [
      event(1, 'DamageDealt', 80),
      event(2, 'ShieldAbsorbed', 27.6),
    ]
    await nextTick()

    const feedback = wrapper.get('[data-combat-block-feedback]')
    expect(feedback.text()).toContain('Блок −28')

    vi.advanceTimersByTime(1_250)
    await nextTick()
    expect(wrapper.find('[data-combat-block-feedback]').exists()).toBe(false)
  })

  it('ignores ordinary damage events', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const combat = useCombatSessionStore(pinia)
    combat.events = [event(1, 'DamageDealt', 42)]

    const wrapper = mount(CombatBlockFeedback, { global: { plugins: [pinia] } })
    await nextTick()

    expect(wrapper.find('[data-combat-block-feedback]').exists()).toBe(false)
  })
})
