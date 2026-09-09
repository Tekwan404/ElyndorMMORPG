const itemArtModules = import.meta.glob<string>(
  './items/**/*.{png,jpg,jpeg,webp,svg}',
  { eager: true, import: 'default' },
)

function itemArtPriority(path: string): number {
  if (path.startsWith('./items/sets/')) return 0
  if (path.split('/').length === 3) return 2
  return 1
}

const itemArtById = new Map<string, string>(
  Object.entries(itemArtModules)
    .sort(([firstPath], [secondPath]) =>
      itemArtPriority(firstPath) - itemArtPriority(secondPath)
      || firstPath.localeCompare(secondPath),
    )
    .map(([path, url]) => {
      const fileName = path.split('/').pop() ?? path
      const iconId = fileName.replace(/\.[^.]+$/, '')
      return [iconId, url]
    }),
)

export function itemArtUrl(iconId: string | null | undefined): string | undefined {
  if (!iconId) return undefined
  return itemArtById.get(iconId)
}
