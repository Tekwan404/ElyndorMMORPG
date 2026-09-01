import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const signalRMock = vi.hoisted(() => ({ accessTokenFactory: null as null | (() => string | Promise<string>), calls: [] as string[] }))

vi.mock('@microsoft/signalr', () => ({
  HubConnectionState: { Disconnected: 'Disconnected', Connected: 'Connected' },
  LogLevel: { Warning: 3, Error: 4 },
  HubConnectionBuilder: class {
    withUrl(_url: string, options: { accessTokenFactory?: () => string | Promise<string> }) { signalRMock.accessTokenFactory = options.accessTokenFactory ?? null; return this }
    withAutomaticReconnect() { return this }
    configureLogging() { return this }
    build() {
      const connection = { state: 'Disconnected', on: vi.fn(), onreconnecting: vi.fn(), onreconnected: vi.fn(), onclose: vi.fn(), start: vi.fn(async () => { signalRMock.calls.push('signalr:start'); await signalRMock.accessTokenFactory?.(); connection.state = 'Connected' }), invoke: vi.fn() }
      return connection
    }
  },
}))

import { apiClient } from '@/api/apiClient'
import { useCombatSessionStore } from '@/stores/combatSession'

describe('combatSession realtime authentication', () => {
  beforeEach(() => { setActivePinia(createPinia()); signalRMock.calls.length = 0; signalRMock.accessTokenFactory = null; vi.restoreAllMocks() })

  it('ensures a fresh JWT before SignalR starts and uses the same fresh-token path for negotiate', async () => {
    const ensureFreshAccessToken = vi.spyOn(apiClient, 'ensureFreshAccessToken').mockImplementation(async () => { signalRMock.calls.push('token:fresh'); return 'fresh-token' })
    const store = useCombatSessionStore(); await store.connect()
    expect(signalRMock.calls).toEqual(['token:fresh', 'signalr:start', 'token:fresh']); expect(ensureFreshAccessToken).toHaveBeenCalledTimes(2); expect(store.connectionState).toBe('connected')
  })
})
