import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

import { apiClient } from '@/api/apiClient'
import { useGameSessionStore } from '@/stores/gameSession'
import { commerceMessage, type TradeItem, type TradeResult, type TradeSnapshot } from './commerce'

export const useTradeStore = defineStore('playerTrade', () => {
  const current = ref<TradeSnapshot | null>(null)
  const connected = ref(false)
  const joined = ref(false)
  const pending = ref(false)
  const error = ref<string | null>(null)
  const items = ref<TradeItem[]>([])
  const myId = computed(() => useGameSessionStore().snapshot?.character?.id ?? '')
  const isA = computed(() => current.value?.characterAId === myId.value)
  const ownItems = computed(() => current.value ? (isA.value ? current.value.itemsA : current.value.itemsB) : [])
  const otherItems = computed(() => current.value ? (isA.value ? current.value.itemsB : current.value.itemsA) : [])
  const ownGold = computed(() => current.value ? (isA.value ? current.value.goldA : current.value.goldB) : 0)
  const otherGold = computed(() => current.value ? (isA.value ? current.value.goldB : current.value.goldA) : 0)
  const ownLocked = computed(() => current.value ? (isA.value ? current.value.lockedA : current.value.lockedB) : false)
  const bothLocked = computed(() => !!current.value?.lockedA && !!current.value?.lockedB)
  const ownConfirmed = computed(() => current.value ? (isA.value ? current.value.confirmedA : current.value.confirmedB) : false)
  let hub: HubConnection | null = null
  let starting: Promise<void> | null = null
  let syncSequence = 0

  function apply(snapshot: TradeSnapshot): void {
    if (current.value?.id === snapshot.id && snapshot.revision < current.value.revision) return
    if (current.value?.id === snapshot.id && current.value.state !== 'OPEN' && snapshot.state === 'OPEN') return
    current.value = snapshot
    if (snapshot.state !== 'OPEN') joined.value = false
    else void loadItems(snapshot.id, snapshot.revision)
  }

  async function syncTrade(id: string): Promise<void> {
    if (hub?.state !== HubConnectionState.Connected) return
    const sequence = ++syncSequence
    const snapshot = await hub.invoke<TradeSnapshot | null>('Get', id)
    if (sequence === syncSequence && snapshot && (current.value?.id === id || !current.value)) apply(snapshot)
  }

  function onTradeUpdated(snapshot: TradeSnapshot): void {
    if (!current.value || current.value.id !== snapshot.id || snapshot.revision > current.value.revision || snapshot.state !== 'OPEN') apply(snapshot)
    void syncTrade(snapshot.id).catch(() => { error.value = commerceMessage('trade_sync_failed') })
  }

  async function loadItems(id: string, revision: number): Promise<void> {
    if (hub?.state !== HubConnectionState.Connected) return
    try {
      const loaded = await hub.invoke<TradeItem[]>('Items', id)
      if (current.value?.id === id && current.value.revision === revision) items.value = loaded
    } catch { items.value = [] }
  }

  async function refresh(): Promise<void> {
    if (hub?.state !== HubConnectionState.Connected) return
    const sequence = ++syncSequence
    const active = await hub.invoke<TradeSnapshot[]>('Active')
    const previousId = current.value?.id
    const snapshot = active[0] ?? (previousId ? await hub.invoke<TradeSnapshot | null>('Get', previousId) : null)
    if (sequence !== syncSequence) return
    current.value = snapshot
    joined.value = false
    if (current.value) await loadItems(current.value.id, current.value.revision)
    else items.value = []
  }

  async function connect(): Promise<void> {
    if (hub?.state === HubConnectionState.Connected) return
    if (starting) return starting
    starting = (async () => {
      if (!hub) {
        hub = new HubConnectionBuilder()
          .withUrl('/hubs/trade', { accessTokenFactory: async () => apiClient.ensureFreshAccessToken() })
          .withAutomaticReconnect([0, 1_000, 3_000, 10_000])
          .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
          .build()
        hub.on('TradeUpdated', onTradeUpdated)
        hub.onreconnecting(() => { connected.value = false; joined.value = false })
        hub.onreconnected(() => { connected.value = true; void refresh().catch(() => { error.value = commerceMessage('trade_sync_failed') }) })
        hub.onclose(() => { connected.value = false; joined.value = false })
      }
      await hub.start()
      connected.value = true
      await refresh()
    })().finally(() => { starting = null })
    return starting
  }

  async function invoke(method: string, ...args: unknown[]): Promise<boolean> {
    if (pending.value) return false
    pending.value = true
    error.value = null
    try {
      await connect()
      const result = await hub!.invoke<TradeResult>(method, ...args)
      if (!result.succeeded || !result.snapshot) {
        error.value = commerceMessage(result.errorCode ?? 'trade_failed')
        return false
      }
      if (!current.value || result.snapshot.id !== current.value.id || result.snapshot.revision > current.value.revision || result.snapshot.state !== 'OPEN') apply(result.snapshot)
      try { await syncTrade(result.snapshot.id) }
      catch { apply(result.snapshot) }
      if (method === 'Open' || method === 'Join') joined.value = true
      return true
    } catch (failure) {
      error.value = commerceMessage(failure instanceof Error ? failure.message : 'trade_connection_failed')
      if (current.value?.id && hub?.state === HubConnectionState.Connected) {
        try {
          const latest = await hub.invoke<TradeSnapshot | null>('Get', current.value.id)
          if (latest) apply(latest)
        } catch { /* The visible error remains; reconnect will reconcile. */ }
      }
      return false
    } finally {
      pending.value = false
    }
  }

  function open(characterId: string): Promise<boolean> { return invoke('Open', characterId, crypto.randomUUID()) }
  function join(): Promise<boolean> { return current.value ? invoke('Join', current.value.id, crypto.randomUUID()) : Promise.resolve(false) }
  function offer(itemIds: string[], gold: number): Promise<boolean> {
    return current.value ? invoke('Offer', current.value.id, { requestId: crypto.randomUUID(), revision: current.value.revision, itemIds, gold }) : Promise.resolve(false)
  }
  function lock(): Promise<boolean> {
    return current.value ? invoke('Lock', current.value.id, { requestId: crypto.randomUUID(), revision: current.value.revision }) : Promise.resolve(false)
  }
  function confirm(): Promise<boolean> {
    return current.value ? invoke('Confirm', current.value.id, { requestId: crypto.randomUUID(), revision: current.value.revision }) : Promise.resolve(false)
  }
  function cancel(): Promise<boolean> { return current.value ? invoke('Cancel', current.value.id, crypto.randomUUID()) : Promise.resolve(false) }
  function decline(): Promise<boolean> { return current.value ? invoke('Decline', current.value.id, crypto.randomUUID()) : Promise.resolve(false) }

  return { current, items, connected, joined, pending, error, myId, isA, ownItems, otherItems, ownGold,
    otherGold, ownLocked, bothLocked, ownConfirmed, connect, refresh, open, join, offer, lock, confirm, cancel, decline }
})
