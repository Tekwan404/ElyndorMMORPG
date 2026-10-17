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
  const merchant =
    MARKET_MERCHANTS.find((merchant) => merchant.id === id.replace(/^UNDEAD_/, '')) ??
    MARKET_MERCHANTS[3]
  if (!id.startsWith('UNDEAD_')) return merchant
  const names: Record<string, string> = {
    BORIN_ARMORY: 'Костоправ Гримм',
    LIORA_HUNTING: 'Ловчая Нэрис',
    ELARA_ARCANA: 'Чародейка Велена',
    MARCUS_SUPPLIES: 'Снабженец Морт',
  }
  return { ...merchant, id, name: names[merchant.id] ?? merchant.name }
}
