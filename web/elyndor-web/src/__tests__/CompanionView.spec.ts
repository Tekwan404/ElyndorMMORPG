import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { CharacterCompanionSnapshot } from '@/api/contracts'
import CompanionView from '@/game/character/views/CompanionView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

const companion: CharacterCompanionSnapshot = {
  selectedPhysicalProfileId: 'ARCHER_STARTER_PREDATOR',
  effectiveProfileId: 'ARCHER_STARTER_PREDATOR',
  availableProfiles: [
    { id: 'ARCHER_STARTER_PREDATOR', name: 'Охотничий волк', archetype: 'PREDATOR', artId: 'forest-wolf' },
    { id: 'ARCHER_GUARDIAN', name: 'Зверь-страж', archetype: 'GUARDIAN', artId: 'forest-wolf' },
    { id: 'ARCHER_TRAPPER', name: 'Зверь-ловчий', archetype: 'TRAPPER', artId: 'forest-wolf' },
  ],
}

describe('CompanionView', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('shows every physical companion and selects a new active companion', async () => {
    const session = useGameSessionStore()
    session.getCompanion = vi.fn<() => Promise<CharacterCompanionSnapshot>>().mockResolvedValue(companion)
    session.selectCompanion = vi.fn<(profileId: string) => Promise<CharacterCompanionSnapshot>>().mockResolvedValue(companion)

    const wrapper = mount(CompanionView)
    await flushPromises()

    expect(wrapper.get('[data-companion-active]').text()).toContain('Охотничий волк')
    expect(wrapper.findAll('[data-companion-profile]').length).toBe(3)

    await wrapper.get('[data-companion-profile="ARCHER_GUARDIAN"]').trigger('click')

    expect(session.selectCompanion).toHaveBeenCalledWith('ARCHER_GUARDIAN')
  })
})
