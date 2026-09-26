<script setup lang="ts">
import { watch } from 'vue'

import { apiClient } from '@/api/apiClient'
import { isTrainingDummyCombat } from '@/game/combat/trainingDummy'
import { isTrainingDummyCombatLogEnabled } from '@/game/combat/trainingDummyCombatLogSettings'
import { useCombatSessionStore } from '@/stores/combatSession'

interface CombatLogResponse {
  sent: boolean
  errorCode: string | null
}

const combat = useCombatSessionStore()
const reportedSessions = new Set<string>()
const reportingSessions = new Set<string>()

async function reportTerminalCombat(sessionId: string, attempt = 0): Promise<void> {
  if (
    reportedSessions.has(sessionId) ||
    reportingSessions.has(sessionId) ||
    !isTrainingDummyCombatLogEnabled()
  )
    return

  reportingSessions.add(sessionId)
  try {
    const response = await apiClient.request<CombatLogResponse>(
      '/api/v1/combat/boss-log/telegram-v2',
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sessionId }),
      },
    )

    if (response.sent) {
      reportedSessions.add(sessionId)
      return
    }

    if (
      response.errorCode === 'combat_log_not_training_dummy' ||
      response.errorCode === 'combat_log_not_boss'
    ) {
      reportedSessions.add(sessionId)
      return
    }

    console.warn('[training-dummy-combat-log] beta combat log was not sent', response.errorCode)
    if (attempt < 2) {
      window.setTimeout(
        () => void reportTerminalCombat(sessionId, attempt + 1),
        500 * (attempt + 1),
      )
    }
  } catch (error) {
    console.warn('[training-dummy-combat-log] failed to send beta combat log', error)
    if (attempt < 2) {
      window.setTimeout(
        () => void reportTerminalCombat(sessionId, attempt + 1),
        500 * (attempt + 1),
      )
    }
  } finally {
    reportingSessions.delete(sessionId)
  }
}

watch(
  () => combat.snapshot,
  (snapshot) => {
    if (!snapshot || snapshot.status === 'Active' || !isTrainingDummyCombat(snapshot)) return

    // The server archives combat updates before publishing the terminal SignalR event,
    // so this export remains available even if another fight starts immediately.
    void reportTerminalCombat(snapshot.sessionId)
  },
)
</script>

<template>
  <span v-if="false" />
</template>
