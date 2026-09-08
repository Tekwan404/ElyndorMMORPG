<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { socialErrorMessage } from '@/game/social/socialPresentation'

import { useDungeonStore } from '@/game/party/dungeonStore'
import { usePartyStore } from '@/game/party/partyStore'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UICard } from '@/ui/components'

const emit = defineEmits<{ 'open-party': [] }>()

const party = usePartyStore()
const dungeon = useDungeonStore()
const combat = useCombatSessionStore()
const session = useGameSessionStore()
const currentCharacterId = computed(() => session.snapshot?.character?.id ?? '')
const preview = computed(() => dungeon.previews.find(item => item.id === 'ANCIENT_MINE') ?? null)
const isLeader = computed(() => party.snapshot?.leaderCharacterId === currentCharacterId.value)
const currentMember = computed(() => dungeon.current?.members.find(member => member.characterId === currentCharacterId.value))
const needsEntry = computed(() => currentMember.value?.state !== 'Active')
const currentEncounter = computed(() => dungeon.current?.encounters.find(
  encounter => encounter.encounterIndex === dungeon.current?.currentEncounterIndex,
))
const canStart = computed(() => Boolean(
  dungeon.current
    && isLeader.value
    && currentMember.value?.state === 'Active'
    && dungeon.current.state === 'Active'
    && currentEncounter.value
    && currentEncounter.value.state !== 'Active',
))

onMounted(() => {
  void Promise.all([party.refresh(), dungeon.refresh()])
})

async function createRun(): Promise<void> {
  if (preview.value) await dungeon.create(preview.value.id)
}

async function enterRun(): Promise<void> {
  if (dungeon.current) await dungeon.enter(dungeon.current.runId)
}

async function startEncounter(): Promise<void> {
  if (!dungeon.current || !canStart.value) return
  await combat.connect()
  await combat.startDungeonEncounter(dungeon.current.runId)
}

async function restartEncounter(): Promise<void> {
  if (dungeon.current && isLeader.value) await dungeon.restart(dungeon.current.runId)
}

async function exitRun(): Promise<void> {
  if (dungeon.current) await dungeon.exit(dungeon.current.runId)
}
</script>

<template>
  <UICard class="dungeon-location-card" data-dungeon-location-card>
    <header class="dungeon-location-card__header">
      <div>
        <small>ПОДЗЕМЕЛЬЕ · ГРУППОВАЯ АКТИВНОСТЬ</small>
        <h2>Древняя шахта</h2>
      </div>
      <span class="dungeon-badge">15+</span>
    </header>

    <p>Пять столкновений под глубоким лесом. Лидер запускает бой, остальные участники входят в текущий забег из этой локации.</p>
    <div class="dungeon-location-card__requirements">
      <span>Уровень 15+ · враги 15–16</span>
      <span>Группа 1–5</span>
      <span>5 столкновений</span>
    </div>

    <p>Финальный босс: Прародительница Глубин</p>
    <UIButton variant="secondary" @click="emit('open-party')">Открыть группу</UIButton>
    <p v-if="dungeon.errorCode" class="dungeon-error" role="alert">{{ socialErrorMessage(dungeon.errorCode) }}</p>

    <template v-if="dungeon.current">
      <p v-if="dungeon.current.state === 'Completed'">Подземелье пройдено!</p>
      <p v-else-if="dungeon.current.state === 'Abandoned'">Забег завершён без награды за прохождение.</p>
      <p v-else-if="!isLeader">Следующее столкновение запускает лидер группы.</p>
      <div class="dungeon-run-status">
        <strong>Текущий забег · {{ Math.min(dungeon.current.currentEncounterIndex + 1, dungeon.current.encounterCount) }}/{{ dungeon.current.encounterCount }}</strong>
        <small>Пройдено столкновений: {{ dungeon.current.currentEncounterIndex }}</small>
      </div>
      <div class="dungeon-progress" aria-label="Прогресс подземелья">
        <span v-for="encounter in dungeon.current.encounters" :key="encounter.encounterId" :data-state="encounter.state">
          {{ encounter.encounterIndex + 1 }}
        </span>
      </div>
      <div class="dungeon-actions">
        <UIButton v-if="needsEntry && dungeon.current.state === 'Active'" variant="secondary" @click="enterRun">Войти в текущий забег</UIButton>
        <UIButton v-if="dungeon.current.state !== 'Active' && isLeader" @click="createRun">Новый забег</UIButton>
        <UIButton v-else-if="canStart" :loading="combat.pending" data-start-dungeon @click="startEncounter">Начать бой</UIButton>
        <UIButton v-if="isLeader && dungeon.current.encounters.some(encounter => encounter.state === 'Wiped')" variant="secondary" @click="restartEncounter">Перезапустить</UIButton>
        <UIButton v-if="currentMember?.state === 'Active' && !dungeon.current.encounters.some(encounter => encounter.state === 'Active')" variant="ghost" @click="exitRun">Выйти из забега</UIButton>
      </div>
    </template>

    <template v-else>
      <p v-if="!party.snapshot" class="dungeon-hint">Создайте группу или примите приглашение перед входом в подземелье.</p>
      <p v-else-if="!isLeader" class="dungeon-hint">Забег создаёт только лидер группы.</p>
      <UIButton v-if="!party.snapshot" variant="secondary" @click="party.create">Создать группу</UIButton>
      <UIButton v-else-if="isLeader" data-create-dungeon @click="createRun">Создать забег</UIButton>
    </template>
  </UICard>
</template>

<style scoped>
.dungeon-location-card { display: grid; gap: 12px; border-color: rgb(190 153 82 / 38%); background: radial-gradient(circle at 100% 0, rgb(190 153 82 / 12%), transparent 13rem), var(--ui-color-surface-1); }
.dungeon-location-card__header { display: flex; align-items: start; justify-content: space-between; gap: 12px; }
.dungeon-location-card__header small { color: var(--ui-color-gold); font-size: .54rem; letter-spacing: .12em; }
.dungeon-location-card h2 { margin: 4px 0 0; font-family: var(--ui-font-display); font-size: 1.25rem; }
.dungeon-location-card p, .dungeon-hint { margin: 0; color: var(--ui-color-text-muted); font-size: .72rem; line-height: 1.5; }
.dungeon-badge { display: grid; min-width: 2.3rem; height: 2.3rem; place-items: center; border: 1px solid var(--ui-color-gold); border-radius: 50%; color: var(--ui-color-gold); font-weight: 800; }
.dungeon-location-card__requirements { display: flex; flex-wrap: wrap; gap: 6px; }
.dungeon-location-card__requirements span { padding: 4px 7px; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-round); color: var(--ui-color-text-muted); font-size: .6rem; }
.dungeon-run-status { display: grid; gap: 3px; padding-top: 4px; }
.dungeon-run-status small { color: var(--ui-color-text-muted); font-size: .63rem; }
.dungeon-progress { display: flex; gap: 6px; }
.dungeon-progress span { display: grid; width: 27px; height: 27px; place-items: center; border: 1px solid var(--ui-color-border); border-radius: 50%; color: var(--ui-color-text-muted); font-size: .64rem; }
.dungeon-progress span[data-state='Completed'] { border-color: var(--ui-color-success); color: var(--ui-color-success); }
.dungeon-progress span[data-state='Active'] { border-color: var(--ui-color-primary); color: var(--ui-color-primary); }
.dungeon-actions { display: flex; flex-wrap: wrap; gap: 7px; }
.dungeon-error { color: var(--ui-color-danger, #ff8d8d) !important; }
</style>
