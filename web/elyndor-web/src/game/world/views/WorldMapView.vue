<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'

import { apiClient } from '@/api/apiClient'
import type { WorldLocation } from '@/api/contracts'
import { gameArt } from '@/assets/gameArt'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UICard, UILoadingState, UIToast } from '@/ui/components'

const emit = defineEmits<{
  'open-location': []
}>()

const session = useGameSessionStore()
const locations = ref<WorldLocation[]>([])
const selectedLocationId = ref<string | null>(null)
const loading = ref(true)
const catalogError = ref(false)

const world = computed(() => session.snapshot?.world)
const activeTravel = computed(() => world.value?.travel ?? null)
const isTravelling = computed(() => activeTravel.value !== null)
const currentLocationId = computed(() => world.value?.currentLocation.id ?? null)
const characterLevel = computed(() => session.snapshot?.character?.level ?? 1)
const contracts = computed(() => world.value?.contracts ?? [])
const reachableLocationIds = computed(
  () => new Set(world.value?.outgoingTransitions.map(location => location.id) ?? []),
)
const visibleLocations = computed(() => {
  const byId = new Map<string, WorldLocation>()
  for (const location of locations.value) byId.set(location.id, location)
  if (world.value) {
    byId.set(world.value.currentLocation.id, world.value.currentLocation)
    for (const location of world.value.outgoingTransitions) byId.set(location.id, location)
  }
  return [...byId.values()].sort((left, right) =>
    left.recommendedLevel - right.recommendedLevel
      || left.displayName.localeCompare(right.displayName),
  )
})
const selectedLocation = computed(() =>
  visibleLocations.value.find(location => location.id === selectedLocationId.value)
  ?? world.value?.currentLocation
  ?? null,
)
const selectedIsCurrent = computed(
  () => selectedLocation.value?.id === currentLocationId.value,
)
const selectedIsReachable = computed(
  () => !isTravelling.value && selectedLocation.value
    ? reachableLocationIds.value.has(selectedLocation.value.id)
    : false,
)
function locationArt(locationId: string | null | undefined): string {
  if (locationId === 'STARTER_TOWN') return gameArt.world.starterTown
  if (locationId === 'ANCIENT_MINE') return gameArt.world.ancientRuins
  if (locationId === 'BROODMOTHER_LAIR') return gameArt.world.ancientRuins
  if (locationId === 'BLIGHTED_GROVE') return gameArt.world.caravanRoad
  return gameArt.world.whisperingForest
}

const mapArt = computed(() => locationArt(currentLocationId.value))
const selectedArt = computed(() => locationArt(selectedLocation.value?.id))
const activeContract = computed(() =>
  contracts.value.find(contract => contract.status === 'ACTIVE') ?? null,
)
const selectedDangerLabel = computed(() => {
  const danger = selectedLocation.value?.dangerLevel
  if (danger === 'SAFE') return 'Безопасная зона'
  if (danger === 'DANGEROUS') return 'Высокий риск'
  return 'Приключение'
})
const routeLines = computed(() => {
  const currentIndex = visibleLocations.value.findIndex(
    location => location.id === currentLocationId.value,
  )
  if (currentIndex < 0) return []

  const current = nodePosition(currentIndex, visibleLocations.value.length)
  return visibleLocations.value
    .map((location, index) => ({
      location,
      target: nodePosition(index, visibleLocations.value.length),
    }))
    .filter(entry => reachableLocationIds.value.has(entry.location.id))
    .map(entry => ({
      id: entry.location.id,
      x1: current.x,
      y1: current.y,
      x2: entry.target.x,
      y2: entry.target.y,
    }))
})

async function loadLocations(): Promise<void> {
  loading.value = true
  catalogError.value = false
  try {
    locations.value = await apiClient.request<WorldLocation[]>('/api/v1/world/locations')
  } catch {
    catalogError.value = true
    locations.value = []
  } finally {
    loading.value = false
  }
}

function selectLocation(locationId: string): void {
  selectedLocationId.value = locationId
}

async function travel(): Promise<void> {
  const location = selectedLocation.value
  if (isTravelling.value || !location || !selectedIsReachable.value || session.mutationPending) return

  await session.travel(location.id)
  if (!session.errorCode) selectedLocationId.value = location.id
}

