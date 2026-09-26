import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr'

import { apiClient, ApiRequestError } from '@/api/apiClient'
import { usePartyStore } from '@/game/party/partyStore'
import { useDungeonStore } from '@/game/party/dungeonStore'
import type {
  CombatActorSnapshot,
  CombatEvent,
  CombatLootRoll,
  CombatReward,
  CombatSnapshot,
  CombatUpdate,
  WorldEncounter,
} from '@/api/contracts'

type CombatRealtimeStage = 'auth_refresh' | 'signalr_start' | 'hub_invoke' | 'resume'

function activeFriendlyActors(current: CombatSnapshot): CombatActorSnapshot[] {
  const rosterStatus = new Map(current.participantRoster?.map(item => [item.actorId, item.status]))
  return [current.player, ...(current.players ?? [])]
    .filter((actor, index, actors) => actor.hp > 0
      && (rosterStatus.get(actor.actorId) ?? 'Active') === 'Active'
      && actors.findIndex(candidate => candidate.actorId === actor.actorId) === index)
}

export type CombatConnectionState =
  | 'disconnected'
  | 'connecting'
  | 'reconnecting'
  | 'syncing'
  | 'connected'

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

export interface CombatThreatEntry {
  actorId: string
  name: string
  threat: number
  isCurrentTarget: boolean
}

export interface CombatThreatSnapshot {
  enemyActorId: string
  enemyName: string
  currentTargetActorId: string | null
  forcedTargetActorId: string | null
  entries: CombatThreatEntry[]
}

interface InvokeOutcome {
  succeeded: boolean
  receivedResponse: boolean
}

interface QueuedAbility {
  abilityId: string
  targetActorId: string
}

const TRAINING_DUMMY_ID = 'TRAINING_DUMMY'
const ABILITY_QUEUE_WINDOW_MS = 250
const COMBAT_EVENT_BUFFER_LIMIT = 1500
const emptyTrainingStats = (): TrainingStats => ({
  startedAtUtc: null,
  totalDamage: 0,
  criticalHits: 0,
  maxHit: 0,
})
const requiresLootRollDecision = (roll: CombatLootRoll): boolean =>
  roll.eligibleCharacterIds.length > 1

