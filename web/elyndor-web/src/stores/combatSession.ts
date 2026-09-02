import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import {
  HubConnectionBuilder,
  HubConnectionState,
  HttpTransportType,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr'

import { apiClient, ApiRequestError } from '@/api/apiClient'
import type { CombatEvent, CombatReward, CombatSnapshot, CombatUpdate } from '@/api/contracts'

type CombatRealtimeStage = 'auth_refresh' | 'signalr_start' | 'hub_invoke' | 'resume'

export interface CombatRealtimeDiagnostic {
  stage: CombatRealtimeStage
  operation: string | null
  code: string
  statusCode: number | null
  message: string
}

export const useCombatSessionStore = defineStore('combatSession', () => {
  const connectionState = ref<'disconnected' | 'connecting' | 'connected'>('disconnected')
  const snapshot = ref<CombatSnapshot | null>(null)
  const events = ref<CombatEvent[]>([])
  const reward = ref<CombatReward | null>(null)
  const errorCode = ref<string | null>(null)
  const diagnostic = ref<CombatRealtimeDiagnostic | null>(null)
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
    diagnostic.value = null

    try {
      await apiClient.ensureFreshAccessToken()
    } catch (error) {
      recordFailure('auth_refresh', null, error)
      connectionState.value = 'disconnected'
      throw error
    }

    if (!connection) {
      connection = new HubConnectionBuilder()
        .withUrl('/hubs/combat', {
          accessTokenFactory: async () => await apiClient.ensureFreshAccessToken(),
          transport: HttpTransportType.LongPolling,
        })
        .withAutomaticReconnect([0, 1_000, 3_000, 10_000])
        .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
        .build()
      connection.on('CombatUpdated', applyUpdate)
      connection.on('CombatEnded', applyUpdate)
      connection.onreconnecting((error) => {
        connectionState.value = 'connecting'
        if (error) recordFailure('signalr_start', 'automatic_reconnect', error)
      })
      connection.onreconnected(() => {
        connectionState.value = 'connected'
        diagnostic.value = null
        void resume()
      })
      connection.onclose((error) => {
        connectionState.value = 'disconnected'
        if (error) recordFailure('signalr_start', 'connection_closed', error)
      })
    }

    try {
      await connection.start()
      connectionState.value = 'connected'
      diagnostic.value = null
    } catch (error) {
      connectionState.value = 'disconnected'
      recordFailure('signalr_start', 'connect', error)
      throw error
    }
  }

  async function startCombat(monsterId = 'WOLF'): Promise<boolean> {
    reward.value = null
    return await invoke('StartCombat', monsterId)
  }

  async function useAbility(abilityId: string): Promise<void> {
    if (!snapshot.value) return
    await invoke('UseAbility', snapshot.value.sessionId, abilityId, crypto.randomUUID())
  }

  async function useConsumable(itemDefinitionId: string): Promise<void> {
    if (!snapshot.value) return
    await invoke('UseConsumable', snapshot.value.sessionId, itemDefinitionId, crypto.randomUUID())
  }

  async function toggleAutoAttack(): Promise<void> {
    if (!snapshot.value) return
    await invoke(
      snapshot.value.player.autoAttackEnabled ? 'StopAutoAttack' : 'StartAutoAttack',
      snapshot.value.sessionId,
      crypto.randomUUID(),
    )
  }

  async function resume(): Promise<boolean> {
    if (connection?.state !== HubConnectionState.Connected) return false
    try {
      const update = await connection.invoke<CombatUpdate>('ResumeCombat')
      if (update.errorCode === 'combat_not_found') {
        snapshot.value = null
        events.value = []
        errorCode.value = null
        diagnostic.value = null
        return true
      }
      applyUpdate(update)
      return update.succeeded
    } catch (error) {
      recordFailure('resume', 'ResumeCombat', error)
      return false
    }
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
    diagnostic.value = null
    let connected = false
    try {
      await connect()
      connected = true
      const update = await connection!.invoke<CombatUpdate>(method, ...args)
      applyUpdate(update)
      return update.succeeded
    } catch (error) {
      if (connected || diagnostic.value === null) {
        recordFailure('hub_invoke', method, error)
      }
      return false
    } finally {
      pending.value = false
    }
  }

  function applyUpdate(update: CombatUpdate): void {
    if (!update.succeeded) {
      errorCode.value = update.errorCode
      diagnostic.value = null
      return
    }
    errorCode.value = null
    diagnostic.value = null
    if (update.snapshot && snapshot.value?.sessionId !== update.snapshot.sessionId) {
      snapshot.value = null
      events.value = []
      reward.value = null
    }
    if (update.snapshot && (!snapshot.value || update.snapshot.sequence >= snapshot.value.sequence)) {
      snapshot.value = update.snapshot
    }
    const lastSequence = events.value.length > 0 ? events.value[events.value.length - 1]!.sequence : 0
    const fresh = update.events.filter((event) => event.sequence > lastSequence)
    events.value = [...events.value, ...fresh].slice(-40)
    if (update.reward) reward.value = update.reward
  }

  function recordFailure(stage: CombatRealtimeStage, operation: string | null, error: unknown): void {
    const statusCode = getStatusCode(error)
    const message = sanitizeDiagnosticMessage(getErrorMessage(error))
    const code = classifyFailure(stage, operation, statusCode, message, error)
    const details: CombatRealtimeDiagnostic = { stage, operation, code, statusCode, message }
    diagnostic.value = details
    errorCode.value = code
    console.error('[combat-realtime]', details)
  }

  return {
    connectionState,
    snapshot,
    events,
    reward,
    errorCode,
    diagnostic,
    pending,
    isActive,
    connect,
    startCombat,
    useAbility,
    useConsumable,
    toggleAutoAttack,
    resume,
    leave,
  }
})

