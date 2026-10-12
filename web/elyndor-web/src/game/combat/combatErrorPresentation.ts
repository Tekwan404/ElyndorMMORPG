// CombatErrorCodes in Elyndor.Core is the canonical wire contract.
// Classify the server's actual codes; routine gameplay rejections are not network failures.
export const COMBAT_ERRORS = {
  abilityOnCooldown: 'ability_on_cooldown',
  insufficientResource: 'insufficient_resource',
  invalidTarget: 'invalid_target',
  actorDead: 'actor_dead',
  duplicateCommand: 'duplicate_command',
  combatNotFound: 'combat_not_found',
  combatEnded: 'combat_ended',
  commandRejected: 'combat_command_rejected',
  rateLimited: 'rate_limited',
} as const

const expectedRejections = new Set<string>([
  COMBAT_ERRORS.abilityOnCooldown,
  COMBAT_ERRORS.insufficientResource,
  COMBAT_ERRORS.invalidTarget,
  COMBAT_ERRORS.actorDead,
  COMBAT_ERRORS.commandRejected,
  COMBAT_ERRORS.rateLimited,
  'ability_not_known',
  'combat_participant_not_active',
  'combat_consumable_on_cooldown',
  'combat_consumable_not_needed',
  'combat_consumable_unavailable',
  'combat_auto_attack_unavailable',
])

export function combatErrorPresentation(code: string | null): { message: string; showToast: boolean } {
  if (code === null) return { message: '', showToast: false }
  if (expectedRejections.has(code) || code === COMBAT_ERRORS.duplicateCommand) {
    return { message: '', showToast: false }
  }
  if (code === COMBAT_ERRORS.combatNotFound || code === COMBAT_ERRORS.combatEnded) {
    return { message: 'Бой уже завершён. Вернитесь в локацию.', showToast: true }
  }
  if (code.startsWith('combat_hub_') || code.startsWith('combat_signalr_')
    || code.startsWith('combat_negotiate_') || code === 'combat_realtime_timeout') {
    return { message: 'Не удалось связаться с боем. Повторите подключение.', showToast: true }
  }
  return { message: 'Не удалось выполнить действие. Повторите попытку.', showToast: true }
}
