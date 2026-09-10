<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { locationPresentation } from '@/game/world/locationPresentation'
import { socialErrorMessage } from '@/game/social/socialPresentation'
import { useDungeonStore } from '@/game/party/dungeonStore'
import { usePartyStore } from '@/game/party/partyStore'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton } from '@/ui/components'

const props = defineProps<{ dungeonId: string }>()
const emit = defineEmits<{ 'open-party': []; 'open-map': [] }>()

const party = usePartyStore()
const dungeon = useDungeonStore()
const combat = useCombatSessionStore()
const session = useGameSessionStore()
const hasLoaded = ref(false)

const currentCharacterId = computed(() => session.snapshot?.character?.id ?? '')
const preview = computed(() =>
  dungeon.previews.find(item => item.id === props.dungeonId) ?? null,
)
const current = computed(() =>
  dungeon.current?.dungeonId === props.dungeonId ? dungeon.current : null,
)
const isLeader = computed(() => party.snapshot?.leaderCharacterId === currentCharacterId.value)
const currentMember = computed(() => current.value?.members.find(
  member => member.characterId === currentCharacterId.value,
))
const needsEntry = computed(() => currentMember.value?.state !== 'Active')
const currentEncounter = computed(() => current.value?.encounters.find(
  encounter => encounter.encounterIndex === current.value?.currentEncounterIndex,
))
const canStart = computed(() => Boolean(
  current.value
    && isLeader.value
    && currentMember.value?.state === 'Active'
    && current.value.state === 'Active'
    && currentEncounter.value
    && currentEncounter.value.state !== 'Active',
))
const minimumLevel = computed(() => preview.value?.minimumLevel ?? 1)
const encounterCount = computed(() => preview.value?.encounters?.length ?? current.value?.encounterCount ?? 0)
const dungeonArt = computed(() => locationPresentation(
  props.dungeonId,
  preview.value?.displayName,
).art)
const cardState = computed<'loading' | 'error' | 'ready'>(() => {
  if (!hasLoaded.value || dungeon.loading) return 'loading'
  if (dungeon.errorCode || !preview.value) return 'error'
  return 'ready'
})
const stateLabel = computed(() => {
  if (!current.value) return 'ГОТОВО К ЗАПУСКУ'
  if (current.value.state === 'Completed') return 'ПРОЙДЕНО'
  if (current.value.state === 'Abandoned') return 'ЗАБЕГ ЗАВЕРШЁН'
  if (currentEncounter.value?.state === 'Active') return 'БОЙ ИДЁТ'
  if (currentEncounter.value?.state === 'Wiped') return 'ТРЕБУЕТСЯ ПОВТОР'
  return 'ЗАБЕГ АКТИВЕН'
})

async function refreshCard(): Promise<void> {
  hasLoaded.value = false
  try {
    await Promise.all([party.refresh(), dungeon.refresh()])
  } finally {
    hasLoaded.value = true
  }
}

onMounted(() => {
  void refreshCard()
})

async function createRun(): Promise<void> {
  if (!preview.value) return
  if (!party.snapshot) await party.create()
  if (!party.snapshot || party.snapshot.leaderCharacterId !== currentCharacterId.value) return
  await dungeon.create(preview.value.id)
}

async function enterRun(): Promise<void> {
  if (current.value) await dungeon.enter(current.value.runId)
}

async function startEncounter(): Promise<void> {
  if (!current.value || !canStart.value) return
  await combat.connect()
  await combat.startDungeonEncounter(current.value.runId)
}

async function restartEncounter(): Promise<void> {
  if (current.value && isLeader.value) await dungeon.restart(current.value.runId)
}

async function exitRun(): Promise<void> {
  if (current.value) await dungeon.exit(current.value.runId)
}
</script>

