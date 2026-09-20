import { describe, expect, it } from 'vitest'

import { ApiClient } from '@/api/apiClient'

describe('ApiClient legacy errorCode compatibility', () => {
  it('uses errorCode returned by spatial inventory endpoints', async () => {
    const client = new ApiClient(async () => new Response(
      JSON.stringify({ errorCode: 'inventory_full' }),
      {
        status: 409,
        headers: { 'Content-Type': 'application/json' },
      },
    ))

    await expect(client.request('/api/v1/inventory/spatial-artifact/unequip', {}, false))
      .rejects.toMatchObject({
        status: 409,
        code: 'inventory_full',
      })
  })
})