<script setup lang="ts">
import { computed } from 'vue'

import IconGenerator from '../icons/IconGenerator.vue'
import type { IconConfig } from '../icons/icon.types'

const props = withDefaults(
  defineProps<{ icon: IconConfig; label: string; quantity?: number; cooldown?: number }>(),
  { quantity: 1, cooldown: 0 },
)
defineEmits<{ activate: [] }>()

const accessibleLabel = computed(() => {
  const details = [props.label]
  if (props.icon.state === 'locked' && props.label.toLocaleLowerCase() !== 'locked') {
    details.push('locked')
  }
  if (props.quantity > 1) details.push(`quantity ${props.quantity}`)
  return details.join(', ')
})
const cooldown = computed(() => Math.min(100, Math.max(0, props.cooldown)))
const unavailable = computed(() => props.icon.state === 'locked' || props.icon.state === 'disabled')
</script>

<template>
  <button
    data-item-slot
    class="ui-item-slot"
    type="button"
    :aria-label="accessibleLabel"
    :disabled="unavailable"
    @click="$emit('activate')"
  >
    <span class="ui-item-slot__icon">
      <IconGenerator :config="icon" />
      <span v-if="quantity > 1" class="ui-item-slot__quantity">{{ quantity }}</span>
      <span
        v-if="cooldown > 0"
        class="ui-item-slot__cooldown"
        :style="{ '--cooldown': `${cooldown}%` }"
        aria-hidden="true"
      />
    </span>
    <span class="ui-item-slot__label">{{ label }}</span>
  </button>
</template>

<style scoped>
.ui-item-slot {
  display: grid;
  min-width: var(--ui-icon-slot-md);
  max-width: var(--ui-icon-slot-lg);
  gap: var(--ui-space-1);
  padding: 0;
  border: 0;
  background: transparent;
  color: var(--ui-color-text-secondary);
  font: inherit;
  font-size: var(--ui-font-size-xs);
  cursor: pointer;
}
.ui-item-slot:disabled {
  cursor: not-allowed;
}
.ui-item-slot__icon {
  position: relative;
  width: var(--ui-icon-slot-md);
  height: var(--ui-icon-slot-md);
}
.ui-item-slot__quantity {
  position: absolute;
  right: var(--ui-space-1);
  bottom: var(--ui-space-1);
  padding-inline: var(--ui-space-1);
  border-radius: var(--ui-radius-xs);
  background: var(--ui-color-overlay);
  color: var(--ui-color-text-primary);
  font-weight: var(--ui-font-weight-bold);
}
.ui-item-slot__cooldown {
  position: absolute;
  inset: 0;
  border-radius: var(--ui-radius-sm);
  background: conic-gradient(var(--ui-color-overlay) var(--cooldown), transparent 0);
  pointer-events: none;
}
.ui-item-slot__label {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
