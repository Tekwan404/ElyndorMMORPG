import type { CombatEvent, CombatSnapshot } from '@/api/contracts'

export type ArenaQueueMode = 'Ranked' | 'Unranked'

export interface ArenaStatus {
  enabled: boolean
  honor: number
  rating: number
  wins: number
  losses: number
  draws: number
  isQueued: boolean
  queueMode: ArenaQueueMode | null
  queuedAtUtc: string | null
  activeMatchId: string | null
}

export interface ArenaQueueResponse {
  succeeded: boolean
  errorCode: string | null
  status: ArenaStatus | null
}

export interface ArenaHonorShopItem {
  itemId: string
  name: string
  rarity: string
  slot: string | null
  iconId: string | null
  setId: string | null
  requiredLevel: number
  honorPrice: number
}

export interface ArenaHonorShop {
  honor: number
  items: ArenaHonorShopItem[]
}

export interface ArenaHonorShopPurchaseResponse {
  succeeded: boolean
  errorCode: string | null
  shop: ArenaHonorShop | null
}

export interface ArenaLeaderboardEntry {
  rank: number
  characterId: string
  name: string
  rating: number
  wins: number
  losses: number
  draws: number
}

export interface ArenaMatch {
  status: 'Searching' | 'Active' | 'Completed'
  matchId: string | null
  characterId: string
  opponentCharacterId: string | null
  opponentName: string | null
  outcome: 'Active' | 'WinnerA' | 'WinnerB' | 'Draw' | 'Cancelled'
  result: 'Active' | 'Victory' | 'Defeat' | 'Cancelled' | null
  sequence: number
  battle: CombatSnapshot | null
  events: CombatEvent[]
}

export interface ArenaCommandResponse {
  succeeded: boolean
  errorCode: string | null
  match: ArenaMatch | null
}

export interface ArenaMatchNotification {
  matchId: string
  sequence: number
}
export interface ArenaInvitation {
  id: string
  inviterCharacterId: string
  inviterName: string
  targetCharacterId: string
  targetName: string
  status: 'Pending' | 'Accepted' | 'Declined' | 'Cancelled' | 'Expired'
  expiresAtUtc: string
  matchId: string | null
  incoming: boolean
}
