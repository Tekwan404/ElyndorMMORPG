import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import type { CombatAbility } from '@/api/contracts'
import SkillPanel from './SkillPanel.vue'

function ability(index: number): CombatAbility {
  return {
    id: `ABILITY_${index}`,
    displayName: `Способность ${index}`,
    description: `Описание ${index}`,
    iconId: null,
    resourceCost: index,
    cooldownSeconds: 6,
    targetType: 'SingleEnemy',
  }
}

describe('SkillPanel', () => {
  it('shows effective cast time and individual cooldown when inspecting an ability', async () => {
    const wrapper = mount(SkillPanel, {
      props: {
        abilities: [{ ...ability(1), castTimeSeconds: 1.1 }],
        cooldowns: {},
        resource: 100,
        queuedAbilityIds: [],
        now: Date.parse('2026-09-25T12:00:00Z'),
        disabled: false,
      },
    })
    expect(wrapper.get('[data-ability-slot]').attributes('aria-label')).toContain('каст 1.1')
    await wrapper.get('[data-ability-slot]').trigger('keydown', { key: 'F1' })
    expect(wrapper.get('[data-ability-timing]').text()).toContain('Каст: 1.1 с')
    expect(wrapper.get('[data-ability-timing]').text()).toContain('Перезарядка: 6 с')
    wrapper.unmount()
  })

  it('renders five skill columns and forwards ready ability use', async () => {
    const wrapper = mount(SkillPanel, {
      props: {
        abilities: [1, 2, 3, 4].map(ability),
        cooldowns: {},
        resource: 100,
        queuedAbilityIds: [],
        now: Date.parse('2026-09-25T12:00:00Z'),
        disabled: false,
      },
    })

    expect(wrapper.get('[data-skill-grid]').attributes('data-columns')).toBe('5')
    expect(wrapper.findAll('[data-ability-slot]')).toHaveLength(4)

    await wrapper.get('[data-ability-slot="ABILITY_1"]').trigger('click')
    expect(wrapper.emitted('use')).toEqual([[expect.objectContaining({ id: 'ABILITY_1' })]])
  })

  it('uses dimming and a label in addition to cooldown numbers', () => {
    const wrapper = mount(SkillPanel, {
      props: {
        abilities: [ability(1)],
        cooldowns: { ABILITY_1: '2026-09-25T12:00:05Z' },
        resource: 100,
        queuedAbilityIds: [],
        now: Date.parse('2026-09-25T12:00:00Z'),
        disabled: false,
      },
    })

    const slot = wrapper.get('[data-ability-slot="ABILITY_1"]')
    expect(slot.attributes('data-state')).toBe('cooldown')
    expect(slot.attributes('aria-label')).toContain('восстановление')
    expect(slot.find('[data-cooldown-overlay]').exists()).toBe(true)
  })

  it('does not activate an ability without enough resource', async () => {
    const wrapper = mount(SkillPanel, {
      props: {
        abilities: [ability(4)],
        cooldowns: {},
        resource: 0,
        queuedAbilityIds: [],
        now: Date.parse('2026-09-25T12:00:00Z'),
        disabled: false,
      },
    })

    await wrapper.get('[data-ability-slot="ABILITY_4"]').trigger('click')
    expect(wrapper.emitted('use')).toBeUndefined()
    expect(wrapper.get('[data-ability-slot="ABILITY_4"]').attributes('data-state')).toBe('resource')
  })

  it('keeps at most ten abilities visible and pages to every extra ability', async () => {
    const wrapper = mount(SkillPanel, {
      props: {
        abilities: Array.from({ length: 16 }, (_, index) => ability(index + 1)),
        cooldowns: {},
        resource: 100,
        queuedAbilityIds: [],
        now: Date.parse('2026-09-25T12:00:00Z'),
        disabled: false,
      },
    })
    expect(wrapper.findAll('[data-ability-slot]')).toHaveLength(10)
    expect(wrapper.find('[data-ability-slot] strong').exists()).toBe(false)
    await wrapper.get('[data-skill-next]').trigger('click')
    expect(wrapper.findAll('[data-ability-slot]')).toHaveLength(6)
    await wrapper.get('[data-ability-slot="ABILITY_16"]').trigger('click')
    expect(wrapper.emitted('use')).toEqual([[expect.objectContaining({ id: 'ABILITY_16' })]])
    await wrapper.setProps({ abilities: [ability(1)] })
    expect(wrapper.find('[data-ability-slot="ABILITY_1"]').exists()).toBe(true)
    wrapper.unmount()
  })

  it('opens details on hold without firing the ability on release', async () => {
    vi.useFakeTimers()
    const wrapper = mount(SkillPanel, {
      props: {
        abilities: [ability(1)],
        cooldowns: {},
        resource: 100,
        queuedAbilityIds: [],
        now: 0,
        disabled: false,
      },
    })
    try {
      await wrapper.get('[data-ability-slot]').trigger('pointerdown')
      await vi.advanceTimersByTimeAsync(500)
      await wrapper.get('[data-ability-slot]').trigger('pointerup')
      await wrapper.get('[data-ability-slot]').trigger('click')
      expect(wrapper.get('[data-ability-inspection]').text()).toContain('Описание 1')
      expect(wrapper.emitted('use')).toBeUndefined()
    } finally {
      wrapper.unmount()
      vi.useRealTimers()
    }
  })
})
