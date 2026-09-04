export type CreatableSection = 'monsters' | 'items'
export type NewItemType = 'Material' | 'Equipment' | 'Consumable'
export type JsonRecord = Record<string, unknown>

export interface CreateEntityRequest {
  section: CreatableSection
  id: string
  name: string
  itemType?: NewItemType
}

export interface CreateEntityResult {
  packageObject: JsonRecord
  entity: JsonRecord
}

export function createDraftEntity(
  sourcePackage: JsonRecord,
  request: CreateEntityRequest,
): CreateEntityResult {
  const id = request.id.trim().toUpperCase()
  const name = request.name.trim()
  if (!/^[A-Z][A-Z0-9_]*$/.test(id)) {
    throw new Error('ID: только A-Z, 0-9 и _, первый символ — буква.')
  }
  if (!name) {
    throw new Error('Укажи название новой сущности.')
  }

  const packageObject = cloneJson(sourcePackage)
  const source = packageObject[request.section]
  if (!Array.isArray(source)) {
    throw new Error('В выбранной категории нет массива сущностей.')
  }
  if (source.some((entry) => isRecord(entry) && entry.id === id)) {
    throw new Error(`ID ${id} уже существует.`)
  }

  const entity = request.section === 'monsters'
    ? createMonster(packageObject, id, name)
    : createItem(id, name, request.itemType ?? 'Material')

  packageObject[request.section] = [...source, entity]
  return { packageObject, entity }
}

function createMonster(packageObject: JsonRecord, id: string, name: string): JsonRecord {
  const aiProfileId = `${id}_BASIC_AI`
  const profiles = packageObject.monsterAiProfiles
  if (!Array.isArray(profiles)) {
    throw new Error('В content package отсутствует monsterAiProfiles.')
  }
  if (profiles.some((entry) => isRecord(entry) && entry.id === aiProfileId)) {
    throw new Error(`AI profile ${aiProfileId} уже существует.`)
  }

  packageObject.monsterAiProfiles = [
    ...profiles,
    {
      id: aiProfileId,
      priorityAbilityIds: [],
      version: 1,
    },
  ]

  return {
    id,
    name,
    rank: 'Normal',
    level: 1,
    maxHp: 100,
    stats: {
      level: 1,
      accuracy: 95,
      dodge: 0,
      criticalChance: 5,
      criticalDamage: 1,
      armor: 0,
      magicResistance: 0,
      armorPenetration: 0,
      magicPenetration: 0,
      attackPower: 10,
      spellPower: 0,
    },
    autoAttackInterval: '00:00:02.5000000',
    autoAttackBaseDamage: 5,
    autoAttackAttackPowerCoefficient: 0.5,
    abilityIds: [],
    aiProfileId,
    version: 1,
    xpReward: 0,
    lootTableId: null,
    goldRewardMin: 0,
    goldRewardMax: 0,
    displayName: name,
    description: 'Новый противник Elyndor.',
    artId: null,
  }
}

function createItem(id: string, name: string, type: NewItemType): JsonRecord {
  const base: JsonRecord = {
    id,
    name,
    type,
    rarity: 'Common',
    requiredLevel: 1,
    stackable: true,
    maxStack: 99,
    slot: null,
    stats: {
      strength: 0,
      agility: 0,
      intellect: 0,
      stamina: 0,
    },
    description: 'Новый предмет Elyndor.',
    version: 1,
    setId: null,
    weaponBaseAttackIntervalSeconds: null,
    attackSpeedPercent: 0,
    dodgePercent: 0,
    healAmount: 0,
    consumableCooldownSeconds: 0,
    buyPriceGold: 0,
    sellPriceGold: 0,
    weaponCategory: null,
    armorCategory: null,
    allowedClassIds: [],
  }

  if (type === 'Equipment') {
    base.stackable = false
    base.maxStack = 1
    base.slot = 'Accessory'
  } else if (type === 'Consumable') {
    base.maxStack = 20
    base.healAmount = 50
    base.consumableCooldownSeconds = 30
  }

  return base
}

function cloneJson(value: JsonRecord): JsonRecord {
  return JSON.parse(JSON.stringify(value)) as JsonRecord
}

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}
