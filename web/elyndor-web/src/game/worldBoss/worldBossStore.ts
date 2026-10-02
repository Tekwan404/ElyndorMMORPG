import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr'

import { apiClient, ApiRequestError } from '@/api/apiClient'
import type { CombatUpdate } from '@/api/contracts'
import { useCombatSessionStore } from '@/stores/combatSession'

export interface WorldBossActiveSnapshot {
  spawnId: string
  bossDefinitionId: string
  name: string
  level: number
  currentHealth: number
  maxHealth: number
  currentPhase: number
  phaseName: string
  spawnedAtUtc: string
  expiresAtUtc: string
  participants: number
  personalDamage: number
  partyDamage: number
  rewardEligible: boolean
  rewardTier: string | null
  nextRewardTier: string | null
  nextRewardTierAtDamage: number | null
  damageToNextRewardTier: number
  contentVersion: string
  balanceVersion: string
}

export interface WorldBossPersonalLeaderboardEntry {
  rank: number
  characterId: string
  name: string
  damage: number
}

export interface WorldBossPartyLeaderboardEntry {
  rank: number
  partyId: string
  leaderName: string
  damage: number
}

export interface WorldBossLeaderboard {
  spawnId: string
  players: WorldBossPersonalLeaderboardEntry[]
  parties: WorldBossPartyLeaderboardEntry[]
  personalRank: number | null
  personalDamage: number
  partyId: string | null
  partyRank: number | null
  partyDamage: number
}

export interface WorldBossItemStats {
  strength: number
  agility: number
  intellect: number
  stamina: number
  maxHp: number
  attackPower: number
  spellPower: number
  criticalChance: number
  criticalDamage: number
  accuracy: number
  armor: number
  magicResistance: number
  dodge: number
  armorPenetration: number
  magicPenetration: number
  attackSpeed: number
  maxResource: number
}

export interface WorldBossGeneratedAffix {
  slotKey: string
  statId: string
  value: number
  min: number
  max: number
  step: number
  affixTier: number
  isGuaranteed: boolean
  isReforgeSlot: boolean
}

export interface WorldBossGeneratedItem {
  itemLevel: number
  itemPower: number
  maxItemPower: number
  rollQuality: number
  stars: number
  isPerfect: boolean
  perfectOrigin: string | null
  generatedPrefixId: string | null
  generatedSuffixId: string | null
  displayName: string
  affixes: WorldBossGeneratedAffix[]
}

export interface WorldBossRewardItem {
  itemId: string
  name: string
  rarity: string
  quantity: number
  iconId: string | null
  instanceId: string | null
  pending: boolean
  stats: WorldBossItemStats | null
  generatedItem: WorldBossGeneratedItem | null
  weaponDamageMin: number | null
  weaponDamageMax: number | null
  blockChance: number
  blockValueMin: number
  blockValueMax: number
}

export interface WorldBossReward {
  spawnId: string
  contribution: number
  tier: string
  experience: number
  bossGold: number
  chestGold: number
  totalGold: number
  items: WorldBossRewardItem[]
  settledAtUtc: string
}

export interface WorldBossDefeatedEvent {
  spawnId: string
  defeatedAtUtc: string
}

export interface WorldBossRewardsSettledEvent {
  spawnId: string
  contribution: number
  rewardEligible: boolean
  reward: WorldBossReward | null
  settledAtUtc: string
}

export type WorldBossConnectionState =
  | 'disconnected'
  | 'connecting'
  | 'reconnecting'
  | 'connected'

const DISCOVERY_REFRESH_MS = 10_000

