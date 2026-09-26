import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { apiClient } from '@/api/apiClient'
import type { BootstrapSnapshot } from '@/api/contracts'
import LocationOverview from '@/game/world/components/LocationOverview.vue'
import { useGameSessionStore } from '@/stores/gameSession'

describe('LocationOverview', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.restoreAllMocks()
  })

  it('keeps residents and loot compact until the player expands them', async () => {
    vi.spyOn(apiClient, 'request').mockResolvedValue([{
      id: 'WHISPERING_FOREST',
      displayName: 'Шепчущий лес',
      description: 'Лесная область.',
      dangerLevel: 'ADVENTURE',
      recommendedLevel: 1,
      minimumLevel: 1,
      maximumLevel: 5,
      requiredContractId: null,
      artId: null,
      allowAfk: true,
      residents: [{
        monsterId: 'FOREST_WOLF_L1',
        displayName: 'Лесной волк',
        level: 1,
        rank: 'Normal',
        description: 'Хищник.',
        artId: null,
        xpReward: 10,
        goldRewardMin: 1,
        goldRewardMax: 2,
      }],
      loot: [{
        itemId: 'WOLF_PELT',
        name: 'Волчья шкура',
        type: 'Material',
        rarity: 'Common',
        requiredLevel: 1,
        description: 'Материал.',
        iconId: null,
      }],
    }])
    const session = useGameSessionStore()
    session.snapshot = {
      accountId: 'account-1',
      character: { id: 'character-1' },
      world: {
        currentLocation: {
          id: 'WHISPERING_FOREST',
          displayName: 'Шепчущий лес',
          description: 'Лесная область.',
          dangerLevel: 'ADVENTURE',
          recommendedLevel: 1,
          minimumLevel: 1,
          maximumLevel: 5,
          requiredContractId: null,
          artId: null,
          allowAfk: true,
        },
        version: 1,
        outgoingTransitions: [],
        contracts: [],
      },
      contentVersion: 'location-overview-disclosure-test',
      balanceVersion: 'test',
      serverTimeUtc: '2026-09-26T00:00:00Z',
    } as unknown as BootstrapSnapshot

    const wrapper = mount(LocationOverview)
    await flushPromises()

    const residents = wrapper.get<HTMLDetailsElement>('[data-location-residents]')
    const loot = wrapper.get<HTMLDetailsElement>('[data-location-loot]')
    expect(residents.element.open).toBe(false)
    expect(loot.element.open).toBe(false)
    expect(wrapper.get('[data-location-disclosure="residents"]').text()).toContain('1')
    expect(wrapper.get('[data-location-disclosure="loot"]').text()).toContain('1')

    await wrapper.get('[data-location-disclosure="residents"]').trigger('click')
    expect(residents.element.open).toBe(true)
    expect(loot.element.open).toBe(false)
  })
})
