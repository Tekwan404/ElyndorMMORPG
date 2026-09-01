import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import WarriorTalentTreeView from '@/game/talents/views/WarriorTalentTreeView.vue'
import { apiClient } from '@/api/apiClient'

vi.mock('@/api/apiClient', () => ({ apiClient: { request: vi.fn() } }))

const snapshot = {
  treeId: 'WARRIOR_TREE', classId: 'WARRIOR', version: 1,
  activeLoadoutId: 'LOADOUT_1', stateVersion: 1, earnedPoints: 9, availablePoints: 9,
  branches: [{ id: 'GUARDIAN', name: 'Страж', fantasy: 'Защита', nodeCount: 1 }],
  nodes: [{ id: 'G-1-1', branchId: 'GUARDIAN', tier: 1, requiredSpentPoints: 0,
    name: 'Железная Кожа', englishName: 'Iron Skin', maxRank: 2, prerequisites: [],
    description: 'Armor повышен.', requiredLevel: null, iconId: null,
    runtimeStatus: 'SUPPORTED', unlockedAbilityId: null }],
  loadouts: [
    { id: 'LOADOUT_1', selectedRanks: {}, spentPoints: 0 },
    { id: 'LOADOUT_2', selectedRanks: {}, spentPoints: 0 },
  ],
}

describe('WarriorTalentTreeView', () => {
  beforeEach(() => vi.mocked(apiClient.request).mockReset())

  it('renders server talent content and learns the selected node', async () => {
    vi.mocked(apiClient.request)
      .mockResolvedValueOnce(snapshot)
      .mockResolvedValueOnce({ ...snapshot, stateVersion: 2, availablePoints: 8,
        loadouts: [{ id: 'LOADOUT_1', selectedRanks: { 'G-1-1': 1 }, spentPoints: 1 }, snapshot.loadouts[1]] })

    const wrapper = mount(WarriorTalentTreeView, { attachTo: document.body })
    await flushPromises()
    expect(wrapper.get('[data-talent-node]').attributes('aria-label')).toContain('Железная Кожа')

    await wrapper.get('[data-talent-node]').trigger('click')
    const learnButton = document.body.querySelector<HTMLButtonElement>('[data-learn-talent]')
    expect(learnButton).not.toBeNull()
    learnButton?.click()
    await flushPromises()

    expect(apiClient.request).toHaveBeenLastCalledWith('/api/v1/talents/learn', expect.objectContaining({
      method: 'POST',
      body: expect.stringContaining('"mutationId"'),
    }))
    const calls = vi.mocked(apiClient.request).mock.calls
    const request = calls[calls.length - 1]?.[1]
    expect(JSON.parse(String(request?.body))).toMatchObject({
      talentId: 'G-1-1', loadoutId: 'LOADOUT_1', expectedStateVersion: 1,
    })
    expect(document.body.textContent).toContain('Ранг 1/2')
    wrapper.unmount()
  })
})
