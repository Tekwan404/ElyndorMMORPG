<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'

import { useGameSessionStore } from '@/stores/gameSession'

const session = useGameSessionStore()
const visible = ref(false)
const hasDiagnostic = computed(() => Boolean(session.errorCode))
let dismissTimer: ReturnType<typeof setTimeout> | null = null

function clearDismissTimer(): void {
  if (dismissTimer === null) return
  clearTimeout(dismissTimer)
  dismissTimer = null
}

function dismiss(): void {
  clearDismissTimer()
  visible.value = false
}

function scheduleDismiss(): void {
  clearDismissTimer()
  dismissTimer = setTimeout(() => {
    visible.value = false
    dismissTimer = null
  }, 8_000)
}

watch(
  [() => session.errorCode, () => session.errorCorrelationId],
  ([errorCode]) => {
    if (!errorCode) {
      dismiss()
      return
    }

    visible.value = true
    scheduleDismiss()
  },
  { immediate: true },
)

onBeforeUnmount(clearDismissTimer)
</script>

<template>
  <aside
    v-if="hasDiagnostic && visible"
    class="beta-error-diagnostic"
    role="alert"
    aria-live="assertive"
  >
    <div class="beta-error-diagnostic__copy">
      <strong>Ошибка · закрытая бета</strong>
      <span>Код: {{ session.errorCode }}</span>
      <span v-if="session.errorCorrelationId">ID: {{ session.errorCorrelationId }}</span>
    </div>
    <button
      class="beta-error-diagnostic__close"
      type="button"
      aria-label="Закрыть диагностическую ошибку"
      @click="dismiss"
    >×</button>
  </aside>
</template>

<style scoped>
.beta-error-diagnostic {
  position: fixed;
  z-index: 10000;
  left: max(12px, env(safe-area-inset-left));
  right: max(12px, env(safe-area-inset-right));
  bottom: max(76px, calc(env(safe-area-inset-bottom) + 68px));
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 10px;
  padding: 10px 10px 10px 12px;
  border: 1px solid rgba(255, 118, 118, 0.55);
  border-radius: 10px;
  background: rgba(36, 8, 10, 0.96);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4);
  color: #ffd7d7;
  font-size: 12px;
  line-height: 1.35;
  overflow-wrap: anywhere;
}

.beta-error-diagnostic__copy {
  display: grid;
  min-width: 0;
  gap: 3px;
}

.beta-error-diagnostic strong {
  color: #ff9b9b;
  font-size: 13px;
}

.beta-error-diagnostic__close {
  flex: 0 0 auto;
  width: 28px;
  height: 28px;
  padding: 0;
  border: 0;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.08);
  color: #ffd7d7;
  font: inherit;
  font-size: 20px;
  line-height: 1;
  cursor: pointer;
}

.beta-error-diagnostic__close:focus-visible {
  outline: 2px solid #ff9b9b;
  outline-offset: 2px;
}
</style>
