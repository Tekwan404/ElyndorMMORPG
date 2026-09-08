import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import type { DungeonRun } from '@/api/contracts'
import { apiClient } from '@/api/apiClient'
import { useDungeonStore } from '@/game/party/dungeonStore'

describe('dungeon store', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('restarts a wiped encounter through the authoritative API', async () => {
    const run = dungeonRun()
    const request = vi.spyOn(apiClient, 'request').mockResolvedValue(run)
    const store = useDungeonStore()

    await store.restart(run.runId)

    expect(request).toHaveBeenCalledWith(`/api/v1/dungeons/runs/${run.runId}/restart`, {
      method: 'POST',
    })
    expect(store.current).toEqual(run)
  })

  it('exits a run through the authoritative API and keeps the returned membership state', async () => {
    const run = dungeonRun()
    const request = vi.spyOn(apiClient, 'request').mockResolvedValue(run)
    const store = useDungeonStore()

    await store.exit(run.runId)

    expect(request).toHaveBeenCalledWith(`/api/v1/dungeons/runs/${run.runId}/exit`, {
      method: 'POST',
    })
    expect(store.current).toEqual(run)
  })

  it('exposes an authoritative mutation error without leaving an unhandled rejection', async () => {
    vi.spyOn(apiClient, 'request').mockRejectedValue(new Error('dungeon_encounter_active'))
    const store = useDungeonStore()

    await store.exit('run-1')

    expect(store.errorCode).toBe('dungeon_encounter_active')
  })
})

function dungeonRun(): DungeonRun {
  return {
    runId: 'run-1',
    dungeonId: 'ANCIENT_MINE',
    displayName: 'Ancient Mine',
    description: 'Test dungeon',
    state: 'Active',
    currentEncounterIndex: 0,
    currentCheckpointId: 'MINE_ENTRANCE',
    encounterCount: 5,
    partyId: 'party-1',
    members: [],
    encounters: [],
  }
}
