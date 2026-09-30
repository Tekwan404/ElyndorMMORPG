<script setup lang="ts">
import { computed, onUnmounted, shallowRef } from 'vue'

import type { CombatAbility, CombatCastSnapshot, CombatEvent, CombatSnapshot } from '@/api/contracts'
import { gameArt } from '@/assets/gameArt'
import { isAuraAbility } from '@/game/combat/combatAbilityGroups'
import { orderCombatAbilities } from '@/game/combat/combatHotbarSettings'
import { projectBattleEvents } from '@/game/combat/battleEventPresentation'
import CharacterFigure from '@/game/combat/components/CharacterFigure.vue'
import CombatLog from '@/game/combat/components/CombatLog.vue'
import CombatNumbers from '@/game/combat/components/CombatNumbers.vue'
import SkillPanel from '@/game/combat/components/SkillPanel.vue'
import CombatEffectStrip from '@/game/combat/CombatEffectStrip.vue'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton } from '@/ui/components'

const props = defineProps<{
  battle: CombatSnapshot
  events: readonly CombatEvent[]
  active: boolean
  resultLabel: string | null
}>()

const emit = defineEmits<{
  useAbility: [abilityId: string, targetActorId: string]
  surrender: []
  dismiss: []
}>()

const session = useGameSessionStore()
const now = shallowRef(Date.now())
const timer = window.setInterval(() => {
  now.value = Date.now()
}, 100)

const localActor = computed(() => props.battle.player)
const enemyActor = computed(() => props.battle.enemy)
const activeAbilities = computed(() => {
  const abilities = localActor.value.abilities.filter((ability) => !isAuraAbility(ability.id))
  return orderCombatAbilities(session.snapshot?.character?.id ?? '', abilities)
})
const actorNames = computed(
  () => new Map([
    [localActor.value.actorId, localActor.value.name],
    [enemyActor.value.actorId, enemyActor.value.name],
  ]),
)
const abilityNames = computed(() => {
  const entries = [...localActor.value.abilities, ...enemyActor.value.abilities]
    .map((ability) => [ability.id, ability.displayName] as const)
  return new Map(entries)
})
const eventProjection = computed(() =>
  projectBattleEvents(props.events, {
    actorNames: actorNames.value,
    abilityNames: abilityNames.value,
    enemyActorIds: new Set([enemyActor.value.actorId]),
    localActorId: localActor.value.actorId,
  }),
)
const playerCast = computed(() => localActor.value.activeCast ?? null)
const enemyCast = computed(() => enemyActor.value.activeCast ?? null)

function ratio(value: number, max: number): number {
  return max > 0 ? Math.max(0, Math.min(100, (value / max) * 100)) : 0
}

function resourceLabel(resourceType: string): string {
  if (resourceType === 'MANA') return 'Мана'
  if (resourceType === 'FOCUS') return 'Фокус'
  return 'Ярость'
}

function abilityName(abilityId: string): string {
  const ability = [...localActor.value.abilities, ...enemyActor.value.abilities]
    .find((candidate) => candidate.id === abilityId)
  if (ability) return ability.displayName
  if (abilityId === 'AUTO_ATTACK') return 'Автоатака'
  return 'Способность'
}

function castRemaining(cast: CombatCastSnapshot | null): number {
  return cast ? Math.max(0, (Date.parse(cast.resolvesAtUtc) - now.value) / 1_000) : 0
}

function castProgress(cast: CombatCastSnapshot | null): number {
  if (!cast) return 0
  const start = Date.parse(cast.startedAtUtc)
  const duration = Math.max(1, Date.parse(cast.resolvesAtUtc) - start)
  return Math.max(0, Math.min(100, ((now.value - start) / duration) * 100))
}

function abilityTargetId(ability: CombatAbility): string {
  switch (ability.targetType) {
    case 'Self':
    case 'SingleAlly':
    case 'Owner':
    case 'SelfAndPartyMembersInCombat':
      return localActor.value.actorId
    default:
      return enemyActor.value.actorId
  }
}

function useAbility(ability: CombatAbility): void {
  emit('useAbility', ability.id, abilityTargetId(ability))
}

onUnmounted(() => window.clearInterval(timer))
</script>

