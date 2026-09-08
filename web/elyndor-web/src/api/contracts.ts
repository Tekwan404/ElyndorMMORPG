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

export interface PlayerSearchResult {
  characterId: string
  name: string
  level: number
  classId: string
  publicCode: string
  telegramUsername: string | null
}

export type FriendProfile = PlayerSearchResult

export interface FriendRequest {
  id: string
  requesterCharacterId: string
  targetCharacterId: string
  status: 'Pending' | 'Accepted' | 'Declined'
  createdAtUtc: string
}

export interface FriendsSnapshot {
  friends: FriendProfile[]
  incomingRequests: FriendRequest[]
  outgoingRequests: FriendRequest[]
}

export interface PartyMember {
  characterId: string
  name: string
  level: number
  classId: string
  isLeader: boolean
  joinedAtUtc: string
}

export interface PartySnapshot {
  partyId: string
  leaderCharacterId: string
  version: number
  members: PartyMember[]
}

export interface PartyInvite {
  id: string
  partyId: string
  inviterCharacterId: string
  targetCharacterId: string
  mode: 'Friend' | 'Direct'
  status: 'Pending' | 'Accepted' | 'Declined' | 'Expired' | 'Cancelled'
  createdAtUtc: string
  expiresAtUtc: string
}

export interface DungeonPreview {
  id: string
  displayName: string
  description: string
  minimumLevel: number
  maximumLevel: number
  entryLocationId: string
  minimumPartySize: number
  maximumPartySize: number
  encounters: DungeonEncounterPreview[]
}

export interface DungeonTeleportResponse {
  dungeonId: string
  locationId: string
  locationVersion: number
}

export interface DungeonEncounterPreview {
  id: string
  monsterId: string
  checkpointId: string
  isBoss: boolean
}

export interface DungeonRun {
  runId: string
  dungeonId: string
  displayName: string
  description: string
  state: 'Active' | 'Completed' | 'Abandoned'
  currentEncounterIndex: number
  currentCheckpointId: string
  encounterCount: number
  partyId: string
  members: DungeonRunMember[]
  encounters: DungeonEncounter[]
}

export interface DungeonRunMember {
  characterId: string
  state: 'Active' | 'Left'
  joinedAtUtc: string
}

export interface DungeonEncounter {
  encounterId: string
  encounterIndex: number
  monsterId: string
  state: 'Pending' | 'Active' | 'Wiped' | 'Completed'
  wipeCount: number
  characterIds: string[]
}

export interface WorldLocation {
  id: string
  displayName: string
  dangerLevel: 'SAFE' | 'ADVENTURE' | 'DANGEROUS'
  recommendedLevel: number
  minimumLevel: number
  maximumLevel: number
  requiredContractId: string | null
  artId: string | null
  description: string
  travelDurationSeconds?: number
}

export interface WorldContract {
  id: string
  displayName: string
  description: string
  requiredLevel: number
  targetMonsterId: string
  unlockLocationId: string
  status: 'LOCKED' | 'AVAILABLE' | 'ACTIVE' | 'COMPLETED'
  offerLocationId: string | null
  rewardXp: number
  rewardGold: number
}


export type QuestStatus = 'LOCKED' | 'AVAILABLE' | 'ACTIVE' | 'READY_TO_CLAIM' | 'COMPLETED'

export interface QuestObjective {
  id: string
  type: 'KillMonster' | 'CollectItem' | string
  targetId: string
  currentCount: number
  requiredCount: number
  completed: boolean
  consumeOnClaim: boolean
}

export interface QuestRewardItem {
  itemId: string
  quantity: number
}

export interface Quest {
  id: string
  displayName: string
  description: string
  type: 'STORY' | 'SIDE' | 'CONTRACT' | string
  requiredLevel: number
  offerLocationId: string
  status: QuestStatus
  objectives: QuestObjective[]
  rewardXp: number
  rewardGold: number
  rewardItems: QuestRewardItem[]
  prerequisiteQuestIds: string[]
  unlockLocationId: string | null
  issuerName: string | null
  issuerRole: string | null
  regionName: string | null
  contractNumber: string | null
  threatLevel: string | null
}

export interface QuestJournalResponse {
  quests: Quest[]
}

