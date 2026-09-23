import type { PremiumStoreSnapshot } from '@/api/contracts'

export type PremiumStoreCategory = 'recommended' | 'cosmetics' | 'artifacts' | 'convenience' | 'services'
export type PremiumStoreProductType = 'cosmetic' | 'spatial-artifact' | 'consumable' | 'service' | 'bundle'
export type PremiumStoreBadge = 'new' | 'popular' | 'limited'

export const PREMIUM_CURRENCY = {
  id: 'CRYSTAL',
  displayName: 'Осколки Эфира',
  symbol: '✦',
} as const

export interface PremiumStoreProduct {
  id: string
  sku?: string
  category: PremiumStoreCategory
  type: PremiumStoreProductType
  title: string
  subtitle?: string
  description: string
  artwork: 'fire-mage' | 'spatial-ring' | 'reforge' | 'service' | 'profile-frame' | 'battle-entry' | 'ash-border' | 'item'
  price: number
  quantity?: number
  badge?: PremiumStoreBadge
  previewable?: boolean
  repeatable: boolean
  bundle?: readonly string[]
  inventoryCapacity?: number
  cosmeticId?: string
  serviceType?: string
  unitPriceLabel?: string
  featured?: boolean
  owned?: boolean
  equipped?: boolean
  canPurchase?: boolean
  iconId?: string
  itemDefinitionId?: string
  rarity?: string
  backendBacked: boolean
}

type Offer = PremiumStoreSnapshot['offers'][number]

type Presentation = Omit<PremiumStoreProduct, 'price' | 'owned' | 'canPurchase' | 'iconId' | 'itemDefinitionId' | 'rarity' | 'backendBacked'> & {
  fallbackPrice: number
}

const STORE_PRESENTATION: readonly Presentation[] = [
  {
    id: 'fire-sorceress',
    category: 'cosmetics',
    type: 'cosmetic',
    title: 'Огненная чародейка',
    subtitle: 'Облик мага',
    description: 'Изменяет внешний вид персонажа. Не влияет на характеристики.',
    artwork: 'fire-mage',
    fallbackPrice: 650,
    badge: 'new',
    previewable: true,
    repeatable: false,
    cosmeticId: 'COSMETIC_MAGE_FIRE_SORCERESS',
    featured: true,
  },
  {
    id: 'wanderer-spatial-ring',
    sku: 'SPATIAL_EXPANDED_RING',
    category: 'artifacts',
    type: 'spatial-artifact',
    title: 'Пространственное кольцо Странника',
    subtitle: '+15 ячеек инвентаря',
    description: 'Расширяет единый инвентарь персонажа на 15 ячеек. Не создаёт отдельную сумку или отдельный инвентарь.',
    artwork: 'spatial-ring',
    fallbackPrice: 350,
    repeatable: false,
    inventoryCapacity: 15,
  },
  {
    id: 'reforge-stones-10',
    category: 'convenience',
    type: 'consumable',
    title: 'Камни перековки',
    subtitle: '×10',
    description: 'Позволяют повторно изменить характеристики предмета.',
    artwork: 'reforge',
    fallbackPrice: 25,
    quantity: 10,
    badge: 'popular',
    repeatable: true,
    unitPriceLabel: '1 шт. = 2,5 ✦',
  },
  {
    id: 'rename-character',
    category: 'services',
    type: 'service',
    title: 'Смена имени',
    description: 'Позволяет изменить имя персонажа.',
    artwork: 'service',
    fallbackPrice: 150,
    repeatable: true,
    serviceType: 'rename-character',
  },
  {
    id: 'profile-frame',
    category: 'cosmetics',
    type: 'cosmetic',
    title: 'Рамка профиля',
    description: 'Косметическая рамка профиля. Не влияет на характеристики.',
    artwork: 'profile-frame',
    fallbackPrice: 120,
    repeatable: false,
    cosmeticId: 'COSMETIC_PROFILE_FRAME_ASH',
    previewable: true,
  },
  {
    id: 'battle-entry-effect',
    category: 'cosmetics',
    type: 'cosmetic',
    title: 'Эффект входа в бой',
    description: 'Косметический battlefield-эффект появления персонажа.',
    artwork: 'battle-entry',
    fallbackPrice: 200,
    repeatable: false,
    cosmeticId: 'COSMETIC_BATTLE_ENTRY_ASH',
    previewable: true,
  },
  {
    id: 'ash-border-collection',
    category: 'recommended',
    type: 'bundle',
    title: 'Коллекция «Пепельная граница»',
    subtitle: 'Облик + рамка + эффект входа',
    description: 'Тематическая коллекция косметики Пепельной границы. Состав набора определяется контентом, а не разметкой экрана.',
    artwork: 'ash-border',
    fallbackPrice: 1200,
    repeatable: false,
    bundle: ['fire-sorceress', 'profile-frame', 'battle-entry-effect'],
    badge: 'new',
  },
]

