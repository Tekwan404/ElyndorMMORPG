export type CreatableSection = 'monsters' | 'items' | 'lootTables' | 'merchants' | 'abilities'
export type NewItemType = 'Material' | 'Equipment' | 'Consumable'
export type MonsterTemplate = 'NormalMelee' | 'NormalCaster' | 'EliteMelee' | 'Boss'
export type AbilityScaling = 'AttackPower' | 'SpellPower'
export type JsonRecord = Record<string, unknown>

export interface CreateEntityRequest {
  section: CreatableSection
  id: string
  name?: string
  itemType?: NewItemType
  locationId?: string
  monsterTemplate?: MonsterTemplate
  monsterRank?: 'Normal' | 'Elite' | 'Boss'
  createLootTable?: boolean
  locationIds?: string[]
  encounterWeight?: number
  abilityScaling?: AbilityScaling
  abilitySchool?: string
}

export interface CreateEntityResult {
  packageObject: JsonRecord
  entity: JsonRecord
}

export interface DuplicateEntityRequest {
  section: string
  sourceId: string
  id: string
  name?: string
  copyMonsterLoot?: boolean
}

export function createDraftEntity(
  sourcePackage: JsonRecord,
  request: CreateEntityRequest,
): CreateEntityResult {
  const id = canonicalId(request.id)
  const name = request.name?.trim() ?? ''
  if (request.section !== 'lootTables' && !name) {
    throw new Error('Укажи название новой сущности.')
  }

  const packageObject = cloneJson(sourcePackage)
  const source = packageObject[request.section]
  if (!Array.isArray(source)) {
    throw new Error('В выбранной категории нет массива сущностей.')
  }
  ensureUniqueId(source, id)

  const entity = request.section === 'monsters'
    ? createMonster(packageObject, id, name, request)
    : request.section === 'items'
      ? createItem(id, name, request.itemType ?? 'Material')
      : request.section === 'lootTables'
        ? createLootTable(id)
        : request.section === 'abilities'
          ? createAbility(id, name, request.abilityScaling ?? 'AttackPower', request.abilitySchool)
          : createMerchant(packageObject, id, name, request.locationId)

  packageObject[request.section] = [...source, entity]
  return { packageObject, entity }
}

export function duplicateDraftEntity(
  sourcePackage: JsonRecord,
  request: DuplicateEntityRequest,
): CreateEntityResult {
  const id = canonicalId(request.id)
  const packageObject = cloneJson(sourcePackage)
  const source = packageObject[request.section]
  if (!Array.isArray(source)) {
    throw new Error('В выбранной категории нет массива сущностей.')
  }

  ensureUniqueId(source, id)
  const original = source.find(entry => isRecord(entry) && entry.id === request.sourceId)
  if (!isRecord(original)) {
    throw new Error('Исходная сущность не найдена.')
  }

  const entity = cloneJson(original)
  entity.id = id
  if (typeof request.name === 'string' && request.name.trim()) {
    if (typeof entity.displayName === 'string') entity.displayName = request.name.trim()
    if (typeof entity.name === 'string') entity.name = request.name.trim()
  }
  if (typeof entity.version === 'number') entity.version = 1

  if (request.section === 'monsters') {
    duplicateMonsterRelations(
      packageObject,
      original,
      entity,
      id,
      request.copyMonsterLoot ?? true,
    )
  }

  packageObject[request.section] = [...source, entity]
  return { packageObject, entity }
}

export function createAndLinkMonsterLootTable(
  sourcePackage: JsonRecord,
  monsterId: string,
): JsonRecord {
  const packageObject = cloneJson(sourcePackage)
  const monsters = recordArray(packageObject.monsters)
  const monster = monsters.find(entry => entry.id === monsterId)
  if (!monster) throw new Error('Monster не найден.')

  if (typeof monster.lootTableId === 'string' && monster.lootTableId) {
    return packageObject
  }

  const lootId = `${monsterId}_LOOT`
  const lootTables = packageObject.lootTables
  if (!Array.isArray(lootTables)) {
    throw new Error('В content package отсутствует lootTables.')
  }
  ensureUniqueId(lootTables, lootId)

  monster.lootTableId = lootId
  packageObject.monsters = monsters
  packageObject.lootTables = [...lootTables, createLootTable(lootId)]
  return packageObject
}

export function attachMonsterToLocation(
  sourcePackage: JsonRecord,
  monsterId: string,
  locationId: string,
  weight = 1,
): JsonRecord {
  if (!Number.isFinite(weight) || weight <= 0) {
    throw new Error('Encounter weight должен быть больше нуля.')
  }

  const packageObject = cloneJson(sourcePackage)
  const locations = recordArray(packageObject.locations)
  const location = locations.find(entry => entry.id === locationId)
  if (!location) throw new Error('Локация не найдена.')
  if (location.dangerLevel === 'SAFE') {
    throw new Error('Нельзя добавить hostile encounter в SAFE-локацию.')
  }

  const encounters = recordArray(location.encounters)
  if (!encounters.some(entry => entry.monsterId === monsterId)) {
    encounters.push({ monsterId, weight })
    location.encounters = encounters
  }
  packageObject.locations = locations
  return packageObject
}

