export interface AuthenticationResponse {
  accessToken: string
  expiresAtUtc: string
  roles: string[]
}

export interface ContentAdminCurrent {
  contentVersion: string
  balanceVersion: string
  sourcePublishedAtUtc: string
  revisionId: string | null
  releaseId: string | null
  payloadSha256: string
  payloadJson: string
}

export interface ContentAdminValidationError {
  code: string
  path: string
  message: string
}

export interface ContentAdminValidation {
  isValid: boolean
  canonicalPayloadJson: string | null
  payloadSha256: string | null
  errors: ContentAdminValidationError[]
}

export interface ContentAdminRevision {
  id: string
  contentVersion: string
  balanceVersion: string
  sourcePublishedAtUtc: string
  payloadSha256: string
  createdAtUtc: string
  createdBy: string
  note: string | null
}

export interface ContentAdminRevisionDetail extends ContentAdminRevision {
  payloadJson: string
}

export interface ContentAdminRelease {
  id: string
  revisionId: string
  publishedAtUtc: string
  publishedBy: string
  note: string | null
}

export interface ContentAdminHistory {
  revisions: ContentAdminRevision[]
  releases: ContentAdminRelease[]
}

export interface ContentAdminSimulationDamageSource {
  definitionId: string
  averageDamage: number
  damageSharePercent: number
}

export interface ContentAdminSimulation {
  contentVersion: string
  balanceVersion: string
  classId: string
  playerLevel: number
  monsterId: string
  iterations: number
  victories: number
  defeats: number
  timeouts: number
  winRatePercent: number
  averageDurationSeconds: number
  p50DurationSeconds: number
  p95DurationSeconds: number
  averagePlayerDps: number
  averageEnemyDps: number
  averagePlayerRemainingHp: number
  damageSources: ContentAdminSimulationDamageSource[]
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

export interface WorldEncounter {
  encounterId: string
  monsterId: string
  name: string
  level: number
  rank: string
  description: string
  artId: string
}

export interface KnownAbility {
  id: string
  displayName: string
  description: string
  iconId: string | null
  resourceCost: number
  cooldownSeconds: number
  type: string
  targetType: string
  sourceTalentId: string | null
  sourceTalentName: string | null
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
  gold: number
  primaryAttribute: 'STRENGTH' | 'AGILITY' | 'INTELLECT'
  classProfileVersion: string
  knownAbilityIds: string[]
  knownAbilities: KnownAbility[]
  stats: CharacterStats
  statBreakdown: Record<keyof CharacterStats, CharacterStatBreakdown>
  vitals: CharacterVitals
  inventory: InventorySnapshot
}

export interface ItemStats {
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
  armor: number
  magicResistance: number
  dodge: number
  armorPenetration: number
  magicPenetration: number
  attackSpeed: number
  maxResource: number
}

export type EquipmentSlot =
  | 'MainHand' | 'OffHand' | 'Head' | 'Chest' | 'Hands' | 'Legs' | 'Feet'
  | 'Cloak' | 'Amulet' | 'Ring1' | 'Ring2'
  | 'Weapon' | 'Boots' | 'Accessory'
export type ItemType = 'Equipment' | 'Material' | 'Consumable'
export type ItemRarity = 'Common' | 'Uncommon' | 'Rare' | 'Epic' | 'Legendary' | 'Unique'

export interface InventoryItem {
  id: string
  definitionId: string
  name: string
  type: ItemType
  rarity: ItemRarity
  requiredLevel: number
  quantity: number
  slot: EquipmentSlot | null
  equippedSlot: EquipmentSlot | null
  stats: ItemStats
  description: string
  setId: string | null
  weaponCategory: string | null
  armorCategory: string | null
  allowedClassIds: string[]
  weaponBaseAttackIntervalSeconds: number | null
  attackSpeedPercent: number
  dodgePercent: number
  healAmount: number
  consumableCooldownSeconds: number
  buyPriceGold: number
  sellPriceGold: number
  iconId: string | null
  appearanceProfileId: string | null
}

export interface InventorySnapshot {
  items: InventoryItem[]
  equipped: {
    weapon: InventoryItem | null
    head: InventoryItem | null
    chest: InventoryItem | null
    legs: InventoryItem | null
    boots: InventoryItem | null
    accessory: InventoryItem | null
    mainHand: InventoryItem | null
    offHand: InventoryItem | null
    hands: InventoryItem | null
    feet: InventoryItem | null
    cloak: InventoryItem | null
    amulet: InventoryItem | null
    ring1: InventoryItem | null
    ring2: InventoryItem | null
  }
}

export interface MerchantItem {
  definitionId: string
  name: string
  type: ItemType
  rarity: ItemRarity
  description: string
  buyPriceGold: number
  sellPriceGold: number
  healAmount: number
}

export interface MerchantSnapshot {
  id: string
  name: string
  description: string
  gold: number
  items: MerchantItem[]
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

export interface CharacterStatContribution {
  source: 'CLASS_BASE' | 'LEVEL_GROWTH' | 'EQUIPMENT' | 'TALENT_FLAT' | 'TALENT_PERCENT'
    | 'EFFECTS' | 'FORMULA_BASE' | 'STRENGTH' | 'AGILITY' | 'INTELLECT' | 'STAMINA' | 'TALENT_BONUS' | 'EQUIPMENT_BONUS'
  value: number
}

export interface CharacterStatBreakdown {
  finalValue: number
  contributions: CharacterStatContribution[]
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
export type TalentBranchId = 'GUARDIAN' | 'BERSERKER' | 'WARLORD' | 'FIRE' | 'ARCANE' | 'FROST'
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

export interface CombatAbility {
  id: string
  displayName: string
  description: string
  iconId: string | null
  resourceCost: number
  cooldownSeconds: number
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
  abilities: CombatAbility[]
  effects: CombatEffectSnapshot[]
  level?: number
  artId?: string | null
}

export interface CombatSnapshot {
  sessionId: string
  sequence: number
  status: 'Active' | 'Victory' | 'Defeat' | 'Cancelled'
  serverTimeUtc: string
  contentVersion: string
  balanceVersion: string
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
  amountBeforeShields: number
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
  goldEarned: number
  leveledUp: boolean
  previousLevel: number
  currentLevel: number
  items: {
    itemId: string
    name: string
    type: ItemType
    rarity: ItemRarity
    quantity: number
  }[]
}
