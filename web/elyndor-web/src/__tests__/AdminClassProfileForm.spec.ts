import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import AdminClassProfileForm from '@/admin/AdminClassProfileForm.vue'

function baseClass() {
  return {
    id: 'WARRIOR',
    primaryAttribute: 'STRENGTH',
    resourceProfileId: 'RAGE',
    baseStats: { strength: 12, agility: 6, intellect: 4, stamina: 10 },
    levelGrowth: { strength: 3, agility: 1, intellect: 0.5, stamina: 2 },
    allowedWeaponCategories: ['ONE_HAND_SWORD'],
    allowedArmorCategories: ['HEAVY'],
    prototypeIdentity: 'Warrior',
    startingAbilityIds: [],
    abilityUnlocks: [],
    combatAutoAttack: {
      interval: '00:00:02',
      baseDamage: 0,
      attackPowerCoefficient: 0.65,
      resourceOnHit: 10,
    },
  }
}

describe('AdminClassProfileForm', () => {
  it('updates class growth without dropping sibling stats', async () => {
    const wrapper = mount(AdminClassProfileForm, {
      props: { entity: baseClass(), resourceIds: ['RAGE', 'MANA'] },
    })

    await wrapper.get('[data-testid="class-growth-strength"]').setValue('3.5')
    const emitted = wrapper.emitted('update:entity') ?? []
    const next = emitted[emitted.length - 1]?.[0] as { levelGrowth: Record<string, number> }

    expect(next.levelGrowth.strength).toBe(3.5)
    expect(next.levelGrowth.stamina).toBe(2)
  })

  it('does not expose class-based ability grants', () => {
    const wrapper = mount(AdminClassProfileForm, {
      props: { entity: baseClass(), resourceIds: ['RAGE'] },
    })

    expect(wrapper.text()).toContain('Только через Talents')
    expect(wrapper.find('[data-testid="class-starting-ability"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="class-unlock-level"]').exists()).toBe(false)
  })

})
