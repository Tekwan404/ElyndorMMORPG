const itemArtModules = import.meta.glob<string>(
  './items/**/*.{png,jpg,jpeg,webp,svg}',
  { eager: true, import: 'default' },
)

const itemArtById = new Map<string, string>(
  Object.entries(itemArtModules).map(([path, url]) => [
    path.replace(/^\.\/items\//, '').replace(/\.[^.]+$/, ''),
    url,
  ]),
)

export function itemArtUrl(iconId: string | null | undefined): string | undefined {
  if (!iconId) return undefined
  return itemArtById.get(iconId)
}
