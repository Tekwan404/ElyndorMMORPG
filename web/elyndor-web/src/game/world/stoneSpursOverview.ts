export type StoneSpursResident = {
  readonly id: string
  readonly name: string
  readonly level: string
  readonly artId: string | null
  readonly monsterId: string
  readonly kind: 'common' | 'rare' | 'boss'
  readonly hidden?: boolean
}

export type StoneSpursLoot = {
  readonly id: string
  readonly name: string
  readonly glyph: 'ore' | 'skull' | 'star' | 'lock'
  readonly rarity: 'common' | 'uncommon' | 'rare' | 'epic'
  readonly unknown?: boolean
}

export type StoneSpursContract = {
  readonly id: string
  readonly title: string
  readonly objective: string
  readonly current: number
  readonly target: number
  readonly rewards: readonly ('xp' | 'gold' | 'item' | 'magic')[]
}

export type StoneSpursNearbyPlayer = {
  readonly name: string
  readonly level: number
  readonly classId: 'ARCHER' | 'WARRIOR' | 'MAGE' | 'PALADIN'
  readonly genderId: 'MALE' | 'FEMALE'
}

export const STONE_SPURS_OVERVIEW = {
  locationId: 'STONE_SPURS',
  title: 'Каменные отроги',
  riskLabel: 'ВЫСОКИЙ РИСК',
  levelLabel: 'ур. 1–6',
  description: 'Руда, тяжёлая броня и вход в древнюю шахту.',
  playersCount: 7,
  residentsSummary: '3 обычных · 1 редкий · 1 босс',
  residents: [
    {
      id: 'wolf',
      name: 'Горный волк',
      level: 'ур. 3–5',
      artId: 'wolf',
      monsterId: 'STONE_SPURS_WOLF',
      kind: 'common',
    },
    {
      id: 'goblin-miner',
      name: 'Рудокоп-гоблин',
      level: 'ур. 4–6',
      artId: 'goblin',
      monsterId: 'STONE_SPURS_GOBLIN_MINER',
      kind: 'common',
    },
    {
      id: 'stone-guardian',
      name: 'Каменный страж',
      level: 'ур. 5–6',
      artId: 'kamennyi-strazh',
      monsterId: 'STONE_SPURS_STONE_GUARDIAN',
      kind: 'common',
    },
    {
      id: 'unknown',
      name: 'Неизвестный противник',
      level: '???',
      artId: null,
      monsterId: 'STONE_SPURS_UNKNOWN_RARE',
      kind: 'rare',
      hidden: true,
    },
    {
      id: 'boss',
      name: 'Хозяин отрогов',
      level: 'ур. 6',
      artId: 'molodoi-kamneglot',
      monsterId: 'STONE_SPURS_BOSS',
      kind: 'boss',
    },
  ] satisfies readonly StoneSpursResident[],
  lootStats: [
    { label: 'Материалы', value: 8 },
    { label: 'Снаряжение', value: 14 },
    { label: 'Редкости', value: 3 },
  ],
  loot: [
    { id: 'copper-ore', name: 'Медная руда', glyph: 'ore', rarity: 'common' },
    { id: 'tin-ore', name: 'Оловянная руда', glyph: 'ore', rarity: 'uncommon' },
    { id: 'rough-stone', name: 'Грубый камень', glyph: 'ore', rarity: 'common' },
    { id: 'bone', name: 'Кость', glyph: 'skull', rarity: 'common' },
    { id: 'violet-crystal', name: 'Фиолетовый кристалл', glyph: 'star', rarity: 'epic' },
    { id: 'unknown-loot', name: 'Неизвестный предмет', glyph: 'lock', rarity: 'rare', unknown: true },
  ] satisfies readonly StoneSpursLoot[],
  contracts: [
    {
      id: 'clear-mountain-pass',
      title: 'Зачистить горный проход',
      objective: 'Убить горных рудокопов',
      current: 2,
      target: 8,
      rewards: ['xp', 'gold', 'item'],
    },
    {
      id: 'find-ancient-mine',
      title: 'Найти вход в древнюю шахту',
      objective: 'Исследовать отмеченную область',
      current: 0,
      target: 1,
      rewards: ['xp', 'gold', 'magic'],
    },
  ] satisfies readonly StoneSpursContract[],
  nearbyPlayers: [
    { name: 'tekwan', level: 30, classId: 'ARCHER', genderId: 'MALE' },
    { name: 'Ragnar', level: 12, classId: 'WARRIOR', genderId: 'MALE' },
    { name: 'Aelira', level: 8, classId: 'MAGE', genderId: 'FEMALE' },
    { name: 'Mini', level: 30, classId: 'MAGE', genderId: 'MALE' },
  ] satisfies readonly StoneSpursNearbyPlayer[],
} as const
