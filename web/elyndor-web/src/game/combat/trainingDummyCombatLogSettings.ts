// Keep the original storage key so existing beta testers retain their preference.
const TRAINING_DUMMY_COMBAT_LOG_SETTING_KEY = 'elyndor:send-boss-combat-logs'

export function isTrainingDummyCombatLogEnabled(): boolean {
  try {
    const stored = globalThis.localStorage?.getItem(TRAINING_DUMMY_COMBAT_LOG_SETTING_KEY)
    return stored !== '0'
  } catch {
    return true
  }
}

export function setTrainingDummyCombatLogEnabled(enabled: boolean): void {
  try {
    globalThis.localStorage?.setItem(
      TRAINING_DUMMY_COMBAT_LOG_SETTING_KEY,
      enabled ? '1' : '0',
    )
  } catch {
    // The beta diagnostic preference is best-effort when storage is unavailable.
  }
}
