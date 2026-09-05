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
    @click="interactive && !disabled && $emit('activate')"
  >
    <slot />
  </component>
</template>

<style scoped>
.ui-card {
  display: block;
  width: 100%;
  min-height: var(--ui-touch-target);
  padding: var(--ui-space-4);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-lg);
  background: var(--ui-gradient-panel);
  box-shadow: var(--ui-shadow-inset);
  color: var(--ui-color-text-primary);
  text-align: left;
}

.ui-card--interactive {
  font: inherit;
  cursor: pointer;
  transition:
    border-color var(--ui-transition-fast),
    background var(--ui-transition-fast),
    box-shadow var(--ui-transition-fast),
    transform var(--ui-transition-fast);
}

.ui-card--interactive:hover:not(:disabled) {
  border-color: var(--ui-color-border-strong);
  box-shadow: var(--ui-shadow-inset), var(--ui-shadow-elevated);
  transform: translateY(-1px);
}

.ui-card--selected {
  border-color: color-mix(in srgb, var(--ui-color-primary) 78%, white 8%);
  box-shadow: var(--ui-shadow-inset), var(--ui-glow-selected);
}

.ui-card:disabled,
.ui-card[aria-disabled='true'] {
  opacity: .42;
  cursor: not-allowed;
  filter: saturate(.7);
}
</style>
