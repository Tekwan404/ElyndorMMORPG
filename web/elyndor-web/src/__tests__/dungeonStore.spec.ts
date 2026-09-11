import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import type { DungeonRun } from '@/api/contracts'
import { apiClient } from '@/api/apiClient'
import { useDungeonStore } from '@/game/party/dungeonStore'
import { useGameSessionStore } from '@/stores/gameSession'

describe('dungeon store', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    setActivePinia(createPinia())
  })

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

  it('leaves a run, clears local run state and refreshes the world snapshot', async () => {
    const run = dungeonRun()
    const request = vi.spyOn(apiClient, 'request').mockResolvedValue({
      locationId: 'STARTER_TOWN',
      locationVersion: 2,
    })
    const session = useGameSessionStore()
    const refreshSnapshot = vi.spyOn(session, 'refreshSnapshot').mockResolvedValue(undefined)
    const store = useDungeonStore()
    store.current = run

    await store.exit(run.runId)

    expect(request).toHaveBeenCalledWith(`/api/v1/dungeons/runs/${run.runId}/exit`, {
      method: 'POST',
    })
    expect(store.current).toBeNull()
    expect(refreshSnapshot).toHaveBeenCalledOnce()
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
