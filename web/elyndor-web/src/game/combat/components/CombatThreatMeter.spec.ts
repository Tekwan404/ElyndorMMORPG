import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import type { CombatThreatSnapshot } from '@/stores/combatSession'
import CombatThreatMeter from './CombatThreatMeter.vue'

const threat: CombatThreatSnapshot = {
  enemyActorId: 'boss',
  enemyName: 'Архон Пепла',
  currentTargetActorId: 'tank',
  forcedTargetActorId: 'tank',
  entries: [
    { actorId: 'tank', name: 'Защитник', threat: 1280, isCurrentTarget: true },
    { actorId: 'local', name: 'Берсерк', threat: 700, isCurrentTarget: false },
    { actorId: 'healer', name: 'Целитель', threat: 240, isCurrentTarget: false },
  ],
}

describe('CombatThreatMeter', () => {
  it('shows authoritative threat values with current target, taunt and self markers', () => {
    const wrapper = mount(CombatThreatMeter, {
      props: { threat, enemyActorId: 'boss', localActorId: 'local' },
    })

    expect(wrapper.get('[data-combat-threat-meter]').text()).toContain('Архон Пепла')
    expect(wrapper.findAll('[data-threat-actor-id]')).toHaveLength(3)
    expect(wrapper.get('[data-threat-actor-id="tank"]').text()).toMatch(/1\\s280/)
    expect(wrapper.get('[data-threat-actor-id="tank"]').text()).toContain('ПРОВОКАЦИЯ')
    expect(wrapper.get('[data-threat-actor-id="tank"]').classes()).toContain('is-target')
    expect(wrapper.get('[data-threat-actor-id="local"]').text()).toContain('ВЫ')
    expect(wrapper.get('[data-threat-actor-id="local"]').classes()).toContain('is-self')
    expect(wrapper.get('[data-threat-actor-id="tank"] .combat-threat-meter__bar span').attributes('style')).toContain('100%')
    wrapper.unmount()
  })

  it('hides old enemy threat values while switching targets', async () => {
    const wrapper = mount(CombatThreatMeter, {
      props: { threat, enemyActorId: 'boss', localActorId: 'local' },
    })

    await wrapper.setProps({ enemyActorId: 'other' })
    expect(wrapper.findAll('[data-threat-actor-id]')).toHaveLength(0)
    expect(wrapper.get('[data-combat-threat-meter]').text()).toContain('Ожидание угрозы')
    expect(wrapper.get('[data-combat-threat-meter]').text()).not.toContain('1')
    wrapper.unmount()
  })

  it('renders only up to five participants in the small mobile overlay', () => {
    const many: CombatThreatSnapshot = {
      ...threat,
      entries: Array.from({ length: 9 }, (_, i) => ({
        actorId: `actor-${i}`,
        name: `Участник ${i}`,
        threat: 900 - i * 50,
        isCurrentTarget: false,
      })),
    }
    const wrapper = mount(CombatThreatMeter, {
      props: { threat: many, enemyActorId: 'boss', localActorId: 'local' },
    })
    expect(wrapper.findAll('[data-threat-actor-id]')).toHaveLength(5)
    wrapper.unmount()
  })
})