function classifyFailure(
  stage: CombatRealtimeStage,
  operation: string | null,
  statusCode: number | null,
  message: string,
  error: unknown,
): string {
  if (stage === 'auth_refresh') {
    return error instanceof ApiRequestError
      ? `combat_auth_refresh_${error.code}`
      : 'combat_auth_refresh_failed'
  }

  if (stage === 'signalr_start') {
    if (statusCode !== null && /negotiat/i.test(message)) return `combat_negotiate_http_${statusCode}`
    if (statusCode !== null) return `combat_signalr_start_http_${statusCode}`
    if (/negotiat/i.test(message)) return 'combat_negotiate_failed'
    if (/long\s*poll|longpoll/i.test(message)) return 'combat_long_polling_start_failed'
    return 'combat_signalr_start_failed'
  }

  if (stage === 'resume') {
    return statusCode !== null ? `combat_resume_http_${statusCode}` : 'combat_resume_failed'
  }

  const operationCode = operation?.replace(/([a-z])([A-Z])/g, '$1_$2').toLowerCase() ?? 'unknown'
  return statusCode !== null
    ? `combat_hub_${operationCode}_http_${statusCode}`
    : `combat_hub_${operationCode}_failed`
}

function getStatusCode(error: unknown): number | null {
  if (error instanceof ApiRequestError) return error.status
  if (typeof error === 'object' && error !== null && 'statusCode' in error) {
    const value = (error as { statusCode?: unknown }).statusCode
    return typeof value === 'number' ? value : null
  }
  return null
}

function getErrorMessage(error: unknown): string {
  if (error instanceof Error) return error.message
  if (typeof error === 'string') return error
  return 'Unknown realtime error'
}

function sanitizeDiagnosticMessage(message: string): string {
  return message
    .replace(/([?&]access_token=)[^&\s]+/gi, '$1[redacted]')
    .replace(/Bearer\s+[A-Za-z0-9._~-]+/gi, 'Bearer [redacted]')
    .slice(0, 320)
}
