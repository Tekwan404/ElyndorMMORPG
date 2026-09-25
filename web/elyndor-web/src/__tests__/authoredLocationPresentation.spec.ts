import { describe, expect, it } from 'vitest'

import { locationPresentation } from '@/game/world/locationPresentation'

describe('authored location presentation', () => {
  it('presents every authored group instance as a dungeon', () => {
    for (const id of [
      'FLOWER_MEADOW',
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
      'HEART_OF_BLIGHTED_GROVE',
      'SHATTERED_ORDER_CITADEL',
      'BLACK_BASTION',
    ]) {
      const presentation = locationPresentation(id)
      expect(presentation.art).not.toBe(locationPresentation('UNKNOWN').art)
      expect(presentation.label).not.toBe('Неизвестная область')
    }

    expect(locationPresentation('HEART_OF_BLIGHTED_GROVE').dangerLabel).toBe(
      'Подземелье · 20 уровень',
    )
    expect(locationPresentation('SHATTERED_ORDER_CITADEL').dangerLabel).toBe(
      'Подземелье · 30 уровень',
    )
    expect(locationPresentation('BLACK_BASTION').dangerLabel).toBe('Подземелье · 40 уровень')
    expect(locationPresentation('SHATTERED_ORDER_CITADEL_TEST').label).toBe(
      'Обсерватория Расколотого Зеркала',
    )
  })
})
