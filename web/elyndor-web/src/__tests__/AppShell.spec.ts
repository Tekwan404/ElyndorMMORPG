import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import AppShell from '@/app/AppShell.vue'
import { apiClient } from '@/api/apiClient'
import { useGameSessionStore } from '@/stores/gameSession'
import { useCombatSessionStore } from '@/stores/combatSession'
import { usePartyStore } from '@/game/party/partyStore'
import { useDungeonStore } from '@/game/party/dungeonStore'

vi.mock('@/telegram/telegramWebApp', () => ({
  getTelegramInitData: vi.fn<() => string | null>(() => null),
  initializeTelegramWebApp: vi.fn<() => void>(),
}))

describe('AppShell', () => {
  it('shows combat above the menu when a shared encounter becomes active', async () => {
    setActivePinia(createPinia())
    const session = useGameSessionStore()
    vi.spyOn(session, 'start').mockResolvedValue(undefined)
    vi.spyOn(apiClient, 'request').mockResolvedValue([])
    session.state = 'world'
    session.snapshot = worldSnapshot()
    session.snapshot.world!.currentLocation.id = 'ANCIENT_MINE'
    const characterId = session.snapshot.character!.id
    const party = usePartyStore()
    party.snapshot = { partyId: 'party', leaderCharacterId: characterId, members: [], version: 1 }
    vi.spyOn(party, 'refresh').mockResolvedValue(undefined)
    const dungeon = useDungeonStore()
    dungeon.previews = [{ id: 'ANCIENT_MINE' } as (typeof dungeon.previews)[number]]
    dungeon.current = {
      runId: 'run', dungeonId: 'ANCIENT_MINE', state: 'Active', currentEncounterIndex: 0,
      members: [{ characterId, state: 'Active' }], encounters: [{ encounterIndex: 0, state: 'Pending' }],
    } as NonNullable<typeof dungeon.current>
    vi.spyOn(dungeon, 'refresh').mockResolvedValue(undefined)
    const combat = useCombatSessionStore()
    vi.spyOn(combat, 'connect').mockResolvedValue(undefined)
    vi.spyOn(combat, 'resume').mockResolvedValue(true)
    vi.spyOn(combat, 'startDungeonEncounter').mockImplementation(async () => {
      combat.$patch({ snapshot: { status: 'Active' } as NonNullable<typeof combat.snapshot> })
      return true
    })
    const wrapper = mount(AppShell, { global: { stubs: { CombatView: { template: '<div data-global-combat />' } } } })
    await flushPromises()
    await wrapper.get('[data-start-dungeon]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-global-combat]').exists()).toBe(true)
    expect(wrapper.find('.navigation').exists()).toBe(false)
    expect(wrapper.find('.hud').exists()).toBe(false)
    wrapper.unmount()
  })

  beforeEach(() => {
    setActivePinia(createPinia())
    vi.restoreAllMocks()
  })

  it('keeps World, Location and Hero as separate canonical navigation destinations', async () => {
    vi.spyOn(apiClient, 'request').mockResolvedValue([
      {
        id: 'STARTER_TOWN',
        displayName: 'Starter Town',
        dangerLevel: 'SAFE',
        recommendedLevel: 1,
      minimumLevel: 1,
      maximumLevel: 60,
      requiredContractId: null,
      artId: null,
      description: 'Test location',
      },
      {
        id: 'WHISPERING_FOREST',
        displayName: 'Whispering Forest',
        dangerLevel: 'ADVENTURE',
        recommendedLevel: 1,
      minimumLevel: 1,
      maximumLevel: 60,
      requiredContractId: null,
      artId: null,
      description: 'Test location',
      },
    ])
    const store = useGameSessionStore()
    vi.spyOn(store, 'start').mockResolvedValue(undefined)
    store.state = 'world'
    store.snapshot = worldSnapshot()
    const wrapper = mount(AppShell)
    expect(wrapper.get('[role="progressbar"][aria-label="Здоровье"]')).toBeTruthy()
    expect(wrapper.get('[role="progressbar"][aria-label="Фокус"]')).toBeTruthy()
    expect(wrapper.get('main').text()).toContain('Стартовый город')
    expect(wrapper.findAll('.navigation__item')).toHaveLength(5)
    expect(wrapper.get('[data-nav="location"]').attributes('aria-current')).toBe('page')
    expect(wrapper.find('[data-nav="inventory"]').exists()).toBe(false)
    expect(wrapper.get('[data-nav="location"]').attributes('disabled')).toBeUndefined()
    expect(wrapper.get('[data-hud-location]').text()).toContain('Стартовый город')
    expect(wrapper.find('.game-shell__header').exists()).toBe(false)
    expect(wrapper.get('.hud').text()).toContain('ELYNDOR')

    await wrapper.get('[data-nav="world"]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-nav="world"]').attributes('aria-current')).toBe('page')
    expect(wrapper.get('main').text()).toContain('Карта мира')

    await wrapper.get('[data-nav="location"]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-nav="location"]').attributes('aria-current')).toBe('page')
    expect(wrapper.get('main').text()).toContain('Стартовый город')
    expect(wrapper.find('[data-open-world-map]').exists()).toBe(false)

    await wrapper.get('[data-nav="hero"]').trigger('click')
    expect(wrapper.get('main').text()).toContain('Развитие героя')
    expect(wrapper.get('main').text()).toContain('Надетое снаряжение')
  })

  it('shows the authoritative quest journal on the quest tab', async () => {
    vi.spyOn(apiClient, 'request').mockImplementation(async (path) => {
      if (path === '/api/v1/quests/') {
        return {
          quests: [
            {
              id: 'CONTRACT_BROODMOTHER_GATE',
              displayName: 'Контракт: Прародительница',
              description: 'Уничтожьте Паучью Прародительницу.',
              type: 'CONTRACT',
              requiredLevel: 14,
              offerLocationId: 'BROODMOTHER_LAIR',
              status: 'ACTIVE',
              objectives: [
                {
                  id: 'KILL_BROODMOTHER',
                  type: 'KillMonster',
                  targetId: 'SPIDER_BROODMOTHER_L14',
                  currentCount: 0,
                  requiredCount: 1,
                  completed: false,
                  consumeOnClaim: false,
                },
              ],
              rewardXp: 2000,
              rewardGold: 150,
              rewardItems: [],
              prerequisiteQuestIds: ['QUEST_13_BROODMOTHER_TRACE'],
              unlockLocationId: 'BLIGHTED_GROVE',
            },
          ],
        } as never
      }
      return [] as never
    })
    const store = useGameSessionStore()
    vi.spyOn(store, 'start').mockResolvedValue(undefined)
    store.state = 'world'
    store.snapshot = worldSnapshot()

    const wrapper = mount(AppShell)
    await wrapper.get('[data-nav="quests"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-nav="quests"]').attributes('aria-current')).toBe('page')
    expect(wrapper.get('[data-quest-view]').text()).toContain('Контракт: Прародительница')
    expect(wrapper.get('[data-quest-id="CONTRACT_BROODMOTHER_GATE"]').text())
      .toContain('Паучья Прародительница')
    expect(wrapper.get('[data-quest-id="CONTRACT_BROODMOTHER_GATE"]').text())
      .toContain('2000 опыта')
  })

  it('opens the map when an empty journal sends the player back into the world', async () => {
    vi.spyOn(apiClient, 'request').mockResolvedValue([])
    const store = useGameSessionStore()
    vi.spyOn(store, 'start').mockResolvedValue(undefined)
    vi.spyOn(store, 'refreshQuestJournal').mockResolvedValue({ quests: [] })
    store.state = 'world'
    store.snapshot = worldSnapshot()
    store.questJournal = { quests: [] }

    const wrapper = mount(AppShell)
    await wrapper.get('[data-nav="quests"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-quest-open-world]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-nav="world"]').attributes('aria-current')).toBe('page')
    expect(wrapper.get('main').text()).toContain('Карта мира')
  })

  it('opens the map from the party dungeon entry', async () => {
    vi.spyOn(apiClient, 'request').mockResolvedValue([])
    const store = useGameSessionStore()
    vi.spyOn(store, 'start').mockResolvedValue(undefined)
    store.state = 'world'
    store.snapshot = worldSnapshot()
    const party = usePartyStore()
    vi.spyOn(party, 'refresh').mockResolvedValue(undefined)

    const wrapper = mount(AppShell)
    await wrapper.get('[data-nav="menu"]').trigger('click')
    await wrapper.get('.menu-tile--violet').trigger('click')
    await flushPromises()
    await wrapper.get('[data-party-open-dungeons]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-nav="world"]').attributes('aria-current')).toBe('page')
    expect(wrapper.get('main').text()).toContain('Карта мира')
  })

  it('resets the content scroll when switching to another main screen', async () => {
    vi.spyOn(apiClient, 'request').mockResolvedValue([])
    const store = useGameSessionStore()
    vi.spyOn(store, 'start').mockResolvedValue(undefined)
    store.state = 'world'
    store.snapshot = worldSnapshot()

    const wrapper = mount(AppShell)
    const content = wrapper.get('.content').element
    content.scrollTop = 180

    await wrapper.get('[data-nav="world"]').trigger('click')

    expect(content.scrollTop).toBe(0)
  })

  it('explains a failed connection and offers an explicit retry', async () => {
    const store = useGameSessionStore()
    vi.spyOn(store, 'start').mockResolvedValue(undefined)
    store.state = 'offline'
    store.errorCode = 'network_unavailable'
    const wrapper = mount(AppShell)
    expect(wrapper.get('[role="alert"]').text()).toContain('Не удалось связаться с сервером')
    await wrapper.get('[data-retry-session]').trigger('click')
    expect(store.start).toHaveBeenCalledTimes(2)
  })
})

