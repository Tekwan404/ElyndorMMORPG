<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'

import type { WorldEncounter } from '@/api/contracts'
import { gameArt } from '@/assets/gameArt'
import { monsterArtUrl } from '@/assets/monsterArt'
import CombatView from '@/game/combat/views/CombatView.vue'
import MerchantShop from '@/game/world/components/MerchantShop.vue'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UICard, UIToast } from '@/ui/components'

type CombatResult = 'Victory' | 'Defeat' | 'Cancelled'

const STARTER_TOWN_ID = 'STARTER_TOWN'
const WHISPERING_FOREST_ID = 'WHISPERING_FOREST'

const session = useGameSessionStore()
const combat = useCombatSessionStore()
const selectedEncounter = ref<WorldEncounter | null>(null)
const lastCombatResult = ref<CombatResult | null>(null)
const lastEnemyName = ref<string | null>(null)
const merchantOpen = ref(false)
let vitalsRefreshTimer: ReturnType<typeof setInterval> | null = null
let vitalsRefreshPending = false

const world = computed(() => session.snapshot?.world)
const character = computed(() => session.snapshot?.character)
const currentLocationId = computed(() => world.value?.currentLocation.id)
const isStarterTown = computed(() => currentLocationId.value === STARTER_TOWN_ID)
const isWhisperingForest = computed(() => currentLocationId.value === WHISPERING_FOREST_ID)
const canExplore = computed(() => world.value?.currentLocation.dangerLevel !== 'SAFE')
const selectedEncounterArt = computed(() => monsterArtUrl(selectedEncounter.value?.artId))
const locationName = computed(() => isStarterTown.value ? 'Стартовый город' : isWhisperingForest.value ? 'Шепчущий лес' : world.value?.currentLocation.displayName ?? 'Неизвестная область')
const locationDescription = computed(() => isStarterTown.value
  ? 'Безопасный город для отдыха, торговли, тренировки билдов и подготовки к следующему походу.'
  : isWhisperingForest.value
    ? 'Сумрачный лес старых дорог. Исследуйте область, чтобы встретить противника.'
    : 'Исследуйте текущую область и её доступные пути.')
const sceneBackground = computed(() => isStarterTown.value ? gameArt.world.capital : gameArt.world.forest)
const dangerLabel = computed(() => {
  const danger = world.value?.currentLocation.dangerLevel
  if (danger === 'SAFE') return 'БЕЗОПАСНАЯ ЗОНА'
  if (danger === 'DANGEROUS') return 'ВЫСОКИЙ РИСК'
  return 'ОПАСНАЯ ОБЛАСТЬ'
})
const worldErrorMessage = computed(() => {
  const code = session.errorCode
  if (!code) return null
  if (code === 'world_encounter_unavailable') return 'В этой области сейчас не удалось найти противника.'
  if (code === 'world_encounter_location_unavailable') return 'Текущее положение героя не удалось подтвердить.'
  if (code === 'travel_conflict') return 'Мир изменился во время перехода. Попробуйте ещё раз.'
  if (code === 'character_in_combat') return 'Сначала завершите текущий бой.'
  return 'Действие не удалось выполнить.'
})
const recoveryMessage = computed(() => {
  const vitals = character.value?.vitals
  if (!vitals || combat.isActive) return null
  if (isStarterTown.value && vitals.currentHp < vitals.maxHp) return 'Отдых в городе: здоровье восстанавливается по 5 ед. в секунду.'
  if (vitals.resourceType === 'RAGE' && vitals.currentResource > 0) return 'После боя ярость постепенно угасает.'
  return null
})
const needsOutOfCombatRefresh = computed(() => {
  const vitals = character.value?.vitals
  if (!vitals || combat.isActive) return false
  return (isStarterTown.value && vitals.currentHp < vitals.maxHp)
    || (vitals.resourceType === 'RAGE' && vitals.currentResource > 0)
})

async function explore(): Promise<void> {
  if (!canExplore.value || combat.isActive || session.mutationPending) return
  lastCombatResult.value = null
  selectedEncounter.value = await session.explore()
}

