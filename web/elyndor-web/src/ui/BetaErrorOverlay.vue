<script setup lang="ts">
import { computed } from 'vue'

import { useGameSessionStore } from '@/stores/gameSession'

const session = useGameSessionStore()
const hasDiagnostic = computed(() => Boolean(session.errorCode))
</script>

<template>
  <aside
    v-if="hasDiagnostic"
    class="beta-error-diagnostic"
    role="alert"
    aria-live="assertive"
  >
    <strong>Ошибка · закрытая бета</strong>
    <span>Код: {{ session.errorCode }}</span>
    <span v-if="session.errorCorrelationId">ID: {{ session.errorCorrelationId }}</span>
  </aside>
</template>

<style scoped>
.beta-error-diagnostic {
  position: fixed;
  z-index: 10000;
  left: max(12px, env(safe-area-inset-left));
  right: max(12px, env(safe-area-inset-right));
  bottom: max(76px, calc(env(safe-area-inset-bottom) + 68px));
  display: grid;
  gap: 3px;
  padding: 10px 12px;
  border: 1px solid rgba(255, 118, 118, 0.55);
  border-radius: 10px;
  background: rgba(36, 8, 10, 0.96);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4);
  color: #ffd7d7;
  font-size: 12px;
  line-height: 1.35;
  overflow-wrap: anywhere;
  pointer-events: none;
}

.beta-error-diagnostic strong {
  color: #ff9b9b;
  font-size: 13px;
}
</style>
