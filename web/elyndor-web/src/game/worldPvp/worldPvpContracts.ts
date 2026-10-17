/**
 * Read-only presentation contracts for World PvP.
 *
 * Not an Arena DTO. No currency, rating or reward is read from arenaStore.
 * The game server owns all values and all mutations; the UI never calculates
 * a reward, grants currency, spends tokens or determines PvP eligibility.
 */
export type WorldPvpFaction = 'HUMAN' | 'UNDEAD'
export type WorldPvpSeasonStatus = 'Upcoming' | 'Active' | 'Finished'
export type WorldPvpRewardCategory = 'Equipment' | 'Cosmetic' | 'Collectible'

export interface WorldPvpSeason {
  id: string
  name: string
  status: WorldPvpSeasonStatus
  endsAtUtc: string | null
}

export interface WorldPvpPersonalRecord {
  characterId: string
  faction: WorldPvpFaction
  rank: number | null
  score: number
  kills: number
  assists: number
  deaths: number
  bestStreak: number
  currentStreak: number
  fieldHonor: number
  bloodTokens: number
}

export interface WorldPvpStanding {
  characterId: string
  name: string
  faction: WorldPvpFaction
  rank: number
  score: number
  kills: number
  assists: number
  deaths: number
}

export interface WorldPvpReward {
  id: string
  name: string
  description: string
  category: WorldPvpRewardCategory
  cost: number
  currency: 'BloodTokens' | 'FieldHonor'
  requiredRank?: number | null
  owned: boolean
}

export interface WorldPvpBounty {
  id: string
  targetCharacterId: string
  targetName: string
  targetFaction: WorldPvpFaction
  killStreak: number
  lastSeenLocationName: string | null
  honorReward: number
  tokenReward: number
}

export interface WorldPvpTitle {
  id: string
  name: string
  description: string
  /** Server-authored unlock criteria. Never interpreted as client reward logic. */
  requirement: string
  progress: number | null
  target: number | null
  unlocked: boolean
  equipped: boolean
}

export interface WorldPvpDashboard {
  season: WorldPvpSeason
  personal: WorldPvpPersonalRecord | null
  standings: WorldPvpStanding[]
  rewards: WorldPvpReward[]
  bounties: WorldPvpBounty[]
  titles?: WorldPvpTitle[]
}

export function worldPvpFactionLabel(faction: WorldPvpFaction): string {
  return faction === 'HUMAN' ? 'Люди' : 'Нежить'
}

export function worldPvpSeasonLabel(status: WorldPvpSeasonStatus): string {
  if (status === 'Active') return 'Сезон идёт'
  if (status === 'Finished') return 'Сезон завершён'
  return 'Сезон готовится'
}
