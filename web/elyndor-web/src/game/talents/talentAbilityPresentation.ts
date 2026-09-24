type TalentAbilityUnlock = {
  unlockedAbilityId: string | null
  unlockedAbilityName?: string | null
}

export function unlockedAbilityLabel(talent: TalentAbilityUnlock): string | null {
  const abilityId = talent.unlockedAbilityId?.trim()
  if (!abilityId) return null

  return talent.unlockedAbilityName?.trim() || abilityId
}
