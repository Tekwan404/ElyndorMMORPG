import { describe, expect, it } from 'vitest'

import { GLYPHS } from '@/ui/icons/glyphs'
import { resolveIcon } from '@/ui/icons/icon-renderer'

describe('resolveIcon', () => {
  it('resolves glyph rarity modifier and state into semantic layers', () => {
    const result = resolveIcon({
      id: 'flameblade',
      glyph: 'sword',
      category: 'weapon',
      rarity: 'epic',
      modifier: 'fire',
      state: 'selected',
    })

    expect(result.glyph).toBe(GLYPHS.sword)
    expect(result.modifier).toBe(GLYPHS.fire)
    expect(result.classes).toEqual(['icon--epic', 'icon--fire', 'icon--selected'])
  })

  it('uses common rarity and default state when optional values are absent', () => {
    const result = resolveIcon({ id: 'iron-ore', glyph: 'ore', category: 'resource' })

    expect(result.rarity).toBe('common')
    expect(result.state).toBe('default')
    expect(result.modifier).toBeNull()
    expect(result.classes).toEqual(['icon--common', 'icon--default'])
  })
})
