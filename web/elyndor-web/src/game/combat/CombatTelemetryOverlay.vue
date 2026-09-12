<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'

import type { CombatActorSnapshot, CombatEvent } from '@/api/contracts'
import { useCombatSessionStore, type CombatThreatEntry } from '@/stores/combatSession'

type CombatThreatEntryWithTarget = CombatThreatEntry & {
  selectedTargetActorId?: string | null
}

const combat = useCombatSessionStore()
const threat = computed(() => combat.threat)
const maximumThreat = computed(() => Math.max(0, ...(threat.value?.entries.map(entry => entry.threat) ?? [0])))
const isCombatActive = computed(() => combat.snapshot?.status === 'Active')
const isTelemetryVisible = computed(() => combat.connectionState !== 'disconnected' || isCombatActive.value)
const currentThreatTarget = computed(() =>
  threat.value?.entries.find(entry => entry.actorId === threat.value?.currentTargetActorId) ?? null,
)
const playerTarget = computed(() => {
  const targetActorId = combat.snapshot?.selectedTargetActorId
  if (!targetActorId) return null
  return combat.enemies.find(enemy => enemy.actorId === targetActorId) ?? null
})
const partyMembers = computed(() =>
  (combat.snapshot?.players ?? [])
    .filter(player => player.actorId !== combat.snapshot?.player.actorId)
    .slice(0, 4),
)
const bossCast = computed(() => combat.snapshot?.enemy.activeCast ?? null)
const bossCastTargetName = computed(() => currentThreatTarget.value?.name ?? 'цель определяется')
const connectionLabel = computed(() => {
  switch (combat.connectionState) {
    case 'connected': return 'В СЕТИ'
    case 'connecting': return 'ПОДКЛЮЧЕНИЕ'
    case 'reconnecting': return 'ПЕРЕПОДКЛ.'
    case 'syncing': return 'СИНХРОНИЗАЦИЯ'
    default: return 'НЕТ СВЯЗИ'
  }
})
const aggroAlert = ref<string | null>(null)
const compactFeed = computed(() =>
  combat.events
    .filter(event => [
      'DamageDealt',
      'HealingApplied',
      'TauntApplied',
      'ActorDied',
      'EnemyKilled',
      'TargetChanged',
      'CombatEnded',
    ].includes(event.type))
    .slice(-3)
    .reverse(),
)
let telemetryTimer: number | null = null
let aggroAlertTimer: number | null = null

function threatPercent(entry: CombatThreatEntry): number {
  if (maximumThreat.value <= 0) return 0
  return Math.min(100, Math.max(0, (entry.threat / maximumThreat.value) * 100))
}

function healthPercent(actor: CombatActorSnapshot): number {
  if (actor.maxHp <= 0) return 0
  return Math.min(100, Math.max(0, (actor.hp / actor.maxHp) * 100))
}

function threatLabel(entry: CombatThreatEntry): string {
  if (threat.value?.forcedTargetActorId === entry.actorId) return 'ТАУНТ'
  if (entry.isCurrentTarget) return 'АГРО'
  if (combat.snapshot?.player.actorId === entry.actorId) return 'ВЫ'
  return ''
}

function memberTargetName(actorId: string): string {
  const entry = threat.value?.entries.find(candidate => candidate.actorId === actorId) as
    | CombatThreatEntryWithTarget
    | undefined
  const targetActorId = entry?.selectedTargetActorId
  if (!targetActorId) return 'цель не выбрана'
  return combat.enemies.find(enemy => enemy.actorId === targetActorId)?.name ?? 'неизвестная цель'
}

function actorName(actorId: string | null): string {
  if (!actorId) return '—'
  const snapshot = combat.snapshot
  if (!snapshot) return '—'
  if (snapshot.player.actorId === actorId) return 'Вы'
  const player = snapshot.players?.find(candidate => candidate.actorId === actorId)
  if (player) return player.name
  const enemy = combat.enemies.find(candidate => candidate.actorId === actorId)
  if (enemy) return enemy.name
  if (snapshot.companion?.actorId === actorId) return snapshot.companion.name
  return 'Участник'
}

function definitionName(definitionId: string | null): string {
  if (!definitionId) return ''
  const ability = [
    ...(combat.snapshot?.player.abilities ?? []),
    ...(combat.snapshot?.enemy.abilities ?? []),
  ].find(candidate => candidate.id === definitionId)
  return ability?.displayName ?? definitionId.split('_').join(' ')
}

