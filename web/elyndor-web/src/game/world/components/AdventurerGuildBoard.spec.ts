import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { BootstrapSnapshot } from '@/api/contracts'
import { apiClient } from '@/api/apiClient'
import AdventurerGuildBoard from '@/game/world/components/AdventurerGuildBoard.vue'
import { useGameSessionStore } from '@/stores/gameSession'

function openGuild() {
  return mount(AdventurerGuildBoard, {
    props: { open: true, locationId: 'STARTER_TOWN' },
    global: {
      stubs: {
        UIModal: {
          props: ['open'],
          template: '<div v-if="open"><slot /></div>',
        },
      },
    },
  })
}

describe('AdventurerGuildBoard: registrar and World PvP', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.restoreAllMocks()
    vi.spyOn(apiClient, 'request').mockImplementation(async path => (path.endsWith('/shop') ? [] : {
      season: 1, endsAtUtc: '2026-12-12T00:00:00Z', fieldHonor: 0, bloodyTokens: 0,
      rewardedToday: 0, dailyLimit: 20, leaders: [],
      self: { characterId: 'human-hero', name: 'Воин', raceId: 'HUMAN', seasonHonor: 0, victories: 0, assists: 0, deaths: 0 },
    }) as never)
    const session = useGameSessionStore()
    session.snapshot = {
      character: { id: 'human-hero', raceId: 'HUMAN', level: 60 },
      world: { currentLocation: { id: 'STARTER_TOWN' } },
    } as unknown as BootstrapSnapshot
    vi.spyOn(session, 'refreshQuestJournal').mockResolvedValue(null)
  })

  it('keeps the registrar as the only guild NPC and opens World PvP inside the guild', async () => {
    const wrapper = openGuild()
    await flushPromises()

    expect(wrapper.get('[data-adventurer-guild-board]').text()).toContain('Селия')
    expect(wrapper.get('[data-adventurer-guild-board]').text()).not.toContain('Гаррет')
    expect(wrapper.get('[data-adventurer-guild-board]').text()).not.toContain('Бран')
    expect(wrapper.get('[data-adventurer-guild-board]').text()).not.toContain('Эллира')
    expect(wrapper.find('.guild-board__people').exists()).toBe(false)
    expect(wrapper.get('[data-guild-section="contracts"]').attributes('aria-pressed')).toBe('true')

    await wrapper.get('[data-guild-section="world-pvp"]').trigger('click')

    expect(wrapper.find('[data-guild-world-pvp]').exists()).toBe(true)
    expect(wrapper.get('[data-world-pvp]').text()).toContain('Война фракций')
    expect(wrapper.find('[data-world-pvp-preview]').exists()).toBe(false)
    expect(wrapper.find('[data-guild-accept-contract]').exists()).toBe(false)
    await wrapper.get('[data-world-pvp-tab="rating"]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-world-pvp-ranking]').text()).toContain('Слава открытого мира')
    await wrapper.get('[data-world-pvp-tab="titles"]').trigger('click')
    expect(wrapper.text()).toContain('Четырнадцать ступеней славы')

    await wrapper.get('[data-guild-section="contracts"]').trigger('click')
    expect(wrapper.get('[data-guild-section="contracts"]').attributes('aria-pressed')).toBe('true')
    wrapper.unmount()
  })

  it('preserves existing registered contract actions when World PvP is added', async () => {
    const session = useGameSessionStore()
    session.questJournal = {
      quests: [{
        id: 'CONTRACT_TEST',
        type: 'CONTRACT',
        status: 'AVAILABLE',
        displayName: 'Контракт: Теневой страж',
        description: 'Победить стража.',
        offerLocationId: 'STARTER_TOWN',
        regionName: 'Стартовый город',
        requiredLevel: 1,
        rewardXp: 80,
        rewardGold: 500,
        unlockLocationId: null,
      }],
    } as never
    const accept = vi.spyOn(session, 'acceptQuest').mockResolvedValue(undefined)
    const wrapper = openGuild()
    await flushPromises()

    expect(wrapper.get('[data-guild-contract-id="CONTRACT_TEST"]').text()).toContain('Теневой страж')
    await wrapper.get('[data-guild-accept-contract]').trigger('click')
    expect(accept).toHaveBeenCalledWith('CONTRACT_TEST')

    await wrapper.get('[data-guild-section="world-pvp"]').trigger('click')
    expect(wrapper.find('[data-guild-accept-contract]').exists()).toBe(false)
    await wrapper.get('[data-guild-section="contracts"]').trigger('click')
    expect(wrapper.find('[data-guild-accept-contract]').exists()).toBe(true)
    wrapper.unmount()
  })

  it('uses Morvena as the only undead registrar', async () => {
    useGameSessionStore().snapshot!.character!.raceId = 'UNDEAD'
    const wrapper = openGuild()
    await flushPromises()

    expect(wrapper.get('[data-adventurer-guild-board]').text()).toContain('Морвена')
    expect(wrapper.find('.guild-board__people').exists()).toBe(false)
    wrapper.unmount()
  })
})
