<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import StoneSpursLocationOverview from '@/game/world/components/StoneSpursLocationOverview.vue'
import { locationPresentation } from '@/game/world/locationPresentation'
import { usePartyStore } from '@/game/party/partyStore'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIModal } from '@/ui/components'

import WorldViewLegacy from './WorldViewLegacy.vue'

const props = withDefaults(defineProps<{ openGuild?: boolean }>(), { openGuild: false })
const emit = defineEmits<{ 'open-party': [] }>()

const session = useGameSessionStore()
const combat = useCombatSessionStore()
const party = usePartyStore()

const afkOpen = ref(false)
const afkDurationMinutes = ref(60)
const afkTargetMonsterId = ref<string | null>(null)
const afkTargets = ref<{ monsterId: string; displayName: string }[]>([])
const afkPreviewLoading = ref(false)
const afkPreview = ref<Awaited<ReturnType<typeof session.previewAfkFarm>>>(null)

const world = computed(() => session.snapshot?.world)
const character = computed(() => session.snapshot?.character)
const currentLocationId = computed(() => world.value?.currentLocation.id ?? '')
const isStoneSpursLocation = computed(() => currentLocationId.value === 'STONE_SPURS')
const isTravelling = computed(() => world.value?.travel !== null && world.value?.travel !== undefined)
const activeAfkFarm = computed(() => session.snapshot?.afkFarm?.status === 'Active'
  ? session.snapshot.afkFarm
  : null)
const canStartWorldCombat = computed(() =>
  party.snapshot === null
    || party.snapshot.leaderCharacterId === character.value?.id,
)
const canExplore = computed(() =>
  isStoneSpursLocation.value
  && !isTravelling.value
  && !combat.isActive
  && world.value?.currentLocation.dangerLevel !== 'SAFE'
  && canStartWorldCombat.value,
)
const canUseAfkFarm = computed(() =>
  isStoneSpursLocation.value
  && !isTravelling.value
  && !combat.isActive
  && world.value?.currentLocation.allowAfk === true
  && activeAfkFarm.value === null,
)
const sceneBackground = computed(() => locationPresentation(currentLocationId.value).art)

async function exploreStoneSpurs(): Promise<void> {
  if (!canExplore.value || session.mutationPending || combat.pending) return

  const encounter = await session.explore()
  if (!encounter) return
  await combat.startCombat(encounter)
}

async function openAfkFarm(): Promise<void> {
  if (!canUseAfkFarm.value) return
  afkOpen.value = true
  afkTargets.value = await session.getAfkFarmTargets()
  await loadAfkPreview()
}

async function loadAfkPreview(): Promise<void> {
  if (!currentLocationId.value || afkPreviewLoading.value) return
  afkPreviewLoading.value = true
  try {
    afkPreview.value = await session.previewAfkFarm(
      currentLocationId.value,
      afkDurationMinutes.value,
      afkTargetMonsterId.value,
    )
  } finally {
    afkPreviewLoading.value = false
  }
}

async function selectAfkDuration(durationMinutes: number): Promise<void> {
  afkDurationMinutes.value = durationMinutes
  await loadAfkPreview()
}

async function selectAfkTarget(targetMonsterId: string): Promise<void> {
  afkTargetMonsterId.value = targetMonsterId || null
  await loadAfkPreview()
}

async function startAfkFarm(): Promise<void> {
  if (!currentLocationId.value || session.mutationPending) return
  const started = await session.startAfkFarm(
    currentLocationId.value,
    afkDurationMinutes.value,
    afkTargetMonsterId.value,
  )
  if (started) afkOpen.value = false
}

onMounted(() => {
  void party.refresh()
})
</script>

