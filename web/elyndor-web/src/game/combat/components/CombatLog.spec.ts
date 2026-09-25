import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import type { BattleLogEntry } from '@/game/combat/battleEventPresentation'
import CombatLog from './CombatLog.vue'

const entries: BattleLogEntry[] = [
  { key: 1, side: 'player', text: 'Текван: 42 урона', occurredAtUtc: '2026-09-25T12:00:00Z', eventType: 'DamageDealt' },
  { key: 2, side: 'enemy', text: 'Жрец: 20 урона', occurredAtUtc: '2026-09-25T12:00:01Z', eventType: 'DamageDealt' },
]

describe('CombatLog', () => {
  it('starts as one latest-event row and opens an accessible drawer', async () => {
    const wrapper = mount(CombatLog, { props: { entries }, attachTo: document.body })

    expect(wrapper.get('[data-combat-log-toggle]').text()).toContain('Жрец: 20 урона')
    expect(wrapper.get('[data-combat-log-toggle]').attributes('aria-expanded')).toBe('false')
    expect(wrapper.find('[data-combat-log-drawer]').exists()).toBe(false)

    await wrapper.get('[data-combat-log-toggle]').trigger('click')

    expect(wrapper.get('[data-combat-log-toggle]').attributes('aria-expanded')).toBe('true')
    expect(wrapper.get('[data-combat-log-drawer]').attributes('role')).toBe('dialog')
    expect(wrapper.findAll('[data-combat-log-entry]')).toHaveLength(2)
    wrapper.unmount()
  })

  it('closes on Escape and restores focus to the toggle', async () => {
    const wrapper = mount(CombatLog, { props: { entries }, attachTo: document.body })
    const toggle = wrapper.get<HTMLButtonElement>('[data-combat-log-toggle]')
    await toggle.trigger('click')

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    await wrapper.vm.$nextTick()

    expect(wrapper.find('[data-combat-log-drawer]').exists()).toBe(false)
    expect(document.activeElement).toBe(toggle.element)
    wrapper.unmount()
  })
})
