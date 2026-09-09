import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { BootstrapSnapshot, DungeonPreview, DungeonRun } from '@/api/contracts'
import DungeonLocationCard from '@/game/world/components/DungeonLocationCard.vue'
import { useDungeonStore } from '@/game/party/dungeonStore'
import { usePartyStore } from '@/game/party/partyStore'
import { useGameSessionStore } from '@/stores/gameSession'

const CHARACTER_ID = 'character-1'

describe('DungeonLocationCard', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('shows the dungeon that matches the actual current location instead of Ancient Mine', async () => {
    const session = useGameSessionStore()
    session.snapshot = {
      accountId: 'account-1',
      character: { id: CHARACTER_ID, level: 25 },
      world: { currentLocation: { id: 'ECLIPSED_CITADEL' } },
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
    dungeon.previews = [ancientMine(), eclipsedCitadel()]
    dungeon.current = citadelRun()
    vi.spyOn(dungeon, 'refresh').mockResolvedValue(undefined)

    const wrapper = mount(DungeonLocationCard, {
      props: { dungeonId: 'ECLIPSED_CITADEL' },
    })
    await flushPromises()

    expect(wrapper.get('[data-dungeon-id="ECLIPSED_CITADEL"]').text()).toContain('Цитадель Затмения')
    expect(wrapper.text()).not.toContain('Древняя шахта')
    expect(wrapper.text()).toContain('Предыдущий забег завершён')
    expect(wrapper.find('[data-create-dungeon]').exists()).toBe(true)
  })

  it('does not leak a run from another dungeon into the current location', async () => {
    const session = useGameSessionStore()
    session.snapshot = {
      accountId: 'account-1',
      character: { id: CHARACTER_ID, level: 25 },
      world: { currentLocation: { id: 'ECLIPSED_CITADEL' } },
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
    dungeon.previews = [ancientMine(), eclipsedCitadel()]
    dungeon.current = {
      ...citadelRun(),
      dungeonId: 'ANCIENT_MINE',
      displayName: 'Древняя шахта',
    }
    vi.spyOn(dungeon, 'refresh').mockResolvedValue(undefined)

    const wrapper = mount(DungeonLocationCard, {
      props: { dungeonId: 'ECLIPSED_CITADEL' },
    })
    await flushPromises()

    expect(wrapper.text()).toContain('Цитадель Затмения')
    expect(wrapper.text()).toContain('Группа готова к новому заходу')
    expect(wrapper.text()).not.toContain('Древняя шахта')
  })
})

function ancientMine(): DungeonPreview {
  return {
    id: 'ANCIENT_MINE',
    displayName: 'Древняя шахта',
    description: 'Старая шахта.',
    minimumLevel: 14,
    maximumLevel: 16,
    entryLocationId: 'ANCIENT_MINE',
    minimumPartySize: 1,
    maximumPartySize: 5,
    encounters: [],
  }
}

function eclipsedCitadel(): DungeonPreview {
  return {
    id: 'ECLIPSED_CITADEL',
    displayName: 'Цитадель Затмения',
    description: 'Эндгейм-подземелье 25 уровня.',
    minimumLevel: 25,
    maximumLevel: 25,
    entryLocationId: 'ECLIPSED_CITADEL',
    minimumPartySize: 1,
    maximumPartySize: 5,
    encounters: [
      { id: 'C1', monsterId: 'M1', checkpointId: 'P1', isBoss: false },
      { id: 'C2', monsterId: 'M2', checkpointId: 'P2', isBoss: false },
      { id: 'C3', monsterId: 'M3', checkpointId: 'P3', isBoss: false },
      { id: 'C4', monsterId: 'M4', checkpointId: 'P4', isBoss: false },
      { id: 'C5', monsterId: 'BOSS', checkpointId: 'P5', isBoss: true },
    ],
  }
}

function citadelRun(): DungeonRun {
  return {
    runId: 'run-citadel',
    dungeonId: 'ECLIPSED_CITADEL',
    displayName: 'Цитадель Затмения',
    description: 'Эндгейм-подземелье 25 уровня.',
    state: 'Abandoned',
    currentEncounterIndex: 0,
    currentCheckpointId: 'OUTER_SEAL',
    encounterCount: 5,
    partyId: 'party-1',
    members: [{ characterId: CHARACTER_ID, state: 'Left', joinedAtUtc: '2026-09-10T00:00:00Z' }],
    encounters: [{
      encounterId: 'encounter-1',
      encounterIndex: 0,
      monsterId: 'M1',
      state: 'Pending',
      wipeCount: 0,
      characterIds: [],
    }],
  }
}
