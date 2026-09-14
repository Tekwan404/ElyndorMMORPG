import { apiClient } from '@/api/apiClient'
import { runReplaySafeGameMutation } from '@/api/replaySafeMutation'

export interface ProfessionStateItem {
  id: string
  name: string
  category: string | number
  skill: number
  maxSkill: number
}

export interface SkinnableCorpseState {
  combatSessionId: string
  enemyActorId: string
  monsterDefinitionId: string
  monsterName: string
  requiredSkill: number
  expiresAtUtc: string
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
}

export interface ProfessionStateSnapshot {
  learned: ProfessionStateItem[]
  skinnableCorpses: SkinnableCorpseState[]
  recipes: ProfessionRecipeState[]
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
