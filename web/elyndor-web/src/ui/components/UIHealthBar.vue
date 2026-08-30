<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    value: number
    max: number
    label?: string
    tone?: 'hp' | 'rage' | 'focus' | 'mana'
    showValue?: boolean
  }>(),
  { tone: 'hp', showValue: true },
)

const safeMax = computed(() => Math.max(0, props.max))
const safeValue = computed(() => Math.min(Math.max(0, props.value), safeMax.value))
const percentage = computed(() =>
  safeMax.value === 0 ? 0 : (safeValue.value / safeMax.value) * 100,
)
</script>

<template>
  <div class="ui-bar" :class="`ui-bar--${tone}`">
    <div v-if="label || showValue" class="ui-bar__meta">
      <span>{{ label }}</span
      ><span v-if="showValue">{{ safeValue }} / {{ safeMax }}</span>
    </div>
    <div
      class="ui-bar__track"
      role="progressbar"
      :aria-label="label"
      :aria-valuemin="0"
      :aria-valuemax="safeMax"
      :aria-valuenow="safeValue"
    >
      <span class="ui-bar__fill" :style="{ width: `${percentage}%` }" />
    </div>
  </div>
</template>

<style scoped>
.ui-bar {
  --ui-bar-color: var(--ui-color-hp);
  display: grid;
  gap: var(--ui-space-1);
}
.ui-bar--rage {
  --ui-bar-color: var(--ui-color-rage);
}
.ui-bar--focus {
  --ui-bar-color: var(--ui-color-focus-resource);
}
.ui-bar--mana {
  --ui-bar-color: var(--ui-color-mana);
}
.ui-bar__meta {
  display: flex;
  justify-content: space-between;
  gap: var(--ui-space-3);
  color: var(--ui-color-text-secondary);
  font-size: var(--ui-font-size-xs);
}
.ui-bar__track {
  height: var(--ui-space-2);
  overflow: hidden;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-round);
  background: var(--ui-color-background);
}
.ui-bar__fill {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: var(--ui-bar-color);
  transition: width var(--ui-transition-normal);
}
</style>
