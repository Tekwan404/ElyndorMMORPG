import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import type { BootstrapSnapshot } from '@/api/contracts'
import WorldViewLegacy from '@/game/world/views/WorldViewLegacy.vue'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'

describe('WorldViewLegacy action feedback', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.spyOn(useGameSessionStore(), 'refreshQuestJournal').mockResolvedValue(null)
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('disables exploration without attributing another mutation to exploration loading', () => {
    prepareWorld()
    useGameSessionStore().mutationPending = true
    const wrapper = mount(WorldViewLegacy)
    expect(wrapper.get('[data-explore]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-explore]').attributes('aria-busy')).toBeUndefined()
    wrapper.unmount()
  })

  it('announces combat startup as exploration loading', () => {
    prepareWorld()
    vi.spyOn(useCombatSessionStore(), 'lifecyclePending', 'get').mockReturnValue(true)
    const wrapper = mount(WorldViewLegacy)
    expect(wrapper.get('[data-explore]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-explore]').attributes('aria-busy')).toBe('true')
    wrapper.unmount()
  })

  it('renders a readable inline alert when combat startup fails', async () => {
    prepareWorld()
    useCombatSessionStore().errorCode = 'combat_connection_failed'
    const wrapper = mount(WorldViewLegacy)
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('Не удалось')
    expect(wrapper.get('[data-explore]').attributes('disabled')).toBeUndefined()
    wrapper.unmount()
  })

  it.each(['MANA', 'FOCUS'] as const)(
    'refreshes incomplete %s outside combat',
    async (resourceType) => {
      vi.useFakeTimers()
      prepareWorld('STARTER_TOWN', {
        resourceType,
        currentResource: 10,
        maxResource: 100,
      })
      const refreshSnapshot = vi.spyOn(useGameSessionStore(), 'refreshSnapshot').mockResolvedValue()

      const wrapper = mount(WorldViewLegacy)
      await vi.advanceTimersByTimeAsync(1_000)

      expect(refreshSnapshot).toHaveBeenCalled()
      wrapper.unmount()
    },
  )

  it('blocks training during another mutation without showing a training spinner', () => {
    prepareWorld('STARTER_TOWN')
    useGameSessionStore().mutationPending = true
    const wrapper = mount(WorldViewLegacy)
    expect(wrapper.get('[data-start-training]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-start-training]').attributes('aria-busy')).toBeUndefined()
    wrapper.unmount()
  })

  it('disables joining a party fight during another combat operation without a loading spinner', () => {
    prepareWorld()
    const combat = useCombatSessionStore()
    vi.spyOn(combat, 'isAwaitingAttachment', 'get').mockReturnValue(true)
    vi.spyOn(combat, 'pending', 'get').mockReturnValue(true)
    const wrapper = mount(WorldViewLegacy)
    expect(wrapper.get('[data-attach-party-combat]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-attach-party-combat]').attributes('aria-busy')).toBeUndefined()
    wrapper.unmount()
  })
})

function prepareWorld(
  locationId = 'WHISPERING_FOREST',
  vitals: Partial<{
    currentHp: number
    maxHp: number
    resourceType: string
    currentResource: number
    maxResource: number
  }> = {},
) {
  useGameSessionStore().snapshot = {
    character: {
      id: 'character-1',
      vitals: {
        currentHp: 100,
        maxHp: 100,
        resourceType: 'RAGE',
        currentResource: 0,
        maxResource: 100,
        checkpointedAtUtc: '2026-10-06T00:00:00Z',
        ...vitals,
      },
    },
    world: {
      currentLocation: {
        id: locationId, displayName: 'Локация', dangerLevel: locationId === 'STARTER_TOWN' ? 'SAFE' : 'ADVENTURE',
        minimumLevel: 1, maximumLevel: 5, allowAfk: true,
      },
      contracts: [],
    },
  } as unknown as BootstrapSnapshot
}
