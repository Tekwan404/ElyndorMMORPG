import { GLYPHS } from './glyphs'
import type { IconConfig, ResolvedIcon } from './icon.types'

export function resolveIcon(config: IconConfig): ResolvedIcon {
  const rarity = config.rarity ?? 'common'
  const state = config.state ?? 'default'

  return {
    id: config.id,
    glyph: GLYPHS[config.glyph],
    modifier: config.modifier ? GLYPHS[config.modifier] : null,
    rarity,
    state,
    classes: [
      `icon--${rarity}`,
      ...(config.modifier ? [`icon--${config.modifier}`] : []),
      `icon--${state}`,
    ],
  }
}