<template>
  <section
    class="dungeon-expedition"
    :class="{ 'dungeon-expedition--state': cardState !== 'ready' }"
    :style="{ '--dungeon-art': `url(${dungeonArt})` }"
    data-dungeon-location-card
    :data-dungeon-id="dungeonId"
  >
    <div v-if="cardState !== 'ready'" class="dungeon-expedition__state" role="status">
      <template v-if="cardState === 'loading'">
        <strong>Загружаем подземелье</strong>
        <p>Проверяем доступ и текущий забег.</p>
      </template>
      <template v-else>
        <strong>Подземелье временно недоступно</strong>
        <p>Не удалось получить описание этой локации. Можно повторить запрос или открыть карту мира.</p>
        <p v-if="dungeon.errorCode" class="dungeon-error" role="alert">{{ socialErrorMessage(dungeon.errorCode) }}</p>
        <div class="dungeon-actions">
          <UIButton variant="secondary" :loading="dungeon.loading" @click="refreshCard">Повторить</UIButton>
          <UIButton variant="ghost" @click="emit('open-map')">Карта мира</UIButton>
        </div>
      </template>
    </div>

    <template v-else-if="preview">
    <div class="dungeon-expedition__backdrop" />
    <div class="dungeon-expedition__content">
      <header class="dungeon-expedition__header">
        <div>
          <small>ИНСТАНС · {{ preview.minimumPartySize }}–{{ preview.maximumPartySize }} ИГРОКОВ</small>
          <h2>{{ preview.displayName }}</h2>
          <p>{{ preview.description }}</p>
        </div>
        <div class="dungeon-expedition__level">
          <span>УР.</span>
          <strong>{{ minimumLevel }}+</strong>
        </div>
      </header>

      <div class="dungeon-expedition__meta">
        <span>{{ encounterCount }} столкновений</span>
        <span>Группа до {{ preview.maximumPartySize }}</span>
        <span>{{ stateLabel }}</span>
      </div>

      <p v-if="dungeon.errorCode" class="dungeon-error" role="alert">
        {{ socialErrorMessage(dungeon.errorCode) }}
      </p>

      <template v-if="current">
        <div class="dungeon-run">
          <div class="dungeon-run__heading">
            <div>
              <small>ТЕКУЩИЙ ЗАБЕГ</small>
              <strong>
                Этап {{ Math.min(current.currentEncounterIndex + 1, current.encounterCount) }}
                / {{ current.encounterCount }}
              </strong>
            </div>
            <span>{{ stateLabel }}</span>
          </div>

          <div
            class="dungeon-progress"
            :style="{ '--encounter-columns': Math.min(Math.max(encounterCount, 1), 5) }"
            aria-label="Прогресс подземелья"
          >
            <div
              v-for="encounter in current.encounters"
              :key="encounter.encounterId"
              class="dungeon-progress__step"
              :data-state="encounter.state"
            >
              <i />
              <small>{{ encounter.encounterIndex + 1 }}</small>
            </div>
          </div>
        </div>

        <p v-if="current.state === 'Completed'" class="dungeon-hint">Подземелье пройдено. Можно начать новый забег.</p>
        <p v-else-if="current.state === 'Abandoned'" class="dungeon-hint">Предыдущий забег завершён. Он не смешивается с другими подземельями.</p>
        <p v-else-if="!isLeader" class="dungeon-hint">Следующее столкновение запускает лидер группы.</p>

        <div class="dungeon-actions">
          <UIButton
            v-if="needsEntry && current.state === 'Active'"
            variant="secondary"
            @click="enterRun"
          >Войти в забег</UIButton>
          <UIButton
            v-if="current.state !== 'Active' && isLeader"
            data-create-dungeon
            @click="createRun"
          >Новый забег</UIButton>
          <UIButton
            v-else-if="canStart"
            :loading="combat.pending"
            data-start-dungeon
            @click="startEncounter"
          >Начать столкновение</UIButton>
          <UIButton
            v-if="isLeader && current.encounters.some(encounter => encounter.state === 'Wiped')"
            variant="secondary"
            data-dungeon-restart
            @click="restartEncounter"
          >Повторить столкновение</UIButton>
          <UIButton
            v-if="currentMember?.state === 'Active' && !current.encounters.some(encounter => encounter.state === 'Active')"
            variant="ghost"
            data-dungeon-exit
            @click="exitRun"
          >Покинуть забег</UIButton>
          <UIButton variant="secondary" @click="emit('open-party')">Состав группы</UIButton>
          <UIButton variant="ghost" @click="emit('open-map')">Карта мира</UIButton>
        </div>
      </template>

      <template v-else>
        <div class="dungeon-ready">
          <div>
            <small>ЭКСПЕДИЦИЯ</small>
            <strong>{{ party.snapshot ? 'Группа готова к новому заходу' : 'Можно войти одному' }}</strong>
            <p>
              {{ party.snapshot
                ? 'Лидер создаёт инстанс для текущего состава.'
                : 'Для одиночного входа группа из одного игрока создастся автоматически.' }}
            </p>
          </div>
          <div class="dungeon-actions">
            <UIButton
              v-if="!party.snapshot"
              data-create-dungeon
              @click="createRun"
            >Начать одиночный забег</UIButton>
            <UIButton
              v-else-if="isLeader"
              data-create-dungeon
              @click="createRun"
            >Создать забег</UIButton>
            <UIButton v-else variant="secondary" @click="emit('open-party')">Открыть группу</UIButton>
          </div>
        </div>
      </template>
    </div>
    </template>
  </section>
</template>

<style scoped>
.dungeon-expedition {
  --dungeon-art: none;

  position: relative;
  min-height: 21rem;
  overflow: hidden;
  border: 1px solid rgb(207 170 90 / 42%);
  border-radius: calc(var(--ui-radius-lg) + 3px);
  background:
    linear-gradient(90deg, rgb(4 6 10 / 97%) 0 42%, rgb(4 6 10 / 78%) 68%, rgb(4 6 10 / 45%)),
    var(--dungeon-art) center / cover;
  box-shadow: var(--ui-shadow-inset), 0 1.2rem 2.8rem rgb(0 0 0 / 35%);
}

