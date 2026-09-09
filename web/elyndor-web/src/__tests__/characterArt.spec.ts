import { describe, expect, it } from 'vitest'

import { resolveCharacterArt } from '@/assets/characterArt'

describe('character art registry', () => {
  it('resolves class and gender portraits with a safe fallback', () => {
    expect(resolveCharacterArt('MAGE', 'FEMALE')).toMatch(/mage-female-transparent\.webp$/)
    expect(resolveCharacterArt('ARCHER', 'MALE', 'scene')).toMatch(/archer-male-scene\.webp$/)
    expect(resolveCharacterArt('ARCHER', 'FEMALE', 'scene')).toMatch(/archer-female-scene\.webp$/)
    expect(resolveCharacterArt('WARRIOR', 'FEMALE')).toMatch(/warrior-male-transparent\.webp$/)
  })
})
