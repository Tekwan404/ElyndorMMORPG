import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { apiClient } from '@/api/apiClient'
import { useGameSessionStore } from '@/stores/gameSession'

vi.mock('@/telegram/telegramWebApp', () => ({ getTelegramInitData: vi.fn<() => string | null>(() => 'signed-init-data') }))

describe('gameSession', () => {
  beforeEach(() => { setActivePinia(createPinia()); vi.restoreAllMocks() })

  it('authenticates with Telegram and enters character creation from bootstrap', async () => {
    const request = vi.spyOn(apiClient, 'request')
    request.mockResolvedValueOnce({ accessToken: 'token', expiresAtUtc: '2026-08-30T00:15:00Z', roles: [] }).mockResolvedValueOnce({ accountId: crypto.randomUUID(), character: null, world: null, contentVersion: '0.1.0', balanceVersion: '0.1.0', serverTimeUtc: '2026-08-30T00:00:00Z' })
    const store = useGameSessionStore()
    await store.start()
    expect(request.mock.calls[0]?.[0]).toBe('/api/v1/auth/telegram')
    expect(store.state).toBe('needs-character')
  })

  it('exposes the server-issued admin role', async () => {
    vi.spyOn(apiClient, 'request').mockResolvedValue({
      accessToken: 'admin-token',
      expiresAtUtc: '2026-08-30T00:15:00Z',
      roles: ['SUPER_ADMIN'],
    })
    const store = useGameSessionStore()
    await store.authenticate()
    expect(store.isAdmin).toBe(true)
    expect(store.roles).toEqual(['SUPER_ADMIN'])
  })

  it('reports an offline state without inventing a snapshot', async () => {
    vi.spyOn(apiClient, 'request').mockRejectedValue(new TypeError('offline'))
    const store = useGameSessionStore()
    await store.start()
    expect(store.state).toBe('offline')
    expect(store.snapshot).toBeNull()
    expect(store.errorCode).toBe('network_unavailable')
  })

  it('prevents concurrent mutations', async () => {
    let resolveRequest: ((value: unknown) => void) | undefined
    vi.spyOn(apiClient, 'request').mockImplementation(() => new Promise((resolve) => (resolveRequest = resolve)))
    const store = useGameSessionStore()
    const first = store.travel('WHISPERING_FOREST')
    const second = store.travel('DEEP_FOREST')
    expect(store.mutationPending).toBe(true)
    resolveRequest?.({ locationId: 'WHISPERING_FOREST', version: 2 })
    await Promise.resolve()
    resolveRequest?.({ accountId: crypto.randomUUID(), character: null, world: null, contentVersion: '0.1.0', balanceVersion: '0.1.0', serverTimeUtc: '2026-08-30T00:00:00Z' })
    await Promise.all([first, second])
  })

  it('restores the stable world state after a transparent token refresh', async () => {
    const request = vi.spyOn(apiClient, 'request').mockResolvedValue({ accessToken: 'renewed-token', expiresAtUtc: '2026-09-01T19:15:00Z', roles: [] })
    const store = useGameSessionStore()
    store.state = 'world'
    await expect(store.authenticate(true)).resolves.toBe('renewed-token')
    expect(request).toHaveBeenCalledWith('/api/v1/auth/telegram', expect.any(Object), false)
    expect(store.state).toBe('world')
  })
})
