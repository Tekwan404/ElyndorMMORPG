import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

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
  it('renders four mobile skill columns and forwards ready ability use', async () => {
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

    expect(wrapper.get('[data-skill-grid]').attributes('data-columns')).toBe('4')
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
})