function locationName(location: WorldLocation): string {
  if (location.id === 'STARTER_TOWN') return 'Стартовый город'
  if (location.id === 'WHISPERING_FOREST') return 'Шепчущий лес'
  if (location.id === 'DEEP_FOREST') return 'Глубокий лес'
  if (location.id === 'ANCIENT_MINE') return 'Древняя шахта'
  if (location.id === 'BROODMOTHER_LAIR') return 'Логово Прародительницы'
  if (location.id === 'BLIGHTED_GROVE') return 'Осквернённая чаща'
  return location.displayName
}

function levelRangeLabel(location: WorldLocation): string {
  return location.minimumLevel === location.maximumLevel
    ? `ур. ${location.minimumLevel}`
    : `ур. ${location.minimumLevel}–${location.maximumLevel}`
}

function lockReason(location: WorldLocation): string | null {
  if (isTravelling.value && location.id !== currentLocationId.value) {
    return 'Герой уже находится в пути. Дождитесь завершения перехода.'
  }
  if (characterLevel.value < location.minimumLevel) {
    return `Требуется ${location.minimumLevel} уровень.`
  }
  if (location.requiredContractId) {
    const contract = contracts.value.find(item => item.id === location.requiredContractId)
    if (contract?.status !== 'COMPLETED') {
      return contract
        ? `Сначала выполните «${contract.displayName}».`
        : 'Сначала выполните требуемый контракт.'
    }
  }
  if (location.id !== currentLocationId.value && !reachableLocationIds.value.has(location.id)) {
    return 'Из текущей локации нет открытого прямого маршрута.'
  }
  return null
}

function locationState(location: WorldLocation): 'current' | 'reachable' | 'locked' {
  if (location.id === currentLocationId.value) return 'current'
  if (reachableLocationIds.value.has(location.id)) return 'reachable'
  return 'locked'
}

function nodePosition(index: number, total: number): { x: number; y: number } {
  if (total <= 1) return { x: 50, y: 52 }

  const safeTotal = Math.max(total - 1, 1)
  const progress = index / safeTotal
  const x = 14 + progress * 72
  const wave = [62, 38, 64, 34, 56, 44]
  const y = wave[index % wave.length] ?? 50
  return { x, y }
}

function nodeStyle(index: number): Record<string, string> {
  const position = nodePosition(index, visibleLocations.value.length)
  return {
    left: `${position.x}%`,
    top: `${position.y}%`,
  }
}

watch(currentLocationId, locationId => {
  if (locationId) selectedLocationId.value = locationId
}, { immediate: true })

onMounted(() => void loadLocations())
</script>

