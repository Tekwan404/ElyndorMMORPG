import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import WorldView from '@/game/world/views/WorldView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

describe('WorldView', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('renders only server-provided transitions and sends their target id', async () => {
    const store = useGameSessionStore()
    store.snapshot = snapshot()
    const travel = vi.spyOn(store, 'travel').mockResolvedValue()
    const wrapper = mount(WorldView)

    expect(wrapper.text()).toContain('Starter Town')
    expect(wrapper.text()).toContain('Whispering Forest')
    expect(wrapper.findAll('[data-travel]')).toHaveLength(1)
    expect(wrapper.find('input').exists()).toBe(false)

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
})

function snapshot() {
  return {
    accountId: crypto.randomUUID(),
    character: {
      id: crypto.randomUUID(),
      name: 'Arthas',
      raceId: 'HUMAN' as const,
      genderId: 'MALE' as const,
      classId: 'ARCHER' as const,
      level: 1,
      primaryAttribute: 'AGILITY' as const,
      classProfileVersion: '0.2.0',
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
      vitals: {
        currentHp: 120,
        maxHp: 120,
        resourceType: 'FOCUS' as const,
        currentResource: 100,
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
      },
      version: 1,
      outgoingTransitions: [
        {
          id: 'WHISPERING_FOREST',
          displayName: 'Whispering Forest',
          dangerLevel: 'ADVENTURE' as const,
          recommendedLevel: 1,
        },
      ],
    },
    contentVersion: '0.1.0',
    balanceVersion: '0.1.0',
    serverTimeUtc: '2026-08-30T00:00:00Z',
  }
}
