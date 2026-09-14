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
}))

function snapshotFor(node: Record<string, unknown>, branchId = 'GUARDIAN') {
  return {
    treeId: 'WARRIOR_TREE',
    classId: 'WARRIOR',
    version: 1,
    activeLoadoutId: 'LOADOUT_1',
    stateVersion: 1,
    earnedPoints: 30,
    availablePoints: 30,
    branches: [{ id: branchId, name: branchId === 'GUARDIAN' ? 'Страж' : 'Берсерк', fantasy: 'Воин', nodeCount: 1 }],
    nodes: [node],
    loadouts: [
      { id: 'LOADOUT_1', selectedRanks: {}, spentPoints: 0 },
      { id: 'LOADOUT_2', selectedRanks: {}, spentPoints: 0 },
    ],
  }
}

const baseNode = {
  id: 'G-6-5',
  branchId: 'GUARDIAN',
  tier: 6,
  requiredSpentPoints: 25,
  name: 'НЕПОКОЛЕБИМЫЙ СТРАЖ',
  englishName: 'Unyielding Guardian',
  maxRank: 1,
  prerequisites: [],
  description: 'Капстоун ветки.',
  requiredLevel: null,
  iconId: null,
  runtimeStatus: 'SUPPORTED',
  unlockedAbilityId: null,
}

describe('TalentTreeView Guardian presentation layout', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(apiClient.request).mockReset()
    vi.mocked(apiClient.setReauthenticate).mockReset()
  })

  it('renders the Guardian capstone on visual row 9 while keeping gameplay tier 6', async () => {
    vi.mocked(apiClient.request).mockResolvedValueOnce(snapshotFor(baseNode))

    const wrapper = mount(TalentTreeView)
    await flushPromises()

    const node = wrapper.get('[data-talent-id="G-6-5"]')
    expect(node.attributes('data-gameplay-tier')).toBe('6')
    expect(node.attributes('data-visual-row')).toBe('9')
    expect(node.attributes('style')).toContain('grid-column: 6')

    const rowNine = wrapper.get('section[data-visual-row="9"]')
    expect(rowNine.text()).toContain('Ряд 9')
    expect(rowNine.text()).toContain('нужно 25 очков')
    wrapper.unmount()
  })

  it('keeps branches without a custom layout on their gameplay tier', async () => {
    vi.mocked(apiClient.request).mockResolvedValueOnce(snapshotFor({
      ...baseNode,
      id: 'B-3-1',
      branchId: 'BERSERKER',
      tier: 3,
      requiredSpentPoints: 10,
      name: 'Берсерк-тест',
    }, 'BERSERKER'))

    const wrapper = mount(TalentTreeView)
    await flushPromises()

    const node = wrapper.get('[data-talent-id="B-3-1"]')
    expect(node.attributes('data-gameplay-tier')).toBe('3')
    expect(node.attributes('data-visual-row')).toBe('3')
    expect(node.attributes('style') ?? '').not.toContain('grid-column')
    wrapper.unmount()
  })
})
