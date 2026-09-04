import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import AdminEntityForm from '@/admin/AdminEntityForm.vue'

describe('AdminEntityForm', () => {
  it('updates common monster balance fields without dropping unknown data', async () => {
    const wrapper = mount(AdminEntityForm, {
      props: {
        sectionKey: 'monsters',
        entity: {
          id: 'WOLF',
          maxHp: 180,
          customFutureField: 'keep-me',
          stats: { attackPower: 12, spellPower: 0, accuracy: 95, criticalChance: 5, armor: 16, magicResistance: 8, dodge: 3 },
        },
      },
    })

    await wrapper.get('[data-testid="monster-max-hp"]').setValue('240')
    const emitted = wrapper.emitted('update:entity') ?? []
    const next = emitted[emitted.length - 1]?.[0] as Record<string, unknown>

    expect(next.maxHp).toBe(240)
    expect(next.customFutureField).toBe('keep-me')
  })

  it('updates an ability damage coefficient inside the existing damage action', async () => {
    const wrapper = mount(AdminEntityForm, {
      props: {
        sectionKey: 'abilities',
        entity: {
          id: 'MAGE_FIREBALL',
          resourceCost: 20,
          actions: [{ type: 'Damage', damageType: 'Magical', spellPowerCoefficient: 1.25 }],
        },
      },
    })

    await wrapper.get('[data-testid="ability-damage-coefficient"]').setValue('1.5')
    const emitted = wrapper.emitted('update:entity') ?? []
    const next = emitted[emitted.length - 1]?.[0] as { actions: Array<Record<string, unknown>> }

    expect(next.actions[0]?.spellPowerCoefficient).toBe(1.5)
    expect(next.actions[0]?.damageType).toBe('Magical')
  })

  it('keeps physical ability coefficients on Attack Power', async () => {
    const wrapper = mount(AdminEntityForm, {
      props: {
        sectionKey: 'abilities',
        entity: {
          id: 'STRIKE',
          actions: [{ type: 'Damage', damageType: 'Physical', attackPowerCoefficient: 0.8 }],
        },
      },
    })

    await wrapper.get('[data-testid="ability-damage-coefficient"]').setValue('0.95')
    const emitted = wrapper.emitted('update:entity') ?? []
    const next = emitted[emitted.length - 1]?.[0] as { actions: Array<Record<string, unknown>> }

    expect(next.actions[0]?.attackPowerCoefficient).toBe(0.95)
    expect(next.actions[0]?.spellPowerCoefficient).toBeUndefined()
  })

  it('normalizes item shape when changing a material into equipment', async () => {
    const wrapper = mount(AdminEntityForm, {
      props: {
        sectionKey: 'items',
        entity: {
          id: 'OLD_RING',
          type: 'Material',
          stackable: true,
          maxStack: 99,
          slot: null,
          stats: { strength: 0, agility: 0, intellect: 0, stamina: 0 },
          healAmount: 0,
          consumableCooldownSeconds: 0,
        },
      },
    })

    await wrapper.get('[data-testid="item-type"]').setValue('Equipment')
    const emitted = wrapper.emitted('update:entity') ?? []
    const next = emitted[emitted.length - 1]?.[0] as Record<string, unknown>

    expect(next.type).toBe('Equipment')
    expect(next.stackable).toBe(false)
    expect(next.maxStack).toBe(1)
    expect(next.slot).toBe('Accessory')
  })

  it('updates nested item stats', async () => {
    const wrapper = mount(AdminEntityForm, {
      props: {
        sectionKey: 'items',
        entity: {
          id: 'RANGER_FANG_BLADE',
          stats: { strength: 2, agility: 0, intellect: 0, stamina: 0 },
        },
      },
    })

    await wrapper.get('[data-testid="item-strength"]').setValue('4')
    const emitted = wrapper.emitted('update:entity') ?? []
    const next = emitted[emitted.length - 1]?.[0] as { stats: Record<string, unknown> }

    expect(next.stats.strength).toBe(4)
    expect(next.stats.agility).toBe(0)
  })
})
