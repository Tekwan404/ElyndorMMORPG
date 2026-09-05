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
  gap: 3px;
}

.ui-bar--rage { --ui-bar-color: var(--ui-color-rage); }
.ui-bar--focus { --ui-bar-color: var(--ui-color-focus-resource); }
.ui-bar--mana { --ui-bar-color: var(--ui-color-mana); }

.ui-bar__meta {
  display: flex;
  justify-content: space-between;
  gap: var(--ui-space-3);
  color: var(--ui-color-text-secondary);
  font-size: var(--ui-font-size-xs);
  font-variant-numeric: tabular-nums;
}

.ui-bar__meta span:first-child {
  color: var(--ui-color-text-muted);
  font-weight: var(--ui-font-weight-medium);
}

.ui-bar__meta span:last-child {
  color: var(--ui-color-text-primary);
}

.ui-bar__track {
  height: 9px;
  overflow: hidden;
  border: 1px solid rgb(255 255 255 / 7%);
  border-radius: var(--ui-radius-round);
  background: rgb(2 4 8 / 82%);
  box-shadow: inset 0 1px 3px rgb(0 0 0 / 55%);
}

.ui-bar__fill {
  position: relative;
  display: block;
  height: 100%;
  border-radius: inherit;
  background:
    linear-gradient(180deg, rgb(255 255 255 / 20%), transparent 48%),
    linear-gradient(90deg, color-mix(in srgb, var(--ui-bar-color) 72%, black), var(--ui-bar-color));
  box-shadow: 0 0 10px color-mix(in srgb, var(--ui-bar-color) 35%, transparent);
  transition: width var(--ui-transition-normal);
}

.ui-bar__fill::after {
  position: absolute;
  inset: 0;
  background: linear-gradient(90deg, transparent, rgb(255 255 255 / 14%), transparent);
  content: '';
  opacity: .45;
}
</style>
