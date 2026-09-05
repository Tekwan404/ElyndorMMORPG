export type JsonRecord = Record<string, unknown>

export interface AdminEntityPresentation {
  id: string
  title: string
  subtitle: string
}

export interface AdminSectionDescriptor {
  key: string
  label: string
}

export interface AdminGlobalSearchResult {
  section: string
  sectionLabel: string
  entityId: string
  title: string
  subtitle: string
}

export interface AdminEntityReference {
  section: string
  sectionLabel: string
  entityId: string
  title: string
  path: string
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

export function searchAdminPackage(
  packageObject: JsonRecord,
  sections: readonly AdminSectionDescriptor[],
  query: string,
  limit = 20,
): AdminGlobalSearchResult[] {
  const normalized = normalize(query)
  if (!normalized || limit <= 0) return []

  const results: AdminGlobalSearchResult[] = []
  for (const section of sections) {
    const source = packageObject[section.key]
    if (!Array.isArray(source)) continue

    for (const entry of source) {
      if (!isRecord(entry)) continue
      if (!searchableValues(entry).some(value => normalize(value).includes(normalized))) {
        continue
      }

      const presentation = presentAdminEntity(entry)
      results.push({
        section: section.key,
        sectionLabel: section.label,
        entityId: presentation.id,
        title: presentation.title,
        subtitle: presentation.subtitle,
      })

      if (results.length >= limit) return results
    }
  }

  return results
}

export function findAdminEntityReferences(
  packageObject: JsonRecord,
  sections: readonly AdminSectionDescriptor[],
  targetId: string,
  limit = 50,
): AdminEntityReference[] {
  if (!targetId || limit <= 0) return []

  const results: AdminEntityReference[] = []

  for (const section of sections) {
    const source = packageObject[section.key]
    if (!Array.isArray(source)) continue

    for (const entry of source) {
      if (!isRecord(entry)) continue

      const presentation = presentAdminEntity(entry)
      const paths: string[] = []
      collectReferencePaths(entry, targetId, '', paths, true)

      for (const path of paths) {
        results.push({
          section: section.key,
          sectionLabel: section.label,
          entityId: presentation.id,
          title: presentation.title,
          path,
        })
        if (results.length >= limit) return results
      }
    }
  }

  return results
}

function collectReferencePaths(
  value: unknown,
  targetId: string,
  path: string,
  output: string[],
  isEntityRoot = false,
): void {
  if (typeof value === 'string') {
    if (value === targetId) output.push(path || '(value)')
    return
  }

  if (Array.isArray(value)) {
    value.forEach((entry, index) => {
      collectReferencePaths(entry, targetId, `${path}[${index}]`, output)
    })
    return
  }

  if (!isRecord(value)) return

  for (const [key, nested] of Object.entries(value)) {
    if (isEntityRoot && key === 'id' && nested === targetId) continue
    const nextPath = path ? `${path}.${key}` : key
    collectReferencePaths(nested, targetId, nextPath, output)
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
