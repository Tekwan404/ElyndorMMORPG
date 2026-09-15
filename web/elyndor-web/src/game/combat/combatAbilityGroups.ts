const auraAbilityIds = new Set([
  'DEVOTION_AURA',
  'CONCENTRATION_AURA',
  'SANCTITY_AURA',
])

export function isAuraAbility(abilityId: string): boolean {
  return auraAbilityIds.has(abilityId)
}