function locationLabel(id: string, displayName: string): string {
  if (id === STARTER_TOWN_ID) return 'Стартовый город'
  if (id === WHISPERING_FOREST_ID) return 'Шепчущий лес'
  return displayName
}

async function travelTo(locationId: string): Promise<void> {
  await session.travel(locationId)
  selectedEncounter.value = null
  lastCombatResult.value = null
  lastEnemyName.value = null
}

async function startEncounterCombat(): Promise<void> {
  if (!selectedEncounter.value || combat.pending) return
  const encounter = selectedEncounter.value
  if (await combat.startCombat(encounter)) {
    lastEnemyName.value = encounter.name
    selectedEncounter.value = null
    lastCombatResult.value = null
  }
}

async function startTraining(): Promise<void> {
  if (!isStarterTown.value || combat.pending) return
  if (await combat.startTraining()) {
    lastEnemyName.value = 'Тренировочный манекен'
    selectedEncounter.value = null
    lastCombatResult.value = null
  }
}

async function restoreCombat(): Promise<void> {
  try {
    await combat.connect()
    await combat.resume()
  } catch {
    // Мир остаётся доступным при временной ошибке realtime.
  }
}

async function refreshOutOfCombatVitals(): Promise<void> {
  if (vitalsRefreshPending || combat.isActive || session.mutationPending) return
  vitalsRefreshPending = true
  try { await session.refreshSnapshot() } catch { /* background refresh */ }
  finally { vitalsRefreshPending = false }
}

function syncVitalsRefreshTimer(enabled: boolean): void {
  if (enabled && vitalsRefreshTimer === null) {
    vitalsRefreshTimer = setInterval(() => void refreshOutOfCombatVitals(), 1000)
  } else if (!enabled && vitalsRefreshTimer !== null) {
    clearInterval(vitalsRefreshTimer)
    vitalsRefreshTimer = null
  }
}

watch(currentLocationId, (locationId, previousLocationId) => {
  if (locationId !== previousLocationId) {
    selectedEncounter.value = null
    merchantOpen.value = false
  }
  if (locationId) void restoreCombat()
}, { immediate: true })

watch(() => combat.snapshot?.status, (status) => {
  if (status === 'Victory' || status === 'Defeat') {
    selectedEncounter.value = null
    if (combat.snapshot) lastEnemyName.value = combat.snapshot.enemy.name
    lastCombatResult.value = status
    void session.refreshSnapshot()
  }
})

watch(needsOutOfCombatRefresh, syncVitalsRefreshTimer, { immediate: true })
onBeforeUnmount(() => syncVitalsRefreshTimer(false))
</script>

