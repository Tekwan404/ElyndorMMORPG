<script setup lang="ts">
import { computed, nextTick, ref, toRef } from 'vue'

import { monsterArtUrl } from '@/assets/monsterArt'
import ItemIcon from '@/game/items/components/ItemIcon.vue'
import type { WorldSceneObject } from '@/game/world/locationDetails'
import { useLocationScene } from '@/game/world/useLocationScene'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'
import { formatMoney } from '@/shared/money'
import { UIButton } from '@/ui/components'
import IconGenerator from '@/ui/icons/IconGenerator.vue'

const props = defineProps<{
  locationId: string
  contentVersion: string
  title: string
  background: string
  levelLabel: string
  canAttack: boolean
}>()
const session = useGameSessionStore()
const combat = useCombatSessionStore()
const root = ref<HTMLElement | null>(null)
const busy = ref(false)
const questLoading = ref(false)
const actionError = ref<string | null>(null)
const {
  scene,
  selected,
  selectedId,
  page,
  pageCount,
  visibleObjects,
  loading,
  error,
  refresh,
  select,
  turnPage,
} = useLocationScene(toRef(props, 'locationId'), toRef(props, 'contentVersion'))
const locked = computed(
  () =>
    busy.value ||
    session.mutationPending ||
    combat.pending ||
    combat.isActive ||
    session.snapshot?.world?.travel != null ||
    session.snapshot?.afkFarm?.status === 'Active',
)
const stateLabel = computed(() =>
  scene.value?.state === 'Invasion'
    ? 'Нашествие'
    : scene.value?.state === 'Corruption'
      ? 'Осквернение'
      : 'Спокойствие',
)
const selectedQuest = computed(() =>
  session.questJournal?.quests.find((quest) => quest.id === selected.value?.questId),
)
const questStatusLabel = computed(() => {
  switch (selectedQuest.value?.status) {
    case 'ACTIVE':
      return 'Задание принято'
    case 'READY_TO_CLAIM':
      return 'Награда готова'
    case 'COMPLETED':
      return 'Задание выполнено'
    case 'LOCKED':
      return 'Сначала выполните условия задания'
    case 'AVAILABLE':
      return 'Доступно'
    default:
      return 'Не удалось получить задание'
  }
})

function rankLabel(object: WorldSceneObject): string {
  const rank = object.resident?.rank
  const label = rank === 'Elite' ? 'Элитный' : rank === 'Boss' ? 'Босс' : 'Обычный'
  return object.isRare ? `Редкий · ${label}` : label
}

async function choose(object: WorldSceneObject): Promise<void> {
  if (busy.value) return
  actionError.value = null
  select(object)
  if (object.questId) {
    questLoading.value = true
    await session.refreshQuestJournal()
    questLoading.value = false
  }
}

async function closePanel(): Promise<void> {
  const previous = selectedId.value
  selectedId.value = null
  actionError.value = null
  await nextTick()
  const markers = root.value?.querySelectorAll<HTMLButtonElement>('[data-scene-marker]')
  Array.from(markers ?? [])
    .find((marker) => marker.dataset.sceneMarker === previous)
    ?.focus()
}

async function attack(): Promise<void> {
  const object = selected.value
  if (!props.canAttack || locked.value || !object?.resident) return
  busy.value = true
  actionError.value = null
  const locationId = props.locationId
  try {
    const encounter = await session.selectEncounter(locationId, object.resident.monsterId)
    if (locationId !== props.locationId) return
    if (!encounter) {
      actionError.value =
        session.errorCode === 'world_target_unavailable'
          ? 'Противник уже покинул область. Обновляем локацию.'
          : 'Не удалось начать бой. Проверьте связь и повторите попытку.'
      await refresh()
      return
    }
    if (await combat.startCombat(encounter)) selectedId.value = null
    else
      actionError.value =
        'Не удалось войти в бой. Повторите нападение или восстановите текущий бой.'
  } finally {
    busy.value = false
  }
}

