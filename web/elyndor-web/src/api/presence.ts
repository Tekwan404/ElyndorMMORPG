import { apiClient } from './apiClient'

export const PRESENCE_HEARTBEAT_INTERVAL_MS = 30_000

let heartbeatTimer: ReturnType<typeof setInterval> | null = null
let heartbeatInFlight = false

async function sendHeartbeat(): Promise<void> {
  if (!apiClient.getAccessToken() || heartbeatInFlight) return

  heartbeatInFlight = true
  try {
    await apiClient.request<void>(
      '/api/v1/presence/heartbeat',
      { method: 'POST' },
      false,
    )
  } catch {
    // Presence is best-effort and must never interrupt gameplay or trigger auth recovery.
  } finally {
    heartbeatInFlight = false
  }
}

function sendHeartbeatIfVisible(): void {
  if (document.visibilityState === 'visible') {
    void sendHeartbeat()
  }
}

function handleVisibilityChange(): void {
  sendHeartbeatIfVisible()
}

function handleResume(): void {
  sendHeartbeatIfVisible()
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
  window.addEventListener('pageshow', handleResume)
  window.addEventListener('online', handleResume)
}

export function stopPresenceHeartbeat(): void {
  if (heartbeatTimer !== null) {
    clearInterval(heartbeatTimer)
    heartbeatTimer = null
  }

  document.removeEventListener('visibilitychange', handleVisibilityChange)
  window.removeEventListener('pageshow', handleResume)
  window.removeEventListener('online', handleResume)
}
