import { describe, expect, it } from 'vitest'

import { resolveCharacterArt, resolveSkinPreview } from '@/assets/characterArt'

describe('character art registry', () => {
  it('resolves class and gender portraits with a safe fallback', () => {
    expect(resolveCharacterArt('MAGE', 'FEMALE')).toMatch(/mage-female-default\.webp$/)
    expect(resolveCharacterArt('ARCHER', 'MALE', 'scene')).toMatch(/archer-male-default\.webp$/)
    expect(resolveCharacterArt('ARCHER', 'FEMALE', 'scene')).toMatch(/archer-female-default\.webp$/)
    expect(resolveCharacterArt('WARRIOR', 'FEMALE')).toMatch(/warrior-female-default\.webp$/)
    expect(resolveCharacterArt('WARRIOR', 'FEMALE', 'scene')).toMatch(/warrior-female-default\.webp$/)
  })

  it('uses the equipped skin for both profile and battlefield artwork', () => {
    expect(resolveCharacterArt('MAGE', 'FEMALE', 'transparent', 'MAGE_FEMALE_FIRE')).toMatch(/mage-female-fire\.webp$/)
    expect(resolveCharacterArt('MAGE', 'FEMALE', 'scene', 'MAGE_FEMALE_FIRE')).toMatch(/mage-female-fire\.webp$/)
    expect(resolveCharacterArt('WARRIOR', 'FEMALE', 'transparent', 'MAGE_FEMALE_FIRE')).toMatch(/warrior-female-default\.webp$/)
    expect(resolveSkinPreview('archer-female-admin')).toMatch(/archer-female-admin\.webp$/)
    expect(resolveSkinPreview('../bad')).toBeNull()
  })
})
