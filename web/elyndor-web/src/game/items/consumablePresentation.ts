import type { ConsumableAction } from '@/api/contracts'

function resourceLabel(resourceType: string | null): string {
  if (resourceType === 'MANA') return 'маны'
  if (resourceType === 'FOCUS') return 'Focus'
  if (resourceType === 'RAGE') return 'Rage'
  return resourceType ?? 'ресурса'
}

export function consumableActionLabel(action: ConsumableAction): string {
  if (action.type === 'RestoreHp') return `+${action.amount} здоровья`
  if (action.type === 'RestoreResource') {
    return `+${action.amount} ${resourceLabel(action.resourceType)}`
  }
  if (action.type === 'ApplyEffect') {
    return action.effectId ? `Эффект: ${action.effectId}` : 'Накладывает эффект'
  }
  if (action.dispelCategory) return `Снимает ${action.dispelCategory}`
  if (action.effectId) return `Снимает эффект ${action.effectId}`
  return 'Снимает отрицательный эффект'
}

export function consumableSummary(
  actions: ConsumableAction[],
  cooldownSeconds: number,
): string {
  const effect = actions.map(consumableActionLabel).join(' · ')
  return cooldownSeconds > 0
    ? `${effect}. Кулдаун категории: ${cooldownSeconds} сек.`
    : effect
}
