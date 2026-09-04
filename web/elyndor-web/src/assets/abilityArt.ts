const abilityArtModules = import.meta.glob<string>(
  './abilities/**/*.{png,jpg,jpeg,webp,svg}',
  { eager: true, import: 'default' },
)

const abilityArtById = new Map<string, string>(
  Object.entries(abilityArtModules).map(([path, url]) => {
    const fileName = path.split('/').pop() ?? path
    const iconId = fileName.replace(/\.[^.]+$/, '')
    return [iconId, url]
  }),
)

export function abilityArtUrl(iconId: string | null | undefined): string | undefined {
  if (!iconId) return undefined
  return abilityArtById.get(iconId)
}
