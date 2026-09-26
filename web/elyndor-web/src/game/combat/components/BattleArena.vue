<script setup lang="ts">
import { computed } from 'vue'

import type { CombatActorSnapshot } from '@/api/contracts'
import { monsterArtUrl } from '@/assets/monsterArt'
import type { CombatNumberPresentation } from '@/game/combat/battleEventPresentation'
import { buildBattleFormation } from '@/game/combat/composables/useBattleFormation'
import CharacterFigure from './CharacterFigure.vue'
import CombatNumbers from './CombatNumbers.vue'

const props = withDefaults(
  defineProps<{
    allies: readonly CombatActorSnapshot[]
    enemies: readonly CombatActorSnapshot[]
    localActorId: string
    selectedFriendlyActorId: string | null
    selectedEnemyActorId: string | null
    aggroActorIds: readonly string[]
    numbers: readonly CombatNumberPresentation[]
    companion?: CombatActorSnapshot | null
    battlefieldArt?: string | null
    disabled: boolean
    layoutWidthPx?: number
  }>(),
  {
    battlefieldArt: null,
    layoutWidthPx: 390,
    companion: null,
  },
)

const emit = defineEmits<{
  selectFriendly: [actorId: string]
  selectEnemy: [actorId: string]
}>()

const primaryEnemy = computed(
  () =>
    props.enemies.find((enemy) => enemy.actorId === props.selectedEnemyActorId) ??
    props.enemies[0] ??
    null,
)
const primaryAggroActorId = computed(
  () =>
    primaryEnemy.value?.currentAggroTargetActorId ?? props.aggroActorIds[0] ?? props.localActorId,
)
const formation = computed(() =>
  buildBattleFormation({
    actorIds: props.allies.map((actor) => actor.actorId),
    localActorId: props.localActorId,
    aggroActorId: primaryAggroActorId.value,
    selectedActorId: props.selectedFriendlyActorId,
    viewportWidthPx: props.layoutWidthPx,
    arenaWidthPx: props.layoutWidthPx,
    arenaHeightPx: Math.max(430, props.layoutWidthPx * 1.28),
  }),
)
const actorById = computed(() => new Map(props.allies.map((actor) => [actor.actorId, actor])))
const visibleSlots = computed(() => formation.value.slots.filter((slot) => slot.visible))
const backgroundStyle = computed(() =>
  props.battlefieldArt
    ? {
        backgroundImage: `linear-gradient(180deg, rgb(5 7 13 / 8%), rgb(4 6 10 / 54%)), url("${props.battlefieldArt}")`,
      }
    : undefined,
)
const enemyArt = computed(() =>
  primaryEnemy.value
    ? monsterArtUrl(primaryEnemy.value.artId, primaryEnemy.value.definitionId)
    : undefined,
)
const companionArt = computed(() =>
  props.companion ? monsterArtUrl(props.companion.artId, props.companion.definitionId) : undefined,
)
const enemyScale = computed(() => {
  if (primaryEnemy.value?.monsterRank === 'Boss') return 'boss'
  if (primaryEnemy.value?.monsterRank === 'Elite') return 'elite'
  return 'normal'
})
const enemyHealthRatio = computed(() =>
  primaryEnemy.value && primaryEnemy.value.maxHp > 0
    ? Math.max(0, Math.min(100, (primaryEnemy.value.hp / primaryEnemy.value.maxHp) * 100))
    : 0,
)

function slotStyle(slot: (typeof visibleSlots.value)[number]) {
  return {
    left: `${slot.bounds.x * 100}%`,
    top: `${slot.bounds.y * 100}%`,
    width: `${slot.bounds.width * 100}%`,
    height: `${slot.bounds.height * 100}%`,
    zIndex: slot.zIndex,
    '--actor-scale': slot.scale,
  }
}
</script>

