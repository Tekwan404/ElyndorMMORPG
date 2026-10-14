<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{ styleId?: string | null }>()
const style = computed(() => ['ash', 'ether', 'iron'].includes(props.styleId ?? '') ? props.styleId : null)
</script>

<template>
  <span class="profile-frame" :class="style ? 'profile-frame--' + style : undefined" :data-profile-frame="style ?? undefined">
    <slot />
    <span v-if="style" class="profile-frame__ornament" aria-hidden="true">
      <i v-for="corner in ['nw', 'ne', 'se', 'sw']" :key="corner" class="profile-frame__corner" :class="'profile-frame__corner--' + corner" />
    </span>
  </span>
</template>

<style scoped>
.profile-frame {
  position: relative;
  display: inline-grid;
  place-items: center;
  min-width: 0;
  border-radius: inherit;
  isolation: isolate;
}
.profile-frame[data-profile-frame] {
  border: 1px solid var(--frame-metal);
  box-shadow: inset 0 0 0 2px #080d16, 0 0 7px var(--frame-shadow);
}
.profile-frame--ash { --frame-metal: #cb9663; --frame-shadow: #b263322b; }
.profile-frame--ether { --frame-metal: #b6a0de; --frame-shadow: #8865c43b; }
.profile-frame--iron { --frame-metal: #b9bdc6; --frame-shadow: #a193642b; }
.profile-frame__ornament {
  position: absolute;
  z-index: 2;
  inset: 0;
  border-radius: inherit;
  pointer-events: none;
}
.profile-frame__corner {
  position: absolute;
  width: 10px;
  height: 10px;
  border-top: 2px solid var(--frame-metal);
  border-left: 2px solid var(--frame-metal);
  border-radius: 2px 0 0;
  filter: drop-shadow(0 1px 1px #05080d);
}
.profile-frame__corner--nw { top: -2px; left: -2px; }
.profile-frame__corner--ne { top: -2px; right: -2px; transform: rotate(90deg); }
.profile-frame__corner--se { bottom: -2px; right: -2px; transform: rotate(180deg); }
.profile-frame__corner--sw { bottom: -2px; left: -2px; transform: rotate(270deg); }
.profile-frame--ether .profile-frame__corner::after {
  position: absolute;
  top: -3px;
  left: -3px;
  width: 4px;
  height: 4px;
  background: #d0b8f2;
  box-shadow: 0 0 4px #ad8ce477;
  transform: rotate(45deg);
  content: '';
}
.profile-frame--iron .profile-frame__corner { border-width: 3px; border-color: #d0c498; }
.profile-frame--ash .profile-frame__corner { border-radius: 5px 0 0; }
</style>
