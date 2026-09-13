const COMBAT_HOTBAR_SETTING_PREFIX = 'elyndor:combat-hotbar:v1:'

function storageKey(characterId: string): string {
  return `${COMBAT_HOTBAR_SETTING_PREFIX}${characterId}`
}

export function normalizeCombatHotbarOrder(
  availableAbilityIds: readonly string[],
  preferredOrder: readonly string[],
): string[] {
  const available = [...new Set(availableAbilityIds.filter(Boolean))]
  const availableSet = new Set(available)
  const normalized: string[] = []
  const seen = new Set<string>()

  for (const abilityId of preferredOrder) {
    if (!availableSet.has(abilityId) || seen.has(abilityId)) continue
    normalized.push(abilityId)
    seen.add(abilityId)
  }

  for (const abilityId of available) {
    if (seen.has(abilityId)) continue
    normalized.push(abilityId)
    seen.add(abilityId)
  }

  return normalized
}

export function loadCombatHotbarOrder(
  characterId: string,
  availableAbilityIds: readonly string[],
): string[] {
  if (!characterId) return normalizeCombatHotbarOrder(availableAbilityIds, [])

  try {
    const raw = globalThis.localStorage?.getItem(storageKey(characterId))
    if (!raw) return normalizeCombatHotbarOrder(availableAbilityIds, [])
    const stored = JSON.parse(raw)
    return normalizeCombatHotbarOrder(
      availableAbilityIds,
      Array.isArray(stored) ? stored.filter((value): value is string => typeof value === 'string') : [],
    )
  } catch {
    return normalizeCombatHotbarOrder(availableAbilityIds, [])
  }
}

export function saveCombatHotbarOrder(characterId: string, order: readonly string[]): void {
  if (!characterId) return
  try {
    globalThis.localStorage?.setItem(storageKey(characterId), JSON.stringify([...order]))
  } catch {
    // Hotbar customization is best-effort when storage is unavailable.
  }
}

export function resetCombatHotbarOrder(characterId: string): void {
  if (!characterId) return
  try {
    globalThis.localStorage?.removeItem(storageKey(characterId))
  } catch {
    // Hotbar customization is best-effort when storage is unavailable.
  }
}

export function orderCombatAbilities<TAbility extends { id: string }>(
  characterId: string,
  abilities: readonly TAbility[],
): TAbility[] {
  const byId = new Map(abilities.map(ability => [ability.id, ability]))
  return loadCombatHotbarOrder(characterId, abilities.map(ability => ability.id))
    .map(abilityId => byId.get(abilityId))
    .filter((ability): ability is TAbility => ability !== undefined)
}
