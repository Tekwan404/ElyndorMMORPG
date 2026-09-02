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
  experience: number
  xpToNextLevel: number
  primaryAttribute: 'STRENGTH' | 'AGILITY' | 'INTELLECT'
  classProfileVersion: string
  knownAbilityIds: string[]
  stats: CharacterStats
  vitals: CharacterVitals
  inventory: InventorySnapshot
}

export interface ItemStats {
  strength: number
  agility: number
  intellect: number
  stamina: number
}

export type EquipmentSlot = 'Weapon' | 'Head' | 'Chest'

export interface InventoryItem {
  id: string
  definitionId: string
  name: string
  type: 'Equipment' | 'Material'
  rarity: 'Common' | 'Uncommon' | 'Rare'
  requiredLevel: number
  quantity: number
  slot: EquipmentSlot | null
  equippedSlot: EquipmentSlot | null
  stats: ItemStats
  description: string
}

export interface InventorySnapshot {
  items: InventoryItem[]
  equipped: Record<EquipmentSlot, InventoryItem | null>
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

export interface CombatEffectSnapshot {
  id: string
  stacks: number
  expiresAtUtc: string
}

export interface CombatActorSnapshot {
  actorId: string
  kind: 'Player' | 'Monster'
  definitionId: string
  name: string
  hp: number
  maxHp: number
  resourceType: string
  resource: number
  maxResource: number
  autoAttackEnabled: boolean
  cooldowns: Record<string, string>
  knownAbilityIds: string[]
  abilities: { id: string; resourceCost: number; cooldownSeconds: number }[]
  effects: CombatEffectSnapshot[]
}

export interface CombatSnapshot {
  sessionId: string
  sequence: number
  status: 'Active' | 'Victory' | 'Defeat' | 'Cancelled'
  serverTimeUtc: string
  player: CombatActorSnapshot
  enemy: CombatActorSnapshot
}

export interface CombatEvent {
  sequence: number
  type: string
  actorId: string
  sourceActorId: string | null
  targetActorId: string | null
  definitionId: string | null
  amount: number
  serverTimeUtc: string
}

export interface CombatUpdate {
  succeeded: boolean
  errorCode: string | null
  snapshot: CombatSnapshot | null
  events: CombatEvent[]
  reward: CombatReward | null
}

export interface CombatReward {
  xpEarned: number
  leveledUp: boolean
  previousLevel: number
  currentLevel: number
  items: {
    itemId: string
    name: string
    type: 'Equipment' | 'Material'
    rarity: 'Common' | 'Uncommon' | 'Rare'
    quantity: number
  }[]
}
