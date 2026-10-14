import atlas from './store/spatial-artifacts-atlas.webp'

const positions: Record<string, string> = {
  SPATIAL_CRACKED_RING: '0% 0%',
  SPATIAL_MINOR_RING: '50% 0%',
  SPATIAL_EXPANDED_RING: '100% 0%',
  SPATIAL_SEAL: '0% 50%',
  SPATIAL_BOTTOMLESS_RING: '50% 50%',
  SPATIAL_POCKET_SHARD: '100% 50%',
  SPATIAL_VOID_SEAL: '0% 100%',
}

export function spatialArtifactArt(itemId: string, iconId?: string | null) {
  const position = positions[itemId] ?? positions[iconId?.toUpperCase() ?? '']
  return position
    ? { backgroundImage: 'url(' + atlas + ')', backgroundPosition: position }
    : undefined
}
