import type { IconConfig } from '../icon.types'

export const ITEM_ICON_PRESETS = {
  flameblade: {
    id: 'flameblade',
    glyph: 'sword',
    category: 'weapon',
    rarity: 'epic',
    modifier: 'fire',
  },
  frostStaff: {
    id: 'frost-staff',
    glyph: 'staff',
    category: 'weapon',
    rarity: 'rare',
    modifier: 'ice',
  },
  poisonDagger: {
    id: 'poison-dagger',
    glyph: 'dagger',
    category: 'weapon',
    rarity: 'uncommon',
    modifier: 'poison',
  },
  healingPotion: {
    id: 'healing-potion',
    glyph: 'potion',
    category: 'consumable',
    rarity: 'common',
  },
  lockedChest: {
    id: 'locked-chest',
    glyph: 'chest',
    category: 'utility',
    rarity: 'rare',
    state: 'locked',
  },
  newOre: { id: 'new-ore', glyph: 'ore', category: 'resource', rarity: 'common', state: 'new' },
} as const satisfies Record<string, IconConfig>
