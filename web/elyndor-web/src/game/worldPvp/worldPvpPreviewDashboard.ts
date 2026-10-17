import type { WorldPvpDashboard } from './worldPvpContracts'

/** Fixture for the development preview only. Never pass this to the live player screen. */
export const worldPvpPreviewDashboard: WorldPvpDashboard = {
  season: {
    id: 'PREVIEW-S1',
    name: 'Сезон I — Война за пределы',
    status: 'Active',
    endsAtUtc: '2026-12-12T00:00:00Z',
  },
  personal: {
    characterId: 'preview-undead-hero',
    faction: 'UNDEAD',
    rank: 12,
    score: 3450,
    kills: 94,
    assists: 31,
    deaths: 26,
    bestStreak: 18,
    currentStreak: 6,
    fieldHonor: 1840,
    bloodTokens: 125,
  },
  standings: [
    { characterId: 'preview-human-a', name: 'Арториус', faction: 'HUMAN', rank: 1, score: 8920, kills: 219, assists: 73, deaths: 31 },
    { characterId: 'preview-undead-a', name: 'Мортарион', faction: 'UNDEAD', rank: 2, score: 8100, kills: 204, assists: 49, deaths: 35 },
    { characterId: 'preview-human-b', name: 'Лиандра', faction: 'HUMAN', rank: 3, score: 7690, kills: 165, assists: 120, deaths: 27 },
    { characterId: 'preview-undead-hero', name: 'Тенеклык', faction: 'UNDEAD', rank: 12, score: 3450, kills: 94, assists: 31, deaths: 26 },
  ],
  rewards: [
    { id: 'preview-cloak', name: 'Плащ кровавого жнеца', description: 'Редкий декоративный плащ для ветеранов Спорных земель.', category: 'Cosmetic', cost: 160, currency: 'BloodTokens', owned: false },
    { id: 'preview-sword', name: 'Клинок военного интенданта', description: 'Концепт снаряжения World PvP, параметры определяет сервер.', category: 'Equipment', cost: 1500, currency: 'FieldHonor', owned: false },
    { id: 'preview-frame', name: 'Рамка «Непокорённый»', description: 'Украшение профиля героя, пережившего охоту за головой.', category: 'Cosmetic', cost: 80, currency: 'BloodTokens', owned: false },
    { id: 'preview-trophy', name: 'Знак павшего соперника', description: 'Коллекционный трофей за победы в открытом мире.', category: 'Collectible', cost: 250, currency: 'FieldHonor', owned: true },
  ],
  bounties: [
    { id: 'preview-bounty-a', targetCharacterId: 'preview-human-a', targetName: 'Арториус', targetFaction: 'HUMAN', killStreak: 35, lastSeenLocationName: 'Мёртвые Пределы', honorReward: 350, tokenReward: 25 },
    { id: 'preview-bounty-b', targetCharacterId: 'preview-human-b', targetName: 'Лиандра', targetFaction: 'HUMAN', killStreak: 21, lastSeenLocationName: null, honorReward: 200, tokenReward: 10 },
    { id: 'preview-bounty-c', targetCharacterId: 'preview-undead-a', targetName: 'Мортарион', targetFaction: 'UNDEAD', killStreak: 50, lastSeenLocationName: 'Мёртвые Пределы', honorReward: 500, tokenReward: 50 },
  ],
  titles: [
    {
      id: 'preview-hunter',
      name: 'Охотник за головами',
      description: 'Регистратор отмечает героя, который добыл славу в полевых схватках.',
      requirement: 'Пример: победи разыскиваемого игрока',
      progress: 1, target: 1, unlocked: true, equipped: true,
    },
    {
      id: 'preview-executioner',
      name: 'Палач',
      description: 'Герой, чьё имя шепчут у дорог и заброшенных крепостей.',
      requirement: 'Пример: 10 убийств подряд без смерти',
      progress: 6, target: 10, unlocked: false, equipped: false,
    },
    {
      id: 'preview-blood-reaper',
      name: 'Кровавый жнец',
      description: 'Титул за выдающуюся серию побед против вражеской фракции.',
      requirement: 'Пример: 35 убийств подряд без смерти',
      progress: 18, target: 35, unlocked: false, equipped: false,
    },
  ],
}
