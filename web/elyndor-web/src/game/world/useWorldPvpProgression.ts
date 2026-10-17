import { onBeforeUnmount, onMounted, ref, shallowRef } from 'vue'
import { apiClient } from '@/api/apiClient'
import { runReplaySafeGameMutation } from '@/api/replaySafeMutation'
import { useGameSessionStore } from '@/stores/gameSession'

interface Rank {
  characterId: string
  name: string
  raceId: string
  rating: number
  victories: number
  assists: number
  deaths: number
}
interface Overview {
  season: number
  endsAtUtc: string
  self: Rank
  fieldHonor: number
  bloodyTokens: number
  rewardedToday: number
  dailyLimit: number
  leaders: Rank[]
}
interface Offer {
  id: string
  name: string
  requiredLevel: number
  fieldHonorPrice: number
  tokenPrice: number
}
export function useWorldPvpProgression() {
  const overview = shallowRef<Overview | null>(null)
  const session = useGameSessionStore()
  const offers = shallowRef<Offer[]>([])
  const race = ref('')
  const loading = ref(false)
  const buying = ref(false)
  const error = ref<string | null>(null)
  const message = ref<string | null>(null)
  let disposed = false
  let revision = 0
  const controller = new AbortController()

  async function refresh() {
    const version = ++revision
    loading.value = true
    error.value = null
    try {
      const [state, catalog] = await Promise.all([
        apiClient.request<Overview>(
          `/api/v1/world/pvp/progression${race.value ? `?raceId=${race.value}` : ''}`,
          { signal: controller.signal, cache: 'no-store' },
        ),
        apiClient.request<Offer[]>('/api/v1/world/pvp/shop', {
          signal: controller.signal,
          cache: 'no-store',
        }),
      ])
      if (!disposed && version === revision) {
        overview.value = state
        offers.value = catalog
      }
    } catch {
      if (!disposed && version === revision)
        error.value = 'Не удалось загрузить реестр войны. Можно повторить.'
    } finally {
      if (!disposed && version === revision) loading.value = false
    }
  }

  async function buy(offer: Offer, tokens: boolean) {
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

  return { overview, offers, race, loading, buying, error, message, refresh, buy }
}
