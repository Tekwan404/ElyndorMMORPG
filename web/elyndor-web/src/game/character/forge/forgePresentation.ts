import type { GeneratedItemSummary, InventoryItem, ItemAffix } from '@/api/contracts'

export interface ForgeItemAvailability {
  available: boolean
  reason: string | null
}

export interface ReforgeResultAffixes {
  current: ItemAffix | null
  proposed: ItemAffix | null
}

export function availableForgeMaterialQuantity(
  items: readonly InventoryItem[],
  definitionId: string,
): number {
  return items
    .filter(item => item.definitionId === definitionId && !item.isLocked && !item.transactionLocked)
    .reduce((total, item) => total + item.quantity, 0)
}

export function forgeItemAvailability(item: InventoryItem): ForgeItemAvailability {
  if (item.type !== 'Equipment' || !item.generatedItem) {
    return { available: false, reason: 'Этот предмет нельзя перековать.' }
  }
  if (item.isLocked) {
    return { available: false, reason: 'Предмет защищён. Снимите блокировку в инвентаре.' }
  }
  if (item.transactionLocked) {
    return { available: false, reason: 'С этим предметом уже выполняется действие.' }
  }
  if (!item.generatedItem.affixes.some(affix => !affix.isGuaranteed)) {
    return { available: false, reason: 'У предмета нет характеристик для перековки.' }
  }
  return { available: true, reason: null }
}

export function shouldRestorePendingReforge(item: InventoryItem): boolean {
  return item.transactionLocked === true
}

export function forgeableAffixes(item: InventoryItem) {
  return item.generatedItem?.affixes.filter(affix => !affix.isGuaranteed) ?? []
}

export function reforgeResultAffixes(
  current: GeneratedItemSummary,
  proposed: GeneratedItemSummary,
  slotKey: string,
): ReforgeResultAffixes {
  return {
    current: current.affixes.find(affix => affix.slotKey === slotKey) ?? null,
    proposed: proposed.affixes.find(affix => affix.slotKey === slotKey) ?? null,
  }
}

const percentageStats = new Set([
  'CRITICAL_CHANCE',
  'CRITICAL_DAMAGE',
  'ACCURACY',
  'ATTACK_SPEED',
  'DODGE',
  'ARMOR_PENETRATION',
  'MAGIC_PENETRATION',
  'BLOCK_CHANCE',
])

export function forgeAffixQuality(affix: Pick<ItemAffix, 'value' | 'min' | 'max'>): number {
  if (affix.max <= affix.min) return 0
  const quality = ((affix.value - affix.min) / (affix.max - affix.min)) * 100
  return Math.round(Math.max(0, Math.min(100, quality)) * 100) / 100
}

export function forgeStatValue(statId: string, value: number): string {
  const formatted = Number.isInteger(value) ? String(value) : value.toFixed(1).replace(/\.0$/, '')
  return `+${formatted}${percentageStats.has(statId) ? '%' : ''}`
}

export function forgeStatRange(statId: string, min: number, max: number): string {
  const format = (value: number) =>
    Number.isInteger(value) ? String(value) : value.toFixed(1).replace(/\.0$/, '')
  const suffix = percentageStats.has(statId) ? '%' : ''
  return `${format(min)}–${format(max)}${suffix}`
}

export function forgePercent(value: number): string {
  return `${value.toFixed(2).replace(/\.00$/, '')}%`
}

export function forgeStatLabel(statId: string): string {
  const labels: Record<string, string> = {
    STRENGTH: 'Сила',
    AGILITY: 'Ловкость',
    INTELLECT: 'Интеллект',
    STAMINA: 'Выносливость',
    MAX_HP: 'Макс. здоровье',
    ATTACK_POWER: 'Сила атаки',
    SPELL_POWER: 'Сила заклинаний',
    ARMOR: 'Броня',
    MAGIC_RESISTANCE: 'Сопротивление магии',
    CRITICAL_CHANCE: 'Крит. шанс',
    CRITICAL_DAMAGE: 'Крит. урон',
    ACCURACY: 'Точность',
    ATTACK_SPEED: 'Скорость атаки',
    DODGE: 'Уклонение',
    ARMOR_PENETRATION: 'Пробивание брони',
    MAGIC_PENETRATION: 'Пробивание магии',
    MAX_RESOURCE: 'Макс. ресурс',
    BLOCK_CHANCE: 'Шанс блока',
    BLOCK_VALUE: 'Сила блока',
    WEAPON_DAMAGE: 'Урон оружия',
  }
  return labels[statId] ?? 'Характеристика'
}