<template>
  <section class="arena-battle-screen" data-arena-battle-screen>
    <header class="arena-battle-screen__header">
      <section class="arena-fighter-card arena-fighter-card--player" aria-label="Состояние героя">
        <div class="arena-fighter-card__identity">
          <strong>{{ localActor.name }}</strong>
          <small>Ур. {{ localActor.level ?? 1 }}</small>
        </div>
        <label>Здоровье <b>{{ Math.ceil(localActor.hp) }} / {{ Math.ceil(localActor.maxHp) }}</b></label>
        <span class="arena-fighter-card__bar arena-fighter-card__bar--health">
          <i :style="{ width: `${ratio(localActor.hp, localActor.maxHp)}%` }" />
        </span>
        <label>{{ resourceLabel(localActor.resourceType) }} <b>{{ Math.ceil(localActor.resource) }} / {{ Math.ceil(localActor.maxResource) }}</b></label>
        <span class="arena-fighter-card__bar" :data-resource="localActor.resourceType">
          <i :style="{ width: `${ratio(localActor.resource, localActor.maxResource)}%` }" />
        </span>
      </section>

      <div class="arena-battle-screen__versus" aria-hidden="true">
        <small>АРЕНА</small>
        <strong>VS</strong>
      </div>

      <section class="arena-fighter-card arena-fighter-card--enemy" aria-label="Состояние соперника">
        <div class="arena-fighter-card__identity">
          <strong>{{ enemyActor.name }}</strong>
          <small>Ур. {{ enemyActor.level ?? 1 }}</small>
        </div>
        <label>Здоровье <b>{{ Math.ceil(enemyActor.hp) }} / {{ Math.ceil(enemyActor.maxHp) }}</b></label>
        <span class="arena-fighter-card__bar arena-fighter-card__bar--health">
          <i :style="{ width: `${ratio(enemyActor.hp, enemyActor.maxHp)}%` }" />
        </span>
        <label>{{ resourceLabel(enemyActor.resourceType) }} <b>{{ Math.ceil(enemyActor.resource) }} / {{ Math.ceil(enemyActor.maxResource) }}</b></label>
        <span class="arena-fighter-card__bar" :data-resource="enemyActor.resourceType">
          <i :style="{ width: `${ratio(enemyActor.resource, enemyActor.maxResource)}%` }" />
        </span>
      </section>
    </header>

    <div class="arena-battle-screen__field-wrap">
      <section
        class="arena-battlefield"
        :style="{ backgroundImage: `linear-gradient(180deg, rgb(5 7 13 / 8%), rgb(4 6 10 / 58%)), url(${gameArt.world.combatWhispering})` }"
        aria-label="Поле боя арены"
        data-arena-battlefield
      >
        <div class="arena-battlefield__vignette" aria-hidden="true" />

        <div class="arena-battlefield__fighter arena-battlefield__fighter--player">
          <CharacterFigure
            :actor="localActor"
            :selected="false"
            :aggro="false"
            :local="true"
            :disabled="!active"
            :frontline="true"
          />
          <CombatNumbers :actor-id="localActor.actorId" :entries="eventProjection.numbers" />
          <CombatEffectStrip
            v-if="localActor.effects.length"
            class="arena-battlefield__effects"
            :effects="localActor.effects"
            :now="now"
            side="player"
          />
        </div>

        <div class="arena-battlefield__center" aria-hidden="true">
          <span>1 × 1</span>
        </div>

        <div class="arena-battlefield__fighter arena-battlefield__fighter--enemy">
          <CharacterFigure
            :actor="enemyActor"
            :selected="true"
            :aggro="false"
            :local="false"
            :disabled="true"
            :frontline="true"
          />
          <CombatNumbers :actor-id="enemyActor.actorId" :entries="eventProjection.numbers" />
          <CombatEffectStrip
            v-if="enemyActor.effects.length"
            class="arena-battlefield__effects"
            :effects="enemyActor.effects"
            :now="now"
            side="enemy"
          />
        </div>

        <div v-if="playerCast" class="arena-cast arena-cast--player" data-arena-player-cast>
          <div><strong>{{ abilityName(playerCast.abilityId) }}</strong><small>{{ castRemaining(playerCast).toFixed(1) }}с</small></div>
          <i><span :style="{ width: `${castProgress(playerCast)}%` }" /></i>
        </div>

        <div v-if="enemyCast" class="arena-cast arena-cast--enemy" data-arena-enemy-cast>
          <div><strong>{{ abilityName(enemyCast.abilityId) }}</strong><small>{{ castRemaining(enemyCast).toFixed(1) }}с</small></div>
          <i><span :style="{ width: `${castProgress(enemyCast)}%` }" /></i>
        </div>
      </section>
    </div>

    <section v-if="active" class="arena-battle-screen__actions">
      <SkillPanel
        :abilities="activeAbilities"
        :cooldowns="localActor.cooldowns"
        :resource="localActor.resource"
        :queued-ability-ids="[]"
        :now="now"
        :disabled="false"
        @use="useAbility"
      />
      <div class="arena-battle-screen__controls">
        <span :data-enabled="localActor.autoAttackEnabled">Автоатака: {{ localActor.autoAttackEnabled ? 'включена' : 'выключена' }}</span>
        <UIButton variant="danger" @click="emit('surrender')">Сдаться</UIButton>
      </div>
    </section>

    <section v-else class="arena-battle-screen__result" :data-result="resultLabel">
      <small>БОЙ ЗАВЕРШЁН</small>
      <strong>{{ resultLabel ?? 'Результат матча' }}</strong>
      <UIButton @click="emit('dismiss')">К арене</UIButton>
    </section>

    <div class="arena-battle-screen__log">
      <CombatLog :entries="eventProjection.logEntries" />
    </div>
  </section>
