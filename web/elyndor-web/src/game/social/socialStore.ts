import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import { apiClient } from '@/api/apiClient'
import type { FriendRequest, FriendsSnapshot, PlayerSearchResult } from '@/api/contracts'

export const useSocialStore = defineStore('social', () => {
  const snapshot = ref<FriendsSnapshot | null>(null)
  const searchResults = ref<PlayerSearchResult[]>([])
  const loading = ref(false)
  const errorCode = ref<string | null>(null)
  const friends = computed(() => snapshot.value?.friends ?? [])
  const incomingRequests = computed(() => snapshot.value?.incomingRequests ?? [])
  const outgoingRequests = computed(() => snapshot.value?.outgoingRequests ?? [])

  async function refresh(): Promise<void> {
    loading.value = true
    errorCode.value = null
    try {
      snapshot.value = await apiClient.request<FriendsSnapshot>('/api/v1/friends')
    } catch (error) {
      errorCode.value = error instanceof Error ? error.message : 'social_load_failed'
    } finally {
      loading.value = false
    }
  }

  async function search(query: string): Promise<void> {
    if (query.trim().length < 2) {
      searchResults.value = []
      return
    }
    try {
      searchResults.value = await apiClient.request<PlayerSearchResult[]>(
        `/api/v1/social/search?query=${encodeURIComponent(query.trim())}`,
      )
    } catch (error) {
      errorCode.value = error instanceof Error ? error.message : 'social_search_failed'
    }
  }

  async function runMutation(action: () => Promise<void>, fallbackErrorCode: string): Promise<void> {
    errorCode.value = null
    try {
      await action()
      await refresh()
    } catch (error) {
      errorCode.value = error instanceof Error ? error.message : fallbackErrorCode
    }
  }

  async function sendRequest(targetCharacterId: string): Promise<void> {
    await runMutation(async () => {
      await apiClient.request('/api/v1/friends/requests', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ requestId: crypto.randomUUID(), targetCharacterId }),
      })
    }, 'friend_request_failed')
  }

  async function decideRequest(request: FriendRequest, accept: boolean): Promise<void> {
    await runMutation(async () => {
      await apiClient.request(`/api/v1/friends/requests/${request.id}/${accept ? 'accept' : 'decline'}`, {
        method: 'POST',
      })
    }, 'friend_request_decision_failed')
  }

  async function removeFriend(friendCharacterId: string): Promise<void> {
    await runMutation(async () => {
      await apiClient.request(`/api/v1/friends/${friendCharacterId}`, { method: 'DELETE' })
    }, 'friend_remove_failed')
  }

  return {
    snapshot,
    searchResults,
    loading,
    errorCode,
    friends,
    incomingRequests,
    outgoingRequests,
    refresh,
    search,
    sendRequest,
    decideRequest,
    removeFriend,
  }
})
