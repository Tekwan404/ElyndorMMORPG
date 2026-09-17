import type { KnownAbility } from '@/api/contracts'

export function classLabel(classId: string): string {
  if (classId === 'WARRIOR') return 'Воин'
  if (classId === 'ARCHER') return 'Лучник'
  if (classId === 'MAGE') return 'Маг'
  if (classId === 'PALADIN') return 'Паладин'
  return 'Неизвестный класс'
}

export function raceLabel(raceId: string): string {
  if (raceId === 'HUMAN') return 'Человек'
  if (raceId === 'UNDEAD') return 'Нежить'
  return 'Неизвестная раса'
}

export function genderLabel(genderId: string): string {
  if (genderId === 'MALE') return 'Мужской'
  if (genderId === 'FEMALE') return 'Женский'
  return 'Не указан'
}

export function resourceLabel(resourceType: string): string {
  if (resourceType === 'RAGE') return 'Ярость'
  if (resourceType === 'MANA') return 'Мана'
  if (resourceType === 'FOCUS') return 'Фокус'
  return 'Ресурс'
}

const abilityPresentation: Record<string, { name: string; description: string }> = {
  STRIKE: {
    name: 'Удар',
    description: 'Базовая атака воина: наносит физический урон и накапливает ярость.',
  },
  WILD_STRIKE: {
    name: 'Дикий удар',
    description: 'Мощная одиночная атака с повышенным физическим уроном.',
  },
  WHIRLWIND: {
    name: 'Вихрь',
    description: 'Размашистая атака по всем противникам в бою.',
  },
  BASTION: {
    name: 'Бастион',
    description: 'На короткое время значительно снижает получаемый урон.',
  },
  BERSERK: {
    name: 'Берсерк',
    description: 'На время повышает силу, шанс критического удара и скорость атаки.',
  },
  SHIELD_BASH: {
    name: 'Удар щитом',
    description: 'Удар щитом, который ненадолго оглушает цель.',
  },
  PROVOKE: {
    name: 'Провокация',
    description: 'Заставляет выбранного противника атаковать воина.',
  },
  HEAVY_BLOW: {
    name: 'Тяжёлый удар',
    description: 'Сильный одиночный удар, усиленный силой атаки.',
  },
  BATTLE_FOCUS: {
    name: 'Боевой фокус',
    description: 'На короткое время повышает силу атаки.',
  },
  BATTLE_SHOUT: {
    name: 'Боевой клич',
    description: 'Мгновенно восстанавливает часть ярости.',
  },
}

export function abilityName(ability: KnownAbility): string {
  return abilityPresentation[ability.id]?.name ?? ability.displayName ?? 'Способность'
}

export function abilityDescription(ability: KnownAbility): string {
  return abilityPresentation[ability.id]?.description
    ?? 'Описание способности пока недоступно.'
}

export function abilityTypeLabel(type: string): string {
  if (type === 'Instant') return 'Мгновенная'
  if (type === 'Casted') return 'С применением'
  if (type === 'NextAttackModifier') return 'Усиление следующей атаки'
  if (type === 'Taunt') return 'Провокация'
  return 'Особая'
}

export function abilityTargetLabel(targetType: string): string {
  if (targetType === 'Self') return 'На себя'
  if (targetType === 'SingleEnemy') return 'Один противник'
  if (targetType === 'AllEnemiesInCombat') return 'Все противники'
  if (targetType === 'SingleAlly') return 'Один союзник'
  return 'Особая цель'
}