export const useCombatSessionStore = defineStore('combatSession', () => {
  const connectionState = ref<CombatConnectionState>('disconnected')
  const reconnectCount = ref(0)
  const lastResyncedAtUtc = ref<string | null>(null)
  const snapshot = ref<CombatSnapshot | null>(null)
  const events = ref<CombatEvent[]>([])
  const reward = ref<CombatReward | null>(null)
  const lootRolls = ref<CombatLootRoll[]>([])
  const errorCode = ref<string | null>(null)
  const diagnostic = ref<CombatRealtimeDiagnostic | null>(null)
  const pendingOperations = ref<Set<string>>(new Set())
  const pending = computed(() => pendingOperations.value.size > 0)
  const abilityPending = computed(() => hasPendingPrefix('ability:'))
  const targetPending = computed(() => pendingOperations.value.has('target'))
  const autoAttackPending = computed(() => pendingOperations.value.has('auto-attack'))
  const fleePending = computed(() => pendingOperations.value.has('flee'))
  const lifecyclePending = computed(() => pendingOperations.value.has('lifecycle'))
  const abilityQueue = ref<QueuedAbility[]>([])
  const selectedFriendlyTargetActorId = ref<string | null>(null)
  const latencyMs = ref<number | null>(null)
  const threat = ref<CombatThreatSnapshot | null>(null)
  const trainingStats = ref<TrainingStats>(emptyTrainingStats())
  const encounterPresentation = ref<WorldEncounter | null>(null)
  const participantStatus = computed(() => {
    const current = snapshot.value
    if (!current || !current.participantRoster) return null
    return current.participantRoster.find(
      participant => participant.actorId === current.player.actorId,
    )?.status ?? null
  })
  const isParticipantActive = computed(() =>
    participantStatus.value === null
      || participantStatus.value === 'Active'
      || participantStatus.value === 'Dead',
  )
  const isAwaitingAttachment = computed(() =>
    snapshot.value?.status === 'Active' && participantStatus.value === 'Rostered',
  )
  const isActive = computed(() => snapshot.value?.status === 'Active' && isParticipantActive.value)
  const enemies = computed(() => snapshot.value?.enemies ?? (snapshot.value ? [snapshot.value.enemy] : []))
  const isTraining = computed(() => snapshot.value?.enemy.definitionId === TRAINING_DUMMY_ID)
  let connection: HubConnection | null = null
  let connectPromise: Promise<void> | null = null
  let resyncPromise: Promise<void> | null = null
  let lootRefreshTimer: number | null = null
  let abilityQueueTimer: number | null = null
  let telemetryBusy = false
  let abilitySending = false
  let abilitySendingId: string | null = null
  let gapRecoveryPromise: Promise<void> | null = null
  const retryCommandIds = new Map<string, string>()
  const seenEventSequences = new Set<number>()
  let lastAppliedSequence = 0

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
        })
        .withAutomaticReconnect([0, 1_000, 3_000, 10_000])
        .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
        .build()
      connection.on('CombatUpdated', applyUpdate)
      connection.on('CombatEnded', applyUpdate)
      connection.on('PartyUpdated', () => {
        void usePartyStore().refresh()
        void useDungeonStore().refresh()
      })
      connection.onreconnecting((error) => {
        reconnectCount.value += 1
        connectionState.value = 'reconnecting'
        latencyMs.value = null
        if (error) recordFailure('signalr_start', 'automatic_reconnect', error)
      })
      connection.onreconnected(() => {
        void resynchronizeAfterReconnect()
      })
      connection.onclose((error) => {
        connectionState.value = 'disconnected'
        latencyMs.value = null
        threat.value = null
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

  async function resynchronizeAfterReconnect(): Promise<void> {
    if (resyncPromise) return await resyncPromise

    resyncPromise = resynchronizeCore().finally(() => {
      resyncPromise = null
    })
    return await resyncPromise
  }

  async function resynchronizeCore(): Promise<void> {
    if (connection?.state !== HubConnectionState.Connected) return

    connectionState.value = 'syncing'
    diagnostic.value = null

    const resumeSucceeded = await resume()
    await Promise.allSettled([
      usePartyStore().refresh(),
      useDungeonStore().refresh(),
    ])

    if (connection?.state !== HubConnectionState.Connected) return

    await refreshCombatTelemetry()
    if (connection?.state !== HubConnectionState.Connected) return

    if (resumeSucceeded) lastResyncedAtUtc.value = new Date().toISOString()
    connectionState.value = 'connected'
  }

  async function startCombat(encounter: WorldEncounter): Promise<boolean> {
    reward.value = null
    clearLootRolls()
    encounterPresentation.value = encounter
    const succeeded = await invoke('lifecycle', 'StartCombat', encounter.encounterId)
    if (!succeeded) encounterPresentation.value = null
    return succeeded
  }

  async function startTraining(): Promise<boolean> {
    reward.value = null
    clearLootRolls()
    encounterPresentation.value = null
    return await invoke('lifecycle', 'StartTraining')
  }

  async function startDungeonEncounter(runId: string): Promise<boolean> {
    reward.value = null
    clearLootRolls()
    return await invoke('lifecycle', 'StartDungeonEncounter', runId)
  }

  async function attachCombat(sessionId: string): Promise<boolean> {
    if (!sessionId) return false
    return await invoke('lifecycle', 'AttachCombat', sessionId)
  }

  async function resetTraining(): Promise<boolean> {
    if (!isTraining.value) return false
    return await invoke('lifecycle', 'ResetTraining')
  }

  async function useAbility(abilityId: string, requestedTargetActorId?: string): Promise<void> {
    const current = snapshot.value
    if (!current || current.status !== 'Active') return
    const ability = current.player.abilities.find((candidate) => candidate.id === abilityId)
    if (!ability) return
    if (abilitySendingId === abilityId || abilityQueue.value.some(item => item.abilityId === abilityId)) return

    const readyAt = current.player.cooldowns[abilityId]
    if (readyAt) {
      const remainingMs = Date.parse(readyAt) - Date.now()
      if (remainingMs > ABILITY_QUEUE_WINDOW_MS) return
    }

    abilityQueue.value = [{
      abilityId,
      targetActorId: requestedTargetActorId ?? resolveAbilityTarget(current, ability.targetType),
    }]
    scheduleAbilityQueueDrain()
  }

  function scheduleAbilityQueueDrain(delayMs = 0): void {
    if (abilityQueueTimer !== null) window.clearTimeout(abilityQueueTimer)
    abilityQueueTimer = window.setTimeout(() => {
      abilityQueueTimer = null
      void drainAbilityQueue()
    }, Math.max(0, delayMs))
  }

  async function drainAbilityQueue(): Promise<void> {
    if (abilitySending || abilityQueue.value.length === 0) {
      if (abilityQueue.value.length > 0) scheduleAbilityQueueDrain(20)
      return
    }

    const current = snapshot.value
    if (!current || current.status !== 'Active') {
      clearAbilityQueue()
      return
    }

    if (current.player.activeCast) {
      scheduleAbilityQueueDrain(Math.max(15, Date.parse(current.player.activeCast.resolvesAtUtc) - Date.now() + 25))
      return
    }

    const queued = abilityQueue.value[0]!
    const abilityId = queued.abilityId
    const ability = current.player.abilities.find((candidate) => candidate.id === abilityId)
    if (!ability) {
      abilityQueue.value = abilityQueue.value.slice(1)
      scheduleAbilityQueueDrain()
      return
    }

    const readyAt = current.player.cooldowns[abilityId]
    if (readyAt) {
      const remainingMs = Date.parse(readyAt) - Date.now()
      if (remainingMs > ABILITY_QUEUE_WINDOW_MS) {
        abilityQueue.value = abilityQueue.value.slice(1)
        return
      }
      if (remainingMs > 0) {
        scheduleAbilityQueueDrain(Math.max(15, remainingMs + 25))
        return
      }
    }

    abilityQueue.value = abilityQueue.value.slice(1)
    abilitySending = true
    abilitySendingId = abilityId
    const sessionId = current.sessionId
    try {
      await invokeRetryableCommand(
        `UseAbility:${sessionId}:${abilityId}:${queued.targetActorId}`,
        commandId => invokeWithOutcome(
          `ability:${abilityId}`, 'UseAbility', sessionId, abilityId, queued.targetActorId, commandId),
      )
    } finally {
      abilitySending = false
      abilitySendingId = null
      if (abilityQueue.value.length > 0) scheduleAbilityQueueDrain()
    }
  }

  function clearAbilityQueue(): void {
    abilityQueue.value = []
    if (abilityQueueTimer !== null) {
      window.clearTimeout(abilityQueueTimer)
      abilityQueueTimer = null
    }
  }

  async function useConsumable(itemDefinitionId: string): Promise<void> {
    if (!snapshot.value || isTraining.value) return
    const sessionId = snapshot.value.sessionId
    await invokeRetryableCommand(
      `UseConsumable:${sessionId}:${itemDefinitionId}`,
      commandId => invokeWithOutcome(
        `consumable:${itemDefinitionId}`, 'UseConsumable', sessionId, itemDefinitionId, commandId),
    )
  }

  async function toggleAutoAttack(): Promise<void> {
    if (!snapshot.value) return
    const sessionId = snapshot.value.sessionId
    const method = snapshot.value.player.autoAttackEnabled ? 'StopAutoAttack' : 'StartAutoAttack'
    await invokeRetryableCommand(
      `${method}:${sessionId}`,
      commandId => invokeWithOutcome('auto-attack', method, sessionId, commandId),
    )
  }

  async function selectTarget(targetActorId: string): Promise<void> {
    if (!snapshot.value || snapshot.value.status !== 'Active') return
    const sessionId = snapshot.value.sessionId
    if (snapshot.value.selectedTargetActorId === targetActorId) return
    await invokeRetryableCommand(
      `SelectTarget:${sessionId}:${targetActorId}`,
      commandId => invokeWithOutcome('target', 'SelectTarget', sessionId, targetActorId, commandId),
    )
  }

  function selectFriendlyTarget(targetActorId: string): void {
    const current = snapshot.value
    if (!current || current.status !== 'Active') return
    const activeFriendlies = activeFriendlyActors(current)
    if (activeFriendlies.some(actor => actor.actorId === targetActorId)) {
      selectedFriendlyTargetActorId.value = targetActorId
    }
  }

  function resolveAbilityTarget(current: CombatSnapshot, targetType?: string): string {
    if (!targetType || targetType === 'SingleEnemy') {
      return current.selectedTargetActorId ?? current.enemy.actorId
    }
    if (targetType === 'SingleAlly') {
      return selectedFriendlyTargetActorId.value ?? current.player.actorId
    }
    return current.player.actorId
  }

  async function resume(lastSeenSequence = highestAppliedSequence()): Promise<boolean> {
    if (connection?.state !== HubConnectionState.Connected) return false
    try {
      const update = await connection.invoke<CombatUpdate>('ResumeCombatFromSequence', lastSeenSequence)
      if (update.errorCode === 'combat_not_found') {
        snapshot.value = null
        selectedFriendlyTargetActorId.value = null
        events.value = []
        seenEventSequences.clear()
        lastAppliedSequence = 0
        reward.value = null
        encounterPresentation.value = null
        trainingStats.value = emptyTrainingStats()
        threat.value = null
        retryCommandIds.clear()
        clearAbilityQueue()
        clearLootRolls()
        errorCode.value = null
        diagnostic.value = null
        return true
      }
      applyUpdate(update)
      await refreshLootRolls()
      return update.succeeded
    } catch (error) {
      recordFailure('resume', 'ResumeCombat', error)
      return false
    }
  }

  async function leave(): Promise<boolean> {
    if (!snapshot.value) return false
    const sessionId = snapshot.value.sessionId
    const succeeded = await invokeRetryableCommand(
      `LeaveCombat:${sessionId}`,
      commandId => invokeWithOutcome('lifecycle', 'LeaveCombat', commandId),
    )
    if (succeeded || errorCode.value === 'combat_not_found') {
      snapshot.value = null
      selectedFriendlyTargetActorId.value = null
      events.value = []
      seenEventSequences.clear()
      lastAppliedSequence = 0
      encounterPresentation.value = null
      trainingStats.value = emptyTrainingStats()
      threat.value = null
      retryCommandIds.clear()
      clearAbilityQueue()
      clearLootRolls()
    }
    return succeeded
  }

  async function flee(): Promise<boolean> {
    if (!snapshot.value || snapshot.value.status !== 'Active') return false
    return await invokeRetryableCommand(
      `FleeCombat:${snapshot.value.sessionId}`,
      commandId => invokeWithOutcome(
        'flee', 'FleeCombat',
        snapshot.value!.sessionId,
        commandId,
      ),
    )
  }

  async function invoke(pendingKey: string, method: string, ...args: unknown[]): Promise<boolean> {
    return (await invokeWithOutcome(pendingKey, method, ...args)).succeeded
  }

  async function invokeWithOutcome(
    pendingKey: string,
    method: string,
    ...args: unknown[]
  ): Promise<InvokeOutcome> {
    if (pendingOperations.value.has(pendingKey)) {
      return { succeeded: false, receivedResponse: false }
    }
    setOperationPending(pendingKey, true)
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
      setOperationPending(pendingKey, false)
    }
  }

  async function refreshCombatTelemetry(): Promise<void> {
    if (telemetryBusy || connection?.state !== HubConnectionState.Connected) return
    telemetryBusy = true
    const startedAt = performance.now()
    try {
      await connection.invoke<string>('Ping')
      latencyMs.value = Math.max(0, Math.round(performance.now() - startedAt))
      threat.value = snapshot.value?.status === 'Active'
        ? await connection.invoke<CombatThreatSnapshot | null>('GetThreatSnapshot')
        : null
    } catch {
      latencyMs.value = null
      if (connection?.state !== HubConnectionState.Connected) threat.value = null
    } finally {
      telemetryBusy = false
    }
  }

  async function refreshLootRolls(): Promise<void> {
    if (connection?.state !== HubConnectionState.Connected) return
    try {
      const openRolls = await connection.invoke<CombatLootRoll[]>('GetLootRolls')
      lootRolls.value = openRolls.filter(requiresLootRollDecision)
      if (lootRolls.value.length === 0) stopLootRefresh()
      else ensureLootRefresh()
    } catch (error) {
      recordFailure('hub_invoke', 'GetLootRolls', error)
    }
  }

  async function chooseLootRoll(
    lootRollId: string,
    choice: 'Need' | 'Greed' | 'Pass',
  ): Promise<boolean> {
    const pendingKey = `loot:${lootRollId}`
    if (pendingOperations.value.has(pendingKey)) return false
    setOperationPending(pendingKey, true)
    errorCode.value = null
    diagnostic.value = null
    try {
      await connect()
      const response = await connection!.invoke<{
        succeeded: boolean
        errorCode: string | null
        roll: CombatLootRoll | null
        winnerCharacterId: string | null
      }>('ChooseLootRoll', lootRollId, choice)
      if (response.succeeded) {
        if (response.roll === null) {
          lootRolls.value = lootRolls.value.filter((roll) => roll.lootRollId !== lootRollId)
          if (lootRolls.value.length === 0) stopLootRefresh()
        } else {
          const index = lootRolls.value.findIndex((roll) => roll.lootRollId === lootRollId)
          if (index >= 0) lootRolls.value[index] = response.roll
        }
      } else {
        errorCode.value = response.errorCode
      }
      return response.succeeded
    } catch (error) {
      recordFailure('hub_invoke', 'ChooseLootRoll', error)
      return false
    } finally {
      setOperationPending(pendingKey, false)
    }
  }

  function setOperationPending(key: string, value: boolean): void {
    const next = new Set(pendingOperations.value)
    if (value) next.add(key)
    else next.delete(key)
    pendingOperations.value = next
  }

  function hasPendingPrefix(prefix: string): boolean {
    for (const key of pendingOperations.value) {
      if (key.startsWith(prefix)) return true
    }
    return false
  }

  function isConsumablePending(itemDefinitionId: string): boolean {
    return pendingOperations.value.has(`consumable:${itemDefinitionId}`)
  }

  function isLootPending(lootRollId: string): boolean {
    return pendingOperations.value.has(`loot:${lootRollId}`)
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

    const incomingSnapshot = update.snapshot
    const currentSnapshot = snapshot.value
    const newSession = incomingSnapshot !== null
      && snapshot.value?.sessionId !== incomingSnapshot.sessionId
    if (!update.fullResyncRequired && hasSequenceGap(update.events)) {
      recoverSequenceGap()
      return
    }

    if (update.fullResyncRequired) {
      events.value = []
      seenEventSequences.clear()
      trainingStats.value = emptyTrainingStats()
      lastAppliedSequence = incomingSnapshot?.sequence ?? 0
    }
    const isStaleSameSession = incomingSnapshot !== null
      && currentSnapshot !== null
      && incomingSnapshot.sessionId === currentSnapshot.sessionId
      && incomingSnapshot.sequence < currentSnapshot.sequence

    errorCode.value = null
    diagnostic.value = null

    if (newSession && incomingSnapshot) {
      retryCommandIds.clear()
      clearAbilityQueue()
      snapshot.value = null
      selectedFriendlyTargetActorId.value = null
      events.value = []
      seenEventSequences.clear()
      lastAppliedSequence = update.fullResyncRequired ? incomingSnapshot.sequence : 0
      reward.value = null
      threat.value = null
      clearLootRolls()
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

    if (!isStaleSameSession
        && incomingSnapshot
        && (!snapshot.value || incomingSnapshot.sequence >= snapshot.value.sequence)) {
      snapshot.value = incomingSnapshot
      normalizeFriendlyTarget(incomingSnapshot)
    }
    if (!isStaleSameSession && incomingSnapshot && incomingSnapshot.status !== 'Active') {
      retryCommandIds.clear()
      clearAbilityQueue()
      threat.value = null
    }

    const eventSequenceCeiling = currentSnapshot?.sequence
      ?? incomingSnapshot?.sequence
      ?? Number.MAX_SAFE_INTEGER
    const fresh = update.events.filter((event) => {
      // A stale snapshot may legitimately carry a late event that fills a gap behind
      // the current authoritative sequence, but it must never inject future events.
      if (isStaleSameSession && event.sequence > eventSequenceCeiling) return false
      if (seenEventSequences.has(event.sequence)) return false
      seenEventSequences.add(event.sequence)
      return true
    })
    if (fresh.length > 0) {
      const bySequence = new Map(events.value.map((event) => [event.sequence, event]))
      for (const event of fresh) bySequence.set(event.sequence, event)
      events.value = [...bySequence.values()]
        .sort((left, right) => left.sequence - right.sequence)
        .slice(-COMBAT_EVENT_BUFFER_LIMIT)
      lastAppliedSequence = Math.max(lastAppliedSequence, ...fresh.map(event => event.sequence))
    }
    accumulateTrainingStats(fresh, snapshot.value ?? incomingSnapshot)
    if (!isStaleSameSession && update.reward) {
      reward.value = update.reward
      if (update.reward.lootRolls?.length) mergeLootRolls(update.reward.lootRolls)
    }
    if (abilityQueue.value.length > 0) scheduleAbilityQueueDrain()
  }

  function highestAppliedSequence(): number {
    return lastAppliedSequence
  }

  function hasSequenceGap(incoming: CombatEvent[]): boolean {
    const unseen = incoming
      .filter(event => !seenEventSequences.has(event.sequence))
      .sort((left, right) => left.sequence - right.sequence)
    if (unseen.length === 0) return false
    let expected = highestAppliedSequence() + 1
    for (const event of unseen) {
      if (event.sequence !== expected) return true
      expected++
    }
    return false
  }

  function recoverSequenceGap(): void {
    if (gapRecoveryPromise || connection?.state !== HubConnectionState.Connected) return
    gapRecoveryPromise = resume(highestAppliedSequence())
      .then(() => undefined)
      .finally(() => {
        gapRecoveryPromise = null
      })
  }

  function normalizeFriendlyTarget(current: CombatSnapshot): void {
    if (current.status !== 'Active') {
      selectedFriendlyTargetActorId.value = null
      return
    }
    const activeFriendlies = activeFriendlyActors(current)
    if (!activeFriendlies.some(actor => actor.actorId === selectedFriendlyTargetActorId.value)) {
      selectedFriendlyTargetActorId.value = activeFriendlies.find(actor => actor.actorId === current.player.actorId)?.actorId
        ?? activeFriendlies[0]?.actorId ?? null
    }
  }

  function mergeLootRolls(incoming: CombatLootRoll[]): void {
    const byId = new Map(lootRolls.value.map((roll) => [roll.lootRollId, roll]))
    for (const roll of incoming.filter(requiresLootRollDecision)) byId.set(roll.lootRollId, roll)
    lootRolls.value = [...byId.values()].filter(requiresLootRollDecision)
    if (lootRolls.value.length > 0) ensureLootRefresh()
  }

  function clearLootRolls(): void {
    lootRolls.value = []
    stopLootRefresh()
  }

  function ensureLootRefresh(): void {
    if (lootRefreshTimer !== null) return
    lootRefreshTimer = window.setInterval(() => {
      if (lootRolls.value.length === 0) {
        stopLootRefresh()
        return
      }
      void refreshLootRolls()
    }, 2_000)
  }

  function stopLootRefresh(): void {
    if (lootRefreshTimer === null) return
    window.clearInterval(lootRefreshTimer)
    lootRefreshTimer = null
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
    reconnectCount,
    lastResyncedAtUtc,
    snapshot,
    events,
    reward,
    lootRolls,
    errorCode,
    diagnostic,
    pending,
    abilityPending,
    targetPending,
    autoAttackPending,
    fleePending,
    lifecyclePending,
    abilityQueue,
    selectedFriendlyTargetActorId,
    latencyMs,
    threat,
    isActive,
    participantStatus,
    isParticipantActive,
    isAwaitingAttachment,
    enemies,
    isTraining,
    trainingStats,
    encounterPresentation,
    connect,
    startCombat,
    startTraining,
    startDungeonEncounter,
    attachCombat,
    resetTraining,
    useAbility,
    useConsumable,
    toggleAutoAttack,
    selectTarget,
    selectFriendlyTarget,
    resume,
    leave,
    flee,
    refreshCombatTelemetry,
    refreshLootRolls,
    chooseLootRoll,
    isConsumablePending,
    isLootPending,
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
