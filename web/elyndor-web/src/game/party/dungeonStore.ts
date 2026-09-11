import { ref } from 'vue'
import { defineStore } from 'pinia'

import { apiClient } from '@/api/apiClient'
import type { DungeonPreview, DungeonRun, DungeonTeleportResponse } from '@/api/contracts'
import { useGameSessionStore } from '@/stores/gameSession'

export const useDungeonStore = defineStore('dungeon', () => {
  const previews = ref<DungeonPreview[]>([])
  const current = ref<DungeonRun | null>(null)
  const loading = ref(false)
  const teleporting = ref(false)
  const errorCode = ref<string | null>(null)

  async function refresh(): Promise<void> {
    loading.value = true
    errorCode.value = null
    try {
      previews.value = await apiClient.request<DungeonPreview[]>('/api/v1/dungeons')
      const response = await apiClient.request<DungeonRun | null>('/api/v1/dungeons/current')
      current.value = response
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
    await useGameSessionStore().refreshSnapshot()
  }

  async function enter(runId: string): Promise<void> {
    await mutate('dungeon_enter_failed', () => apiClient.request<DungeonRun>(
      `/api/v1/dungeons/runs/${runId}/enter`,
      { method: 'POST' },
    ))
    await useGameSessionStore().refreshSnapshot()
  }

  async function restart(runId: string): Promise<void> {
    await mutate('dungeon_restart_failed', () => apiClient.request<DungeonRun>(
      `/api/v1/dungeons/runs/${runId}/restart`,
      { method: 'POST' },
    ))
  }

  async function exit(runId: string): Promise<void> {
    errorCode.value = null
    try {
      await apiClient.request<{ locationId?: string | null; locationVersion?: number | null }>(
        `/api/v1/dungeons/runs/${runId}/exit`,
        { method: 'POST' },
      )
      current.value = null
      await useGameSessionStore().refreshSnapshot()
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

  return { previews, current, loading, teleporting, errorCode, refresh, create, enter, restart, exit, teleport }
})
