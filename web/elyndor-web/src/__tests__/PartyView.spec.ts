import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { BootstrapSnapshot } from '@/api/contracts'
import PartyView from '@/game/party/views/PartyView.vue'
import { usePartyStore } from '@/game/party/partyStore'
import { useGameSessionStore } from '@/stores/gameSession'

const CHARACTER_ID = 'character-1'

describe('PartyView', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('keeps party management focused while exposing one map entry to dungeons', async () => {
    const session = useGameSessionStore()
    session.snapshot = {
      accountId: 'account-1',
      character: { id: CHARACTER_ID },
    } as unknown as BootstrapSnapshot

    const party = usePartyStore()
    party.snapshot = {
      partyId: 'party-1',
      leaderCharacterId: CHARACTER_ID,
      version: 1,
      members: [{
        characterId: CHARACTER_ID,
        name: 'Tekwan',
        level: 25,
        classId: 'WARRIOR',
        isLeader: true,
        joinedAtUtc: '2026-09-10T00:00:00Z',
      }],
    }
    vi.spyOn(party, 'refresh').mockResolvedValue(undefined)

    const wrapper = mount(PartyView)
    await flushPromises()

    expect(wrapper.text()).toContain('Группа')
    expect(wrapper.text()).toContain('Tekwan')
    expect(wrapper.find('[data-party-disband]').exists()).toBe(true)
    await wrapper.get('[data-party-open-dungeons]').trigger('click')
    expect(wrapper.emitted('open-world')).toEqual([[]])
    expect(wrapper.find('[data-dungeon-teleport]').exists()).toBe(false)
    expect(wrapper.find('[data-create-dungeon]').exists()).toBe(false)
  })
})
