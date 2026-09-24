import { describe, expect, it } from 'vitest'

import type { InventoryItem } from '@/api/contracts'
import {
  armorCategoryLabel,
  equipmentCompatibilityReason,
  setPresentationForItem,
  type InventoryPresentation,
} from './inventoryPresentation'

const presentation: InventoryPresentation = {
  equipmentSets: [
    {
      id: 'SET_ANCIENT_MINE_WARRIOR_BERSERKER',
      name: 'Кровь Подземного Клыка',
      bonuses: [{ requiredPieces: 2 }, { requiredPieces: 4 }, { requiredPieces: 5 }].map(requiredPieces => ({
        requiredPieces,
        attackSpeedPercent: 0,
        dodgePercent: 0,
        maxHpFlat: 0,
        attackPowerFlat: 0,
        spellPowerFlat: 0,
        criticalChancePercent: 0,
        criticalDamagePercent: 0,
        accuracyPercent: 0,
        armorFlat: 0,
        magicResistanceFlat: 0,
        armorPenetrationPercent: 0,
        magicPenetrationPercent: 0,
        maxResourceFlat: 0,
      })),
    },
    { id: 'SET_ANCIENT_MINE_MAGE_FIRE', name: 'Одеяния Пепельной Жилы', bonuses: [] },
    { id: 'SET_ANCIENT_MINE_ARCHER_MARKSMANSHIP', name: 'Снаряжение Шахтного Следопыта', bonuses: [] },
    { id: 'SET_ANCIENT_MINE_PALADIN_HOLY', name: 'Латы Светоча Глубин', bonuses: [] },
  ],
  classRules: [
    { classId: 'MAGE', allowedWeaponCategories: ['STAFF', 'WAND'], allowedArmorCategories: ['CLOTH'], allowedOffHandCategories: [] },
    { classId: 'ARCHER', allowedWeaponCategories: ['BOW'], allowedArmorCategories: ['LEATHER'], allowedOffHandCategories: [] },
    { classId: 'WARRIOR', allowedWeaponCategories: ['ONE_HAND_SWORD'], allowedArmorCategories: ['HEAVY'], allowedOffHandCategories: ['SHIELD'] },
    { classId: 'PALADIN', allowedWeaponCategories: ['ONE_HAND_SWORD'], allowedArmorCategories: ['HEAVY'], allowedOffHandCategories: ['SHIELD'] },
  ],
}

function item(overrides: Partial<InventoryItem> = {}): InventoryItem {
  return {
    id: 'instance',
    definitionId: 'definition',
    name: 'Предмет',
    type: 'Equipment',
    rarity: 'Rare',
    requiredLevel: 1,
    quantity: 1,
    slot: 'Chest',
    equippedSlot: null,
    stats: {
      strength: 0,
      agility: 0,
      intellect: 0,
      stamina: 0,
      maxHp: 0,
      attackPower: 0,
      spellPower: 0,
      criticalChance: 0,
      criticalDamage: 0,
      accuracy: 0,
      armor: 0,
      magicResistance: 0,
      dodge: 0,
      armorPenetration: 0,
      magicPenetration: 0,
      attackSpeed: 0,
      maxResource: 0,
    },
    description: 'Короткое игровое описание.',
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
    ...overrides,
  }
}

describe('inventory presentation', () => {
  it('resolves real set metadata for all four classes', () => {
    expect(setPresentationForItem(item({ setId: 'SET_ANCIENT_MINE_WARRIOR_BERSERKER' }), presentation)?.name)
      .toBe('Кровь Подземного Клыка')
    expect(setPresentationForItem(item({ setId: 'SET_ANCIENT_MINE_MAGE_FIRE' }), presentation)?.name)
      .toBe('Одеяния Пепельной Жилы')
    expect(setPresentationForItem(item({ setId: 'SET_ANCIENT_MINE_ARCHER_MARKSMANSHIP' }), presentation)?.name)
      .toBe('Снаряжение Шахтного Следопыта')
    expect(setPresentationForItem(item({ setId: 'SET_ANCIENT_MINE_PALADIN_HOLY' }), presentation)?.name)
      .toBe('Латы Светоча Глубин')

    expect(setPresentationForItem(item({ setId: 'SET_ANCIENT_MINE_WARRIOR_BERSERKER' }), presentation)?.bonuses.map(bonus => bonus.requiredPieces))
      .toEqual([2, 4, 5])
  })

  it('does not invent a set for missing or unknown set ids', () => {
    expect(setPresentationForItem(item(), presentation)).toBeNull()
    expect(setPresentationForItem(item({ setId: 'SET_UNKNOWN' }), presentation)).toBeNull()
  })

  it('presents armor by category rather than class ownership', () => {
    expect(armorCategoryLabel('CLOTH')).toBe('Ткань')
    expect(armorCategoryLabel('LEATHER')).toBe('Кожа')
    expect(armorCategoryLabel('HEAVY')).toBe('Латы')
  })

  it('computes equip restrictions from class armor rules without changing lore', () => {
    const cloth = item({ armorCategory: 'CLOTH', description: 'Старая мантия, пропахшая дымом свечей.' })
    const mage = presentation.classRules.find(rule => rule.classId === 'MAGE')!
    const warrior = presentation.classRules.find(rule => rule.classId === 'WARRIOR')!

    expect(equipmentCompatibilityReason(cloth, mage)).toBeNull()
    expect(equipmentCompatibilityReason(cloth, warrior)).toBe('Ваш класс не может носить тканевую броню.')
    expect(cloth.description).toBe('Старая мантия, пропахшая дымом свечей.')
  })
})