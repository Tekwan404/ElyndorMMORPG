<script setup lang="ts">
import { computed, nextTick, onUnmounted, ref, watch } from 'vue'

import {
  collectCombatDamageFeedHits,
  type CombatDamageFeedHit,
} from '@/game/combat/combatDamageFeed'
import { useCombatSessionStore } from '@/stores/combatSession'

const combat = useCombatSessionStore()
const battlefieldReady = ref(false)
const playerDamageFeed = ref<CombatDamageFeedHit[]>([])
const enemyDamageFeed = ref<CombatDamageFeedHit[]>([])
let battlefieldFrame: number | null = null
let lastProcessedDamageSequence = 0
const removalTimers = new Map<number, number>()

const DAMAGE_FEED_LIMIT = 4
const DAMAGE_FEED_LIFETIME_MS = 1_700

const isPartyCombat = computed(() => (combat.snapshot?.players?.length ?? 1) > 1)
const selectedEnemy = computed(() => {
  const snapshot = combat.snapshot
  if (!snapshot) return null
  const enemies = snapshot.enemies ?? [snapshot.enemy]
  const selectedActorId = snapshot.selectedTargetActorId ?? snapshot.enemy.actorId
  return enemies.find(enemy => enemy.actorId === selectedActorId) ?? snapshot.enemy
})

function cancelBattlefieldFrame(): void {
  if (battlefieldFrame === null) return
  window.cancelAnimationFrame(battlefieldFrame)
  battlefieldFrame = null
}

function clearRemovalTimer(sequence: number): void {
  const timer = removalTimers.get(sequence)
  if (timer === undefined) return
  window.clearTimeout(timer)
  removalTimers.delete(sequence)
}

function clearDamageFeed(): void {
  for (const timer of removalTimers.values()) window.clearTimeout(timer)
  removalTimers.clear()
  playerDamageFeed.value = []
  enemyDamageFeed.value = []
}

function primeCursor(): void {
  clearDamageFeed()
  lastProcessedDamageSequence = Math.max(0, ...combat.events.map(event => event.sequence))
}

function removeHit(hit: CombatDamageFeedHit): void {
  const feed = hit.side === 'player' ? playerDamageFeed : enemyDamageFeed
  feed.value = feed.value.filter(entry => entry.sequence !== hit.sequence)
  clearRemovalTimer(hit.sequence)
}

function pushHit(hit: CombatDamageFeedHit): void {
  const feed = hit.side === 'player' ? playerDamageFeed : enemyDamageFeed
  const next = [...feed.value.filter(entry => entry.sequence !== hit.sequence), hit]
  const overflow = Math.max(0, next.length - DAMAGE_FEED_LIMIT)
  for (const removed of next.slice(0, overflow)) clearRemovalTimer(removed.sequence)
  feed.value = next.slice(-DAMAGE_FEED_LIMIT)

  clearRemovalTimer(hit.sequence)
  removalTimers.set(hit.sequence, window.setTimeout(() => removeHit(hit), DAMAGE_FEED_LIFETIME_MS))
}

function processDamageEvents(): void {
  const snapshot = combat.snapshot
  const enemy = selectedEnemy.value
  if (!snapshot || !enemy || !isPartyCombat.value) return

  const batch = collectCombatDamageFeedHits(
    combat.events,
    lastProcessedDamageSequence,
    snapshot.player.actorId,
    enemy.actorId,
  )
  lastProcessedDamageSequence = batch.latestSequence
  for (const hit of batch.hits) pushHit(hit)
}

async function syncBattlefield(active: boolean): Promise<void> {
  cancelBattlefieldFrame()
  battlefieldReady.value = false
  if (!active || !isPartyCombat.value) return

  await nextTick()
  let attempts = 0
  const findBattlefield = (): void => {
    battlefieldReady.value = document.querySelector('[data-combat-battlefield]') !== null
    if (battlefieldReady.value || attempts >= 20) {
      battlefieldFrame = null
      return
    }
    attempts += 1
    battlefieldFrame = window.requestAnimationFrame(findBattlefield)
  }
  findBattlefield()
}

watch(
  () => combat.snapshot?.sessionId ?? null,
  () => primeCursor(),
  { immediate: true },
)

