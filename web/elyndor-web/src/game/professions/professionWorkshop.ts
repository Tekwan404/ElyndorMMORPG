import type { InventoryItem } from '@/api/contracts'
import type { ProfessionRecipeState } from './professionApi'

export function usableMaterialQuantities(
  items: readonly Pick<
    InventoryItem,
    'definitionId' | 'quantity' | 'isLocked' | 'equippedSlot' | 'transactionLocked'
  >[],
): Map<string, number> {
  const quantities = new Map<string, number>()
  for (const item of items) {
    if (item.isLocked || item.equippedSlot || item.transactionLocked) continue
    quantities.set(item.definitionId, (quantities.get(item.definitionId) ?? 0) + item.quantity)
  }
  return quantities
}

export function recipeBlockReason(
  recipe: ProfessionRecipeState,
  skill: number | null,
  locationId: string | null,
  quantities: ReadonlyMap<string, number>,
): string | null {
  if (skill === null) return 'Изучите нужную профессию.'
  if (skill < recipe.requiredSkill) return `Требуется навык ${recipe.requiredSkill}.`
  if (recipe.requiredLocationId && recipe.requiredLocationId !== locationId)
    return 'Нужна городская мастерская.'
  if (
    recipe.ingredients.some(
      (ingredient) => (quantities.get(ingredient.itemId) ?? 0) < ingredient.quantity,
    )
  ) {
    return 'Не хватает доступных материалов.'
  }
  return null
}
