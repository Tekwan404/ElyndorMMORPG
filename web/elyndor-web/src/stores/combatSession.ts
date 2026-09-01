import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr'

import { apiClient } from '@/api/apiClient'
import type { CombatEvent, CombatSnapshot, CombatUpdate } from '@/api/contracts'

export const useCombatSessionStore = defineStore('combatSession', () => {
  const connectionState = ref<'disconnected' | 'connecting' | 'connected'>('disconnected')
  const snapshot = ref<CombatSnapshot | null>(null)
  const events = ref<CombatEvent[]>([])
  const errorCode = ref<string | null>(null)
  const pending = ref(false)
  const isActive = computed(() => snapshot.value?.status === 'Active')
  let connection: HubConnection | null = null

  async function connect(): Promise<void> {
    if (connection?.state === HubConnectionState.Connected) return
    connectionState.value = 'connecting'
    if (!connection) {
      connection = new HubConnectionBuilder()
        .withUrl('/hubs/combat', {
          accessTokenFactory: () => apiClient.getAccessToken() ?? '',
        })
        .withAutomaticReconnect([0, 1_000, 3_000, 10_000])
        .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
        .build()
      connection.on('CombatUpdated', applyUpdate)
      connection.on('CombatEnded', applyUpdate)
      connection.onreconnecting(() => (connectionState.value = 'connecting'))
      connection.onreconnected(() => {
        connectionState.value = 'connected'
        void resume()
      })
      connection.onclose(() => (connectionState.value = 'disconnected'))
    }
    await connection.start()
    connectionState.value = 'connected'
  }

  async function startCombat(): Promise<void> {
    await invoke('StartCombat', 'WOLF')
  }

  async function useAbility(abilityId: string): Promise<void> {
    if (!snapshot.value) return
    await invoke('UseAbility', snapshot.value.sessionId, abilityId, crypto.randomUUID())
  }

  async function toggleAutoAttack(): Promise<void> {
    if (!snapshot.value) return
    await invoke(
      snapshot.value.player.autoAttackEnabled ? 'StopAutoAttack' : 'StartAutoAttack',
      snapshot.value.sessionId,
      crypto.randomUUID(),
    )
  }

  async function resume(): Promise<void> {
    if (connection?.state !== HubConnectionState.Connected) return
    const update = await connection.invoke<CombatUpdate>('ResumeCombat')
    if (update.errorCode === 'combat_not_found') {
      snapshot.value = null
      events.value = []
      errorCode.value = null
      return
    }
    applyUpdate(update)
  }

  async function leave(): Promise<void> {
    await invoke('LeaveCombat')
    snapshot.value = null
    events.value = []
  }

  async function invoke(method: string, ...args: unknown[]): Promise<void> {
    if (pending.value) return
    pending.value = true
    errorCode.value = null
    try {
      await connect()
      const update = await connection!.invoke<CombatUpdate>(method, ...args)
      applyUpdate(update)
    } catch {
      errorCode.value = 'combat_connection_failed'
    } finally {
      pending.value = false
    }
  }

  function applyUpdate(update: CombatUpdate): void {
    if (!update.succeeded) {
      errorCode.value = update.errorCode
      return
    }
    if (update.snapshot && snapshot.value?.sessionId !== update.snapshot.sessionId) {
      snapshot.value = null
      events.value = []
    }
    if (update.snapshot && (!snapshot.value || update.snapshot.sequence >= snapshot.value.sequence)) {
      snapshot.value = update.snapshot
    }
    const lastSequence = events.value.length > 0 ? events.value[events.value.length - 1]!.sequence : 0
    const fresh = update.events.filter((event) => event.sequence > lastSequence)
    events.value = [...events.value, ...fresh].slice(-40)
  }

  return {
    connectionState,
    snapshot,
    events,
    errorCode,
    pending,
    isActive,
    connect,
    startCombat,
    useAbility,
    toggleAutoAttack,
    resume,
    leave,
  }
})
