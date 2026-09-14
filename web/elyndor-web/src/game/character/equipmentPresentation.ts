import type { CharacterSnapshot, InventoryItem } from '@/api/contracts'

export type DetailedInventoryItem = InventoryItem & {
  blockChancePercent?: number
  blockValueMin?: number
  blockValueMax?: number
  weaponDamageMin?: number | null
  weaponDamageMax?: number | null
}

export interface ItemStatRow {
  id: string
  label: string
  value: string
}

export interface CharacterCombatSummaryRow {
  id: string
  label: string
  value: string
  effective?: string | null
}

export function rarityLabel(item: InventoryItem): string {
  if (item.rarity === 'Unique') return 'Уникальный'
  if (item.rarity === 'Legendary') return 'Легендарный'
  if (item.rarity === 'Epic') return 'Эпический'
  if (item.rarity === 'Rare') return 'Редкий'
  if (item.rarity === 'Uncommon') return 'Необычный'
  return 'Обычный'
}

export function slotLabel(item: InventoryItem): string {
  const labels: Record<string, string> = {
    MainHand: 'Основная рука',
    OffHand: 'Вторая рука',
    Weapon: 'Оружие',
    Head: 'Шлем',
    Shoulders: 'Наплечники',
    Chest: 'Нагрудник',
    Hands: 'Перчатки',
    Legs: 'Поножи',
    Feet: 'Обувь',
    Boots: 'Обувь',
    Cloak: 'Плащ',
    Amulet: 'Амулет',
    Ring1: 'Кольцо',
    Ring2: 'Кольцо',
    Accessory: 'Аксессуар',
  }
  return item.slot ? labels[item.slot] ?? 'Снаряжение' : 'Снаряжение'
}

export function categoryLabel(item: InventoryItem): string | null {
  const category = item.weaponCategory ?? item.armorCategory
  if (!category) return null
  const labels: Record<string, string> = {
    ONE_HAND_SWORD: 'Одноручный меч',
    TWO_HAND_SWORD: 'Двуручный меч',
    AXE: 'Топор',
    MACE: 'Булава',
    BOW: 'Лук',
    DAGGER: 'Кинжал',
    STAFF: 'Посох',
    WAND: 'Жезл',
    HEAVY: 'Тяжёлая броня',
    LEATHER: 'Кожаная броня',
    CLOTH: 'Тканевая броня',
  }
  return labels[category] ?? category
}

export function allowedClassesLabel(item: InventoryItem): string | null {
  if (!item.allowedClassIds.length) return null
  const labels: Record<string, string> = {
    WARRIOR: 'Воин',
    ARCHER: 'Лучник',
    MAGE: 'Маг',
  }
  return item.allowedClassIds.map(id => labels[id] ?? id).join(', ')
}

export function itemLevel(item: InventoryItem): number | null {
  return item.generatedItem?.itemLevel ?? null
}

export function itemStatRows(rawItem: InventoryItem): ItemStatRow[] {
  const item = rawItem as DetailedInventoryItem
  const rows: ItemStatRow[] = []
  const add = (id: string, label: string, value: number | null | undefined, suffix = '') => {
    if (!value) return
    rows.push({ id, label, value: `+${formatNumber(value)}${suffix}` })
  }

  if (item.weaponDamageMin !== null && item.weaponDamageMin !== undefined
    && item.weaponDamageMax !== null && item.weaponDamageMax !== undefined
    && item.weaponDamageMax > 0) {
    rows.push({
      id: 'weaponDamage',
      label: 'Урон оружия',
      value: `${formatNumber(item.weaponDamageMin)}–${formatNumber(item.weaponDamageMax)}`,
    })
  }

  add('strength', 'Сила', item.stats.strength)
  add('agility', 'Ловкость', item.stats.agility)
  add('intellect', 'Интеллект', item.stats.intellect)
  add('stamina', 'Выносливость', item.stats.stamina)
  add('maxHp', 'Макс. здоровье', item.stats.maxHp)
  add('attackPower', 'Сила атаки', item.stats.attackPower)
  add('spellPower', 'Сила заклинаний', item.stats.spellPower)
  add('criticalChance', 'Критический шанс', item.stats.criticalChance, '%')
  add('criticalDamage', 'Критический урон', item.stats.criticalDamage, '%')
  add('accuracy', 'Меткость', item.stats.accuracy, '%')
  add('armor', 'Броня', item.stats.armor)
  add('magicResistance', 'Сопротивление магии', item.stats.magicResistance)
  add('dodge', 'Уклонение', item.stats.dodge, '%')
  add('armorPenetration', 'Пробивание брони', item.stats.armorPenetration, '%')
  add('magicPenetration', 'Пробивание магии', item.stats.magicPenetration, '%')
  add('maxResource', 'Макс. ресурс', item.stats.maxResource)

  const attackSpeed = Math.max(item.attackSpeedPercent ?? 0, item.stats.attackSpeed ?? 0)
  add('attackSpeed', 'Скорость атаки', attackSpeed, '%')
  if ((item.dodgePercent ?? 0) > item.stats.dodge) add('dodgeBonus', 'Уклонение', item.dodgePercent, '%')
  add('blockChance', 'Шанс блока', item.blockChancePercent, '%')

  if ((item.blockValueMax ?? 0) > 0) {
    const min = item.blockValueMin ?? 0
    const max = item.blockValueMax ?? min
    rows.push({
      id: 'blockValue',
      label: 'Сила блока',
      value: min === max ? `+${formatNumber(max)}` : `${formatNumber(min)}–${formatNumber(max)}`,
    })
  }

  return dedupeRows(rows)
}

