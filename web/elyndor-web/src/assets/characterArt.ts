const characterArtModules = import.meta.glob<string>(
  './characters/personal/*.{png,jpg,jpeg,webp}',
  { eager: true, import: 'default' },
)

const characterArtByKey = new Map<string, string>(
  Object.entries(characterArtModules).map(([path, url]) => {
    const fileName = path.split('/').pop() ?? path
    return [fileName.replace(/\.[^.]+$/, ''), url]
  }),
)

function artKey(classId: string, genderId: string, variant: 'transparent' | 'scene'): string {
  return `${classId.toLowerCase()}-${genderId.toLowerCase()}-${variant}`
}

export function resolveCharacterArt(
  classId: string,
  genderId: string,
  variant: 'transparent' | 'scene' = 'transparent',
): string | null {
  const exact = characterArtByKey.get(artKey(classId, genderId, variant))
  if (exact) return exact

  const classFallback = characterArtByKey.get(artKey(classId, 'male', variant))
  if (classFallback) return classFallback

  return characterArtByKey.get(artKey('warrior', 'male', variant)) ?? null
}
