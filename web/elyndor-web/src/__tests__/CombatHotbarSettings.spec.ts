import { mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { KnownAbility } from '@/api/contracts'
import CombatHotbarSettings from '@/game/combat/CombatHotbarSettings.vue'

vi.mock('@/assets/abilityArt', () => ({
  abilityArtUrl: (iconId: string | null | undefined) => iconId ? `/abilities/${iconId}.webp` : undefined,
}))

const ability: KnownAbility = {
  id: 'TEST_COMBAT_STRIKE',
  displayName: 'Боевой удар',
  description: 'Тестовая боевая способность.',
  iconId: 'warrior/combat-strike',
  resourceCost: 10,
  cooldownSeconds: 4,
  type: 'Active',
  targetType: 'SingleEnemy',
  sourceTalentId: null,
  sourceTalentName: null,
}

describe('CombatHotbarSettings', () => {
  beforeEach(() => globalThis.localStorage.clear())

  it('renders Elyndor ability art inside combat-order slots', () => {
    const wrapper = mount(CombatHotbarSettings, {
      props: {
        characterId: 'character-1',
        abilities: [ability],
      },
    })

    const icon = wrapper.get('[data-ability-icon]')
    expect(icon.attributes('src')).toBe('/abilities/warrior/combat-strike.webp')
    expect(icon.attributes('alt')).toBe('Боевой удар')
    expect(wrapper.get('[data-hotbar-slot="1"]').text()).toContain('Боевой удар')
  })

  it('keeps the numbered 12-slot combat layout', () => {
    const wrapper = mount(CombatHotbarSettings, {
      props: {
        characterId: 'character-1',
        abilities: [ability],
      },
    })

    expect(wrapper.findAll('[data-hotbar-slot]')).toHaveLength(12)
  })
})
