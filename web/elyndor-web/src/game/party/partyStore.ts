import { ref } from 'vue'
import { defineStore } from 'pinia'

import { apiClient } from '@/api/apiClient'
import type { PartyInvite, PartySnapshot } from '@/api/contracts'

export const usePartyStore = defineStore('party', () => {
  const snapshot = ref<PartySnapshot | null>(null)
  const invites = ref<PartyInvite[]>([])
  const loading = ref(false)
  const errorCode = ref<string | null>(null)

  async function refresh(): Promise<void> {
    loading.value = true
    errorCode.value = null
    try {
      const response = await apiClient.request<PartySnapshot | null>('/api/v1/party')
      snapshot.value = response
      invites.value = await apiClient.request<PartyInvite[]>('/api/v1/party/invites')
    } catch (error) {
      errorCode.value = error instanceof Error ? error.message : 'party_load_failed'
    } finally {
      loading.value = false
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

  async function create(): Promise<void> {
    await runMutation(async () => {
      await apiClient.request('/api/v1/party', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ requestId: crypto.randomUUID() }),
      })
    }, 'party_create_failed')
  }

  async function acceptInvite(inviteId: string): Promise<void> {
    await runMutation(async () => {
      await apiClient.request(`/api/v1/party/invites/${inviteId}/accept`, { method: 'POST' })
    }, 'party_invite_accept_failed')
  }

  async function invite(targetCharacterId: string, mode: 'Friend' | 'Direct' = 'Friend'): Promise<void> {
    await runMutation(async () => {
      await apiClient.request('/api/v1/party/invites', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ inviteId: crypto.randomUUID(), targetCharacterId, mode }),
      })
    }, 'party_invite_failed')
  }

  async function declineInvite(inviteId: string): Promise<void> {
    await runMutation(async () => {
      await apiClient.request(`/api/v1/party/invites/${inviteId}/decline`, { method: 'POST' })
    }, 'party_invite_decline_failed')
  }

  async function leave(): Promise<void> {
    await runMutation(async () => {
      await apiClient.request('/api/v1/party/leave', { method: 'POST' })
    }, 'party_leave_failed')
  }

  async function kick(characterId: string): Promise<void> {
    await runMutation(async () => {
      await apiClient.request(`/api/v1/party/kick/${characterId}`, { method: 'POST' })
    }, 'party_kick_failed')
  }

  async function transferLeadership(characterId: string): Promise<void> {
    await runMutation(async () => {
      await apiClient.request(`/api/v1/party/transfer/${characterId}`, { method: 'POST' })
    }, 'party_transfer_failed')
  }

  async function disband(): Promise<void> {
    await runMutation(async () => {
      await apiClient.request('/api/v1/party/disband', { method: 'POST' })
    }, 'party_disband_failed')
  }

  return {
    snapshot,
    invites,
    loading,
    errorCode,
    refresh,
    create,
    invite,
    acceptInvite,
    declineInvite,
    leave,
    kick,
    transferLeadership,
    disband,
  }
})
