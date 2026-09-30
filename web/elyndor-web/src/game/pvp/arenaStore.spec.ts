import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { apiClient } from '@/api/apiClient'
import { useArenaStore } from './arenaStore'
import type { ArenaMatch, ArenaStatus } from './arenaContracts'

const signalR = vi.hoisted(() => ({
  start: vi.fn<() => void>(),
  stop: vi.fn<() => void>(),
  invoke: vi.fn<(...args: unknown[]) => Promise<unknown>>(async () => null),
}))

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl() { return this }
    withAutomaticReconnect() { return this }
    configureLogging() { return this }
    build() {
      const connection = {
        state: 'Disconnected',
        on() {},
        onreconnected() {},
        start: async () => {
          signalR.start()
          connection.state = 'Connected'
        },
        stop: async () => {
          signalR.stop()
          connection.state = 'Disconnected'
        },
        invoke: signalR.invoke,
      }
      return connection
    }
  },
  HubConnectionState: { Disconnected: 'Disconnected', Connected: 'Connected' },
  LogLevel: { Warning: 3, Error: 4 },
}))

const status = (patch: Partial<ArenaStatus> = {}): ArenaStatus => ({
  enabled: true, honor: 0, rating: 1000, wins: 0, losses: 0, draws: 0,
  isQueued: false, queueMode: null, queuedAtUtc: null, activeMatchId: null, ...patch,
})

const match = (patch: Partial<ArenaMatch> = {}): ArenaMatch => ({
  status: 'Active',
  matchId: 'match-1',
  characterId: 'character-a',
  opponentCharacterId: 'character-b',
  opponentName: 'Opponent',
  outcome: 'Active',
  result: 'Active',
  sequence: 1,
  battle: null,
  events: [],
  ...patch,
})

describe('arenaStore', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    signalR.start.mockClear()
    signalR.stop.mockClear()
    signalR.invoke.mockClear()
    signalR.invoke.mockResolvedValue(null)
    setActivePinia(createPinia())
  })

  it('reports disabled arena without touching the hub', async () => {
    vi.spyOn(apiClient, 'request').mockResolvedValueOnce(status({ enabled: false }))
    const arena = useArenaStore()
    await arena.refresh()
    expect(arena.enabled).toBe(false)
    expect(arena.inMatch).toBe(false)
    expect(signalR.start).not.toHaveBeenCalled()
  })

  it('reconnects the hub when refresh restores a queued status', async () => {
    vi.spyOn(apiClient, 'request').mockResolvedValueOnce(status({ isQueued: true, activeMatchId: null }))
    const arena = useArenaStore()

    await arena.refresh()

    expect(signalR.start).toHaveBeenCalledTimes(1)
    expect(signalR.invoke).not.toHaveBeenCalled()
  })

  it('reconnects the hub and loads an active match', async () => {
    vi.spyOn(apiClient, 'request').mockResolvedValueOnce(status({ activeMatchId: 'match-1' }))
    const arena = useArenaStore()

    await arena.refresh()

    expect(signalR.start).toHaveBeenCalledTimes(1)
    expect(signalR.invoke).toHaveBeenCalledWith('GetMatch', 'match-1', 0)
  })

  it('does not connect an idle enabled arena status', async () => {
    vi.spyOn(apiClient, 'request').mockResolvedValueOnce(status())
    const arena = useArenaStore()

    await arena.refresh()

    expect(signalR.start).not.toHaveBeenCalled()
  })

  it('keeps queued reload presence connected across subsequent refreshes', async () => {
    vi.spyOn(apiClient, 'request')
      .mockResolvedValueOnce(status({ isQueued: true }))
      .mockResolvedValueOnce(status({ isQueued: true }))
    const arena = useArenaStore()

    await arena.refresh()
    await arena.refresh()

    expect(signalR.start).toHaveBeenCalledTimes(1)
    expect(signalR.stop).not.toHaveBeenCalled()
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

  it('refreshes rating, honor and leaderboard immediately when a command completes the match', async () => {
    const request = vi.spyOn(apiClient, 'request')
      .mockResolvedValueOnce(status({ activeMatchId: 'match-1', rating: 1000, honor: 20 }))
      .mockResolvedValueOnce(status({ rating: 984, honor: 25, losses: 1 }))
      .mockResolvedValueOnce([])

    signalR.invoke
      .mockResolvedValueOnce(match())
      .mockResolvedValueOnce({
        succeeded: true,
        errorCode: null,
        match: match({ status: 'Completed', outcome: 'WinnerB', result: 'Defeat', sequence: 2 }),
      })

    const arena = useArenaStore()
    await arena.refresh()
    await arena.surrender()

    expect(arena.match?.status).toBe('Completed')
    expect(arena.status?.rating).toBe(984)
    expect(arena.status?.honor).toBe(25)
    expect(arena.status?.losses).toBe(1)
    expect(arena.ratingDelta).toBe(-16)
    expect(arena.honorDelta).toBe(5)
    expect(request).toHaveBeenCalledWith('/api/v1/arena/leaderboard')
  })

  it('queues the next ranked opponent directly from a completed result', async () => {
    const arena = useArenaStore()
    arena.status = status({ rating: 1016, honor: 8 })
    arena.match = match({ status: 'Completed', outcome: 'WinnerA', result: 'Victory' })
    vi.spyOn(apiClient, 'request').mockResolvedValueOnce({
      succeeded: true,
      errorCode: null,
      status: status({ rating: 1016, honor: 8, isQueued: true, queueMode: 'Ranked' }),
    })

    await arena.findNextOpponent('Ranked')

    expect(arena.match).toBeNull()
    expect(arena.status?.isQueued).toBe(true)
    expect(arena.status?.queueMode).toBe('Ranked')
    expect(signalR.start).toHaveBeenCalledTimes(1)
  })
})
