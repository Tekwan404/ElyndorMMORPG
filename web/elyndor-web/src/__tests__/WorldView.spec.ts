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
  beforeEach(() => setActivePinia(createPinia()))

  it('renders only the current location and leaves travel to the World map', () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot()
    const wrapper = mount(WorldView)

    expect(wrapper.text()).toContain('Стартовый город')
    expect(wrapper.find('[data-travel]').exists()).toBe(false)
    expect(wrapper.find('.location-routes').exists()).toBe(false)
    expect(wrapper.findAll('[data-town-service]')).toHaveLength(3)
    expect(wrapper.get('[data-town-service="training"]').text()).toContain('Манекен')
    expect(wrapper.get('[data-town-service="merchant"]').text()).toContain('Маркус')
  })

  it('renders explore as a dedicated current-location activity outside the artwork', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot('WHISPERING_FOREST')
    const combat = useCombatSessionStore()
    vi.spyOn(combat, 'connect').mockResolvedValue(undefined)
    vi.spyOn(combat, 'resume').mockResolvedValue(true)

    const wrapper = mount(WorldView)
    await flushPromises()

    expect(wrapper.find('.scene [data-explore]').exists()).toBe(false)
    expect(wrapper.find('.location-activities [data-explore]').exists()).toBe(true)
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
    expect(wrapper.get('[role="alert"]').text()).toContain('world_encounter_unavailable')
  })

  it('shows the Broodmother contract in the lair and accepts it explicitly', async () => {
    const session = useGameSessionStore()
    session.snapshot = snapshot('WHISPERING_FOREST')
    session.snapshot.character!.level = 14
    session.snapshot.world!.currentLocation = {
      id: 'BROODMOTHER_LAIR',
      displayName: 'Логово Прародительницы',
      dangerLevel: 'DANGEROUS',
      recommendedLevel: 14,
      minimumLevel: 14,
      maximumLevel: 14,
      requiredContractId: null,
      artId: null,
      description: 'Логово босса',
    }
    session.snapshot.world!.contracts = [
      {
        id: 'CONTRACT_BROODMOTHER_GATE',
        displayName: 'Контракт: Прародительница',
        description: 'Уничтожьте Паучью Прародительницу.',
        requiredLevel: 14,
        targetMonsterId: 'SPIDER_BROODMOTHER_L14',
        unlockLocationId: 'BLIGHTED_GROVE',
        status: 'AVAILABLE',
        offerLocationId: 'BROODMOTHER_LAIR',
        rewardXp: 2000,
        rewardGold: 150,
      },
    ]
    const acceptContract = vi.spyOn(session, 'acceptContract').mockResolvedValue(undefined)
    const combat = useCombatSessionStore()
    vi.spyOn(combat, 'connect').mockResolvedValue(undefined)
    vi.spyOn(combat, 'resume').mockResolvedValue(true)

    const wrapper = mount(WorldView)
    await flushPromises()

    expect(wrapper.get('[data-contract-id="CONTRACT_BROODMOTHER_GATE"]').text())
      .toContain('2000 опыта')
    expect(wrapper.get('[data-contract-id="CONTRACT_BROODMOTHER_GATE"]').text())
      .toContain('150 золота')
    await wrapper.get('[data-accept-contract]').trigger('click')
    await flushPromises()

    expect(acceptContract).toHaveBeenCalledWith('CONTRACT_BROODMOTHER_GATE')
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
    expect(wrapper.text()).toContain('Волк')
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
