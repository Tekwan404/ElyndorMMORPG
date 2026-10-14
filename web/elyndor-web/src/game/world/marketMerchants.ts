export const MARKET_MERCHANTS = [
  {
    id: 'BORIN_ARMORY',
    name: 'Оружейник Борин',
    role: 'Воин и паладин',
    description: 'Сталь, щиты и тяжёлое снаряжение.',
  },
  {
    id: 'LIORA_HUNTING',
    name: 'Следопыт Лиора',
    role: 'Лучник',
    description: 'Луки, колчаны и снаряжение следопыта.',
  },
  {
    id: 'ELARA_ARCANA',
    name: 'Чародейка Элара',
    role: 'Маг',
    description: 'Посохи, фокусы и облачения.',
  },
  {
    id: 'MARCUS_SUPPLIES',
    name: 'Торговец Маркус',
    role: 'Зелья и припасы',
    description: 'Зелья, материалы и украшения в дорогу.',
  },
] as const

export function marketMerchant(id: string) {
  return MARKET_MERCHANTS.find((merchant) => merchant.id === id) ?? MARKET_MERCHANTS[3]
}
