import { afterEach, describe, expect, it, vi } from 'vitest'
import { adminRequest, AdminApiError, setAdminAccessToken } from '../api'

describe('admin API session safety', () => {
  afterEach(() => {
    setAdminAccessToken(null)
    vi.unstubAllGlobals()
  })

  it('clears expired credentials and signals the shell when a protected API returns 401', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(
        JSON.stringify({ code: 'auth_token_expired' }), { status: 401 },
      ))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ok: true }), { status: 200 }))
    vi.stubGlobal('fetch', fetchMock)
    const sessionExpired = vi.fn()
    window.addEventListener('elyndor-admin-session-expired', sessionExpired)

    try {
      setAdminAccessToken('test-jwt')
      await expect(adminRequest('/api/v1/admin/content/current')).rejects.toBeInstanceOf(AdminApiError)
      expect(sessionExpired).toHaveBeenCalledTimes(1)
      const firstHeaders = new Headers((fetchMock.mock.calls[0]?.[1] as RequestInit).headers)
      expect(firstHeaders.get('Authorization')).toBe('Bearer test-jwt')

      await adminRequest('/api/v1/status')
      const secondHeaders = new Headers((fetchMock.mock.calls[1]?.[1] as RequestInit).headers)
      expect(secondHeaders.has('Authorization')).toBe(false)
    } finally {
      window.removeEventListener('elyndor-admin-session-expired', sessionExpired)
    }
  })
})
