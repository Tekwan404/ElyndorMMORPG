import { ref } from 'vue'
import { defineStore } from 'pinia'

import { apiClient } from '@/api/apiClient'
import type { DungeonPreview, DungeonRun, DungeonTeleportResponse } from '@/api/contracts'
import { usePartyStore, type PartySnapshotWithPresence } from '@/game/party/partyStore'
import { useGameSessionStore } from '@/stores/gameSession'

export interface PartyDungeonState {
  party: PartySnapshotWithPresence | null
  currentRun: DungeonRun | null
  characterId: string
  locationId: string | null
  needsEntry: boolean
  canEnter: boolean
  enterBlockedReason: string | null
}

export const useDungeonStore = defineStore('dungeon', () => {
  const previews = ref<DungeonPreview[]>([])
  const current = ref<DungeonRun | null>(null)
  const state = ref<PartyDungeonState | null>(null)
  const loading = ref(false)
  const teleporting = ref(false)
  const errorCode = ref<string | null>(null)

  async function refresh(): Promise<void> {
    loading.value = true
    errorCode.value = null
    try {
      const [previewResponse, stateResponse] = await Promise.all([
        apiClient.request<DungeonPreview[]>('/api/v1/dungeons'),
        apiClient.request<PartyDungeonState>('/api/v1/party/state'),
      ])
      previews.value = previewResponse
      state.value = stateResponse
      current.value = stateResponse.currentRun
      usePartyStore().applySnapshot(stateResponse.party)
    } catch (error) {
      errorCode.value = error instanceof Error ? error.message : 'dungeon_load_failed'
    } finally {
      loading.value = false
    }
  }

  async function create(dungeonId: string): Promise<void> {
    await mutate('dungeon_create_failed', () => apiClient.request<DungeonRun>('/api/v1/dungeons/runs', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ requestId: crypto.randomUUID(), dungeonId }),
    }))
    if (errorCode.value) return
    await useGameSessionStore().refreshSnapshot()
    await refresh()
  }

  async function enter(runId: string): Promise<void> {
    await mutate('dungeon_enter_failed', () => apiClient.request<DungeonRun>(
      `/api/v1/dungeons/runs/${runId}/enter`,
      { method: 'POST' },
    ))
    if (errorCode.value) return
    await useGameSessionStore().refreshSnapshot()
    await refresh()
  }

  async function restart(runId: string): Promise<void> {
    await mutate('dungeon_restart_failed', () => apiClient.request<DungeonRun>(
      `/api/v1/dungeons/runs/${runId}/restart`,
      { method: 'POST' },
    ))
    if (!errorCode.value) await refresh()
  }

  async function returnToTown(runId: string): Promise<void> {
    errorCode.value = null
    try {
      await apiClient.request<{ locationId?: string | null; locationVersion?: number | null }>(
        `/api/v1/party/dungeon-runs/${runId}/return-to-town`,
        { method: 'POST' },
      )
      await useGameSessionStore().refreshSnapshot()
      await refresh()
    } catch (error) {
      errorCode.value = error instanceof Error ? error.message : 'dungeon_return_to_town_failed'
    }
  }

  async function exit(runId: string): Promise<void> {
    errorCode.value = null
    try {
      await apiClient.request<{ locationId?: string | null; locationVersion?: number | null }>(
        `/api/v1/dungeons/runs/${runId}/exit`,
        { method: 'POST' },
      )
      await useGameSessionStore().refreshSnapshot()
      await refresh()
    } catch (error) {
      errorCode.value = error instanceof Error ? error.message : 'dungeon_exit_failed'
    }
  }

  async function teleport(dungeonId: string): Promise<boolean> {
    teleporting.value = true
    errorCode.value = null
    try {
      await apiClient.request<DungeonTeleportResponse>('/api/v1/dungeons/teleport', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ requestId: crypto.randomUUID(), dungeonId }),
      })
      await useGameSessionStore().refreshSnapshot()
      await refresh()

      if (state.value?.needsEntry && current.value) {
        if (!state.value.canEnter) {
          errorCode.value = state.value.enterBlockedReason ?? 'dungeon_member_cannot_enter'
          return false
        }
        await enter(current.value.runId)
        if (errorCode.value) return false
      }

      return true
    } catch (error) {
      errorCode.value = error instanceof Error ? error.message : 'dungeon_teleport_failed'
      return false
    } finally {
      teleporting.value = false
    }
  }

  async function mutate(
    fallbackCode: string,
    operation: () => Promise<DungeonRun>,
  ): Promise<void> {
    errorCode.value = null
    try {
      current.value = await operation()
    } catch (error) {
      errorCode.value = error instanceof Error ? error.message : fallbackCode
    }
  }

  return {
    previews,
    current,
    state,
    loading,
    teleporting,
    errorCode,
    refresh,
    create,
    enter,
    restart,
    returnToTown,
    exit,
    teleport,
  }
})