async function interactWithQuest(): Promise<void> {
  const quest = selectedQuest.value
  if (!quest || locked.value || questLoading.value) return
  busy.value = true
  actionError.value = null
  try {
    if (quest.status === 'AVAILABLE') await session.acceptQuest(quest.id)
    else if (quest.status === 'READY_TO_CLAIM') await session.claimQuest(quest.id)
    if (session.errorCode)
      actionError.value = 'Не удалось выполнить действие. Проверьте условия задания и повторите.'
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <section
    ref="root"
    class="location-scene"
    aria-label="Интерактивная локация"
    @keydown.esc="closePanel"
  >
    <img class="location-scene__background" :src="background" alt="" aria-hidden="true" />
    <div class="location-scene__shade" />
    <header class="location-scene__heading">
      <div>
        <h1>{{ title }}</h1>
        <span>{{ levelLabel }}</span>
      </div>
      <span v-if="scene" class="location-scene__state" :data-world-state="scene.state">{{
        stateLabel
      }}</span>
    </header>
    <p class="location-scene__hint">Выберите, с кем сражаться или о чём узнать</p>

    <div v-if="loading && !scene" class="location-scene__feedback" role="status">
      Осматриваем окрестности…
    </div>
    <div v-else-if="error" class="location-scene__feedback" role="alert">
      <p>{{ error }}</p>
      <UIButton variant="secondary" data-scene-retry @click="refresh">Повторить</UIButton>
    </div>
    <p v-else-if="scene && !scene.objects.length" class="location-scene__feedback" role="status">
      Поблизости нет доступных точек взаимодействия.
    </p>

    <button
      v-for="object in visibleObjects"
      :key="object.id"
      class="location-scene__marker"
      :class="{ 'location-scene__marker--rare': object.isRare }"
      :data-scene-marker="object.id"
      :data-kind="object.kind"
      :data-rank="object.resident?.rank"
      :style="{ left: object.x + '%', top: object.y + '%' }"
      :aria-label="
        object.resident
          ? object.displayName + ', ур. ' + object.resident.level + ', ' + rankLabel(object)
          : object.displayName
      "
      :aria-expanded="selectedId === object.id"
      :disabled="busy"
      @click="choose(object)"
    >
      <span class="location-scene__medallion">
        <img
          v-if="object.resident && monsterArtUrl(object.resident.artId)"
          :src="monsterArtUrl(object.resident.artId)!"
          alt=""
        />
        <IconGenerator
          v-else
          :config="{
            id: object.id,
            glyph: object.kind === 'Enemy' ? 'skull' : object.kind === 'Npc' ? 'scroll' : 'star',
            category: 'utility',
          }"
        />
      </span>
      <strong>{{ object.displayName }}</strong>
      <small>{{
        object.resident
          ? 'Ур. ' + object.resident.level
          : object.kind === 'Npc'
            ? 'Поручение'
            : 'Осмотреть'
      }}</small>
      <em v-if="object.isRare">Редкий</em>
    </button>

    <nav v-if="pageCount > 1" class="location-scene__pages" aria-label="Точки локации">
      <button :disabled="page === 0 || busy" aria-label="Предыдущие точки" @click="turnPage(-1)">
        ‹
      </button>
      <span>{{ page + 1 }} / {{ pageCount }}</span>
      <button
        data-scene-next
        :disabled="page + 1 >= pageCount || busy"
        aria-label="Следующие точки"
        @click="turnPage(1)"
      >
        ›
      </button>
    </nav>

    <section
      v-if="selected"
      class="location-scene__panel"
      data-scene-panel
      :aria-label="selected.displayName"
    >
      <button
        class="location-scene__close"
        aria-label="Закрыть взаимодействие"
        :disabled="busy"
        @click="closePanel"
      >
        ×
      </button>
      <div class="location-scene__panel-title">
        <img
          v-if="selected.resident && monsterArtUrl(selected.resident.artId)"
          :src="monsterArtUrl(selected.resident.artId)!"
          :alt="selected.displayName"
        />
        <div>
          <h2>{{ selected.displayName }}</h2>
          <span v-if="selected.resident"
            >Ур. {{ selected.resident.level }} · {{ rankLabel(selected) }}</span
          >
          <span v-else>{{ selected.kind === 'Npc' ? 'Житель области' : 'Место интереса' }}</span>
        </div>
      </div>
      <p>{{ selected.description }}</p>
      <template v-if="selected.resident">
        <small
          >+{{ selected.resident.xpReward }} опыта ·
          {{ formatMoney(selected.resident.goldRewardMin) }}–{{
            formatMoney(selected.resident.goldRewardMax)
          }}</small
        >
        <p v-if="selected.isRare" class="location-scene__rare-note">
          Редкое появление<template v-if="selected.availableUntilUtc">
            · до
            {{
              new Date(selected.availableUntilUtc).toLocaleTimeString('ru-RU', {
                hour: '2-digit',
                minute: '2-digit',
              })
            }}</template
          >
        </p>
        <details class="location-scene__loot">
          <summary>Возможная добыча · {{ selected.resident.loot?.length ?? 0 }}</summary>
          <ul v-if="selected.resident.loot?.length">
            <li v-for="item in selected.resident.loot" :key="item.itemId">
              <ItemIcon
                :item-id="item.itemId"
                :icon-id="item.iconId"
                :name="item.name"
                :type="item.type"
                :rarity="item.rarity"
              />
              <span>{{ item.name }}</span>
            </li>
          </ul>
          <p v-else>Предметы не предусмотрены.</p>
          <small>Добыча не гарантирована. Награду рассчитывает сервер.</small>
        </details>
        <UIButton data-scene-attack :loading="busy" :disabled="!canAttack || locked" @click="attack"
          >Напасть</UIButton
        >
        <small v-if="!canAttack"
          >Нападение сейчас недоступно. Завершите текущую активность; в группе бой начинает
          лидер.</small
        >
      </template>
      <template v-else-if="selected.questId">
        <p v-if="questLoading" role="status">Узнаём о поручении…</p>
        <template v-else>
          <strong v-if="selectedQuest">{{ selectedQuest.displayName }}</strong>
          <small>{{ questStatusLabel }}</small>
          <small v-if="selectedQuest"
            >Ур. {{ selectedQuest.requiredLevel }} · +{{ selectedQuest.rewardXp }} опыта ·
            {{ formatMoney(selectedQuest.rewardGold) }}</small
          >
          <UIButton
            v-if="
              selectedQuest?.status === 'AVAILABLE' || selectedQuest?.status === 'READY_TO_CLAIM'
            "
            data-scene-quest
            :loading="busy"
            :disabled="locked"
            @click="interactWithQuest"
          >
            {{ selectedQuest.status === 'AVAILABLE' ? 'Принять задание' : 'Получить награду' }}
          </UIButton>
          <UIButton v-else-if="!selectedQuest" variant="secondary" @click="choose(selected)"
            >Повторить</UIButton
          >
        </template>
      </template>
    </section>
    <p v-if="actionError" class="location-scene__error" role="alert">{{ actionError }}</p>
  </section>
</template>

<style scoped>
.location-scene {
  position: relative;
  isolation: isolate;
  height: clamp(380px, calc(100svh - 225px), 640px);
  overflow: hidden;
  border: 1px solid var(--ui-color-border-strong, #645339);
  border-radius: 10px;
  background: #080d12;
  color: var(--ui-color-text-primary, #f0e6d0);
}
.location-scene__background,
.location-scene__shade {
  position: absolute;
  z-index: -1;
  inset: 0;
  width: 100%;
  height: 100%;
}
.location-scene__background {
  object-fit: cover;
}
.location-scene__shade {
  background: linear-gradient(#050b12d9, transparent 35%, #050b1233 60%, #050b12d9);
}
.location-scene__heading {
  display: flex;
  justify-content: space-between;
  gap: 8px;
  padding: 16px 12px 0;
}
.location-scene h1 {
  margin: 0 0 4px;
  font-family: var(--ui-font-family-display);
  font-size: clamp(20px, 6vw, 28px);
  line-height: 1.1;
}
.location-scene__heading span {
  color: #cbc0a5;
  font-size: 11px;
}
.location-scene__state {
  align-self: start;
  padding: 4px 6px;
  border: 1px solid #7a856846;
  background: #10201cbb;
  white-space: nowrap;
}
.location-scene__hint {
  margin: 10px 12px;
  color: #bab3a4;
  font-size: 11px;
}
.location-scene__marker {
  position: absolute;
  display: grid;
  width: 86px;
  min-height: 80px;
  justify-items: center;
  gap: 2px;
  padding: 0;
  border: 0;
  background: transparent;
  color: #e8dfcb;
  font: inherit;
  text-align: center;
  transform: translate(-50%, -50%);
  cursor: pointer;
}
.location-scene__medallion {
  display: grid;
  width: 48px;
  height: 48px;
  place-items: center;
  overflow: hidden;
  border: 1px solid #a29a85;
  border-radius: 50%;
  background: #0a0f16df;
  box-shadow: 0 3px 14px #000c;
  transition:
    border-color 160ms,
    box-shadow 160ms;
}
.location-scene__medallion img {
  width: 100%;
  height: 100%;
  object-fit: contain;
}
.location-scene__medallion :deep(svg) {
  width: 26px;
  height: 26px;
}
.location-scene__marker strong {
  display: -webkit-box;
  width: 100%;
  overflow: hidden;
  padding: 2px 3px;
  background: #050a10c9;
  font-size: 11px;
  line-height: 1.2;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
}
.location-scene__marker small {
  padding: 1px 4px;
  background: #050a10c9;
  color: #c0b6a1;
  font-size: 10px;
}
.location-scene__marker em {
  color: #e2b8fc;
  font-size: 9px;
  font-style: normal;
  background: #130d1acd;
}
.location-scene__marker[data-rank='Elite'] .location-scene__medallion {
  border-color: #df9858;
}
.location-scene__marker--rare .location-scene__medallion {
  border-color: #be88e0;
}
.location-scene__marker[data-kind='Npc'] .location-scene__medallion {
  border-color: #a4ad73;
}
.location-scene__marker[aria-expanded='true'] .location-scene__medallion {
  box-shadow: 0 0 18px #deb7748a;
  border-color: #f4d18e;
}
.location-scene button:focus-visible,
.location-scene summary:focus-visible {
  outline: 2px solid #e3c88c;
  outline-offset: 3px;
}
.location-scene button:disabled {
  opacity: 0.5;
  cursor: default;
}
.location-scene__pages {
  position: absolute;
  right: 12px;
  bottom: 6px;
  left: 12px;
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 16px;
}
.location-scene__pages button {
  width: 44px;
  height: 44px;
  border: 0;
  background: #09111dc9;
  color: #dac38b;
  font-size: 26px;
}
.location-scene__pages span {
  font-size: 11px;
}
.location-scene__panel {
  position: absolute;
  z-index: 2;
  right: 8px;
  bottom: 8px;
  left: 8px;
  display: grid;
  gap: 8px;
  max-height: 68%;
  overflow-y: auto;
  padding: 12px;
  border: 1px solid #a4895b;
  border-radius: 5px;
  background: linear-gradient(#111923f5, #080c12fa);
  box-shadow: 0 -8px 28px #0009;
}
.location-scene__close {
  position: absolute;
  right: 0;
  top: 0;
  width: 44px;
  height: 44px;
  border: 0;
  background: transparent;
  color: #cfbea0;
  font-size: 24px;
}
.location-scene__panel-title {
  display: flex;
  align-items: center;
  gap: 10px;
  padding-right: 30px;
}
.location-scene__panel-title img {
  width: 58px;
  height: 58px;
  object-fit: contain;
  border: 1px solid #6e5c3e;
  background: #060b12;
}
.location-scene h2 {
  margin: 0 0 5px;
  font-size: 16px;
  font-family: var(--ui-font-family-display);
}
.location-scene__panel p {
  margin: 0;
  font-size: 12px;
  color: #c2b9a6;
  line-height: 1.4;
}
.location-scene__panel small,
.location-scene__panel-title span {
  color: #b8a989;
  font-size: 11px;
}
.location-scene__rare-note {
  color: #d9b2f4 !important;
}
.location-scene__loot summary {
  min-height: 32px;
  padding: 6px 0;
  cursor: pointer;
  font-size: 12px;
  color: #ddc69b;
}
.location-scene__loot ul {
  display: grid;
  gap: 4px;
  margin: 0 0 8px;
  padding: 0;
  list-style: none;
}
.location-scene__loot li {
  display: flex;
  gap: 8px;
  align-items: center;
  font-size: 11px;
}
.location-scene__loot li :deep(.item-icon) {
  width: 28px;
  height: 28px;
  flex: 0 0 28px;
}
.location-scene__feedback {
  position: absolute;
  top: 40%;
  right: 24px;
  left: 24px;
  padding: 16px;
  background: #070d16dd;
  font-size: 13px;
  text-align: center;
}
.location-scene__error {
  position: absolute;
  z-index: 3;
  right: 8px;
  top: 82px;
  left: 8px;
  padding: 10px;
  margin: 0;
  background: #431c1af5;
  color: #ffdfb9;
  font-size: 12px;
}
@media (prefers-reduced-motion: reduce) {
  .location-scene__medallion {
    transition: none;
  }
}
</style>
