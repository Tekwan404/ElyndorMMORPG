export type JsonRecord = Record<string, unknown>

export interface CompletenessCheck {
  key: string
  label: string
  complete: boolean
  detail?: string
}

export interface EntityCompleteness {
  completed: number
  total: number
  ready: boolean
  checks: CompletenessCheck[]
}

export function evaluateEntityCompleteness(
  packageObject: JsonRecord,
  section: string,
  entity: JsonRecord,
): EntityCompleteness {
  const checks = section === 'monsters'
    ? monsterChecks(packageObject, entity)
    : section === 'items'
      ? itemChecks(entity)
      : genericChecks(entity)

  const completed = checks.filter(check => check.complete).length
  return {
    completed,
    total: checks.length,
    ready: completed === checks.length,
    checks,
  }
}

function monsterChecks(packageObject: JsonRecord, monster: JsonRecord): CompletenessCheck[] {
  const id = stringValue(monster.id)
  const aiId = stringValue(monster.aiProfileId)
  const lootId = stringValue(monster.lootTableId)
  const ai = recordArray(packageObject.monsterAiProfiles).find(entry => entry.id === aiId)
  const loot = recordArray(packageObject.lootTables).find(entry => entry.id === lootId)
  const location = recordArray(packageObject.locations).find(entry =>
    recordArray(entry.encounters).some(encounter => encounter.monsterId === id),
  )

  return [
    check('identity', 'Имя и описание', Boolean(stringValue(monster.displayName) || stringValue(monster.name)) && Boolean(stringValue(monster.description))),
    check('combat', 'Боевые характеристики', numberValue(monster.maxHp) > 0 && numberValue(monster.level) > 0),
    check('ai', 'AI profile', Boolean(ai), aiId || 'не назначен'),
    check('abilities', 'Abilities корректно связаны', arrayOfStrings(monster.abilityIds).every(abilityId =>
      recordArray(packageObject.abilities).some(ability => ability.id === abilityId))),
    check('loot', 'Loot заполнен', Boolean(loot) && recordArray(loot?.entries).length > 0, lootId || 'не назначен'),
    check('location', 'Добавлен в локацию', Boolean(location), stringValue(location?.id) || 'не добавлен'),
    check('art', 'Art ID', Boolean(stringValue(monster.artId)), stringValue(monster.artId) || 'не назначен'),
  ]
}

function itemChecks(item: JsonRecord): CompletenessCheck[] {
  const equipment = item.type === 'Equipment'
  return [
    check('identity', 'Название и описание', Boolean(stringValue(item.name)) && Boolean(stringValue(item.description))),
    check('rarity', 'Редкость', Boolean(stringValue(item.rarity))),
    check('level', 'Required level', numberValue(item.requiredLevel) >= 1),
    check('shape', 'Тип/слот', !equipment || Boolean(stringValue(item.slot))),
    check('icon', 'Icon ID', Boolean(stringValue(item.iconId)), stringValue(item.iconId) || 'не назначен'),
  ]
}

function genericChecks(entity: JsonRecord): CompletenessCheck[] {
  return [
    check('id', 'ID', Boolean(stringValue(entity.id))),
    check('name', 'Название', Boolean(stringValue(entity.displayName) || stringValue(entity.name) || stringValue(entity.englishName))),
  ]
}

function check(key: string, label: string, complete: boolean, detail?: string): CompletenessCheck {
  return { key, label, complete, detail }
}

function recordArray(value: unknown): JsonRecord[] {
  return Array.isArray(value) ? value.filter(isRecord) : []
}

function arrayOfStrings(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((entry): entry is string => typeof entry === 'string') : []
}

function numberValue(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : 0
}

function stringValue(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}
