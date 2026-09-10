import { describe, expect, it } from 'vitest'

import type { InventoryItem } from '@/api/contracts'
import { forgeItemAvailability } from '@/game/character/forge/forgePresentation'

function equipment(overrides: Partial<InventoryItem> = {}): InventoryItem {
  return {
    id: 'item-1',
    definitionId: 'FOREST_SWORD',
    name: 'Forest Sword',
    type: 'Equipment',
    rarity: 'Rare',
    requiredLevel: 1,
    quantity: 1,
    slot: 'MainHand',
    equippedSlot: null,
    stats: { strength: 0, agility: 0, intellect: 0, stamina: 0, maxHp: 0, attackPower: 0, spellPower: 0, criticalChance: 0, criticalDamage: 0, accuracy: 0, armor: 0, magicResistance: 0, dodge: 0, armorPenetration: 0, magicPenetration: 0, attackSpeed: 0, maxResource: 0 },
    description: '',
    setId: null,
    weaponCategory: null,
    armorCategory: null,
    allowedClassIds: [],
    weaponBaseAttackIntervalSeconds: null,
    attackSpeedPercent: 0,
    dodgePercent: 0,
    consumableActions: [],
    consumableCooldownCategoryId: null,
    consumableCooldownSeconds: 0,
    buyPriceGold: 0,
    sellPriceGold: 0,
    isLocked: false,
    iconId: null,
    appearanceProfileId: null,
    generatedItem: {
      itemLevel: 5,
      itemPower: 20,
      maxItemPower: 30,
      rollQuality: 0.5,
      stars: 3,
      isPerfect: false,
      perfectOrigin: null,
      generatedPrefixId: null,
      generatedSuffixId: null,
      displayName: 'Forest Sword',
      affixes: [{ slotKey: 'suffix-1', statId: 'STRENGTH', value: 4, min: 2, max: 8, step: 1, affixTier: 1, isGuaranteed: false, isReforgeSlot: true }],
    },
    ...overrides,
  }
}

describe('forgeItemAvailability', () => {
  it('marks a locked generated item unavailable with an explicit reason', () => {
    expect(forgeItemAvailability(equipment({ isLocked: true }))).toEqual({
      available: false,
      reason: 'Предмет защищён. Снимите блокировку в инвентаре.',
    })
  })

  it('accepts an unequipped item with a rerollable affix', () => {
    expect(forgeItemAvailability(equipment())).toEqual({ available: true, reason: null })
  })
})
