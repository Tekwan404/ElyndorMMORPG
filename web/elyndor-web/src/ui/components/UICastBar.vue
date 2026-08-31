<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{ label: string; elapsed: number; duration: number; interrupted?: boolean }>()
const progress = computed(() =>
  props.duration <= 0 ? 100 : Math.min(100, Math.max(0, (props.elapsed / props.duration) * 100)),
)
</script>

<template>
  <div class="ui-cast" :class="{ 'ui-cast--interrupted': interrupted }" role="progressbar" :aria-label="label" aria-valuemin="0" aria-valuemax="100" :aria-valuenow="progress">
    <span class="ui-cast__fill" :style="{ width: `${progress}%` }" />
    <strong>{{ interrupted ? 'Interrupted' : label }}</strong>
  </div>
</template>

<style scoped>
.ui-cast {
  position: relative;
  display: grid;
  min-height: 2rem;
  place-items: center;
  overflow: hidden;
  border: 1px solid var(--ui-color-border-primary);
  border-radius: var(--ui-radius-sm);
  background: var(--ui-color-surface-raised);
  color: var(--ui-color-text-primary);
}
.ui-cast__fill {
  position: absolute;
  inset: 0 auto 0 0;
  background: color-mix(in srgb, var(--ui-color-primary) 42%, transparent);
  transition: width 100ms linear;
}
.ui-cast strong { z-index: 1; font-size: var(--ui-font-size-sm); }
.ui-cast--interrupted { border-color: var(--ui-color-danger); }
</style>
