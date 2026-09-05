export type JsonRecord = Record<string, unknown>

export interface AdminEntityPresentation {
  id: string
  title: string
  subtitle: string
}

export function presentAdminEntity(entity: JsonRecord): AdminEntityPresentation {
  const id = stringValue(entity.id) || '(without id)'
  const title = firstNonEmpty(
    entity.displayName,
    entity.name,
    entity.englishName,
    entity.prototypeIdentity,
  ) || id

  const descriptors = [
    stringValue(entity.type),
    stringValue(entity.rank),
    stringValue(entity.classId),
    stringValue(entity.locationId),
  ].filter(Boolean)

  return {
    id,
    title,
    subtitle: descriptors.join(' · '),
  }
}

export function filterAdminEntities(
  entities: JsonRecord[],
  query: string,
): JsonRecord[] {
  const normalized = normalize(query)
  if (!normalized) return entities

  return entities.filter(entity => searchableValues(entity)
    .some(value => normalize(value).includes(normalized)))
}

export function replaceDraftEntity(
  packageObject: JsonRecord,
  section: string,
  selectedEntityId: string,
  entity: JsonRecord,
): JsonRecord {
  const source = packageObject[section]
  if (!Array.isArray(source)) {
    throw new Error('В выбранной категории нет массива сущностей.')
  }

  const nextId = stringValue(entity.id)
  if (!nextId) {
    throw new Error('У сущности должен быть строковый id.')
  }

  const index = source.findIndex(item =>
    isRecord(item) && stringValue(item.id) === selectedEntityId,
  )
  if (index < 0) {
    throw new Error('Сущность больше не найдена в текущем draft.')
  }

  if (
    nextId !== selectedEntityId
    && source.some((item, itemIndex) =>
      itemIndex !== index
      && isRecord(item)
      && stringValue(item.id) === nextId)
  ) {
    throw new Error(`Сущность с ID '${nextId}' уже существует.`)
  }

  return {
    ...packageObject,
    [section]: source.map((item, itemIndex) =>
      itemIndex === index ? entity : item),
  }
}

function searchableValues(entity: JsonRecord): string[] {
  const preferred = [
    entity.id,
    entity.displayName,
    entity.name,
    entity.englishName,
    entity.description,
    entity.prototypeIdentity,
    entity.type,
    entity.rank,
    entity.classId,
    entity.locationId,
  ]

  return preferred
    .filter((value): value is string | number =>
      typeof value === 'string' || typeof value === 'number')
    .map(String)
}

function normalize(value: string): string {
  return value.trim().toLocaleLowerCase()
}

function firstNonEmpty(...values: unknown[]): string {
  for (const value of values) {
    const text = stringValue(value).trim()
    if (text) return text
  }
  return ''
}

function stringValue(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}
