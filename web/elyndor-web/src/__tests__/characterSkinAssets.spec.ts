import { describe, expect, it } from 'vitest'

import content from '../../../../content/economy/character-skins.json'
import undeadContent from '../../../../content/economy/undead-character-skins.json'
import { resolveCharacterArt, resolveSkinPreview } from '@/assets/characterArt'

describe('published character skins', () => {
  it('connects every female definition to a real cutout and the equipped resolver', () => {
    expect(content.characterSkins).toHaveLength(27)
    const ids = new Set<string>()
    for (const skin of content.characterSkins) {
      expect(ids.has(skin.id)).toBe(false)
      ids.add(skin.id)
      expect(resolveSkinPreview(skin.imageId)).toMatch(/\.webp$/)
      expect(resolveCharacterArt(skin.classId, skin.genderId, 'transparent', skin.id)).toBe(
        resolveSkinPreview(skin.imageId),
      )
    }
  })

  it('connects all eighty undead variants for both genders to their race-specific art', () => {
    expect(undeadContent.characterSkins).toHaveLength(80)
    const ids = new Set<string>()
    for (const skin of undeadContent.characterSkins) {
      expect(ids.has(skin.id)).toBe(false)
      ids.add(skin.id)
      const art = resolveSkinPreview(skin.imageId)
      expect(art).toMatch(/\.webp$/)
      expect(
        resolveCharacterArt(skin.classId, skin.genderId, 'transparent', skin.id, 'UNDEAD'),
      ).toBe(art)
      expect(resolveCharacterArt(skin.classId, skin.genderId, 'scene', skin.id)).toBe(art)
    }
  })
})