export const useWorldBossStore = defineStore('worldBoss', () => {
  const active = ref<WorldBossActiveSnapshot | null>(null)
  const leaderboard = ref<WorldBossLeaderboard | null>(null)
  const settlement = ref<WorldBossRewardsSettledEvent | null>(null)
  const loading = ref(false)
  const leaderboardLoading = ref(false)
  const entering = ref(false)
  const errorCode = ref<string | null>(null)
  const connectionState = ref<WorldBossConnectionState>('disconnected')
  const defeatedAtUtc = ref<string | null>(null)
  const lastSpawnId = ref<string | null>(null)
  const acknowledgedResultSpawnId = ref<string | null>(null)

  const resultUnseen = computed(() =>
    settlement.value !== null
      && settlement.value.spawnId !== acknowledgedResultSpawnId.value,
  )
  const isDefeated = computed(() =>
    active.value !== null
      && (active.value.currentHealth <= 0 || defeatedAtUtc.value !== null),
  )

  let connection: HubConnection | null = null
  let connectPromise: Promise<void> | null = null
  let startPromise: Promise<void> | null = null
  let discoveryTimer: number | null = null
  let watchedSpawnId: string | null = null

  async function start(): Promise<void> {
    if (startPromise) return await startPromise
    startPromise = startCore().finally(() => {
      startPromise = null
    })
    return await startPromise
  }

  async function startCore(): Promise<void> {
    await connect()
    await refreshActive(true)
    if (discoveryTimer === null) {
      discoveryTimer = window.setInterval(() => {
        void refreshActive(true)
      }, DISCOVERY_REFRESH_MS)
    }
  }

  async function stop(): Promise<void> {
    if (discoveryTimer !== null) {
      window.clearInterval(discoveryTimer)
      discoveryTimer = null
    }
    watchedSpawnId = null
    if (connection) {
      await connection.stop()
      connection = null
    }
    connectionState.value = 'disconnected'
  }

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
    await apiClient.ensureFreshAccessToken()

    if (!connection) {
      connection = new HubConnectionBuilder()
        .withUrl('/hubs/world-boss', {
          accessTokenFactory: async () => await apiClient.ensureFreshAccessToken(),
        })
        .withAutomaticReconnect([0, 1_000, 3_000, 10_000])
        .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
        .build()

      connection.on('WorldBossDefeated', handleDefeated)
      connection.on('RewardsSettled', handleSettled)
      connection.onreconnecting(() => {
        watchedSpawnId = null
        connectionState.value = 'reconnecting'
      })
      connection.onreconnected(() => {
        watchedSpawnId = null
        connectionState.value = 'connected'
        void resynchronize()
      })
      connection.onclose(() => {
        watchedSpawnId = null
        connectionState.value = 'disconnected'
      })
    }

    await connection.start()
    connectionState.value = 'connected'
  }

  async function resynchronize(): Promise<void> {
    const knownSpawnId = lastSpawnId.value
    const fresh = await refreshActive(true)
    if (!fresh && knownSpawnId && settlement.value?.spawnId !== knownSpawnId) {
      await refreshRewardOnce(knownSpawnId)
    }
    if (active.value) await refreshLeaderboard(true)
  }

  async function refreshActive(silent = false): Promise<WorldBossActiveSnapshot | null> {
    if (!silent) loading.value = true
    errorCode.value = null
    try {
      const incoming = await apiClient.request<WorldBossActiveSnapshot | null>(
        '/api/v1/world-boss/active',
      )
      if (incoming) {
        if (lastSpawnId.value !== incoming.spawnId) {
          settlement.value = null
          defeatedAtUtc.value = null
          leaderboard.value = null
          acknowledgedResultSpawnId.value = null
        }
        active.value = incoming
        lastSpawnId.value = incoming.spawnId
        await watchSpawn(incoming.spawnId)
        return incoming
      }

      if (!(active.value && defeatedAtUtc.value && active.value.spawnId === lastSpawnId.value)) {
        active.value = null
      }
      return null
    } catch (error) {
      errorCode.value = toErrorCode(error, 'world_boss_load_failed')
      return null
    } finally {
      if (!silent) loading.value = false
    }
  }

  async function refreshLeaderboard(silent = false): Promise<void> {
    const spawnId = active.value?.spawnId ?? lastSpawnId.value
    if (!spawnId) return
    if (!silent) leaderboardLoading.value = true
    try {
      leaderboard.value = await apiClient.request<WorldBossLeaderboard>(
        `/api/v1/world-boss/${encodeURIComponent(spawnId)}/leaderboard`,
      )
    } catch (error) {
      errorCode.value = toErrorCode(error, 'world_boss_leaderboard_failed')
    } finally {
      if (!silent) leaderboardLoading.value = false
    }
  }

  async function refreshLive(): Promise<void> {
    await refreshActive(true)
    await refreshLeaderboard(true)
  }

  async function refreshRewardOnce(spawnId: string): Promise<WorldBossReward | null> {
    try {
      const reward = await apiClient.request<WorldBossReward | null>(
        `/api/v1/world-boss/${encodeURIComponent(spawnId)}/rewards/me`,
      )
      if (reward) {
        settlement.value = {
          spawnId,
          contribution: reward.contribution,
          rewardEligible: true,
          reward,
          settledAtUtc: reward.settledAtUtc,
        }
        defeatedAtUtc.value ??= reward.settledAtUtc
      }
      return reward
    } catch (error) {
      if (error instanceof ApiRequestError && error.status === 404) return null
      errorCode.value = toErrorCode(error, 'world_boss_reward_failed')
      return null
    }
  }

  async function enter(): Promise<boolean> {
    const boss = active.value
    if (!boss || boss.currentHealth <= 0 || entering.value) return false

    entering.value = true
    errorCode.value = null
    try {
      const update = await apiClient.request<CombatUpdate>(
        `/api/v1/world-boss/${encodeURIComponent(boss.spawnId)}/enter`,
        { method: 'POST' },
      )
      if (!update.succeeded || !update.snapshot) {
        errorCode.value = update.errorCode ?? 'world_boss_enter_failed'
        return false
      }

      const combat = useCombatSessionStore()
      await combat.connect()
      const resumed = await combat.resume(0)
      if (!resumed) {
        errorCode.value = combat.errorCode ?? 'world_boss_combat_attach_failed'
        return false
      }
      return true
    } catch (error) {
      errorCode.value = toErrorCode(error, 'world_boss_enter_failed')
      return false
    } finally {
      entering.value = false
    }
  }

  function acknowledgeResult(): void {
    if (settlement.value) acknowledgedResultSpawnId.value = settlement.value.spawnId
  }

  async function watchSpawn(spawnId: string): Promise<void> {
    if (connection?.state !== HubConnectionState.Connected) return
    if (watchedSpawnId === spawnId) return

    if (watchedSpawnId) {
      try {
        await connection.invoke('UnwatchSpawn', watchedSpawnId)
      } catch {
        // A reconnect already drops the old server-side group membership.
      }
    }

    await connection.invoke('WatchSpawn', spawnId)
    watchedSpawnId = spawnId
  }

  function handleDefeated(event: WorldBossDefeatedEvent): void {
    if (event.spawnId !== lastSpawnId.value && event.spawnId !== active.value?.spawnId) return

    lastSpawnId.value = event.spawnId
    defeatedAtUtc.value = event.defeatedAtUtc
    if (active.value?.spawnId === event.spawnId) {
      active.value = {
        ...active.value,
        currentHealth: 0,
      }
    }
    void refreshLeaderboard(true)
  }

  function handleSettled(event: WorldBossRewardsSettledEvent): void {
    if (event.spawnId !== lastSpawnId.value && event.spawnId !== active.value?.spawnId) return

    lastSpawnId.value = event.spawnId
    settlement.value = event
    defeatedAtUtc.value ??= event.settledAtUtc
    if (active.value?.spawnId === event.spawnId) {
      active.value = {
        ...active.value,
        currentHealth: 0,
        personalDamage: event.contribution,
      }
    }
  }

  return {
    active,
    leaderboard,
    settlement,
    loading,
    leaderboardLoading,
    entering,
    errorCode,
    connectionState,
    defeatedAtUtc,
    lastSpawnId,
    resultUnseen,
    isDefeated,
    start,
    stop,
    connect,
    refreshActive,
    refreshLeaderboard,
    refreshLive,
    refreshRewardOnce,
    enter,
    acknowledgeResult,
  }
})

function toErrorCode(error: unknown, fallback: string): string {
  return error instanceof ApiRequestError
    ? error.code
    : error instanceof Error
      ? error.message
      : fallback
}