.dungeon-expedition__backdrop {
  position: absolute;
  inset: 0;
  background:
    radial-gradient(circle at 82% 24%, rgb(207 170 90 / 16%), transparent 24%),
    linear-gradient(180deg, transparent 45%, rgb(3 5 9 / 86%));
  pointer-events: none;
}

.dungeon-expedition__content {
  position: relative;
  z-index: 1;
  display: grid;
  min-height: 21rem;
  align-content: end;
  gap: var(--ui-space-3);
  padding: clamp(1rem, 4vw, 1.5rem);
}

.dungeon-expedition__header {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: start;
  gap: var(--ui-space-4);
}

.dungeon-expedition__header > div:first-child {
  display: grid;
  max-width: 34rem;
  gap: 5px;
}

.dungeon-expedition__header small,
.dungeon-ready small,
.dungeon-run__heading small {
  color: var(--ui-color-gold);
  font-size: .54rem;
  font-weight: 800;
  letter-spacing: .12em;
}

.dungeon-expedition h2 {
  margin: 0;
  font-family: var(--ui-font-display);
  font-size: clamp(1.55rem, 7vw, 2.2rem);
}

.dungeon-expedition p {
  margin: 0;
  color: #c2c8d4;
  font-size: .72rem;
  line-height: 1.5;
}

.dungeon-expedition__level {
  display: grid;
  min-width: 3.6rem;
  min-height: 3.6rem;
  place-items: center;
  align-content: center;
  border: 1px solid rgb(224 188 100 / 62%);
  border-radius: 50%;
  background: rgb(7 8 12 / 72%);
  box-shadow: 0 0 1.3rem rgb(224 188 100 / 10%);
}

.dungeon-expedition__level span {
  color: var(--ui-color-text-muted);
  font-size: .48rem;
}

.dungeon-expedition__level strong {
  color: var(--ui-color-gold);
  font-size: 1rem;
}

.dungeon-expedition__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.dungeon-expedition__meta span {
  padding: 5px 8px;
  border: 1px solid rgb(255 255 255 / 10%);
  border-radius: var(--ui-radius-round);
  background: rgb(5 7 11 / 58%);
  color: var(--ui-color-text-secondary);
  font-size: .58rem;
}

.dungeon-run,
.dungeon-ready {
  display: grid;
  gap: var(--ui-space-3);
  padding: var(--ui-space-3);
  border: 1px solid rgb(255 255 255 / 9%);
  border-radius: var(--ui-radius-md);
  background: rgb(5 8 14 / 82%);
  backdrop-filter: blur(12px);
}

.dungeon-run__heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-3);
}

.dungeon-run__heading > div,
.dungeon-ready > div:first-child {
  display: grid;
  gap: 3px;
}

.dungeon-run__heading > span {
  color: #d9c38a;
  font-size: .56rem;
  font-weight: 800;
}

.dungeon-progress {
  display: grid;
  grid-template-columns: repeat(var(--encounter-columns, 1), minmax(0, 1fr));
  gap: 5px;
}

.dungeon-progress__step {
  display: grid;
  min-height: 2.4rem;
  place-items: center;
  gap: 2px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  background: rgb(255 255 255 / 2%);
}

.dungeon-progress__step i {
  width: .45rem;
  height: .45rem;
  border-radius: 50%;
  background: #6f7789;
}

.dungeon-progress__step small {
  color: var(--ui-color-text-muted);
  font-size: .52rem;
}

.dungeon-progress__step[data-state='Completed'] {
  border-color: rgb(79 185 150 / 46%);
}

.dungeon-progress__step[data-state='Completed'] i {
  background: var(--ui-color-success);
}

.dungeon-progress__step[data-state='Active'] {
  border-color: rgb(224 188 100 / 58%);
}

.dungeon-progress__step[data-state='Active'] i,
.dungeon-progress__step[data-state='Wiped'] i {
  background: var(--ui-color-gold);
}

.dungeon-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 7px;
}

.dungeon-hint {
  color: var(--ui-color-text-muted) !important;
}

.dungeon-error {
  color: var(--ui-color-danger, #ff8d8d) !important;
}

.dungeon-expedition--state {
  display: grid;
  min-height: 15rem;
  place-items: center;
}

.dungeon-expedition__state {
  display: grid;
  max-width: 28rem;
  gap: var(--ui-space-2);
  padding: var(--ui-space-5);
  text-align: center;
}

.dungeon-expedition__state strong {
  font-family: var(--ui-font-display);
  font-size: 1.25rem;
}

.dungeon-expedition__state p {
  color: var(--ui-color-text-muted);
}

@media (max-width: 520px) {
  .dungeon-expedition {
    min-height: 21rem;
    background:
      linear-gradient(180deg, rgb(4 6 10 / 24%) 0 25%, rgb(4 6 10 / 94%) 66%),
      var(--dungeon-art) center top / cover;
  }

  .dungeon-expedition__content {
    min-height: 21rem;
  }

  .dungeon-expedition__header {
    grid-template-columns: minmax(0, 1fr) auto;
  }

  .dungeon-actions :deep(.ui-button) {
    flex: 1 1 10rem;
  }
}
</style>
