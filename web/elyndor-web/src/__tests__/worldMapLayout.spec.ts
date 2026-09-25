import { describe, expect, it } from 'vitest'

import { worldMapPosition, WORLD_MAP_POSITIONS } from '@/game/world/worldMapLayout'

const LOCATION_IDS = [
  'STARTER_TOWN',
  'WHISPERING_FOREST',
  'FLOWER_MEADOW',
  'DEEP_FOREST',
  'OLD_ROAD',
  'STONE_SPURS',
  'BLIGHTED_GROVE',
  'ASHEN_BORDER',
  'MOON_ASH_MARSHES',
  'ECLIPSE_OUTSKIRTS',
  'SHATTERED_LANDS',
  'BLACKSTONE_HIGHLANDS',
  'CRIMSON_WASTELAND',
  'OBSIDIAN_EDGE',
  'BLACK_BASTION',
  'ANCIENT_MINE',
  'ECLIPSED_CITADEL',
  'SHATTERED_ORDER_CITADEL_TEST',
  'HEART_OF_BLIGHTED_GROVE',
  'SHATTERED_ORDER_CITADEL',
  'BROODMOTHER_LAIR',
]

describe('world map placement', () => {
  it('has a hand-authored position for every current world and dungeon location', () => {
    expect(Object.keys(WORLD_MAP_POSITIONS).sort()).toEqual([...LOCATION_IDS].sort())
  })

  it('places location markers over their matching biomes', () => {
    expect(worldMapPosition('WHISPERING_FOREST', 0)).toMatchObject({ x: 14, y: 19 })
    expect(worldMapPosition('MOON_ASH_MARSHES', 0).x).toBeLessThan(30)
    expect(worldMapPosition('MOON_ASH_MARSHES', 0).y).toBeGreaterThan(60)
    expect(worldMapPosition('BLIGHTED_GROVE', 0).x).toBeGreaterThan(70)
    expect(worldMapPosition('ECLIPSED_CITADEL', 0).y).toBeLessThan(30)
    expect(worldMapPosition('SHATTERED_ORDER_CITADEL', 0).y).toBeLessThan(30)
    expect(worldMapPosition('OBSIDIAN_EDGE', 0).y).toBeGreaterThanOrEqual(70)
    expect(worldMapPosition('BLACK_BASTION', 0).y).toBeGreaterThanOrEqual(70)
  })

  it('keeps authored touch targets inside the map frame', () => {
    expect(Object.entries(WORLD_MAP_POSITIONS).every(([, point]) =>
      point.x >= 8 && point.x <= 92 && point.y >= 6 && point.y <= 94,
    )).toBe(true)
  })

  it('keeps fallback markers distinct and inside the map for new content ids', () => {
    const points = Array.from({ length: 21 }, (_, index) =>
      worldMapPosition(`UNMAPPED_LOCATION_${index}`, index),
    )

    expect(new Set(points.map(point => `${point.x}:${point.y}`)).size).toBe(points.length)
    expect(points.every(point => point.x >= 10 && point.x <= 90 && point.y >= 12 && point.y <= 86)).toBe(true)
  })

  it('keeps the 44px map touch targets from overlapping on a 320px phone', () => {
    const entries = Object.entries(WORLD_MAP_POSITIONS)
    for (let firstIndex = 0; firstIndex < entries.length; firstIndex += 1) {
      const [firstId, first] = entries[firstIndex]!
      for (let secondIndex = firstIndex + 1; secondIndex < entries.length; secondIndex += 1) {
        const [secondId, second] = entries[secondIndex]!
        const horizontalDistance = Math.abs(first.x - second.x) * 2.88
        const verticalDistance = Math.abs(first.y - second.y) * 4.07

        expect(
          horizontalDistance >= 44 || verticalDistance >= 44,
          `${firstId} and ${secondId} overlap their mobile touch targets`,
        ).toBe(true)
      }
    }
  })
})
