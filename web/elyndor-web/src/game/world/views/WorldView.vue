<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'

import { gameArt } from '@/assets/gameArt'
import CombatView from '@/game/combat/views/CombatView.vue'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UICard, UILoadingState, UIPanel, UIToast } from '@/ui/components'

type CombatResult = 'Victory' | 'Defeat' | 'Cancelled'
interface ForestEncounter {
  id: 'WOLF' | 'FOREST_BOAR' | 'GIANT_SPIDER'
  name: string
  level: number
  description: string
}

const STARTER_TOWN_ID = 'STARTER_TOWN'
const WHISPERING_FOREST_ID = 'WHISPERING_FOREST'
const FOREST_ENCOUNTERS: readonly ForestEncounter[] = [
  {
    id: 'WOLF',
    name: 'Волк',
    level: 3,
    description: 'Дикий волк вышел на тропу и внимательно следит за каждым движением.',
  },
  {
    id: 'FOREST_BOAR',
    name: 'Лесной кабан',
    level: 2,
    description: 'Тяжёлый кабан роет землю копытом и готовится броситься вперёд.',
  },
  {
    id: 'GIANT_SPIDER',
    name: 'Гигантский паук',
    level: 2,
    description: 'Из тёмных корней выполз огромный паук. Он быстрый, но заметно хрупче других зверей.',
  },
]

const session = useGameSessionStore()
const combat = useCombatSessionStore()
const selectedEncounter = ref<ForestEncounter | null>(null)
const lastCombatResult = ref<CombatResult | null>(null)
const lastEnemyName = ref<string | null>(null)
let encounterCursor = -1
let vitalsRefreshTimer: ReturnType<typeof setInterval> | null = null
let vitalsRefreshPending = false

const world = computed(() => session.snapshot?.world)
const character = computed(() => session.snapshot?.character)
const currentLocationId = computed(() => world.value?.currentLocation.id)
const isStarterTown = computed(() => currentLocationId.value === STARTER_TOWN_ID)
const isWhisperingForest = computed(() => currentLocationId.value === WHISPERING_FOREST_ID)
const needsOutOfCombatRefresh = computed(() => {
  const vitals = character.value?.vitals
  if (!vitals || combat.isActive) return false

  const hpRecovering = isStarterTown.value && vitals.currentHp < vitals.maxHp
  const resourceRecovering = vitals.resourceType === 'RAGE'
    ? vitals.currentResource > 0
    : vitals.currentResource < vitals.maxResource
  return hpRecovering || resourceRecovering
})
const recoveryMessage = computed(() => {
  const vitals = character.value?.vitals
  if (!vitals || combat.isActive) return null
  if (isStarterTown.value && vitals.currentHp < vitals.maxHp) {
    return 'Отдых в городе: здоровье восстанавливается по 5 ед. в секунду.'
  }
  if (vitals.resourceType === 'RAGE' && vitals.currentResource > 0) {
    return 'Ярость постепенно угасает вне боя.'
  }
  return null
})
const sceneBackground = computed(() => {
  const dangerLevel = world.value?.currentLocation.dangerLevel
  if (dangerLevel === 'DANGEROUS') return gameArt.world.ruins
  if (dangerLevel === 'ADVENTURE') return gameArt.world.forest
  return gameArt.world.capital
})

function explore(): void {
  if (!isWhisperingForest.value || combat.isActive) return
  lastCombatResult.value = null
  encounterCursor = (encounterCursor + 1) % FOREST_ENCOUNTERS.length
  selectedEncounter.value = FOREST_ENCOUNTERS[encounterCursor] ?? null
}

async function startEncounterCombat(): Promise<void> {
  if (!isWhisperingForest.value || combat.pending || !selectedEncounter.value) return
  const encounter = selectedEncounter.value
  const started = await combat.startCombat(encounter.id)
  if (started) {
    lastEnemyName.value = encounter.name
    selectedEncounter.value = null
    lastCombatResult.value = null
  }
}

function cancelEncounter(): void {
  selectedEncounter.value = null
}

function handleCombatLeft(): void {
  lastCombatResult.value = 'Cancelled'
}

async function restoreCombat(): Promise<void> {
  try {
    await combat.connect()
    await combat.resume()
    if (combat.snapshot) {
      lastEnemyName.value = localizedEnemyName(
        combat.snapshot.enemy.definitionId,
        combat.snapshot.enemy.name,
      )
    }
  } catch {
    // World navigation must stay usable when the realtime channel is temporarily unavailable.
  }
}

async function refreshOutOfCombatVitals(): Promise<void> {
  if (vitalsRefreshPending || combat.isActive || session.mutationPending) return
  vitalsRefreshPending = true
  try {
    await session.refreshSnapshot()
  } catch {
    // Background recovery refresh must never make the world unusable.
  } finally {
    vitalsRefreshPending = false
  }
}

function syncVitalsRefreshTimer(enabled: boolean): void {
  if (enabled && vitalsRefreshTimer === null) {
    vitalsRefreshTimer = setInterval(() => void refreshOutOfCombatVitals(), 1000)
    return
  }
  if (!enabled && vitalsRefreshTimer !== null) {
    clearInterval(vitalsRefreshTimer)
    vitalsRefreshTimer = null
  }
}

function localizedEnemyName(definitionId: string, fallback: string): string {
  return FOREST_ENCOUNTERS.find((encounter) => encounter.id === definitionId)?.name ?? fallback
}

