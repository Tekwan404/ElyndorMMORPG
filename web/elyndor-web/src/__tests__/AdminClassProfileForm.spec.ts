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
    startingAbilityIds: ['STRIKE'],
    abilityUnlocks: [{ abilityId: 'HEAVY_BLOW', unlockLevel: 2 }],
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
      props: { entity: baseClass(), resourceIds: ['RAGE', 'MANA'], abilityIds: ['STRIKE', 'HEAVY_BLOW'] },
    })

    await wrapper.get('[data-testid="class-growth-strength"]').setValue('3.5')
    const emitted = wrapper.emitted('update:entity') ?? []
    const next = emitted[emitted.length - 1]?.[0] as { levelGrowth: Record<string, number> }

    expect(next.levelGrowth.strength).toBe(3.5)
    expect(next.levelGrowth.stamina).toBe(2)
  })

  it('adds only available starting abilities', async () => {
    const wrapper = mount(AdminClassProfileForm, {
      props: {
        entity: baseClass(),
        resourceIds: ['RAGE'],
        abilityIds: ['STRIKE', 'HEAVY_BLOW', 'PROVOKE'],
      },
    })

    await wrapper.get('[data-testid="class-starting-ability"]').setValue('PROVOKE')
    await wrapper.get('button').trigger('click')
    const emitted = wrapper.emitted('update:entity') ?? []
    const next = emitted[emitted.length - 1]?.[0] as { startingAbilityIds: string[] }

    expect(next.startingAbilityIds).toEqual(['STRIKE', 'PROVOKE'])
  })

  it('clamps unlock level to the supported 2-60 range', async () => {
    const wrapper = mount(AdminClassProfileForm, {
      props: { entity: baseClass(), resourceIds: ['RAGE'], abilityIds: ['STRIKE', 'HEAVY_BLOW'] },
    })

    await wrapper.get('[data-testid="class-unlock-level"]').setValue('99')
    const emitted = wrapper.emitted('update:entity') ?? []
    const next = emitted[emitted.length - 1]?.[0] as { abilityUnlocks: Array<{ unlockLevel: number }> }

    expect(next.abilityUnlocks[0]?.unlockLevel).toBe(60)
  })
})