watch(
  () => combat.events.map(event => event.sequence),
  () => processDamageEvents(),
)

watch(
  () => selectedEnemy.value?.actorId ?? null,
  () => {
    for (const entry of enemyDamageFeed.value) clearRemovalTimer(entry.sequence)
    enemyDamageFeed.value = []
    lastProcessedDamageSequence = Math.max(0, ...combat.events.map(event => event.sequence))
  },
)

watch(
  [() => combat.isActive, isPartyCombat],
  ([active]) => void syncBattlefield(active),
  { immediate: true },
)

onUnmounted(() => {
  cancelBattlefieldFrame()
  clearDamageFeed()
})
</script>

<template>
  <Teleport v-if="battlefieldReady && isPartyCombat && combat.snapshot" to="[data-combat-battlefield]">
    <TransitionGroup
      name="party-damage-stack"
      tag="div"
      class="party-damage-feed party-damage-feed--player"
      data-combat-party-player-damage-feed
      aria-hidden="true"
    >
      <span
        v-for="hit in playerDamageFeed"
        :key="hit.sequence"
        class="party-damage-feed__hit"
        :class="{ 'party-damage-feed__hit--critical': hit.critical }"
      >
        {{ hit.critical ? 'КРИТ ' : '' }}−{{ hit.amount }}
      </span>
    </TransitionGroup>

    <TransitionGroup
      name="party-damage-stack"
      tag="div"
      class="party-damage-feed party-damage-feed--enemy"
      data-combat-party-enemy-damage-feed
      aria-hidden="true"
    >
      <span
        v-for="hit in enemyDamageFeed"
        :key="hit.sequence"
        class="party-damage-feed__hit"
        :class="{ 'party-damage-feed__hit--critical': hit.critical }"
      >
        {{ hit.critical ? 'КРИТ ' : '' }}−{{ hit.amount }}
      </span>
    </TransitionGroup>
  </Teleport>
</template>

<style scoped>
.party-damage-feed {
  position: absolute;
  z-index: 7;
  bottom: 4.8rem;
  display: flex;
  width: min(30%, 7rem);
  flex-direction: column;
  gap: 3px;
  pointer-events: none;
}

.party-damage-feed--player {
  left: 2.5%;
  align-items: flex-start;
}

.party-damage-feed--enemy {
  right: 2.5%;
  align-items: flex-end;
}

.party-damage-feed__hit {
  display: inline-flex;
  min-width: 2.6rem;
  min-height: 1.25rem;
  align-items: center;
  justify-content: center;
  padding: 3px 6px;
  border: 1px solid rgb(216 95 114 / 42%);
  border-radius: 6px;
  background: rgb(8 9 14 / 78%);
  box-shadow: 0 5px 13px rgb(0 0 0 / 30%);
  color: rgb(244 168 180);
  font-size: .6rem;
  font-weight: 900;
  line-height: 1;
  text-shadow: 0 1px 7px rgb(0 0 0 / 75%);
  backdrop-filter: blur(3px);
}

.party-damage-feed--enemy .party-damage-feed__hit {
  border-color: rgb(205 177 113 / 44%);
  color: rgb(239 216 159);
}

.party-damage-feed__hit--critical {
  border-color: rgb(244 187 93 / 76%);
  background: rgb(38 22 10 / 86%);
  color: rgb(255 225 149);
  font-size: .7rem;
}

.party-damage-stack-enter-active,
.party-damage-stack-leave-active,
.party-damage-stack-move {
  transition: opacity 180ms ease, transform 180ms ease;
}

.party-damage-stack-enter-from {
  opacity: 0;
  transform: translateY(7px) scale(.92);
}

.party-damage-stack-leave-to {
  opacity: 0;
  transform: translateY(-8px) scale(.94);
}

@media (max-width: 420px) {
  .party-damage-feed {
    bottom: 4.15rem;
    width: 27%;
    gap: 2px;
  }

  .party-damage-feed--player { left: 1.5%; }
  .party-damage-feed--enemy { right: 1.5%; }
  .party-damage-feed__hit { min-width: 2.3rem; min-height: 1.15rem; padding: 2px 5px; font-size: .54rem; }
  .party-damage-feed__hit--critical { font-size: .62rem; }
}
</style>
