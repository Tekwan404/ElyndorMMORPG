export interface AuthenticationResponse {
  accessToken: string
  expiresAtUtc: string
}

export interface ApiProblem {
  code?: string
  correlationId?: string
  title?: string
  status?: number
}

export interface WorldLocation {
  id: string
  displayName: string
  dangerLevel: 'SAFE' | 'ADVENTURE' | 'DANGEROUS'
  recommendedLevel: number
}

export interface CharacterSnapshot {
  id: string
  name: string
  raceId: 'HUMAN' | 'UNDEAD'
  genderId: 'MALE' | 'FEMALE'
  classId: 'WARRIOR' | 'ARCHER' | 'MAGE'
  level: number
  primaryAttribute: 'STRENGTH' | 'AGILITY' | 'INTELLECT'
  classProfileVersion: string
  knownAbilityIds: string[]
  stats: CharacterStats
  vitals: CharacterVitals
}

export interface CharacterStats {
  strength: number
  agility: number
  intellect: number
  stamina: number
  maxHp: number
  attackPower: number
  spellPower: number
  criticalChance: number
  criticalDamage: number
  accuracy: number
  armorPenetration: number
  magicPenetration: number
  attackSpeed: number
  armor: number
  magicResistance: number
  dodge: number
}

export interface CharacterVitals {
  currentHp: number
  maxHp: number
  resourceType: 'RAGE' | 'FOCUS' | 'MANA'
  currentResource: number
  maxResource: number
  checkpointedAtUtc: string
}

export interface BootstrapSnapshot {
  accountId: string
  character: CharacterSnapshot | null
  world: {
    currentLocation: WorldLocation
    version: number
    outgoingTransitions: WorldLocation[]
  } | null
  contentVersion: string
  balanceVersion: string
  serverTimeUtc: string
}

export interface CreateCharacterRequest {
  requestId: string
  name: string
  raceId: 'HUMAN' | 'UNDEAD'
  genderId: 'MALE' | 'FEMALE'
  classId: 'WARRIOR' | 'ARCHER' | 'MAGE'
}

export interface TravelResponse {
  locationId: string
  version: number
}

export type TalentLoadoutId = 'LOADOUT_1' | 'LOADOUT_2'
export type TalentBranchId = 'GUARDIAN' | 'BERSERKER' | 'WARLORD'
export interface TalentPrerequisite { talentId: string; requiredRank: number }
export interface TalentNode {
  id: string; branchId: TalentBranchId; tier: number; requiredSpentPoints: number
  name: string; englishName: string; maxRank: number
  prerequisites: TalentPrerequisite[]; description: string; requiredLevel: number | null
  iconId: string | null; runtimeStatus: 'SUPPORTED' | 'PARTIAL' | 'DEFERRED'
  unlockedAbilityId: string | null
}
export interface TalentBranch {
  id: TalentBranchId; name: string; fantasy: string; nodeCount: number
}
export interface TalentLoadout {
  id: TalentLoadoutId; selectedRanks: Record<string, number>; spentPoints: number
}
export interface TalentSnapshot {
  treeId: string; classId: string; version: number; activeLoadoutId: TalentLoadoutId
  stateVersion: number; earnedPoints: number; availablePoints: number
  branches: TalentBranch[]; nodes: TalentNode[]; loadouts: TalentLoadout[]
}
