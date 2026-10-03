import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'

import { apiClient } from '@/api/apiClient'
import type { BootstrapSnapshot } from '@/api/contracts'
import LocationOverview from '@/game/world/components/LocationOverview.vue'
import type { DetailedWorldLocation } from '@/game/world/locationDetails'
import { useGameSessionStore } from '@/stores/gameSession'
import { useCombatSessionStore } from '@/stores/combatSession'

describe('LocationOverview', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.restoreAllMocks()
  })

  it('announces details loading and lets the player retry after a catalog failure', async () => {
    const session = prepareLocation('location-feedback-retry')
    let rejectCatalog!: (reason: Error) => void
    const request = vi.spyOn(apiClient, 'request')
      .mockImplementationOnce(() => new Promise((_resolve, reject) => { rejectCatalog = reject }))
      .mockResolvedValueOnce([{ ...session.snapshot!.world!.currentLocation, residents: [], loot: [] }])
    const wrapper = mount(LocationOverview)

    expect(wrapper.get('[role="status"]').text()).toContain('Загружаем сведения')
    rejectCatalog(new Error('offline'))
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('Подробности области недоступны')
    expect(wrapper.get('[data-explore]').attributes('disabled')).toBeUndefined()

    await wrapper.get('[data-retry-location-details]').trigger('click')
    await flushPromises()
    expect(request).toHaveBeenCalledTimes(2)
    expect(wrapper.find('[role="alert"]').exists()).toBe(false)
    expect(wrapper.get('[role="status"]').text()).toContain('Здесь нет открытого списка')
    wrapper.unmount()
  })

  it('disables exploration without a spinner for an unrelated pending mutation', async () => {
    prepareLocation('location-feedback-other-mutation')
    vi.spyOn(apiClient, 'request').mockResolvedValue([])
    const session = useGameSessionStore()
    session.mutationPending = true
    const wrapper = mount(LocationOverview)
    await flushPromises()

    expect(wrapper.get('[data-explore]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-explore]').attributes('aria-busy')).toBeUndefined()
    wrapper.unmount()
  })

  it('keeps the latest content details when an older catalog request finishes last', async () => {
    const session = prepareLocation('location-feedback-old-version')
    const current = session.snapshot!.world!.currentLocation
    let resolveOldCatalog!: (value: DetailedWorldLocation[]) => void
    vi.spyOn(apiClient, 'request')
      .mockImplementationOnce(() => new Promise(resolve => { resolveOldCatalog = resolve }))
      .mockResolvedValueOnce([{ ...current, displayName: 'Обновлённая область' }])
    const wrapper = mount(LocationOverview)
    session.snapshot!.contentVersion = 'location-feedback-new-version'
    await flushPromises()
    expect(wrapper.get('h1').text()).toBe('Обновлённая область')

    resolveOldCatalog([{ ...current, displayName: 'Устаревшая область' }])
    await flushPromises()
    expect(wrapper.get('h1').text()).toBe('Обновлённая область')
    wrapper.unmount()
  })

  it('shows exploration loading through encounter selection and combat startup', async () => {
    prepareLocation('location-feedback-explore')
    vi.spyOn(apiClient, 'request').mockResolvedValue([])
    const session = useGameSessionStore()
    const combat = useCombatSessionStore()
    let resolveEncounter!: (value: { encounterId: string; name: string }) => void
    const encounter = { encounterId: 'server-encounter', name: 'Волк' }
    vi.spyOn(session, 'explore').mockImplementation(async () => {
      session.mutationPending = true
      const result = await new Promise<typeof encounter>(resolve => { resolveEncounter = resolve })
      session.mutationPending = false
      return result as Awaited<ReturnType<typeof session.explore>>
    })
    vi.spyOn(session, 'isMutationPending').mockImplementation(key => key === 'world:explore' && session.mutationPending)
    const startingCombat = ref(false)
    vi.spyOn(combat, 'lifecyclePending', 'get').mockImplementation(() => startingCombat.value)
    let resolveCombat!: (value: boolean) => void
    vi.spyOn(combat, 'startCombat').mockImplementation(async () => {
      startingCombat.value = true
      const result = await new Promise<boolean>(resolve => { resolveCombat = resolve })
      startingCombat.value = false
      return result
    })
    const wrapper = mount(LocationOverview)
    await flushPromises()

    await wrapper.get('[data-explore]').trigger('click')
    expect(wrapper.get('[data-explore]').attributes('aria-busy')).toBe('true')
    resolveEncounter(encounter)
    await flushPromises()
    expect(wrapper.get('[data-explore]').attributes('aria-busy')).toBe('true')
    expect(wrapper.get('[data-afk-farming]').attributes('disabled')).toBeDefined()
    resolveCombat(false)
    await flushPromises()
    expect(wrapper.get('[data-explore]').attributes('aria-busy')).toBeUndefined()
    wrapper.unmount()
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

function prepareLocation(contentVersion: string) {
  const session = useGameSessionStore()
  session.snapshot = {
    character: { id: 'character-1' },
    world: {
      currentLocation: {
        id: 'WHISPERING_FOREST', displayName: 'Шепчущий лес', dangerLevel: 'ADVENTURE',
        minimumLevel: 1, maximumLevel: 5, allowAfk: true,
      },
      contracts: [],
    },
    contentVersion,
  } as unknown as BootstrapSnapshot
  return session
}