<template>
  <section v-if="world" class="world-map">
    <header class="world-map__header">
      <div>
        <small>МИР · ПОГРАНИЧНЫЕ ЗЕМЛИ</small>
        <h1>Карта мира</h1>
        <p>Выберите известную точку. Сервер разрешит переход только по открытому маршруту.</p>
      </div>
      <div class="world-map__meta">
        <span>{{ isTravelling ? 'В пути' : 'Сейчас' }}</span>
        <strong>
          {{
            isTravelling
              ? locations.find(item => item.id === activeTravel?.targetLocationId)?.displayName
                ?? activeTravel?.targetLocationId
                ?? 'Переход'
              : world.currentLocation
                ? locationName(world.currentLocation)
                : '—'
          }}
        </strong>
      </div>
    </header>

    <UIToast
      v-if="activeTravel"
      tone="info"
      title="Герой в пути"
      data-travel-status
    >
      Переход к
      {{
        locations.find(item => item.id === activeTravel?.targetLocationId)?.displayName
          ?? activeTravel.targetLocationId
      }}
      выполняется сервером. После прибытия карта обновится автоматически.
    </UIToast>

    <UIToast
      v-if="catalogError"
      tone="warning"
      title="Карта загружена частично"
    >
      Полный каталог мира недоступен. Показаны текущая локация и доступные переходы.
    </UIToast>

    <UILoadingState
      v-if="loading && visibleLocations.length === 0"
      state="loading"
      title="Открываем карту"
      message="Синхронизируем доступные области мира."
    />

    <template v-else>
      <section
        class="map-canvas"
        :style="{ '--map-art': `url(${mapArt})` }"
        aria-label="Карта доступных локаций"
      >
        <div class="map-canvas__fog" />
        <div class="map-canvas__grid" />
        <div class="map-canvas__caption" aria-hidden="true">
          <small>РЕГИОН</small>
          <strong>Пограничные земли</strong>
        </div>

        <svg
          class="map-routes"
          viewBox="0 0 100 100"
          preserveAspectRatio="none"
          aria-hidden="true"
        >
          <line
            v-for="route in routeLines"
            :key="route.id"
            :x1="route.x1"
            :y1="route.y1"
            :x2="route.x2"
            :y2="route.y2"
          />
        </svg>

        <button
          v-for="(location, index) in visibleLocations"
          :key="location.id"
          class="map-node"
          :class="{
            'map-node--selected': selectedLocation?.id === location.id,
          }"
          :data-state="locationState(location)"
          :data-location-id="location.id"
          :style="nodeStyle(index)"
          type="button"
          :aria-pressed="selectedLocation?.id === location.id"
          :aria-label="`${locationName(location)}, ${levelRangeLabel(location)}`"
          @click="selectLocation(location.id)"
        >
          <span class="map-node__pulse" />
          <span class="map-node__marker" :data-location-kind="location.id === 'ANCIENT_MINE' ? 'dungeon' : 'normal'">
            <i />
          </span>
          <span class="map-node__label">
            <strong>{{ locationName(location) }}</strong>
            <small>{{ levelRangeLabel(location) }}</small>
          </span>
        </button>

        <div v-if="selectedLocation" class="map-selection" data-map-selection>
          <div>
            <small>ВЫБРАНО</small>
            <strong>{{ locationName(selectedLocation) }}</strong>
            <span>{{ levelRangeLabel(selectedLocation) }}</span>
          </div>
          <UIButton
            v-if="selectedIsCurrent"
            data-map-open-location
            @click="emit('open-location')"
          >
            Открыть
          </UIButton>
          <UIButton
            v-else
            data-map-travel-inline
            :disabled="isTravelling || !selectedIsReachable"
            :loading="session.mutationPending"
            @click="travel"
          >
            {{ isTravelling ? 'В пути' : selectedIsReachable ? 'Отправиться' : 'Закрыто' }}
          </UIButton>
        </div>

        <div class="map-legend" aria-label="Легенда карты">
          <span><i data-state="current" /> Вы здесь</span>
          <span><i data-state="reachable" /> Доступно</span>
          <span><i data-state="locked" /> Нет прямого пути</span>
        </div>
      </section>

      <UICard v-if="activeContract" class="contract-card" data-world-contract>
        <div>
          <small>АКТИВНЫЙ КОНТРАКТ</small>
          <strong>{{ activeContract.displayName }}</strong>
          <p>{{ activeContract.description }}</p>
        </div>
        <span>Открывает: {{ locations.find(item => item.id === activeContract?.unlockLocationId)?.displayName ?? activeContract?.unlockLocationId }}</span>
      </UICard>

      <UICard v-if="selectedLocation" class="location-preview" data-map-preview>
        <div
          class="location-preview__art"
          :style="{ backgroundImage: `url(${selectedArt})` }"
          aria-hidden="true"
        >
          <div />
        </div>

        <div class="location-preview__body">
          <div class="location-preview__eyebrow">
            <span :data-danger="selectedLocation.dangerLevel">{{ selectedDangerLabel }}</span>
            <span>{{ levelRangeLabel(selectedLocation) }}</span>
          </div>

          <div>
            <small>ВЫБРАННАЯ ТОЧКА</small>
            <h2>{{ locationName(selectedLocation) }}</h2>
          </div>

          <p>{{ selectedLocation.description || 'Описание этой области пока не заполнено.' }}</p>
          <p v-if="lockReason(selectedLocation)" class="location-preview__lock">
            {{ lockReason(selectedLocation) }}
          </p>
          <p v-else-if="selectedIsCurrent">
            Герой находится здесь. Откройте экран локации, чтобы увидеть активности и противников.
          </p>
          <p v-else-if="selectedIsReachable">
            Маршрут открыт. Переход будет подтверждён сервером.
          </p>

          <div class="location-preview__actions">
            <UIButton
              v-if="selectedIsCurrent"
              data-open-location
              @click="emit('open-location')"
            >
              Открыть локацию
            </UIButton>
            <UIButton
              v-else
              data-map-travel
              :disabled="isTravelling || !selectedIsReachable"
              :loading="session.mutationPending"
              @click="travel"
            >
              {{ isTravelling ? 'В пути' : selectedIsReachable ? 'Отправиться' : 'Путь недоступен' }}
            </UIButton>
          </div>
        </div>
      </UICard>
    </template>
  </section>