export interface QuestClaimResponse {
  questId: string
  granted: boolean
  xpEarned: number
  goldEarned: number
  leveledUp: boolean
  previousLevel: number
  currentLevel: number
  items: QuestRewardItem[]
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
  publicCode?: string
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

export interface ConsumableAction {
  type: 'RestoreHp' | 'RestoreResource' | 'ApplyEffect' | 'RemoveEffect'
  amount: number
  resourceType: string | null
  effectId: string | null
  dispelCategory: string | null
}

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
  consumableActions: ConsumableAction[]
  consumableCooldownCategoryId: string | null
  consumableCooldownSeconds: number
  buyPriceGold: number
  sellPriceGold: number
  isLocked: boolean
  iconId: string | null
  appearanceProfileId: string | null
  weaponHandsRequired?: number | null
  hasRandomStats?: boolean
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
    mainHand?: InventoryItem | null
    offHand?: InventoryItem | null
    hands?: InventoryItem | null
    feet?: InventoryItem | null
    cloak?: InventoryItem | null
    amulet?: InventoryItem | null
    ring1?: InventoryItem | null
    ring2?: InventoryItem | null
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
  consumableActions: ConsumableAction[]
  consumableCooldownCategoryId: string | null
  consumableCooldownSeconds: number
  iconId: string | null
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

export interface BootstrapTravel {
  fromLocationId: string
  targetLocationId: string
  startedAtUtc: string
  endsAtUtc: string
}

export interface BootstrapSnapshot {
  accountId: string
  character: CharacterSnapshot | null
  world: {
    currentLocation: WorldLocation
    version: number
    outgoingTransitions: WorldLocation[]
    contracts: WorldContract[]
    travel?: BootstrapTravel | null
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
  isTravelling?: boolean
  targetLocationId?: string | null
  endsAtUtc?: string | null
}

export type TalentLoadoutId = 'LOADOUT_1' | 'LOADOUT_2'
export type TalentBranchId =
  | 'GUARDIAN' | 'BERSERKER' | 'WARLORD'
  | 'FIRE' | 'ARCANE' | 'FROST'
  | 'MARKSMAN' | 'BEAST_MASTERY' | 'ARCANE_ARCHER'
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

export interface CombatCastSnapshot {
  abilityId: string
  startedAtUtc: string
  resolvesAtUtc: string
}

export interface CombatActorSnapshot {
  actorId: string
  kind: 'Player' | 'Companion' | 'Monster'
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
  activeCast?: CombatCastSnapshot | null
  consumableCooldowns?: Record<string, string> | null
  level?: number
  artId?: string | null
}

export interface CombatContributionSnapshot {
  characterId: string
  qualifyingActions: number
  damageDealt: number
  effectiveHealing: number
  supportContribution: number
  tankingContribution: number
  joinedAtUtc: string
  fledAtUtc?: string | null
  diedAtUtc?: string | null
}

export interface CombatParticipantSnapshot {
  accountId: string
  characterId: string
  actorId: string
  status: 'Rostered' | 'Active' | 'Fled' | 'Dead' | 'Completed'
  rosteredAtUtc: string
  joinedAtUtc?: string | null
  fledAtUtc?: string | null
  diedAtUtc?: string | null
}

export interface CombatParticipantContributionSnapshot {
  contribution: CombatContributionSnapshot
  isEligible: boolean
  reason: string
  contributionScore: number
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
  enemies?: CombatActorSnapshot[]
  selectedTargetActorId?: string | null
  companion?: CombatActorSnapshot | null
  playerContribution?: CombatContributionSnapshot | null
  players?: CombatActorSnapshot[] | null
  participantRoster?: CombatParticipantSnapshot[] | null
  playerContributionEligible?: boolean | null
  participantContributions?: CombatParticipantContributionSnapshot[] | null
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
  weaponHand?: 'MainHand' | 'OffHand' | null
  weaponDefinitionId?: string | null
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
  completedContractIds?: string[] | null
  lootRolls?: CombatLootRoll[] | null
}

export interface CombatLootRoll {
  lootRollId: string
  itemId: string
  name: string
  rarity: ItemRarity
  quantity: number
  endsAtUtc: string
  eligibleCharacterIds: string[]
  canNeed: boolean
}
