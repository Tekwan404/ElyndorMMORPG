<script setup lang="ts">
import { computed, onUnmounted, shallowRef, watch } from 'vue'

import type { CombatNumberPresentation } from '@/game/combat/battleEventPresentation'
import {
  DEFAULT_COMBAT_NUMBER_SETTINGS,
  type CombatNumberSettings,
} from '@/game/combat/combatNumberSettings'

const props = withDefaults(
  defineProps<{
    actorId: string
    entries: readonly CombatNumberPresentation[]
    settings?: CombatNumberSettings
  }>(),
  { settings: () => ({ ...DEFAULT_COMBAT_NUMBER_SETTINGS }) },
)

interface VisibleCombatNumber extends CombatNumberPresentation {
  offsetX: number
  offsetY: number
}

const visibleEntries = shallowRef<VisibleCombatNumber[]>([])
const capacity = computed(() =>
  props.settings.density === 'full' ? 5 : props.settings.density === 'minimal' ? 2 : 4,
)
const timers = new Map<number, number>()
let lastSeenKey = 0
let initialized = false
let lane = 0

function removeEntry(key: number): void {
  const timer = timers.get(key)
  if (timer !== undefined) {
    window.clearTimeout(timer)
    timers.delete(key)
  }
  visibleEntries.value = visibleEntries.value.filter((entry) => entry.key !== key)
}

function addEntry(entry: CombatNumberPresentation): void {
  // Four small offset lanes avoid overlapping text without moving the 2D battlefield.
  const offsets = [-22, 17, -6, 27, 4]
  const heights = [0, -13, 7, -6, 12]
  const offsetIndex = lane++ % offsets.length
  const next: VisibleCombatNumber = {
    ...entry,
    offsetX: offsets[offsetIndex]!,
    offsetY: heights[offsetIndex]!,
  }
  const overflow = visibleEntries.value.length + 1 - capacity.value
  for (const old of visibleEntries.value.slice(0, Math.max(0, overflow))) {
    removeEntry(old.key)
  }
  visibleEntries.value = [...visibleEntries.value, next]
  const timer = window.setTimeout(() => removeEntry(entry.key), 780)
  timers.set(entry.key, timer)
}

watch(
  () => props.entries,
  (entries) => {
    const latestKey = entries.at(-1)?.key ?? 0
    if (!initialized) {
      // A reconnect or remount must not replay the entire combat history.
      initialized = true
      lastSeenKey = latestKey
      return
    }
    if (latestKey <= lastSeenKey) return

    const incoming: CombatNumberPresentation[] = []
    for (let index = entries.length - 1; index >= 0 && incoming.length < capacity.value; index--) {
      const entry = entries[index]!
      if (entry.key <= lastSeenKey) break
      if (entry.targetActorId !== props.actorId) continue
      if (!props.settings.showPeriodicDamage && entry.periodic) continue
      if (props.settings.density === 'minimal' && entry.kind === 'damage' && entry.key % 3 !== 0) continue
      incoming.push(entry)
    }
    lastSeenKey = latestKey
    for (const entry of incoming.reverse()) addEntry(entry)
  },
  { immediate: true },
)

watch(capacity, (limit) => {
  for (const old of visibleEntries.value.slice(0, -limit)) removeEntry(old.key)
})

onUnmounted(() => {
  for (const timer of timers.values()) window.clearTimeout(timer)
  timers.clear()
})

function label(entry: CombatNumberPresentation): string {
  if (entry.kind === 'miss') return 'ПРОМАХ'
  if (entry.kind === 'heal') return `+${Math.round(entry.value ?? 0)}`
  return `-${Math.round(entry.value ?? 0)}`
}
</script>

<template>
  <span class="combat-numbers" :data-combat-numbers-for="actorId" aria-hidden="true">
    <TransitionGroup name="combat-number">
      <b
        v-for="entry in visibleEntries"
        :key="entry.key"
        class="combat-numbers__entry"
        :class="[
          `combat-numbers__entry--${entry.kind === 'crit' && !settings.emphasizeCrits ? 'damage' : entry.kind}`,
        ]"
        :data-combat-number="entry.key"
        :data-impact="settings.hitEffects && (entry.kind === 'damage' || entry.kind === 'crit')"
        :style="{ '--offset-x': `${entry.offsetX}px`, '--offset-y': `${entry.offsetY}px` }"
      >{{ label(entry) }}</b>
    </TransitionGroup>
  </span>
</template>

<style scoped>
.combat-numbers {
  position: absolute;
  z-index: 20;
  top: 28%;
  left: 50%;
  width: 0;
  height: 0;
  pointer-events: none;
}
.combat-numbers__entry {
  position: absolute;
  top: 0;
  left: 0;
  display: block;
  width: max-content;
  color: #ff8490;
  font-family: var(--ui-font-body);
  font-size: clamp(0.8rem, 3.3vw, 1.1rem);
  font-weight: 900;
  line-height: 1;
  text-shadow: 0 2px 2px #000, 0 0 6px rgb(0 0 0 / 88%);
  white-space: nowrap;
  will-change: transform, opacity;
}
.combat-numbers__entry--crit {
  color: #ffd36d;
  font-size: clamp(1.05rem, 4.6vw, 1.4rem);
}
.combat-numbers__entry--heal {
  color: #75e6a5;
}
.combat-numbers__entry--miss {
  color: #c7c0b2;
  font-size: 0.66rem;
  letter-spacing: 0.08em;
}
.combat-numbers__entry[data-impact='true']::after {
  position: absolute;
  top: -0.45rem;
  left: 50%;
  width: 2.1rem;
  height: 2.1rem;
  border: 1px solid currentColor;
  border-radius: 50%;
  content: '';
  pointer-events: none;
  animation: combat-hit-pulse 230ms ease-out both;
}
.combat-number-enter-active {
  animation: combat-number-rise 780ms ease-out both;
}
.combat-number-leave-active {
  display: none;
}
@keyframes combat-number-rise {
  0% {
    opacity: 0;
    transform: translate3d(calc(-50% + var(--offset-x)), calc(var(--offset-y) + 8px), 0) scale(0.85);
  }
  18% {
    opacity: 1;
  }
  100% {
    opacity: 0;
    transform: translate3d(calc(-50% + var(--offset-x)), calc(var(--offset-y) - 35px), 0) scale(1.03);
  }
}
@keyframes combat-hit-pulse {
  from {
    opacity: 0.7;
    transform: translateX(-50%) scale(0.6);
  }
  to {
    opacity: 0;
    transform: translateX(-50%) scale(1.3);
  }
}
@media (prefers-reduced-motion: reduce) {
  .combat-number-enter-active {
    animation: combat-number-fade 400ms linear both;
  }
  .combat-numbers__entry[data-impact='true']::after {
    animation: none;
    display: none;
  }
}
@keyframes combat-number-fade {
  from { opacity: 1; }
  to { opacity: 0; }
}
</style>