const SPATIAL_CAPACITY_BY_ITEM: Readonly<Record<string, number>> = {
  SPATIAL_CRACKED_RING: 5,
  SPATIAL_MINOR_RING: 10,
  SPATIAL_EXPANDED_RING: 15,
  SPATIAL_SEAL: 20,
  SPATIAL_BOTTOMLESS_RING: 25,
  SPATIAL_POCKET_SHARD: 30,
  SPATIAL_VOID_SEAL: 40,
}

function fromServerOffer(offer: Offer): PremiumStoreProduct {
  const capacity = SPATIAL_CAPACITY_BY_ITEM[offer.itemDefinitionId]
  return {
    id: `offer:${offer.sku}`,
    sku: offer.sku,
    category: capacity ? 'artifacts' : 'convenience',
    type: capacity ? 'spatial-artifact' : 'consumable',
    title: offer.name,
    subtitle: capacity ? `+${capacity} ячеек инвентаря` : offer.quantity > 1 ? `×${offer.quantity}` : undefined,
    description: offer.description,
    artwork: capacity ? 'spatial-ring' : 'item',
    price: offer.crystalPrice,
    quantity: offer.quantity,
    repeatable: offer.canPurchase,
    inventoryCapacity: capacity,
    owned: !offer.canPurchase,
    canPurchase: offer.canPurchase,
    iconId: offer.iconId ?? undefined,
    itemDefinitionId: offer.itemDefinitionId,
    rarity: offer.rarity,
    backendBacked: true,
  }
}

export function buildPremiumStoreProducts(snapshot: PremiumStoreSnapshot): PremiumStoreProduct[] {
  const offersBySku = new Map(snapshot.offers.map((offer) => [offer.sku, offer]))
  const usedSkus = new Set<string>()
  const products = STORE_PRESENTATION.map((presentation): PremiumStoreProduct => {
    const offer = presentation.sku ? offersBySku.get(presentation.sku) : undefined
    if (offer) usedSkus.add(offer.sku)
    return {
      ...presentation,
      price: offer?.crystalPrice ?? presentation.fallbackPrice,
      quantity: presentation.quantity ?? offer?.quantity,
      owned: offer ? !offer.canPurchase && !presentation.repeatable : false,
      canPurchase: offer?.canPurchase ?? false,
      iconId: offer?.iconId ?? undefined,
      itemDefinitionId: offer?.itemDefinitionId,
      rarity: offer?.rarity,
      backendBacked: Boolean(offer),
    }
  })

  for (const offer of snapshot.offers) {
    if (!usedSkus.has(offer.sku)) products.push(fromServerOffer(offer))
  }

  return products
}

export function purchaseLabel(product: PremiumStoreProduct): string {
  if (product.equipped) return 'ИСПОЛЬЗУЕТСЯ'
  if (product.owned && !product.repeatable) return product.cosmeticId ? 'НАДЕТЬ' : 'КУПЛЕНО'
  return `${PREMIUM_CURRENCY.symbol} ${product.price} — ПРИОБРЕСТИ`
}

export const PREMIUM_STORE_TABS: ReadonlyArray<{ id: PremiumStoreCategory; label: string }> = [
  { id: 'recommended', label: 'Рекомендуем' },
  { id: 'cosmetics', label: 'Облики' },
  { id: 'artifacts', label: 'Артефакты' },
  { id: 'convenience', label: 'Удобства' },
  { id: 'services', label: 'Услуги' },
]
