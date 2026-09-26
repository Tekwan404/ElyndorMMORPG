<script setup lang="ts">
import { computed } from 'vue'

import DungeonLocationCard from '@/game/world/components/DungeonLocationCard.vue'
import LocationOverview from '@/game/world/components/LocationOverview.vue'
import { locationKind } from '@/game/world/locationPresentation'
import { useGameSessionStore } from '@/stores/gameSession'

import WorldViewLegacy from './WorldViewLegacy.vue'

const props = withDefaults(defineProps<{ openGuild?: boolean }>(), { openGuild: false })
const emit = defineEmits<{ 'open-party': [] }>()
const session = useGameSessionStore()
const currentLocationId = computed(() => session.snapshot?.world?.currentLocation.id ?? '')
const isDungeonLocation = computed(() => locationKind(currentLocationId.value) === 'dungeon')
</script>

<template>
  <section class="location-screen">
    <LocationOverview>
      <template v-if="isDungeonLocation && currentLocationId" #primary-actions>
        <DungeonLocationCard
          :dungeon-id="currentLocationId"
          @open-party="emit('open-party')"
        />
      </template>
    </LocationOverview>
    <div class="location-screen__systems">
      <WorldViewLegacy
        :open-guild="props.openGuild"
        :show-dungeon-location-card="false"
        @open-party="emit('open-party')"
      />
    </div>
  </section>
</template>

<style scoped>
.location-screen {
  display: grid;
  width: 100%;
  max-width: var(--ui-content-width-tablet);
  min-width: 0;
  margin-inline: auto;
  gap: var(--ui-space-3);
  padding: var(--ui-space-3) var(--ui-space-4) var(--ui-space-7);
}

.location-screen__systems {
  min-width: 0;
}

.location-screen__systems :deep(.world) {
  width: 100%;
  max-width: none;
  margin: 0;
  padding: 0;
}

.location-screen__systems :deep(.scene) {
  display: none;
}

@media (max-width: 520px) {
  .location-screen {
    padding: var(--ui-space-3);
    padding-bottom: var(--ui-space-6);
  }
}

@media (min-width: 720px) {
  .location-screen {
    gap: var(--ui-space-4);
    padding-inline: clamp(16px, 2.4vw, 28px);
  }
}
</style>
