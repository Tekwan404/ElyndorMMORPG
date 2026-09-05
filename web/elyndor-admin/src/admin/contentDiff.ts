export type ContentDiffKind = 'added' | 'removed' | 'changed'

export interface ContentDiffEntry {
  path: string
  kind: ContentDiffKind
  before: string | null
  after: string | null
}

type JsonRecord = Record<string, unknown>

export function diffContentJson(beforeJson: string, afterJson: string): ContentDiffEntry[] {
  const before: unknown = JSON.parse(beforeJson)
  const after: unknown = JSON.parse(afterJson)
  const entries: ContentDiffEntry[] = []
  walk(before, after, '', entries)
  return entries
}

function walk(
  before: unknown,
  after: unknown,
  path: string,
  entries: ContentDiffEntry[],
): void {
  if (Object.is(before, after)) return

  if (isRecord(before) && isRecord(after)) {
    const keys = new Set([...Object.keys(before), ...Object.keys(after)])
    for (const key of [...keys].sort()) {
      const nextPath = path ? `${path}.${key}` : key
      if (!(key in before)) {
        entries.push(change(nextPath, 'added', null, after[key]))
      } else if (!(key in after)) {
        entries.push(change(nextPath, 'removed', before[key], null))
      } else {
        walk(before[key], after[key], nextPath, entries)
      }
    }
    return
  }

  if (Array.isArray(before) && Array.isArray(after)) {
    const beforeById = indexById(before)
    const afterById = indexById(after)
    if (beforeById && afterById) {
      const ids = new Set([...beforeById.keys(), ...afterById.keys()])
      for (const id of [...ids].sort()) {
        const nextPath = `${path}[${id}]`
        const beforeItem = beforeById.get(id)
        const afterItem = afterById.get(id)
        if (beforeItem === undefined) {
          entries.push(change(nextPath, 'added', null, afterItem))
        } else if (afterItem === undefined) {
          entries.push(change(nextPath, 'removed', beforeItem, null))
        } else {
          walk(beforeItem, afterItem, nextPath, entries)
        }
      }
      return
    }

    const length = Math.max(before.length, after.length)
    for (let index = 0; index < length; index++) {
      const nextPath = `${path}[${index}]`
      if (index >= before.length) {
        entries.push(change(nextPath, 'added', null, after[index]))
      } else if (index >= after.length) {
        entries.push(change(nextPath, 'removed', before[index], null))
      } else {
        walk(before[index], after[index], nextPath, entries)
      }
    }
    return
  }

  entries.push(change(path || '$', 'changed', before, after))
}

function change(
  path: string,
  kind: ContentDiffKind,
  before: unknown,
  after: unknown,
): ContentDiffEntry {
  return {
    path,
    kind,
    before: before === null ? null : summarize(before),
    after: after === null ? null : summarize(after),
  }
}

function summarize(value: unknown): string {
  if (typeof value === 'string') return value
  if (typeof value === 'number' || typeof value === 'boolean') return String(value)
  if (value === null) return 'null'
  if (Array.isArray(value)) return `[${value.length} item(s)]`
  if (isRecord(value)) {
    const id = value.id
    if (typeof id === 'string') return `{ id: ${id} }`
    return '{…}'
  }
  return String(value)
}

function indexById(values: unknown[]): Map<string, JsonRecord> | null {
  const result = new Map<string, JsonRecord>()
  for (const value of values) {
    if (!isRecord(value) || typeof value.id !== 'string' || result.has(value.id)) {
      return null
    }
    result.set(value.id, value)
  }
  return result
}

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}
