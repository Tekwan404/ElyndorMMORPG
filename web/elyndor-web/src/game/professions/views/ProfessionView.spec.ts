import { mount, flushPromises } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiRequestError } from '@/api/apiClient'
import * as api from '../professionApi'
import ProfessionView from './ProfessionView.vue'

vi.mock('../professionApi', () => ({
  getProfessionState: vi.fn<typeof api.getProfessionState>(),
  learnProfession: vi.fn<typeof api.learnProfession>(),
  forgetProfession: vi.fn<typeof api.forgetProfession>(),
  studyProfessionRecipe: vi.fn<typeof api.studyProfessionRecipe>(),
  craftProfessionRecipe: vi.fn<typeof api.craftProfessionRecipe>(),
}))
const session = vi.hoisted(() => ({
  snapshot: null as null | {
    character: {
      inventory: {
        items: {
          definitionId: string
          quantity: number
          isLocked: boolean
          equippedSlot: null
          transactionLocked: boolean
        }[]
      }
    }
  },
  refreshSnapshot: vi.fn<() => Promise<void>>().mockResolvedValue(undefined),
}))
vi.mock('@/stores/gameSession', () => ({ useGameSessionStore: () => session }))

const state = { learned: [], recipes: [], skinnableCorpses: [] }
async function open() {
  const wrapper = mount(ProfessionView)
  await flushPromises()
  return wrapper
}
describe('ProfessionView mutations', () => {
  it('opens the authored alchemy workshop and offers its gathering partner', async () => {
    vi.mocked(api.getProfessionState).mockResolvedValue({
      ...state,
      maxPrimaryProfessions: 2,
      available: [
        { id: 'HERBALISM', name: 'Травничество', category: 'Gathering', maxSkill: 300 },
        {
          id: 'ALCHEMY',
          name: 'Алхимия',
          category: 'Production',
          maxSkill: 300,
          relatedProfessionId: 'HERBALISM',
        },
        {
          id: 'LEATHERWORKING',
          name: 'Кожевничество',
          category: 'Production',
          maxSkill: 300,
          relatedProfessionId: 'SKINNING',
        },
      ],
    })
    vi.mocked(api.learnProfession).mockResolvedValue({
      isSuccess: true,
      errorCode: null,
      state: null,
      itemId: null,
      quantity: 0,
      skillIncreased: false,
      replayed: false,
    })
    const wrapper = await open()
    await wrapper.get('[data-production="ALCHEMY"]').trigger('click')
    expect(wrapper.text()).toContain('Алхимическая лаборатория')
    expect(wrapper.text()).toContain('Травничество')
    await wrapper.get('[data-learn-profession="HERBALISM"]').trigger('click')
    await flushPromises()
    expect(api.learnProfession).toHaveBeenCalledWith('HERBALISM')
    wrapper.unmount()
  })
  beforeEach(() => {
    vi.clearAllMocks()
    session.snapshot = null
    vi.mocked(api.getProfessionState).mockResolvedValue(state)
  })
  it('consumes a scroll through study and enables the returned learned recipe', async () => {
    session.snapshot = {
      character: {
        inventory: {
          items: [
            {
              definitionId: 'SCROLL',
              quantity: 1,
              isLocked: false,
              equippedSlot: null,
              transactionLocked: false,
            },
          ],
        },
      },
    }
    const recipe = {
      id: 'RECIPE',
      name: 'Капюшон',
      professionId: 'LEATHERWORKING',
      requiredSkill: 1,
      outputItemId: 'HEAD',
      outputQuantity: 1,
      ingredients: [],
      requiredLocationId: null,
      isLearned: false,
      learningScrollItemId: 'SCROLL',
      learningScrollName: 'Свиток капюшона',
    }
    const initial = {
      ...state,
      learned: [
        {
          id: 'LEATHERWORKING',
          name: 'Кожевничество',
          category: 'Production',
          skill: 1,
          maxSkill: 300,
        },
      ],
      recipes: [recipe],
    }
    vi.mocked(api.getProfessionState).mockResolvedValue(initial)
    vi.mocked(api.studyProfessionRecipe).mockResolvedValue({
      isSuccess: true,
      errorCode: null,
      state: { ...initial, recipes: [{ ...recipe, isLearned: true }] },
      itemId: null,
      quantity: 0,
      skillIncreased: false,
      replayed: false,
    })
    const wrapper = await open()
    await wrapper.get('[data-study-recipe="RECIPE"]').trigger('click')
    await flushPromises()
    expect(api.studyProfessionRecipe).toHaveBeenCalledWith('RECIPE')
    expect(session.refreshSnapshot).toHaveBeenCalledOnce()
    expect(wrapper.find('[data-study-recipe="RECIPE"]').exists()).toBe(false)
    expect(
      wrapper.get('[data-profession-recipe="RECIPE"] button').attributes('disabled'),
    ).toBeUndefined()
    expect(wrapper.text()).toContain('Свиток израсходован')
    wrapper.unmount()
  })
  it('studies a scroll recipe before allowing crafting', async () => {
    const recipe = {
      id: 'LW_SCROLL_HEAD',
      name: 'Кожаный капюшон',
      professionId: 'LEATHERWORKING',
      requiredSkill: 1,
      outputItemId: 'HEAD',
      outputQuantity: 1,
      ingredients: [],
      requiredLocationId: null,
      isLearned: false,
      learningScrollItemId: 'SCROLL_HEAD',
      learningScrollName: 'Свиток: Кожаный капюшон',
    }
    vi.mocked(api.getProfessionState).mockResolvedValue({
      ...state,
      learned: [
        {
          id: 'LEATHERWORKING',
          name: 'Кожевничество',
          category: 'Production',
          skill: 1,
          maxSkill: 300,
        },
      ],
      recipes: [recipe],
    })
    const wrapper = await open()
    expect(wrapper.get('[data-study-recipe="LW_SCROLL_HEAD"]').attributes('disabled')).toBeDefined()
    expect(wrapper.text()).toContain('Свиток: Кожаный капюшон')
    expect(
      wrapper
        .get('[data-profession-recipe="LW_SCROLL_HEAD"] button:last-child')
        .attributes('disabled'),
    ).toBeDefined()
    wrapper.unmount()
  })
  it('requires explicit confirmation before losing profession skill', async () => {
    vi.mocked(api.getProfessionState).mockResolvedValue({
      ...state,
      learned: [
        { id: 'SKINNING', name: 'Снятие шкур', category: 'Gathering', skill: 42, maxSkill: 300 },
      ],
    })
    vi.mocked(api.forgetProfession).mockResolvedValue({
      isSuccess: true,
      errorCode: null,
      state,
      itemId: null,
      quantity: 0,
      skillIncreased: false,
      replayed: false,
    })
    const wrapper = await open()
    await wrapper.get('[data-forget-profession="SKINNING"]').trigger('click')
    expect(api.forgetProfession).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Восстановить его нельзя')
    await wrapper.get('[data-confirm-forget="SKINNING"]').trigger('click')
    await flushPromises()
    expect(api.forgetProfession).toHaveBeenCalledWith('SKINNING')
    expect(wrapper.text()).toContain('При повторном изучении навык начнётся с 1')
    wrapper.unmount()
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
