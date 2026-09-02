import { computed, ref } from 'vue'
import { defineStore } from 'pinia'

import { apiClient, ApiRequestError } from '@/api/apiClient'
import type {
  AuthenticationResponse,
  BootstrapSnapshot,
  CreateCharacterRequest,
  TravelResponse,
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
  const errorCode = ref<string | null>(null)
  const mutationPending = ref(false)
  const isReady = computed(() => state.value === 'needs-character' || state.value === 'world')

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
      return authentication.accessToken
    } finally {
      if (isRetry && state.value === 'reauthenticating') {
        state.value = previousState
      }
    }
  }

  async function refreshSnapshot(): Promise<void> {
    snapshot.value = await apiClient.request<BootstrapSnapshot>('/api/v1/bootstrap')
  }

  async function bootstrap(): Promise<void> {
    state.value = 'loading'
    await refreshSnapshot()
    state.value = snapshot.value?.character ? 'world' : 'needs-character'
  }

  async function start(): Promise<void> {
    errorCode.value = null
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

  async function travel(targetLocationId: string): Promise<void> {
    await mutate('/api/v1/world/travel', {
      requestId: crypto.randomUUID(),
      targetLocationId,
    })
  }

  async function equip(characterItemId: string): Promise<void> {
    await mutate('/api/v1/inventory/equip', { characterItemId })
  }

  async function unequip(slot: string): Promise<void> {
    await mutate('/api/v1/inventory/unequip', { slot })
  }

  async function mutate(path: string, body: object): Promise<void> {
    if (mutationPending.value) return
    mutationPending.value = true
    errorCode.value = null
    try {
      await apiClient.request<TravelResponse>(path, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      })
      await bootstrap()
    } catch (error) {
      handleError(error)
      if (error instanceof ApiRequestError && error.code === 'travel_conflict') {
        await bootstrap()
      }
    } finally {
      mutationPending.value = false
    }
  }

  function handleError(error: unknown): void {
    if (error instanceof ApiRequestError) {
      errorCode.value = error.code
      state.value = 'error'
      return
    }
    errorCode.value = 'network_unavailable'
    state.value = 'offline'
  }

  return {
    state,
    snapshot,
    errorCode,
    mutationPending,
    isReady,
    authenticate,
    refreshSnapshot,
    bootstrap,
    start,
    createCharacter,
    travel,
    equip,
    unequip,
  }
})

function isLoopbackOrigin(): boolean {
  return ['localhost', '127.0.0.1', '[::1]'].includes(window.location.hostname)
}
