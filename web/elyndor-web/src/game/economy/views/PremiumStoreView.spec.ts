import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { PremiumStoreSnapshot } from '@/api/contracts'
import PremiumStoreView from '@/game/economy/views/PremiumStoreView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

describe('PremiumStoreView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.restoreAllMocks()
  })

  it('presents server-backed forge supplies and opens the real item preview', async () => {
    const session = useGameSessionStore()
    vi.spyOn(session, 'getPremiumStore').mockResolvedValue(snapshot())

    const wrapper = mount(PremiumStoreView)
    await flushPromises()

    expect(wrapper.get('[data-forge-supplies]').text()).toContain('Кузнечные припасы')
    expect(wrapper.get('[data-product-id="enhancement-ore-20"]').text()).toContain('Закалочная руда')
    expect(wrapper.get('[data-product-id="enhancement-ore-20"]').text()).toContain('×20')

    await wrapper.get('[data-product-id="enhancement-ore-20"]').trigger('click')

    expect(wrapper.get('[role="dialog"]').text()).toContain('Закалочная руда')
    expect(wrapper.find('[role="dialog"] [data-icon-id="ore"]').exists()).toBe(true)
  })
})

function snapshot(): PremiumStoreSnapshot {
  return {
    crystalBalance: 100,
    offers: [
      {
        sku: 'ENHANCEMENT_ORE_SMALL',
        itemDefinitionId: 'ENHANCEMENT_ORE',
        name: 'Закалочная руда',
        description: 'Материал для усиления снаряжения от +1 до +5.',
        rarity: 'Uncommon',
        iconId: 'ore',
        quantity: 20,
        crystalPrice: 30,
        canPurchase: true,
      },
      {
        sku: 'REFORGE_STONES_SMALL',
        itemDefinitionId: 'REFORGE_STONE',
        name: 'Камень перековки',
        description: 'Материал для перековки.',
        rarity: 'Uncommon',
        iconId: 'ore',
        quantity: 10,
        crystalPrice: 25,
        canPurchase: true,
      },
      {
        sku: 'FORGE_SCRAP_SMALL',
        itemDefinitionId: 'FORGE_SCRAP',
        name: 'Кузнечный лом',
        description: 'Материал для кузнечных работ.',
        rarity: 'Common',
        iconId: 'ore',
        quantity: 20,
        crystalPrice: 20,
        canPurchase: true,
      },
    ],
  } as PremiumStoreSnapshot
}
