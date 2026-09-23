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
  skinId: string | null = null,
): string | null {
  if (skinId) {
    const skin = characterArtByKey.get(skinId.toLowerCase().replace(/_/g, '-'))
    if (skin && skinId.startsWith(`${classId.toUpperCase()}_${genderId.toUpperCase()}_`)) return skin
  }
  const exact = characterArtByKey.get(artKey(classId, genderId, variant))
  if (exact) return exact

  const defaultArt = characterArtByKey.get(`${classId.toLowerCase()}-${genderId.toLowerCase()}-default`)
  if (defaultArt) return defaultArt

  const classFallback = characterArtByKey.get(artKey(classId, 'male', variant))
  if (classFallback) return classFallback

  return characterArtByKey.get(`${classId.toLowerCase()}-male-default`)
    ?? characterArtByKey.get('warrior-male-default') ?? null
}

export function resolveSkinPreview(imageId: string): string | null {
  return characterArtByKey.get(imageId) ?? null
}
