import { describe, expect, it } from 'vitest'

import type { GeneratedItemSummary, InventoryItem } from '@/api/contracts'
import { availableForgeMaterialQuantity, forgeAffixQuality, forgeItemAvailability, forgePercent, forgeStatLabel, forgeStatRange, forgeStatValue, reforgeResultAffixes, shouldRestorePendingReforge } from '@/game/character/forge/forgePresentation'

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
      rollQuality: 50,
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

function generatedAffix(statId: string, value: number, min: number, max: number): GeneratedItemSummary {
  return {
    itemLevel: 23,
    itemPower: 290.2,
    maxItemPower: 494.91,
    rollQuality: 42,
    stars: 2,
    isPerfect: false,
    perfectOrigin: null,
    generatedPrefixId: null,
    generatedSuffixId: null,
    displayName: 'Сапоги Чёрного Созвездия',
    affixes: [{
      slotKey: 'AFFIX_4',
      statId,
      value,
      min,
      max,
      step: 0.1,
      affixTier: 4,
      isGuaranteed: false,
      isReforgeSlot: true,
    }],
  }
}

describe('forgeItemAvailability', () => {
  it('marks a locked generated item unavailable with an explicit reason', () => {
    expect(forgeItemAvailability(equipment({ isLocked: true }))).toEqual({
      available: false,
      reason: 'Предмет защищён. Снимите блокировку в инвентаре.',
    })
  })

  it('allows developer-locked items in reforge without making normal locked items tradable', () => {
    expect(forgeItemAvailability(equipment({
      isLocked: true, sourceType: 'GM_FORGE',
    }))).toEqual({ available: true, reason: null })
    expect(forgeItemAvailability(equipment({
      isLocked: true, sourceType: 'ADMIN_GRANT',
    })).available).toBe(false)
  })

  it('accepts an unequipped item with a rerollable affix', () => {
    expect(forgeItemAvailability(equipment())).toEqual({ available: true, reason: null })
  })

  it('accepts an equipped item because reforge changes the same instance', () => {
    expect(forgeItemAvailability(equipment({ equippedSlot: 'MainHand' }))).toEqual({ available: true, reason: null })
  })
})

describe('availableForgeMaterialQuantity', () => {
  it('excludes protected and transaction-locked material stacks', () => {
    expect(availableForgeMaterialQuantity([
      equipment({ definitionId: 'REFORGE_STONE', type: 'Material', quantity: 4, transactionLocked: false }),
      equipment({ definitionId: 'REFORGE_STONE', type: 'Material', quantity: 10, isLocked: true, transactionLocked: false }),
      equipment({ definitionId: 'REFORGE_STONE', type: 'Material', quantity: 5, transactionLocked: true }),
    ], 'REFORGE_STONE')).toBe(4)
  })
})

describe('shouldRestorePendingReforge', () => {
  it('restores a pending result even when the item is transaction-locked', () => {
    expect(shouldRestorePendingReforge(equipment({ transactionLocked: true }))).toBe(true)
  })
})

describe('reforgeResultAffixes', () => {
  it('keeps current and proposed stat identities and ranges separate', () => {
    const result = reforgeResultAffixes(
      generatedAffix('ACCURACY', 4.4, 2.3, 5.9),
      generatedAffix('DODGE', 1.6, 0.8, 2.1),
      'AFFIX_4',
    )

    expect(result.current).toMatchObject({ statId: 'ACCURACY', value: 4.4, min: 2.3, max: 5.9 })
    expect(result.proposed).toMatchObject({ statId: 'DODGE', value: 1.6, min: 0.8, max: 2.1 })
    expect(forgeStatLabel(result.current!.statId)).toBe('Точность')
    expect(forgeStatLabel(result.proposed!.statId)).toBe('Уклонение')
  })
})


describe('forge affix quality presentation', () => {
  it('shows realized affix quality separately from item quality', () => {
    expect(forgeAffixQuality({ value: 17, min: 10, max: 20 })).toBe(70)
    expect(forgePercent(95.93)).toBe('95.93%')
  })

  it('formats percentage stats and authoritative ranges', () => {
    expect(forgeStatValue('CRITICAL_DAMAGE', 17.9)).toBe('+17.9%')
    expect(forgeStatRange('CRITICAL_DAMAGE', 12, 24)).toBe('12–24%')
    expect(forgeStatValue('SPELL_POWER', 176)).toBe('+176')
    expect(forgeStatValue('PHYSICAL_VAMPIRISM', 2.5)).toBe('+2.5%')
    expect(forgeStatRange('MAGICAL_VAMPIRISM', 0.4, 3.2)).toBe('0.4–3.2%')
    expect(forgeStatLabel('UNIVERSAL_VAMPIRISM')).toBe('Универсальный вампиризм')
  })
})