export function addItemToLootTable(
  sourcePackage: JsonRecord,
  lootTableId: string,
  itemId: string,
): JsonRecord {
  const packageObject = cloneJson(sourcePackage)
  const tables = recordArray(packageObject.lootTables)
  const table = tables.find(entry => entry.id === lootTableId)
  if (!table) throw new Error('Loot Table не найдена.')

  const entries = recordArray(table.entries)
  if (!entries.some(entry => entry.itemId === itemId)) {
    const item = recordArray(packageObject.items).find(entry => entry.id === itemId)
    entries.push({
      itemId,
      dropChance: 1,
      minQuantity: 1,
      maxQuantity: item?.stackable === false ? 1 : 1,
    })
    table.entries = entries
  }

  packageObject.lootTables = tables
  return packageObject
}

function createMonster(
  packageObject: JsonRecord,
  id: string,
  name: string,
  request: CreateEntityRequest,
): JsonRecord {
  const aiProfileId = `${id}_BASIC_AI`
  const profiles = packageObject.monsterAiProfiles
  if (!Array.isArray(profiles)) {
    throw new Error('В content package отсутствует monsterAiProfiles.')
  }
  if (profiles.some(entry => isRecord(entry) && entry.id === aiProfileId)) {
    throw new Error(`AI profile ${aiProfileId} уже существует.`)
  }

  const defaults = monsterTemplateDefaults(request.monsterTemplate ?? 'NormalMelee')
  const rank = request.monsterRank ?? defaults.rank
  const createLoot = request.createLootTable === true
  const lootId = createLoot ? `${id}_LOOT` : null

  if (lootId) {
    const lootTables = packageObject.lootTables
    if (!Array.isArray(lootTables)) {
      throw new Error('В content package отсутствует lootTables.')
    }
    ensureUniqueId(lootTables, lootId)
    packageObject.lootTables = [...lootTables, createLootTable(lootId)]
  }

  packageObject.monsterAiProfiles = [
    ...profiles,
    {
      id: aiProfileId,
      priorityAbilityIds: [],
      version: 1,
    },
  ]

  const monster: JsonRecord = {
    id,
    name,
    rank,
    level: defaults.level,
    maxHp: defaults.maxHp,
    stats: {
      level: defaults.level,
      accuracy: 95,
      dodge: defaults.dodge,
      criticalChance: 5,
      criticalDamage: 1,
      armor: defaults.armor,
      magicResistance: defaults.magicResistance,
      armorPenetration: 0,
      magicPenetration: 0,
      attackPower: defaults.attackPower,
      spellPower: defaults.spellPower,
    },
    autoAttackInterval: defaults.autoAttackInterval,
    autoAttackBaseDamage: defaults.autoAttackBaseDamage,
    autoAttackAttackPowerCoefficient: defaults.autoAttackCoefficient,
    abilityIds: [],
    aiProfileId,
    version: 1,
    xpReward: defaults.xpReward,
    lootTableId: lootId,
    goldRewardMin: defaults.goldMin,
    goldRewardMax: defaults.goldMax,
    displayName: name,
    description: 'Новый противник Elyndor.',
    artId: null,
  }

  for (const locationId of request.locationIds ?? []) {
    packageObject = attachMonsterToLocation(
      packageObject,
      id,
      locationId,
      request.encounterWeight ?? 1,
    )
  }

  // attachMonsterToLocation clones the package, so carry its mutated location list back.
  if (request.locationIds?.length) {
    const updated = request.locationIds.reduce(
      (current, locationId) => attachMonsterToLocation(
        current,
        id,
        locationId,
        request.encounterWeight ?? 1,
      ),
      { ...packageObject, monsters: [monster] } as JsonRecord,
    )
    packageObject.locations = updated.locations
  }

  return monster
}

function monsterTemplateDefaults(template: MonsterTemplate) {
  if (template === 'NormalCaster') {
    return {
      rank: 'Normal' as const,
      level: 1,
      maxHp: 85,
      attackPower: 4,
      spellPower: 14,
      armor: 0,
      magicResistance: 6,
      dodge: 1,
      autoAttackInterval: '00:00:03',
      autoAttackBaseDamage: 3,
      autoAttackCoefficient: 0.35,
      xpReward: 0,
      goldMin: 0,
      goldMax: 0,
    }
  }
  if (template === 'EliteMelee') {
    return {
      rank: 'Elite' as const,
      level: 3,
      maxHp: 350,
      attackPower: 24,
      spellPower: 0,
      armor: 18,
      magicResistance: 8,
      dodge: 2,
      autoAttackInterval: '00:00:02.2',
      autoAttackBaseDamage: 10,
      autoAttackCoefficient: 0.7,
      xpReward: 40,
      goldMin: 4,
      goldMax: 8,
    }
  }
  if (template === 'Boss') {
    return {
      rank: 'Boss' as const,
      level: 5,
      maxHp: 1200,
      attackPower: 38,
      spellPower: 0,
      armor: 32,
      magicResistance: 18,
      dodge: 1,
      autoAttackInterval: '00:00:02',
      autoAttackBaseDamage: 16,
      autoAttackCoefficient: 0.8,
      xpReward: 150,
      goldMin: 20,
      goldMax: 40,
    }
  }
  return {
    rank: 'Normal' as const,
    level: 1,
    maxHp: 100,
    attackPower: 10,
    spellPower: 0,
    armor: 0,
    magicResistance: 0,
    dodge: 0,
    autoAttackInterval: '00:00:02.5',
    autoAttackBaseDamage: 5,
    autoAttackCoefficient: 0.5,
    xpReward: 0,
    goldMin: 0,
    goldMax: 0,
  }
}