function compactEventText(event: CombatEvent): string {
  const source = actorName(event.sourceActorId ?? event.actorId)
  const target = actorName(event.targetActorId)
  const definition = definitionName(event.definitionId)
  switch (event.type) {
    case 'DamageDealt': return `${source} → ${target} · ${Math.round(event.amount)}`
    case 'HealingApplied': return `${source} · +${Math.round(event.amount)} HP`
    case 'TauntApplied': return `${source} · провокация${definition ? ` · ${definition}` : ''}`
    case 'ActorDied': return `${actorName(event.actorId)} · пал`
    case 'EnemyKilled': return `${actorName(event.actorId)} · повержен`
    case 'TargetChanged': return `${source} → ${target} · новая цель`
    case 'CombatEnded': return event.definitionId === 'Victory' ? 'Победа' : 'Бой завершён'
    default: return definition || event.type
  }
}

watch(
  () => threat.value?.currentTargetActorId ?? null,
  (current, previous) => {
    if (!current || !previous || current === previous) return
    const targetName = threat.value?.entries.find(entry => entry.actorId === current)?.name ?? 'новая цель'
    const forced = threat.value?.forcedTargetActorId === current
    aggroAlert.value = forced ? `ТАУНТ → ${targetName}` : `АГРО СМЕНИЛОСЬ → ${targetName}`
    if (aggroAlertTimer !== null) window.clearTimeout(aggroAlertTimer)
    aggroAlertTimer = window.setTimeout(() => {
      aggroAlert.value = null
      aggroAlertTimer = null
    }, 2_400)
  },
)

onMounted(() => {
  void combat.refreshCombatTelemetry()
  telemetryTimer = window.setInterval(() => {
    void combat.refreshCombatTelemetry()
  }, 1_000)
})

onUnmounted(() => {
  if (telemetryTimer !== null) window.clearInterval(telemetryTimer)
  if (aggroAlertTimer !== null) window.clearTimeout(aggroAlertTimer)
})
</script>

<template>
  <aside v-if="isTelemetryVisible" class="combat-telemetry" aria-label="Сетевая задержка, цели и агро">
    <div class="combat-telemetry__ping" :data-state="combat.connectionState">
      <span>{{ connectionLabel }}</span>
      <strong>{{ combat.latencyMs ?? '—' }}</strong>
      <small>ms</small>
    </div>

    <div v-if="aggroAlert" class="combat-telemetry__aggro-alert" role="status" aria-live="assertive">
      {{ aggroAlert }}
    </div>

    <section
      v-if="isCombatActive"
      class="combat-telemetry__targets"
      aria-label="Текущие цели"
      data-combat-target-summary
    >
      <div>
        <span>ВЫ</span>
        <b>→ {{ playerTarget?.name ?? 'цель не выбрана' }}</b>
      </div>
      <div v-if="threat">
        <span>{{ threat.enemyName }}</span>
        <b>→ {{ currentThreatTarget?.name ?? 'цель определяется' }}</b>
      </div>
    </section>

    <section
      v-if="isCombatActive && partyMembers.length > 0"
      class="combat-telemetry__party"
      aria-label="Состояние группы"
      data-combat-party-frames
    >
      <header>
        <span>ГРУППА</span>
        <strong>{{ partyMembers.length + 1 }} / 5</strong>
      </header>
      <ol>
        <li v-for="member in partyMembers" :key="member.actorId">
          <div>
            <span>{{ member.name }}</span>
            <b>{{ Math.ceil(member.hp) }} / {{ Math.ceil(member.maxHp) }}</b>
          </div>
          <small>→ {{ memberTargetName(member.actorId) }}</small>
          <i aria-hidden="true"><span :style="{ width: `${healthPercent(member)}%` }" /></i>
        </li>
      </ol>
    </section>

    <section
      v-if="isCombatActive && bossCast"
      class="combat-telemetry__boss-cast"
      data-combat-boss-cast-target
    >
      <span>КАСТ БОССА</span>
      <strong>{{ definitionName(bossCast.abilityId) }}</strong>
      <small>→ {{ bossCastTargetName }}</small>
    </section>

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

    <section
      v-if="isCombatActive && compactFeed.length > 0"
      class="combat-telemetry__feed"
      aria-label="Последние события боя"
      data-combat-compact-feed
    >
      <header>БОЙ</header>
      <p v-for="event in compactFeed" :key="event.sequence">
        {{ compactEventText(event) }}
      </p>
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
  width: min(12rem, calc(100vw - 12px));
  gap: 4px;
  pointer-events: none;
  font-family: var(--ui-font-body, system-ui, sans-serif);
}

