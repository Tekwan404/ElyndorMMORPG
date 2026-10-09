import { describe, expect, it } from 'vitest'
import { recipeBlockReason, usableMaterialQuantities } from './professionWorkshop'
import type { ProfessionRecipeState } from './professionApi'

const recipe: ProfessionRecipeState = {
  id: 'CRAFT',
  name: 'Куртка',
  professionId: 'LEATHERWORKING',
  requiredSkill: 35,
  outputItemId: 'CHEST',
  outputQuantity: 1,
  requiredLocationId: 'STARTER_TOWN',
  ingredients: [{ itemId: 'LIGHT_LEATHER', quantity: 4 }],
}
describe('workshop availability', () => {
  it('counts only ingredients the server may consume', () => {
    const base = {
      definitionId: 'LIGHT_LEATHER',
      quantity: 10,
      isLocked: false,
      equippedSlot: null,
    }
    expect(
      usableMaterialQuantities([
        { ...base, quantity: 3 },
        { ...base, isLocked: true },
        { ...base, equippedSlot: 'Chest' },
        { ...base, transactionLocked: true },
      ]).get('LIGHT_LEATHER'),
    ).toBe(3)
  })
  it('explains skill, station and material requirements independently', () => {
    const materials = new Map([['LIGHT_LEATHER', 4]])
    expect(recipeBlockReason(recipe, null, 'STARTER_TOWN', materials)).toContain('Изучите')
    expect(recipeBlockReason(recipe, 34, 'STARTER_TOWN', materials)).toContain('35')
    expect(recipeBlockReason(recipe, 35, 'DEEP_FOREST', materials)).toContain('мастерская')
    expect(recipeBlockReason(recipe, 35, 'STARTER_TOWN', new Map())).toContain('материалов')
    expect(recipeBlockReason(recipe, 35, 'STARTER_TOWN', materials)).toBeNull()
  })
})
