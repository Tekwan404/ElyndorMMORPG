import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import { afterEach, describe, expect, it, vi } from 'vitest'

import type { CombatNumberPresentation } from '@/game/combat/battleEventPresentation'
import { DEFAULT_COMBAT_NUMBER_SETTINGS } from '@/game/combat/combatNumberSettings'
import CombatNumbers from './CombatNumbers.vue'

const damage = (key: number, extras: Partial<CombatNumberPresentation> = {}): CombatNumberPresentation => ({
  key,
  targetActorId: 'enemy',
  kind: 'damage',
  value: key * 10,
  ...extras,
})

afterEach(() => vi.useRealTimers())

describe('CombatNumbers', () => {
  it('does not replay historical numbers when entering or reconnecting to combat', async () => {
    vi.useFakeTimers()
    const wrapper = mount(CombatNumbers, {
      props: { actorId: 'enemy', entries: [damage(1), damage(2)] },
    })
    expect(wrapper.findAll('[data-combat-number]')).toHaveLength(0)

    await wrapper.setProps({ entries: [damage(1), damage(2), damage(3)] })
    expect(wrapper.findAll('[data-combat-number]')).toHaveLength(1)
    expect(wrapper.get('[data-combat-number]').attributes('data-combat-number')).toBe('3')
    wrapper.unmount()
  })

  it('caps mobile burst numbers at four and removes them by lifetime', async () => {
    vi.useFakeTimers()
    const wrapper = mount(CombatNumbers, {
      props: { actorId: 'enemy', entries: [] as CombatNumberPresentation[] },
    })
    await wrapper.setProps({ entries: Array.from({ length: 12 }, (_, i) => damage(i + 1)) })
    expect(wrapper.findAll('[data-combat-number]')).toHaveLength(4)
    expect(wrapper.find('[data-combat-number="8"]').exists()).toBe(false)
    expect(wrapper.get('[data-combat-number="12"]').text()).toBe('-120')

    await vi.advanceTimersByTimeAsync(780)
    await nextTick()
    expect(wrapper.findAll('[data-combat-number]')).toHaveLength(0)
    wrapper.unmount()
  })

  it('keeps periodic and critical hits visible with balanced default settings', async () => {
    vi.useFakeTimers()
    const wrapper = mount(CombatNumbers, {
      props: { actorId: 'enemy', entries: [] as CombatNumberPresentation[] },
    })
    await wrapper.setProps({
      entries: [damage(1, { kind: 'crit', periodic: true })],
    })
    expect(wrapper.get('[data-combat-number="1"]').classes()).toContain('combat-numbers__entry--crit')
    expect(wrapper.get('[data-combat-number="1"]').attributes('data-impact')).toBe('true')
    wrapper.unmount()
  })

  it('honors disabled effects and explicitly marked DoTs without hiding normal damage', async () => {
    vi.useFakeTimers()
    const wrapper = mount(CombatNumbers, {
      props: {
        actorId: 'enemy',
        entries: [] as CombatNumberPresentation[],
        settings: {
          ...DEFAULT_COMBAT_NUMBER_SETTINGS,
          emphasizeCrits: false,
          hitEffects: false,
          showPeriodicDamage: false,
        },
      },
    })
    await wrapper.setProps({
      entries: [
        damage(1, { periodic: true }),
        damage(2, { kind: 'crit' }),
        damage(3, { targetActorId: 'ally' }),
      ],
    })
    expect(wrapper.find('[data-combat-number="1"]').exists()).toBe(false)
    expect(wrapper.find('[data-combat-number="3"]').exists()).toBe(false)
    const crit = wrapper.get('[data-combat-number="2"]')
    expect(crit.classes()).toContain('combat-numbers__entry--damage')
    expect(crit.attributes('data-impact')).toBe('false')
    wrapper.unmount()
  })

  it('keeps minimal presentation bounded to two numbers', async () => {
    vi.useFakeTimers()
    const wrapper = mount(CombatNumbers, {
      props: {
        actorId: 'enemy',
        entries: [] as CombatNumberPresentation[],
        settings: { ...DEFAULT_COMBAT_NUMBER_SETTINGS, density: 'minimal' },
      },
    })
    await wrapper.setProps({
      entries: [
        damage(1), damage(2), damage(3), damage(4, { kind: 'heal' }),
        damage(5, { kind: 'crit' }),
      ],
    })
    expect(wrapper.findAll('[data-combat-number]')).toHaveLength(2)
    expect(wrapper.get('[data-combat-number="5"]').exists()).toBe(true)
    wrapper.unmount()
  })
})
