<script setup lang="ts">
import { computed } from 'vue'

import type { CombatActorSnapshot } from '@/api/contracts'
import { combatActorVitalsLabel } from '@/game/combat/combatActorPresentation'
import { resolveCharacterArt } from '@/assets/characterArt'
import { useGameSessionStore } from '@/stores/gameSession'
import CombatActorStatus from './CombatActorStatus.vue'
import AggroIndicator from './AggroIndicator.vue'
import TargetSelection from './TargetSelection.vue'

const props = withDefaults(
  defineProps<{
    actor: CombatActorSnapshot
    selected: boolean
    aggro: boolean
    local: boolean
    disabled: boolean
    frontline: boolean
    canSelectDead?: boolean
    threatPercent?: number | null
    showStatus?: boolean
  }>(),
  {
    threatPercent: null,
    showStatus: true,
  },
)

const emit = defineEmits<{ select: [actorId: string] }>()
const session = useGameSessionStore()

const art = computed(() =>
  resolveCharacterArt(
    props.actor.definitionId,
    props.actor.genderId ?? (props.local ? session.snapshot?.character?.genderId : null) ?? 'MALE',
    'transparent',
    props.local
      ? (session.snapshot?.character?.activeSkinId ?? props.actor.skinId)
      : props.actor.skinId,
  ),
)
const urgentArt = computed(() => props.local || props.frontline)
const responsiveSizes = computed(() =>
  props.frontline
    ? '(max-width: 390px) 42vw, (max-width: 768px) 34vw, 280px'
    : '(max-width: 390px) 27vw, (max-width: 768px) 22vw, 190px',
)
const accessibleLabel = computed(() =>
  [
    combatActorVitalsLabel(props.actor),
    props.aggro ? 'под агро' : null,
    props.selected ? 'выбранная цель' : null,
    props.actor.hp <= 0 ? 'погиб' : null,
  ]
    .filter(Boolean)
    .join(', '),
)
</script>

<template>
  <article
    class="character-figure"
    :data-character-figure="actor.actorId"
    :data-friendly-battlefield-actor="actor.actorId"
    :data-selected="selected"
    :data-aggro="aggro"
    :data-frontline="frontline"
    :data-dead="actor.hp <= 0"
  >
    <button
      type="button"
      class="character-figure__button"
      :aria-label="accessibleLabel"
      :tabindex="showStatus ? 0 : -1"
      :aria-pressed="selected"
      :disabled="disabled || (actor.hp <= 0 && !canSelectDead)"
      @click="emit('select', actor.actorId)"
    >
      <span class="character-figure__aura" aria-hidden="true" />
      <span class="character-figure__art">
        <img
          v-if="art"
          :src="art"
          :alt="actor.name"
          loading="eager"
          :fetchpriority="urgentArt ? 'high' : 'low'"
          decoding="async"
          :sizes="responsiveSizes"
        />
        <b v-else class="character-figure__fallback">{{ actor.name.slice(0, 1).toUpperCase() }}</b>
        <TargetSelection v-if="selected" />
        <AggroIndicator v-if="aggro" :threat-percent="threatPercent" />
      </span>
      <span v-if="showStatus" class="character-figure__caption">
        <CombatActorStatus
          :actor="actor"
          :fallback-level="local ? session.snapshot?.character?.level : undefined"
        />
      </span>
      <span v-else class="character-figure__status-space" aria-hidden="true" />
    </button>
  </article>
</template>

<style scoped>
.character-figure {
  position: relative;
  width: 100%;
  height: 100%;
  min-width: 44px;
  min-height: 44px;
  transition:
    transform 240ms ease-out,
    filter 180ms ease-out;
}
.character-figure__button {
  position: relative;
  display: grid;
  width: 100%;
  height: 100%;
  min-width: 44px;
  min-height: 44px;
  grid-template-rows: minmax(0, 1fr) auto;
  padding: 0;
  border: 0;
  background: transparent;
  color: #f5eee1;
  font: inherit;
  cursor: pointer;
  touch-action: manipulation;
}
.character-figure__button:focus-visible {
  outline: 2px solid #f2d47d;
  outline-offset: 3px;
  border-radius: 12px;
}
.character-figure__button:disabled {
  cursor: default;
}
.character-figure__art {
  align-self: end;
  width: 100%;
  max-height: 100%;
  aspect-ratio: 2 / 3;
  position: relative;
  z-index: 2;
  display: grid;
  min-height: 0;
  place-items: end center;
}
.character-figure__art img {
  display: block;
  width: 100%;
  height: 100%;
  object-fit: contain;
  object-position: center bottom;
  filter: drop-shadow(0 0.55rem 0.45rem rgb(0 0 0 / 82%));
  pointer-events: none;
}
.character-figure__fallback {
  align-self: center;
  font-family: var(--ui-font-display);
  font-size: clamp(2rem, 10vw, 4rem);
}
.character-figure__aura {
  position: absolute;
  z-index: 1;
  right: 12%;
  bottom: 9%;
  left: 12%;
  height: 22%;
  border-radius: 50%;
  background: radial-gradient(ellipse, rgb(118 82 177 / 28%), transparent 70%);
  filter: blur(7px);
  pointer-events: none;
}
.character-figure__caption {
  --combat-status-bar-height: calc(14px / var(--actor-scale, 1));
  --combat-status-font-size: calc(9px / var(--actor-scale, 1));
  --combat-status-name-size: calc(10px / var(--actor-scale, 1));
  min-width: calc(80px / var(--actor-scale, 1));
  justify-self: start;
  width: 100%;
  position: relative;
  z-index: 6;
  display: grid;
  gap: 2px;
  margin: -0.1rem 0.15rem 0;
  padding: 0.28rem 0.36rem 0.24rem;
  border: 1px solid rgb(184 160 102 / 25%);
  border-radius: 7px;
  background: linear-gradient(180deg, rgb(8 10 16 / 72%), rgb(5 7 11 / 94%));
  box-shadow: 0 4px 12px rgb(0 0 0 / 35%);
}
.character-figure[data-aggro='true'] {
  filter: drop-shadow(0 0 12px rgb(218 68 82 / 28%));
}
.character-figure[data-dead='true'] {
  filter: grayscale(0.8) opacity(0.56);
}

@media (prefers-reduced-motion: reduce) {
  .character-figure {
    transition-duration: 1ms;
  }
}
.character-figure__status-space {
  height: 52px;
}
</style>
