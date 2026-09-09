import { describe, expect, it } from 'vitest'

import { itemArtUrl } from '@/assets/itemArt'

describe('item art', () => {
  it('prefers the root item asset when a set crop has the same icon id', () => {
    const url = itemArtUrl('mage_epic_shattered_star_focus')

    expect(url).toBeDefined()
    expect(url).not.toContain('/sets/')
  })

  it('keeps legacy nested warrior artwork available', () => {
    expect(itemArtUrl('item-warrior-sword')).toMatch(/item-warrior-sword\.png$/)
  })
})
