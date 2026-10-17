const PVP_COMBAT_LOG_SETTING_KEY = 'elyndor:send-pvp-combat-logs'

// PvP reports are opt-in; the on-screen combat journal is always available.
export function isPvpCombatLogEnabled(): boolean {
  try {
    return globalThis.localStorage?.getItem(PVP_COMBAT_LOG_SETTING_KEY) === '1'
  } catch {
    return false
  }
}

export function setPvpCombatLogEnabled(enabled: boolean): void {
  try {
    globalThis.localStorage?.setItem(PVP_COMBAT_LOG_SETTING_KEY, enabled ? '1' : '0')
  } catch {
    // Optional beta diagnostics should never prevent gameplay.
  }
}
