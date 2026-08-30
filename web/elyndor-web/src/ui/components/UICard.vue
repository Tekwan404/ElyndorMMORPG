<script setup lang="ts">
withDefaults(defineProps<{ interactive?: boolean; selected?: boolean; disabled?: boolean }>(), {
  interactive: false,
  selected: false,
  disabled: false,
})
defineEmits<{ activate: [] }>()
</script>

<template>
  <component
    :is="interactive ? 'button' : 'article'"
    class="ui-card"
    :class="{ 'ui-card--interactive': interactive, 'ui-card--selected': selected }"
    :type="interactive ? 'button' : undefined"
    :disabled="interactive && disabled ? true : undefined"
    :aria-pressed="interactive ? selected : undefined"
    :aria-disabled="!interactive && disabled ? 'true' : undefined"
    @click="!disabled && $emit('activate')"
  >
    <slot />
  </component>
</template>

<style scoped>
.ui-card {
  display: block;
  width: 100%;
  min-height: var(--ui-touch-target);
  padding: var(--ui-space-3);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: var(--ui-color-surface-2);
  color: var(--ui-color-text-primary);
  text-align: left;
}
.ui-card--interactive {
  font: inherit;
  cursor: pointer;
  transition:
    border-color var(--ui-transition-fast),
    background var(--ui-transition-fast),
    box-shadow var(--ui-transition-fast);
}
.ui-card--interactive:hover:not(:disabled) {
  border-color: var(--ui-color-border-strong);
  background: var(--ui-color-surface-3);
}
.ui-card--selected {
  border-color: var(--ui-color-primary);
  box-shadow: var(--ui-glow-selected);
}
.ui-card:disabled,
.ui-card[aria-disabled='true'] {
  opacity: 0.45;
  cursor: not-allowed;
}
</style>
