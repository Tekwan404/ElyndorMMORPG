import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import WarriorTalentTreeView from '@/game/talents/views/WarriorTalentTreeView.vue'
import { apiClient } from '@/api/apiClient'

vi.mock('@/api/apiClient', () => ({
  apiClient: {
    request: vi.fn<(...args: unknown[]) => Promise<unknown>>(),
    setReauthenticate: vi.fn<(handler: () => Promise<string>) => void>(),
  },
}))

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
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(apiClient.request).mockReset()
    vi.mocked(apiClient.setReauthenticate).mockReset()
  })

  it('renders server talent content, learns a node, and refreshes the character snapshot', async () => {
    vi.mocked(apiClient.request)
      .mockResolvedValueOnce(snapshot)
      .mockResolvedValueOnce({ ...snapshot, stateVersion: 2, availablePoints: 8,
        loadouts: [{ id: 'LOADOUT_1', selectedRanks: { 'G-1-1': 1 }, spentPoints: 1 }, snapshot.loadouts[1]] })
      .mockResolvedValueOnce({ character: { id: 'character-1' } })

    const wrapper = mount(WarriorTalentTreeView, { attachTo: document.body })
    await flushPromises()
    expect(wrapper.get('[data-talent-node]').attributes('aria-label')).toContain('Железная Кожа')

    await wrapper.get('[data-talent-node]').trigger('click')
    const learnButton = document.body.querySelector<HTMLButtonElement>('[data-learn-talent]')
    expect(learnButton).not.toBeNull()
    expect(learnButton?.disabled).toBe(false)
    learnButton?.click()
    await flushPromises()

    expect(apiClient.request).toHaveBeenNthCalledWith(2, '/api/v1/talents/learn', expect.objectContaining({
      method: 'POST',
      body: expect.stringContaining('"mutationId"'),
    }))
    const request = vi.mocked(apiClient.request).mock.calls[1]?.[1]
    expect(JSON.parse(String(request?.body))).toMatchObject({
      talentId: 'G-1-1', loadoutId: 'LOADOUT_1', expectedStateVersion: 1,
    })
    expect(apiClient.request).toHaveBeenNthCalledWith(3, '/api/v1/bootstrap')
    expect(document.body.textContent).toContain('Ранг 1/2')
    wrapper.unmount()
  })

  it('disables learning an upgradable talent when there are no available points', async () => {
    vi.mocked(apiClient.request).mockResolvedValueOnce({
      ...snapshot,
      earnedPoints: 1,
      availablePoints: 0,
      loadouts: [
        { id: 'LOADOUT_1', selectedRanks: { 'G-1-1': 1 }, spentPoints: 1 },
        snapshot.loadouts[1],
      ],
    })

    const wrapper = mount(WarriorTalentTreeView, { attachTo: document.body })
    await flushPromises()
    await wrapper.get('[data-talent-node]').trigger('click')

    const learnButton = document.body.querySelector<HTMLButtonElement>('[data-learn-talent]')
    expect(learnButton).not.toBeNull()
    expect(learnButton?.disabled).toBe(true)
    expect(document.body.textContent).toContain('Нет свободных очков талантов')

    learnButton?.click()
    await flushPromises()
    expect(apiClient.request).toHaveBeenCalledTimes(1)
    wrapper.unmount()
  })

  it('uses the Guardian presentation row without changing the gameplay tier', async () => {
    vi.mocked(apiClient.request).mockResolvedValueOnce({
      ...snapshot,
      earnedPoints: 30,
      availablePoints: 30,
      nodes: [{
        ...snapshot.nodes[0],
        id: 'G-6-5',
        tier: 6,
        requiredSpentPoints: 25,
        name: 'НЕПОКОЛЕБИМЫЙ СТРАЖ',
        maxRank: 1,
      }],
    })

    const wrapper = mount(WarriorTalentTreeView)
    await flushPromises()

    const node = wrapper.get('[data-talent-node]')
    expect(node.attributes('data-gameplay-tier')).toBe('6')
    expect(node.attributes('data-visual-row')).toBe('9')
    expect(node.attributes('style')).toContain('grid-column: 6')
    wrapper.unmount()
  })

  it('falls back to gameplay tiers when Guardian content contains an unmapped node', async () => {
    vi.mocked(apiClient.request).mockResolvedValueOnce({
      ...snapshot,
      nodes: [{
        ...snapshot.nodes[0],
        id: 'G-FUTURE',
        tier: 4,
        requiredSpentPoints: 15,
        name: 'Будущий талант',
      }],
    })

    const wrapper = mount(WarriorTalentTreeView)
    await flushPromises()

    const node = wrapper.get('[data-talent-node]')
    expect(node.attributes('data-gameplay-tier')).toBe('4')
    expect(node.attributes('data-visual-row')).toBe('4')
    expect(node.attributes('style') ?? '').not.toContain('grid-column')
    wrapper.unmount()
  })
})
