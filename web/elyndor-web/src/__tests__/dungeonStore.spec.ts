import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import type { DungeonRun } from '@/api/contracts'
import { apiClient } from '@/api/apiClient'
import type { PartyDungeonState } from '@/game/party/dungeonStore'
import { useDungeonStore } from '@/game/party/dungeonStore'
import { useGameSessionStore } from '@/stores/gameSession'

describe('dungeon store', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    setActivePinia(createPinia())
  })

  it('restarts a wiped encounter and resyncs the authoritative party/run state', async () => {
    const run = dungeonRun()
    const request = vi.spyOn(apiClient, 'request').mockImplementation(async (path) => {
      if (path === `/api/v1/dungeons/runs/${run.runId}/restart`) return run
      if (path === '/api/v1/dungeons') return []
      if (path === '/api/v1/party/state') return partyDungeonState(run)
      throw new Error(`Unexpected request: ${path}`)
    })
    const store = useDungeonStore()

    await store.restart(run.runId)

    expect(request).toHaveBeenCalledWith(`/api/v1/dungeons/runs/${run.runId}/restart`, {
      method: 'POST',
    })
    expect(request).toHaveBeenCalledWith('/api/v1/party/state')
    expect(store.current).toEqual(run)
  })

  it('returns to town without clearing the active run membership', async () => {
    const run = dungeonRun()
    const request = vi.spyOn(apiClient, 'request').mockImplementation(async (path) => {
      if (path === `/api/v1/party/dungeon-runs/${run.runId}/return-to-town`) {
        return { locationId: 'STARTER_TOWN', locationVersion: 2 }
      }
      if (path === '/api/v1/dungeons') return []
      if (path === '/api/v1/party/state') {
        return partyDungeonState(run, { locationId: 'STARTER_TOWN' })
      }
      throw new Error(`Unexpected request: ${path}`)
    })
    const session = useGameSessionStore()
    const refreshSnapshot = vi.spyOn(session, 'refreshSnapshot').mockResolvedValue(undefined)
    const store = useDungeonStore()
    store.current = run

    await store.returnToTown(run.runId)

    expect(request).toHaveBeenCalledWith(`/api/v1/party/dungeon-runs/${run.runId}/return-to-town`, {
      method: 'POST',
    })
    expect(store.current).toEqual(run)
    expect(store.state?.locationId).toBe('STARTER_TOWN')
    expect(refreshSnapshot).toHaveBeenCalledOnce()
  })

  it('leaves a run, clears authoritative run state and refreshes the world snapshot', async () => {
    const run = dungeonRun()
    const request = vi.spyOn(apiClient, 'request').mockImplementation(async (path) => {
      if (path === `/api/v1/dungeons/runs/${run.runId}/exit`) {
        return { locationId: 'STARTER_TOWN', locationVersion: 2 }
      }
      if (path === '/api/v1/dungeons') return []
      if (path === '/api/v1/party/state') return partyDungeonState(null)
      throw new Error(`Unexpected request: ${path}`)
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

function partyDungeonState(
  currentRun: DungeonRun | null,
  overrides: Partial<PartyDungeonState> = {},
): PartyDungeonState {
  return {
    party: null,
    currentRun,
    characterId: 'character-1',
    locationId: 'ANCIENT_MINE_ENTRY',
    needsEntry: false,
    canEnter: Boolean(currentRun),
    enterBlockedReason: null,
    ...overrides,
  }
}

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
