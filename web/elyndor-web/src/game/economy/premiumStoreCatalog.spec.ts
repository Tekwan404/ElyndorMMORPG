import { describe, expect, it } from 'vitest'

import type { PremiumStoreSnapshot } from '@/api/contracts'
import { buildPremiumStoreProducts, purchaseLabel } from '@/game/economy/premiumStoreCatalog'

function snapshot(canPurchase = true): PremiumStoreSnapshot {
  return {
    crystalBalance: 8280,
    offers: [
      {
        sku: 'SPATIAL_EXPANDED_RING',
        itemDefinitionId: 'SPATIAL_EXPANDED_RING',
        name: 'Кольцо расширенного пространства',
        description: 'Увеличивает вместимость инвентаря на 15 ячеек.',
        rarity: 'Rare',
        iconId: 'spatial_expanded_ring',
        quantity: 1,
        crystalPrice: 350,
        canPurchase,
      },
    ],
  } as PremiumStoreSnapshot
}

describe('premium storefront catalog', () => {
  it('keeps server price authoritative while applying storefront presentation metadata', () => {
    const ring = buildPremiumStoreProducts(snapshot()).find((product) => product.id === 'wanderer-spatial-ring')

    expect(ring).toMatchObject({
      title: 'Пространственное кольцо Странника',
      subtitle: '+15 ячеек инвентаря',
      price: 350,
      inventoryCapacity: 15,
      backendBacked: true,
      canPurchase: true,
      owned: false,
    })
  })

  it('maps exhausted one-time offer to owned state instead of offering a repurchase', () => {
    const ring = buildPremiumStoreProducts(snapshot(false)).find((product) => product.id === 'wanderer-spatial-ring')

    expect(ring?.owned).toBe(true)
    expect(ring && purchaseLabel(ring)).toBe('КУПЛЕНО')
  })

  it('keeps demo consumables repeatable and separate from backend purchase flow', () => {
    const stones = buildPremiumStoreProducts(snapshot()).find((product) => product.id === 'reforge-stones-10')

    expect(stones).toMatchObject({
      quantity: 10,
      price: 25,
      repeatable: true,
      backendBacked: false,
    })
  })
})
