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
import type {
  CombatEvent,
  CombatReward,
  CombatSnapshot,
  CombatUpdate,
  WorldEncounter,
} from '@/api/contracts'

type CombatRealtimeStage = 'auth_refresh' | 'signalr_start' | 'hub_invoke' | 'resume'

export interface CombatRealtimeDiagnostic {
  stage: CombatRealtimeStage
  operation: string | null
  code: string
  statusCode: number | null
  message: string
}

export interface TrainingStats {
  startedAtUtc: string | null
  totalDamage: number
  criticalHits: number
  maxHit: number
}

interface InvokeOutcome {
  succeeded: boolean
  receivedResponse: boolean
}

const TRAINING_DUMMY_ID = 'TRAINING_DUMMY'
const emptyTrainingStats = (): TrainingStats => ({
  startedAtUtc: null,
  totalDamage: 0,
  criticalHits: 0,
  maxHit: 0,
})

export const useCombatSessionStore = defineStore('combatSession', () => {
  const connectionState = ref<'disconnected' | 'connecting' | 'connected'>('disconnected')
  const snapshot = ref<CombatSnapshot | null>(null)
  const events = ref<CombatEvent[]>([])
  const reward = ref<CombatReward | null>(null)
  const errorCode = ref<string | null>(null)
  const diagnostic = ref<CombatRealtimeDiagnostic | null>(null)
  const pending = ref(false)
  const trainingStats = ref<TrainingStats>(emptyTrainingStats())
  const encounterPresentation = ref<WorldEncounter | null>(null)
  const isActive = computed(() => snapshot.value?.status === 'Active')
  const isTraining = computed(() => snapshot.value?.enemy.definitionId === TRAINING_DUMMY_ID)
  let connection: HubConnection | null = null
  let connectPromise: Promise<void> | null = null
  const retryCommandIds = new Map<string, string>()

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

  async function startCombat(encounter: WorldEncounter): Promise<boolean> {
    reward.value = null
    encounterPresentation.value = encounter
    const succeeded = await invoke('StartCombat', encounter.encounterId)
    if (!succeeded) encounterPresentation.value = null
    return succeeded
  }

  async function startTraining(): Promise<boolean> {
    reward.value = null
    encounterPresentation.value = null
    return await invoke('StartTraining')
  }

  async function resetTraining(): Promise<boolean> {
    if (!isTraining.value) return false
    return await invoke('ResetTraining')
  }

  async function useAbility(abilityId: string): Promise<void> {
    if (!snapshot.value) return
    const sessionId = snapshot.value.sessionId
    await invokeRetryableCommand(
      `UseAbility:${sessionId}:${abilityId}`,
      commandId => invokeWithOutcome('UseAbility', sessionId, abilityId, commandId),
    )
  }

  async function useConsumable(itemDefinitionId: string): Promise<void> {
    if (!snapshot.value || isTraining.value) return
    const sessionId = snapshot.value.sessionId
    await invokeRetryableCommand(
      `UseConsumable:${sessionId}:${itemDefinitionId}`,
      commandId => invokeWithOutcome('UseConsumable', sessionId, itemDefinitionId, commandId),
    )
  }

  async function toggleAutoAttack(): Promise<void> {
    if (!snapshot.value) return
    const sessionId = snapshot.value.sessionId
    const method = snapshot.value.player.autoAttackEnabled ? 'StopAutoAttack' : 'StartAutoAttack'
    await invokeRetryableCommand(
      `${method}:${sessionId}`,
      commandId => invokeWithOutcome(method, sessionId, commandId),
    )
  }

  async function resume(): Promise<boolean> {
    if (connection?.state !== HubConnectionState.Connected) return false
    try {
      const update = await connection.invoke<CombatUpdate>('ResumeCombat')
      if (update.errorCode === 'combat_not_found') {
        snapshot.value = null
        events.value = []
        encounterPresentation.value = null
        trainingStats.value = emptyTrainingStats()
        retryCommandIds.clear()
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
      encounterPresentation.value = null
      trainingStats.value = emptyTrainingStats()
      retryCommandIds.clear()
    }
    return succeeded
  }

  async function invoke(method: string, ...args: unknown[]): Promise<boolean> {
    return (await invokeWithOutcome(method, ...args)).succeeded
  }

  async function invokeWithOutcome(method: string, ...args: unknown[]): Promise<InvokeOutcome> {
    if (pending.value) return { succeeded: false, receivedResponse: false }
    pending.value = true
    errorCode.value = null
    diagnostic.value = null
    let connected = false
    try {
      await connect()
      connected = true
      const update = await connection!.invoke<CombatUpdate>(method, ...args)
      applyUpdate(update)
      return { succeeded: update.succeeded, receivedResponse: true }
    } catch (error) {
      if (connected || diagnostic.value === null) {
        recordFailure('hub_invoke', method, error)
      }
      return { succeeded: false, receivedResponse: false }
    } finally {
      pending.value = false
    }
  }

  async function invokeRetryableCommand(
    key: string,
    operation: (commandId: string) => Promise<InvokeOutcome>,
  ): Promise<boolean> {
    const commandId = retryCommandIds.get(key) ?? crypto.randomUUID()
    retryCommandIds.set(key, commandId)
    const outcome = await operation(commandId)
    if (outcome.receivedResponse) retryCommandIds.delete(key)
    return outcome.succeeded
  }

  function applyUpdate(update: CombatUpdate): void {
    if (!update.succeeded) {
      errorCode.value = update.errorCode
      diagnostic.value = null
      return
    }
    errorCode.value = null
    diagnostic.value = null

    const incomingSnapshot = update.snapshot
    const newSession = incomingSnapshot !== null
      && snapshot.value?.sessionId !== incomingSnapshot.sessionId
    if (newSession && incomingSnapshot) {
      retryCommandIds.clear()
      snapshot.value = null
      events.value = []
      reward.value = null
      if (encounterPresentation.value?.monsterId !== incomingSnapshot.enemy.definitionId) {
        encounterPresentation.value = null
      }
      trainingStats.value = incomingSnapshot.enemy.definitionId === TRAINING_DUMMY_ID
        ? {
            ...emptyTrainingStats(),
            startedAtUtc: update.events.find((event) => event.type === 'CombatStarted')?.serverTimeUtc
              ?? incomingSnapshot.serverTimeUtc,
          }
        : emptyTrainingStats()
    }

    if (incomingSnapshot && (!snapshot.value || incomingSnapshot.sequence >= snapshot.value.sequence)) {
      snapshot.value = incomingSnapshot
    }
    if (incomingSnapshot && incomingSnapshot.status !== 'Active') {
      retryCommandIds.clear()
    }
    const lastSequence = events.value.length > 0 ? events.value[events.value.length - 1]!.sequence : 0
    const fresh = update.events.filter((event) => event.sequence > lastSequence)
    events.value = [...events.value, ...fresh].slice(-40)
    accumulateTrainingStats(fresh, incomingSnapshot ?? snapshot.value)
    if (update.reward) reward.value = update.reward
  }

  function accumulateTrainingStats(fresh: CombatEvent[], current: CombatSnapshot | null): void {
    if (!current || current.enemy.definitionId !== TRAINING_DUMMY_ID) return
    if (!trainingStats.value.startedAtUtc) {
      trainingStats.value.startedAtUtc = fresh.find((event) => event.type === 'CombatStarted')?.serverTimeUtc
        ?? current.serverTimeUtc
    }

    let totalDamage = trainingStats.value.totalDamage
    let criticalHits = trainingStats.value.criticalHits
    let maxHit = trainingStats.value.maxHit
    for (const event of fresh) {
      const playerToDummy = event.sourceActorId === current.player.actorId
        && event.targetActorId === current.enemy.actorId
      if (!playerToDummy) continue
      if (event.type === 'DamageDealt') {
        const damage = event.amountBeforeShields > 0 ? event.amountBeforeShields : event.amount
        if (damage <= 0) continue
        totalDamage += damage
        maxHit = Math.max(maxHit, damage)
      } else if (event.type === 'CriticalHit') {
        criticalHits += 1
      }
    }
    trainingStats.value = {
      startedAtUtc: trainingStats.value.startedAtUtc,
      totalDamage,
      criticalHits,
      maxHit,
    }
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
    isTraining,
    trainingStats,
    encounterPresentation,
    connect,
    startCombat,
    startTraining,
    resetTraining,
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
