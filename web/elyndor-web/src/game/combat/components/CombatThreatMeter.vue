<script setup lang="ts">
import { computed } from 'vue'

import type { CombatThreatSnapshot } from '@/stores/combatSession'

const props = defineProps<{
  threat: CombatThreatSnapshot | null
  enemyActorId: string
  localActorId: string
}>()

// The threat query always belongs to the viewer's currently selected enemy.
// Never display a previous target's values while the next query is in flight.
const currentThreat = computed(() =>
  props.threat?.enemyActorId === props.enemyActorId ? props.threat : null,
)
const entries = computed(() => currentThreat.value?.entries.slice(0, 5) ?? [])
const maxThreat = computed(() => Math.max(0, ...entries.value.map((entry) => entry.threat)))

function threatPercent(value: number): number {
  return maxThreat.value > 0 ? Math.min(100, Math.max(0, (value / maxThreat.value) * 100)) : 0
}

function isCurrentTarget(actorId: string): boolean {
  return currentThreat.value?.currentTargetActorId === actorId
}

function isForcedTarget(actorId: string): boolean {
  return currentThreat.value?.forcedTargetActorId === actorId
}
</script>

<template>
  <aside class="combat-threat-meter" data-combat-threat-meter aria-label="Таблица угрозы">
    <header class="combat-threat-meter__heading">
      <b>УГРОЗА</b>
      <span :title="currentThreat?.enemyName ?? 'Ожидание данных'">{{ currentThreat?.enemyName ?? '—' }}</span>
    </header>
    <ol v-if="entries.length" class="combat-threat-meter__list">
      <li
        v-for="entry in entries"
        :key="entry.actorId"
        :class="{
          'is-self': entry.actorId === localActorId,
          'is-target': isCurrentTarget(entry.actorId),
          'is-forced': isForcedTarget(entry.actorId),
        }"
        :data-threat-actor-id="entry.actorId"
      >
        <div class="combat-threat-meter__line">
          <span :title="entry.name">{{ entry.name }}</span>
          <strong>{{ Math.round(entry.threat).toLocaleString('ru-RU') }}</strong>
        </div>
        <i class="combat-threat-meter__bar" aria-hidden="true">
          <span :style="{ width: `${threatPercent(entry.threat)}%` }" />
        </i>
        <small v-if="isForcedTarget(entry.actorId)">ПРОВОКАЦИЯ</small>
        <small v-else-if="isCurrentTarget(entry.actorId)">ЦЕЛЬ ВРАГА</small>
        <small v-else-if="entry.actorId === localActorId">ВЫ</small>
      </li>
    </ol>
    <p v-else class="combat-threat-meter__empty">Ожидание угрозы…</p>
  </aside>
</template>

<style scoped>
.combat-threat-meter {
  position: absolute;
  top: 3.55rem;
  left: 0.4rem;
  z-index: 68;
  display: grid;
  width: min(10.8rem, 46vw);
  max-height: min(19rem, 42svh);
  gap: 0.38rem;
  padding: 0.5rem;
  overflow: hidden;
  border: 1px solid rgb(183 154 91 / 38%);
  border-radius: 8px;
  background: rgb(7 10 17 / 91%);
  color: #e9dfd4;
  box-shadow: 0 5px 18px rgb(0 0 0 / 35%);
  font-family: var(--ui-font-body);
  pointer-events: none;
}
.combat-threat-meter__heading {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 0.3rem;
  min-width: 0;
}
.combat-threat-meter__heading b {
  color: #d9b477;
  font-size: 0.58rem;
  letter-spacing: 0.08em;
}
.combat-threat-meter__heading span {
  overflow: hidden;
  color: #b8b0a7;
  font-size: 0.53rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.combat-threat-meter__list {
  display: grid;
  gap: 0.34rem;
  margin: 0;
  padding: 0;
  overflow: hidden;
  list-style: none;
}
.combat-threat-meter__list li {
  display: grid;
  gap: 0.12rem;
  min-width: 0;
}
.combat-threat-meter__line {
  display: flex;
  justify-content: space-between;
  gap: 0.25rem;
  align-items: baseline;
  min-width: 0;
  font-size: 0.62rem;
}
.combat-threat-meter__line span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.combat-threat-meter__line strong {
  flex: none;
  color: #b8b0a7;
  font-variant-numeric: tabular-nums;
  font-size: 0.59rem;
}
.combat-threat-meter__bar {
  display: block;
  height: 3px;
  overflow: hidden;
  border-radius: 8px;
  background: rgb(255 255 255 / 8%);
}
.combat-threat-meter__bar span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: #8c98a7;
}
.combat-threat-meter__list li.is-self .combat-threat-meter__bar span {
  background: #d5aa6a;
}
.combat-threat-meter__list li.is-target .combat-threat-meter__bar span {
  background: #df6677;
}
.combat-threat-meter__list li.is-forced .combat-threat-meter__bar span {
  background: #f0c15d;
}
.combat-threat-meter__list li.is-target .combat-threat-meter__line span,
.combat-threat-meter__list li.is-forced .combat-threat-meter__line span {
  color: #fff3e0;
  font-weight: 800;
}
.combat-threat-meter__list small {
  color: #f0bb7a;
  font-size: 0.49rem;
  font-weight: 800;
}
.combat-threat-meter__empty {
  margin: 0;
  color: #b8b0a7;
  font-size: 0.58rem;
}
@media (max-width: 350px) {
  .combat-threat-meter {
    width: 46vw;
    padding: 0.38rem;
  }
}
</style>
