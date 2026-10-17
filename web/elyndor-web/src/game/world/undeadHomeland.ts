// Presentation names mirror content/locations/undead-homeland.json.
export const UNDEAD_LOCATION_NAMES: Record<string, string> = {
  UNDEAD_STARTER_TOWN: 'Тихоград',
  UNDEAD_WHISPERING_FOREST: 'Лес Последнего Шёпота',
  UNDEAD_FLOWER_MEADOW: 'Поляна Бледноцвета',
  UNDEAD_DEEP_FOREST: 'Могильная чаща',
  UNDEAD_OLD_ROAD: 'Тракт Усопших',
  UNDEAD_STONE_SPURS: 'Костяные отроги',
  UNDEAD_BLIGHTED_GROVE: 'Роща Тлена',
  UNDEAD_ASHEN_BORDER: 'Пепельный рубеж',
  UNDEAD_MOON_ASH_MARSHES: 'Топи Забвения',
  UNDEAD_ECLIPSE_OUTSKIRTS: 'Предместья Безмолвия',
  UNDEAD_SHATTERED_LANDS: 'Земли Угасших Клятв',
  UNDEAD_BLACKSTONE_HIGHLANDS: 'Надгробное нагорье',
  UNDEAD_CRIMSON_WASTELAND: 'Пустошь Потерянных Душ',
  UNDEAD_OBSIDIAN_EDGE: 'Предел Вечной Ночи',
  UNDEAD_ANCIENT_MINE: 'Шахта Погребённых',
  UNDEAD_BROODMOTHER_LAIR: 'Логово Могильной Матриархи',
  UNDEAD_ECLIPSED_CITADEL: 'Цитадель Безмолвия',
  UNDEAD_SHATTERED_ORDER_CITADEL_TEST: 'Обсерватория Мёртвых Звёзд',
  UNDEAD_HEART_OF_BLIGHTED_GROVE: 'Сердце Рощи Тлена',
  UNDEAD_SHATTERED_ORDER_CITADEL: 'Цитадель Угасших Клятв',
  UNDEAD_BLACK_BASTION: 'Бастион Вечной Ночи',
}

export function isTown(locationId: string | null | undefined): boolean {
  return locationId === 'STARTER_TOWN' || locationId === 'UNDEAD_STARTER_TOWN'
}

export function baseLocationId(locationId: string): string {
  return locationId.startsWith('UNDEAD_') ? locationId.slice(7) : locationId
}