<template>
  <section v-if="isStoneSpursLocation" class="stone-spurs-location-screen">
    <StoneSpursLocationOverview
      :background-url="sceneBackground"
      :can-explore="canExplore"
      :can-auto-hunt="canUseAfkFarm"
      :actions-disabled="session.mutationPending || combat.pending || isTravelling"
      :explore-loading="session.mutationPending"
      @explore="exploreStoneSpurs"
      @auto-hunt="openAfkFarm"
    />

    <div class="stone-spurs-location-screen__systems">
      <WorldViewLegacy
        :open-guild="props.openGuild"
        @open-party="emit('open-party')"
      />
    </div>

    <UIModal :open="afkOpen" title="Автоматическая охота" @close="afkOpen = false">
      <div class="afk-modal" data-afk-farm-modal>
        <p>Герой будет сражаться в Каменных отрогах до окончания выбранного времени.</p>
        <div class="afk-duration" aria-label="Длительность автоматической охоты">
          <UIButton
            v-for="duration in [15, 60, 240]"
            :key="duration"
            :variant="afkDurationMinutes === duration ? 'primary' : 'secondary'"
            :disabled="afkPreviewLoading || session.mutationPending"
            @click="selectAfkDuration(duration)"
          >
            {{ duration < 60 ? `${duration} мин` : `${duration / 60} ч` }}
          </UIButton>
        </div>

        <label v-if="afkTargets.length" class="afk-target">
          <span>Цель</span>
          <select
            :value="afkTargetMonsterId ?? ''"
            :disabled="afkPreviewLoading || session.mutationPending"
            @change="selectAfkTarget(($event.target as HTMLSelectElement).value)"
          >
            <option value="">Любые противники</option>
            <option v-for="target in afkTargets" :key="target.monsterId" :value="target.monsterId">
              {{ target.displayName }}
            </option>
          </select>
        </label>

        <div v-if="afkPreview" class="afk-preview">
          <span>Примерно {{ afkPreview.kills }} побед</span>
          <strong>+{{ afkPreview.estimatedXp }} опыта · +{{ afkPreview.estimatedGold }} золота</strong>
          <small>{{ afkPreview.potentialLootRolls }} возможных розыгрышей добычи · эффективность: {{ afkPreview.efficiencyPercent }}%</small>
        </div>
        <p v-else-if="afkPreviewLoading">Рассчитываем результат…</p>
        <p v-else-if="session.errorCode">Не удалось получить расчёт. Проверьте условия области.</p>
      </div>
      <template #actions>
        <UIButton variant="secondary" @click="afkOpen = false">Отмена</UIButton>
        <UIButton :loading="session.mutationPending" :disabled="!afkPreview" @click="startAfkFarm">Начать</UIButton>
      </template>
    </UIModal>
  </section>

  <WorldViewLegacy
    v-else
    :open-guild="props.openGuild"
    @open-party="emit('open-party')"
  />
</template>

<style scoped>
.stone-spurs-location-screen {
  display: grid;
  width: 100%;
  max-width: var(--ui-content-width-tablet);
  min-width: 0;
  margin-inline: auto;
  gap: var(--ui-space-3);
  padding: var(--ui-space-3) var(--ui-space-4) var(--ui-space-7);
}

.stone-spurs-location-screen__systems {
  min-width: 0;
}

.stone-spurs-location-screen__systems :deep(.world) {
  width: 100%;
  max-width: none;
  margin: 0;
  padding: 0;
}

.stone-spurs-location-screen__systems :deep(.scene) {
  display: none;
}

.afk-modal,
.afk-preview,
.afk-target {
  display: grid;
  gap: var(--ui-space-2);
}

.afk-modal > p,
.afk-preview small {
  margin: 0;
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-sm);
}

.afk-duration {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: var(--ui-space-2);
}

.afk-target {
  color: var(--ui-color-text-secondary);
  font-size: var(--ui-font-size-sm);
}

.afk-target select {
  min-height: 44px;
  padding: 0 var(--ui-space-3);
  border: 1px solid var(--ui-color-border-strong);
  border-radius: var(--ui-radius-md);
  background: var(--ui-color-surface-2);
  color: var(--ui-color-text-primary);
  font: inherit;
}

.afk-preview {
  padding: var(--ui-space-3);
  border: 1px solid rgb(102 141 225 / 35%);
  border-radius: var(--ui-radius-md);
  background: rgb(61 81 137 / 14%);
}

.afk-preview strong {
  color: var(--ui-color-text-primary);
}

@media (max-width: 520px) {
  .stone-spurs-location-screen {
    padding: var(--ui-space-3);
    padding-bottom: var(--ui-space-6);
  }
}

@media (min-width: 720px) {
  .stone-spurs-location-screen {
    gap: var(--ui-space-4);
    padding-inline: clamp(16px, 2.4vw, 28px);
  }
}
</style>