function createAbility(
  id: string,
  name: string,
  scaling: AbilityScaling,
  requestedSchool?: string,
): JsonRecord {
  const spell = scaling === 'SpellPower'
  return {
    id,
    type: 'Instant',
    targetType: 'SingleEnemy',
    resourceCost: 0,
    cooldown: '00:00:03',
    castTime: '00:00:00',
    usesGlobalCooldown: true,
    globalCooldownCategory: 'Standard',
    isSpell: spell,
    school: requestedSchool?.trim() || (spell ? 'ARCANE' : 'PHYSICAL'),
    actions: [
      spell
        ? { type: 'Damage', damageType: 'Magical', spellPowerCoefficient: 1 }
        : { type: 'Damage', damageType: 'Physical', attackPowerCoefficient: 1 },
    ],
    displayName: name,
    description: 'Новая способность Elyndor.',
    iconId: null,
  }
}

function createLootTable(id: string): JsonRecord {
  return {
    id,
    version: 1,
    entries: [],
  }
}

function createMerchant(
  packageObject: JsonRecord,
  id: string,
  name: string,
  requestedLocationId?: string,
): JsonRecord {
  const locations = recordArray(packageObject.locations)
  const fallbackLocationId = locations
    .map(location => location.id)
    .find((value): value is string => typeof value === 'string')
  const locationId = requestedLocationId?.trim() || fallbackLocationId
  if (!locationId) {
    throw new Error('Для нового торговца нужна существующая локация.')
  }

  return {
    id,
    name,
    locationId,
    description: 'Новый торговец Elyndor.',
    itemIds: [],
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
    iconId: null,
    appearanceProfileId: null,
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
    base.slot = 'Amulet'
  } else if (type === 'Consumable') {
    base.maxStack = 20
    base.healAmount = 50
    base.consumableCooldownSeconds = 30
  }

  return base
}

function duplicateMonsterRelations(
  packageObject: JsonRecord,
  original: JsonRecord,
  entity: JsonRecord,
  newId: string,
  copyLoot: boolean,
): void {
  const aiProfiles = recordArray(packageObject.monsterAiProfiles)
  const sourceAiId = stringValue(original.aiProfileId)
  const sourceAi = aiProfiles.find(profile => profile.id === sourceAiId)
  const nextAiId = `${newId}_BASIC_AI`
  ensureUniqueId(aiProfiles, nextAiId)
  aiProfiles.push({
    id: nextAiId,
    priorityAbilityIds: Array.isArray(sourceAi?.priorityAbilityIds)
      ? cloneJson(sourceAi!.priorityAbilityIds as unknown as JsonRecord)
      : [],
    version: 1,
  })
  packageObject.monsterAiProfiles = aiProfiles
  entity.aiProfileId = nextAiId

  const sourceLootId = stringValue(original.lootTableId)
  if (!copyLoot || !sourceLootId) {
    entity.lootTableId = null
    return
  }

  const lootTables = recordArray(packageObject.lootTables)
  const sourceLoot = lootTables.find(table => table.id === sourceLootId)
  if (!sourceLoot) {
    entity.lootTableId = null
    return
  }
  const nextLootId = `${newId}_LOOT`
  ensureUniqueId(lootTables, nextLootId)
  const clonedLoot = cloneJson(sourceLoot)
  clonedLoot.id = nextLootId
  clonedLoot.version = 1
  lootTables.push(clonedLoot)
  packageObject.lootTables = lootTables
  entity.lootTableId = nextLootId
}

function canonicalId(value: string): string {
  const id = value.trim().toUpperCase()
  if (!/^[A-Z][A-Z0-9_]*$/.test(id)) {
    throw new Error('ID: только A-Z, 0-9 и _, первый символ — буква.')
  }
  return id
}

function ensureUniqueId(source: unknown[], id: string): void {
  if (source.some(entry => isRecord(entry) && entry.id === id)) {
    throw new Error(`ID ${id} уже существует.`)
  }
}

function recordArray(value: unknown): JsonRecord[] {
  return Array.isArray(value) ? value.filter(isRecord) : []
}

function stringValue(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

function cloneJson<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}
