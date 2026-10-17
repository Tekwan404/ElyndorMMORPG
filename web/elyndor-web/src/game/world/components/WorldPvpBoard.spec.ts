import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { clearPendingGameMutation } from '@/api/replaySafeMutation'
import WorldPvpBoard from './WorldPvpBoard.vue'

const mocks = vi.hoisted(() => ({
  request: vi.fn<(path: string, init: RequestInit) => Promise<unknown>>(),
}))
vi.mock('@/api/apiClient', () => ({
  apiClient: { request: mocks.request },
  ApiRequestError: class extends Error {},
}))
vi.mock('@/stores/gameSession', () => ({
  useGameSessionStore: () => ({
    snapshot: { character: { level: 60 } },
    refreshSnapshot: vi.fn<() => Promise<void>>(),
  }),
}))
const overview = {
  rewards: { goldPerKill: 25000, goldPerAssist: 5000 },
  season: 1,
  endsAtUtc: '2026-12-12T00:00:00Z',
  fieldHonor: 5,
  bloodyTokens: 1,
  rewardedToday: 1,
  dailyLimit: 20,
  self: { rating: 10, victories: 1, assists: 0, deaths: 0 },
  leaders: [
    {
      characterId: 'a',
      name: 'Воин',
      raceId: 'HUMAN',
      rating: 10,
      victories: 1,
      assists: 0,
      deaths: 0,
    },
  ],
}

describe('WorldPvpBoard', () => {
  afterEach(() => clearPendingGameMutation())
  it('loads server balances, filters rankings and buys with a stable retry id', async () => {
    let failPurchase = true
    mocks.request.mockReset().mockImplementation(async (url: string) => {
      if (url.endsWith('/buy')) {
        if (failPurchase) {
          failPurchase = false
          throw new Error('lost reply')
        }
        return { succeeded: true, errorCode: null }
      }
      if (url.includes('/shop'))
        return [
          { id: 'sword', name: 'Клинок', requiredLevel: 60, fieldHonorPrice: 5, tokenPrice: 1 },
        ]
      return overview
    })
    let wrapper = mount(WorldPvpBoard)
    await flushPromises()
    expect(wrapper.text()).toContain('Полевая честь: 5')
    expect(wrapper.find('[aria-label="Золото: 2; серебро: 50; бронза: 0"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Воин')
    await wrapper.get('select').setValue('UNDEAD')
    await flushPromises()
    expect(mocks.request).toHaveBeenCalledWith(
      expect.stringContaining('raceId=UNDEAD'),
      expect.anything(),
    )
    await wrapper.get('[data-buy-honor]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('повторить')
    wrapper.unmount()
    wrapper = mount(WorldPvpBoard)
    await flushPromises()
    await wrapper.get('[data-buy-honor]').trigger('click')
    await flushPromises()
    const calls = mocks.request.mock.calls.filter((call) => String(call[0]).endsWith('/buy'))
    expect(calls[0]![1].body).toEqual(calls[1]![1].body)
    expect(calls[0]![1].headers).toEqual({ 'Content-Type': 'application/json' })
    wrapper.unmount()
  })

  it('keeps the leaderboard available if the reward shop is offline', async () => {
    mocks.request.mockReset().mockImplementation(async (url: string) => {
      if (url.includes('/shop')) throw new Error('catalog unavailable')
      return overview
    })
    const wrapper = mount(WorldPvpBoard)
    await flushPromises()
    expect(wrapper.text()).toContain('Полевая честь: 5')
    expect(wrapper.text()).toContain('Воин')
    expect(wrapper.find('[data-world-pvp-ranking]').exists()).toBe(true)
    expect(wrapper.get('[role="alert"]').text()).toContain('Магазин временно недоступен')
    expect(wrapper.find('[data-buy-honor]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('shows loading failure and can retry without fabricated balances', async () => {
    mocks.request.mockReset().mockRejectedValue(new Error('offline'))
    const wrapper = mount(WorldPvpBoard)
    await flushPromises()
    expect(wrapper.find('[role="alert"]').exists()).toBe(true)
    expect(wrapper.text()).not.toContain('Полевая честь:')
    expect(wrapper.find('[data-buy-honor]').exists()).toBe(false)
    wrapper.unmount()
  })
})
