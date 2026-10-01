<script setup lang="ts">
import { computed, onUnmounted, shallowRef } from 'vue'

import type { CombatAbility, CombatCastSnapshot, CombatEvent, CombatSnapshot } from '@/api/contracts'
import { gameArt } from '@/assets/gameArt'
import { isAuraAbility } from '@/game/combat/combatAbilityGroups'
import { orderCombatAbilities } from '@/game/combat/combatHotbarSettings'
import { projectBattleEvents } from '@/game/combat/battleEventPresentation'
import BattleControls from '@/game/combat/components/BattleControls.vue'
import BattleHeader from '@/game/combat/components/BattleHeader.vue'
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
  pending?: boolean
  rating?: number | null
  ratingDelta?: number | null
  honorDelta?: number | null
  errorMessage?: string | null
  autoAttackPending?: boolean
}>()

const emit = defineEmits<{
  useAbility: [abilityId: string, targetActorId: string]
  surrender: []
  dismiss: []
  nextOpponent: []
  toggleAutoAttack: []
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
  <Teleport to="body">
    <div class="arena-battle-overlay">
      <p v-if="errorMessage" class="arena-battle-error" role="alert">{{ errorMessage }}</p>
      <section class="arena-battle-screen" data-arena-battle-screen data-battle-screen>
        <BattleHeader
          :local-actor="localActor"
          :enemy="enemyActor"
          :allies="[]"
          :selected-friendly-actor-id="null"
          :aggro-actor-ids="[]"
          :disabled="!active"
        />

        <div class="arena-battle-screen__field-wrap">
          <section
            class="arena-battlefield"
            :style="{ backgroundImage: `linear-gradient(180deg, rgb(5 7 13 / 8%), rgb(4 6 10 / 58%)), url(${gameArt.world.combatWhispering})` }"
            aria-label="Поле боя арены"
            data-arena-battlefield
          >
            <div class="arena-battlefield__vignette" aria-hidden="true" />
            <div class="arena-battlefield__duel" aria-hidden="true"><span>Арена</span><b>⚔</b><span>1 на 1</span></div>

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

            <div class="arena-battlefield__fighter arena-battlefield__fighter--enemy">
              <CharacterFigure
                :actor="enemyActor"
                :selected="false"
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
          <BattleControls
            :auto-attack-enabled="localActor.autoAttackEnabled"
            :auto-attack-disabled="Boolean(autoAttackPending)"
            :lifecycle-disabled="Boolean(pending)"
            :flee-disabled="Boolean(pending)"
            :training="false"
            :show-auto-attack="true"
            exit-label="Сдаться"
            exit-description="Завершить матч поражением"
            exit-icon="⚑"
            @flee="emit('surrender')"
            @toggle-auto-attack="emit('toggleAutoAttack')"
          />
        </section>

        <section v-else class="arena-battle-screen__result" :data-result="resultLabel">
          <small>Бой завершён</small>
          <strong>{{ resultLabel ?? 'Результат матча' }}</strong>
          <div v-if="rating != null || ratingDelta != null || honorDelta != null" class="arena-battle-screen__rewards">
            <span v-if="rating != null"><small>Рейтинг</small><b>{{ rating }}</b><em v-if="ratingDelta">{{ ratingDelta > 0 ? '+' : '' }}{{ ratingDelta }}</em></span>
            <span v-if="honorDelta != null"><small>Честь</small><b>{{ honorDelta > 0 ? '+' : '' }}{{ honorDelta }}</b></span>
          </div>
          <div class="arena-battle-screen__result-actions">
            <UIButton :disabled="Boolean(pending)" @click="emit('nextOpponent')">Следующий соперник</UIButton>
            <UIButton :disabled="Boolean(pending)" variant="ghost" @click="emit('dismiss')">К арене</UIButton>
          </div>
        </section>

        <div class="arena-battle-screen__log">
          <CombatLog :entries="eventProjection.logEntries" />
        </div>
      </section>
    </div>
  </Teleport>
</template>

<style scoped>
.arena-battle-error {
  position: fixed;
  z-index: 1100;
  bottom: max(1rem, env(safe-area-inset-bottom));
  left: 50%;
  width: min(90vw, 28rem);
  transform: translateX(-50%);
  margin: 0;
  padding: 0.75rem;
  border: 1px solid #a84a60;
  border-radius: 8px;
  background: #35141c;
  color: #ffe4e7;
}
.arena-battle-overlay {
  position: fixed;
  z-index: 1000;
  inset: 0;
  display: flex;
  justify-content: center;
  overflow: hidden;
  background: #05070c;
}
.arena-battle-screen {
  --arena-gap: clamp(0.28rem, 1vw, 0.46rem);
  display: grid;
  width: min(100%, var(--ui-content-width));
  height: var(--tg-viewport-stable-height, 100dvh);
  min-height: 0;
  grid-template-rows: auto minmax(0, 1fr) auto auto;
  gap: var(--arena-gap);
  padding: max(0.35rem, env(safe-area-inset-top)) max(0.4rem, env(safe-area-inset-right)) max(0.35rem, env(safe-area-inset-bottom)) max(0.4rem, env(safe-area-inset-left));
  overflow: hidden;
  border-inline: 1px solid var(--ui-color-frame);
  background: radial-gradient(circle at 50% 18%, rgb(116 45 65 / 16%), transparent 25rem), #05070c;
  color: #eee6da;
}
.arena-battle-screen__field-wrap {
  position: relative;
  min-height: 0;
}
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
.arena-cast {
  position: absolute;
  z-index: 50;
  top: 2.8rem;
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
.arena-cast strong { overflow: hidden; font-size: 0.7rem; text-overflow: ellipsis; white-space: nowrap; }
.arena-cast small { color: #d1c7b5; font-size: 0.7rem; font-variant-numeric: tabular-nums; }
.arena-cast > i { display: block; height: 4px; overflow: hidden; border-radius: 999px; background: #08090d; }
.arena-cast > i span { display: block; height: 100%; background: linear-gradient(90deg, #6553ba, #b497f0); }
.arena-cast--enemy > i span { background: linear-gradient(90deg, #9d3148, #eb7182); }
.arena-battle-screen__actions {
  display: grid;
  gap: 0.3rem;
}
.arena-battle-screen__result {
  display: grid;
  justify-items: center;
  gap: 0.35rem;
  padding: 0.55rem;
  border: 1px solid rgb(182 153 87 / 35%);
  border-radius: 8px;
  background: #0c1018;
  text-align: center;
}
.arena-battle-screen__result > small { color: #9b927e; font-size: 0.46rem; letter-spacing: 0.12em; }
.arena-battle-screen__result > strong { color: #e9d49d; font-family: var(--ui-font-display); font-size: 1rem; }
.arena-battle-screen__rewards {
  display: flex;
  justify-content: center;
  gap: 1rem;
}
.arena-battle-screen__rewards span { display: flex; align-items: baseline; gap: 0.25rem; }
.arena-battle-screen__rewards small { color: #8f887d; font-size: 0.46rem; }
.arena-battle-screen__rewards b { color: #ead089; font-size: 0.72rem; }
.arena-battle-screen__rewards em { color: #80c992; font-size: 0.55rem; font-style: normal; }
.arena-battle-screen__result-actions {
  display: grid;
  width: min(100%, 20rem);
  grid-template-columns: 1.35fr 1fr;
  gap: 0.35rem;
}
.arena-battle-screen__log {
  min-height: 0;
}
.arena-battlefield__duel {
  position: absolute;
  inset: 0.5rem 0 auto;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.65rem;
  color: #e8d4a3;
  font-family: var(--ui-font-display);
  font-size: 0.75rem;
  text-shadow: 0 2px 5px #000;
  pointer-events: none;
}
.arena-battlefield__duel b { font-size: 1.25rem; color: #d5b86f; }
.arena-battlefield__fighter :deep(.character-figure__caption small) { font-size: 0.65rem; }
.arena-battlefield__fighter :deep(.character-figure__caption strong) { font-size: 0.8rem; }
.arena-battle-screen :deep(.battle-header__status label),
.arena-battle-screen :deep(.battle-header__identity small) { font-size: 0.7rem; }
.arena-battle-screen :deep(.battle-header__bar) { height: 6px; }
.arena-battlefield__effects { top: 0; }
.arena-battle-screen__result > small,
.arena-battle-screen__rewards small,
.arena-battle-screen__rewards em { font-size: 0.7rem; }
.arena-battle-screen__result > strong { font-size: 1.4rem; }
.arena-battle-screen__rewards b { font-size: 0.95rem; }
.arena-battle-screen__result { background: linear-gradient(135deg, #292116, #0c1018 65%); }
.arena-battle-screen__result-actions :deep(button) { min-height: 44px; }

@media (max-width: 480px) {
  .arena-battlefield__fighter { width: 48%; height: 86%; }
  .arena-battlefield__fighter--player { left: -1%; }
  .arena-battlefield__fighter--enemy { right: -1%; }
  .arena-battle-screen__result-actions { width: 100%; }
}

@media (max-height: 680px) {
  .arena-battle-screen { min-height: 0; }
  .arena-battlefield__fighter { height: 84%; }
}
</style>
