import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import type { CombatEffectSnapshot } from '@/api/contracts'
import CombatEffectStrip from '@/game/combat/CombatEffectStrip.vue'

describe('CombatEffectStrip', () => {
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
    })

    expect(wrapper.get('.combat-effect-strip__stacks').text()).toBe('2')
    expect(wrapper.findAll('.combat-effect-strip__effect small')[0]?.text()).toBe('8с')
    expect(wrapper.text()).not.toContain(effect.description)

    await wrapper.get('.combat-effect-strip__effect').trigger('click')

    expect(wrapper.get('[data-effect-inspection]').text()).toContain('Метка охотника')
    expect(wrapper.get('[data-effect-inspection]').text()).toContain(effect.description)
    expect(wrapper.get('[data-effect-inspection]').text()).toContain('Стаки: 2')
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