</template>

<style scoped>
.world-map {
  display: grid;
  width: min(100%, var(--ui-content-width));
  margin-inline: auto;
  gap: var(--ui-space-3);
  padding: var(--ui-space-3) var(--ui-space-4) var(--ui-space-7);
}

.world-map__header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: var(--ui-space-3);
  padding: var(--ui-space-2) var(--ui-space-1);
}

.world-map__header > div {
  display: grid;
  gap: var(--ui-space-1);
}

.world-map__header small,
.location-preview__body small {
  color: #aaa3ff;
  font-size: .62rem;
  font-weight: 700;
  letter-spacing: .1em;
}

.world-map__header h1,
.world-map__header p,
.location-preview h2,
.location-preview p {
  margin: 0;
}

.world-map__header h1 {
  font-family: var(--ui-font-display);
  font-size: clamp(1.55rem, 7vw, 2rem);
}

.world-map__header p,
.location-preview p {
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-sm);
  line-height: 1.5;
}

.world-map__meta {
  display: grid;
  min-width: 7rem;
  justify-items: end;
  gap: 1px;
  padding: 6px 9px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: rgb(10 14 24 / 72%);
}

.world-map__meta span {
  color: var(--ui-color-text-muted);
  font-size: .5rem;
  font-weight: 700;
  letter-spacing: .08em;
  text-transform: uppercase;
}

