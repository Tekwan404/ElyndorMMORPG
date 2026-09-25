<script setup lang="ts">
import { computed } from 'vue'

import type { CombatNumberPresentation } from '@/game/combat/battleEventPresentation'

const props = defineProps<{
  actorId: string
  entries: readonly CombatNumberPresentation[]
}>()

const visibleEntries = computed(() => props.entries
  .filter((entry) => entry.targetActorId === props.actorId)
  .slice(-4))

function label(entry: CombatNumberPresentation): string {
  if (entry.kind === 'miss') return 'ПРОМАХ'
  if (entry.kind === 'heal') return `+${Math.round(entry.value ?? 0)}`
  return `-${Math.round(entry.value ?? 0)}`
}
</script>

<template>
  <span class="combat-numbers" :data-combat-numbers-for="actorId" aria-live="polite" aria-atomic="false">
    <TransitionGroup name="combat-number">
      <b
        v-for="entry in visibleEntries"
        :key="entry.key"
        class="combat-numbers__entry"
        :class="`combat-numbers__entry--${entry.kind}`"
      >{{ label(entry) }}</b>
    </TransitionGroup>
  </span>
</template>

<style scoped>
.combat-numbers { position: absolute; z-index: 20; top: 28%; left: 50%; display: grid; width: max-content; place-items: center; pointer-events: none; transform: translateX(-50%); }
.combat-numbers__entry { grid-area: 1 / 1; color: #ff8490; font-family: var(--ui-font-body); font-size: clamp(.88rem, 4vw, 1.25rem); font-weight: 900; line-height: 1; text-shadow: 0 2px 2px #000, 0 0 7px rgb(0 0 0 / 90%); }
.combat-numbers__entry--crit { color: #ffd36d; font-size: clamp(1.12rem, 5vw, 1.55rem); }
.combat-numbers__entry--heal { color: #75e6a5; }
.combat-numbers__entry--miss { color: #c7c0b2; font-size: .66rem; letter-spacing: .08em; }
.combat-number-enter-active { animation: combat-number-rise 850ms ease-out both; }
.combat-number-leave-active { display: none; }
@keyframes combat-number-rise { from { opacity: 0; transform: translateY(.45rem) scale(.85); } 18% { opacity: 1; } to { opacity: 0; transform: translateY(-2.4rem) scale(1.05); } }
@media (prefers-reduced-motion: reduce) { .combat-number-enter-active { animation: combat-number-fade 400ms linear both; } }
@keyframes combat-number-fade { from { opacity: 1; } to { opacity: 0; } }
</style>
