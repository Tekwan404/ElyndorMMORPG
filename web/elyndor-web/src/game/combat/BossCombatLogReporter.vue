<script setup lang="ts">
import { watch } from 'vue'

import { apiClient } from '@/api/apiClient'
import { isBossCombatLogEnabled } from '@/game/combat/bossCombatLogSettings'
import { useCombatSessionStore } from '@/stores/combatSession'

interface BossCombatLogResponse {
  sent: boolean
  errorCode: string | null
}

const combat = useCombatSessionStore()
const reportedSessions = new Set<string>()
const reportingSessions = new Set<string>()

async function reportTerminalCombat(sessionId: string, attempt = 0): Promise<void> {
  if (
    reportedSessions.has(sessionId)
    || reportingSessions.has(sessionId)
    || !isBossCombatLogEnabled()
  ) return

  reportingSessions.add(sessionId)
  try {
    const response = await apiClient.request<BossCombatLogResponse>('/api/v1/combat/boss-log/telegram-v2', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ sessionId }),
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
  () => combat.snapshot?.status ?? null,
  status => {
    if (!status || status === 'Active') return
    const sessionId = combat.snapshot?.sessionId
    if (!sessionId) return

    // The server archives combat updates before publishing the terminal SignalR event,
    // so this export remains available even if another fight starts immediately.
    void reportTerminalCombat(sessionId)
  },
)
</script>
