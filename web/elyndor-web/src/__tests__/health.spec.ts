import { afterEach, describe, expect, it, vi } from 'vitest'

import { isApiHealthy } from '@/api/health'

describe('isApiHealthy', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('returns true for a successful server response', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true }))

    await expect(isApiHealthy()).resolves.toBe(true)
  })

  it('returns false when the server cannot be reached', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Network error')))

    await expect(isApiHealthy()).resolves.toBe(false)
  })
})
