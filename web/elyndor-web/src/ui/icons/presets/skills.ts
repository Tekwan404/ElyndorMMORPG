import type { IconConfig } from '../icon.types'

export const SKILL_ICON_PRESETS = {
  flameStrike: { id: 'flame-strike', glyph: 'greatsword', category: 'skill', modifier: 'fire' },
  frozenArrow: { id: 'frozen-arrow', glyph: 'bow', category: 'skill', modifier: 'ice' },
  shadowBolt: { id: 'shadow-bolt', glyph: 'staff', category: 'skill', modifier: 'shadow' },
} as const satisfies Record<string, IconConfig>
