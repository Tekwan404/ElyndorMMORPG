import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { PremiumStoreSnapshot } from '@/api/contracts'
import PremiumStoreView from '@/game/economy/views/PremiumStoreView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

describe('PremiumStoreView', () => {
  it('accepts 51 packs, previews totals and submits one purchase', async () => {
    const session = useGameSessionStore()
    const rich = snapshot()
    rich.crystalBalance = 5000
    vi.spyOn(session, 'getPremiumStore').mockResolvedValue(rich)
    const purchase = vi.spyOn(session, 'buyPremiumStoreOffer').mockResolvedValue({ crystalBalance: 3725 })
    const wrapper = mount(PremiumStoreView, { global: { stubs: { Teleport: true } } })
    await flushPromises()
    await wrapper.get('[data-product-id="reforge-stones-10"]').trigger('click')
    await wrapper.get('[data-pack-count]').setValue('51')
    expect(wrapper.get('[data-purchase-summary]').text()).toContain('510')
    expect(wrapper.get('[data-purchase-summary]').text().replace(/\s/g, '')).toContain('1275')
    await wrapper.get('[data-purchase-cta]').trigger('click')
    await flushPromises()
    expect(purchase).toHaveBeenCalledTimes(1)
    expect(purchase).toHaveBeenCalledWith('REFORGE_STONES_SMALL', 51)
  })
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.restoreAllMocks()
  })

  it('previews 50 packs and rejects fractional, empty and excessive quantities', async () => {
    const session = useGameSessionStore()
    const rich = snapshot()
    rich.crystalBalance = 5000
    vi.spyOn(session, 'getPremiumStore').mockResolvedValue(rich)
    const purchase = vi.spyOn(session, 'buyPremiumStoreOffer')
    const wrapper = mount(PremiumStoreView, { global: { stubs: { Teleport: true } } })
    await flushPromises()
    await wrapper.get('[data-product-id="reforge-stones-10"]').trigger('click')
    await wrapper.get('[data-pack-count]').setValue('50')
    expect(wrapper.get('[data-purchase-cta]').text().replace(/\s/g, '')).toContain('КУПИТЬ×500·✦1250')
    for (const value of ['0', '-1', '1.5', '', '10001']) {
      await wrapper.get('[data-pack-count]').setValue(value)
      expect(wrapper.get('[data-purchase-cta]').attributes('disabled')).toBeDefined()
    }
    expect(purchase).not.toHaveBeenCalled()
  })

  it('honors server availability instead of bypassing purchase limits', async () => {
    const session = useGameSessionStore()
    const limited = snapshot()
    limited.offers[0]!.canPurchase = false
    limited.offers[0]!.maxPackCount = 0
    vi.spyOn(session, 'getPremiumStore').mockResolvedValue(limited)
    const wrapper = mount(PremiumStoreView, { global: { stubs: { Teleport: true } } })
    await flushPromises()
    await wrapper.get('[data-product-id="enhancement-ore-20"]').trigger('click')
    expect(wrapper.get('[data-purchase-cta]').attributes('disabled')).toBeDefined()
  })

  it('presents server-backed forge supplies and opens the real item preview', async () => {
    const session = useGameSessionStore()
    vi.spyOn(session, 'getPremiumStore').mockResolvedValue(snapshot())

    const wrapper = mount(PremiumStoreView, { global: { stubs: { Teleport: true } } })
    await flushPromises()

    expect(wrapper.get('[data-forge-supplies]').text()).toContain('Кузнечные припасы')
    expect(wrapper.get('[data-forge-supplies]').text()).toContain('БЕЗ ЛИМИТА')
    expect(wrapper.get('[data-product-id="enhancement-ore-20"]').text()).toContain('Закалочная руда')
    expect(wrapper.get('[data-product-id="enhancement-ore-20"]').text()).toContain('×20')

    await wrapper.get('[data-product-id="enhancement-ore-20"]').trigger('click')

    expect(wrapper.get('[role="dialog"]').text()).toContain('Закалочная руда')
    expect(wrapper.get('[data-purchase-summary]').text()).toContain('После покупки')
    expect(wrapper.get('[data-purchase-summary]').text()).toContain('70')
    expect(wrapper.get('[role="dialog"]').text()).toContain('Можно покупать повторно')
    expect(wrapper.get('.product-sheet').classes()).toContain('product-sheet--consumable')
    expect(wrapper.find('[role="dialog"] [data-icon-id="ore"]').exists()).toBe(true)
  })

  it('purchases one pack by default for a repeatable offer', async () => {
    const session = useGameSessionStore()
    const stale = snapshot()
    stale.crystalBalance = 9190
    stale.offers[0]!.canPurchase = true
    vi.spyOn(session, 'getPremiumStore').mockResolvedValue(stale)
    const purchase = vi.spyOn(session, 'buyPremiumStoreOffer').mockResolvedValue({ crystalBalance: 9160 })

    const wrapper = mount(PremiumStoreView, { global: { stubs: { Teleport: true } } })
    await flushPromises()
    await wrapper.get('[data-product-id="enhancement-ore-20"]').trigger('click')

    const cta = wrapper.get('[data-purchase-cta]')
    expect(cta.attributes('disabled')).toBeUndefined()

    await cta.trigger('click')
    await flushPromises()

    expect(purchase).toHaveBeenCalledWith('ENHANCEMENT_ORE_SMALL', 1)
  })

  it('explains insufficient currency instead of leaving an ambiguous disabled purchase button', async () => {
    const session = useGameSessionStore()
    const poor = snapshot()
    poor.crystalBalance = 10
    vi.spyOn(session, 'getPremiumStore').mockResolvedValue(poor)

    const wrapper = mount(PremiumStoreView, { global: { stubs: { Teleport: true } } })
    await flushPromises()
    await wrapper.get('[data-product-id="enhancement-ore-20"]').trigger('click')

    const dialog = wrapper.get('[role="dialog"]')
    expect(dialog.text()).toContain('НЕДОСТАТОЧНО ОСКОЛКОВ')
    expect(dialog.get('[data-purchase-cta]').attributes('disabled')).toBeDefined()
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
