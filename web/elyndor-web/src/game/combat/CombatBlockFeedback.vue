<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'

import type { CombatEvent } from '@/api/contracts'
import { useCombatSessionStore } from '@/stores/combatSession'

interface BlockFeedback {
  sequence: number
  amount: number
  fullBlock: boolean
  playerSide: boolean
}

type AudioContextConstructor = new () => AudioContext

type WindowWithWebkitAudio = Window & typeof globalThis & {
  webkitAudioContext?: AudioContextConstructor
}

const combat = useCombatSessionStore()
const feedback = ref<BlockFeedback | null>(null)
let hideTimer: number | null = null
let lastSequence = 0
let audioContext: AudioContext | null = null

const latestBlock = computed<CombatEvent | null>(() => {
  for (let index = combat.events.length - 1; index >= 0; index--) {
    const event = combat.events[index]
    if (event?.type === 'DamageBlocked') return event
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

function playMetalBlockSound(): void {
  try {
    const browserWindow = window as WindowWithWebkitAudio
    const Context = window.AudioContext ?? browserWindow.webkitAudioContext
    if (!Context) return

    audioContext ??= new Context()
    const context = audioContext
    if (context.state === 'suspended') void context.resume()

    const now = context.currentTime
    const gain = context.createGain()
    const oscillator = context.createOscillator()
    oscillator.type = 'square'
    oscillator.frequency.setValueAtTime(420, now)
    oscillator.frequency.exponentialRampToValueAtTime(135, now + 0.16)
    gain.gain.setValueAtTime(0.0001, now)
    gain.gain.exponentialRampToValueAtTime(0.12, now + 0.006)
    gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.2)
    oscillator.connect(gain)
    gain.connect(context.destination)
    oscillator.start(now)
    oscillator.stop(now + 0.21)
  } catch {
    // Combat feedback must remain visual if WebAudio is unavailable or blocked by the WebView.
  }
}

watch(
  () => latestBlock.value?.sequence ?? 0,
  () => {
    const event = latestBlock.value
    if (!event || event.sequence <= lastSequence || event.amount <= 0) return

    lastSequence = event.sequence
    feedback.value = {
      sequence: event.sequence,
      amount: event.amount,
      fullBlock: event.amountBeforeShields <= 0,
      playerSide: isPlayerSideTarget(event.targetActorId ?? event.actorId),
    }
    playMetalBlockSound()

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

onUnmounted(() => {
  clearHideTimer()
  if (audioContext) {
    void audioContext.close()
    audioContext = null
  }
})
</script>

<template>
  <Transition name="combat-block-pop">
    <div
      v-if="feedback"
      :key="feedback.sequence"
      class="combat-block-feedback"
      :class="{
        'combat-block-feedback--enemy': !feedback.playerSide,
        'combat-block-feedback--full': feedback.fullBlock,
      }"
      role="status"
      aria-live="polite"
      data-combat-block-feedback
    >
      <span class="combat-block-feedback__flash" aria-hidden="true" />
      <span class="combat-block-feedback__shield" aria-hidden="true">🛡</span>
      <strong v-if="feedback.fullBlock">ПОЛНЫЙ БЛОК</strong>
      <strong v-else>БЛОК −{{ Math.round(feedback.amount) }}</strong>
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

.combat-block-feedback--full {
  border-color: rgb(225 239 255 / 86%);
  background: linear-gradient(135deg, rgb(28 66 95 / 97%), rgb(8 22 38 / 97%));
  color: rgb(235 247 255);
  box-shadow: 0 8px 26px rgb(0 0 0 / 42%), 0 0 24px rgb(116 201 255 / 22%);
}

.combat-block-feedback__flash {
  position: absolute;
  inset: 50% auto auto 16px;
  width: 22px;
  aspect-ratio: 1;
  border: 2px solid rgb(172 224 255 / 78%);
  border-radius: 50%;
  box-shadow: 0 0 14px rgb(100 193 255 / 58%);
  opacity: 0;
  transform: translate(-50%, -50%) scale(.35);
  animation: combat-shield-flash .46s ease-out both;
}

.combat-block-feedback__shield {
  position: relative;
  z-index: 1;
  filter: drop-shadow(0 0 5px rgb(91 177 255 / 48%));
  font-size: 1rem;
  line-height: 1;
  animation: combat-shield-hit .34s ease-out both;
}

.combat-block-feedback strong {
  position: relative;
  z-index: 1;
  font-size: .72rem;
  font-weight: 950;
  letter-spacing: .035em;
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

@keyframes combat-shield-flash {
  0% { opacity: .92; transform: translate(-50%, -50%) scale(.35); }
  65% { opacity: .46; }
  100% { opacity: 0; transform: translate(-50%, -50%) scale(2.35); }
}

@keyframes combat-shield-hit {
  0% { transform: scale(.7) rotate(-8deg); }
  45% { transform: scale(1.18) rotate(3deg); }
  100% { transform: scale(1) rotate(0); }
}

@media (prefers-reduced-motion: reduce) {
  .combat-block-feedback__flash,
  .combat-block-feedback__shield {
    animation: none;
  }
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
