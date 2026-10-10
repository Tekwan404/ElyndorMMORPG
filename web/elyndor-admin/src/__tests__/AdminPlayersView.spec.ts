import { mount, flushPromises } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import AdminPlayersView from '../admin/AdminPlayersView.vue'

describe('admin player inspector', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('looks up a player and hands off their Telegram ID to GM Forge', async () => {
    const mockFetch = vi.fn<typeof fetch>().mockImplementation(async (input) =>
      new Response(JSON.stringify(String(input).includes('/players?')
        ? { page: 1, total: 1, pageSize: 50, players: [
            { telegramUserId: 123, telegramUsername: 'tester', character: { name: 'DevWarrior', level: 60, classId: 'WARRIOR' } },
          ] }
        : {
        telegramUserId: 123,
        telegramUsername: 'tester',
        createdAtUtc: '2026-10-01T12:00:00Z',
        lastSeenAtUtc: '2026-10-09T12:00:00Z',
        character: {
          id: 'char', name: 'DevWarrior', classId: 'WARRIOR', raceId: 'HUMAN',
          level: 60, experience: 1000, gold: 500,
          locationId: 'STARTER_TOWN', currentHp: 100, currentResource: 50,
          inventoryCount: 12, equippedCount: 8, gmItemsCount: 2,
        },
      }), { status: 200 }),
    )
    vi.stubGlobal('fetch', mockFetch)

    const wrapper = mount(AdminPlayersView)
    await wrapper.find('#player-telegram-id').setValue('123')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(mockFetch).toHaveBeenCalledWith('/api/v1/admin/players/123', expect.any(Object))
    expect(wrapper.text()).toContain('DevWarrior')
    expect(wrapper.text()).toContain('GM-предметов')
    const openForge = wrapper.findAll('button').find(button => button.text().includes('Выдать предметы'))
    await openForge!.trigger('click')
    expect(wrapper.emitted('forge-target')?.[0]).toEqual(['123'])
    wrapper.unmount()
  })

  it('rejects invalid identifiers without making a request', async () => {
    const mockFetch = vi.fn<typeof fetch>()
    vi.stubGlobal('fetch', mockFetch)
    const wrapper = mount(AdminPlayersView)
    await wrapper.find('#player-telegram-id').setValue('invalid')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.text()).toContain('корректный числовой Telegram ID')
    expect(mockFetch.mock.calls.every(call => String(call[0]).includes('/players?'))).toBe(true)
    wrapper.unmount()
  })
})
