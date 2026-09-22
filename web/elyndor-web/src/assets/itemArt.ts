const itemArtModules = import.meta.glob<string>(
  './items/**/*.{png,jpg,jpeg,webp,svg}',
  { eager: true, import: 'default' },
)

const ITEM_ICON_ID = /^[A-Za-z0-9][A-Za-z0-9._-]*(?:\/[A-Za-z0-9][A-Za-z0-9._-]*)*$/

export function isCanonicalItemIconId(iconId: string | null | undefined): iconId is string {
  if (!iconId || !ITEM_ICON_ID.test(iconId)) return false
  return !iconId.split('/').some(segment => segment === '.' || segment === '..')
}

export function canonicalItemIconIdFromModulePath(path: string): string {
  const relative = path.replace(/^\.\/items\//, '')
  return relative.replace(/\.[^.]+$/, '')
}

const itemArtById = new Map<string, string>()
for (const [path, url] of Object.entries(itemArtModules)) {
  const iconId = canonicalItemIconIdFromModulePath(path)
  if (itemArtById.has(iconId)) {
    throw new Error(`Duplicate canonical item icon id: ${iconId}`)
  }
  itemArtById.set(iconId, url)
}

export function resolveItemArtUrl(iconId: string | null | undefined): string | undefined {
  if (!isCanonicalItemIconId(iconId)) return undefined
  return itemArtById.get(iconId)
}

/** @deprecated Use ItemIcon.vue; retained only until the branch migration replaces old call sites. */
export const itemArtUrl = resolveItemArtUrl
