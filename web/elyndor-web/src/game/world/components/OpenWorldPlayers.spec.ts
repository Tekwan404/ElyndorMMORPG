import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import OpenWorldPlayers from './OpenWorldPlayers.vue'

const mocks = vi.hoisted(() => ({
  request: vi.fn<() => Promise<unknown>>(),
  enter: vi.fn<(id: string, attack: boolean) => Promise<boolean>>(),
}))
vi.mock('@/api/apiClient', () => ({ apiClient: { request: mocks.request } }))
vi.mock('@/stores/gameSession', () => ({
  useGameSessionStore: () => ({
    snapshot: { character: { raceId: 'HUMAN' }, afkFarm: null },
    mutationPending: false,
  }),
}))
vi.mock('@/stores/combatSession', () => ({
  useCombatSessionStore: () => ({
    enterOpenWorldCombat: mocks.enter, isActive: false, lifecyclePending: false, errorCode: null,
  }),
}))

const player = (id: string, raceId: string, inCombat: boolean) => ({
  characterId: id, name: id, raceId, inCombat, classId: 'WARRIOR',
  genderId: 'MALE', skinId: null, level: 60,
})

describe('OpenWorldPlayers', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.clearAllMocks()
    Object.defineProperty(document, 'hidden', { configurable: true, value: false })
    mocks.request.mockResolvedValue({
      available: true, players: [player('Enemy', 'UNDEAD', true), player('Ally', 'HUMAN', true)],
    })
    mocks.enter.mockResolvedValue(true)
  })
  afterEach(() => vi.useRealTimers())

  it('filters opponents and allies, and uses the correct server join actions', async () => {
    const wrapper = mount(OpenWorldPlayers)
    await flushPromises()
    expect(wrapper.text()).toContain('Enemy')
    expect(wrapper.findAll('.world-players__player')).toHaveLength(1)
    await wrapper.get('[aria-label="Атаковать игрока Enemy"]').trigger('click')
    await flushPromises()
    expect(mocks.enter).toHaveBeenCalledWith('Enemy', true)
    await wrapper.get('[aria-label="Фильтр игроков"] button:nth-child(2)').trigger('click')
    expect(wrapper.text()).toContain('Ally')
    expect(wrapper.findAll('.world-players__player')).toHaveLength(1)
    await wrapper.get('[aria-label="Помочь игроку Ally"]').trigger('click')
    await flushPromises()
    expect(mocks.enter).toHaveBeenLastCalledWith('Ally', false)
    wrapper.unmount()
  })

  it('drops stale targets on failed refresh and explains unavailable participation', async () => {
    mocks.request.mockResolvedValueOnce({
      available: true, players: [player('Enemy', 'UNDEAD', true)],
    }).mockRejectedValueOnce(new Error('network'))
      .mockResolvedValueOnce({ available: false, players: [] })
    const wrapper = mount(OpenWorldPlayers)
    await flushPromises()
    expect(wrapper.text()).toContain('Enemy')
    await wrapper.get('.world-players__refresh').trigger('click')
    await flushPromises()
    expect(wrapper.findAll('.world-players__player')).toHaveLength(0)
    expect(wrapper.get('[role="alert"]').text()).toContain('Список игроков не обновился')
    await wrapper.get('.world-players__refresh').trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Участие недоступно')
    wrapper.unmount()
    const calls = mocks.request.mock.calls.length
    await vi.advanceTimersByTimeAsync(30_000)
    expect(mocks.request).toHaveBeenCalledTimes(calls)
  })
})