<template>
  <section
    class="battle-arena"
    :style="backgroundStyle"
    :data-formation-mode="formation.mode"
    data-battle-arena
    data-combat-battlefield
    aria-label="Поле боя"
  >
    <div class="battle-arena__vignette" aria-hidden="true" />

    <nav v-if="enemies.length > 1" class="battle-arena__enemy-strip" aria-label="Противники">
      <button
        v-for="enemy in enemies"
        :key="enemy.actorId"
        type="button"
        :aria-pressed="enemy.actorId === selectedEnemyActorId"
        :data-target-actor-id="enemy.actorId"
        :disabled="disabled || enemy.hp <= 0"
        @click="emit('selectEnemy', enemy.actorId)"
      >
        <span>{{ enemy.name }}</span>
        <i
          ><b
            :style="{
              width: `${enemy.maxHp > 0 ? (enemy.hp / enemy.maxHp) * 100 : 0}%`,
            }"
        /></i>
      </button>
    </nav>

    <div class="battle-arena__allies" aria-label="Союзники на поле боя">
      <div
        v-for="slot in visibleSlots"
        :key="slot.actorId"
        class="battle-arena__ally-slot"
        :style="slotStyle(slot)"
        :data-slot-id="slot.slotId"
      >
        <CharacterFigure
          v-if="actorById.get(slot.actorId)"
          :actor="actorById.get(slot.actorId)!"
          :selected="selectedFriendlyActorId === slot.actorId"
          :aggro="aggroActorIds.includes(slot.actorId)"
          :local="localActorId === slot.actorId"
          :disabled="disabled"
          :frontline="slot.isFrontline"
          @select="emit('selectFriendly', $event)"
        />
        <CombatNumbers :actor-id="slot.actorId" :entries="numbers" />
      </div>
    </div>

    <button
      v-if="primaryEnemy"
      type="button"
      class="battle-arena__enemy enemy-figure"
      :data-enemy-scale="enemyScale"
      :aria-label="`${primaryEnemy.name}, ${Math.round(enemyHealthRatio)}% HP`"
      :disabled="disabled || primaryEnemy.hp <= 0"
      @click="emit('selectEnemy', primaryEnemy.actorId)"
    >
      <span class="battle-arena__enemy-art">
        <img
          v-if="enemyArt"
          :src="enemyArt"
          :alt="primaryEnemy.name"
          loading="eager"
          fetchpriority="high"
        />
        <b v-else>{{ primaryEnemy.name.slice(0, 1) }}</b>
      </span>
      <span class="battle-arena__enemy-caption">
        <strong>{{ primaryEnemy.name }}</strong>
        <i><b :style="{ width: `${enemyHealthRatio}%` }" /></i>
        <small>Ур. {{ primaryEnemy.level ?? 1 }}</small>
      </span>
      <CombatNumbers :actor-id="primaryEnemy.actorId" :entries="numbers" />
    </button>

    <aside
      v-if="companion"
      class="battle-arena__companion"
      :data-dead="companion.hp <= 0"
      data-combat-companion
    >
      <span
        ><img v-if="companionArt" :src="companionArt" alt="" /><b v-else>{{
          companion.name.slice(0, 1)
        }}</b></span
      >
      <div>
        <strong>{{ companion.name }}</strong
        ><i
          ><b
            :style="{
              width: `${companion.maxHp > 0 ? (companion.hp / companion.maxHp) * 100 : 0}%`,
            }"
        /></i>
      </div>
    </aside>

    <p v-if="formation.overflowActorIds.length" class="battle-arena__overflow">
      Ещё {{ formation.overflowActorIds.length }} в полоске группы
    </p>
  </section>
</template>

