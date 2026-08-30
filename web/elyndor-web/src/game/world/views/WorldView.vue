<script setup lang="ts">
import { computed } from 'vue'

import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UICard, UILoadingState, UIPanel, UIToast } from '@/ui/components'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { IconConfig } from '@/ui/icons/icon.types'

const session = useGameSessionStore()
const world = computed(() => session.snapshot?.world)
const character = computed(() => session.snapshot?.character)
const locationIcon = computed<IconConfig>(() => ({
  id: `location-${world.value?.currentLocation.id ?? 'unknown'}`,
  glyph: world.value?.currentLocation.dangerLevel === 'DANGEROUS' ? 'skull' : 'star',
  category: 'utility',
  modifier:
    world.value?.currentLocation.dangerLevel === 'DANGEROUS'
      ? 'fire'
      : world.value?.currentLocation.dangerLevel === 'ADVENTURE'
        ? 'shadow'
        : 'holy',
}))
</script>

<template>
  <section v-if="world && character" class="world">
    <header class="world__header">
      <p class="kicker">{{ world.currentLocation.dangerLevel }}</p>
      <h1>{{ world.currentLocation.displayName }}</h1>
      <p class="hero">{{ character.name }} · уровень {{ character.level }}</p>
    </header>

    <div class="scene" aria-hidden="true">
      <IconGenerator class="scene__icon" :config="locationIcon" />
    </div>

    <UIPanel class="paths">
      <template #title>Доступные пути</template>
      <div v-if="world.outgoingTransitions.length > 0" class="paths__list">
        <UICard v-for="location in world.outgoingTransitions" :key="location.id" class="path-card">
          <div class="path-card__copy">
            <strong>{{ location.displayName }}</strong>
            <small
              >Опасность: {{ location.dangerLevel }} · рекомендован ур.
              {{ location.recommendedLevel }}</small
            >
          </div>
          <UIButton
            :data-travel="location.id"
            :aria-label="`Отправиться: ${location.displayName}`"
            variant="secondary"
            :loading="session.mutationPending"
            :disabled="session.mutationPending"
            @click="session.travel(location.id)"
          >
            Отправиться
          </UIButton>
        </UICard>
      </div>
      <UILoadingState
        v-else
        state="empty"
        title="Пути не найдены"
        message="Эта область пока не открывает новых направлений."
      />
    </UIPanel>

    <div v-if="session.errorCode" role="alert">
      <UIToast tone="danger">{{ session.errorCode }}</UIToast>
    </div>
  </section>
</template>

<style scoped>
.world {
  display: grid;
  width: min(100%, var(--ui-content-width));
  margin-inline: auto;
  gap: var(--ui-space-4);
  padding: var(--ui-space-6) calc(var(--ui-space-4) + var(--ui-safe-area-right)) var(--ui-space-7)
    calc(var(--ui-space-4) + var(--ui-safe-area-left));
}
.world__header {
  text-align: center;
}
.kicker {
  margin: 0;
  color: var(--ui-color-secondary);
  font-size: var(--ui-font-size-xs);
  font-weight: var(--ui-font-weight-bold);
  letter-spacing: var(--ui-space-1);
}
h1 {
  margin: var(--ui-space-1) 0;
  color: var(--ui-color-text-primary);
  font-family: var(--ui-font-display);
  font-size: clamp(var(--ui-font-size-xl), 9vw, var(--ui-font-size-2xl));
  font-weight: var(--ui-font-weight-semibold);
}
.hero {
  margin: 0;
  color: var(--ui-color-text-muted);
}
.scene {
  display: grid;
  min-height: calc(var(--ui-icon-slot-lg) * 2);
  place-items: center;
  border-block: 1px solid var(--ui-color-border);
  background: var(--ui-color-surface-1);
}
.scene__icon {
  width: var(--ui-icon-slot-lg);
  height: var(--ui-icon-slot-lg);
  box-shadow: var(--ui-glow-magic);
}
.paths {
  box-shadow: none;
}
.paths__list {
  display: grid;
  gap: var(--ui-space-3);
}
.path-card {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--ui-space-3);
}
.path-card__copy {
  display: grid;
  min-width: 0;
  gap: var(--ui-space-1);
}
.path-card__copy strong {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.path-card__copy small {
  color: var(--ui-color-text-muted);
  line-height: var(--ui-line-height-normal);
}
@media (max-width: 360px) {
  .world {
    padding-inline: calc(var(--ui-space-3) + var(--ui-safe-area-left))
      calc(var(--ui-space-3) + var(--ui-safe-area-right));
  }
  .path-card {
    grid-template-columns: 1fr;
  }
}
</style>
