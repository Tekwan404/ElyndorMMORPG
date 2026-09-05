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
  position: relative;
  isolation: isolate;
  display: inline-flex;
  min-width: var(--ui-touch-target);
  min-height: var(--ui-control-height-sm);
  align-items: center;
  justify-content: center;
  gap: var(--ui-space-2);
  overflow: hidden;
  padding: var(--ui-space-2) var(--ui-space-4);
  border: 1px solid transparent;
  border-radius: var(--ui-radius-md);
  box-shadow: var(--ui-shadow-inset);
  font: inherit;
  font-weight: var(--ui-font-weight-semibold);
  color: var(--ui-color-text-primary);
  cursor: pointer;
  transition:
    background var(--ui-transition-fast),
    border-color var(--ui-transition-fast),
    box-shadow var(--ui-transition-fast),
    color var(--ui-transition-fast),
    transform var(--ui-transition-fast),
    opacity var(--ui-transition-fast);
}

.ui-button::after {
  position: absolute;
  z-index: -1;
  inset: 0;
  background: linear-gradient(180deg, rgb(255 255 255 / 7%), transparent 44%);
  content: '';
  opacity: .7;
  pointer-events: none;
}

.ui-button:hover:not(:disabled) {
  transform: translateY(-1px);
}

.ui-button:active:not(:disabled) {
  transform: translateY(0) scale(.99);
}

.ui-button:disabled {
  cursor: not-allowed;
  opacity: .42;
  filter: saturate(.7);
}

.ui-button--primary {
  border-color: rgb(190 184 255 / 34%);
  background: var(--ui-gradient-primary);
  box-shadow:
    var(--ui-shadow-inset),
    0 8px 18px rgb(75 65 170 / 22%);
  color: #090b13;
  text-shadow: 0 1px rgb(255 255 255 / 18%);
}

.ui-button--primary:hover:not(:disabled) {
  border-color: rgb(208 203 255 / 52%);
  box-shadow:
    var(--ui-shadow-inset),
    0 10px 24px rgb(104 91 218 / 30%);
}

.ui-button--secondary {
  border-color: color-mix(in srgb, var(--ui-color-secondary) 48%, var(--ui-color-border));
  background: linear-gradient(180deg, rgb(74 184 207 / 11%), rgb(16 23 37 / 88%));
}

.ui-button--secondary:hover:not(:disabled) {
  border-color: var(--ui-color-secondary);
  background: linear-gradient(180deg, rgb(74 184 207 / 16%), rgb(16 23 37 / 96%));
}

.ui-button--ghost {
  border-color: var(--ui-color-border);
  background: rgb(255 255 255 / 2%);
  color: var(--ui-color-text-secondary);
}

.ui-button--ghost:hover:not(:disabled) {
  border-color: var(--ui-color-border-strong);
  background: rgb(255 255 255 / 4%);
  color: var(--ui-color-text-primary);
}

.ui-button--danger {
  border-color: color-mix(in srgb, var(--ui-color-danger) 72%, white 8%);
  background: linear-gradient(135deg, #df667a, #ae4054);
  color: #0b080a;
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
  to { transform: rotate(360deg); }
}
</style>
