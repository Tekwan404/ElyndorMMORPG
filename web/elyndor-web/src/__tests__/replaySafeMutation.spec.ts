import { beforeEach, describe, expect, it, vi } from 'vitest'

import { apiClient, ApiRequestError } from '@/api/apiClient'
import {
  clearPendingGameMutation,
  hasPendingGameMutation,
  reconcilePendingGameMutation,
  runReplaySafeGameMutation,
} from '@/api/replaySafeMutation'

describe('replaySafeMutation', () => {
  beforeEach(() => {
    clearPendingGameMutation()
    vi.restoreAllMocks()
  })

  it('reuses the exact request id after an uncertain transport failure', async () => {
    const request = vi.spyOn(apiClient, 'request')
      .mockRejectedValueOnce(new TypeError('response lost'))
      .mockResolvedValueOnce({ locationId: 'WHISPERING_FOREST', version: 2 })

    const options = {
      key: 'world:travel',
      path: '/api/v1/world/travel',
      idField: 'requestId' as const,
      intent: { targetLocationId: 'WHISPERING_FOREST' },
    }

    await expect(runReplaySafeGameMutation(options)).rejects.toThrow('response lost')
    expect(hasPendingGameMutation()).toBe(true)

    await expect(runReplaySafeGameMutation(options)).resolves.toEqual({
      locationId: 'WHISPERING_FOREST',
      version: 2,
    })

    const first = bodyAt(request.mock.calls, 0)
    const second = bodyAt(request.mock.calls, 1)
    expect(first.requestId).toBe(second.requestId)
    expect(first.targetLocationId).toBe('WHISPERING_FOREST')
    expect(hasPendingGameMutation()).toBe(false)
  })

  it('reconciles an uncertain mutation before bootstrap after store reload', async () => {
    const request = vi.spyOn(apiClient, 'request')
      .mockRejectedValueOnce(new TypeError('connection reset'))
      .mockResolvedValueOnce({ locationId: 'WHISPERING_FOREST', version: 2 })

    await expect(runReplaySafeGameMutation({
      key: 'world:travel',
      path: '/api/v1/world/travel',
      idField: 'requestId',
      intent: { targetLocationId: 'WHISPERING_FOREST' },
    })).rejects.toThrow('connection reset')

    await expect(reconcilePendingGameMutation()).resolves.toBeUndefined()

    expect(bodyAt(request.mock.calls, 0).requestId).toBe(bodyAt(request.mock.calls, 1).requestId)
    expect(hasPendingGameMutation()).toBe(false)
  })

  it('clears an acknowledged rejection so a later intent gets a fresh id', async () => {
    const request = vi.spyOn(apiClient, 'request')
      .mockRejectedValueOnce(new ApiRequestError(409, 'inventory_mutation_conflict'))
      .mockResolvedValueOnce({})

    const options = {
      key: 'inventory:equip',
      path: '/api/v1/inventory/equip',
      idField: 'mutationId' as const,
      intent: { characterItemId: 'item-1' },
    }

    await expect(runReplaySafeGameMutation(options)).rejects.toMatchObject({
      code: 'inventory_mutation_conflict',
    })
    expect(hasPendingGameMutation()).toBe(false)

    await runReplaySafeGameMutation(options)

    expect(bodyAt(request.mock.calls, 0).mutationId).not.toBe(bodyAt(request.mock.calls, 1).mutationId)
  })

  it('resolves an older uncertain mutation before accepting a different mutation', async () => {
    const request = vi.spyOn(apiClient, 'request')
      .mockRejectedValueOnce(new TypeError('offline'))
      .mockResolvedValueOnce({ locationId: 'WHISPERING_FOREST', version: 2 })
      .mockResolvedValueOnce({})

    await expect(runReplaySafeGameMutation({
      key: 'world:travel',
      path: '/api/v1/world/travel',
      idField: 'requestId',
      intent: { targetLocationId: 'WHISPERING_FOREST' },
    })).rejects.toThrow('offline')

    await runReplaySafeGameMutation({
      key: 'inventory:equip',
      path: '/api/v1/inventory/equip',
      idField: 'mutationId',
      intent: { characterItemId: 'item-2' },
    })

    expect(request.mock.calls.map(([path]) => path)).toEqual([
      '/api/v1/world/travel',
      '/api/v1/world/travel',
      '/api/v1/inventory/equip',
    ])
    expect(bodyAt(request.mock.calls, 0).requestId).toBe(bodyAt(request.mock.calls, 1).requestId)
    expect(hasPendingGameMutation()).toBe(false)
  })
})

function bodyAt(
  calls: readonly (readonly unknown[])[],
  index: number,
): Record<string, unknown> {
  const init = calls[index]?.[1] as RequestInit | undefined
  expect(typeof init?.body).toBe('string')
  return JSON.parse(init?.body as string) as Record<string, unknown>
}
