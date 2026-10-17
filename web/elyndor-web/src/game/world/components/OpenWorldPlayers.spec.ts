import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import OpenWorldPlayers from './OpenWorldPlayers.vue'

const mocks = vi.hoisted(() => ({ request: vi.fn<() => Promise<unknown>>(), enter: vi.fn<(id: string, attack: boolean) => Promise<boolean>>() }))
vi.mock('@/api/apiClient', () => ({ apiClient: { request: mocks.request } }))
vi.mock('@/stores/gameSession', () => ({ useGameSessionStore: () => ({ snapshot: { character: { raceId: 'HUMAN' } } }) }))
vi.mock('@/stores/combatSession', () => ({ useCombatSessionStore: () => ({ enterOpenWorldCombat: mocks.enter, isActive: false, errorCode: null }) }))

const player = (id: string, raceId: string, inCombat: boolean) => ({
  characterId: id, name: id, raceId, inCombat, classId: 'WARRIOR', genderId: 'MALE', skinId: null, level: 60,
})

describe('OpenWorldPlayers', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.clearAllMocks()
    Object.defineProperty(document, 'hidden', { configurable: true, value: false })
    mocks.request.mockResolvedValue({ available: true, players: [player('Enemy', 'UNDEAD', true), player('Ally', 'HUMAN', true)] })
    mocks.enter.mockResolvedValue(true)
  })
  afterEach(() => vi.useRealTimers())

  it('offers enemy attack and independent entry, but only assistance for allies', async () => {
    const wrapper = mount(OpenWorldPlayers)
    await flushPromises()
    const rows = wrapper.findAll('li')
    expect(rows[0]!.text()).toContain('Атаковать')
    expect(rows[0]!.text()).toContain('Вступить в схватку')
    expect(rows[1]!.text()).toContain('Помочь союзнику')
    expect(rows[1]!.text()).not.toContain('Атаковать')
    await rows[0]!.findAll('button')[0]!.trigger('click')
    await flushPromises()
    expect(mocks.enter).toHaveBeenCalledWith('Enemy', true)
    await rows[1]!.find('button').trigger('click')
    await flushPromises()
    expect(mocks.enter).toHaveBeenLastCalledWith('Ally', false)
    wrapper.unmount()
  })

  it('shows a recoverable discovery error and stops refreshing after unmount', async () => {
    mocks.request.mockRejectedValueOnce(new Error('network'))
    const wrapper = mount(OpenWorldPlayers)
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('Обнови список')
    await wrapper.get('header button').trigger('click')
    await flushPromises()
    expect(wrapper.findAll('li')).toHaveLength(2)
    wrapper.unmount()
    const calls = mocks.request.mock.calls.length
    await vi.advanceTimersByTimeAsync(30_000)
    expect(mocks.request).toHaveBeenCalledTimes(calls)
  })
})
