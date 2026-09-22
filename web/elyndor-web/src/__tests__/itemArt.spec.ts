import { describe, expect, it } from 'vitest'

import { itemArtUrl } from '@/assets/itemArt'

describe('item art', () => {
  it('keeps canonical paths distinct when assets share a basename', () => {
    const nestedUrl = itemArtUrl('sets/mage_epic_shattered_star_focus')
    const rootUrl = itemArtUrl('mage_epic_shattered_star_focus')

    expect(nestedUrl).toMatch(/sets\/mage_epic_shattered_star_focus\.webp$/)
    expect(rootUrl).toMatch(/items\/mage_epic_shattered_star_focus\.webp$/)
    expect(nestedUrl).not.toBe(rootUrl)
  })

  it('keeps canonical nested warrior artwork available', () => {
    expect(itemArtUrl('warrior/item-warrior-sword')).toMatch(/item-warrior-sword\.png$/)
  })
})
