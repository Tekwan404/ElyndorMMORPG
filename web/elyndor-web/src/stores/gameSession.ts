import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import { apiClient, ApiRequestError } from '@/api/apiClient'
import {
  reconcilePendingGameMutation,
  runReplaySafeGameMutation,
  type ReplaySafeIdField,
} from '@/api/replaySafeMutation'
import type {
  AuthenticationResponse,
  AfkFarmPreview,
  AfkFarmState,
  AfkFarmTarget,
  BootstrapSnapshot,
  CreateCharacterRequest,
  CharacterCompanionSnapshot,
  EquipmentSlot,
  MerchantSnapshot,
  PremiumStoreSnapshot,
  PremiumStorePurchaseResponse,
  PromoCodeRedemptionResponse,
  ItemReforgeResponse,
  ItemReforgePreview,
  ItemStarUpgradeResponse,
  ItemSalvagePreview,
  ItemSalvageReward,
  QuestClaimResponse,
  QuestJournalResponse,
  WorldEncounter,
} from '@/api/contracts'
import { getTelegramInitData } from '@/telegram/telegramWebApp'

export type GameSessionState =
  | 'idle'
  | 'authenticating'
  | 'reauthenticating'
  | 'loading'
  | 'needs-character'
  | 'world'
  | 'offline'
  | 'error'

