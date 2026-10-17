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

interface OpenWorldPlayersResponse {
  available: boolean
  players: OpenWorldPlayer[]
}

const POLL_INTERVAL_MS = 10_000

export function useOpenWorldPlayers() {
  const players = shallowRef<OpenWorldPlayer[]>([])
  const available = shallowRef(true)
  const loading = shallowRef(false)
  const error = shallowRef<string | null>(null)
  let controller: AbortController | null = null
  let timer: ReturnType<typeof setTimeout> | null = null
  let disposed = false

  function stopTimer(): void {
    if (timer !== null) clearTimeout(timer)
    timer = null
  }

  function schedule(): void {
    stopTimer()
    if (!disposed && !document.hidden)
      timer = setTimeout(() => void refresh(), POLL_INTERVAL_MS)
  }

  async function refresh(): Promise<void> {
    if (disposed || loading.value || document.hidden) return
    stopTimer()
    controller = new AbortController()
    loading.value = true
    error.value = null
    try {
      const result = await apiClient.request<OpenWorldPlayersResponse>(
        '/api/v1/world/pvp/players', { signal: controller.signal, cache: 'no-store' },
      )
      if (disposed) return
      available.value = result.available
      players.value = result.available ? result.players : []
    } catch {
      if (!disposed) {
        // A stale list must never offer actions against departed or unavailable players.
        players.value = []
        error.value = 'Список игроков не обновился. Проверь связь и осмотрись ещё раз.'
      }
    } finally {
      loading.value = false
      controller = null
      schedule()
    }
  }

  function onVisibilityChange(): void {
    if (document.hidden) stopTimer()
    else void refresh()
  }

  onMounted(() => {
    document.addEventListener('visibilitychange', onVisibilityChange)
    void refresh()
  })
  onBeforeUnmount(() => {
    disposed = true
    stopTimer()
    controller?.abort()
    document.removeEventListener('visibilitychange', onVisibilityChange)
  })
  return { players, available, loading, error, refresh }
}
