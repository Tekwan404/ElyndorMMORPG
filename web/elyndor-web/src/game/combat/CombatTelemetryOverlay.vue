<script setup lang="ts">
import { computed, onMounted, onUnmounted } from 'vue'

import { useCombatSessionStore, type CombatThreatEntry } from '@/stores/combatSession'

const combat = useCombatSessionStore()
const threat = computed(() => combat.threat)
const maximumThreat = computed(() => Math.max(0, ...(threat.value?.entries.map(entry => entry.threat) ?? [0])))
const isCombatActive = computed(() => combat.snapshot?.status === 'Active')
const isTelemetryVisible = computed(() => combat.connectionState !== 'disconnected' || isCombatActive.value)
let telemetryTimer: number | null = null

function threatPercent(entry: CombatThreatEntry): number {
  if (maximumThreat.value <= 0) return 0
  return Math.min(100, Math.max(0, (entry.threat / maximumThreat.value) * 100))
}

function threatLabel(entry: CombatThreatEntry): string {
  if (threat.value?.forcedTargetActorId === entry.actorId) return 'ТАУНТ'
  if (entry.isCurrentTarget) return 'АГРО'
  if (combat.snapshot?.player.actorId === entry.actorId) return 'ВЫ'
  return ''
}

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
  <aside v-if="isTelemetryVisible" class="combat-telemetry" aria-label="Сетевая задержка и агро">
    <div class="combat-telemetry__ping" :data-connected="combat.connectionState === 'connected'">
      <span>PING</span>
      <strong>{{ combat.latencyMs ?? '—' }}</strong>
      <small>ms</small>
    </div>

    <section
      v-if="isCombatActive && threat && threat.entries.length > 0"
      class="combat-telemetry__threat"
      data-combat-threat-meter
    >
      <header>
        <span>АГРО</span>
        <strong>{{ threat.enemyName }}</strong>
      </header>
      <ol>
        <li
          v-for="entry in threat.entries"
          :key="entry.actorId"
          :class="{
            'is-target': entry.isCurrentTarget,
            'is-forced': threat.forcedTargetActorId === entry.actorId,
            'is-self': combat.snapshot?.player.actorId === entry.actorId,
          }"
        >
          <div class="combat-telemetry__threat-line">
            <span>{{ entry.name }}</span>
            <b>{{ Math.round(entry.threat) }}</b>
            <em v-if="threatLabel(entry)">{{ threatLabel(entry) }}</em>
          </div>
          <i aria-hidden="true">
            <span :style="{ width: `${threatPercent(entry)}%` }" />
          </i>
        </li>
      </ol>
    </section>
  </aside>
</template>

<style scoped>
.combat-telemetry {
  position: fixed;
  top: calc(env(safe-area-inset-top, 0px) + 5px);
  right: 6px;
  z-index: 120;
  display: grid;
  width: min(11.25rem, calc(100vw - 12px));
  gap: 4px;
  pointer-events: none;
  font-family: var(--ui-font-body, system-ui, sans-serif);
}

.combat-telemetry__ping {
  justify-self: end;
  display: inline-flex;
  align-items: baseline;
  gap: 3px;
  padding: 2px 5px;
  border: 1px solid rgb(255 255 255 / 8%);
  border-radius: 5px;
  background: rgb(3 6 11 / 76%);
  color: rgb(151 160 176);
  box-shadow: 0 4px 12px rgb(0 0 0 / 18%);
  backdrop-filter: blur(5px);
}

.combat-telemetry__ping span,
.combat-telemetry__ping small {
  font-size: .42rem;
  font-weight: 800;
  letter-spacing: .05em;
}

.combat-telemetry__ping strong {
  color: rgb(213 221 232);
  font-size: .56rem;
  line-height: 1;
}

.combat-telemetry__ping[data-connected='false'] strong {
  color: rgb(121 128 139);
}

.combat-telemetry__threat {
  display: grid;
  gap: 4px;
  padding: 6px;
  border: 1px solid rgb(255 255 255 / 9%);
  border-radius: 7px;
  background: rgb(3 6 11 / 86%);
  box-shadow: 0 8px 20px rgb(0 0 0 / 28%);
  backdrop-filter: blur(7px);
}

.combat-telemetry__threat header {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 6px;
}

.combat-telemetry__threat header span {
  color: rgb(221 103 117);
  font-size: .45rem;
  font-weight: 900;
  letter-spacing: .08em;
}

.combat-telemetry__threat header strong {
  overflow: hidden;
  color: rgb(189 197 210);
  font-size: .48rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.combat-telemetry__threat ol {
  display: grid;
  gap: 4px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.combat-telemetry__threat li {
  display: grid;
  gap: 2px;
}

.combat-telemetry__threat-line {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto auto;
  align-items: center;
  gap: 4px;
  min-width: 0;
  color: rgb(169 178 191);
  font-size: .47rem;
}

.combat-telemetry__threat-line span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.combat-telemetry__threat-line b {
  color: rgb(203 211 222);
  font-variant-numeric: tabular-nums;
}

.combat-telemetry__threat-line em {
  color: rgb(239 124 137);
  font-size: .39rem;
  font-style: normal;
  font-weight: 900;
  letter-spacing: .04em;
}

.combat-telemetry__threat li.is-forced .combat-telemetry__threat-line em {
  color: rgb(244 187 93);
}

.combat-telemetry__threat li.is-target .combat-telemetry__threat-line span {
  color: rgb(242 246 252);
  font-weight: 800;
}

.combat-telemetry__threat li > i {
  display: block;
  height: 3px;
  overflow: hidden;
  border-radius: 999px;
  background: rgb(255 255 255 / 7%);
}

.combat-telemetry__threat li > i > span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: rgb(133 143 160 / 70%);
}

.combat-telemetry__threat li.is-target > i > span {
  background: rgb(213 82 101 / 82%);
}

.combat-telemetry__threat li.is-forced > i > span {
  background: rgb(222 160 73 / 88%);
}
</style>
