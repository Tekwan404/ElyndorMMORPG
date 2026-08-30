<script setup lang="ts">
withDefaults(
  defineProps<{ state: 'loading' | 'empty' | 'error'; title?: string; message?: string }>(),
  {
    title: '',
    message: '',
  },
)
</script>

<template>
  <div
    class="ui-system-state"
    :class="`ui-system-state--${state}`"
    :role="state === 'error' ? 'alert' : 'status'"
  >
    <span v-if="state === 'loading'" class="ui-system-state__spinner" aria-hidden="true" />
    <span v-else class="ui-system-state__mark" aria-hidden="true">{{
      state === 'empty' ? '—' : '!'
    }}</span>
    <strong>{{
      title ||
      (state === 'loading'
        ? 'Loading'
        : state === 'empty'
          ? 'Nothing here yet'
          : 'Something went wrong')
    }}</strong>
    <p v-if="message">{{ message }}</p>
    <slot />
  </div>
</template>

<style scoped>
.ui-system-state {
  display: grid;
  min-height: calc(var(--ui-touch-target) * 3);
  place-items: center;
  align-content: center;
  gap: var(--ui-space-2);
  padding: var(--ui-space-5);
  border: 1px dashed var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  color: var(--ui-color-text-muted);
  text-align: center;
}
.ui-system-state strong {
  color: var(--ui-color-text-primary);
}
.ui-system-state p {
  margin: 0;
}
.ui-system-state__spinner {
  width: var(--ui-space-6);
  height: var(--ui-space-6);
  border: 2px solid var(--ui-color-border-strong);
  border-top-color: var(--ui-color-primary);
  border-radius: var(--ui-radius-round);
  animation: ui-state-spin var(--ui-transition-slow) infinite linear;
}
.ui-system-state__mark {
  display: grid;
  width: var(--ui-space-6);
  height: var(--ui-space-6);
  place-items: center;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: var(--ui-radius-round);
}
.ui-system-state--error .ui-system-state__mark {
  border-color: var(--ui-color-danger);
  color: var(--ui-color-danger);
}
@keyframes ui-state-spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
