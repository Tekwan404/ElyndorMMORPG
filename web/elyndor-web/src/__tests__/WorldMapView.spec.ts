import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { apiClient } from '@/api/apiClient'
import type { BootstrapSnapshot, WorldLocation } from '@/api/contracts'
import WorldMapView from '@/game/world/views/WorldMapView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

const LOCATIONS: WorldLocation[] = [
  {
    id: 'STARTER_TOWN',
    displayName: 'Starter Town',
    dangerLevel: 'SAFE',
    recommendedLevel: 1,
    minimumLevel: 1,
    maximumLevel: 60,
    requiredContractId: null,
    artId: null,
    description: 'Safe settlement',
  },
  {
    id: 'WHISPERING_FOREST',
    displayName: 'Whispering Forest',
    dangerLevel: 'ADVENTURE',
    recommendedLevel: 3,
    minimumLevel: 1,
    maximumLevel: 5,
    requiredContractId: null,
    artId: null,
    description: 'Early forest zone',
  },
  {
    id: 'DEEP_FOREST',
    displayName: 'Deep Forest',
    dangerLevel: 'DANGEROUS',
    recommendedLevel: 9,
    minimumLevel: 6,
    maximumLevel: 11,
    requiredContractId: null,
    artId: null,
    description: 'Dangerous forest zone',
  },
]

describe('WorldMapView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.restoreAllMocks()
  })

  it('loads the server world catalog and marks only authoritative transitions reachable', async () => {
    vi.spyOn(apiClient, 'request').mockResolvedValue(LOCATIONS)

    const session = useGameSessionStore()
    session.snapshot = snapshot()
    const wrapper = mount(WorldMapView)
    await flushPromises()

    expect(apiClient.request).toHaveBeenCalledWith('/api/v1/world/locations')
    expect(wrapper.findAll('[data-location-id]')).toHaveLength(3)
    expect(wrapper.get('[data-location-id="STARTER_TOWN"]').attributes('data-state')).toBe('current')
    expect(wrapper.get('[data-location-id="WHISPERING_FOREST"]').attributes('data-state')).toBe('reachable')
    expect(wrapper.get('[data-location-id="DEEP_FOREST"]').attributes('data-state')).toBe('locked')
  })

  it('keeps the destination brief below the map instead of hiding routes', async () => {
    vi.spyOn(apiClient, 'request').mockResolvedValue(LOCATIONS)

    const session = useGameSessionStore()
    session.snapshot = snapshot()
    const wrapper = mount(WorldMapView)
    await flushPromises()

    const selection = wrapper.get('[data-map-selection]')
    expect(selection.element.parentElement?.classList.contains('map-canvas')).toBe(false)
    expect(selection.text()).toContain('Осмотреть локацию')

    await wrapper.get('[data-location-id="WHISPERING_FOREST"]').trigger('click')
    expect(wrapper.get('[data-map-travel]').text()).toContain('Начать переход')
  })

  it('keeps map node order stable when the current location changes', async () => {
    vi.spyOn(apiClient, 'request').mockResolvedValue(LOCATIONS)

    const session = useGameSessionStore()
    session.snapshot = snapshot()
    const wrapper = mount(WorldMapView)
    await flushPromises()

    const order = () => wrapper.findAll('[data-location-id]')
      .map(node => node.attributes('data-location-id'))

    expect(order()).toEqual(['STARTER_TOWN', 'WHISPERING_FOREST', 'DEEP_FOREST'])

    session.snapshot.world = {
      currentLocation: LOCATIONS[1]!,
      version: 2,
      outgoingTransitions: [LOCATIONS[0]!, LOCATIONS[2]!],
      contracts: [],
    }
    await flushPromises()

    expect(order()).toEqual(['STARTER_TOWN', 'WHISPERING_FOREST', 'DEEP_FOREST'])
    expect(wrapper.get('[data-location-id="WHISPERING_FOREST"]').attributes('data-state')).toBe('current')
  })

  it('travels only to a server-provided outgoing transition', async () => {
    vi.spyOn(apiClient, 'request').mockResolvedValue(LOCATIONS)

    const session = useGameSessionStore()
    session.snapshot = snapshot()
    const travel = vi.spyOn(session, 'travel').mockResolvedValue(undefined)

    const wrapper = mount(WorldMapView)
    await flushPromises()

    await wrapper.get('[data-location-id="WHISPERING_FOREST"]').trigger('click')
    expect(wrapper.get('[data-map-travel]').attributes('disabled')).toBeUndefined()
    await wrapper.get('[data-map-travel]').trigger('click')
    expect(travel).toHaveBeenCalledWith('WHISPERING_FOREST')

    await wrapper.get('[data-location-id="DEEP_FOREST"]').trigger('click')
    expect(wrapper.get('[data-map-travel]').attributes('disabled')).toBeDefined()
    await wrapper.get('[data-map-travel]').trigger('click')
    expect(travel).toHaveBeenCalledTimes(1)
  })

  it('keeps the current location usable when the full catalog request fails', async () => {
    vi.spyOn(apiClient, 'request').mockRejectedValue(new TypeError('offline'))

    const session = useGameSessionStore()
    session.snapshot = snapshot()
    const wrapper = mount(WorldMapView)
    await flushPromises()

    expect(wrapper.text()).toContain('Карта загружена частично')
    expect(wrapper.findAll('[data-location-id]')).toHaveLength(2)
    expect(wrapper.get('[data-location-id="STARTER_TOWN"]')).toBeTruthy()
    expect(wrapper.get('[data-location-id="WHISPERING_FOREST"]')).toBeTruthy()
  })
})

function snapshot(): BootstrapSnapshot {
  return {
    accountId: crypto.randomUUID(),
    character: {
      id: crypto.randomUUID(),
      name: 'Arthas',
      raceId: 'HUMAN',
      genderId: 'MALE',
      classId: 'ARCHER',
      level: 1,
      experience: 0,
      xpToNextLevel: 100,
      gold: 0,
      primaryAttribute: 'AGILITY',
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
        resourceType: 'FOCUS',
        currentResource: 100,
        maxResource: 100,
        checkpointedAtUtc: '2026-09-06T05:00:00Z',
      },
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
    },
    world: {
      currentLocation: LOCATIONS[0]!,
      version: 1,
      outgoingTransitions: [LOCATIONS[1]!],
      contracts: [],
    },
    contentVersion: '0.9.4',
    balanceVersion: '0.9.1',
    serverTimeUtc: '2026-09-06T05:00:00Z',
  }
}
