<script setup lang="ts">
defineProps<{
  modelValue: string
  tabs: readonly { value: string; label: string; disabled?: boolean }[]
  label?: string
}>()
defineEmits<{ 'update:modelValue': [value: string] }>()
</script>

<template>
  <div class="ui-tabs" role="tablist" :aria-label="label">
    <button
      v-for="tab in tabs"
      :key="tab.value"
      class="ui-tabs__tab"
      type="button"
      role="tab"
      :data-tab="tab.value"
      :aria-selected="tab.value === modelValue"
      :disabled="tab.disabled"
      @click="!tab.disabled && $emit('update:modelValue', tab.value)"
    >
      {{ tab.label }}
    </button>
  </div>
</template>

<style scoped>
.ui-tabs {
  display: flex;
  gap: var(--ui-space-1);
  overflow-x: auto;
  padding: 3px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: rgb(3 6 11 / 55%);
}

.ui-tabs__tab {
  min-width: var(--ui-touch-target);
  min-height: calc(var(--ui-touch-target) - 4px);
  padding: var(--ui-space-2) var(--ui-space-3);
  border: 1px solid transparent;
  border-radius: calc(var(--ui-radius-md) - 3px);
  background: transparent;
  color: var(--ui-color-text-muted);
  font: inherit;
  font-size: var(--ui-font-size-sm);
  white-space: nowrap;
  cursor: pointer;
  transition: background var(--ui-transition-fast), color var(--ui-transition-fast), border-color var(--ui-transition-fast);
}

.ui-tabs__tab[aria-selected='true'] {
  border-color: var(--ui-color-border);
  background: linear-gradient(180deg, rgb(146 136 255 / 14%), rgb(255 255 255 / 3%));
  color: var(--ui-color-text-primary);
  box-shadow: var(--ui-shadow-inset);
}

.ui-tabs__tab:disabled {
  color: var(--ui-color-disabled);
  cursor: not-allowed;
  opacity: .55;
}
</style>
