import { mount, flushPromises } from '@vue/test-utils'
import { reactive } from 'vue'
import { describe, expect, it, vi } from 'vitest'
import * as api from '../professionApi'
import SkinningAftermath from './SkinningAftermath.vue'

vi.mock('../professionApi', () => ({ getProfessionState: vi.fn<typeof api.getProfessionState>(), skinCorpse: vi.fn<typeof api.skinCorpse>() }))
vi.mock('@/stores/gameSession', () => ({ useGameSessionStore: () => ({ refreshSnapshot: vi.fn<() => Promise<void>>() }) }))
const combat = reactive<{ snapshot: { status: string; sessionId: string } | null }>({ snapshot: null })
vi.mock('@/stores/combatSession', () => ({ useCombatSessionStore: () => combat }))

describe('SkinningAftermath', () => {
  it('loads new corpses after a victory while the location remains mounted', async () => {
    const state = { learned: [{ id: 'SKINNING', name: 'Снятие шкур', category: 'Gathering', skill: 1, maxSkill: 300 }], recipes: [], skinnableCorpses: [] }
    vi.mocked(api.getProfessionState).mockResolvedValueOnce(state).mockResolvedValueOnce({ ...state,
      skinnableCorpses: [{ combatSessionId: 'fight', enemyActorId: 'wolf', monsterDefinitionId: 'FOREST_WOLF_L1', monsterName: 'Волк', requiredSkill: 1, expiresAtUtc: '2026-10-02T12:00:00Z' }] })
    const wrapper = mount(SkinningAftermath)
    await flushPromises()
    expect(wrapper.find('[data-skin-corpse]').exists()).toBe(false)
    combat.snapshot = { status: 'Victory', sessionId: 'fight' }
    await flushPromises()
    expect(wrapper.find('[data-skin-corpse]').exists()).toBe(true)
    expect(api.getProfessionState).toHaveBeenCalledTimes(2)
    wrapper.unmount()
  })
})
