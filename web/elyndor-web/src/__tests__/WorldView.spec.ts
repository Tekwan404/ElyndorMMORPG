import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { BootstrapSnapshot, CombatSnapshot } from '@/api/contracts'
import WorldView from '@/game/world/views/WorldView.vue'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'

describe('WorldView', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('renders only server-provided transitions and sends their target id', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot()
    const travel = vi.spyOn(store, 'travel').mockResolvedValue(undefined)
    const wrapper = mount(WorldView)
    expect(wrapper.text()).toContain('Starter Town')
    expect(wrapper.text()).toContain('Whispering Forest')
    expect(wrapper.findAll('[data-travel]')).toHaveLength(1)
    expect(wrapper.find('input').exists()).toBe(false)
    expect(wrapper.get('[data-travel="WHISPERING_FOREST"]').attributes('aria-label')).toContain(
      'Whispering Forest',
    )
    await wrapper.get('[data-travel="WHISPERING_FOREST"]').trigger('click')
    expect(travel).toHaveBeenCalledWith('WHISPERING_FOREST')
  })

  it('disables travel while a mutation is pending and shows server errors', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot()
    store.mutationPending = true
    store.errorCode = 'travel_conflict'
    const wrapper = mount(WorldView)
    expect(wrapper.get('[data-travel="WHISPERING_FOREST"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-travel="WHISPERING_FOREST"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.get('[role="alert"]').text()).toBe('travel_conflict')
  })

  it('explains when the current location has no outgoing path', () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot()
    store.snapshot.world!.outgoingTransitions = []
    const wrapper = mount(WorldView)
    expect(wrapper.get('[role="status"]').text()).toContain('Пути не найдены')
    expect(wrapper.find('[data-travel]').exists()).toBe(false)
  })

  it('discovers Wolf before starting the existing combat flow', async () => {
    const session = useGameSessionStore()
    session.snapshot = snapshot('WHISPERING_FOREST')
    const combat = useCombatSessionStore()
    vi.spyOn(combat, 'connect').mockResolvedValue(undefined)
    vi.spyOn(combat, 'resume').mockResolvedValue(true)
    const startCombat = vi.spyOn(combat, 'startCombat').mockImplementation(async () => {
      combat.snapshot = combatSnapshot()
      return true
    })
    const wrapper = mount(WorldView)
    await flushPromises()
    expect(wrapper.find('[data-world-encounter]').exists()).toBe(false)
    expect(startCombat).not.toHaveBeenCalled()
    await wrapper.get('[data-explore]').trigger('click')
    expect(wrapper.get('[data-world-encounter]').text()).toContain('Волк')
    expect(startCombat).not.toHaveBeenCalled()
    await wrapper.get('[data-start-encounter]').trigger('click')
    await flushPromises()
    expect(startCombat).toHaveBeenCalledWith('WOLF')
    expect(wrapper.text()).toContain('Forest Wolf')
    expect(wrapper.find('[data-world-encounter]').exists()).toBe(false)
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
      inventory: { items: [], equipped: { Weapon: null, Head: null, Chest: null } },
      primaryAttribute: 'STRENGTH',
      classProfileVersion: '0.2.0',
      knownAbilityIds: ['STRIKE'],
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
          }
        : {
            id: 'STARTER_TOWN',
            displayName: 'Starter Town',
            dangerLevel: 'SAFE',
            recommendedLevel: 1,
          },
      version: 1,
      outgoingTransitions: inForest
        ? [
            {
              id: 'STARTER_TOWN',
              displayName: 'Starter Town',
              dangerLevel: 'SAFE',
              recommendedLevel: 1,
            },
          ]
        : [
            {
              id: 'WHISPERING_FOREST',
              displayName: 'Whispering Forest',
              dangerLevel: 'ADVENTURE',
              recommendedLevel: 1,
            },
          ],
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
    player: combatActor('Player', 'WARRIOR', 'Arthas', 120, 120, 0, 100),
    enemy: combatActor('Monster', 'WOLF', 'Forest Wolf', 100, 100, 0, 0),
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
) {
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
    abilities: kind === 'Player' ? [{ id: 'STRIKE', resourceCost: 0, cooldownSeconds: 0 }] : [],
    effects: [],
  }
}
