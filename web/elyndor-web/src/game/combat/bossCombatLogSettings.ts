const BOSS_COMBAT_LOG_SETTING_KEY = 'elyndor:send-boss-combat-logs'

export function isBossCombatLogEnabled(): boolean {
  try {
    return globalThis.localStorage?.getItem(BOSS_COMBAT_LOG_SETTING_KEY) === '1'
  } catch {
    return false
  }
}

export function setBossCombatLogEnabled(enabled: boolean): void {
  try {
    globalThis.localStorage?.setItem(BOSS_COMBAT_LOG_SETTING_KEY, enabled ? '1' : '0')
  } catch {
    // The beta diagnostic preference is best-effort when storage is unavailable.
  }
}
