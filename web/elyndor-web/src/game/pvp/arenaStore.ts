import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr'

import { apiClient } from '@/api/apiClient'
import type { CombatEvent } from '@/api/contracts'
import type {
  ArenaCommandResponse,
  ArenaLeaderboardEntry,
  ArenaMatch,
  ArenaMatchNotification,
  ArenaQueueMode,
  ArenaQueueResponse,
  ArenaStatus,
} from './arenaContracts'

const MAX_LOG_EVENTS = 200

function newCommandId(): string {
  return globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`
}

export const useArenaStore = defineStore('arena', () => {
  const status = ref<ArenaStatus | null>(null)
  const leaderboard = ref<ArenaLeaderboardEntry[]>([])
  const match = ref<ArenaMatch | null>(null)
  const log = ref<CombatEvent[]>([])
  const pending = ref(false)
  const errorCode = ref<string | null>(null)
  const matchStartRating = ref<number | null>(null)
  const matchStartHonor = ref<number | null>(null)
  const enabled = computed(() => status.value?.enabled === true)
  const inMatch = computed(() => match.value?.status === 'Active')
  const ratingDelta = computed(() =>
    match.value?.status === 'Completed' && status.value && matchStartRating.value !== null
      ? status.value.rating - matchStartRating.value
      : null,
  )
  const honorDelta = computed(() =>
    match.value?.status === 'Completed' && status.value && matchStartHonor.value !== null
      ? status.value.honor - matchStartHonor.value
      : null,
  )

  let connection: HubConnection | null = null
  let loadedMatchId: string | null = null
  let lastSequence = 0

  function fail(error: unknown, fallback: string): void {
    errorCode.value = error instanceof Error ? error.message : fallback
  }

  function rememberMatchBaseline(): void {
    if (!status.value) return
    if (matchStartRating.value === null) matchStartRating.value = status.value.rating
    if (matchStartHonor.value === null) matchStartHonor.value = status.value.honor
  }

  async function refreshSummary(): Promise<void> {
    try {
      const [nextStatus, nextLeaderboard] = await Promise.all([
        apiClient.request<ArenaStatus>('/api/v1/arena/status'),
        apiClient.request<ArenaLeaderboardEntry[]>('/api/v1/arena/leaderboard'),
      ])
      status.value = nextStatus
      leaderboard.value = nextLeaderboard
    } catch (error) {
      fail(error, 'arena_load_failed')
    }
  }

  async function refresh(): Promise<void> {
    try {
      status.value = await apiClient.request<ArenaStatus>('/api/v1/arena/status')
      if (status.value.enabled && (status.value.isQueued || status.value.activeMatchId)) {
        if (status.value.activeMatchId) rememberMatchBaseline()
        await connect()
        if (status.value.activeMatchId) await loadMatch(status.value.activeMatchId)
      }
    } catch (error) {
      fail(error, 'arena_load_failed')
    }
  }

  async function loadLeaderboard(): Promise<void> {
    try {
      leaderboard.value = await apiClient.request<ArenaLeaderboardEntry[]>('/api/v1/arena/leaderboard')
    } catch (error) {
      fail(error, 'arena_load_failed')
    }
  }

  /** A live hub connection is the player's presence: it keeps the queue entry and the match alive. */
  async function connect(): Promise<void> {
    if (!connection) {
      connection = new HubConnectionBuilder()
        .withUrl('/hubs/arena', {
          accessTokenFactory: async () => await apiClient.ensureFreshAccessToken(),
        })
        .withAutomaticReconnect([0, 1_000, 3_000, 10_000])
        .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
        .build()
      connection.on('ArenaMatchFound', (note: ArenaMatchNotification) => {
        void refreshAfterNotification(note.matchId)
      })
      connection.on('ArenaMatchUpdated', (note: ArenaMatchNotification) => {
        void refreshAfterNotification(note.matchId)
      })
      connection.onreconnected(() => {
        void refresh()
      })
    }
    if (connection.state === HubConnectionState.Disconnected) await connection.start()
  }

  async function disconnect(): Promise<void> {
    if (connection && connection.state !== HubConnectionState.Disconnected) await connection.stop()
  }

  async function refreshAfterNotification(matchId: string): Promise<void> {
    rememberMatchBaseline()
    await loadMatch(matchId)
    if (match.value?.status === 'Completed' || status.value?.activeMatchId !== matchId) {
      await refreshSummary()
    }
  }

  async function loadMatch(matchId: string): Promise<void> {
    if (!connection || connection.state !== HubConnectionState.Connected) return
    if (loadedMatchId !== matchId) {
      loadedMatchId = matchId
      lastSequence = 0
      log.value = []
    }
    try {
      const response = await connection.invoke<ArenaMatch | null>('GetMatch', matchId, lastSequence)
      applyMatch(response)
    } catch (error) {
      fail(error, 'arena_load_failed')
    }
  }

  function applyMatch(next: ArenaMatch | null): void {
    if (!next) return
    if (next.status === 'Active') rememberMatchBaseline()
    match.value = next
    if (next.sequence >= lastSequence) lastSequence = next.sequence
    if (next.events.length > 0) {
      const known = new Set(log.value.map(event => event.sequence))
      log.value = [...log.value, ...next.events.filter(event => !known.has(event.sequence))]
        .slice(-MAX_LOG_EVENTS)
    }
  }

  async function joinQueue(mode: ArenaQueueMode = 'Ranked'): Promise<void> {
    if (pending.value) return
    pending.value = true
    errorCode.value = null
    matchStartRating.value = status.value?.rating ?? null
    matchStartHonor.value = status.value?.honor ?? null
    try {
      await connect()
      const response = await apiClient.request<ArenaQueueResponse>('/api/v1/arena/queue', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ mode }),
      })
      if (response.status) status.value = response.status
    } catch (error) {
      fail(error, 'arena_queue_failed')
    } finally {
      pending.value = false
    }
  }

  async function leaveQueue(): Promise<void> {
    if (pending.value) return
    pending.value = true
    errorCode.value = null
    try {
      const response = await apiClient.request<ArenaQueueResponse>('/api/v1/arena/queue', { method: 'DELETE' })
      if (response.status) status.value = response.status
    } catch (error) {
      fail(error, 'arena_queue_failed')
    } finally {
      pending.value = false
    }
  }

  async function useAbility(abilityId: string, targetActorId: string): Promise<void> {
    const current = match.value
    if (!current?.matchId || !connection || current.status !== 'Active') return
    errorCode.value = null
    try {
      const response = await connection.invoke<ArenaCommandResponse>(
        'UseAbility', current.matchId, abilityId, targetActorId, newCommandId())
      applyMatch(response.match)
      if (response.match?.status === 'Completed') await refreshSummary()
      if (!response.succeeded) errorCode.value = response.errorCode
    } catch (error) {
      fail(error, 'arena_command_failed')
    }
  }

  async function surrender(): Promise<void> {
    const current = match.value
    if (!current?.matchId || !connection || current.status !== 'Active') return
    errorCode.value = null
    try {
      const response = await connection.invoke<ArenaCommandResponse>('Surrender', current.matchId)
      applyMatch(response.match)
      if (response.match?.status === 'Completed') await refreshSummary()
      if (!response.succeeded) errorCode.value = response.errorCode
    } catch (error) {
      fail(error, 'arena_command_failed')
    }
  }

  function clearMatch(): void {
    match.value = null
    loadedMatchId = null
    lastSequence = 0
    log.value = []
    matchStartRating.value = null
    matchStartHonor.value = null
  }

  function dismissMatch(): void {
    clearMatch()
    void refreshSummary()
  }

  async function findNextOpponent(mode: ArenaQueueMode = 'Ranked'): Promise<void> {
    if (pending.value) return
    clearMatch()
    await joinQueue(mode)
  }

  return {
    status, leaderboard, match, log, pending, errorCode, enabled, inMatch,
    ratingDelta, honorDelta,
    refresh, loadLeaderboard, connect, disconnect, joinQueue, leaveQueue,
    useAbility, surrender, dismissMatch, findNextOpponent,
  }
})
