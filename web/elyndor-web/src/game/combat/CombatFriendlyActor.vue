<script setup lang="ts">
import { computed } from 'vue'

import type { CombatActorSnapshot } from '@/api/contracts'
import { resolveCharacterArt } from '@/assets/characterArt'
import { useGameSessionStore } from '@/stores/gameSession'

const props = defineProps<{
  actor: CombatActorSnapshot
  selected: boolean
  aggro: boolean
  local: boolean
  disabled: boolean
}>()
const emit = defineEmits<{ select: [actorId: string] }>()
const session = useGameSessionStore()
const art = computed(() => resolveCharacterArt(props.actor.definitionId, props.actor.genderId ?? 'MALE', 'transparent',
  props.local ? session.snapshot?.character?.activeSkinId ?? props.actor.skinId : props.actor.skinId))
const healthRatio = computed(() => props.actor.maxHp > 0
  ? Math.max(0, Math.min(100, props.actor.hp / props.actor.maxHp * 100))
  : 0)
</script>

<template>
  <button
    type="button"
    class="combat-friendly-actor"
    :data-friendly-battlefield-actor="actor.actorId"
    :data-selected="selected"
    :data-aggro="aggro"
    :data-local="local"
    :aria-pressed="selected"
    :aria-label="`${actor.name}, ${Math.round(healthRatio)}% HP${aggro ? ', aggro' : ''}`"
    :disabled="disabled"
    @click="emit('select', actor.actorId)"
  >
    <span class="combat-friendly-actor__art">
      <img v-if="art" :src="art" alt="" />
      <b v-else>{{ actor.name.slice(0, 1).toUpperCase() }}</b>
    </span>
    <span class="combat-friendly-actor__status">
      <span v-if="aggro" class="combat-friendly-actor__marker">AGGRO</span>
      <span v-if="selected" class="combat-friendly-actor__marker combat-friendly-actor__marker--selected">ЦЕЛЬ</span>
    </span>
    <span class="combat-friendly-actor__name">{{ actor.name }}</span>
    <span class="combat-friendly-actor__bar" role="progressbar" :aria-valuenow="Math.round(healthRatio)" aria-valuemin="0" aria-valuemax="100" :aria-label="`HP ${actor.name}`"><i :style="{ width: `${healthRatio}%` }" /></span>
  </button>
</template>

<style scoped>
.combat-friendly-actor {
  display: flex;
  width: 100%;
  min-width: 0;
  min-height: 44px;
  flex-direction: column;
  align-items: center;
  justify-content: flex-end;
  gap: 2px;
  padding: 0 2px 4px;
  border: 1px solid transparent;
  border-radius: 8px;
  background: linear-gradient(0deg, rgb(5 8 13 / 78%), transparent 45%);
  color: #f4eee0;
  font: inherit;
  cursor: pointer;
  touch-action: manipulation;
}
.combat-friendly-actor[data-selected='true'] { border-color: #d4b76f; box-shadow: 0 0 12px rgb(212 183 111 / 28%); }
.combat-friendly-actor[data-aggro='true'] { background: linear-gradient(0deg, rgb(56 22 27 / 90%), transparent 60%); box-shadow: 0 0 14px rgb(225 86 86 / 24%); }
.combat-friendly-actor[data-selected='true'][data-aggro='true'] { border-color: #d4b76f; }
.combat-friendly-actor:disabled { cursor: default; opacity: .55; }
.combat-friendly-actor__art { display: grid; width: 100%; height: clamp(4.6rem, 23vw, 7rem); place-items: end center; }
.combat-friendly-actor__art img { width: 100%; height: 100%; object-fit: contain; object-position: center bottom; filter: drop-shadow(0 6px 8px rgb(0 0 0 / 75%)); pointer-events: none; }
.combat-friendly-actor__art b { font-family: var(--ui-font-display); font-size: 2rem; }
.combat-friendly-actor__status { display: flex; min-height: .7rem; justify-content: center; gap: 2px; }
.combat-friendly-actor__marker { color: #f4a8a8; font-size: .43rem; font-weight: 900; letter-spacing: .04em; }
.combat-friendly-actor__marker--selected { color: #e7ce89; }
.combat-friendly-actor__name { width: 100%; overflow: hidden; font-size: .6rem; font-weight: 700; text-align: center; text-overflow: ellipsis; white-space: nowrap; }
.combat-friendly-actor__bar { display: block; width: 85%; height: 4px; overflow: hidden; border-radius: 9px; background: rgb(0 0 0 / 70%); }
.combat-friendly-actor__bar i { display: block; height: 100%; background: #8bd5ae; }
</style>
