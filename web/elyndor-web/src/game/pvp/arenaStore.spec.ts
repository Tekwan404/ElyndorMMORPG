import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { apiClient } from '@/api/apiClient'
import { useArenaStore } from './arenaStore'
import type { ArenaMatch, ArenaStatus } from './arenaContracts'
import type { CombatSnapshot } from '@/api/contracts'

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

  it('retries an uncertain invite with the same request id and clears its own pending state', async () => {
    const request = vi.spyOn(apiClient, 'request')
      .mockRejectedValueOnce(new Error('network_unavailable'))
      .mockResolvedValueOnce({ id: 'invite' })
      .mockResolvedValueOnce([])
    const arena = useArenaStore()
    await arena.invitePlayer(' Friend ')
    expect(arena.invitePending).toBe(false)
    expect(arena.errorCode).toBe('network_unavailable')
    await arena.invitePlayer('Friend')
    const first = JSON.parse(String(request.mock.calls[0]![1]!.body)) as { requestId: string; targetName: string }
    const retry = JSON.parse(String(request.mock.calls[1]![1]!.body)) as { requestId: string; targetName: string }
    expect(retry.requestId).toBe(first.requestId)
    expect(retry.targetName).toBe('Friend')
    expect(arena.inviteNotice).toContain('отправлено')
    expect(arena.pending).toBe(false)
  })

  it('accepts the invitation and restores the existing authoritative match', async () => {
    vi.spyOn(apiClient, 'request')
      .mockResolvedValueOnce({ matchId: 'match-1' })
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce(status({ activeMatchId: 'match-1' }))
    signalR.invoke.mockResolvedValue(match())
    const arena = useArenaStore()
    await arena.respondToInvite('invite', 'accept')
    expect(arena.match?.matchId).toBe('match-1')
    expect(arena.invitations).toEqual([])
    expect(arena.invitePending).toBe(false)
    expect(signalR.start).toHaveBeenCalledTimes(1)
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

  it('sends autoattack intent and applies the authoritative response', async () => {
    const arena = useArenaStore()
    await arena.connect()
    const battle = { player: { autoAttackEnabled: true } } as CombatSnapshot
    arena.match = match({ battle })
    signalR.invoke.mockResolvedValueOnce({ succeeded: true, errorCode: null,
      match: match({ battle: { player: { autoAttackEnabled: false } } as CombatSnapshot, sequence: 2 }) })
    await arena.toggleAutoAttack()
    expect(signalR.invoke).toHaveBeenCalledWith('SetAutoAttack', 'match-1', false, expect.any(String))
    expect(arena.match?.battle?.player.autoAttackEnabled).toBe(false)
    expect(arena.autoAttackPending).toBe(false)
  })

  it('loads Honor shop offers and keeps the arena Honor balance synchronized', async () => {
    vi.spyOn(apiClient, 'request').mockResolvedValueOnce({
      honor: 140,
      items: [{
        itemId: 'L60_PVP_T1_WARRIOR_GUARDIAN_HEAD',
        name: 'Шлем «Оплот Железного Круга»',
        rarity: 'Legendary',
        slot: 'Head',
        iconId: 'sets/set_heart_of_blighted_grove_warrior_guardian_head',
        setId: 'SET_L60_PVP_T1_WARRIOR_GUARDIAN',
        requiredLevel: 60,
        honorPrice: 80,
      }],
    })
    const arena = useArenaStore()
    arena.status = status({ honor: 0 })

    await arena.loadShop()

    expect(arena.shop?.honor).toBe(140)
    expect(arena.shop?.items).toHaveLength(1)
    expect(arena.status?.honor).toBe(140)
    expect(arena.shopPending).toBe(false)
    expect(arena.shopErrorCode).toBeNull()
  })

  it('retries an uncertain Honor purchase with the same mutation id', async () => {
    const request = vi.spyOn(apiClient, 'request')
      .mockRejectedValueOnce(new Error('network_unavailable'))
      .mockResolvedValueOnce({
        succeeded: true,
        errorCode: null,
        shop: { honor: 40, items: [] },
      })
    const arena = useArenaStore()
    arena.status = status({ honor: 100 })

    await arena.buyHonorItem('L60_PVP_T1_WARRIOR_GUARDIAN_HANDS')
    await arena.buyHonorItem('L60_PVP_T1_WARRIOR_GUARDIAN_HANDS')

    const first = JSON.parse(String(request.mock.calls[0]![1]!.body)) as { itemId: string; mutationId: string }
    const retry = JSON.parse(String(request.mock.calls[1]![1]!.body)) as { itemId: string; mutationId: string }

    expect(retry.itemId).toBe(first.itemId)
    expect(retry.mutationId).toBe(first.mutationId)
    expect(arena.shop?.honor).toBe(40)
    expect(arena.status?.honor).toBe(40)
    expect(arena.shopPurchasePendingId).toBeNull()
    expect(arena.shopErrorCode).toBeNull()
    expect(request).toHaveBeenCalledWith('/api/v1/bootstrap')
  })

})