watch(
  currentLocationId,
  (locationId, previousLocationId) => {
    if (locationId !== previousLocationId) {
      selectedEncounter.value = null
      encounterCursor = -1
      const isDefeatRespawn = lastCombatResult.value === 'Defeat' && locationId === STARTER_TOWN_ID
      if (!isDefeatRespawn) {
        lastCombatResult.value = null
        lastEnemyName.value = null
      }
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
      selectedEncounter.value = null
      if (combat.snapshot) {
        lastEnemyName.value = localizedEnemyName(
          combat.snapshot.enemy.definitionId,
          combat.snapshot.enemy.name,
        )
      }
      lastCombatResult.value = status
      void session.refreshSnapshot()
    }
  },
)

watch(needsOutOfCombatRefresh, syncVitalsRefreshTimer, { immediate: true })
onBeforeUnmount(() => syncVitalsRefreshTimer(false))
</script>

<template>
  <CombatView v-if="combat.isActive" @leave="handleCombatLeft" />

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

    <UIToast v-if="recoveryMessage" tone="info" title="Восстановление" data-world-recovery>
      {{ recoveryMessage }}
    </UIToast>

    <UIToast
      v-if="lastCombatResult === 'Victory'"
      tone="success"
      title="Победа"
      data-combat-result
    >
      {{ lastEnemyName ?? 'Противник' }} повержен. Вы снова можете исследовать Шепчущий лес.
    </UIToast>
    <UICard v-if="lastCombatResult === 'Victory' && combat.reward" class="reward-card" data-victory-reward>
      <div class="reward-card__heading">
        <div><small>Награда за победу</small><h2>+{{ combat.reward.xpEarned }} XP</h2></div>
        <b v-if="combat.reward.leveledUp">Уровень {{ combat.reward.currentLevel }}</b>
      </div>
      <ul v-if="combat.reward.items.length">
        <li v-for="item in combat.reward.items" :key="item.itemId">
          <span>{{ item.name }}</span><b>×{{ item.quantity }}</b>
          <small>{{ item.type === 'Equipment' ? 'Экипировка' : 'Материал' }}</small>
        </li>
      </ul>
      <p v-else>В этот раз предметы не выпали.</p>
    </UICard>
    <UIToast
      v-if="lastCombatResult === 'Defeat'"
      tone="danger"
      title="Поражение"
      data-combat-result
    >
      {{ lastEnemyName ?? 'Противник' }} оказался сильнее. Вы очнулись в Стартовом городе с восстановленным здоровьем.
    </UIToast>
    <UIToast
      v-else-if="lastCombatResult === 'Cancelled'"
      tone="info"
      title="Бой прерван"
      data-combat-result
    >
      Вы покинули бой и вернулись к исследованию.
    </UIToast>

    <UICard
      v-if="selectedEncounter"
      class="encounter"
      data-world-encounter
      :data-monster-id="selectedEncounter.id"
    >
      <div class="encounter__copy">
        <p class="encounter__eyebrow">Обнаружен противник</p>
        <h2>{{ selectedEncounter.name }}</h2>
        <p class="encounter__level">Уровень {{ selectedEncounter.level }}</p>
        <p>{{ selectedEncounter.description }}</p>
        <small>Бой начнётся только после вашего решения.</small>
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
      <p>Осмотрите окрестности. В лесу уже встречаются волки, кабаны и гигантские пауки.</p>
      <UIButton
        data-explore
        variant="secondary"
        :disabled="combat.pending"
        @click="explore"
      >
        Исследовать
      </UIButton>
    </UIPanel>

    <UIPanel v-if="!selectedEncounter" class="paths">
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
    <div v-if="combat.errorCode" role="alert" data-combat-diagnostic>
      <UIToast tone="danger" title="Ошибка realtime-соединения">
        <code>{{ combat.errorCode }}</code>
        <div v-if="combat.diagnostic" class="diagnostic">
          <small><b>stage:</b> {{ combat.diagnostic.stage }}</small>
          <small v-if="combat.diagnostic.operation"><b>operation:</b> {{ combat.diagnostic.operation }}</small>
          <small v-if="combat.diagnostic.statusCode !== null"><b>HTTP:</b> {{ combat.diagnostic.statusCode }}</small>
          <small><b>message:</b> {{ combat.diagnostic.message }}</small>
        </div>
      </UIToast>
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
.encounter__copy small { color: var(--ui-color-text-muted); }
.encounter__level { font-weight: var(--ui-font-weight-semibold); }
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
.diagnostic {
  display: grid;
  gap: var(--ui-space-1);
  margin-top: var(--ui-space-2);
  overflow-wrap: anywhere;
}
.diagnostic small { color: var(--ui-color-text-muted); }
.diagnostic b { color: var(--ui-color-text-secondary); }
.reward-card { display: grid; gap: var(--ui-space-3); border-color: var(--ui-color-success); }
.reward-card__heading { display: flex; align-items: center; justify-content: space-between; gap: var(--ui-space-3); }
.reward-card__heading small { color: var(--ui-color-text-muted); text-transform: uppercase; }
.reward-card__heading h2 { margin: 0; color: var(--ui-color-success); font-family: var(--ui-font-display); }
.reward-card__heading > b { color: var(--ui-color-warning); }
.reward-card ul { display: grid; gap: var(--ui-space-2); margin: 0; padding: 0; list-style: none; }
.reward-card li { display: grid; grid-template-columns: 1fr auto; gap: 0 var(--ui-space-2); padding-top: var(--ui-space-2); border-top: 1px solid var(--ui-color-border); }
.reward-card li small { grid-column: 1 / -1; color: var(--ui-color-text-muted); }
.reward-card p { margin: 0; color: var(--ui-color-text-muted); }
code { overflow-wrap: anywhere; color: var(--ui-color-danger); }
@media (max-width: 360px) {
  .world {
    padding-inline: calc(var(--ui-space-3) + var(--ui-safe-area-left))
      calc(var(--ui-space-3) + var(--ui-safe-area-right));
  }
  .path-card,
  .encounter__actions { grid-template-columns: 1fr; }
}
</style>
