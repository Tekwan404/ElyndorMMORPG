import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { apiClient } from '@/api/apiClient'
import {
  PRESENCE_HEARTBEAT_INTERVAL_MS,
  startPresenceHeartbeat,
  stopPresenceHeartbeat,
} from '@/api/presence'

describe('presence heartbeat', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    stopPresenceHeartbeat()
    Object.defineProperty(document, 'visibilityState', {
      configurable: true,
      value: 'visible',
    })
  })

  afterEach(() => {
    stopPresenceHeartbeat()
    vi.restoreAllMocks()
    vi.useRealTimers()
  })

  it('sends immediately and then on the heartbeat interval', async () => {
    vi.spyOn(apiClient, 'getAccessToken').mockReturnValue('token')
    const request = vi.spyOn(apiClient, 'request').mockResolvedValue(undefined)

    startPresenceHeartbeat()

    expect(request).toHaveBeenCalledTimes(1)
    expect(request).toHaveBeenLastCalledWith(
      '/api/v1/presence/heartbeat',
      { method: 'POST' },
      false,
    )

    await vi.advanceTimersByTimeAsync(PRESENCE_HEARTBEAT_INTERVAL_MS)

    expect(request).toHaveBeenCalledTimes(2)
  })

  it('does not send while hidden and resumes immediately when visible', async () => {
    vi.spyOn(apiClient, 'getAccessToken').mockReturnValue('token')
    const request = vi.spyOn(apiClient, 'request').mockResolvedValue(undefined)

    startPresenceHeartbeat()
    Object.defineProperty(document, 'visibilityState', {
      configurable: true,
      value: 'hidden',
    })

    await vi.advanceTimersByTimeAsync(PRESENCE_HEARTBEAT_INTERVAL_MS * 3)
    expect(request).toHaveBeenCalledTimes(1)

    Object.defineProperty(document, 'visibilityState', {
      configurable: true,
      value: 'visible',
    })
    document.dispatchEvent(new Event('visibilitychange'))

    expect(request).toHaveBeenCalledTimes(2)
  })

  it('refreshes presence after browser restore and network recovery', () => {
    vi.spyOn(apiClient, 'getAccessToken').mockReturnValue('token')
    const request = vi.spyOn(apiClient, 'request').mockResolvedValue(undefined)

    startPresenceHeartbeat()
    window.dispatchEvent(new Event('pageshow'))
    window.dispatchEvent(new Event('online'))

    expect(request).toHaveBeenCalledTimes(3)
  })

  it('does nothing without an authenticated account', async () => {
    vi.spyOn(apiClient, 'getAccessToken').mockReturnValue(null)
    const request = vi.spyOn(apiClient, 'request').mockResolvedValue(undefined)

    startPresenceHeartbeat()
    await vi.advanceTimersByTimeAsync(PRESENCE_HEARTBEAT_INTERVAL_MS * 2)

    expect(request).not.toHaveBeenCalled()
  })

  it('prevents overlapping heartbeat requests', async () => {
    vi.spyOn(apiClient, 'getAccessToken').mockReturnValue('token')
    let resolveRequest!: () => void
    const pending = new Promise<void>((resolve) => {
      resolveRequest = resolve
    })
    const request = vi.spyOn(apiClient, 'request').mockReturnValue(pending)

    startPresenceHeartbeat()
    await vi.advanceTimersByTimeAsync(PRESENCE_HEARTBEAT_INTERVAL_MS * 2)

    expect(request).toHaveBeenCalledTimes(1)

    resolveRequest()
    await pending
    await vi.advanceTimersByTimeAsync(PRESENCE_HEARTBEAT_INTERVAL_MS)

    expect(request).toHaveBeenCalledTimes(2)
  })
})
