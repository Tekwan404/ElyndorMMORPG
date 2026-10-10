import { mount, flushPromises } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiRequestError } from '@/api/apiClient'
import * as api from '../professionApi'
import ProfessionView from './ProfessionView.vue'

vi.mock('../professionApi', () => ({
  getProfessionState: vi.fn<typeof api.getProfessionState>(),
  learnProfession: vi.fn<typeof api.learnProfession>(),
  craftProfessionRecipe: vi.fn<typeof api.craftProfessionRecipe>(),
}))
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
  it('does not promise another location after the highest available gathering step', async () => {
    vi.mocked(api.getProfessionState).mockResolvedValue({
      ...state,
      learned: [
        { id: 'SKINNING', name: 'Снятие шкур', category: 'Gathering', skill: 61, maxSkill: 300 },
      ],
      materialSources: [
        {
          itemId: 'THICK_HIDE',
          itemName: 'Толстая шкура',
          monsterName: 'Волк',
          locationId: 'DEEP_FOREST',
          locationName: 'Глубокий лес',
          requiredSkill: 31,
          skillUpUntil: 61,
        },
      ],
    })
    const wrapper = await open()
    expect(wrapper.text()).toContain('Продолжение пока не открыто')
    expect(wrapper.text()).not.toContain('дальнейшая прокачка в следующей локации')
    wrapper.unmount()
  })
  it('shows growth, material origins and specific locked recipe requirements', async () => {
    vi.mocked(api.getProfessionState).mockResolvedValue({
      ...state,
      learned: [
        {
          id: 'LEATHERWORKING',
          name: 'Кожевничество',
          category: 'Production',
          skill: 40,
          maxSkill: 300,
        },
      ],
      materials: [{ id: 'LIGHT_LEATHER', name: 'Лёгкая кожа' }],
      materialSources: [
        {
          itemId: 'LIGHT_HIDE',
          itemName: 'Лёгкая шкура',
          monsterName: 'Волк',
          locationId: 'DEEP_FOREST',
          locationName: 'Глубокий лес',
          requiredSkill: 35,
        },
      ],
      recipes: [
        {
          id: 'PROCESS',
          name: 'Выделка лёгкой кожи',
          professionId: 'LEATHERWORKING',
          requiredSkill: 35,
          outputItemId: 'LIGHT_LEATHER',
          outputQuantity: 1,
          ingredients: [{ itemId: 'LIGHT_HIDE', quantity: 1 }],
          requiredLocationId: 'STARTER_TOWN',
          skillUpUntil: 100,
        },
        {
          id: 'CHEST',
          name: 'Куртка',
          professionId: 'LEATHERWORKING',
          requiredSkill: 55,
          outputItemId: 'CHEST_ITEM',
          outputQuantity: 1,
          ingredients: [{ itemId: 'LIGHT_LEATHER', quantity: 8 }],
          requiredLocationId: 'STARTER_TOWN',
          skillUpUntil: 100,
        },
      ],
    })
    const wrapper = await open()
    expect(wrapper.text()).toContain('Глубокий лес')
    expect(wrapper.text()).toContain('Может повышать навык до 100')
    expect(wrapper.get('[data-profession-recipe="CHEST"]').text()).toContain('Требуется навык 55')
    await wrapper.findAll('.workshop-filters button')[2]!.trigger('click')
    expect(wrapper.find('[data-profession-recipe="PROCESS"]').exists()).toBe(false)
    expect(wrapper.find('[data-profession-recipe="CHEST"]').exists()).toBe(true)
    wrapper.unmount()
  })
  it('preserves a mutation error after reconciling profession state', async () => {
    vi.mocked(api.learnProfession).mockRejectedValue(
      new ApiRequestError(409, 'profession_limit_reached'),
    )
    const wrapper = await open()
    await wrapper.findAll('button')[0]!.trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Можно изучить не больше двух профессий.')
    wrapper.unmount()
  })
  it('uses the returned canonical state without redundant reloads', async () => {
    vi.mocked(api.learnProfession).mockResolvedValue({
      isSuccess: true,
      errorCode: null,
      state,
      itemId: null,
      quantity: 0,
      skillIncreased: false,
      replayed: false,
    })
    const wrapper = await open()
    await wrapper.findAll('button')[0]!.trigger('click')
    await flushPromises()
    expect(api.getProfessionState).toHaveBeenCalledTimes(1)
    expect(wrapper.text()).toContain('Снятие шкур изучено.')
    wrapper.unmount()
  })
})