<template>
  <CombatView v-if="combat.isActive" @leave="lastCombatResult = 'Cancelled'" />

  <section v-else-if="world && character" class="world">
    <section class="scene" :style="{ backgroundImage: `url(${sceneBackground})` }">
      <div class="scene__shade" />
      <div class="scene__content">
        <div v-if="selectedEncounter" class="scene-encounter" data-world-encounter>
          <img v-if="selectedEncounterArt" :src="selectedEncounterArt" :alt="selectedEncounter.name" />
          <div v-else class="scene-encounter__fallback" role="img" :aria-label="selectedEncounter.name">⚔</div>
          <div class="scene-encounter__copy">
            <small>ОБНАРУЖЕН ПРОТИВНИК</small>
            <h2>{{ selectedEncounter.name }}</h2>
            <p>Уровень {{ selectedEncounter.level }} · {{ selectedEncounter.description }}</p>
          </div>
          <div class="scene__actions">
            <UIButton data-start-encounter :loading="combat.pending" @click="startEncounterCombat">Вступить в бой</UIButton>
            <UIButton variant="ghost" @click="selectedEncounter = null">Уйти</UIButton>
          </div>
        </div>

        <div v-else class="scene-location">
          <div class="scene__eyebrow">
            <span :data-danger="world.currentLocation.dangerLevel">{{ dangerLabel }}</span>
            <span>рек. уровень {{ world.currentLocation.recommendedLevel }}</span>
          </div>
          <h1>{{ locationName }}</h1>
          <p>{{ locationDescription }}</p>

          <div v-if="canExplore && lastCombatResult !== 'Victory'" class="scene__primary-action">
            <UIButton data-explore :loading="session.mutationPending" @click="explore">Исследовать</UIButton>
          </div>

          <nav class="scene__travel" aria-label="Переходы между локациями">
            <small>ПУТЕШЕСТВИЕ</small>
            <div v-if="world.outgoingTransitions.length" class="scene__travel-actions">
              <UIButton
                v-for="location in world.outgoingTransitions"
                :key="location.id"
                :data-travel="location.id"
                :aria-label="`Отправиться: ${locationLabel(location.id, location.displayName)}`"
                variant="secondary"
                :loading="session.mutationPending"
                @click="travelTo(location.id)"
              >
                {{ locationLabel(location.id, location.displayName) }}
              </UIButton>
            </div>
            <p v-else class="scene__no-paths" role="status">Пути не найдены. Исследуйте текущую область.</p>
          </nav>
        </div>
      </div>
    </section>

    <div v-if="session.errorCode" class="world-error" role="alert">
      <strong>{{ worldErrorMessage }}</strong>
      <small>{{ session.errorCode }}</small>
    </div>

    <UICard v-if="lastCombatResult === 'Victory'" class="reward-card">
      <div class="reward-card__heading">
        <small>ПОБЕДА</small>
        <strong>{{ lastEnemyName ?? 'Противник' }} повержен</strong>
      </div>
      <div v-if="combat.reward" class="reward-card__summary">
        <strong>+{{ combat.reward.xpEarned }} опыта · +{{ combat.reward.goldEarned }} золота</strong>
        <ul v-if="combat.reward.items.length">
          <li v-for="item in combat.reward.items" :key="item.itemId">{{ item.name }} ×{{ item.quantity }}</li>
        </ul>
      </div>
      <UIButton v-if="canExplore" data-explore-after-victory :loading="session.mutationPending" @click="explore">Исследовать дальше</UIButton>
    </UICard>

    <UIToast v-if="lastCombatResult === 'Defeat'" tone="danger" title="Поражение">Вы очнулись в Стартовом городе.</UIToast>
    <UIToast v-if="recoveryMessage" tone="info" title="Восстановление">{{ recoveryMessage }}</UIToast>

    <section v-if="isStarterTown" class="town-grid">
      <UICard class="town-service town-service--training">
        <small>ТРЕНИРОВОЧНАЯ ПЛОЩАДКА</small>
        <h2>Тренировочный манекен</h2>
        <p>Проверяйте билды, криты, DoT и ротацию без риска, расхода зелий и наград.</p>
        <UIButton data-start-training :loading="combat.pending" @click="startTraining">Тренироваться</UIButton>
      </UICard>
      <UICard class="town-service">
        <small>ТОРГОВЕЦ</small>
        <h2>Маркус</h2>
        <p>Лечебные припасы, покупка зелий и скупка добытых материалов.</p>
        <UIButton @click="merchantOpen = true">Открыть лавку</UIButton>
      </UICard>
      <UICard class="town-service">
        <small>ОТДЫХ</small>
        <h2>Городская площадь</h2>
        <p>Здесь здоровье постепенно восстанавливается. Подготовьте экипировку и таланты.</p>
      </UICard>
    </section>

    <MerchantShop :open="merchantOpen" @close="merchantOpen = false" />
  </section>
</template>

<style scoped>
.world {
  display: grid;
  width: min(100%, var(--ui-content-width));
  margin-inline: auto;
  gap: var(--ui-space-3);
  padding: var(--ui-space-3) var(--ui-space-4) var(--ui-space-7);
}

.scene {
  position: relative;
  min-height: 23rem;
  overflow: hidden;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: calc(var(--ui-radius-lg) + 2px);
  background-color: var(--ui-color-surface-1);
  background-position: center;
  background-size: cover;
  box-shadow: var(--ui-shadow-inset), 0 18px 42px rgb(0 0 0 / 26%);
}

