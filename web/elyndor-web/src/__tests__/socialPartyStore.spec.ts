import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

import { apiClient } from '@/api/apiClient'
import { usePartyStore } from '@/game/party/partyStore'
import { useSocialStore } from '@/game/social/socialStore'

describe('social and party mutation errors', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    setActivePinia(createPinia())
  })

  it('keeps friend request failures in store state', async () => {
    vi.spyOn(apiClient, 'request').mockRejectedValue(new Error('friend_request_rejected'))
    const store = useSocialStore()

    await store.sendRequest('character-2')

    expect(store.errorCode).toBe('friend_request_rejected')
  })

  it('keeps party mutation failures in store state', async () => {
    vi.spyOn(apiClient, 'request').mockRejectedValue(new Error('party_full'))
    const store = usePartyStore()

    await store.create()

    expect(store.errorCode).toBe('party_full')
  })

  it('detects authoritative leader location changes after the initial party snapshot', async () => {
    const initialParty = {
      partyId: 'party-1',
      leaderCharacterId: 'leader-1',
      version: 1,
      members: [{
        characterId: 'leader-1',
        name: 'Leader',
        level: 25,
        classId: 'WARRIOR',
        isLeader: true,
        joinedAtUtc: '2026-09-11T00:00:00Z',
        locationId: 'STARTER_TOWN',
        activeDungeonRunId: null,
      }],
      activeDungeonRunId: null,
    }
    const movedParty = {
      ...initialParty,
      version: 2,
      members: [{ ...initialParty.members[0], locationId: 'DEEP_FOREST' }],
    }
    vi.spyOn(apiClient, 'request')
      .mockResolvedValueOnce(initialParty)
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce(movedParty)
      .mockResolvedValueOnce([])
    const store = usePartyStore()

    await store.refresh()
    expect(store.leaderLocationChange).toBeNull()

    await store.refresh(true)

    expect(store.leaderLocationChange).toEqual({
      leaderCharacterId: 'leader-1',
      leaderName: 'Leader',
      previousLocationId: 'STARTER_TOWN',
      locationId: 'DEEP_FOREST',
    })
  })

  it('exposes outgoing friend requests from the authoritative snapshot', () => {
    const store = useSocialStore()
    store.snapshot = {
      friends: [],
      incomingRequests: [],
      outgoingRequests: [{
        id: 'request-1',
        requesterCharacterId: 'character-1',
        targetCharacterId: 'character-2',
        status: 'Pending',
        createdAtUtc: '2026-09-08T00:00:00Z',
      }],
    }

    expect(store.outgoingRequests).toHaveLength(1)
    expect(store.outgoingRequests[0]?.targetCharacterId).toBe('character-2')
  })

  it('cancels an outgoing friend request through the authoritative endpoint and refreshes', async () => {
    const snapshot = { friends: [], incomingRequests: [], outgoingRequests: [] }
    const request = vi.spyOn(apiClient, 'request')
      .mockResolvedValueOnce(undefined)
      .mockResolvedValueOnce(snapshot)
    const store = useSocialStore()

    await store.cancelRequest('request-1')

    expect(request).toHaveBeenNthCalledWith(
      1,
      '/api/v1/friends/requests/request-1/cancel',
      { method: 'POST' },
    )
    expect(store.snapshot).toEqual(snapshot)
    expect(store.mutationPending).toBe(false)
  })

  it('does not start a second friend mutation while one is already pending', async () => {
    let release!: () => void
    const pending = new Promise<void>((resolve) => {
      release = resolve
    })
    const request = vi.spyOn(apiClient, 'request')
      .mockImplementationOnce(async () => {
        await pending
      })
      .mockResolvedValue({ friends: [], incomingRequests: [], outgoingRequests: [] })
    const store = useSocialStore()

    const first = store.sendRequest('character-2')
    await Promise.resolve()
    await store.sendRequest('character-3')

    expect(request).toHaveBeenCalledTimes(1)

    release()
    await first
    expect(store.mutationPending).toBe(false)
  })

  it('removes a friend through the authoritative endpoint and refreshes', async () => {
    const snapshot = { friends: [], incomingRequests: [], outgoingRequests: [] }
    const request = vi.spyOn(apiClient, 'request')
      .mockResolvedValueOnce(undefined)
      .mockResolvedValueOnce(snapshot)
    const store = useSocialStore()

    await store.removeFriend('character-2')

    expect(request).toHaveBeenNthCalledWith(1, '/api/v1/friends/character-2', { method: 'DELETE' })
    expect(store.snapshot).toEqual(snapshot)
  })
})
