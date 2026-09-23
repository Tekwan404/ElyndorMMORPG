<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue'

import type { CombatActorSnapshot } from '@/api/contracts'
import { resolveCharacterArt } from '@/assets/characterArt'
import { useCombatSessionStore } from '@/stores/combatSession'
import IconGenerator from '@/ui/icons/IconGenerator.vue'

const props = defineProps<{
  ally: CombatActorSnapshot | null
}>()

const combat = useCombatSessionStore()
const portalReady = ref(
  typeof document !== 'undefined'
    && document.querySelector('.combat-hud__actor--enemy') !== null,
)
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

function allyArt(): string | null {
  return props.ally ? resolveCharacterArt(props.ally.definitionId, 'MALE') : null
}

onMounted(async () => {
  if (!portalReady.value) {
    await nextTick()
    portalReady.value = document.querySelector('.combat-hud__actor--enemy') !== null
  }
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
  <Teleport v-if="portalReady" to=".combat-hud__actor--enemy">
    <aside
      v-if="ally"
      :key="ally.actorId"
      class="combat-frontline"
      :aria-label="`Противник держит агро на ${ally.name}${threatPercent === null ? '' : `, угроза ${threatPercent}%`}`"
    >
      <span class="combat-frontline__marker" aria-hidden="true">
        <img v-if="allyArt()" :src="allyArt()!" alt="" class="combat-frontline__portrait" />
        <IconGenerator v-else :config="{ id: `frontline-${ally.actorId}`, glyph: 'sword', category: 'utility' }" />
      </span>
      <span class="combat-frontline__label">ЦЕЛЬ</span>
      <strong>{{ ally.name }}</strong>
      <span v-if="threatPercent !== null" class="combat-frontline__threat">{{ threatPercent }}%</span>
    </aside>
  </Teleport>

  <aside
    v-else-if="ally"
    :key="`fallback-${ally.actorId}`"
    class="combat-frontline combat-frontline--fallback"
    :aria-label="`Противник держит агро на ${ally.name}${threatPercent === null ? '' : `, угроза ${threatPercent}%`}`"
  >
    <span class="combat-frontline__marker" aria-hidden="true">
      <img v-if="allyArt()" :src="allyArt()!" alt="" class="combat-frontline__portrait" />
      <IconGenerator v-else :config="{ id: `frontline-${ally.actorId}`, glyph: 'sword', category: 'utility' }" />
    </span>
    <span class="combat-frontline__label">ЦЕЛЬ</span>
    <strong>{{ ally.name }}</strong>
    <span v-if="threatPercent !== null" class="combat-frontline__threat">{{ threatPercent }}%</span>
  </aside>
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

.combat-frontline--fallback {
  position: absolute;
  top: 7px;
  left: 50%;
  z-index: 5;
  max-width: 46%;
  margin: 0;
  background: rgb(7 11 18 / 92%);
  transform: translateX(-50%);
}

.combat-frontline__marker {
  display: grid;
  width: 20px;
  height: 20px;
  flex: 0 0 20px;
  place-items: center;
  overflow: hidden;
  border: 1px solid rgb(205 177 113 / 32%);
  border-radius: 50%;
  background: rgb(8 12 20 / 85%);
  color: var(--ui-color-gold);
}

.combat-frontline__portrait {
  width: 100%;
  height: 100%;
  object-fit: cover;
  object-position: top center;
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
</style>