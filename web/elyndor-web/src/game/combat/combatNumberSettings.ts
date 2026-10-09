export type CombatNumberDensity = 'full' | 'balanced' | 'minimal'

export interface CombatNumberSettings {
  density: CombatNumberDensity
  emphasizeCrits: boolean
  hitEffects: boolean
  showPeriodicDamage: boolean
  showThreatTable: boolean
}

export const DEFAULT_COMBAT_NUMBER_SETTINGS: Readonly<CombatNumberSettings> = {
  density: 'balanced',
  emphasizeCrits: true,
  hitEffects: true,
  showPeriodicDamage: true,
  showThreatTable: false,
}

const STORAGE_KEY = 'elyndor:combat-numbers:v1'

export function normalizeCombatNumberSettings(value: unknown): CombatNumberSettings {
  const settings = value !== null && typeof value === 'object'
    ? value as Partial<CombatNumberSettings>
    : {}
  return {
    density: settings.density === 'full' || settings.density === 'minimal'
      ? settings.density
      : 'balanced',
    emphasizeCrits: typeof settings.emphasizeCrits === 'boolean'
      ? settings.emphasizeCrits
      : true,
    hitEffects: typeof settings.hitEffects === 'boolean' ? settings.hitEffects : true,
    showPeriodicDamage: typeof settings.showPeriodicDamage === 'boolean'
      ? settings.showPeriodicDamage
      : true,
    showThreatTable: typeof settings.showThreatTable === 'boolean'
      ? settings.showThreatTable
      : false,
  }
}

export function loadCombatNumberSettings(): CombatNumberSettings {
  try {
    const stored = globalThis.localStorage?.getItem(STORAGE_KEY)
    return normalizeCombatNumberSettings(stored ? JSON.parse(stored) : null)
  } catch {
    return { ...DEFAULT_COMBAT_NUMBER_SETTINGS }
  }
}

export function saveCombatNumberSettings(settings: CombatNumberSettings): void {
  try {
    globalThis.localStorage?.setItem(
      STORAGE_KEY,
      JSON.stringify(normalizeCombatNumberSettings(settings)),
    )
  } catch {
    // Best effort on browsers where storage is unavailable (including Telegram WebView).
  }
}
