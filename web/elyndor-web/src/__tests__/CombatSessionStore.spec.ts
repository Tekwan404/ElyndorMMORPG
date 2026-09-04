import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const signalRMock = vi.hoisted(() => ({
  accessTokenFactory: null as null | (() => string | Promise<string>),
  calls: [] as string[],
  transport: null as number | null,
  startError: null as Error | null,
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
        invoke: vi.fn<(...args: unknown[]) => Promise<unknown>>(),
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
