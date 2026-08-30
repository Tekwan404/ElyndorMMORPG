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
  border-bottom: 1px solid var(--ui-color-border);
}
.ui-tabs__tab {
  min-width: var(--ui-touch-target);
  min-height: var(--ui-touch-target);
  padding: var(--ui-space-2) var(--ui-space-3);
  border: 0;
  border-bottom: 2px solid transparent;
  background: transparent;
  color: var(--ui-color-text-muted);
  font: inherit;
  white-space: nowrap;
  cursor: pointer;
}
.ui-tabs__tab[aria-selected='true'] {
  border-bottom-color: var(--ui-color-primary);
  color: var(--ui-color-text-primary);
}
.ui-tabs__tab:disabled {
  color: var(--ui-color-disabled);
  cursor: not-allowed;
}
</style>
