<script setup lang="ts">
import { computed } from 'vue'
const props = defineProps<{ styleId?: string | null }>()
const style = computed(() => ['ash', 'ether', 'iron'].includes(props.styleId ?? '') ? props.styleId : null)
</script>

<template>
  <span class="profile-frame" :class="style ? 'profile-frame--' + style : undefined" :data-profile-frame="style ?? undefined">
    <slot />
    <svg v-if="style" class="profile-frame__ornament" viewBox="0 0 100 100" preserveAspectRatio="none" aria-hidden="true">
      <path d="M3 29V3h26M71 3h26v26M97 71v26H71M29 97H3V71" />
      <path v-if="style === 'ether'" d="M50 0l5 6-5 6-5-6zM50 88l5 6-5 6-5-6zM0 50l6-5 6 5-6 5zM88 50l6-5 6 5-6 5z" />
      <path v-else-if="style === 'iron'" d="M4 18L18 4M82 4l14 14M96 82L82 96M18 96L4 82M44 1l6 8 6-8" />
      <path v-else d="M37 2l7 5 6-4 6 4 7-5M37 98l7-5 6 4 6-4 7 5" />
    </svg>
  </span>
</template>

<style scoped>
.profile-frame { --frame-metal: #c4aa78; position: relative; display: inline-grid; place-items: center; border-radius: inherit; min-width: 0; }
.profile-frame--ash { --frame-metal: #c9854d; border: 3px double #c9854d; box-shadow: inset 0 0 10px #b94f2633, 0 0 9px #9f50252e; }
.profile-frame--ether { --frame-metal: #c5a0fa; border: 3px double #bca2e8; box-shadow: inset 0 0 10px #a27be344, 0 0 12px #8c68ca44; }
.profile-frame--iron { --frame-metal: #d7c795; border: 3px ridge #a3aebc; box-shadow: inset 0 0 0 2px #4d5662, 0 0 10px #b8aa7233; }
.profile-frame__ornament { position: absolute; z-index: 2; inset: -4px; width: calc(100% + 8px); height: calc(100% + 8px); overflow: visible; pointer-events: none; fill: var(--frame-metal); stroke: var(--frame-metal); stroke-width: 1.5; }
.profile-frame__ornament path:first-child { fill: none; stroke-width: 2; }
</style>