.combat-telemetry__ping {
  justify-self: end;
  display: inline-flex;
  align-items: baseline;
  gap: 4px;
  padding: 3px 6px;
  border: 1px solid rgb(255 255 255 / 8%);
  border-radius: 5px;
  background: rgb(3 6 11 / 80%);
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

.combat-telemetry__ping[data-state='reconnecting'],
.combat-telemetry__ping[data-state='syncing'] {
  border-color: rgb(222 160 73 / 28%);
}

.combat-telemetry__ping[data-state='reconnecting'] span,
.combat-telemetry__ping[data-state='syncing'] span {
  color: rgb(244 187 93);
}

.combat-telemetry__ping[data-state='disconnected'] strong,
.combat-telemetry__ping[data-state='connecting'] strong,
.combat-telemetry__ping[data-state='reconnecting'] strong,
.combat-telemetry__ping[data-state='syncing'] strong {
  color: rgb(121 128 139);
}

.combat-telemetry__aggro-alert {
  justify-self: stretch;
  padding: 6px 8px;
  border: 1px solid rgb(244 187 93 / 35%);
  border-radius: 7px;
  background: rgb(45 24 7 / 92%);
  color: rgb(255 214 126);
  box-shadow: 0 8px 20px rgb(0 0 0 / 30%);
  font-size: .52rem;
  font-weight: 950;
  letter-spacing: .04em;
  text-align: center;
}

.combat-telemetry__targets,
.combat-telemetry__party,
.combat-telemetry__boss-cast,
.combat-telemetry__threat,
.combat-telemetry__feed {
  display: grid;
  gap: 4px;
  padding: 6px;
  border: 1px solid rgb(255 255 255 / 9%);
  border-radius: 7px;
  background: rgb(3 6 11 / 86%);
  box-shadow: 0 8px 20px rgb(0 0 0 / 28%);
  backdrop-filter: blur(7px);
}

.combat-telemetry__targets > div {
  display: grid;
  grid-template-columns: minmax(0, .8fr) minmax(0, 1.2fr);
  align-items: center;
  gap: 5px;
  min-width: 0;
}

.combat-telemetry__targets span,
.combat-telemetry__targets b {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.combat-telemetry__targets span {
  color: rgb(189 197 210);
  font-size: .45rem;
  font-weight: 900;
}

.combat-telemetry__targets b {
  color: rgb(238 242 248);
  font-size: .48rem;
  font-weight: 800;
}

.combat-telemetry__targets > div:last-child b {
  color: rgb(239 124 137);
}

.combat-telemetry__party header,
.combat-telemetry__threat header {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 6px;
}

.combat-telemetry__party header span,
.combat-telemetry__threat header span,
.combat-telemetry__feed header,
.combat-telemetry__boss-cast > span {
  color: rgb(221 103 117);
  font-size: .45rem;
  font-weight: 900;
  letter-spacing: .08em;
}

.combat-telemetry__party header strong,
.combat-telemetry__threat header strong {
  overflow: hidden;
  color: rgb(189 197 210);
  font-size: .48rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.combat-telemetry__party ol,
.combat-telemetry__threat ol {
  display: grid;
  gap: 4px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.combat-telemetry__party li,
.combat-telemetry__threat li {
  display: grid;
  gap: 2px;
}

.combat-telemetry__party li > div {
  display: flex;
  justify-content: space-between;
  gap: 5px;
  color: rgb(176 186 201);
  font-size: .46rem;
}

.combat-telemetry__party li > div span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.combat-telemetry__party li > div b {
  color: rgb(213 221 232);
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.combat-telemetry__party li > small {
  overflow: hidden;
  color: rgb(142 154 173);
  font-size: .41rem;
  font-weight: 750;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.combat-telemetry__party li > i,
.combat-telemetry__threat li > i {
  display: block;
  height: 3px;
  overflow: hidden;
  border-radius: 999px;
  background: rgb(255 255 255 / 7%);
}

.combat-telemetry__party li > i > span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: rgb(105 185 121 / 78%);
}

.combat-telemetry__boss-cast {
  border-color: rgb(221 103 117 / 28%);
  background: rgb(28 6 10 / 90%);
}

.combat-telemetry__boss-cast strong {
  overflow: hidden;
  color: rgb(246 220 224);
  font-size: .56rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.combat-telemetry__boss-cast small {
  color: rgb(239 124 137);
  font-size: .47rem;
  font-weight: 850;
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

.combat-telemetry__feed p {
  overflow: hidden;
  margin: 0;
  color: rgb(176 186 201);
  font-size: .44rem;
  line-height: 1.3;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
