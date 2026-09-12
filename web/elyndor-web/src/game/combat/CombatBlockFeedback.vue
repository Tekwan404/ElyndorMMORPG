<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'

import type { CombatEvent } from '@/api/contracts'
import { useCombatSessionStore } from '@/stores/combatSession'

interface BlockFeedback {
  sequence: number
  amount: number
  playerSide: boolean
}

const combat = useCombatSessionStore()
const feedback = ref<BlockFeedback | null>(null)
let hideTimer: number | null = null
let lastSequence = 0

const latestShieldAbsorb = computed<CombatEvent | null>(() => {
  for (let index = combat.events.length - 1; index >= 0; index--) {
    const event = combat.events[index]
    if (event.type === 'ShieldAbsorbed') return event
  }
  return null
})

function isPlayerSideTarget(actorId: string | null): boolean {
  if (!actorId || !combat.snapshot) return true
  if (combat.snapshot.player.actorId === actorId) return true
  if (combat.snapshot.companion?.actorId === actorId) return true
  return (combat.snapshot.players ?? []).some(player => player.actorId === actorId)
}

function clearHideTimer(): void {
  if (hideTimer === null) return
  window.clearTimeout(hideTimer)
  hideTimer = null
}

watch(
  () => latestShieldAbsorb.value?.sequence ?? 0,
  () => {
    const event = latestShieldAbsorb.value
    if (!event || event.sequence <= lastSequence || event.amount <= 0) return

    lastSequence = event.sequence
    feedback.value = {
      sequence: event.sequence,
      amount: event.amount,
      playerSide: isPlayerSideTarget(event.targetActorId ?? event.actorId),
    }

    clearHideTimer()
    hideTimer = window.setTimeout(() => {
      feedback.value = null
      hideTimer = null
    }, 1_250)
  },
  { immediate: true },
)

watch(
  () => combat.snapshot?.sessionId ?? null,
  () => {
    lastSequence = 0
    feedback.value = null
    clearHideTimer()
  },
)

onUnmounted(clearHideTimer)
</script>

<template>
  <Transition name="combat-block-pop">
    <div
      v-if="feedback"
      :key="feedback.sequence"
      class="combat-block-feedback"
      :class="{ 'combat-block-feedback--enemy': !feedback.playerSide }"
      role="status"
      aria-live="polite"
      data-combat-block-feedback
    >
      <span class="combat-block-feedback__shield" aria-hidden="true">🛡</span>
      <strong>Блок −{{ Math.round(feedback.amount) }}</strong>
    </div>
  </Transition>
</template>

<style scoped>
.combat-block-feedback {
  position: fixed;
  z-index: 9600;
  top: 39%;
  left: 25%;
  display: inline-flex;
  align-items: center;
  gap: 5px;
  min-height: 30px;
  padding: 5px 9px;
  border: 1px solid rgb(108 189 255 / 66%);
  border-radius: 999px;
  background: linear-gradient(135deg, rgb(16 45 70 / 94%), rgb(7 16 28 / 94%));
  box-shadow: 0 8px 22px rgb(0 0 0 / 38%), inset 0 0 14px rgb(91 177 255 / 8%);
  color: rgb(189 226 255);
  pointer-events: none;
  transform: translate(-50%, -50%);
  font-family: var(--ui-font-body, system-ui, sans-serif);
}

.combat-block-feedback--enemy {
  left: auto;
  right: 25%;
  transform: translate(50%, -50%);
}

.combat-block-feedback__shield {
  filter: drop-shadow(0 0 5px rgb(91 177 255 / 48%));
  font-size: 1rem;
  line-height: 1;
}

.combat-block-feedback strong {
  font-size: .72rem;
  font-weight: 950;
  letter-spacing: .025em;
  white-space: nowrap;
}

.combat-block-pop-enter-active,
.combat-block-pop-leave-active {
  transition: opacity .18s ease, transform .18s ease;
}

.combat-block-pop-enter-from,
.combat-block-pop-leave-to {
  opacity: 0;
}

.combat-block-pop-enter-from:not(.combat-block-feedback--enemy),
.combat-block-pop-leave-to:not(.combat-block-feedback--enemy) {
  transform: translate(-50%, -30%) scale(.9);
}

.combat-block-pop-enter-from.combat-block-feedback--enemy,
.combat-block-pop-leave-to.combat-block-feedback--enemy {
  transform: translate(50%, -30%) scale(.9);
}

@media (max-width: 430px) {
  .combat-block-feedback {
    top: 43%;
    left: 22%;
  }

  .combat-block-feedback--enemy {
    left: auto;
    right: 22%;
  }
}
</style>
