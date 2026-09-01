import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr'

import { apiClient, ApiRequestError } from '@/api/apiClient'
import type { CombatEvent, CombatSnapshot, CombatUpdate } from '@/api/contracts'

export const useCombatSessionStore = defineStore('combatSession', () => {
  const connectionState = ref<'disconnected' | 'connecting' | 'connected'>('disconnected')
  const snapshot = ref<CombatSnapshot | null>(null)
  const events = ref<CombatEvent[]>([])
  const errorCode = ref<string | null>(null)
  const pending = ref(false)
  const isActive = computed(() => snapshot.value?.status === 'Active')
  let connection: HubConnection | null = null
  let connectPromise: Promise<void> | null = null

  async function connect(): Promise<void> {
    if (connection?.state === HubConnectionState.Connected) return
    if (connectPromise) return await connectPromise

    connectPromise = connectCore().finally(() => {
      connectPromise = null
    })
    return await connectPromise
  }

  async function connectCore(): Promise<void> {
    connectionState.value = 'connecting'
    try {
      await apiClient.ensureFreshAccessToken()
      if (!connection) {
        connection = new HubConnectionBuilder()
          .withUrl('/hubs/combat', {
            accessTokenFactory: async () => await apiClient.ensureFreshAccessToken(),
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
    } catch (error) {
      connectionState.value = 'disconnected'
      throw error
    }
  }

  async function startCombat(monsterId = 'WOLF'): Promise<boolean> {
    return await invoke('StartCombat', monsterId)
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

  async function leave(): Promise<boolean> {
    const succeeded = await invoke('LeaveCombat')
    if (succeeded || errorCode.value === 'combat_not_found') {
      snapshot.value = null
      events.value = []
    }
    return succeeded
  }

  async function invoke(method: string, ...args: unknown[]): Promise<boolean> {
    if (pending.value) return false
    pending.value = true
    errorCode.value = null
    try {
      await connect()
      const update = await connection!.invoke<CombatUpdate>(method, ...args)
      applyUpdate(update)
      return update.succeeded
    } catch (error) {
      errorCode.value = error instanceof ApiRequestError
        ? error.code
        : 'combat_connection_failed'
      return false
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