function worldSnapshot() {
  return {
    accountId: crypto.randomUUID(),
    character: {
      id: crypto.randomUUID(),
      name: 'Arthas',
      raceId: 'HUMAN' as const,
      genderId: 'MALE' as const,
      classId: 'ARCHER' as const,
      level: 1,
      experience: 0,
      xpToNextLevel: 100,
      gold: 0,
      inventory: {
        items: [],
        equipped: {
          weapon: null,
          head: null,
          chest: null,
          legs: null,
          boots: null,
          accessory: null,
        },
      },
      primaryAttribute: 'AGILITY' as const,
      classProfileVersion: '0.2.0',
      knownAbilityIds: [],
      knownAbilities: [],
      stats: {
        strength: 5,
        agility: 9,
        intellect: 5,
        stamina: 7,
        maxHp: 120,
        attackPower: 19,
        spellPower: 10,
        criticalChance: 7.25,
        criticalDamage: 100,
        accuracy: 95,
        armorPenetration: 0,
        magicPenetration: 0,
        attackSpeed: 1,
        armor: 19,
        magicResistance: 12,
        dodge: 1.8,
      },
      statBreakdown: {} as never,
      vitals: {
        currentHp: 120,
        maxHp: 120,
        resourceType: 'FOCUS' as const,
        currentResource: 84,
        maxResource: 100,
        checkpointedAtUtc: '2026-08-30T00:00:00Z',
      },
    },
    world: {
      currentLocation: {
        id: 'STARTER_TOWN',
        displayName: 'Starter Town',
        dangerLevel: 'SAFE' as const,
        recommendedLevel: 1,
      minimumLevel: 1,
      maximumLevel: 60,
      requiredContractId: null,
      artId: null,
      description: 'Test location',
      },
      version: 1,
      outgoingTransitions: [],
      contracts: [],
    },
    contentVersion: '0.1.0',
    balanceVersion: '0.1.0',
    serverTimeUtc: '2026-08-30T00:00:00Z',
  }
}