export const useGameSessionStore = defineStore('gameSession', () => {
  const state = ref<GameSessionState>('idle')
  const snapshot = ref<BootstrapSnapshot | null>(null)
  const questJournal = ref<QuestJournalResponse | null>(null)
  const errorCode = ref<string | null>(null)
  const errorCorrelationId = ref<string | null>(null)
  const roles = ref<string[]>([])
  const mutationPending = ref(false)
  const isReady = computed(() => state.value === 'needs-character' || state.value === 'world')
  const isAdmin = computed(() => roles.value.includes('SUPER_ADMIN'))
  let travelRefreshTimer: ReturnType<typeof setTimeout> | null = null

  apiClient.setReauthenticate(async () => authenticate(true))

  async function authenticate(isRetry = false): Promise<string> {
    const previousState = state.value
    if (!isRetry) {
      state.value = 'authenticating'
    } else if (previousState !== 'world' && previousState !== 'needs-character') {
      state.value = 'reauthenticating'
    }
    try {
      const initData = getTelegramInitData()
      const endpoint = initData
        ? '/api/v1/auth/telegram'
        : import.meta.env.DEV || isLoopbackOrigin()
          ? '/api/v1/auth/development'
          : null
      if (!endpoint) {
        throw new ApiRequestError(401, 'telegram_init_data_missing')
      }

      const authentication = await apiClient.request<AuthenticationResponse>(
        endpoint,
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(initData ? { initData } : {}),
        },
        false,
      )
      apiClient.setAccessToken(authentication.accessToken)
      roles.value = authentication.roles ?? []
      return authentication.accessToken
    } finally {
      if (isRetry && state.value === 'reauthenticating') {
        state.value = previousState
      }
    }
  }

  async function refreshSnapshot(): Promise<void> {
    snapshot.value = await apiClient.request<BootstrapSnapshot>('/api/v1/bootstrap')
    scheduleTravelCompletionRefresh()
  }

  function scheduleTravelCompletionRefresh(): void {
    if (travelRefreshTimer !== null) {
      clearTimeout(travelRefreshTimer)
      travelRefreshTimer = null
    }

    const travel = snapshot.value?.world?.travel
    if (!travel || !snapshot.value) return

    const serverNow = Date.parse(snapshot.value.serverTimeUtc)
    const endsAt = Date.parse(travel.endsAtUtc)
    if (!Number.isFinite(serverNow) || !Number.isFinite(endsAt)) return

    const delay = Math.max(250, Math.min(60_000, endsAt - serverNow + 250))
    travelRefreshTimer = setTimeout(() => {
      travelRefreshTimer = null
      void refreshTravelCompletion()
    }, delay)
  }

  async function refreshTravelCompletion(): Promise<void> {
    try {
      await refreshSnapshot()
      state.value = snapshot.value?.character ? 'world' : 'needs-character'
    } catch (error) {
      handleError(error)
    }
  }

  async function bootstrap(): Promise<void> {
    state.value = 'loading'
    await reconcilePendingGameMutation()
    await refreshSnapshot()
    state.value = snapshot.value?.character ? 'world' : 'needs-character'
  }

  async function start(): Promise<void> {
    errorCode.value = null
    errorCorrelationId.value = null
    try {
      await authenticate()
      await bootstrap()
    } catch (error) {
      handleError(error)
    }
  }

  async function createCharacter(request: CreateCharacterRequest): Promise<void> {
    await mutate('/api/v1/character', request)
  }

  async function getCompanion(): Promise<CharacterCompanionSnapshot | null> {
    errorCode.value = null
    errorCorrelationId.value = null
    try {
      return await apiClient.request<CharacterCompanionSnapshot>('/api/v1/character/companion')
    } catch (error) {
      handleError(error)
      return null
    }
  }

  async function selectCompanion(companionProfileId: string): Promise<CharacterCompanionSnapshot | null> {
    if (mutationPending.value) return null
    mutationPending.value = true
    errorCode.value = null
    errorCorrelationId.value = null
    try {
      const result = await apiClient.request<CharacterCompanionSnapshot>(
        '/api/v1/character/companion/select',
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ companionProfileId }),
        },
      )
      await refreshSnapshot()
      return result
    } catch (error) {
      handleError(error)
      return null
    } finally {
      mutationPending.value = false
    }
  }

  async function travel(targetLocationId: string): Promise<void> {
    await replaySafeMutate(
      'world:travel',
      '/api/v1/world/travel',
      'requestId',
      { targetLocationId },
    )
  }

  async function refreshQuestJournal(): Promise<QuestJournalResponse | null> {
    try {
      questJournal.value = await apiClient.request<QuestJournalResponse>('/api/v1/quests/')
      return questJournal.value
    } catch (error) {
      handleError(error)
      return null
    }
  }

  async function acceptQuest(questId: string): Promise<void> {
    await mutate('/api/v1/quests/accept', { questId })
    await refreshQuestJournal()
  }

  async function abandonQuest(questId: string): Promise<void> {
    await mutate('/api/v1/quests/abandon', { questId })
    await refreshQuestJournal()
  }

  async function claimQuest(questId: string): Promise<QuestClaimResponse | null> {
    if (mutationPending.value) return null
    mutationPending.value = true
    errorCode.value = null
    errorCorrelationId.value = null
    try {
      const response = await runReplaySafeGameMutation<QuestClaimResponse>({
        key: `quest:claim:${questId}`,
        path: '/api/v1/quests/claim',
        idField: 'mutationId',
        intent: { questId },
      })
      await refreshSnapshot()
      questJournal.value = await apiClient.request<QuestJournalResponse>('/api/v1/quests/')
      state.value = snapshot.value?.character ? 'world' : 'needs-character'
      return response
    } catch (error) {
      handleError(error)
      return null
    } finally {
      mutationPending.value = false
    }
  }

  async function acceptContract(contractId: string): Promise<void> {
    await acceptQuest(contractId)
  }

  async function explore(): Promise<WorldEncounter | null> {
    if (mutationPending.value) return null
    mutationPending.value = true
    errorCode.value = null
    errorCorrelationId.value = null
    try {
      return await apiClient.request<WorldEncounter>('/api/v1/world/explore', { method: 'POST' })
    } catch (error) {
      handleError(error)
      return null
    } finally {
      mutationPending.value = false
    }
  }

  async function previewAfkFarm(locationId: string, durationMinutes: number, targetMonsterId: string | null = null): Promise<AfkFarmPreview | null> {
    errorCode.value = null
    errorCorrelationId.value = null
    try {
      return await apiClient.request<AfkFarmPreview>('/api/v1/afk/preview', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ locationId, durationMinutes, targetMonsterId }),
      })
    } catch (error) {
      handleError(error)
      return null
    }
  }

  async function getAfkFarmTargets(): Promise<AfkFarmTarget[]> {
    try {
      return await apiClient.request<AfkFarmTarget[]>('/api/v1/afk/targets')
    } catch (error) {
      handleError(error)
      return []
    }
  }

  async function startAfkFarm(locationId: string, durationMinutes: number, targetMonsterId: string | null = null): Promise<AfkFarmState | null> {
    if (mutationPending.value) return null
    mutationPending.value = true
    errorCode.value = null
    errorCorrelationId.value = null
    try {
      const result = await apiClient.request<AfkFarmState>('/api/v1/afk/start', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ locationId, durationMinutes, targetMonsterId }),
      })
      await refreshSnapshot()
      return result
    } catch (error) {
      handleError(error)
      return null
    } finally {
      mutationPending.value = false
    }
  }

  async function stopAfkFarm(): Promise<AfkFarmState | null> {
    if (mutationPending.value) return null
    mutationPending.value = true
    errorCode.value = null
    errorCorrelationId.value = null
    try {
      const result = await apiClient.request<AfkFarmState>('/api/v1/afk/stop', { method: 'POST' })
      await refreshSnapshot()
      return result
    } catch (error) {
      handleError(error)
      return null
    } finally {
      mutationPending.value = false
    }
  }

  async function equip(characterItemId: string, targetSlot?: EquipmentSlot): Promise<void> {
    await replaySafeMutate(
      'inventory:equip',
      '/api/v1/inventory/equip',
      'mutationId',
      targetSlot ? { characterItemId, targetSlot } : { characterItemId },
    )
  }

  async function unequip(slot: string): Promise<void> {
    await replaySafeMutate(
      'inventory:unequip',
      '/api/v1/inventory/unequip',
      'mutationId',
      { slot },
    )
  }

  async function useConsumable(characterItemId: string): Promise<void> {
    await replaySafeMutate(
      'inventory:use-consumable',
      '/api/v1/inventory/use-consumable',
      'mutationId',
      { characterItemId },
    )
  }

  async function setItemLock(characterItemId: string, isLocked: boolean): Promise<void> {
    await replaySafeMutate(
      'inventory:set-lock',
      '/api/v1/inventory/set-lock',
      'mutationId',
      { characterItemId, isLocked },
    )
  }

  async function getSalvagePreview(characterItemId: string): Promise<ItemSalvagePreview | null> {
    errorCode.value = null
    errorCorrelationId.value = null
    try {
      return await apiClient.request<ItemSalvagePreview>(
        `/api/v1/inventory/salvage/preview/${encodeURIComponent(characterItemId)}`,
      )
    } catch (error) {
      handleError(error)
      return null
    }
  }

  async function salvageItem(
    characterItemId: string,
    confirmedHighValue: boolean,
  ): Promise<ItemSalvageReward | null> {
    if (mutationPending.value) return null
    mutationPending.value = true
    errorCode.value = null
    errorCorrelationId.value = null
    try {
      const result = await runReplaySafeGameMutation<{ reward: ItemSalvageReward }>({
        key: `inventory:salvage:${characterItemId}:${confirmedHighValue}`,
        path: '/api/v1/inventory/salvage',
        idField: 'mutationId',
        intent: { characterItemId, confirmedHighValue },
      })
      await refreshSnapshot()
      return result.reward
    } catch (error) {
      handleError(error)
      return null
    } finally {
      mutationPending.value = false
    }
  }

  async function getPendingReforge(
    characterItemId?: string,
  ): Promise<ItemReforgeResponse | null> {
    errorCode.value = null
    errorCorrelationId.value = null
    try {
      const query = characterItemId
        ? `?characterItemId=${encodeURIComponent(characterItemId)}`
        : ''
      const response = await apiClient.request<ItemReforgeResponse | null>(
        `/api/v1/inventory/reforge/pending${query}`,
      )
      return response ?? null
    } catch (error) {
      if (error instanceof ApiRequestError && error.status === 204) return null
      handleError(error)
      return null
    }
  }

  async function getReforgePreview(
    characterItemId: string,
    slotKey: string,
  ): Promise<ItemReforgePreview | null> {
    errorCode.value = null
    errorCorrelationId.value = null
    try {
      return await apiClient.request<ItemReforgePreview>(
        `/api/v1/inventory/reforge/preview/${encodeURIComponent(characterItemId)}?slotKey=${encodeURIComponent(slotKey)}`,
      )
    } catch (error) {
      handleError(error)
      return null
    }
  }

  async function rollReforge(
    characterItemId: string,
    slotKey: string,
  ): Promise<ItemReforgeResponse | null> {
    if (mutationPending.value) return null
    mutationPending.value = true
    errorCode.value = null
    errorCorrelationId.value = null
    try {
      const result = await runReplaySafeGameMutation<ItemReforgeResponse>({
        key: `inventory:reforge:${characterItemId}:${slotKey}`,
        path: '/api/v1/inventory/reforge/roll',
        idField: 'operationId',
        intent: { characterItemId, slotKey },
      })
      await refreshSnapshot()
      return result
    } catch (error) {
      handleError(error)
      return null
    } finally {
      mutationPending.value = false
    }
  }

  async function upgradeItemStars(characterItemId: string): Promise<ItemStarUpgradeResponse | null> {
    if (mutationPending.value) return null
    mutationPending.value = true
    errorCode.value = null
    errorCorrelationId.value = null
    try {
      const result = await runReplaySafeGameMutation<ItemStarUpgradeResponse>({
        key: `inventory:star-upgrade:${characterItemId}`,
        path: '/api/v1/inventory/star-upgrade',
        idField: 'mutationId',
        intent: { characterItemId },
      })
      await refreshSnapshot()
      return result
    } catch (error) {
      handleError(error)
      return null
    } finally {
      mutationPending.value = false
    }
  }

  async function decideReforge(
    operationId: string,
    acceptProposed: boolean,
  ): Promise<ItemReforgeResponse | null> {
    if (mutationPending.value) return null
    mutationPending.value = true
    errorCode.value = null
    errorCorrelationId.value = null
    try {
      const result = await apiClient.request<ItemReforgeResponse>(
        '/api/v1/inventory/reforge/decide',
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ operationId, acceptProposed }),
        },
      )
      await refreshSnapshot()
      return result
    } catch (error) {
      handleError(error)
      return null
    } finally {
      mutationPending.value = false
    }
  }

  async function getMerchant(merchantId: string): Promise<MerchantSnapshot> {
    return await apiClient.request<MerchantSnapshot>(`/api/v1/inventory/merchant/${merchantId}`)
  }

  async function getPremiumStore(): Promise<PremiumStoreSnapshot> {
    return await apiClient.request<PremiumStoreSnapshot>('/api/v1/economy/store')
  }

  async function buyPremiumStoreOffer(sku: string): Promise<PremiumStorePurchaseResponse | null> {
    if (mutationPending.value) return null
    mutationPending.value = true
    errorCode.value = null
    try {
      const response = await runReplaySafeGameMutation<PremiumStorePurchaseResponse>({
        key: `premium-store:${sku}`,
        path: '/api/v1/economy/store/purchase',
        idField: 'mutationId',
        intent: { sku },
      })
      await refreshSnapshot()
      return response
    } catch (error) {
      handleError(error)
      return null
    } finally {
      mutationPending.value = false
    }
  }

  async function redeemPromoCode(code: string): Promise<PromoCodeRedemptionResponse | null> {
    if (mutationPending.value) return null
    mutationPending.value = true
    errorCode.value = null
    try {
      const response = await runReplaySafeGameMutation<PromoCodeRedemptionResponse>({
        key: `promo-code:${code.trim().toUpperCase()}`,
        path: '/api/v1/economy/promo/redeem',
        idField: 'mutationId',
        intent: { code },
      })
      await refreshSnapshot()
      return response
    } catch (error) {
      handleError(error)
      return null
    } finally {
      mutationPending.value = false
    }
  }

  async function buyMerchantItem(
    merchantId: string,
    itemDefinitionId: string,
    quantity = 1,
  ): Promise<MerchantSnapshot | null> {
    return await merchantMutation(
      'merchant:buy',
      '/api/v1/inventory/merchant/buy',
      { merchantId, itemDefinitionId, quantity },
    )
  }

  async function sellMerchantItem(
    merchantId: string,
    characterItemId: string,
    quantity = 1,
  ): Promise<MerchantSnapshot | null> {
    return await merchantMutation(
      'merchant:sell-item',
      '/api/v1/inventory/merchant/sell-item',
      { merchantId, characterItemId, quantity },
    )
  }

  async function sellMerchantMaterial(
    merchantId: string,
    characterItemId: string,
    quantity = 1,
  ): Promise<MerchantSnapshot | null> {
    return await merchantMutation(
      'merchant:sell-material',
      '/api/v1/inventory/merchant/sell-material',
      { merchantId, characterItemId, quantity },
    )
  }

  async function merchantMutation(
    key: string,
    path: string,
    intent: Record<string, unknown>,
  ): Promise<MerchantSnapshot | null> {
    if (mutationPending.value) return null
    mutationPending.value = true
    errorCode.value = null
    errorCorrelationId.value = null
    try {
      const merchant = await runReplaySafeGameMutation<MerchantSnapshot>({
        key,
        path,
        idField: 'mutationId',
        intent,
      })
      await refreshSnapshot()
      return merchant
    } catch (error) {
      handleError(error)
      return null
    } finally {
      mutationPending.value = false
    }
  }

  async function replaySafeMutate(
    key: string,
    path: string,
    idField: ReplaySafeIdField,
    intent: Record<string, unknown>,
  ): Promise<void> {
    if (mutationPending.value) return
    mutationPending.value = true
    errorCode.value = null
    errorCorrelationId.value = null
    try {
      await runReplaySafeGameMutation<unknown>({ key, path, idField, intent })
      await refreshSnapshot()
      state.value = snapshot.value?.character ? 'world' : 'needs-character'
    } catch (error) {
      handleError(error)
      if (error instanceof ApiRequestError && error.code === 'travel_conflict') {
        await refreshSnapshot()
        state.value = snapshot.value?.character ? 'world' : 'needs-character'
      }
    } finally {
      mutationPending.value = false
    }
  }

  async function mutate(path: string, body: object): Promise<void> {
    if (mutationPending.value) return
    mutationPending.value = true
    errorCode.value = null
    errorCorrelationId.value = null
    try {
      await apiClient.request<unknown>(path, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      })
      await refreshSnapshot()
      state.value = snapshot.value?.character ? 'world' : 'needs-character'
    } catch (error) {
      handleError(error)
      if (error instanceof ApiRequestError && error.code === 'travel_conflict') {
        await refreshSnapshot()
        state.value = snapshot.value?.character ? 'world' : 'needs-character'
      }
    } finally {
      mutationPending.value = false
    }
  }

  function handleError(error: unknown): void {
    if (error instanceof ApiRequestError) {
      errorCode.value = error.code
      errorCorrelationId.value = error.correlationId ?? null
      if (state.value !== 'world' && state.value !== 'needs-character') {
        state.value = 'error'
      }
      return
    }
    errorCode.value = 'network_unavailable'
    errorCorrelationId.value = null
    if (state.value !== 'world' && state.value !== 'needs-character') {
      state.value = 'offline'
    }
  }

  return {
    state,
    snapshot,
    questJournal,
    errorCode,
    errorCorrelationId,
    roles,
    mutationPending,
    isReady,
    isAdmin,
    authenticate,
    refreshSnapshot,
    bootstrap,
    start,
    createCharacter,
    getCompanion,
    selectCompanion,
    travel,
    refreshQuestJournal,
    acceptQuest,
    abandonQuest,
    claimQuest,
    acceptContract,
    explore,
    previewAfkFarm,
    getAfkFarmTargets,
    startAfkFarm,
    stopAfkFarm,
    equip,
    unequip,
    useConsumable,
    setItemLock,
    getSalvagePreview,
    salvageItem,
    getPendingReforge,
    getReforgePreview,
    rollReforge,
    upgradeItemStars,
    decideReforge,
    getMerchant,
    getPremiumStore,
    buyPremiumStoreOffer,
    redeemPromoCode,
    buyMerchantItem,
    sellMerchantItem,
    sellMerchantMaterial,
  }
})

function isLoopbackOrigin(): boolean {
  return ['localhost', '127.0.0.1', '[::1]'].includes(window.location.hostname)
}
