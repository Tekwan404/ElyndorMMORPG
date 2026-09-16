import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import type { BootstrapSnapshot, DungeonPreview, DungeonRun } from '@/api/contracts'
import { useDungeonStore } from '@/game/party/dungeonStore'
import PartyView from '@/game/party/views/PartyView.vue'
import { usePartyStore } from '@/game/party/partyStore'
import { useGameSessionStore } from '@/stores/gameSession'

const CHARACTER_ID = 'character-1'

describe('PartyView', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('keeps party management focused while exposing one map entry to dungeons', async () => {
    const { party, dungeon } = preparePartyView('STARTER_TOWN')
    vi.spyOn(party, 'refresh').mockResolvedValue(undefined)
    vi.spyOn(dungeon, 'refresh').mockResolvedValue(undefined)

    const wrapper = mount(PartyView)
    await flushPromises()

    expect(wrapper.text()).toContain('Группа')
    expect(wrapper.text()).toContain('Tekwan')
    expect(wrapper.find('[data-party-disband]').exists()).toBe(true)
    await wrapper.get('[data-party-open-dungeons]').trigger('click')
    expect(wrapper.emitted('open-world')).toEqual([[]])
    expect(wrapper.find('[data-dungeon-teleport]').exists()).toBe(false)
    expect(wrapper.find('[data-create-dungeon]').exists()).toBe(false)
  })

  it('lets an active dungeon member return to the same run from town', async () => {
    const { party, dungeon } = preparePartyView('STARTER_TOWN')
    party.snapshot = {
      ...party.snapshot!,
      activeDungeonRunId: 'run-1',
      members: party.snapshot!.members.map(member => ({
        ...member,
        activeDungeonRunId: 'run-1',
        locationId: 'STARTER_TOWN',
      })),
    }
    dungeon.previews = [dungeonPreview()]
    dungeon.current = dungeonRun()
    vi.spyOn(party, 'refresh').mockResolvedValue(undefined)
    vi.spyOn(dungeon, 'refresh').mockResolvedValue(undefined)
    const returnToRun = vi.spyOn(dungeon, 'returnToRun').mockResolvedValue(undefined)

    const wrapper = mount(PartyView)
    await flushPromises()

    const panel = wrapper.get('[data-party-dungeon-run]')
    expect(panel.text()).toContain('Цитадель Затмения')
    expect(panel.text()).toContain('Этап 1 / 5')
    expect(panel.text()).toContain('Между боями')
    expect(panel.text()).toContain('Прогресс забега сохранён')

    await wrapper.get('[data-party-dungeon-return]').trigger('click')
    expect(returnToRun).toHaveBeenCalledWith('run-1')
  })
})

function preparePartyView(locationId: string) {
  const session = useGameSessionStore()
  session.snapshot = {
    accountId: 'account-1',
    character: { id: CHARACTER_ID },
    world: {
      currentLocation: {
        id: locationId,
        displayName: locationId === 'STARTER_TOWN' ? 'Город' : 'Цитадель Затмения',
      },
      outgoingTransitions: [],
    },
  } as unknown as BootstrapSnapshot

  const party = usePartyStore()
  party.snapshot = {
    partyId: 'party-1',
    leaderCharacterId: CHARACTER_ID,
    version: 1,
    members: [{
      characterId: CHARACTER_ID,
      name: 'Tekwan',
      level: 25,
      classId: 'WARRIOR',
      isLeader: true,
      joinedAtUtc: '2026-09-10T00:00:00Z',
      locationId,
    }],
  }

  const dungeon = useDungeonStore()
  return { session, party, dungeon }
}

function dungeonPreview(): DungeonPreview {
  return {
    id: 'ECLIPSED_CITADEL',
    displayName: 'Цитадель Затмения',
    description: 'Test dungeon',
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

function dungeonRun(): DungeonRun {
  return {
    runId: 'run-1',
    dungeonId: 'ECLIPSED_CITADEL',
    displayName: 'Цитадель Затмения',
    description: 'Test dungeon',
    state: 'Active',
    currentEncounterIndex: 0,
    currentCheckpointId: 'P1',
    encounterCount: 5,
    partyId: 'party-1',
    members: [{ characterId: CHARACTER_ID, state: 'Active', joinedAtUtc: '2026-09-13T00:00:00Z' }],
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
