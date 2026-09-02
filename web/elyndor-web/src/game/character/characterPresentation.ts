import type { KnownAbility } from '@/api/contracts'

export function classLabel(classId: string): string {
  if (classId === 'WARRIOR') return 'Воин'
  if (classId === 'ARCHER') return 'Лучник'
  if (classId === 'MAGE') return 'Маг'
  return classId
}

export function raceLabel(raceId: string): string {
  if (raceId === 'HUMAN') return 'Человек'
  if (raceId === 'UNDEAD') return 'Нежить'
  return raceId
}

export function genderLabel(genderId: string): string {
  if (genderId === 'MALE') return 'Мужской'
  if (genderId === 'FEMALE') return 'Женский'
  return genderId
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
    description: 'Базовая атака воина. Наносит физический урон и помогает накапливать ярость.',
  },
  WILD_STRIKE: {
    name: 'Дикий удар',
    description: 'Мощная одиночная атака, открываемая талантом. Наносит повышенный физический урон.',
  },
  WHIRLWIND: {
    name: 'Вихрь',
    description: 'Размашистая атака по всем противникам в бою. Открывается соответствующим талантом.',
  },
  BASTION: {
    name: 'Бастион',
    description: 'Защитная способность, которая на короткое время значительно снижает входящий урон.',
  },
  BERSERK: {
    name: 'Берсерк',
    description: 'Боевой режим, временно усиливающий силу атаки, шанс критического удара и скорость атаки.',
  },
  SHIELD_BASH: {
    name: 'Удар щитом',
    description: 'Физический удар щитом, который также ненадолго оглушает цель.',
  },
  PROVOKE: {
    name: 'Провокация',
    description: 'Заставляет выбранного противника сосредоточить внимание на воине.',
  },
  HEAVY_BLOW: {
    name: 'Тяжёлый удар',
    description: 'Сильный одиночный физический удар с повышенным коэффициентом силы атаки.',
  },
  BATTLE_FOCUS: {
    name: 'Боевой фокус',
    description: 'На короткое время повышает силу атаки персонажа.',
  },
  BATTLE_SHOUT: {
    name: 'Боевой клич',
    description: 'Мгновенно восстанавливает часть ярости для продолжения атаки.',
  },
}

export function abilityName(ability: KnownAbility): string {
  return abilityPresentation[ability.id]?.name ?? ability.id.replace(/_/g, ' ')
}

export function abilityDescription(ability: KnownAbility): string {
  return abilityPresentation[ability.id]?.description
    ?? `Активная боевая способность персонажа.`
}

export function abilityTypeLabel(type: string): string {
  if (type === 'Instant') return 'Мгновенная'
  if (type === 'Casted') return 'С применением'
  if (type === 'NextAttackModifier') return 'Усиление следующей атаки'
  if (type === 'Taunt') return 'Провокация'
  return type
}

export function abilityTargetLabel(targetType: string): string {
  if (targetType === 'Self') return 'На себя'
  if (targetType === 'SingleEnemy') return 'Один противник'
  if (targetType === 'AllEnemiesInCombat') return 'Все противники'
  if (targetType === 'SingleAlly') return 'Один союзник'
  return targetType
}
