const BOSS_COMBAT_LOG_SETTING_KEY = 'elyndor:send-boss-combat-logs'

export function isBossCombatLogEnabled(): boolean {
  try {
    const stored = globalThis.localStorage?.getItem(BOSS_COMBAT_LOG_SETTING_KEY)
    // Closed-beta diagnostics are enabled by default. A tester can still explicitly opt out.
    return stored !== '0'
  } catch {
    return true
  }
}

export function setBossCombatLogEnabled(enabled: boolean): void {
  try {
    globalThis.localStorage?.setItem(BOSS_COMBAT_LOG_SETTING_KEY, enabled ? '1' : '0')
  } catch {
    // The beta diagnostic preference is best-effort when storage is unavailable.
  }
}
