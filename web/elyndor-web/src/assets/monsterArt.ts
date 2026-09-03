const monsterArtModules = import.meta.glob<string>(
  './monsters/*.{png,jpg,jpeg,webp,svg}',
  { eager: true, import: 'default' },
)

const monsterArtById = new Map<string, string>(
  Object.entries(monsterArtModules).map(([path, url]) => {
    const fileName = path.split('/').pop() ?? path
    const artId = fileName.replace(/\.[^.]+$/, '')
    return [artId, url]
  }),
)

export function monsterArtUrl(artId: string | null | undefined): string | undefined {
  if (!artId) return undefined
  return monsterArtById.get(artId)
}
