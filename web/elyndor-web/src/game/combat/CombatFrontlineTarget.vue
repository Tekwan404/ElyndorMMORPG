<script setup lang="ts">
import { computed, onMounted, onUnmounted } from 'vue'

import type { CombatActorSnapshot } from '@/api/contracts'
import { useCombatSessionStore } from '@/stores/combatSession'
import IconGenerator from '@/ui/icons/IconGenerator.vue'

const props = defineProps<{
  ally: CombatActorSnapshot | null
}>()

const combat = useCombatSessionStore()
let telemetryTimer: number | null = null

const threatPercent = computed<number | null>(() => {
  const current = combat.threat
  const ally = props.ally
  const selectedEnemyActorId = combat.snapshot?.selectedTargetActorId ?? combat.snapshot?.enemy.actorId
  if (!current || !ally || !selectedEnemyActorId || current.enemyActorId !== selectedEnemyActorId) return null
  const entry = current.entries.find(candidate => candidate.actorId === ally.actorId)
  if (!entry) return null
  const maximumThreat = Math.max(0, ...current.entries.map(candidate => candidate.threat))
  if (maximumThreat <= 0) return null
  return Math.round(Math.min(100, Math.max(0, (entry.threat / maximumThreat) * 100)))
})

onMounted(() => {
  void combat.refreshCombatTelemetry()
  telemetryTimer = window.setInterval(() => {
    void combat.refreshCombatTelemetry()
  }, 1_000)
})

onUnmounted(() => {
  if (telemetryTimer !== null) window.clearInterval(telemetryTimer)
})
</script>

<template>
  <Teleport to=".combat-hud__actor--enemy">
    <Transition name="frontline-shift" mode="out-in">
      <aside
        v-if="ally"
        :key="ally.actorId"
        class="combat-frontline"
        :aria-label="`Противник держит агро на ${ally.name}${threatPercent === null ? '' : `, угроза ${threatPercent}%`}`"
      >
        <span class="combat-frontline__marker" aria-hidden="true">
          <IconGenerator :config="{ id: `frontline-${ally.actorId}`, glyph: 'sword', category: 'utility' }" />
        </span>
        <span class="combat-frontline__label">ЦЕЛЬ</span>
        <strong>{{ ally.name }}</strong>
        <span v-if="threatPercent !== null" class="combat-frontline__threat">{{ threatPercent }}%</span>
      </aside>
    </Transition>
  </Teleport>
</template>

<style scoped>
.combat-frontline {
  display: flex;
  min-width: 0;
  min-height: 26px;
  align-items: center;
  gap: 5px;
  margin-top: 1px;
  padding: 3px 6px 3px 4px;
  border: 1px solid rgb(205 177 113 / 38%);
  border-radius: var(--ui-radius-round);
  background: rgb(205 177 113 / 7%);
  color: var(--ui-color-text-primary);
}

.combat-frontline__marker {
  display: grid;
  width: 19px;
  height: 19px;
  flex: 0 0 19px;
  place-items: center;
  border-radius: 50%;
  color: var(--ui-color-gold);
}

.combat-frontline__marker :deep(.icon-generator) {
  width: 17px;
  height: 17px;
  border: 0;
  background: transparent;
  box-shadow: none;
}

.combat-frontline__label {
  flex: 0 0 auto;
  color: var(--ui-color-gold-muted);
  font-size: .46rem;
  font-weight: 900;
  letter-spacing: .08em;
}

.combat-frontline strong {
  overflow: hidden;
  min-width: 0;
  color: #f0dfb1;
  font-size: .56rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.combat-frontline__threat {
  flex: 0 0 auto;
  color: #efa1ae;
  font-size: .54rem;
  font-weight: 900;
  font-variant-numeric: tabular-nums;
}

.frontline-shift-enter-active,
.frontline-shift-leave-active {
  transition: opacity 180ms ease, transform 180ms ease;
}

.frontline-shift-enter-from,
.frontline-shift-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}
</style>