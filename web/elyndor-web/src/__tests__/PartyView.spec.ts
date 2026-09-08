import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { BootstrapSnapshot, DungeonPreview, DungeonRun } from '@/api/contracts'
import PartyView from '@/game/party/views/PartyView.vue'
import { useDungeonStore } from '@/game/party/dungeonStore'
import { usePartyStore } from '@/game/party/partyStore'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'

const CHARACTER_ID = 'character-1'
const RUN_ID = 'run-1'

describe('PartyView dungeon controls', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('lets an active member exit a non-active run', async () => {
    const stores = prepareStores(run('Pending'))
    const exit = vi.spyOn(stores.dungeon, 'exit').mockResolvedValue(undefined)
    const wrapper = mount(PartyView)
    await flushPromises()

    await wrapper.get('[data-dungeon-exit]').trigger('click')

    expect(exit).toHaveBeenCalledWith(RUN_ID)
  })

  it('lets the leader explicitly restart a wiped current encounter', async () => {
    const stores = prepareStores(run('Wiped'))
    const restart = vi.spyOn(stores.dungeon, 'restart').mockResolvedValue(undefined)
    const wrapper = mount(PartyView)
    await flushPromises()

    await wrapper.get('[data-dungeon-restart]').trigger('click')

    expect(restart).toHaveBeenCalledWith(RUN_ID)
  })
})

function prepareStores(current: DungeonRun) {
  const session = useGameSessionStore()
  session.snapshot = {
    accountId: 'account-1',
    character: { id: CHARACTER_ID },
    world: { currentLocation: { id: 'ANCIENT_MINE' } },
  } as unknown as BootstrapSnapshot

  const party = usePartyStore()
  party.snapshot = {
    partyId: 'party-1',
    leaderCharacterId: CHARACTER_ID,
    version: 1,
    members: [],
  }
  vi.spyOn(party, 'refresh').mockResolvedValue(undefined)

  const dungeon = useDungeonStore()
  dungeon.previews = [preview()]
  dungeon.current = current
  vi.spyOn(dungeon, 'refresh').mockResolvedValue(undefined)

  const combat = useCombatSessionStore()
  vi.spyOn(combat, 'connect').mockResolvedValue(undefined)
  vi.spyOn(combat, 'startDungeonEncounter').mockResolvedValue(true)

  return { dungeon, party, combat }
}

function preview(): DungeonPreview {
  return {
    id: 'ANCIENT_MINE',
    displayName: 'Ancient Mine',
    description: 'Test dungeon',
    minimumLevel: 1,
    maximumLevel: 60,
    entryLocationId: 'ANCIENT_MINE',
    minimumPartySize: 1,
    maximumPartySize: 5,
    encounters: [],
  }
}

function run(state: 'Pending' | 'Wiped'): DungeonRun {
  return {
    runId: RUN_ID,
    dungeonId: 'ANCIENT_MINE',
    displayName: 'Ancient Mine',
    description: 'Test dungeon',
    state: 'Active',
    currentEncounterIndex: 0,
    currentCheckpointId: 'MINE_ENTRANCE',
    encounterCount: 5,
    partyId: 'party-1',
    members: [{ characterId: CHARACTER_ID, state: 'Active', joinedAtUtc: '2026-09-08T00:00:00Z' }],
    encounters: [{
      encounterId: 'encounter-1',
      encounterIndex: 0,
      monsterId: 'DEEP_WOLF_L6',
      state,
      wipeCount: state === 'Wiped' ? 1 : 0,
      characterIds: [],
    }],
  }
}
