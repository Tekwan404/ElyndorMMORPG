import { describe, expect, it } from 'vitest'

import content from '../../../../content/economy/character-skins.json'
import { resolveCharacterArt, resolveSkinPreview } from '@/assets/characterArt'

describe('published character skins', () => {
  it('connects every female definition to a real cutout and the equipped resolver', () => {
    expect(content.characterSkins).toHaveLength(27)
    const ids = new Set<string>()
    for (const skin of content.characterSkins) {
      expect(ids.has(skin.id)).toBe(false)
      ids.add(skin.id)
      expect(resolveSkinPreview(skin.imageId)).toMatch(/\.webp$/)
      expect(resolveCharacterArt(skin.classId, skin.genderId, 'transparent', skin.id))
        .toBe(resolveSkinPreview(skin.imageId))
    }
  })
})
