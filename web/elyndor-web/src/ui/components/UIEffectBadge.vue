<script setup lang="ts">
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { IconConfig } from '@/ui/icons/icon.types'

withDefaults(defineProps<{ icon: IconConfig; label: string; stacks?: number; remainingSeconds: number; harmful?: boolean }>(), { stacks: 1, harmful: false })
</script>

<template>
  <div class="ui-effect" :class="{ 'ui-effect--harmful': harmful }" :aria-label="`${label}, ${stacks} stack, ${Math.ceil(remainingSeconds)} seconds`">
    <IconGenerator class="ui-effect__icon" :config="icon" />
    <strong v-if="stacks > 1" class="ui-effect__stacks">{{ stacks }}</strong>
    <span class="ui-effect__time">{{ Math.ceil(remainingSeconds) }}s</span>
  </div>
</template>

<style scoped>
.ui-effect {
  position: relative;
  width: var(--ui-icon-slot-sm);
  color: var(--ui-color-primary);
  text-align: center;
  font-size: var(--ui-font-size-xs);
}
.ui-effect--harmful { color: var(--ui-color-danger); }
.ui-effect__icon { width: var(--ui-icon-slot-sm); height: var(--ui-icon-slot-sm); }
.ui-effect__stacks {
  position: absolute;
  top: 0;
  right: 0;
  padding-inline: 0.2rem;
  border-radius: var(--ui-radius-xs);
  background: var(--ui-color-overlay);
  color: var(--ui-color-text-primary);
}
.ui-effect__time { display: block; margin-top: var(--ui-space-1); }
</style>
