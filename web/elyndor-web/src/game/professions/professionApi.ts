import { apiClient } from '@/api/apiClient'
import { runReplaySafeGameMutation } from '@/api/replaySafeMutation'

export interface ProfessionStateItem {
  id: string
  name: string
  category: string | number
  skill: number
  maxSkill: number
  relatedProfessionId?: string | null
}

export interface SkinnableCorpseState {
  combatSessionId: string
  enemyActorId: string
  monsterDefinitionId: string
  monsterName: string
  requiredSkill: number
  expiresAtUtc: string
  itemName?: string | null
  minQuantity?: number
  maxQuantity?: number
}

export interface ProfessionRecipeIngredient {
  itemId: string
  quantity: number
}

export interface ProfessionRecipeState {
  id: string
  name: string
  professionId: string
  requiredSkill: number
  outputItemId: string
  outputQuantity: number
  ingredients: ProfessionRecipeIngredient[]
  requiredLocationId: string | null
  skillUpUntil?: number
  outputName?: string | null
  outputIconId?: string | null
  outputRequiredLevel?: number
  isLearned?: boolean
  learningScrollItemId?: string | null
  learningScrollName?: string | null
}

export interface ProfessionGatheringNode {
  id: string
  name: string
  locationId: string
  requiredSkill: number
  skillUpUntil: number
  itemId: string
  quantity: number
  cooldownSeconds: number
}
export interface ProfessionEnchantment {
  id: string
  name: string
  scrollItemId: string
  statId: string
  value: number
  slots: (string | number)[]
}
export interface ProfessionStateSnapshot {
  gatheringNodes?: ProfessionGatheringNode[]
  enchantments?: ProfessionEnchantment[]
  nextGatherAtUtc?: string | null
  available?: Omit<ProfessionStateItem, 'skill'>[]
  maxPrimaryProfessions?: number
  learned: ProfessionStateItem[]
  skinnableCorpses: SkinnableCorpseState[]
  recipes: ProfessionRecipeState[]
  materials?: { id: string; name: string }[]
  materialSources?: {
    itemId: string
    itemName: string
    monsterName: string
    locationId: string
    locationName: string
    requiredSkill: number
    skillUpUntil?: number
    professionId?: string | null
  }[]
}

export interface ProfessionMutationResult {
  isSuccess: boolean
  errorCode: string | null
  state: ProfessionStateSnapshot | null
  itemId: string | null
  quantity: number
  skillIncreased: boolean
  replayed: boolean
}

export async function getProfessionState(): Promise<ProfessionStateSnapshot> {
  return await apiClient.request<ProfessionStateSnapshot>('/api/v1/professions/')
}

export async function gatherResource(nodeId: string): Promise<ProfessionMutationResult> {
  return await runReplaySafeGameMutation<ProfessionMutationResult>({
    key: `profession:gather:${nodeId}`,
    path: '/api/v1/professions/gather',
    idField: 'mutationId',
    intent: { nodeId },
  })
}

export async function studyProfessionRecipe(recipeId: string): Promise<ProfessionMutationResult> {
  return await runReplaySafeGameMutation<ProfessionMutationResult>({
    key: `profession:study:${recipeId}`,
    path: '/api/v1/professions/study',
    idField: 'mutationId',
    intent: { recipeId },
  })
}

export async function forgetProfession(professionId: string): Promise<ProfessionMutationResult> {
  return await runReplaySafeGameMutation<ProfessionMutationResult>({
    key: `profession:forget:${professionId}`,
    path: '/api/v1/professions/forget',
    idField: 'mutationId',
    intent: { professionId },
  })
}

export async function learnProfession(professionId: string): Promise<ProfessionMutationResult> {
  return await runReplaySafeGameMutation<ProfessionMutationResult>({
    key: `profession:learn:${professionId}`,
    path: '/api/v1/professions/learn',
    idField: 'mutationId',
    intent: { professionId },
  })
}

export async function skinCorpse(
  combatSessionId: string,
  enemyActorId: string,
): Promise<ProfessionMutationResult> {
  return await runReplaySafeGameMutation<ProfessionMutationResult>({
    key: `profession:skin:${combatSessionId}:${enemyActorId}`,
    path: '/api/v1/professions/skinning',
    idField: 'mutationId',
    intent: { combatSessionId, enemyActorId },
  })
}

export async function craftProfessionRecipe(recipeId: string): Promise<ProfessionMutationResult> {
  return await runReplaySafeGameMutation<ProfessionMutationResult>({
    key: `profession:craft:${recipeId}`,
    path: '/api/v1/professions/craft',
    idField: 'mutationId',
    intent: { recipeId },
  })
}

export async function gatherProfessionNode(nodeId: string): Promise<ProfessionMutationResult> {
  return await runReplaySafeGameMutation<ProfessionMutationResult>({
    key: `profession:gather:${nodeId}`,
    path: '/api/v1/professions/gather',
    idField: 'mutationId',
    intent: { nodeId },
  })
}
export async function disenchantProfessionItem(itemId: string): Promise<ProfessionMutationResult> {
  return await runReplaySafeGameMutation<ProfessionMutationResult>({
    key: `profession:disenchant:${itemId}`,
    path: '/api/v1/professions/disenchant',
    idField: 'mutationId',
    intent: { itemId },
  })
}
export async function enchantProfessionItem(
  itemId: string,
  enchantmentId: string,
): Promise<ProfessionMutationResult> {
  return await runReplaySafeGameMutation<ProfessionMutationResult>({
    key: `profession:enchant:${itemId}:${enchantmentId}`,
    path: '/api/v1/professions/enchant',
    idField: 'mutationId',
    intent: { itemId, enchantmentId },
  })
}
