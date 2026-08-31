<script setup lang="ts">
import { computed } from 'vue'

import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { IconConfig } from '@/ui/icons/icon.types'

const props = withDefaults(
  defineProps<{
    icon: IconConfig
    label: string
    resourceCost?: number
    cooldownRemaining?: number
    cooldownTotal?: number
    disabled?: boolean
  }>(),
  { resourceCost: 0, cooldownRemaining: 0, cooldownTotal: 0, disabled: false },
)
defineEmits<{ activate: [] }>()

const cooldownPercent = computed(() => {
  if (props.cooldownTotal <= 0) return 0
  return Math.min(100, Math.max(0, (props.cooldownRemaining / props.cooldownTotal) * 100))
})
const unavailable = computed(() => props.disabled || cooldownPercent.value > 0)
</script>

<template>
  <button
    class="ui-ability"
    type="button"
    :disabled="unavailable"
    :aria-label="`${label}${resourceCost > 0 ? `, cost ${resourceCost}` : ''}`"
    @click="$emit('activate')"
  >
    <span class="ui-ability__icon">
      <IconGenerator :config="icon" />
      <span
        v-if="cooldownPercent > 0"
        class="ui-ability__cooldown"
        :style="{ '--cooldown': `${cooldownPercent}%` }"
        aria-hidden="true"
      />
      <strong v-if="cooldownRemaining > 0" class="ui-ability__seconds">
        {{ Math.ceil(cooldownRemaining) }}
      </strong>
      <span v-if="resourceCost > 0" class="ui-ability__cost">{{ resourceCost }}</span>
    </span>
    <span class="ui-ability__label">{{ label }}</span>
  </button>
</template>

<style scoped>
.ui-ability {
  display: grid;
  width: var(--ui-icon-slot-lg);
  gap: var(--ui-space-1);
  padding: 0;
  border: 0;
  background: transparent;
  color: var(--ui-color-text-secondary);
  font: inherit;
  font-size: var(--ui-font-size-xs);
  cursor: pointer;
}
.ui-ability:disabled {
  opacity: 0.68;
  cursor: not-allowed;
}
.ui-ability__icon {
  position: relative;
  width: var(--ui-icon-slot-lg);
  height: var(--ui-icon-slot-lg);
}
.ui-ability__cooldown {
  position: absolute;
  inset: 0;
  border-radius: var(--ui-radius-sm);
  background: conic-gradient(var(--ui-color-overlay) var(--cooldown), transparent 0);
}
.ui-ability__seconds,
.ui-ability__cost {
  position: absolute;
  z-index: 1;
  color: var(--ui-color-text-primary);
  text-shadow: 0 1px 3px var(--ui-color-overlay);
}
.ui-ability__seconds {
  inset: 50% auto auto 50%;
  translate: -50% -50%;
}
.ui-ability__cost {
  right: var(--ui-space-1);
  bottom: var(--ui-space-1);
  color: var(--ui-color-primary);
  font-weight: var(--ui-font-weight-bold);
}
.ui-ability__label {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
