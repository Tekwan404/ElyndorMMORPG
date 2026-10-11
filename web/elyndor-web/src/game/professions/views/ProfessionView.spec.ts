import { mount, flushPromises } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiRequestError } from '@/api/apiClient'
import * as api from '../professionApi'
import type { InventoryItem } from '@/api/contracts'
import ProfessionView from './ProfessionView.vue'

vi.mock('../professionApi', () => ({
  getProfessionState: vi.fn<typeof api.getProfessionState>(),
  learnProfession: vi.fn<typeof api.learnProfession>(),
  craftProfessionRecipe: vi.fn<typeof api.craftProfessionRecipe>(),
  gatherProfessionNode: vi.fn<typeof api.gatherProfessionNode>(),
  disenchantProfessionItem: vi.fn<typeof api.disenchantProfessionItem>(),
  enchantProfessionItem: vi.fn<typeof api.enchantProfessionItem>(),
  forgetProfession: vi.fn<typeof api.forgetProfession>(),
}))
const session = vi.hoisted(() => ({
  snapshot: null as {
    world: { currentLocation: { id: string } }
    character: { inventory: { items: InventoryItem[] } }
  } | null,
  refreshSnapshot: vi.fn<() => Promise<void>>(),
}))
vi.mock('@/stores/gameSession', () => ({ useGameSessionStore: () => session }))

const state = { learned: [], recipes: [], skinnableCorpses: [] }
async function open() {
  const wrapper = mount(ProfessionView)
  await flushPromises()
  return wrapper
}
describe('ProfessionView mutations', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    session.snapshot = null
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

describe('Profession workshop catalog and actions', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    session.snapshot = null
    vi.mocked(api.getProfessionState).mockResolvedValue(state)
  })
  it('presents five recognizable professions and respects the two-profession limit', async () => {
    vi.mocked(api.getProfessionState).mockResolvedValue({
      ...state,
      learned: [
        { id: 'SKINNING', name: 'Снятие шкур', category: 'Gathering', skill: 1, maxSkill: 300 },
        {
          id: 'LEATHERWORKING',
          name: 'Кожевничество',
          category: 'Production',
          skill: 1,
          maxSkill: 300,
        },
      ],
    })
    const wrapper = await open()
    expect(wrapper.findAll('[data-profession]')).toHaveLength(5)
    expect(wrapper.get('[data-profession="ENCHANTING"]').text()).toContain('Наложение чар')
    expect(wrapper.get('[data-profession="MINING"] button').attributes('disabled')).toBeDefined()
    expect(wrapper.findAll('.profession-art')).toHaveLength(5)
    wrapper.unmount()
  })
  it('gathers from the actual location and reconciles the server cooldown', async () => {
    session.snapshot = {
      world: { currentLocation: { id: 'WHISPERING_FOREST' } },
      character: { inventory: { items: [] } },
    }
    const miningState = {
      ...state,
      learned: [
        { id: 'MINING', name: 'Горное дело', category: 'Gathering', skill: 1, maxSkill: 300 },
      ],
      gatheringNodes: [
        {
          id: 'COPPER_VEIN',
          name: 'Медная жила',
          locationId: 'WHISPERING_FOREST',
          requiredSkill: 1,
          skillUpUntil: 16,
          itemId: 'COPPER_ORE',
          quantity: 2,
          cooldownSeconds: 60,
        },
      ],
    }
    vi.mocked(api.getProfessionState).mockResolvedValue(miningState)
    vi.mocked(api.gatherProfessionNode).mockResolvedValue({
      isSuccess: true,
      errorCode: null,
      state: { ...miningState, nextGatherAtUtc: new Date(Date.now() + 60000).toISOString() },
      itemId: 'COPPER_ORE',
      quantity: 2,
      skillIncreased: true,
      replayed: false,
    })
    const wrapper = await open()
    await wrapper.get('[data-gathering-node="COPPER_VEIN"] button').trigger('click')
    await flushPromises()
    expect(api.gatherProfessionNode).toHaveBeenCalledWith('COPPER_VEIN')
    expect(session.refreshSnapshot).toHaveBeenCalledOnce()
    expect(
      wrapper.get('[data-gathering-node="COPPER_VEIN"] button').attributes('disabled'),
    ).toBeDefined()
    wrapper.unmount()
  })
  it('requires explicit confirmation before forgetting a trained profession', async () => {
    vi.mocked(api.getProfessionState).mockResolvedValue({
      ...state,
      learned: [
        { id: 'MINING', name: 'Горное дело', category: 'Gathering', skill: 16, maxSkill: 300 },
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
    await wrapper.get('.forget-link').trigger('click')
    expect(api.forgetProfession).not.toHaveBeenCalled()
    expect(wrapper.get('[role="alert"]').text()).toContain(
      'Весь навык этой профессии будет потерян',
    )
    await wrapper.get('[role="alert"] button').trigger('click')
    await flushPromises()
    expect(api.forgetProfession).toHaveBeenCalledWith('MINING')
    wrapper.unmount()
  })
})
