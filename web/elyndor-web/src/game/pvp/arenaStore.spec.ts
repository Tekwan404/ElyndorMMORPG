import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { apiClient } from '@/api/apiClient'
import { useArenaStore } from './arenaStore'
import type { ArenaStatus } from './arenaContracts'

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl() { return this }
    withAutomaticReconnect() { return this }
    configureLogging() { return this }
    build() {
      return { state: 'Disconnected', on() {}, onreconnected() {}, start: async () => {}, stop: async () => {} }
    }
  },
  HubConnectionState: { Disconnected: 'Disconnected', Connected: 'Connected' },
  LogLevel: { Warning: 3, Error: 4 },
}))

const status = (patch: Partial<ArenaStatus> = {}): ArenaStatus => ({
  enabled: true, honor: 0, rating: 1000, wins: 0, losses: 0, draws: 0,
  isQueued: false, queueMode: null, queuedAtUtc: null, activeMatchId: null, ...patch,
})

describe('arenaStore', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('reports disabled arena without touching the hub', async () => {
    vi.spyOn(apiClient, 'request').mockResolvedValueOnce(status({ enabled: false }))
    const arena = useArenaStore()
    await arena.refresh()
    expect(arena.enabled).toBe(false)
    expect(arena.inMatch).toBe(false)
  })

  it('surfaces the server error code when joining the queue fails', async () => {
    const arena = useArenaStore()
    vi.spyOn(apiClient, 'request').mockRejectedValueOnce(new Error('arena_dungeon_active'))
    await arena.joinQueue('Ranked')
    expect(arena.errorCode).toBe('arena_dungeon_active')
    expect(arena.pending).toBe(false)
  })

  it('stores the queue status returned by a successful join', async () => {
    const arena = useArenaStore()
    vi.spyOn(apiClient, 'request').mockResolvedValueOnce({
      succeeded: true, errorCode: null, status: status({ isQueued: true, queueMode: 'Ranked' }),
    })
    await arena.joinQueue('Ranked')
    expect(arena.status?.isQueued).toBe(true)
    expect(arena.errorCode).toBeNull()
  })
})
