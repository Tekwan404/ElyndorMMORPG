import alphaWolf from '@/assets/monsters/alpha-wolf.png'
import boar from '@/assets/monsters/forest-boar.png'
import wolf from '@/assets/monsters/wolf.png'

const artByProfileId: Readonly<Record<string, string>> = {
  ARCHER_STARTER_PREDATOR: wolf,
  ARCHER_GUARDIAN: alphaWolf,
  ARCHER_TRAPPER: boar,
}

export function companionArtUrl(profileId: string): string | undefined {
  return artByProfileId[profileId]
}
