import { describe, expect, it, vi } from 'vitest'

import { createAppearancePreviewController } from '@/game/character/appearancePreviewController'

describe('appearance preview controller', () => {
  it('captures real appearance once and restores it when preview closes', () => {
    let renderedAppearance: string | null = 'REAL_APPEARANCE'
    const capture = vi.fn<() => string | null>(() => renderedAppearance)
    const applyPreview = vi.fn<(cosmeticId: string) => void>((cosmeticId) => { renderedAppearance = cosmeticId })
    const restore = vi.fn<(snapshot: string | null) => void>((snapshot) => { renderedAppearance = snapshot })
    const controller = createAppearancePreviewController({ capture, applyPreview, restore })

    controller.preview('COSMETIC_FIRE')
    controller.preview('COSMETIC_ICE')

    expect(capture).toHaveBeenCalledTimes(1)
    expect(renderedAppearance).toBe('COSMETIC_ICE')
    expect(controller.activeCosmeticId).toBe('COSMETIC_ICE')

    controller.close()

    expect(restore).toHaveBeenCalledWith('REAL_APPEARANCE')
    expect(renderedAppearance).toBe('REAL_APPEARANCE')
    expect(controller.activeCosmeticId).toBeNull()
  })

  it('does not mutate ownership or persist anything by itself', () => {
    let renderedAppearance: string | null = null
    const owned = new Set<string>()
    const controller = createAppearancePreviewController({
      capture: () => renderedAppearance,
      applyPreview: (cosmeticId) => { renderedAppearance = cosmeticId },
      restore: (snapshot) => { renderedAppearance = snapshot },
    })

    controller.preview('COSMETIC_FIRE')

    expect(owned.has('COSMETIC_FIRE')).toBe(false)
    controller.close()
    expect(renderedAppearance).toBeNull()
  })
})
