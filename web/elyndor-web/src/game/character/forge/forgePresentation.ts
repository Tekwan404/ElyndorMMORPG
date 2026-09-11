import type { InventoryItem } from '@/api/contracts'

export interface ForgeItemAvailability {
  available: boolean
  reason: string | null
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
    return { available: false, reason: 'С этим предметом уже выполняется операция.' }
  }
  if (!item.generatedItem.affixes.some(affix => !affix.isGuaranteed)) {
    return { available: false, reason: 'У предмета нет характеристик, доступных для перековки.' }
  }
  return { available: true, reason: null }
}

export function forgeableAffixes(item: InventoryItem) {
  return item.generatedItem?.affixes.filter(affix => !affix.isGuaranteed) ?? []
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
  }
  return labels[statId] ?? statId
}
