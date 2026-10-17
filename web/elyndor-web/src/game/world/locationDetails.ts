import { apiClient } from '@/api/apiClient'
import type { WorldLocation, WorldLocationLoot } from '@/api/contracts'
export type { WorldLocationLoot } from '@/api/contracts'

export interface WorldLocationResident {
  monsterId: string
  displayName: string
  level: number
  rank: 'Normal' | 'Elite' | 'Boss' | string
  description: string
  artId: string | null
  xpReward: number
  goldRewardMin: number
  goldRewardMax: number
  loot?: WorldLocationLoot[] | null
}

export interface WorldSceneObject {
  id: string
  kind: 'Enemy' | 'Npc' | 'Landmark' | 'Resource'
  displayName: string
  description: string
  x: number
  y: number
  resident: WorldLocationResident | null
  isRare: boolean
  availableUntilUtc: string | null
  respawnAtUtc?: string | null
  questId: string | null
  resource?: {
    nodeId: string
    itemId: string
    itemName: string
    iconId: string | null
    minQuantity: number
    maxQuantity: number
    requiredSkill: number
    learnedSkill: number
    canGather: boolean
    availableAtUtc: string
  } | null
}

export interface WorldLocationScene {
  locationId: string
  contentVersion: string
  state: 'Calm' | 'Invasion' | 'Corruption'
  serverTimeUtc: string
  nextChangeAtUtc: string | null
  objects: WorldSceneObject[]
}

export interface DetailedWorldLocation extends WorldLocation {
  residents?: WorldLocationResident[] | null
  loot?: WorldLocationLoot[] | null
}

let cachedContentVersion: string | null = null
let catalogPromise: Promise<DetailedWorldLocation[]> | null = null

export function loadLocationCatalog(
  contentVersion: string,
  characterId = '',
): Promise<DetailedWorldLocation[]> {
  const cacheKey = `${contentVersion}:${characterId}`
  if (catalogPromise && cachedContentVersion === cacheKey) return catalogPromise

  cachedContentVersion = cacheKey
  catalogPromise = apiClient
    .request<DetailedWorldLocation[]>('/api/v1/world/locations')
    .catch((error) => {
      catalogPromise = null
      throw error
    })
  return catalogPromise
}