.world-map__meta strong {
  max-width: 9rem;
  overflow: hidden;
  color: #d6d2ff;
  font-size: .63rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.map-canvas {
  --map-art: none;

  position: relative;
  min-height: 25rem;
  overflow: hidden;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: calc(var(--ui-radius-lg) + 3px);
  background:
    linear-gradient(180deg, rgb(7 10 17 / 20%), rgb(5 8 14 / 78%)),
    var(--map-art) center / cover;
  box-shadow: var(--ui-shadow-inset), 0 18px 46px rgb(0 0 0 / 28%);
  isolation: isolate;
}

.map-canvas::after {
  position: absolute;
  inset: 0;
  z-index: -1;
  background:
    radial-gradient(circle at 50% 44%, transparent 0 22%, rgb(3 5 10 / 30%) 70%),
    linear-gradient(180deg, rgb(9 12 21 / 10%), rgb(4 7 12 / 62%));
  content: '';
}

.map-canvas__fog {
  position: absolute;
  inset: -15%;
  background:
    radial-gradient(circle at 22% 18%, rgb(146 136 255 / 10%), transparent 20%),
    radial-gradient(circle at 75% 70%, rgb(65 123 126 / 12%), transparent 24%);
  filter: blur(22px);
  pointer-events: none;
}

.map-canvas__grid {
  position: absolute;
  inset: 0;
  background-image:
    linear-gradient(rgb(255 255 255 / 2%) 1px, transparent 1px),
    linear-gradient(90deg, rgb(255 255 255 / 2%) 1px, transparent 1px);
  background-size: 2.5rem 2.5rem;
  mask-image: linear-gradient(180deg, rgb(0 0 0 / 55%), transparent 88%);
  pointer-events: none;
}

.map-canvas__caption {
  position: absolute;
  top: var(--ui-space-3);
  left: var(--ui-space-3);
  z-index: 1;
  display: grid;
  gap: 1px;
  padding: 6px 8px;
  border-left: 2px solid rgb(170 163 255 / 55%);
  background: linear-gradient(90deg, rgb(5 8 14 / 70%), transparent);
  text-shadow: 0 2px 7px rgb(0 0 0 / 65%);
}

.map-canvas__caption small {
  color: #aaa3ff;
  font-size: .48rem;
  font-weight: 800;
  letter-spacing: .1em;
}

.map-canvas__caption strong {
  font-family: var(--ui-font-display);
  font-size: .72rem;
}

.map-routes {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  overflow: visible;
  pointer-events: none;
}

.map-routes line {
  stroke: rgb(184 177 255 / 55%);
  stroke-width: .45;
  stroke-dasharray: 2 1.4;
  vector-effect: non-scaling-stroke;
  filter: drop-shadow(0 0 3px rgb(146 136 255 / 45%));
}

.map-node {
  position: absolute;
  z-index: 2;
  display: grid;
  min-width: 0;
  place-items: center;
  gap: 3px;
  padding: 0;
  border: 0;
  background: transparent;
  color: var(--ui-color-text-secondary);
  font: inherit;
  text-align: center;
  transform: translate(-50%, -50%);
  cursor: pointer;
}

.map-node__pulse {
  position: absolute;
  top: 0;
  width: 2.8rem;
  height: 2.8rem;
  border: 1px solid transparent;
  border-radius: 50%;
  opacity: 0;
}

.map-node__marker {
  position: relative;
  display: grid;
  width: 2rem;
  height: 2rem;
  place-items: center;
  border: 1px solid rgb(120 127 154 / 52%);
  border-radius: 50%;
  background: linear-gradient(180deg, rgb(30 35 49 / 96%), rgb(9 13 22 / 98%));
  box-shadow: 0 .35rem .9rem rgb(0 0 0 / 42%);
}

.map-node__marker i {
  width: .48rem;
  height: .48rem;
  border-radius: 50%;
  background: #7c849b;
}

.map-node__label {
  display: grid;
  max-width: 8.5rem;
  gap: 1px;
  padding: 3px 6px;
  border-radius: var(--ui-radius-sm);
  background: rgb(5 8 14 / 76%);
  backdrop-filter: blur(6px);
}

.map-node__label strong {
  overflow: hidden;
  font-size: .69rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.map-node__label small {
  color: var(--ui-color-text-muted);
  font-size: .55rem;
}

.map-node[data-state='reachable'] .map-node__marker {
  border-color: rgb(171 163 255 / 72%);
}

.map-node[data-state='reachable'] .map-node__marker i {
  background: #aaa3ff;
  box-shadow: 0 0 8px rgb(146 136 255 / 65%);
}

.map-node[data-state='current'] .map-node__marker {
  border-color: rgb(92 211 168 / 78%);
  box-shadow: 0 0 0 3px rgb(79 185 150 / 10%), 0 .35rem .9rem rgb(0 0 0 / 42%);
}

.map-node[data-state='current'] .map-node__marker i {
  background: #72d5b2;
  box-shadow: 0 0 10px rgb(79 185 150 / 70%);
}

.map-node[data-state='current'] .map-node__pulse {
  border-color: rgb(79 185 150 / 44%);
  opacity: 1;
  animation: map-pulse 2.1s ease-out infinite;
}

.map-node__marker[data-location-kind='dungeon'] {
  width: 2.35rem;
  height: 2.35rem;
  border-color: rgb(224 188 100 / 72%);
  border-radius: 35%;
  transform: rotate(45deg);
}

.map-node__marker[data-location-kind='dungeon'] i {
  width: .62rem;
  height: .62rem;
  border-radius: 2px;
  background: var(--ui-color-gold);
  box-shadow: 0 0 10px rgb(224 188 100 / 68%);
  transform: rotate(-45deg);
}

.map-node[data-state='locked'] {
  opacity: .55;
}

.map-node--selected .map-node__label {
  outline: 1px solid rgb(184 177 255 / 48%);
  background: rgb(18 20 37 / 88%);
  color: #ebe9ff;
}

.map-selection {
  position: absolute;
  right: var(--ui-space-3);
  bottom: 4.6rem;
  left: var(--ui-space-3);
  z-index: 4;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-2);
  padding: 8px 9px;
  border: 1px solid rgb(184 177 255 / 30%);
  border-radius: var(--ui-radius-md);
  background: rgb(5 8 14 / 86%);
  backdrop-filter: blur(10px);
  pointer-events: none;
}

.map-selection button {
  pointer-events: auto;
}

.map-selection > div {
  display: grid;
  min-width: 0;
  gap: 1px;
}

.map-selection small {
  color: #aaa3ff;
  font-size: .48rem;
  font-weight: 800;
  letter-spacing: .08em;
}

.map-selection strong {
  overflow: hidden;
  font-size: .65rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.map-selection span {
  color: var(--ui-color-text-muted);
  font-size: .52rem;
}

.contract-card {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-3);
  border-color: color-mix(in srgb, var(--ui-color-gold) 36%, var(--ui-color-border));
}

.contract-card > div {
  display: grid;
  gap: 3px;
}

.contract-card small {
  color: var(--ui-color-gold);
  font-size: .56rem;
  font-weight: 800;
  letter-spacing: .08em;
}

.contract-card strong {
  font-family: var(--ui-font-display);
}

.contract-card p {
  margin: 0;
  color: var(--ui-color-text-muted);
  font-size: .68rem;
  line-height: 1.4;
}

.contract-card > span {
  flex: 0 0 auto;
  color: var(--ui-color-text-secondary);
  font-size: .58rem;
}

.location-preview__lock {
  color: #e1bd78 !important;
}

.map-legend {
  position: absolute;
  right: var(--ui-space-3);
  bottom: var(--ui-space-3);
  left: var(--ui-space-3);
  display: flex;
  flex-wrap: wrap;
  gap: var(--ui-space-2);
  padding: var(--ui-space-2);
  border: 1px solid rgb(255 255 255 / 7%);
  border-radius: var(--ui-radius-md);
  background: rgb(5 8 14 / 68%);
  color: var(--ui-color-text-muted);
  font-size: .57rem;
  backdrop-filter: blur(8px);
}

.map-legend span {
  display: flex;
  align-items: center;
  gap: 5px;
}

.map-legend i {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: #727a8e;
}

.map-legend i[data-state='current'] {
  background: #72d5b2;
}

.map-legend i[data-state='reachable'] {
  background: #aaa3ff;
}

.location-preview {
  display: grid;
  grid-template-columns: minmax(7.5rem, 10rem) minmax(0, 1fr);
  gap: var(--ui-space-3);
  padding: 0;
  overflow: hidden;
}

.location-preview__art {
  position: relative;
  min-height: 11rem;
  background-position: center;
  background-size: cover;
}

.location-preview__art > div {
  position: absolute;
  inset: 0;
  background: linear-gradient(90deg, transparent 35%, var(--ui-color-surface-1) 100%);
}

.location-preview__body {
  display: grid;
  align-content: center;
  gap: var(--ui-space-2);
  padding: var(--ui-space-3) var(--ui-space-3) var(--ui-space-3) 0;
}

.location-preview__body h2 {
  margin-top: 2px;
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-xl);
}

