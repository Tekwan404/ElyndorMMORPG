const labels: Readonly<Record<string, string>> = {
  Common: 'Обычный',
  Uncommon: 'Необычный',
  Rare: 'Редкий',
  Epic: 'Эпический',
  Legendary: 'Легендарный',
  Unique: 'Уникальный',
}
export function itemRarityLabel(rarity: string): string {
  return labels[rarity] ?? rarity
}
