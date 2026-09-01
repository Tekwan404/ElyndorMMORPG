<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import { gameArt } from '@/assets/gameArt'
import CombatView from '@/game/combat/views/CombatView.vue'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UICard, UILoadingState, UIPanel, UIToast } from '@/ui/components'

type CombatResult = 'Victory' | 'Defeat' | 'Cancelled'

const WHISPERING_FOREST_ID = 'WHISPERING_FOREST'
const WOLF_ID = 'WOLF'

const session = useGameSessionStore()
const combat = useCombatSessionStore()
const encounterOpen = ref(false)
const lastCombatResult = ref<CombatResult | null>(null)

const world = computed(() => session.snapshot?.world)
const character = computed(() => session.snapshot?.character)
const currentLocationId = computed(() => world.value?.currentLocation.id)
const isWhisperingForest = computed(() => currentLocationId.value === WHISPERING_FOREST_ID)
const sceneBackground = computed(() => {
  const dangerLevel = world.value?.currentLocation.dangerLevel
  if (dangerLevel === 'DANGEROUS') return gameArt.world.ruins
  if (dangerLevel === 'ADVENTURE') return gameArt.world.forest
  return gameArt.world.capital
})

function explore(): void {
  if (!isWhisperingForest.value || combat.isActive) return
  lastCombatResult.value = null
  encounterOpen.value = true
}

async function startEncounterCombat(): Promise<void> {
  if (!isWhisperingForest.value || combat.pending) return
  const started = await combat.startCombat(WOLF_ID)
  if (started) {
    encounterOpen.value = false
    lastCombatResult.value = null
  }
}

function cancelEncounter(): void {
  encounterOpen.value = false
}

async function leaveCombat(): Promise<void> {
  const left = await combat.leave()
  if (left) {
    lastCombatResult.value = 'Cancelled'
  }
}

async function restoreCombat(): Promise<void> {
  try {
    await combat.connect()
    await combat.resume()
  } catch {
    // World navigation must stay usable when the realtime channel is temporarily unavailable.
  }
}

watch(
  currentLocationId,
  (locationId, previousLocationId) => {
    if (locationId !== previousLocationId) {
      encounterOpen.value = false
      lastCombatResult.value = null
    }
    if (locationId === WHISPERING_FOREST_ID) {
      void restoreCombat()
    }
  },
  { immediate: true },
)

watch(
  () => combat.snapshot?.status,
  (status) => {
    if (status === 'Victory' || status === 'Defeat') {
      encounterOpen.value = false
      lastCombatResult.value = status
    }
  },
)
</script>

<template>
  <CombatView v-if="combat.isActive" @leave="leaveCombat" />

  <section v-else-if="world && character" class="world">
    <header class="world__header">
      <p class="kicker">{{ world.currentLocation.dangerLevel }}</p>
      <h1>{{ world.currentLocation.displayName }}</h1>
      <p class="hero">{{ character.name }} · уровень {{ character.level }}</p>
    </header>

    <div
      class="scene"
      :style="{ backgroundImage: `url(${sceneBackground})` }"
      role="img"
      :aria-label="world.currentLocation.displayName"
    />

    <UIToast
      v-if="lastCombatResult === 'Victory'"
      tone="success"
      title="Победа"
      data-combat-result
    >
      Волк повержен. Вы снова можете исследовать Шепчущий лес.
    </UIToast>
    <UIToast
      v-else-if="lastCombatResult === 'Defeat'"
      tone="danger"
      title="Поражение"
      data-combat-result
    >
      Бой завершён. Вы вернулись в текущую локацию.
    </UIToast>
    <UIToast
      v-else-if="lastCombatResult === 'Cancelled'"
      tone="info"
      title="Бой прерван"
      data-combat-result
    >
      Вы покинули бой и вернулись к исследованию.
    </UIToast>

    <UICard v-if="encounterOpen" class="encounter" data-world-encounter>
      <div class="encounter__copy">
        <p class="encounter__eyebrow">Обнаружен противник</p>
        <h2>Волк</h2>
        <p>Дикий зверь вышел на тропу. Бой начнётся только после вашего решения.</p>
      </div>
      <div class="encounter__actions">
        <UIButton
          data-start-encounter
          :loading="combat.pending"
          :disabled="combat.pending"
          @click="startEncounterCombat"
        >
          Начать бой
        </UIButton>
        <UIButton variant="ghost" :disabled="combat.pending" @click="cancelEncounter">
          Уйти
        </UIButton>
      </div>
    </UICard>

    <UIPanel v-else-if="isWhisperingForest" class="exploration">
      <template #title>Исследование</template>
      <p>Осмотрите окрестности и найдите противника.</p>
      <UIButton
        data-explore
        variant="secondary"
        :disabled="combat.pending"
        @click="explore"
      >
        Исследовать
      </UIButton>
    </UIPanel>

    <UIPanel v-if="!encounterOpen" class="paths">
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
    <div v-if="combat.errorCode" role="alert">
      <UIToast tone="danger">{{ combat.errorCode }}</UIToast>
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
.world__header { text-align: center; }
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
.hero { margin: 0; color: var(--ui-color-text-muted); }
.scene {
  min-height: clamp(12rem, 46vw, 18rem);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-lg);
  background-color: var(--ui-color-surface-1);
  background-position: center;
  background-size: cover;
  box-shadow: inset 0 -5rem 5rem rgb(7 9 17 / 48%), var(--ui-shadow-panel);
}
.exploration,
.paths { box-shadow: none; }
.exploration p { margin-top: 0; color: var(--ui-color-text-muted); }
.encounter {
  display: grid;
  gap: var(--ui-space-4);
  border-color: var(--ui-color-warning);
}
.encounter__copy { display: grid; gap: var(--ui-space-1); }
.encounter__copy h2,
.encounter__copy p { margin: 0; }
.encounter__copy h2 { color: var(--ui-color-text-primary); font-family: var(--ui-font-display); }
.encounter__copy p:not(.encounter__eyebrow) {
  color: var(--ui-color-text-muted);
  line-height: var(--ui-line-height-normal);
}
.encounter__eyebrow {
  color: var(--ui-color-warning);
  font-size: var(--ui-font-size-xs);
  font-weight: var(--ui-font-weight-bold);
  letter-spacing: 0.08em;
  text-transform: uppercase;
}
.encounter__actions {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--ui-space-2);
}
.paths__list { display: grid; gap: var(--ui-space-3); }
.path-card {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--ui-space-3);
}
.path-card__copy { display: grid; min-width: 0; gap: var(--ui-space-1); }
.path-card__copy strong { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.path-card__copy small { color: var(--ui-color-text-muted); line-height: var(--ui-line-height-normal); }
@media (max-width: 360px) {
  .world {
    padding-inline: calc(var(--ui-space-3) + var(--ui-safe-area-left))
      calc(var(--ui-space-3) + var(--ui-safe-area-right));
  }
  .path-card,
  .encounter__actions { grid-template-columns: 1fr; }
}
</style>
