import { afterEach, describe, expect, it, vi } from 'vitest'
import { adminRequest, AdminApiError, setAdminAccessToken } from '../api'

describe('admin API session safety', () => {
  afterEach(() => {
    setAdminAccessToken(null)
    vi.unstubAllGlobals()
  })

  it('clears expired credentials and signals the shell when a protected API returns 401', async () => {
    const fetchMock = vi.fn<typeof fetch>()
      .mockResolvedValueOnce(new Response(
        JSON.stringify({ code: 'auth_token_expired' }), { status: 401 },
      ))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ok: true }), { status: 200 }))
    vi.stubGlobal('fetch', fetchMock)
    const sessionExpired = vi.fn<(event: Event) => void>()
    window.addEventListener('elyndor-admin-session-expired', sessionExpired)

    try {
      setAdminAccessToken('test-jwt')
      await expect(adminRequest('/api/v1/admin/content/current')).rejects.toBeInstanceOf(AdminApiError)
      expect(sessionExpired).toHaveBeenCalledTimes(1)
      const firstCall = fetchMock.mock.calls[0]
      if (!firstCall) throw new Error('Expected first request')
      const firstHeaders = new Headers(firstCall[1]?.headers)
      expect(firstHeaders.get('Authorization')).toBe('Bearer test-jwt')

      await adminRequest('/api/v1/status')
      const secondCall = fetchMock.mock.calls[1]
      if (!secondCall) throw new Error('Expected second request')
      const secondHeaders = new Headers(secondCall[1]?.headers)
      expect(secondHeaders.has('Authorization')).toBe(false)
    } finally {
      window.removeEventListener('elyndor-admin-session-expired', sessionExpired)
    }
  })
})