export function characterCombatSummary(character: CharacterSnapshot): CharacterCombatSummaryRow[] {
  const breakdown = character.statBreakdown as Record<string, { finalValue: number } | undefined>
  const effective = (id: string): number | null => breakdown[id]?.finalValue ?? null
  const rows: CharacterCombatSummaryRow[] = [
    {
      id: 'hp',
      label: 'Здоровье',
      value: `${formatNumber(character.vitals.currentHp)} / ${formatNumber(character.vitals.maxHp)}`,
    },
    {
      id: character.primaryAttribute.toLowerCase(),
      label: primaryAttributeLabel(character.primaryAttribute),
      value: formatNumber(primaryAttributeValue(character)),
    },
    {
      id: character.classId === 'MAGE' ? 'spellPower' : 'attackPower',
      label: character.classId === 'MAGE' ? 'Сила заклинаний' : 'Сила атаки',
      value: formatNumber(character.classId === 'MAGE' ? character.stats.spellPower : character.stats.attackPower),
    },
    {
      id: 'armor',
      label: 'Броня',
      value: formatNumber(character.stats.armor),
      effective: effective('armorDamageReductionPercent') === null
        ? null
        : `${formatPercent(effective('armorDamageReductionPercent')!)} физ. защиты`,
    },
    {
      id: 'criticalChance',
      label: 'Критический шанс',
      value: formatPercent(character.stats.criticalChance),
    },
    {
      id: 'dodge',
      label: 'Уклонение',
      value: formatPercent(character.stats.dodge),
    },
  ]

  const blockChance = effective('blockChance')
  const blockMin = effective('blockValueMin')
  const blockMax = effective('blockValueMax')
  if (blockChance !== null && blockChance > 0) {
    rows.push({ id: 'blockChance', label: 'Шанс блока', value: formatPercent(blockChance) })
  }
  if (blockMax !== null && blockMax > 0) {
    rows.push({
      id: 'blockValue',
      label: 'Сила блока',
      value: blockMin !== null && blockMin !== blockMax
        ? `${formatNumber(blockMin)}–${formatNumber(blockMax)}`
        : formatNumber(blockMax),
    })
  }

  return rows
}

export function formatNumber(value: number): string {
  const rounded = Math.round(value * 100) / 100
  return Number.isInteger(rounded)
    ? String(rounded)
    : rounded.toFixed(2).replace(/0+$/, '').replace(/\.$/, '')
}

export function formatPercent(value: number): string {
  return `${formatNumber(value)}%`
}

function primaryAttributeLabel(primary: CharacterSnapshot['primaryAttribute']): string {
  if (primary === 'STRENGTH') return 'Сила'
  if (primary === 'AGILITY') return 'Ловкость'
  return 'Интеллект'
}

function primaryAttributeValue(character: CharacterSnapshot): number {
  if (character.primaryAttribute === 'STRENGTH') return character.stats.strength
  if (character.primaryAttribute === 'AGILITY') return character.stats.agility
  return character.stats.intellect
}

function dedupeRows(rows: ItemStatRow[]): ItemStatRow[] {
  const result: ItemStatRow[] = []
  const seen = new Set<string>()
  for (const row of rows) {
    if (seen.has(row.id)) continue
    seen.add(row.id)
    result.push(row)
  }
  return result
}
