import type { ConsumableAction } from '@/api/contracts'

function resourceLabel(resourceType: string | null): string {
  if (resourceType === 'MANA') return 'маны'
  if (resourceType === 'FOCUS') return 'концентрации'
  if (resourceType === 'RAGE') return 'ярости'
  return 'ресурса'
}

export function consumableActionLabel(action: ConsumableAction): string {
  if (action.type === 'RestoreHp') return `+${action.amount} здоровья`
  if (action.type === 'RestoreResource') {
    return `+${action.amount} ${resourceLabel(action.resourceType)}`
  }
  if (action.type === 'ApplyEffect') return 'Даёт временный эффект'
  if (action.type === 'RemoveEffect') return 'Снимает отрицательный эффект'
  return 'Даёт временный эффект'
}

export function consumableSummary(
  actions: ConsumableAction[],
  cooldownSeconds: number,
): string {
  const effect = actions.map(consumableActionLabel).join(' · ')
  return cooldownSeconds > 0
    ? `${effect} · повторное использование через ${cooldownSeconds} сек.`
    : effect
}