.scene::after {
  position: absolute;
  inset: 0;
  border: 1px solid rgb(255 255 255 / 3%);
  border-radius: inherit;
  content: '';
  pointer-events: none;
}

.scene__shade {
  position: absolute;
  inset: 0;
  background:
    linear-gradient(180deg, rgb(2 4 8 / 4%) 0%, rgb(4 7 13 / 22%) 38%, rgb(4 7 13 / 94%) 84%),
    linear-gradient(90deg, rgb(3 5 10 / 30%), transparent 45%);
}

.scene__content {
  position: relative;
  z-index: 1;
  display: grid;
  min-height: 23rem;
  align-items: end;
  padding: var(--ui-space-5);
}

.scene-location {
  display: grid;
  gap: var(--ui-space-2);
  text-shadow: 0 2px 8px rgb(0 0 0 / 60%);
}

.scene__eyebrow {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--ui-space-2);
  margin-bottom: 2px;
}

.scene__eyebrow span {
  padding: 4px 7px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-round);
  background: rgb(5 8 14 / 58%);
  color: var(--ui-color-text-muted);
  font-size: .59rem;
  font-weight: 700;
  letter-spacing: .08em;
  text-transform: uppercase;
  backdrop-filter: blur(8px);
}

.scene__eyebrow span:first-child[data-danger='SAFE'] {
  border-color: rgb(79 185 150 / 35%);
  color: #84d5bb;
}

.scene__eyebrow span:first-child[data-danger='ADVENTURE'] {
  border-color: rgb(208 164 88 / 38%);
  color: #e1bd78;
}

.scene__eyebrow span:first-child[data-danger='DANGEROUS'] {
  border-color: rgb(216 95 114 / 42%);
  color: #ef8fa0;
}

.scene-location h1,
.scene-location p,
.scene-encounter h2,
.scene-encounter p {
  margin: 0;
}

.scene-location h1 {
  max-width: 90%;
  font-family: var(--ui-font-display);
  font-size: clamp(1.8rem, 8vw, 2.55rem);
  line-height: 1.02;
  letter-spacing: -.02em;
}

.scene-location > p,
.scene-encounter p {
  max-width: 38rem;
  color: #c4cad8;
  font-size: var(--ui-font-size-sm);
  line-height: 1.55;
}

.scene__primary-action {
  display: flex;
  margin-top: var(--ui-space-2);
}

.scene__primary-action :deep(.ui-button) {
  min-width: 10rem;
}

.scene__travel {
  display: grid;
  gap: var(--ui-space-2);
  margin-top: var(--ui-space-4);
  padding-top: var(--ui-space-3);
  border-top: 1px solid rgb(255 255 255 / 10%);
}

.scene__travel > small,
.scene-encounter small {
  color: #bcb6ff;
  font-size: .63rem;
  font-weight: 700;
  letter-spacing: .08em;
}

.scene__travel-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ui-space-2);
}

.scene__no-paths {
  margin: 0;
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-sm);
}

.scene-encounter {
  display: grid;
  grid-template-columns: minmax(7rem, 10rem) 1fr;
  gap: var(--ui-space-4);
  align-items: center;
  padding: var(--ui-space-4);
  border: 1px solid rgb(216 95 114 / 24%);
  border-radius: var(--ui-radius-lg);
  background: linear-gradient(135deg, rgb(25 12 17 / 72%), rgb(7 10 17 / 78%));
  box-shadow: 0 16px 32px rgb(0 0 0 / 24%);
  backdrop-filter: blur(7px);
}

.scene-encounter img {
  width: 100%;
  max-height: 12rem;
  object-fit: contain;
  filter: drop-shadow(0 .75rem 1.25rem rgb(0 0 0 / 58%));
}

.scene-encounter__fallback {
  display: grid;
  min-height: 8rem;
  place-items: center;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-lg);
  background: rgb(5 8 14 / 66%);
  color: var(--ui-color-text-muted);
  font-size: 2rem;
}

