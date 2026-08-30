import type { IconConfig } from '../icon.types'

export const EFFECT_ICON_PRESETS = {
  burning: { id: 'burning', glyph: 'fire', category: 'effect', modifier: 'fire' },
  frozen: { id: 'frozen', glyph: 'ice', category: 'effect', modifier: 'ice' },
  poisoned: { id: 'poisoned', glyph: 'poison', category: 'effect', modifier: 'poison' },
} as const satisfies Record<string, IconConfig>
