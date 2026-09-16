<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { ApiRequestError } from '@/api/apiClient'
import AppShell from '@/app/AppShell.vue'
import {
  getTelegramInitData,
  setWebAuthenticationData,
} from '@/telegram/telegramWebApp'
import {
  beginTelegramWebLogin,
  completeTelegramWebLogin,
} from '@/telegram/telegramWebLogin'
import { UIButton, UILoadingState } from '@/ui/components'

type EntryState = 'checking' | 'login' | 'redirecting' | 'error'

const ready = ref(false)
const state = ref<EntryState>('checking')
const errorCode = ref<string | null>(null)

const isDevelopmentRuntime =
  import.meta.env.DEV
  || ['localhost', '127.0.0.1', '[::1]'].includes(window.location.hostname)

const errorMessage = computed(() => {
  switch (errorCode.value) {
    case 'telegram_web_auth_not_configured':
      return 'Вход через Telegram для браузера пока не настроен.'
    case 'telegram_web_login_cancelled':
      return 'Вход через Telegram был отменён.'
    case 'telegram_web_state_invalid':
      return 'Сессия входа устарела. Начните вход заново.'
    case 'telegram_web_auth_unavailable':
      return 'Telegram временно недоступен. Попробуйте войти ещё раз.'
    case 'telegram_web_auth_invalid':
    case 'telegram_web_login_failed':
      return 'Не удалось подтвердить аккаунт Telegram. Попробуйте войти ещё раз.'
    default:
      return 'Не удалось выполнить вход через Telegram.'
  }
})

onMounted(async () => {
  if (getTelegramInitData() || isDevelopmentRuntime) {
    ready.value = true
    return
  }

  try {
    const webCredential = await completeTelegramWebLogin()
    if (!webCredential) {
      state.value = 'login'
      return
    }

    setWebAuthenticationData(webCredential)
    ready.value = true
  } catch (error) {
    showError(error)
  }
})

async function login(): Promise<void> {
  if (state.value === 'redirecting') return

  state.value = 'redirecting'
  errorCode.value = null
  try {
    await beginTelegramWebLogin()
  } catch (error) {
    showError(error)
  }
}

function showError(error: unknown): void {
  errorCode.value = error instanceof ApiRequestError
    ? error.code
    : 'telegram_web_login_failed'
  state.value = 'error'
}
</script>

<template>
  <AppShell v-if="ready" />

  <main v-else class="web-entry">
    <UILoadingState
      v-if="state === 'checking' || state === 'redirecting'"
      state="loading"
      :title="state === 'redirecting' ? 'Открываем Telegram' : 'Проверяем вход'"
      :message="state === 'redirecting'
        ? 'Подтвердите вход в Telegram и вернитесь в Elyndor.'
        : 'Определяем способ входа в Elyndor.'"
    />

    <UILoadingState
      v-else-if="state === 'login'"
      state="empty"
      title="Войти в Elyndor"
      message="Используйте свой Telegram-аккаунт. Если вы уже играете через Mini App, откроется тот же персонаж."
    >
      <UIButton data-telegram-web-login @click="login">Войти через Telegram</UIButton>
    </UILoadingState>

    <UILoadingState
      v-else
      state="error"
      title="Не удалось войти"
      :message="errorMessage"
    >
      <UIButton data-telegram-web-retry variant="secondary" @click="login">Попробовать снова</UIButton>
    </UILoadingState>
  </main>
</template>

<style scoped>
.web-entry {
  display: grid;
  width: min(100%, var(--ui-content-width));
  min-height: var(--ui-viewport-height);
  margin-inline: auto;
  place-items: center;
  padding:
    calc(var(--ui-space-5) + var(--ui-safe-area-top))
    calc(var(--ui-space-4) + var(--ui-safe-area-right))
    calc(var(--ui-space-5) + var(--ui-safe-area-bottom))
    calc(var(--ui-space-4) + var(--ui-safe-area-left));
  background:
    radial-gradient(circle at 50% 12%, rgb(209 170 98 / 12%), transparent 22rem),
    rgb(5 7 13 / 98%);
  color: var(--ui-color-text-primary);
}

.web-entry :deep(.ui-system-state) {
  width: min(100%, 420px);
  border-style: solid;
  background: rgb(12 16 23 / 92%);
  box-shadow: 0 18px 60px rgb(0 0 0 / 34%);
}
</style>
