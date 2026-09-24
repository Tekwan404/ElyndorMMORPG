import type { InventoryItem } from '@/api/contracts'

export interface EquipmentSetBonusPresentation {
  requiredPieces: number
  attackSpeedPercent: number
  dodgePercent: number
  maxHpFlat: number
  attackPowerFlat: number
  spellPowerFlat: number
  criticalChancePercent: number
  criticalDamagePercent: number
  accuracyPercent: number
  armorFlat: number
  magicResistanceFlat: number
  armorPenetrationPercent: number
  magicPenetrationPercent: number
  maxResourceFlat: number
}

export interface EquipmentSetPresentation {
  id: string
  name: string
  bonuses: EquipmentSetBonusPresentation[]
}

export interface ClassEquipmentRules {
  classId: string
  allowedWeaponCategories: string[]
  allowedArmorCategories: string[]
  allowedOffHandCategories: string[]
}

export interface InventoryPresentation {
  equipmentSets: EquipmentSetPresentation[]
  classRules: ClassEquipmentRules[]
}

export function armorCategoryLabel(category: string | null): string | null {
  if (!category) return null
  if (category === 'CLOTH') return 'Ткань'
  if (category === 'LEATHER') return 'Кожа'
  if (category === 'HEAVY') return 'Латы'
  return category
}

function armorRestrictionLabel(category: string): string {
  if (category === 'CLOTH') return 'тканевую броню'
  if (category === 'LEATHER') return 'кожаную броню'
  if (category === 'HEAVY') return 'латы'
  return `броню категории «${category}»`
}

export function equipmentCompatibilityReason(
  item: InventoryItem,
  rules: ClassEquipmentRules | null | undefined,
): string | null {
  if (item.type !== 'Equipment' || !rules) return null

  if (item.armorCategory && !rules.allowedArmorCategories.includes(item.armorCategory)) {
    return `Ваш класс не может носить ${armorRestrictionLabel(item.armorCategory)}.`
  }

  if (item.weaponCategory && !rules.allowedWeaponCategories.includes(item.weaponCategory)) {
    return 'Ваш класс не может использовать этот тип оружия.'
  }

  return null
}

export function setPresentationForItem(
  item: InventoryItem | null | undefined,
  presentation: InventoryPresentation | null | undefined,
): EquipmentSetPresentation | null {
  if (!item?.setId || !presentation) return null
  return presentation.equipmentSets.find(set => set.id === item.setId) ?? null
}

export function setBonusSummary(bonus: EquipmentSetBonusPresentation): string {
  const parts: string[] = []
  const add = (value: number, label: string, suffix = '') => {
    if (value !== 0) parts.push(`+${formatNumber(value)}${suffix} ${label}`)
  }

  add(bonus.attackSpeedPercent, 'к скорости атаки', '%')
  add(bonus.dodgePercent, 'к уклонению', '%')
  add(bonus.maxHpFlat, 'к максимуму здоровья')
  add(bonus.attackPowerFlat, 'к силе атаки')
  add(bonus.spellPowerFlat, 'к силе заклинаний')
  add(bonus.criticalChancePercent, 'к шансу критического удара', '%')
  add(bonus.criticalDamagePercent, 'к критическому урону', '%')
  add(bonus.accuracyPercent, 'к точности', '%')
  add(bonus.armorFlat, 'к броне')
  add(bonus.magicResistanceFlat, 'к сопротивлению магии')
  add(bonus.armorPenetrationPercent, 'к пробиванию брони', '%')
  add(bonus.magicPenetrationPercent, 'к пробиванию магии', '%')
  add(bonus.maxResourceFlat, 'к максимуму ресурса')

  return parts.join(' · ')
}

export function setPieceLabel(requiredPieces: number): string {
  const mod100 = Math.abs(requiredPieces) % 100
  const mod10 = mod100 % 10
  if (mod100 >= 11 && mod100 <= 14) return 'предметов'
  if (mod10 === 1) return 'предмет'
  if (mod10 >= 2 && mod10 <= 4) return 'предмета'
  return 'предметов'
}

function formatNumber(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(2).replace(/0+$/, '').replace(/\.$/, '')
}