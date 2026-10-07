export interface MapPoint {
  x: number
  y: number
}

export const BORDERLANDS_MAP_ID = 'BORDERLANDS'
export const OUTER_REACHES_MAP_ID = 'OUTER_REACHES'

export const WORLD_MAP_POSITIONS: Record<string, MapPoint> = {
  STARTER_TOWN: { x: 50, y: 48 },
  WHISPERING_FOREST: { x: 14, y: 19 },
  FLOWER_MEADOW: { x: 56, y: 59 },
  DEEP_FOREST: { x: 26, y: 35 },
  OLD_ROAD: { x: 15, y: 49 },
  STONE_SPURS: { x: 43, y: 32 },
  BLIGHTED_GROVE: { x: 75, y: 62 },
  ASHEN_BORDER: { x: 68, y: 39 },
  MOON_ASH_MARSHES: { x: 14, y: 70 },
  ECLIPSE_OUTSKIRTS: { x: 74, y: 28 },
  SHATTERED_LANDS: { x: 80, y: 50 },
  BLACKSTONE_HIGHLANDS: { x: 54, y: 18 },
  CRIMSON_WASTELAND: { x: 91, y: 62 },
  OBSIDIAN_EDGE: { x: 48, y: 80 },
  BLACK_BASTION: { x: 70, y: 86 },
  ANCIENT_MINE: { x: 36, y: 21 },
  ECLIPSED_CITADEL: { x: 78, y: 14 },
  SHATTERED_ORDER_CITADEL_TEST: { x: 90, y: 37 },
  HEART_OF_BLIGHTED_GROVE: { x: 84, y: 75 },
  SHATTERED_ORDER_CITADEL: { x: 92, y: 25 },
  BROODMOTHER_LAIR: { x: 31, y: 49 },
}

export const OUTER_REACHES_MAP_POSITIONS: Record<string, MapPoint> = {
  BLACK_CRAGS: { x: 22, y: 30 },
  PLAGUEWOOD: { x: 52, y: 43 },
  GLASS_DESERT: { x: 22, y: 55 },
  FROZEN_EDGE: { x: 78, y: 25 },
  DOOMED_LANDS: { x: 52, y: 69 },
  DEAD_REACHES: { x: 82, y: 61 },
}

export function worldMapPosition(
  locationId: string,
  fallbackIndex: number,
  mapId = BORDERLANDS_MAP_ID,
): MapPoint {
  const positions = mapId === OUTER_REACHES_MAP_ID
    ? OUTER_REACHES_MAP_POSITIONS
    : WORLD_MAP_POSITIONS
  const authoredPoint = positions[locationId]
  if (authoredPoint) return authoredPoint

  const column = fallbackIndex % 5
  const row = Math.floor(fallbackIndex / 5)
  return {
    x: 10 + column * 20,
    y: 12 + row * 18.5,
  }
}
