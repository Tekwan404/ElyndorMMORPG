import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { BootstrapSnapshot, CombatActorSnapshot, CombatSnapshot, WorldEncounter } from '@/api/contracts'
import WorldView from '@/game/world/views/WorldView.vue'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'

const WOLF_ENCOUNTER: WorldEncounter = {
  encounterId: '01991ea5-74a0-7000-8000-000000000001',
  monsterId: 'WOLF',
  name: 'Волк',
  level: 3,
  rank: 'Normal',
  description: 'Дикий волк вышел на тропу и следит за каждым движением.',
  artId: 'wolf',
}

describe('WorldView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.spyOn(useGameSessionStore(), 'refreshQuestJournal').mockResolvedValue(null)
    vi.spyOn(useGameSessionStore(), 'refreshSnapshot').mockResolvedValue(undefined)
    document.body.innerHTML = ''
  })

  it('renders only the current location and leaves travel to the World map', () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot()
    const wrapper = mount(WorldView)

    expect(wrapper.text()).toContain('Стартовый город')
    expect(wrapper.find('[data-travel]').exists()).toBe(false)
    expect(wrapper.find('.location-routes').exists()).toBe(false)
    expect(wrapper.findAll('[data-town-service]')).toHaveLength(4)
    expect(wrapper.get('[data-town-service="training"]').text()).toContain('Манекен')
    expect(wrapper.get('[data-town-service="merchant"]').text()).toContain('Маркус')
    expect(wrapper.get('[data-town-service="guild"]').text()).toContain('ГИЛЬДИЯ АВАНТЮРИСТОВ')
  })

  it('keeps the primary explore action attached to the current-location artwork', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot('WHISPERING_FOREST')
    const combat = useCombatSessionStore()
    vi.spyOn(combat, 'connect').mockResolvedValue(undefined)
    vi.spyOn(combat, 'resume').mockResolvedValue(true)

    const wrapper = mount(WorldView)
    await flushPromises()

    expect(wrapper.find('.scene [data-explore]').exists()).toBe(true)
    expect(wrapper.find('.location-activities').exists()).toBe(false)
    expect(wrapper.find('[data-start-encounter]').exists()).toBe(false)
  })

  it('disables exploration while a world mutation is pending and shows the server error', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot('WHISPERING_FOREST')
    store.mutationPending = true
    store.errorCode = 'world_encounter_unavailable'
    const combat = useCombatSessionStore()
    vi.spyOn(combat, 'connect').mockResolvedValue(undefined)
    vi.spyOn(combat, 'resume').mockResolvedValue(true)

    const wrapper = mount(WorldView)
    await flushPromises()

    expect(wrapper.get('[data-explore]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[role="alert"]').text()).toContain('В этой области сейчас не удалось найти противника.')
  })

  it('surfaces available story from the current location instead of the journal', async () => {
    const session = useGameSessionStore()
    session.snapshot = snapshot('WHISPERING_FOREST')
    session.questJournal = {
      quests: [worldQuest('QUEST_STORY', 'STORY', 'AVAILABLE', 'WHISPERING_FOREST')],
    }
    const acceptQuest = vi.spyOn(session, 'acceptQuest').mockResolvedValue(undefined)
    const combat = useCombatSessionStore()
    vi.spyOn(combat, 'connect').mockResolvedValue(undefined)
    vi.spyOn(combat, 'resume').mockResolvedValue(true)

    const wrapper = mount(WorldView)
    await flushPromises()

    expect(wrapper.get('[data-world-quest-id="QUEST_STORY"]').text()).toContain('СЮЖЕТ')
    await wrapper.get('[data-accept-world-quest]').trigger('click')
    expect(acceptQuest).toHaveBeenCalledWith('QUEST_STORY')
  })

  it('opens the Adventurer Guild registrar and accepts official contracts there', async () => {
    const session = useGameSessionStore()
    session.snapshot = snapshot()
    session.snapshot.character!.level = 14
    session.questJournal = {
      quests: [worldQuest('CONTRACT_BROODMOTHER_GATE', 'CONTRACT', 'AVAILABLE', 'STARTER_TOWN')],
    }
    const acceptQuest = vi.spyOn(session, 'acceptQuest').mockResolvedValue(undefined)

    const wrapper = mount(WorldView, { attachTo: document.body })
    await flushPromises()
    await wrapper.get('[data-open-adventurer-guild]').trigger('click')
    await flushPromises()

    const board = document.body.querySelector('[data-adventurer-guild-board]')
    expect(board?.textContent).toContain('КОНТРАКТ №BF-014')
    expect(board?.textContent).toContain('Заказчик')
    const acceptButton = document.body.querySelector('[data-guild-accept-contract]') as HTMLButtonElement
    acceptButton.click()
    await flushPromises()
    expect(acceptQuest).toHaveBeenCalledWith('CONTRACT_BROODMOTHER_GATE')
    wrapper.unmount()
  })

  it('starts the server-selected encounter immediately after Explore with no confirmation step', async () => {
    const session = useGameSessionStore()
    session.snapshot = snapshot('WHISPERING_FOREST')
    const explore = vi.spyOn(session, 'explore').mockResolvedValue(WOLF_ENCOUNTER)
    const combat = useCombatSessionStore()
    vi.spyOn(combat, 'connect').mockResolvedValue(undefined)
    vi.spyOn(combat, 'resume').mockResolvedValue(true)
    const startCombat = vi.spyOn(combat, 'startCombat').mockImplementation(async () => {
      combat.snapshot = combatSnapshot()
      return true
    })

    const wrapper = mount(WorldView)
    await flushPromises()

    await wrapper.get('[data-explore]').trigger('click')
    await flushPromises()

    expect(explore).toHaveBeenCalledTimes(1)
    expect(startCombat).toHaveBeenCalledWith(WOLF_ENCOUNTER)
    expect(wrapper.find('[data-world-encounter]').exists()).toBe(false)
    expect(wrapper.find('[data-start-encounter]').exists()).toBe(false)
    expect(combat.isActive).toBe(true)
    combat.snapshot = { ...combat.snapshot!, status: 'Victory' }
    await flushPromises()
    expect(wrapper.get('[data-explore-after-victory]').attributes('disabled')).toBeUndefined()
    await wrapper.get('[data-explore-after-victory]').trigger('click')
    await flushPromises()
    expect(explore).toHaveBeenCalledTimes(2)
  })

  it('keeps a rostered remote participant in the world until they reach the combat location', async () => {
    const session = useGameSessionStore()
    session.snapshot = snapshot('STARTER_TOWN')
    const combat = useCombatSessionStore()
    const pendingCombat = combatSnapshot()
    pendingCombat.participantRoster = [{
      accountId: session.snapshot.accountId,
      characterId: session.snapshot.character!.id,
      actorId: pendingCombat.player.actorId,
      status: 'Rostered',
      rosteredAtUtc: '2026-09-01T12:00:00Z',
      joinedAtUtc: null,
      fledAtUtc: null,
      diedAtUtc: null,
    }]
    combat.snapshot = pendingCombat
    vi.spyOn(combat, 'connect').mockResolvedValue(undefined)
    vi.spyOn(combat, 'resume').mockResolvedValue(true)
    const attachCombat = vi.spyOn(combat, 'attachCombat').mockResolvedValue(true)

    const wrapper = mount(WorldView)
    await flushPromises()

    expect(combat.isActive).toBe(false)
    expect(wrapper.find('[data-party-combat-pending]').exists()).toBe(true)
    await wrapper.get('[data-attach-party-combat]').trigger('click')

    expect(attachCombat).toHaveBeenCalledWith(pendingCombat.sessionId)
  })
})

