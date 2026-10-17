<script setup lang="ts">
import { computed } from 'vue'
import atlas from '@/assets/npc/undead-town-atlas.png'
import { useGameSessionStore } from '@/stores/gameSession'

defineProps<{ humanArt: string; role: string; name: string }>()
const session = useGameSessionStore()
const isUndead = computed(() => session.snapshot?.character?.raceId === 'UNDEAD')
const positions: Record<string, string> = {
  blacksmith: '0% 0%',
  huntMaster: '50% 0%',
  cartographer: '50% 0%',
  alchemist: '100% 0%',
  marcus: '0% 100%',
  quartermaster: '0% 100%',
  combatTrainer: '50% 100%',
  registrar: '100% 100%',
  innkeeper: '100% 100%',
}
</script>

<template>
  <span
    v-if="isUndead"
    class="town-npc"
    role="img"
    :aria-label="name"
    :style="{
      backgroundImage: `url(${atlas})`,
      backgroundPosition: positions[role] ?? '100% 100%',
    }"
  />
  <img v-else :src="humanArt" :alt="name" loading="lazy" />
</template>

<style scoped>
.town-npc {
  display: block;
  width: 100%;
  height: 100%;
  min-height: inherit;
  background-size: 300% 200%;
  background-repeat: no-repeat;
}
</style>
