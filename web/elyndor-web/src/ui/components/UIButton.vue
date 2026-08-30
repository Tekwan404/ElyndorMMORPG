<script setup lang="ts">
withDefaults(
  defineProps<{
    variant?: 'primary' | 'secondary' | 'ghost' | 'danger'
    type?: 'button' | 'submit' | 'reset'
    disabled?: boolean
    loading?: boolean
  }>(),
  { variant: 'primary', type: 'button' },
)
</script>

<template>
  <button
    class="ui-button"
    :class="`ui-button--${variant}`"
    :type="type"
    :disabled="disabled || loading"
    :aria-busy="loading ? 'true' : undefined"
  >
    <span v-if="loading" class="ui-button__spinner" aria-hidden="true" />
    <span><slot /></span>
  </button>
</template>

<style scoped>
.ui-button {
  display: inline-flex;
  min-width: var(--ui-touch-target);
  min-height: var(--ui-control-height-sm);
  align-items: center;
  justify-content: center;
  gap: var(--ui-space-2);
  padding: var(--ui-space-2) var(--ui-space-4);
  border: 1px solid transparent;
  border-radius: var(--ui-radius-md);
  font: inherit;
  font-weight: var(--ui-font-weight-semibold);
  color: var(--ui-color-text-primary);
  cursor: pointer;
  transition:
    background var(--ui-transition-fast),
    border-color var(--ui-transition-fast),
    transform var(--ui-transition-fast),
    opacity var(--ui-transition-fast);
}

.ui-button:active:not(:disabled) {
  transform: translateY(1px);
}
.ui-button:disabled {
  cursor: not-allowed;
  opacity: 0.48;
}
.ui-button--primary {
  background: var(--ui-color-primary);
  color: var(--ui-color-text-inverse);
}
.ui-button--primary:hover:not(:disabled) {
  background: var(--ui-color-primary-hover);
}
.ui-button--secondary {
  border-color: var(--ui-color-secondary);
  background: var(--ui-color-surface-2);
}
.ui-button--ghost {
  border-color: var(--ui-color-border);
  background: transparent;
}
.ui-button--danger {
  background: var(--ui-color-danger);
  color: var(--ui-color-text-inverse);
}

.ui-button__spinner {
  width: var(--ui-space-4);
  height: var(--ui-space-4);
  border: 2px solid currentColor;
  border-right-color: transparent;
  border-radius: var(--ui-radius-round);
  animation: ui-button-spin var(--ui-animation-spin-duration) infinite linear;
}

@keyframes ui-button-spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
