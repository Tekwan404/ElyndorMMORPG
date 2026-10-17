import { RaceIds } from '@/game/character/raceIds'

const characterArtModules = import.meta.glob<string>(
  './characters/personal/*.{png,jpg,jpeg,webp}',
  { eager: true, import: 'default' },
)

// CI generates compact 320px WebP shop previews before the frontend build.
const thumbnailModules = import.meta.glob<string>(
  './characters/personal/thumbnails/*.webp',
  { eager: true, import: 'default' },
)
const thumbnailByKey = new Map<string, string>(
  Object.entries(thumbnailModules).map(([path, url]) => {
    const fileName = path.split('/').pop() ?? path
    return [fileName.replace(/\.[^.]+$/, ''), url]
  }),
)

const characterArtByKey = new Map<string, string>(
  Object.entries(characterArtModules).map(([path, url]) => {
    const fileName = path.split('/').pop() ?? path
    return [fileName.replace(/\.[^.]+$/, ''), url]
  }),
)

function skinRaceId(skinId: string): string {
  // The third token is the race only for race-specific catalog skins.
  // Older human skin IDs use their variant in this position instead.
  const candidate = skinId.split('_')[2]
  return Object.values(RaceIds).find((race) => race === candidate) ?? RaceIds.Human
}

function artKey(classId: string, genderId: string, variant: 'transparent' | 'scene'): string {
  return `${classId.toLowerCase()}-${genderId.toLowerCase()}-${variant}`
}

export function resolveCharacterArt(
  classId: string,
  genderId: string,
  variant: 'transparent' | 'scene' = 'transparent',
  skinId: string | null = null,
  raceId?: string,
): string | null {
  const race = raceId ?? (skinId ? skinRaceId(skinId) : RaceIds.Human)
  if (skinId) {
    const skin = characterArtByKey.get(skinId.toLowerCase().replace(/_/g, '-'))
    if (
      skin &&
      skinId.startsWith(`${classId.toUpperCase()}_${genderId.toUpperCase()}_`) &&
      skinRaceId(skinId) === race
    )
      return skin
  }
  if (race === RaceIds.Undead) {
    const starter = characterArtByKey.get(
      `${classId.toLowerCase()}-${genderId.toLowerCase()}-undead-starter`,
    )
    if (starter) return starter
  }
  const exact = characterArtByKey.get(artKey(classId, genderId, variant))
  if (exact) return exact

  const defaultArt = characterArtByKey.get(
    `${classId.toLowerCase()}-${genderId.toLowerCase()}-default`,
  )
  if (defaultArt) return defaultArt

  const classFallback = characterArtByKey.get(artKey(classId, 'male', variant))
  if (classFallback) return classFallback

  return (
    characterArtByKey.get(`${classId.toLowerCase()}-male-default`) ??
    characterArtByKey.get('warrior-male-default') ??
    null
  )
}

export function resolveSkinPreview(imageId: string): string | null {
  return characterArtByKey.get(imageId) ?? null
}

/** Small shop card preview. Full-resolution art is reserved for the character viewer. */
export function resolveSkinThumbnail(imageId: string): string | null {
  return thumbnailByKey.get(imageId) ?? resolveSkinPreview(imageId)
}
