import { onBeforeUnmount, onMounted, shallowRef } from 'vue'
import { apiClient } from '@/api/apiClient'

export interface OpenWorldPlayer {
  characterId: string
  name: string
  raceId: string
  classId: string
  genderId: string
  skinId: string | null
  level: number
  inCombat: boolean
}

export function useOpenWorldPlayers() {
  const players = shallowRef<OpenWorldPlayer[]>([])
  const loading = shallowRef(false)
  const error = shallowRef<string | null>(null)
  let controller: AbortController | null = null
  let timer: ReturnType<typeof setTimeout> | null = null
  let disposed = false

  async function refresh(): Promise<void> {
    if (disposed || loading.value) return
    if (timer) clearTimeout(timer)
    if (document.hidden) {
      timer = setTimeout(() => void refresh(), 10_000)
      return
    }
    controller = new AbortController()
    loading.value = true
    error.value = null
    try {
      const result = await apiClient.request<{ available: boolean; players: OpenWorldPlayer[] }>(
        '/api/v1/world/pvp/players', { signal: controller.signal, cache: 'no-store' },
      )
      if (!disposed) players.value = result.available ? result.players : []
    } catch {
      if (!disposed) error.value = 'Не удалось осмотреться. Обнови список.'
    } finally {
      loading.value = false
      if (!disposed) timer = setTimeout(() => void refresh(), 10_000)
    }
  }

  onMounted(() => void refresh())
  onBeforeUnmount(() => {
    disposed = true
    controller?.abort()
    if (timer) clearTimeout(timer)
  })
  return { players, loading, error, refresh }
}