<style scoped>
.battle-arena {
  position: relative;
  isolation: isolate;
  min-height: clamp(27rem, 59svh, 39rem);
  overflow: hidden;
  border: 1px solid rgb(183 154 91 / 42%);
  border-radius: 12px;
  background-color: #090c13;
  background-position: center;
  background-size: cover;
  box-shadow:
    inset 0 0 38px rgb(0 0 0 / 48%),
    0 8px 24px rgb(0 0 0 / 28%);
}
.battle-arena__vignette {
  position: absolute;
  z-index: 0;
  inset: 0;
  background:
    linear-gradient(90deg, rgb(4 5 9 / 8%) 0 55%, rgb(2 4 8 / 18%) 70%, rgb(2 3 7 / 48%)),
    linear-gradient(0deg, rgb(2 4 7 / 64%), transparent 35%);
  pointer-events: none;
}
.battle-arena__allies {
  position: absolute;
  z-index: 2;
  inset: 0;
}
.battle-arena__ally-slot {
  position: absolute;
  transform: scale(var(--actor-scale));
  transform-origin: center bottom;
  transition:
    top 260ms ease-out,
    left 260ms ease-out,
    width 260ms ease-out,
    height 260ms ease-out,
    transform 260ms ease-out;
}
.battle-arena__enemy {
  position: absolute;
  z-index: 40;
  right: 1.5%;
  bottom: 4%;
  display: grid;
  width: 45%;
  height: 84%;
  min-width: 44px;
  min-height: 44px;
  grid-template-rows: minmax(0, 1fr) auto;
  padding: 0;
  border: 0;
  background: transparent;
  color: #f6eee2;
  font: inherit;
  cursor: pointer;
  touch-action: manipulation;
}
.battle-arena__enemy[data-enemy-scale='elite'] {
  width: 48%;
  height: 88%;
}
.battle-arena__enemy[data-enemy-scale='boss'] {
  width: 51%;
  height: 92%;
}
.battle-arena__enemy:focus-visible {
  outline: 2px solid #df7182;
  outline-offset: 3px;
  border-radius: 12px;
}
.battle-arena__enemy-art {
  display: grid;
  min-height: 0;
  place-items: end center;
}
.battle-arena__enemy-art img {
  display: block;
  width: 100%;
  height: 100%;
  object-fit: contain;
  object-position: center bottom;
  filter: drop-shadow(0 0.65rem 0.55rem rgb(0 0 0 / 85%));
}
.battle-arena__enemy-art > b {
  align-self: center;
  font-family: var(--ui-font-display);
  font-size: 4rem;
}
.battle-arena__enemy-caption {
  display: grid;
  gap: 2px;
  padding: 0.28rem 0.45rem;
  border: 1px solid rgb(207 79 99 / 42%);
  border-radius: 7px;
  background: rgb(7 8 13 / 88%);
  box-shadow: 0 0 14px rgb(157 34 52 / 16%);
}
.battle-arena__enemy-caption strong {
  overflow: hidden;
  font-size: clamp(0.58rem, 2.6vw, 0.78rem);
  text-overflow: ellipsis;
  white-space: nowrap;
}
.battle-arena__enemy-caption i,
.battle-arena__enemy-strip i {
  display: block;
  height: 4px;
  overflow: hidden;
  border-radius: 999px;
  background: #14080c;
}
.battle-arena__enemy-caption i b,
.battle-arena__enemy-strip i b {
  display: block;
  height: 100%;
  background: linear-gradient(90deg, #8f3044, #df6677);
}
.battle-arena__enemy-caption small {
  color: #b9afa3;
  font-size: 0.48rem;
}
.battle-arena__enemy-strip {
  position: absolute;
  z-index: 70;
  top: 0.45rem;
  right: 0.45rem;
  display: flex;
  max-width: 52%;
  gap: 0.3rem;
}
.battle-arena__enemy-strip button {
  display: grid;
  min-width: 4.5rem;
  min-height: 44px;
  gap: 0.15rem;
  padding: 0.3rem;
  border: 1px solid rgb(205 93 110 / 35%);
  border-radius: 7px;
  background: rgb(4 6 11 / 84%);
  color: #e8dfd2;
  font: inherit;
  font-size: 0.52rem;
}
.battle-arena__enemy-strip button[aria-pressed='true'] {
  border-color: #df7182;
  box-shadow: 0 0 10px rgb(210 69 91 / 25%);
}
.battle-arena__overflow {
  position: absolute;
  z-index: 80;
  bottom: 0.45rem;
  left: 0.5rem;
  margin: 0;
  padding: 0.2rem 0.38rem;
  border-radius: 999px;
  background: rgb(5 7 12 / 78%);
  color: #cabf9e;
  font-size: 0.5rem;
}
.battle-arena__companion {
  position: absolute;
  z-index: 65;
  bottom: 0.45rem;
  left: 0.45rem;
  display: grid;
  width: min(7.5rem, 30%);
  grid-template-columns: 2rem minmax(0, 1fr);
  align-items: center;
  gap: 0.3rem;
  padding: 0.25rem;
  border: 1px solid rgb(95 170 145 / 30%);
  border-radius: 7px;
  background: rgb(4 11 13 / 82%);
}
.battle-arena__companion[data-dead='true'] {
  filter: grayscale(0.8);
  opacity: 0.5;
}
.battle-arena__companion > span {
  display: grid;
  width: 2rem;
  height: 2rem;
  place-items: center;
  overflow: hidden;
  border-radius: 50%;
}
.battle-arena__companion img {
  width: 100%;
  height: 100%;
  object-fit: contain;
}
.battle-arena__companion div {
  display: grid;
  min-width: 0;
  gap: 2px;
}
.battle-arena__companion strong {
  overflow: hidden;
  font-size: 0.5rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.battle-arena__companion i {
  display: block;
  height: 3px;
  overflow: hidden;
  border-radius: 999px;
  background: #07100d;
}
.battle-arena__companion i b {
  display: block;
  height: 100%;
  background: #6bc59f;
}

@media (max-width: 380px), (max-height: 740px) {
  .battle-arena {
    min-height: 25.5rem;
  }
}
@media (max-width: 480px) {
  .battle-arena__enemy {
    width: 56%;
    height: 88%;
  }
  .battle-arena__enemy[data-enemy-scale='elite'] {
    width: 58%;
    height: 91%;
  }
  .battle-arena__enemy[data-enemy-scale='boss'] {
    width: 60%;
    height: 94%;
  }
}
@media (prefers-reduced-motion: reduce) {
  .battle-arena__ally-slot {
    transition-duration: 1ms;
  }
}
</style>
