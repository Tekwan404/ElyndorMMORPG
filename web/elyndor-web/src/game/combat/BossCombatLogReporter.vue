<script setup lang="ts">
import { watch } from 'vue'

import { apiClient } from '@/api/apiClient'
import type { CombatEvent } from '@/api/contracts'
import { isBossCombatLogEnabled } from '@/game/combat/bossCombatLogSettings'
import { useCombatSessionStore } from '@/stores/combatSession'

interface BossCombatLogResponse {
  sent: boolean
  errorCode: string | null
}

const combat = useCombatSessionStore()
const bufferedEvents = new Map<number, CombatEvent>()
const reportedSessions = new Set<string>()
const reportingSessions = new Set<string>()
let bufferedSessionId: string | null = null

function captureCurrentEvents(): void {
  const sessionId = combat.snapshot?.sessionId ?? null
  if (!sessionId) return

  if (bufferedSessionId !== sessionId) {
    bufferedSessionId = sessionId
    bufferedEvents.clear()
  }

  for (const event of combat.events) {
    bufferedEvents.set(event.sequence, event)
  }

  if (bufferedEvents.size > 1500) {
    const sequences = [...bufferedEvents.keys()].sort((left, right) => left - right)
    for (const sequence of sequences.slice(0, bufferedEvents.size - 1500)) {
      bufferedEvents.delete(sequence)
    }
  }
}

async function reportTerminalCombat(sessionId: string, attempt = 0): Promise<void> {
  if (
    reportedSessions.has(sessionId)
    || reportingSessions.has(sessionId)
    || !isBossCombatLogEnabled()
  ) return

  captureCurrentEvents()
  if (bufferedSessionId !== sessionId) return

  const events = [...bufferedEvents.values()].sort((left, right) => left.sequence - right.sequence)
  if (events.length === 0) return

  reportingSessions.add(sessionId)
  try {
    const response = await apiClient.request<BossCombatLogResponse>('/api/v1/combat/boss-log/telegram', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ sessionId, events }),
    })

    if (response.sent) {
      reportedSessions.add(sessionId)
      return
    }

    // A normal non-boss fight should never be retried or spam the endpoint.
    if (response.errorCode === 'combat_log_not_boss') {
      reportedSessions.add(sessionId)
      return
    }

    console.warn('[boss-combat-log] beta combat log was not sent', response.errorCode)
    if (attempt < 2) {
      window.setTimeout(() => void reportTerminalCombat(sessionId, attempt + 1), 500 * (attempt + 1))
    }
  } catch (error) {
    console.warn('[boss-combat-log] failed to send beta combat log', error)
    if (attempt < 2) {
      window.setTimeout(() => void reportTerminalCombat(sessionId, attempt + 1), 500 * (attempt + 1))
    }
  } finally {
    reportingSessions.delete(sessionId)
  }
}

watch(
  () => combat.events,
  () => captureCurrentEvents(),
  { immediate: true },
)

watch(
  () => combat.snapshot?.status ?? null,
  status => {
    if (!status || status === 'Active') return
    const sessionId = combat.snapshot?.sessionId
    if (!sessionId) return

    // Send immediately while the finished session is still retained by the server registry.
    captureCurrentEvents()
    void reportTerminalCombat(sessionId)
  },
)
</script>
