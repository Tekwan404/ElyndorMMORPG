import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { apiClient } from '@/api/apiClient'
import TalentTreeView from '@/game/talents/views/TalentTreeView.vue'

vi.mock('@/api/apiClient', () => ({
  apiClient: {
    request: vi.fn<(...args: unknown[]) => Promise<unknown>>(),
    setReauthenticate: vi.fn<(handler: () => Promise<string>) => void>(),
  },
  ApiRequestError: class ApiRequestError extends Error {
    code = 'test_error'
  },
}))

const snapshot = {
  treeId: 'MAGE_TREE',
  classId: 'MAGE',
  version: 1,
  activeLoadoutId: 'LOADOUT_1',
  stateVersion: 1,
  earnedPoints: 20,
  availablePoints: 20,
  branches: [
    { id: 'FIRE', name: 'Пламя', fantasy: 'Критические удары и Горение', nodeCount: 1 },
    { id: 'ARCANE', name: 'Тайная магия', fantasy: 'Мана и Тайные заряды', nodeCount: 1 },
    { id: 'FROST', name: 'Лёд', fantasy: 'Обморожение и контроль темпа', nodeCount: 1 },
  ],
  nodes: [
    {
      id: 'F-1-1', branchId: 'FIRE', tier: 1, requiredSpentPoints: 0,
      name: 'Точное Пламя', englishName: 'Precise Flame', maxRank: 4,
      prerequisites: [], requiredLevel: null, iconId: null, runtimeStatus: 'SUPPORTED',
      unlockedAbilityId: 'MAGE_FIREBALL',
      description: 'Первый ранг открывает «Огненный шар».',
    },
    {
      id: 'A-3-1', branchId: 'ARCANE', tier: 3, requiredSpentPoints: 10,
      name: 'Тайный Взрыв', englishName: 'Arcane Burst', maxRank: 1,
      prerequisites: [], requiredLevel: null, iconId: null, runtimeStatus: 'SUPPORTED',
      unlockedAbilityId: 'ARCANE_BURST',
      description: 'Открывает «Тайный взрыв»: 1,2 сек. произнесения, 25 маны, 6 сек. перезарядки.',
    },
    {
      id: 'I-5-1', branchId: 'FROST', tier: 5, requiredSpentPoints: 20,
      name: 'Сердце Зимы', englishName: 'Heart of Winter', maxRank: 1,
      prerequisites: [], requiredLevel: null, iconId: null, runtimeStatus: 'SUPPORTED',
      unlockedAbilityId: 'HEART_OF_WINTER',
      description: 'Открывает «Сердце зимы»: на 10 сек. усиливает ледяные заклинания.',
    },
  ],
  loadouts: [
    { id: 'LOADOUT_1', selectedRanks: {}, spentPoints: 0 },
    { id: 'LOADOUT_2', selectedRanks: {}, spentPoints: 0 },
  ],
}

describe('Mage TalentTreeView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    vi.mocked(apiClient.request).mockReset()
    vi.mocked(apiClient.request).mockResolvedValue(snapshot)
  })

  it('renders three Mage branches with supported player-facing talent descriptions', async () => {
    const wrapper = mount(TalentTreeView, { attachTo: document.body })
    await flushPromises()

    const branchButtons = wrapper.findAll('.branches button')
    expect(branchButtons.map(button => button.text())).toEqual(
      expect.arrayContaining(['Пламя0', 'Тайная магия0', 'Лёд0']),
    )

    const arcane = branchButtons.find(button => button.text().includes('Тайная магия'))
    expect(arcane).toBeDefined()
    await arcane!.trigger('click')
    await wrapper.get('[data-talent-node]').trigger('click')
    await flushPromises()

    expect(document.body.textContent).toContain('Тайный Взрыв')
    expect(document.body.textContent).toContain('Открывает способность «Тайный взрыв»')
    expect(document.body.textContent).toContain('25 маны')
    expect(document.body.textContent).not.toContain('Этот эффект пока не участвует в бою.')

    const frost = wrapper.findAll('.branches button')
      .find(button => button.text().includes('Лёд'))
    await frost!.trigger('click')
    await wrapper.get('[data-talent-node]').trigger('click')
    await flushPromises()

    expect(document.body.textContent).toContain('Сердце Зимы')
    expect(document.body.textContent).toContain('Открывает способность «Сердце зимы»')
    wrapper.unmount()
  })
})