</template>

<style scoped>
.arena-battle-screen {
  --arena-gap: clamp(0.28rem, 1vw, 0.46rem);
  position: fixed;
  z-index: 220;
  inset: 0;
  display: grid;
  width: min(100%, 62rem);
  height: 100svh;
  min-height: 40rem;
  grid-template-rows: auto minmax(0, 1fr) auto auto;
  gap: var(--arena-gap);
  margin-inline: auto;
  padding: max(0.35rem, env(safe-area-inset-top)) max(0.4rem, env(safe-area-inset-right)) max(0.35rem, env(safe-area-inset-bottom)) max(0.4rem, env(safe-area-inset-left));
  overflow: hidden;
  background: radial-gradient(circle at 50% 18%, rgb(116 45 65 / 16%), transparent 25rem), #05070c;
  color: #eee6da;
}
.arena-battle-screen__header {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto minmax(0, 1fr);
  gap: 0.4rem;
  align-items: stretch;
}
.arena-fighter-card {
  display: grid;
  align-content: center;
  gap: 2px;
  min-width: 0;
  padding: 0.48rem 0.58rem;
  border: 1px solid rgb(178 151 89 / 40%);
  border-radius: 9px;
  background: linear-gradient(135deg, rgb(14 17 25 / 94%), rgb(6 8 13 / 90%));
  box-shadow: inset 0 0 18px rgb(0 0 0 / 25%);
}
.arena-fighter-card--enemy { border-color: rgb(184 71 91 / 50%); }
.arena-fighter-card__identity {
  display: flex;
  min-width: 0;
  align-items: baseline;
  justify-content: space-between;
  gap: 0.35rem;
}
.arena-fighter-card__identity strong {
  overflow: hidden;
  font-family: var(--ui-font-display);
  font-size: clamp(0.72rem, 3.2vw, 0.95rem);
  text-overflow: ellipsis;
  white-space: nowrap;
}
.arena-fighter-card__identity small,
.arena-fighter-card label {
  color: #afa89e;
  font-size: clamp(0.46rem, 2vw, 0.58rem);
}
.arena-fighter-card label { display: flex; justify-content: space-between; }
.arena-fighter-card label b { color: #e9e2d6; font-weight: 700; }
.arena-fighter-card__bar {
  display: block;
  height: 5px;
  overflow: hidden;
  border-radius: 999px;
  background: #090b10;
  box-shadow: inset 0 1px 2px #000;
}
.arena-fighter-card__bar i {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, #9f6729, #dda845);
}
.arena-fighter-card__bar--health i { background: linear-gradient(90deg, #93344b, #dc6579); }
.arena-fighter-card__bar[data-resource='MANA'] i { background: linear-gradient(90deg, #3c57a9, #719bf0); }
.arena-fighter-card__bar[data-resource='FOCUS'] i { background: linear-gradient(90deg, #8b6b2e, #e0bd5f); }
.arena-battle-screen__versus {
  display: grid;
  min-width: 2.7rem;
  place-content: center;
  text-align: center;
}
.arena-battle-screen__versus small { color: #b69655; font-size: 0.42rem; letter-spacing: 0.14em; }
.arena-battle-screen__versus strong { color: #e6c474; font-family: var(--ui-font-display); font-size: 1rem; }
.arena-battle-screen__field-wrap { min-height: 0; max-height: 61.8svh; }
.arena-battlefield {
  position: relative;
  isolation: isolate;
  width: 100%;
  height: 100%;
  min-height: 0;
  overflow: hidden;
  border: 1px solid rgb(183 154 91 / 42%);
  border-radius: 12px;
  background-color: #090c13;
  background-position: center;
  background-size: cover;
  box-shadow: inset 0 0 38px rgb(0 0 0 / 48%), 0 8px 24px rgb(0 0 0 / 28%);
}
.arena-battlefield__vignette {
  position: absolute;
  z-index: 0;
  inset: 0;
  background: linear-gradient(90deg, rgb(14 17 25 / 20%), transparent 42% 58%, rgb(70 18 30 / 20%)), linear-gradient(0deg, rgb(2 4 7 / 68%), transparent 38%);
  pointer-events: none;
}
.arena-battlefield__fighter {
  position: absolute;
  z-index: 10;
  bottom: 3%;
  width: 43%;
  height: 88%;
}
.arena-battlefield__fighter--player { left: 2%; }
.arena-battlefield__fighter--enemy { right: 2%; }
.arena-battlefield__fighter--enemy :deep(.character-figure__art img) { transform: scaleX(-1); }
.arena-battlefield__fighter--enemy :deep(.character-figure__aura) { background: radial-gradient(ellipse, rgb(170 53 73 / 28%), transparent 70%); }
.arena-battlefield__fighter--enemy :deep(.character-figure__caption) { border-color: rgb(197 70 91 / 36%); }
.arena-battlefield__effects {
  position: absolute;
  z-index: 30;
  top: 0.5rem;
  left: 50%;
  width: min(11rem, 82%);
  transform: translateX(-50%);
}
.arena-battlefield__center {
  position: absolute;
  z-index: 8;
  top: 46%;
  left: 50%;
  transform: translate(-50%, -50%);
  color: rgb(231 202 130 / 42%);
  font-family: var(--ui-font-display);
  font-size: clamp(0.78rem, 4vw, 1.15rem);
  letter-spacing: 0.15em;
  text-shadow: 0 2px 12px #000;
}
.arena-cast {
  position: absolute;
  z-index: 50;
  bottom: 0.65rem;
  display: grid;
  width: min(10rem, 38%);
  gap: 0.2rem;
  padding: 0.3rem 0.45rem;
  border: 1px solid rgb(126 105 190 / 42%);
  border-radius: 7px;
  background: rgb(7 9 16 / 94%);
}
.arena-cast--player { left: 2%; }
.arena-cast--enemy { right: 2%; border-color: rgb(213 78 99 / 50%); }
.arena-cast div { display: flex; justify-content: space-between; gap: 0.35rem; }
.arena-cast strong { overflow: hidden; font-size: 0.52rem; text-overflow: ellipsis; white-space: nowrap; }
.arena-cast small { color: #aaa4a0; font-size: 0.46rem; }
.arena-cast > i { display: block; height: 4px; overflow: hidden; border-radius: 999px; background: #08090d; }
.arena-cast > i span { display: block; height: 100%; background: linear-gradient(90deg, #6553ba, #b497f0); }
.arena-cast--enemy > i span { background: linear-gradient(90deg, #9d3148, #eb7182); }
.arena-battle-screen__actions { display: grid; gap: 0.3rem; }
.arena-battle-screen__controls {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}
.arena-battle-screen__controls > span {
  color: #918b82;
  font-size: 0.5rem;
}
.arena-battle-screen__controls > span[data-enabled='true'] { color: #cdb36f; }
.arena-battle-screen__result {
  display: grid;
  justify-items: center;
  gap: 0.25rem;
  padding: 0.55rem;
  border: 1px solid rgb(182 153 87 / 35%);
  border-radius: 8px;
  background: #0c1018;
  text-align: center;
}
.arena-battle-screen__result small { color: #9b927e; font-size: 0.46rem; letter-spacing: 0.12em; }
.arena-battle-screen__result strong { color: #e9d49d; font-family: var(--ui-font-display); font-size: 1rem; }
.arena-battle-screen__log { max-height: 5.3rem; overflow: auto; }

@media (max-width: 480px) {
  .arena-battle-screen { min-height: 36rem; }
  .arena-battle-screen__header { grid-template-columns: minmax(0, 1fr) 2rem minmax(0, 1fr); }
  .arena-fighter-card { padding: 0.38rem 0.42rem; }
  .arena-battle-screen__versus { min-width: 2rem; }
  .arena-battlefield__fighter { width: 48%; height: 86%; }
  .arena-battlefield__fighter--player { left: -1%; }
  .arena-battlefield__fighter--enemy { right: -1%; }
  .arena-battle-screen__log { display: none; }
}

@media (max-height: 740px) {
  .arena-battle-screen__field-wrap { max-height: 54svh; }
  .arena-battle-screen__log { display: none; }
}
</style>
