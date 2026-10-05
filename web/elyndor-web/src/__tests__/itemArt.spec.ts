import { describe, expect, it } from 'vitest'

import { itemArtUrl } from '@/assets/itemArt'

describe('item art', () => {
  it('resolves all forge materials as WebP', () => {
    for (const id of ['forge_scrap', 'enhancement_ore', 'reforge_stone']) {
      expect(itemArtUrl(`forge/${id}`)).toMatch(/\.webp$/)
    }
  })
  it('resolves a canonical nested set asset', () => {
    const nestedUrl = itemArtUrl('sets/ancient-mine/mine_tracker/mine_tracker_helmet')

    expect(nestedUrl).toMatch(/sets\/ancient-mine\/mine_tracker\/mine_tracker_helmet\.webp$/)
  })

  it('resolves a canonical non-set asset', () => {
    expect(itemArtUrl('items_outside_sets/ancient_mine/mine_watch_crossbow'))
      .toMatch(/items_outside_sets\/ancient_mine\/mine_watch_crossbow\.webp$/)
  })
})
