import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import type { InventoryItem } from '@/api/contracts'
import ItemSetSummary from './ItemSetSummary.vue'

describe('ItemSetSummary', () => {
  it('shows six of eight with the highest bonus active and two optional pieces', () => {
    const items = Array.from({ length: 6 }, (_, i) => ({
      id: `${i}`,
      definitionId: `PIECE_${i}`,
      setId: 'SET_TEST',
      equippedSlot: 'Chest',
      setSummary: {
        name: 'Оплот Сломанного Щита',
        totalPieces: 8,
        bonuses: [
          { requiredPieces: 2, description: 'HP + Armor' },
          { requiredPieces: 4, description: 'Block усиливает Revenge' },
          { requiredPieces: 6, description: 'Shield Block запускает окно' },
        ],
      },
    })) as InventoryItem[]
    const wrapper = mount(ItemSetSummary, { props: { setId: 'SET_TEST', items } })
    expect(wrapper.text()).toContain('Оплот Сломанного Щита — 6/8')
    expect(wrapper.findAll('[data-bonus-active="true"]')).toHaveLength(3)
    expect(wrapper.text()).toContain('Осталось доступно частей комплекта: 2')
    expect(wrapper.text()).not.toContain('8 предметов —')
  })
})
