import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const signalRMock = vi.hoisted(() => ({
  accessTokenFactory: null as null | (() => string | Promise<string>),
  calls: [] as string[],
  transport: null as number | null,
  startError: null as Error | null,
  invoke: vi.fn<(...args: unknown[]) => Promise<unknown>>(),
}))

vi.mock('@microsoft/signalr', () => ({
  HubConnectionState: { Disconnected: 'Disconnected', Connected: 'Connected' },
  HttpTransportType: { LongPolling: 4 },
  LogLevel: { Warning: 3, Error: 4 },
  HubConnectionBuilder: class {
    withUrl(
      _url: string,
      options: { accessTokenFactory?: () => string | Promise<string>; transport?: number },
    ) {
      signalRMock.accessTokenFactory = options.accessTokenFactory ?? null
      signalRMock.transport = options.transport ?? null
      return this
    }
    withAutomaticReconnect() { return this }
    configureLogging() { return this }
    build() {
      const connection = {
        state: 'Disconnected',
        on: vi.fn<(...args: unknown[]) => void>(),
        onreconnecting: vi.fn<(...args: unknown[]) => void>(),
        onreconnected: vi.fn<(...args: unknown[]) => void>(),
        onclose: vi.fn<(...args: unknown[]) => void>(),
        start: vi.fn<() => Promise<void>>(async () => {
          signalRMock.calls.push('signalr:start')
          await signalRMock.accessTokenFactory?.()
          if (signalRMock.startError) throw signalRMock.startError
          connection.state = 'Connected'
        }),
        invoke: signalRMock.invoke,
      }
      return connection
    }
  },
}))

import { apiClient } from '@/api/apiClient'
import { useCombatSessionStore } from '@/stores/combatSession'

describe('combatSession realtime authentication', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    signalRMock.calls.length = 0
    signalRMock.accessTokenFactory = null
    signalRMock.transport = null
    signalRMock.startError = null
    signalRMock.invoke.mockReset()
    vi.restoreAllMocks()
  })

  it('refreshes JWT and uses Funnel-safe SignalR long polling', async () => {
    const ensureFreshAccessToken = vi
      .spyOn(apiClient, 'ensureFreshAccessToken')
      .mockImplementation(async () => {
        signalRMock.calls.push('token:fresh')
        return 'fresh-token'
      })

    const store = useCombatSessionStore()
    await store.connect()

    expect(signalRMock.calls).toEqual(['token:fresh', 'signalr:start', 'token:fresh'])
    expect(ensureFreshAccessToken).toHaveBeenCalledTimes(2)
    expect(signalRMock.transport).toBe(4)
    expect(store.connectionState).toBe('connected')
    expect(store.diagnostic).toBeNull()
  })

  it('reuses the same command id after a lost ability response and rotates it after acknowledgement', async () => {
    vi.spyOn(apiClient, 'ensureFreshAccessToken').mockResolvedValue('fresh-token')
    signalRMock.invoke
      .mockResolvedValueOnce({
        succeeded: true,
        errorCode: null,
        snapshot: {
          sessionId: '00000000-0000-0000-0000-000000000101',
          status: 'Active',
          sequence: 1,
          serverTimeUtc: '2026-09-05T18:00:00Z',
          player: {
            actorId: '00000000-0000-0000-0000-000000000201',
            autoAttackEnabled: false,
          },
          enemy: {
            actorId: '00000000-0000-0000-0000-000000000301',
            definitionId: 'WOLF',
          },
        },
        events: [],
        reward: null,
      })
      .mockRejectedValueOnce(new Error('response lost after server accepted command'))
      .mockResolvedValueOnce({
        succeeded: false,
        errorCode: 'combat_duplicate_command',
      })
      .mockResolvedValueOnce({
        succeeded: false,
        errorCode: 'combat_ability_on_cooldown',
      })

    const store = useCombatSessionStore()
    expect(await store.startTraining()).toBe(true)

    await store.useAbility('HEROIC_STRIKE')
    await store.useAbility('HEROIC_STRIKE')
    await store.useAbility('HEROIC_STRIKE')

    const abilityCalls = signalRMock.invoke.mock.calls.filter(([method]) => method === 'UseAbility')
    expect(abilityCalls).toHaveLength(3)
    expect(abilityCalls[0]?.[3]).toBe(abilityCalls[1]?.[3])
    expect(abilityCalls[2]?.[3]).not.toBe(abilityCalls[1]?.[3])
  })

  it('keeps the exact SignalR start stage when negotiate/transport fails', async () => {
    vi.spyOn(apiClient, 'ensureFreshAccessToken').mockResolvedValue('fresh-token')
    signalRMock.startError = new Error('Failed to complete negotiation with the server')

    const store = useCombatSessionStore()

    await expect(store.connect()).rejects.toThrow('Failed to complete negotiation')
    expect(store.connectionState).toBe('disconnected')
    expect(store.errorCode).toBe('combat_negotiate_failed')
    expect(store.diagnostic).toMatchObject({
      stage: 'signalr_start',
      operation: 'connect',
      code: 'combat_negotiate_failed',
    })
  })
})