.scene-encounter__copy {
  display: grid;
  gap: var(--ui-space-1);
}

.scene-encounter__copy h2 {
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-2xl);
}

.scene__actions {
  grid-column: 1 / -1;
  display: flex;
  gap: var(--ui-space-2);
}

.world-error {
  display: grid;
  gap: 2px;
  margin: 0;
  padding: var(--ui-space-3) var(--ui-space-4);
  border: 1px solid rgb(216 95 114 / 38%);
  border-left: 3px solid var(--ui-color-danger);
  border-radius: var(--ui-radius-md);
  background: linear-gradient(90deg, rgb(216 95 114 / 9%), var(--ui-color-surface-1));
  color: var(--ui-color-text-secondary);
}

.world-error strong {
  color: #ef9bab;
  font-size: var(--ui-font-size-sm);
}

.world-error small {
  color: var(--ui-color-text-muted);
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  font-size: .6rem;
}

.reward-card {
  display: grid;
  gap: var(--ui-space-3);
  border-color: color-mix(in srgb, var(--ui-color-success) 35%, var(--ui-color-border));
  background:
    linear-gradient(135deg, rgb(79 185 150 / 8%), transparent 48%),
    var(--ui-gradient-panel);
}

.reward-card__heading {
  display: grid;
  gap: var(--ui-space-1);
}

.reward-card__heading small {
  color: var(--ui-color-success);
  font-weight: 700;
  letter-spacing: .08em;
}

.reward-card__summary > strong {
  color: #83d2b8;
}

.reward-card ul {
  margin: var(--ui-space-2) 0 0;
  padding-left: 1.2rem;
}

.town-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--ui-space-3);
}

.town-service {
  position: relative;
  display: grid;
  gap: var(--ui-space-2);
  overflow: hidden;
}

.town-service::before {
  position: absolute;
  top: 0;
  right: 0;
  width: 7rem;
  height: 7rem;
  border-radius: 50%;
  background: radial-gradient(circle, rgb(146 136 255 / 9%), transparent 68%);
  content: '';
  pointer-events: none;
  transform: translate(30%, -35%);
}

.town-service small {
  position: relative;
  color: var(--ui-color-primary);
  font-size: .63rem;
  font-weight: 700;
  letter-spacing: .07em;
}

.town-service h2,
.town-service p {
  position: relative;
  margin: 0;
}

.town-service h2 {
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-lg);
}

.town-service p {
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-sm);
  line-height: 1.5;
}

.town-service :deep(.ui-button) {
  position: relative;
  justify-self: start;
  margin-top: auto;
}

.town-service--training {
  grid-column: 1 / -1;
  border-color: color-mix(in srgb, var(--ui-color-primary) 34%, var(--ui-color-border));
  background:
    linear-gradient(135deg, rgb(146 136 255 / 10%), transparent 58%),
    var(--ui-gradient-panel);
}

@media (max-width: 520px) {
  .world {
    padding: var(--ui-space-3);
    padding-bottom: var(--ui-space-6);
  }

  .scene,
  .scene__content {
    min-height: 21rem;
  }

  .scene__content {
    padding: var(--ui-space-4);
  }

  .scene-location h1 {
    max-width: 100%;
  }

  .scene__primary-action,
  .scene__primary-action :deep(.ui-button) {
    width: 100%;
  }

  .scene-encounter {
    grid-template-columns: 6.5rem 1fr;
    gap: var(--ui-space-3);
    padding: var(--ui-space-3);
  }

  .scene-encounter img {
    max-height: 9rem;
  }

  .scene__actions,
  .scene__travel-actions {
    display: grid;
    grid-template-columns: 1fr;
  }

  .scene__actions :deep(.ui-button),
  .scene__travel-actions :deep(.ui-button) {
    width: 100%;
  }

  .town-grid {
    grid-template-columns: 1fr;
  }

  .town-service--training {
    grid-column: auto;
  }

  .town-service :deep(.ui-button) {
    width: 100%;
  }
}
</style>