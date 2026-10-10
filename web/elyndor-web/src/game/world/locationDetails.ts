import { apiClient } from '@/api/apiClient'
import type { WorldLocation } from '@/api/contracts'

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
  kind: 'Enemy' | 'Npc' | 'Landmark'
  displayName: string
  description: string
  x: number
  y: number
  resident: WorldLocationResident | null
  isRare: boolean
  availableUntilUtc: string | null
  questId: string | null
}

export interface WorldLocationScene {
  locationId: string
  contentVersion: string
  state: 'Calm' | 'Invasion' | 'Corruption'
  serverTimeUtc: string
  nextChangeAtUtc: string | null
  objects: WorldSceneObject[]
}

export interface WorldLocationLoot {
  itemId: string
  name: string
  type: string
  rarity: string
  requiredLevel: number
  description: string
  iconId: string | null
}

export interface DetailedWorldLocation extends WorldLocation {
  residents?: WorldLocationResident[] | null
  loot?: WorldLocationLoot[] | null
}

let cachedContentVersion: string | null = null
let catalogPromise: Promise<DetailedWorldLocation[]> | null = null

export function loadLocationCatalog(contentVersion: string): Promise<DetailedWorldLocation[]> {
  if (catalogPromise && cachedContentVersion === contentVersion) return catalogPromise

  cachedContentVersion = contentVersion
  catalogPromise = apiClient.request<DetailedWorldLocation[]>('/api/v1/world/locations')
    .catch(error => {
      catalogPromise = null
      throw error
    })
  return catalogPromise
}
