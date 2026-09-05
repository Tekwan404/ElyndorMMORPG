import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import AdminLocationForm from '@/admin/AdminLocationForm.vue'
import AdminLootTableForm from '@/admin/AdminLootTableForm.vue'
import AdminMerchantForm from '@/admin/AdminMerchantForm.vue'
import AdminTalentTreeForm from '@/admin/AdminTalentTreeForm.vue'

describe('rich admin content forms', () => {
  it('edits loot chance as a human-readable percent', async () => {
    const wrapper = mount(AdminLootTableForm, {
      props: {
        entity: {
          id: 'WOLF_LOOT',
          version: 1,
          entries: [{ itemId: 'WOLF_HIDE', dropChance: 0.4, minQuantity: 1, maxQuantity: 2 }],
        },
        items: [{ id: 'WOLF_HIDE', name: 'Wolf Hide', stackable: true }],
      },
    })

    await wrapper.get('[data-testid="loot-drop-chance"]').setValue('45')
    const emitted = wrapper.emitted('update:entity') ?? []
    const next = emitted[emitted.length - 1]?.[0] as { entries: Array<Record<string, unknown>> }

    expect(next.entries[0]?.dropChance).toBe(0.45)
  })

  it('adds an item to a merchant catalog', async () => {
    const wrapper = mount(AdminMerchantForm, {
      props: {
        entity: {
          id: 'MARCUS',
          name: 'Marcus',
          locationId: 'STARTER_TOWN',
          description: 'Supplies',
          itemIds: [],
        },
        locations: [{ id: 'STARTER_TOWN', name: 'Starter Town' }],
        items: [{ id: 'POTION', name: 'Potion', buyPriceGold: 20 }],
      },
    })

    await wrapper.get('[data-testid="merchant-new-item"]').setValue('POTION')
    await wrapper.get('button:last-of-type').trigger('click')
    const emitted = wrapper.emitted('update:entity') ?? []
    const next = emitted[emitted.length - 1]?.[0] as Record<string, unknown>

    expect(next.itemIds).toEqual(['POTION'])
  })

  it('adds a normal monster encounter to a location', async () => {
    const wrapper = mount(AdminLocationForm, {
      props: {
        entity: {
          id: 'FOREST',
          displayName: 'Forest',
          dangerLevel: 'ADVENTURE',
          recommendedLevel: 2,
          transitions: [],
          encounters: [],
        },
        monsters: [{ id: 'DIRE_WOLF', name: 'Лютоволк', rank: 'Elite' }],
      },
    })

    await wrapper.get('[data-testid="location-new-monster"]').setValue('DIRE_WOLF')
    await wrapper.get('button:last-of-type').trigger('click')
    const emitted = wrapper.emitted('update:entity') ?? []
    const next = emitted[emitted.length - 1]?.[0] as Record<string, unknown>

    expect(next.encounters).toEqual([{ monsterId: 'DIRE_WOLF', weight: 1 }])
  })

  it('keeps talent branch node counts consistent when moving a node', async () => {
    const wrapper = mount(AdminTalentTreeForm, {
      props: {
        entity: {
          id: 'TEST_TREE',
          classId: 'MAGE',
          maxSpendablePoints: 10,
          version: 1,
          branches: [
            { id: 'FIRE', name: 'Fire', fantasy: '', nodeCount: 1 },
            { id: 'ARCANE', name: 'Arcane', fantasy: '', nodeCount: 1 },
          ],
          nodes: [
            {
              id: 'F-1-1',
              branchId: 'FIRE',
              tier: 1,
              requiredSpentPoints: 0,
              name: 'Heat',
              englishName: 'Heat',
              maxRank: 1,
              prerequisites: [],
              description: 'Test',
              version: 1,
              modifiers: [{ type: 'StatModifier', key: 'ATTACK_POWER_PERCENT', values: [1] }],
            },
            {
              id: 'A-1-1',
              branchId: 'ARCANE',
              tier: 1,
              requiredSpentPoints: 0,
              name: 'Arcane',
              englishName: 'Arcane',
              maxRank: 1,
              prerequisites: [],
              description: 'Test',
              version: 1,
              modifiers: [{ type: 'StatModifier', key: 'ATTACK_POWER_PERCENT', values: [1] }],
            },
          ],
        },
        abilityIds: [],
      },
    })

    await wrapper.get('[data-testid="talent-branch"]').setValue('ARCANE')
    const emitted = wrapper.emitted('update:entity') ?? []
    const next = emitted[emitted.length - 1]?.[0] as {
      branches: Array<{ id: string; nodeCount: number }>
      nodes: Array<{ id: string; branchId: string }>
    }

    expect(next.nodes.find(node => node.id === 'F-1-1')?.branchId).toBe('ARCANE')
    expect(next.branches.find(branch => branch.id === 'FIRE')?.nodeCount).toBe(0)
    expect(next.branches.find(branch => branch.id === 'ARCANE')?.nodeCount).toBe(2)
  })

  it('resizes talent rank values when max rank changes', async () => {
    const wrapper = mount(AdminTalentTreeForm, {
      props: {
        entity: {
          id: 'TEST_TREE',
          classId: 'MAGE',
          maxSpendablePoints: 10,
          version: 1,
          branches: [{ id: 'FIRE', name: 'Fire', fantasy: '', nodeCount: 1 }],
          nodes: [{
            id: 'F-1-1',
            branchId: 'FIRE',
            tier: 1,
            requiredSpentPoints: 0,
            name: 'Heat',
            englishName: 'Heat',
            maxRank: 2,
            prerequisites: [],
            description: 'Test',
            version: 1,
            modifiers: [{ type: 'StatModifier', key: 'ATTACK_POWER_PERCENT', values: [2, 4] }],
          }],
        },
        abilityIds: [],
      },
    })

    await wrapper.get('[data-testid="talent-max-rank"]').setValue('3')
    const emitted = wrapper.emitted('update:entity') ?? []
    const next = emitted[emitted.length - 1]?.[0] as { nodes: Array<{ maxRank: number; modifiers: Array<{ values: number[] }> }> }

    expect(next.nodes[0]?.maxRank).toBe(3)
    expect(next.nodes[0]?.modifiers[0]?.values).toEqual([2, 4, 4])
  })
})