function snapshot(
  locationId: 'STARTER_TOWN' | 'WHISPERING_FOREST' = 'STARTER_TOWN',
): BootstrapSnapshot {
  const inForest = locationId === 'WHISPERING_FOREST'
  return {
    accountId: crypto.randomUUID(),
    character: {
      id: crypto.randomUUID(),
      name: 'Arthas',
      raceId: 'HUMAN',
      genderId: 'MALE',
      classId: 'WARRIOR',
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
      primaryAttribute: 'STRENGTH',
      classProfileVersion: '0.2.0',
      knownAbilityIds: ['STRIKE'],
      knownAbilities: [
        {
          id: 'STRIKE',
          displayName: 'Strike',
          description: 'A basic melee strike.',
          iconId: null,
          resourceCost: 0,
          cooldownSeconds: 0,
          type: 'Instant',
          targetType: 'SingleEnemy',
          sourceTalentId: null,
          sourceTalentName: null,
        },
      ],
      stats: {
        strength: 9,
        agility: 5,
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
        resourceType: 'RAGE',
        currentResource: 0,
        maxResource: 100,
        checkpointedAtUtc: '2026-08-30T00:00:00Z',
      },
    },
    world: {
      currentLocation: inForest
        ? {
            id: 'WHISPERING_FOREST',
            displayName: 'Whispering Forest',
            dangerLevel: 'ADVENTURE',
            recommendedLevel: 1,
            minimumLevel: 1,
            maximumLevel: 5,
            requiredContractId: null,
            artId: null,
            description: 'Test location',
          }
        : {
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
      version: 1,
      outgoingTransitions: inForest
        ? [
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
          ]
        : [
            {
              id: 'WHISPERING_FOREST',
              displayName: 'Whispering Forest',
              dangerLevel: 'ADVENTURE',
              recommendedLevel: 1,
              minimumLevel: 1,
              maximumLevel: 5,
              requiredContractId: null,
              artId: null,
              description: 'Test location',
            },
          ],
      contracts: [],
    },
    contentVersion: '0.1.0',
    balanceVersion: '0.1.0',
    serverTimeUtc: '2026-08-30T00:00:00Z',
  }
}

function worldQuest(
  id: string,
  type: 'STORY' | 'SIDE' | 'CONTRACT',
  status: 'LOCKED' | 'AVAILABLE' | 'ACTIVE' | 'READY_TO_CLAIM' | 'COMPLETED',
  offerLocationId: string,
) {
  return {
    id,
    displayName: id === 'CONTRACT_BROODMOTHER_GATE' ? 'Контракт: Прародительница' : 'Следы в лесу',
    description: 'Проверьте происходящее в регионе.',
    type,
    requiredLevel: 1,
    offerLocationId,
    status,
    objectives: [{
      id: 'KILL',
      type: 'KillMonster',
      targetId: 'FOREST_WOLF_L1',
      currentCount: 0,
      requiredCount: 1,
      completed: false,
      consumeOnClaim: false,
    }],
    rewardXp: 100,
    rewardGold: 20,
    rewardItems: [],
    prerequisiteQuestIds: [],
    unlockLocationId: type === 'CONTRACT' ? 'BLIGHTED_GROVE' : null,
    issuerName: type === 'CONTRACT' ? 'Гильдия авантюристов' : 'Городской дозор',
    issuerRole: type === 'CONTRACT' ? 'Регистратор' : 'Разведка',
    regionName: type === 'CONTRACT' ? 'Логово Прародительницы' : 'Шепчущий лес',
    contractNumber: type === 'CONTRACT' ? 'BF-014' : null,
    threatLevel: type === 'CONTRACT' ? 'Высокая' : null,
  }
}

function combatSnapshot(): CombatSnapshot {
  return {
    sessionId: crypto.randomUUID(),
    sequence: 1,
    status: 'Active',
    serverTimeUtc: '2026-09-01T12:00:00Z',
    contentVersion: '0.9.3',
    balanceVersion: '0.9.1',
    player: combatActor('Player', 'WARRIOR', 'Arthas', 120, 120, 0, 100),
    enemy: combatActor('Monster', 'WOLF', 'Волк', 100, 100, 0, 0),
  }
}

function combatActor(
  kind: 'Player' | 'Monster',
  definitionId: string,
  name: string,
  hp: number,
  maxHp: number,
  resource: number,
  maxResource: number,
): CombatActorSnapshot {
  return {
    actorId: crypto.randomUUID(),
    kind,
    definitionId,
    name,
    hp,
    maxHp,
    resourceType: kind === 'Player' ? 'RAGE' : 'NONE',
    resource,
    maxResource,
    autoAttackEnabled: false,
    cooldowns: {},
    knownAbilityIds: kind === 'Player' ? ['STRIKE'] : [],
    abilities:
      kind === 'Player'
        ? [
            {
              id: 'STRIKE',
              displayName: 'Strike',
              description: 'A basic melee strike.',
              iconId: null,
              resourceCost: 0,
              cooldownSeconds: 0,
            },
          ]
        : [],
    effects: [],
  }
}
