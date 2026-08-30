export type GlyphName =
  | 'sword'
  | 'greatsword'
  | 'dagger'
  | 'axe'
  | 'bow'
  | 'staff'
  | 'shield'
  | 'helmet'
  | 'armor'
  | 'boots'
  | 'ring'
  | 'potion'
  | 'scroll'
  | 'chest'
  | 'ore'
  | 'herb'
  | 'fire'
  | 'ice'
  | 'lightning'
  | 'poison'
  | 'holy'
  | 'shadow'
  | 'skull'
  | 'star'
  | 'lock'

export type IconCategory =
  'weapon' | 'equipment' | 'consumable' | 'resource' | 'skill' | 'effect' | 'utility'

export type Rarity = 'common' | 'uncommon' | 'rare' | 'epic' | 'legendary' | 'unique'
export type ModifierName = 'fire' | 'ice' | 'lightning' | 'poison' | 'holy' | 'shadow'
export type IconState = 'default' | 'selected' | 'equipped' | 'locked' | 'disabled' | 'new'

export type GlyphDefinition = {
  readonly paths: readonly string[]
}

export type IconConfig = {
  readonly id: string
  readonly glyph: GlyphName
  readonly category: IconCategory
  readonly rarity?: Rarity
  readonly modifier?: ModifierName
  readonly state?: IconState
}

export type ResolvedIcon = {
  readonly id: string
  readonly glyph: GlyphDefinition
  readonly modifier: GlyphDefinition | null
  readonly rarity: Rarity
  readonly state: IconState
  readonly classes: readonly string[]
}
