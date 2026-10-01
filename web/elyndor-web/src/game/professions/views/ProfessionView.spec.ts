import { mount, flushPromises } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiRequestError } from '@/api/apiClient'
import * as api from '../professionApi'
import ProfessionView from './ProfessionView.vue'

vi.mock('../professionApi', () => ({ getProfessionState: vi.fn<typeof api.getProfessionState>(), learnProfession: vi.fn<typeof api.learnProfession>(), craftProfessionRecipe: vi.fn<typeof api.craftProfessionRecipe>() }))
vi.mock('@/stores/gameSession', () => ({ useGameSessionStore: () => ({ snapshot: null }) }))

const state = { learned: [], recipes: [], skinnableCorpses: [] }
async function open() {
  const wrapper = mount(ProfessionView)
  await flushPromises()
  return wrapper
}
describe('ProfessionView mutations', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(api.getProfessionState).mockResolvedValue(state)
  })
  it('preserves a mutation error after reconciling profession state', async () => {
    vi.mocked(api.learnProfession).mockRejectedValue(new ApiRequestError(409, 'profession_limit_reached'))
    const wrapper = await open()
    await wrapper.findAll('button')[0]!.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Можно изучить не больше двух профессий.')
    wrapper.unmount()
  })
  it('uses the returned canonical state without redundant reloads', async () => {
    vi.mocked(api.learnProfession).mockResolvedValue({ isSuccess: true, errorCode: null, state,
      itemId: null, quantity: 0, skillIncreased: false, replayed: false })
    const wrapper = await open()
    await wrapper.findAll('button')[0]!.trigger('click')
    await flushPromises()
    expect(api.getProfessionState).toHaveBeenCalledTimes(1)
    expect(wrapper.text()).toContain('Снятие шкур изучено.')
    wrapper.unmount()
  })
})
