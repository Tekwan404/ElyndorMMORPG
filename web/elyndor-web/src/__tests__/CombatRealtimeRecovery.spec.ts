import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const realtimeMock = vi.hoisted(() => ({
  reconnecting: null as ((error?: Error) => void) | null,
  reconnected: null as (() => void) | null,
  closed: null as ((error?: Error) => void) | null,
  handlers: new Map<string, (payload: unknown) => void>(),
  invoke: vi.fn<(method: string, ...args: unknown[]) => Promise<unknown>>(),
}))

const partyRefresh = vi.hoisted(() => vi.fn<() => Promise<void>>())
const dungeonRefresh = vi.hoisted(() => vi.fn<() => Promise<void>>())

vi.mock('@/game/party/partyStore', () => ({
  usePartyStore: () => ({ refresh: partyRefresh }),
}))

vi.mock('@/game/party/dungeonStore', () => ({
  useDungeonStore: () => ({ refresh: dungeonRefresh }),
}))

vi.mock('@microsoft/signalr', () => ({
  HubConnectionState: { Disconnected: 'Disconnected', Connected: 'Connected' },
  LogLevel: { Warning: 3, Error: 4 },
  HubConnectionBuilder: class {
    withUrl() { return this }
    withAutomaticReconnect() { return this }
    configureLogging() { return this }
    build() {
      const connection = {
        state: 'Disconnected',
        on: vi.fn<(event: string, callback: (payload: unknown) => void) => void>((event, callback) => {
          realtimeMock.handlers.set(event, callback)
        }),
        onreconnecting: vi.fn<(callback: (error?: Error) => void) => void>((callback) => {
          realtimeMock.reconnecting = callback
        }),
        onreconnected: vi.fn<(callback: () => void) => void>((callback) => {
          realtimeMock.reconnected = callback
        }),
        onclose: vi.fn<(callback: (error?: Error) => void) => void>((callback) => {
          realtimeMock.closed = callback
        }),
        start: vi.fn<() => Promise<void>>(async () => {
          connection.state = 'Connected'
        }),
        invoke: realtimeMock.invoke,
      }
      return connection
    }
  },
}))

import { apiClient } from '@/api/apiClient'
import { useCombatSessionStore } from '@/stores/combatSession'

describe('combat realtime recovery', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    realtimeMock.reconnecting = null
    realtimeMock.reconnected = null
    realtimeMock.closed = null
    realtimeMock.handlers.clear()
    realtimeMock.invoke.mockReset()
    partyRefresh.mockReset().mockResolvedValue(undefined)
    dungeonRefresh.mockReset().mockResolvedValue(undefined)
    vi.restoreAllMocks()
    vi.spyOn(apiClient, 'ensureFreshAccessToken').mockResolvedValue('fresh-token')
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
  })

  it('moves through reconnecting and syncing before restoring authoritative state', async () => {
    realtimeMock.invoke.mockImplementation(async (method) => {
      if (method === 'ResumeCombat') {
        return {
          succeeded: false,
          errorCode: 'combat_not_found',
          snapshot: null,
          events: [],
          reward: null,
        }
      }
      if (method === 'Ping') return 'pong'
      return null
    })

    const store = useCombatSessionStore()
    await store.connect()

    realtimeMock.reconnecting?.(new Error('temporary network loss'))
    expect(store.connectionState).toBe('reconnecting')
    expect(store.reconnectCount).toBe(1)

    realtimeMock.reconnected?.()
    expect(store.connectionState).toBe('syncing')

    await vi.waitFor(() => {
      expect(store.connectionState).toBe('connected')
    })

    expect(store.lastResyncedAtUtc).not.toBeNull()
    expect(realtimeMock.invoke).toHaveBeenCalledWith('ResumeCombat')
    expect(partyRefresh).toHaveBeenCalledTimes(1)
    expect(dungeonRefresh).toHaveBeenCalledTimes(1)
    expect(store.diagnostic).toBeNull()
  })

  it('ignores an older response from the same combat session including its events and reward', async () => {
    const store = useCombatSessionStore()
    await store.connect()

    const sessionId = '00000000-0000-0000-0000-000000000501'
    const playerId = '00000000-0000-0000-0000-000000000502'
    const enemyId = '00000000-0000-0000-0000-000000000503'
    store.snapshot = {
      sessionId,
      sequence: 10,
      status: 'Active',
      serverTimeUtc: '2026-09-12T10:00:10Z',
      contentVersion: '1',
      balanceVersion: '1',
      player: { actorId: playerId, abilities: [], cooldowns: {} },
      enemy: { actorId: enemyId, definitionId: 'BOSS' },
    } as never

    const handler = realtimeMock.handlers.get('CombatUpdated')
    expect(handler).toBeDefined()

    handler?.({
      succeeded: true,
      errorCode: null,
      snapshot: {
        ...store.snapshot,
        sequence: 9,
        serverTimeUtc: '2026-09-12T10:00:09Z',
      },
      events: [{
        sequence: 99,
        type: 'DamageDealt',
        actorId: playerId,
        sourceActorId: playerId,
        targetActorId: enemyId,
        definitionId: null,
        amount: 999,
        amountBeforeShields: 999,
        serverTimeUtc: '2026-09-12T10:00:09Z',
      }],
      reward: {
        xpEarned: 999,
        goldEarned: 999,
        leveledUp: false,
        previousLevel: 1,
        currentLevel: 1,
        items: [],
      },
    })

    expect(store.snapshot?.sequence).toBe(10)
    expect(store.events).toEqual([])
    expect(store.reward).toBeNull()
  })
})
