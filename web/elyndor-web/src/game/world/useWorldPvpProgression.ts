import { onBeforeUnmount, onMounted, ref, shallowRef } from 'vue'
import { apiClient } from '@/api/apiClient'
import { runReplaySafeGameMutation } from '@/api/replaySafeMutation'
import { useGameSessionStore } from '@/stores/gameSession'

export interface WorldPvpRank {
  characterId: string
  name: string
  raceId: string
  rating: number
  victories: number
  assists: number
  deaths: number
}
export interface WorldPvpOverview {
  rewards?: { goldPerKill: number; goldPerAssist: number }
  season: number
  endsAtUtc: string
  self: WorldPvpRank
  fieldHonor: number
  bloodyTokens: number
  rewardedToday: number
  dailyLimit: number
  leaders: WorldPvpRank[]
}
export interface WorldPvpOffer {
  id: string
  name: string
  requiredLevel: number
  fieldHonorPrice: number
  tokenPrice: number
}
export function useWorldPvpProgression() {
  const overview = shallowRef<WorldPvpOverview | null>(null)
  const session = useGameSessionStore()
  const offers = shallowRef<WorldPvpOffer[]>([])
  const race = ref('')
  const loading = ref(false)
  const buying = ref(false)
  const error = ref<string | null>(null)
  const shopError = ref<string | null>(null)
  const message = ref<string | null>(null)
  let disposed = false
  let revision = 0
  const controller = new AbortController()

  async function refresh() {
    const version = ++revision
    loading.value = true
    error.value = null
    try {
      const [state, catalog] = await Promise.allSettled([
        apiClient.request<WorldPvpOverview>(
          `/api/v1/world/pvp/progression${race.value ? `?raceId=${race.value}` : ''}`,
          { signal: controller.signal, cache: 'no-store' },
        ),
        apiClient.request<WorldPvpOffer[]>('/api/v1/world/pvp/shop', {
          signal: controller.signal,
          cache: 'no-store',
        }),
      ])
      if (!disposed && version === revision) {
        overview.value = state.status === 'fulfilled' ? state.value : null
        offers.value = catalog.status === 'fulfilled' ? catalog.value : []
        error.value = state.status === 'rejected' ? 'Не удалось обновить рейтинг. Повтори попытку.' : null
        shopError.value = catalog.status === 'rejected' ? 'Магазин временно недоступен. Рейтинг можно просматривать.' : null
      }
    } catch {
      if (!disposed && version === revision)
        error.value = 'Не удалось загрузить реестр войны. Можно повторить.'
    } finally {
      if (!disposed && version === revision) loading.value = false
    }
  }

  async function buy(offer: WorldPvpOffer, tokens: boolean) {
    if (buying.value || loading.value) return
    buying.value = true
    error.value = null
    message.value = null
    try {
      const result = await runReplaySafeGameMutation<{
        succeeded: boolean
        errorCode: string | null
      }>({
        key: `world-pvp:${overview.value?.self.characterId}:${offer.id}:${tokens}`,
        path: '/api/v1/world/pvp/shop/buy',
        idField: 'mutationId',
        intent: { itemId: offer.id, tokens },
      })
      if (disposed) return
      if (!result.succeeded) {
        const reasons: Record<string, string> = {
          not_enough_currency: 'Недостаточно валюты.',
          inventory_full: 'В сумке нет места.',
          required_level: 'Недостаточный уровень.',
          item_not_for_sale: 'Предмет больше не продаётся.',
        }
        error.value = reasons[result.errorCode ?? ''] ?? 'Покупка отклонена сервером.'
        return
      }
      message.value = 'Снаряжение добавлено в сумку.'
      await refresh()
      try {
        await session.refreshSnapshot()
      } catch {
        if (!disposed)
          error.value = 'Покупка завершена. Не удалось обновить сумку — открой её повторно.'
      }
    } catch {
      if (!disposed)
        error.value = 'Ответ о покупке не получен. Можно повторить: повторного списания не будет.'
    } finally {
      if (!disposed) buying.value = false
    }
  }

  onMounted(() => void refresh())
  onBeforeUnmount(() => {
    disposed = true
    controller.abort()
  })

  return { overview, offers, race, loading, buying, error, shopError, message, refresh, buy }
}
