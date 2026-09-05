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

  it('uses content-backed monster loot and ability relations', async () => {
    const wrapper = mount(AdminEntityForm, {
      props: {
        sectionKey: 'monsters',
        entity: {
          id: 'WOLF',
          lootTableId: null,
          aiProfileId: 'WOLF_BASIC_AI',
          abilityIds: [],
          stats: {},
        },
        lootTableIds: ['WOLF_LOOT'],
        aiProfileIds: ['WOLF_BASIC_AI'],
        abilityIds: ['BITE'],
      },
    })

    await wrapper.get('[data-testid="monster-loot-table"]').setValue('WOLF_LOOT')
    let emitted = wrapper.emitted('update:entity') ?? []
    let next = emitted[emitted.length - 1]?.[0] as Record<string, unknown>
    expect(next.lootTableId).toBe('WOLF_LOOT')

    await wrapper.setProps({ entity: next })
    await wrapper.get('[data-testid="monster-add-ability"]').setValue('BITE')
    await wrapper.get('[data-testid="monster-add-ability"]').trigger('change')
    emitted = wrapper.emitted('update:entity') ?? []
    next = emitted[emitted.length - 1]?.[0] as Record<string, unknown>
    expect(next.abilityIds).toEqual(['BITE'])
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
    expect(next.slot).toBe('Amulet')
  })

  it('normalizes equipment category shape when slot changes', async () => {
    const wrapper = mount(AdminEntityForm, {
      props: {
        sectionKey: 'items',
        entity: {
          id: 'OLD_RING',
          type: 'Equipment',
          stackable: false,
          maxStack: 1,
          slot: 'Amulet',
          weaponCategory: null,
          armorCategory: null,
          allowedClassIds: [],
          stats: { strength: 1, agility: 0, intellect: 0, stamina: 0 },
        },
        classIds: ['WARRIOR', 'MAGE'],
      },
    })

    await wrapper.get('[data-testid="item-slot"]').setValue('MainHand')
    const emitted = wrapper.emitted('update:entity') ?? []
    const next = emitted[emitted.length - 1]?.[0] as Record<string, unknown>

    expect(next.slot).toBe('MainHand')
    expect(next.weaponCategory).toBe('ONE_HAND_SWORD')
    expect(next.armorCategory).toBeNull()
  })

  it('adds and updates secondary equipment modifiers', async () => {
    const wrapper = mount(AdminEntityForm, {
      props: {
        sectionKey: 'items',
        entity: {
          id: 'WOLF_CHEST',
          type: 'Equipment',
          slot: 'Chest',
          stats: { strength: 4, agility: 0, intellect: 0, stamina: 7 },
          armorFlat: 18,
          criticalChancePercent: 0,
        },
      },
    })

    const modifierSelect = wrapper.find('.add-modifier select')
    await modifierSelect.setValue('criticalChancePercent')
    await wrapper.get('.add-modifier button').trigger('click')

    let emitted = wrapper.emitted('update:entity') ?? []
    let next = emitted[emitted.length - 1]?.[0] as Record<string, unknown>
    expect(next.criticalChancePercent).toBe(1)

    await wrapper.setProps({ entity: next })
    const critInput = wrapper.findAll('.modifier-row input')
      .find(input => input.element.parentElement?.textContent?.includes('Critical Chance'))
    expect(critInput).toBeDefined()
    await critInput!.setValue('2.5')
    emitted = wrapper.emitted('update:entity') ?? []
    next = emitted[emitted.length - 1]?.[0] as Record<string, unknown>
    expect(next.criticalChancePercent).toBe(2.5)
  })

  it('updates nested item stats', async () => {
    const wrapper = mount(AdminEntityForm, {
      props: {
        sectionKey: 'items',
        entity: {
          id: 'RANGER_FANG_BLADE',
          type: 'Equipment',
          slot: 'MainHand',
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
