<script setup lang="ts">
import { nextTick, watch } from 'vue'

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

async function reportTerminalCombat(sessionId: string): Promise<void> {
  if (reportedSessions.has(sessionId) || !isBossCombatLogEnabled()) return

  reportedSessions.add(sessionId)
  captureCurrentEvents()
  const events = [...bufferedEvents.values()].sort((left, right) => left.sequence - right.sequence)
  if (events.length === 0) return

  try {
    await apiClient.request<BossCombatLogResponse>('/api/v1/combat/boss-log/telegram', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ sessionId, events }),
    })
  } catch (error) {
    console.warn('[boss-combat-log] failed to send beta combat log', error)
  }
}

watch(
  () => combat.events,
  () => captureCurrentEvents(),
  { immediate: true },
)

watch(
  () => combat.snapshot?.status ?? null,
  async status => {
    if (!status || status === 'Active') return
    const sessionId = combat.snapshot?.sessionId
    if (!sessionId) return
    await nextTick()
    captureCurrentEvents()
    await reportTerminalCombat(sessionId)
  },
)
</script>
