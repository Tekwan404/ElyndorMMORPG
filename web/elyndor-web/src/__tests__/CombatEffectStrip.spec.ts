import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import type { CombatEffectSnapshot } from '@/api/contracts'
import CombatEffectStrip from '@/game/combat/CombatEffectStrip.vue'

describe('CombatEffectStrip', () => {
  it('omits expired effects and never turns unknown machine IDs into visible English', async () => {
    const now = Date.parse('2026-09-18T10:00:00Z')
    const wrapper = mount(CombatEffectStrip, {
      props: { now, side: 'player', effects: [
        { id: 'MAGE_INTERNAL_MARKER', stacks: 1, expiresAtUtc: '2026-09-18T10:00:10Z', kind: 'Buff' },
        { id: 'EXPIRED', stacks: 1, expiresAtUtc: '2026-09-18T09:59:59Z' },
      ] }, global: { stubs: { Teleport: true } },
    })
    expect(wrapper.findAll('.combat-effect-strip__effect')).toHaveLength(1)
    await wrapper.get('.combat-effect-strip__effect').trigger('click')
    expect(wrapper.get('[data-effect-inspection]').text()).not.toContain('Mage internal')
    expect(wrapper.get('[data-effect-inspection]').text()).not.toContain('пока не добавлено')
    wrapper.unmount()
  })

  it('keeps inspection open when the same instance is refreshed and distinguishes harmful effects', async () => {
    const now = Date.parse('2026-09-18T10:00:00Z')
    const effect: CombatEffectSnapshot = {
      id: 'MAGE_FIRE_IGNITE', instanceId: 'ignite-1', sourceActorId: 'mage-1',
      displayName: 'Воспламенение', description: '25 ед. урона каждые 2 сек.',
      kind: 'DamageOverTime', disposition: 'Harmful', stacks: 1, maxStacks: 3,
      expiresAtUtc: '2026-09-18T10:00:10Z',
    }
    const wrapper = mount(CombatEffectStrip, {
      props: { effects: [effect], now, side: 'enemy' }, global: { stubs: { Teleport: true } },
    })
    await wrapper.get('.combat-effect-strip__effect').trigger('click')
    await wrapper.setProps({ effects: [{ ...effect, stacks: 2, expiresAtUtc: '2026-09-18T10:00:15Z' }] })
    expect(wrapper.get('[data-effect-inspection]').text()).toContain('Ослабление')
    expect(wrapper.get('[data-effect-inspection]').text()).toContain('25 ед. урона')
    expect(wrapper.get('[data-effect-inspection]').text()).toContain('2 / 3')
    expect(wrapper.get('.combat-effect-strip__effect').attributes('data-disposition')).toBe('Harmful')
    wrapper.unmount()
  })

  it('shows compact stacks and opens effect description on tap', async () => {
    const effect: CombatEffectSnapshot = {
      id: 'HUNTER_MARK_DEBUFF',
      displayName: 'Метка охотника',
      description: 'Помеченная цель получает дополнительный урон.',
      iconId: null,
      stacks: 2,
      expiresAtUtc: '2026-09-18T10:00:08.000Z',
    }
    const wrapper = mount(CombatEffectStrip, {
      props: { effects: [effect], now: Date.parse('2026-09-18T10:00:00.000Z'), side: 'enemy' },
      global: { stubs: { Teleport: true } },
    })

    expect(wrapper.get('.combat-effect-strip__stacks').text()).toBe('2')
    expect(wrapper.findAll('.combat-effect-strip__effect small')[0]?.text()).toBe('8с')
    expect(wrapper.text()).not.toContain(effect.description)

    await wrapper.get('.combat-effect-strip__effect').trigger('click')

    expect(wrapper.get('[role="dialog"]').text()).toContain('Метка охотника')
    expect(wrapper.get('[data-effect-inspection]').text()).toContain(effect.description)
    expect(wrapper.findAll('[data-effect-inspection] dd').map(item => item.text())).toContain('2')
  })

  it('shows a prominent countdown for living black star on the affected player', () => {
    const effect: CombatEffectSnapshot = {
      id: 'BLACK_STAR_LIVING_BOMB_MARK',
      displayName: 'Живая Чёрная Звезда',
      description: 'Взрывается по окончании эффекта.',
      iconId: null,
      stacks: 1,
      expiresAtUtc: '2026-09-18T10:00:06.200Z',
    }
    const now = Date.parse('2026-09-18T10:00:00.000Z')

    const player = mount(CombatEffectStrip, {
      props: { effects: [effect], now, side: 'player' },
    })
    const enemy = mount(CombatEffectStrip, {
      props: { effects: [effect], now, side: 'enemy' },
    })

    const warning = player.get('[data-critical-effect-warning]').text()
    expect(warning).toContain('ЧЁРНАЯ ЗВЕЗДА')
    expect(warning).toContain('·')
    expect(warning).toContain('6.2')
    expect(enemy.find('[data-critical-effect-warning]').exists()).toBe(false)
  })
})