.location-preview__eyebrow {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ui-space-1);
}

.location-preview__eyebrow span {
  padding: 3px 6px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-round);
  color: var(--ui-color-text-muted);
  font-size: .56rem;
  text-transform: uppercase;
}

.location-preview__eyebrow span[data-danger='SAFE'] {
  border-color: rgb(79 185 150 / 35%);
  color: #84d5bb;
}

.location-preview__eyebrow span[data-danger='ADVENTURE'] {
  border-color: rgb(208 164 88 / 38%);
  color: #e1bd78;
}

.location-preview__eyebrow span[data-danger='DANGEROUS'] {
  border-color: rgb(216 95 114 / 42%);
  color: #ef8fa0;
}

.location-preview__actions {
  display: flex;
  gap: var(--ui-space-2);
}

@keyframes map-pulse {
  from {
    opacity: .65;
    transform: scale(.7);
  }
  to {
    opacity: 0;
    transform: scale(1.55);
  }
}

@media (prefers-reduced-motion: reduce) {
  .map-node[data-state='current'] .map-node__pulse {
    animation: none;
  }
}

@media (max-width: 420px) {
  .world-map {
    padding-inline: var(--ui-space-3);
  }

  .world-map__header {
    align-items: start;
    flex-direction: column;
  }

  .world-map__meta {
    width: 100%;
    box-sizing: border-box;
    justify-items: start;
  }

  .map-canvas {
    min-height: 23rem;
  }

  .map-node__label {
    max-width: 6.6rem;
  }

  .location-preview {
    grid-template-columns: 1fr;
  }

  .location-preview__art {
    min-height: 8rem;
  }

  .location-preview__art > div {
    background: linear-gradient(180deg, transparent 20%, var(--ui-color-surface-1) 100%);
  }

  .location-preview__body {
    padding: 0 var(--ui-space-3) var(--ui-space-3);
  }
}
</style>
