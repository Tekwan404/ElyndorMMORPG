import type { InventoryItem } from '@/api/contracts'

export type InventoryCategory = 'all' | 'equipment' | 'supplies' | 'material'
export type InventorySort = 'default' | 'received' | 'rarity' | 'slot' | 'new' | 'level' | 'name'

export const inventoryCategories = [
  { id: 'all', label: 'Всё' },
  { id: 'equipment', label: 'Экипировка' },
  { id: 'supplies', label: 'Припасы' },
  { id: 'material', label: 'Материалы' },
] as const

export function inventoryCategory(
  item: InventoryItem,
): Exclude<InventoryCategory, 'all'> | 'other' {
  if (item.type === 'Equipment' || String(item.type) === 'SpatialArtifact') return 'equipment'
  if (item.type === 'Consumable' || item.type === 'LootContainer') return 'supplies'
  if (item.type === 'Material') return 'material'
  return 'other'
}

export function inventoryGroup(item: InventoryItem): { id: string; label: string; order: number } {
  if (item.type === 'Equipment') return { id: 'equipment', label: 'Снаряжение', order: 0 }
  if (String(item.type) === 'SpatialArtifact')
    return { id: 'artifact', label: 'Артефакты рюкзака', order: 1 }
  if (item.type === 'Consumable') return { id: 'consumable', label: 'Расходники', order: 2 }
  if (item.type === 'LootContainer')
    return { id: 'container', label: 'Сундуки и награды', order: 3 }
  if (item.type === 'Material') return { id: 'material', label: 'Материалы', order: 4 }
  return { id: 'other', label: 'Другие предметы', order: 5 }
}
