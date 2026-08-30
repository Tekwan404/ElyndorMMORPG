import { describe, expect, it, vi } from 'vitest'

import { ApiClient, ApiRequestError } from '@/api/apiClient'

function jsonResponse(body: object, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('ApiClient', () => {
  it('keeps the bearer token in memory and attaches it to requests', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse({ ok: true }))
    const client = new ApiClient(fetchMock)
    const localStorageSpy = vi.spyOn(Storage.prototype, 'setItem')

    client.setAccessToken('runtime-token')
    await client.request('/api/v1/bootstrap')

    const init = fetchMock.mock.calls[0]?.[1] as RequestInit
    expect(new Headers(init.headers).get('Authorization')).toBe('Bearer runtime-token')
    expect(localStorageSpy).not.toHaveBeenCalled()
  })

  it('reauthenticates and retries a request only once after 401', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(jsonResponse({ code: 'expired' }, 401))
      .mockResolvedValueOnce(jsonResponse({ restored: true }))
    const client = new ApiClient(fetchMock)
    const reauthenticate = vi.fn<() => Promise<string>>().mockResolvedValue('renewed-token')
    client.setReauthenticate(reauthenticate)

    await expect(client.request('/api/v1/bootstrap')).resolves.toEqual({ restored: true })
    expect(reauthenticate).toHaveBeenCalledOnce()
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('enters a terminal error when the retry is also unauthorized', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(jsonResponse({ code: 'expired' }, 401))
      .mockResolvedValueOnce(jsonResponse({ code: 'expired_again' }, 401))
    const client = new ApiClient(fetchMock)
    const reauthenticate = vi.fn<() => Promise<string>>().mockResolvedValue('renewed-token')
    client.setReauthenticate(reauthenticate)

    await expect(client.request('/api/v1/bootstrap')).rejects.toMatchObject({
      status: 401,
      code: 'expired_again',
    } satisfies Partial<ApiRequestError>)
    expect(reauthenticate).toHaveBeenCalledOnce()
  })
})
