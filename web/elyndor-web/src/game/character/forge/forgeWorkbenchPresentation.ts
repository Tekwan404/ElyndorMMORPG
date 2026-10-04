import type { InventoryItem, ItemRarity } from '@/api/contracts'
import type { ItemSalvagePreviewV2 } from '@/api/itemEnhancementContracts'

export type ForgeCategory = 'weapon' | 'armor' | 'accessory' | 'artifact'
export function forgeCategory(item: InventoryItem): ForgeCategory {
  if (String(item.type) === 'SpatialArtifact') return 'artifact'
  if (item.weaponCategory || item.slot === 'Weapon' || item.slot === 'MainHand') return 'weapon'
  if (['Accessory', 'Ring1', 'Ring2', 'Amulet'].includes(item.slot ?? '')) return 'accessory'
  return 'armor'
}
export function canSalvage(item: InventoryItem): boolean {
  return (
    item.type === 'Equipment' &&
    item.equippedSlot === null &&
    !item.isLocked &&
    !item.transactionLocked
  )
}
export function itemRarityLabel(rarity: ItemRarity): string {
  return {
    Common: 'Обычный',
    Uncommon: 'Необычный',
    Rare: 'Редкий',
    Epic: 'Эпический',
    Legendary: 'Легендарный',
    Unique: 'Уникальный',
  }[rarity]
}
export function salvageMaterials(
  previews: readonly ItemSalvagePreviewV2[],
): Array<[string, number]> {
  const totals = new Map<string, number>()
  const add = (id: string | null, count: number) => {
    if (id && count > 0) totals.set(id, (totals.get(id) ?? 0) + count)
  }
  for (const { reward, enhancementRefund: refund } of previews) {
    add(reward.reforgeStoneItemId, reward.reforgeStoneQuantity)
    add(reward.materialItemId, reward.materialQuantity)
    add(refund.enhancementMaterialItemId, refund.enhancementMaterialQuantity)
    add(refund.catalystItemId, refund.catalystQuantity)
  }
  return [...totals]
}
