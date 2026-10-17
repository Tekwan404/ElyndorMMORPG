<script setup lang="ts">
import { computed } from 'vue'
import atlas from '@/assets/npc/market-merchants-atlas.webp'
import undeadAtlas from '@/assets/npc/undead-town-atlas.png'
import { useGameSessionStore } from '@/stores/gameSession'
const props = withDefaults(
  defineProps<{ merchantId: string; name?: string; decorative?: boolean }>(),
  { name: '', decorative: false },
)
const positions: Record<string, string> = {
  BORIN_ARMORY: '0% 0%',
  LIORA_HUNTING: '50% 0%',
  ELARA_ARCANA: '100% 0%',
  MARCUS_SUPPLIES: '0% 100%',
  ARENA_QUARTERMASTER: '50% 100%',
}
const session = useGameSessionStore()
const isUndead = computed(
  () => props.merchantId.startsWith('UNDEAD_') || session.snapshot?.character?.raceId === 'UNDEAD',
)
const baseId = computed(() => props.merchantId.replace(/^UNDEAD_/, ''))
const position = computed(() => positions[baseId.value] ?? positions.MARCUS_SUPPLIES)
</script>

<template>
  <span
    class="merchant-portrait"
    :role="decorative ? 'presentation' : 'img'"
    :aria-label="decorative ? undefined : name"
    :aria-hidden="decorative ? true : undefined"
    :style="{
      backgroundImage: 'url(' + (isUndead ? undeadAtlas : atlas) + ')',
      backgroundPosition: position,
    }"
  />
</template>

<style scoped>
.merchant-portrait {
  display: block;
  width: 100%;
  height: 100%;
  min-height: inherit;
  background-size: 300% 200%;
  background-repeat: no-repeat;
  background-color: #281e17;
}
</style>
