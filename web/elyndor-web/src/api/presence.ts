import { apiClient } from './apiClient'

export const PRESENCE_HEARTBEAT_INTERVAL_MS = 30_000

let heartbeatTimer: ReturnType<typeof setInterval> | null = null
let heartbeatInFlight = false

async function sendHeartbeat(): Promise<void> {
  if (!apiClient.getAccessToken() || heartbeatInFlight) return

  heartbeatInFlight = true
  try {
    await apiClient.request<void>('/api/v1/presence/heartbeat', { method: 'POST' })
  } catch {
    // Presence is best-effort and must never interrupt gameplay.
  } finally {
    heartbeatInFlight = false
  }
}

function handleVisibilityChange(): void {
  if (document.visibilityState === 'visible') {
    void sendHeartbeat()
  }
}

export function startPresenceHeartbeat(): void {
  void sendHeartbeat()

  if (heartbeatTimer !== null) return

  heartbeatTimer = setInterval(() => {
    if (document.visibilityState !== 'hidden') {
      void sendHeartbeat()
    }
  }, PRESENCE_HEARTBEAT_INTERVAL_MS)

  document.addEventListener('visibilitychange', handleVisibilityChange)
}

export function stopPresenceHeartbeat(): void {
  if (heartbeatTimer !== null) {
    clearInterval(heartbeatTimer)
    heartbeatTimer = null
  }

  document.removeEventListener('visibilitychange', handleVisibilityChange)
}
