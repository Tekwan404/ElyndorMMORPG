import { computed, onBeforeUnmount, onMounted, ref, watch, type Ref } from 'vue'

import { apiClient } from '@/api/apiClient'
import type { WorldLocationScene, WorldSceneObject } from './locationDetails'

export function useLocationScene(locationId: Ref<string>, contentVersion: Ref<string>) {
  const scene = ref<WorldLocationScene | null>(null)
  const selectedId = ref<string | null>(null)
  const page = ref(0)
  const loading = ref(false)
  const error = ref<string | null>(null)
  let generation = 0
  let controller: AbortController | null = null
  let refreshTimer: ReturnType<typeof setTimeout> | null = null
  let disposed = false

  const selected = computed(
    () => scene.value?.objects.find((object) => object.id === selectedId.value) ?? null,
  )
  const pageCount = computed(() => Math.max(1, Math.ceil((scene.value?.objects.length ?? 0) / 8)))
  const visibleObjects = computed(
    () => scene.value?.objects.slice(page.value * 8, page.value * 8 + 8) ?? [],
  )

  async function refresh(): Promise<void> {
    const request = ++generation
    controller?.abort()
    controller = new AbortController()
    if (refreshTimer) clearTimeout(refreshTimer)
    if (!locationId.value || disposed) return
    loading.value = true
    error.value = null
    try {
      const result = await apiClient.request<WorldLocationScene>('/api/v1/world/scene', {
        signal: controller.signal,
        cache: 'no-store',
      })
      if (disposed || request !== generation) return
      if (result.locationId !== locationId.value || !Array.isArray(result.objects))
        throw new Error('Scene no longer matches the current location')
      scene.value = result
      page.value = Math.min(page.value, pageCount.value - 1)
      if (!result.objects.some((object) => object.id === selectedId.value)) selectedId.value = null
      // Timers only request a fresh server snapshot; they never spawn objects locally.
      const delay = result.nextChangeAtUtc
        ? Math.max(
            1000,
            Date.parse(result.nextChangeAtUtc) - Date.parse(result.serverTimeUtc) + 100,
          )
        : 60_000
      refreshTimer = setTimeout(() => void refresh(), Math.min(delay, 60_000))
    } catch {
      if (!disposed && request === generation) {
        error.value = 'Не удалось обновить локацию. Повторите запрос.'
        scene.value = null
        selectedId.value = null
      }
    } finally {
      if (request === generation) loading.value = false
    }
  }

  function select(object: WorldSceneObject): void {
    selectedId.value = object.id
  }
  function turnPage(delta: number): void {
    page.value = Math.max(0, Math.min(pageCount.value - 1, page.value + delta))
    selectedId.value = null
  }
  function onVisible(): void {
    if (document.visibilityState === 'visible') void refresh()
  }
  function onOnline(): void {
    void refresh()
  }

  watch(
    [locationId, contentVersion],
    () => {
      scene.value = null
      selectedId.value = null
      page.value = 0
      void refresh()
    },
    { immediate: true },
  )
  onMounted(() => {
    document.addEventListener('visibilitychange', onVisible)
    window.addEventListener('focus', onOnline)
    window.addEventListener('online', onOnline)
  })
  onBeforeUnmount(() => {
    disposed = true
    generation++
    controller?.abort()
    if (refreshTimer) clearTimeout(refreshTimer)
    document.removeEventListener('visibilitychange', onVisible)
    window.removeEventListener('focus', onOnline)
    window.removeEventListener('online', onOnline)
  })
  return {
    scene,
    selected,
    selectedId,
    page,
    pageCount,
    visibleObjects,
    loading,
    error,
    refresh,
    select,
    turnPage,
  }
}
