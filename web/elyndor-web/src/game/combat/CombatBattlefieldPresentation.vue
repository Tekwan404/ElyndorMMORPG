<script setup lang="ts">
import { computed, nextTick, onUnmounted, ref, watch } from 'vue'

import { resolveCharacterArt } from '@/assets/characterArt'
import { monsterArtUrl } from '@/assets/monsterArt'
import {
  collectCombatDamageFeedHits,
  type CombatDamageFeedHit,
} from '@/game/combat/combatDamageFeed'
import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'

const combat = useCombatSessionStore()
const session = useGameSessionStore()
const battlefieldReady = ref(false)
const battlefieldElement = ref<HTMLElement | null>(null)
const playerDamageFeed = ref<CombatDamageFeedHit[]>([])
const enemyDamageFeed = ref<CombatDamageFeedHit[]>([])
let battlefieldFrame: number | null = null
let lastProcessedDamageSequence = 0
const damageRemovalTimers = new Map<number, number>()

const DAMAGE_FEED_LIMIT = 4
const DAMAGE_FEED_LIFETIME_MS = 1_700

const selectedEnemy = computed(() => {
  const snapshot = combat.snapshot
  if (!snapshot) return null

  const enemies = snapshot.enemies ?? [snapshot.enemy]
  const selectedActorId = snapshot.selectedTargetActorId ?? snapshot.enemy.actorId
  return enemies.find(enemy => enemy.actorId === selectedActorId) ?? snapshot.enemy
})

const isSoloCombat = computed(() => (combat.snapshot?.players?.length ?? 1) <= 1)
const playerLevel = computed(() =>
  session.snapshot?.character?.level ?? combat.snapshot?.player.level ?? null,
)
const enemyLevel = computed(() =>
  selectedEnemy.value?.level ?? combat.encounterPresentation?.level ?? null,
)
const playerArt = computed(() => {
  const snapshot = combat.snapshot
  if (!snapshot) return null

  return resolveCharacterArt(
    snapshot.player.definitionId,
    session.snapshot?.character?.genderId ?? 'MALE',
    'transparent',
    session.snapshot?.character?.activeSkinId ?? snapshot.player.skinId,
  )
})
const enemyArt = computed(() => {
  const enemy = selectedEnemy.value
  if (!enemy) return null

  const encounter = combat.encounterPresentation
  const matchesEncounter = encounter?.monsterId === enemy.definitionId
  const artId = enemy.artId ?? (matchesEncounter ? encounter?.artId : null)
  return monsterArtUrl(artId, enemy.definitionId)
})
const enemyArtProfile = computed<'beast' | 'boss' | 'humanoid'>(() => {
  const enemy = selectedEnemy.value
  if (!enemy) return 'humanoid'

  const presentationKey = [
    enemy.definitionId,
    enemy.artId,
    enemy.name,
  ]
    .filter(Boolean)
    .join(' ')
    .toLocaleLowerCase('ru-RU')

  if (/(boss|giant|titan|coloss|dragon|hydra|босс|великан|титан|колосс|дракон|гидр)/u.test(presentationKey)) {
    return 'boss'
  }

  if (/(wolf|warg|beast|boar|bear|hound|spider|crawler|rat|волк|варг|звер|кабан|медвед|гонч|паук|крыса)/u.test(presentationKey)) {
    return 'beast'
  }

  return 'humanoid'
})
const enemySize = computed(() => selectedEnemy.value?.monsterRank ?? 'Normal')
const slotLayoutActive = computed(() =>
  isSoloCombat.value && Boolean(playerArt.value) && Boolean(enemyArt.value),
)

function cancelBattlefieldFrame(): void {
  if (battlefieldFrame !== null) {
    window.cancelAnimationFrame(battlefieldFrame)
    battlefieldFrame = null
  }
}

function clearBattlefieldLayoutMarker(): void {
  battlefieldElement.value?.removeAttribute('data-combat-slot-layout')
  battlefieldElement.value = null
}

function clearDamageRemovalTimer(sequence: number): void {
  const timer = damageRemovalTimers.get(sequence)
  if (timer === undefined) return
  window.clearTimeout(timer)
  damageRemovalTimers.delete(sequence)
}

function clearDamageFeed(): void {
  for (const timer of damageRemovalTimers.values()) window.clearTimeout(timer)
  damageRemovalTimers.clear()
  playerDamageFeed.value = []
  enemyDamageFeed.value = []
}

function primeDamageFeedCursor(): void {
  clearDamageFeed()
  lastProcessedDamageSequence = Math.max(0, ...combat.events.map(event => event.sequence))
}

function removeDamageHit(hit: CombatDamageFeedHit): void {
  const feed = hit.side === 'player' ? playerDamageFeed : enemyDamageFeed
  feed.value = feed.value.filter(entry => entry.sequence !== hit.sequence)
  clearDamageRemovalTimer(hit.sequence)
}

function pushDamageHit(hit: CombatDamageFeedHit): void {
  const feed = hit.side === 'player' ? playerDamageFeed : enemyDamageFeed
  const next = [...feed.value.filter(entry => entry.sequence !== hit.sequence), hit]
  const trimmed = next.slice(-DAMAGE_FEED_LIMIT)

  for (const removed of next.slice(0, Math.max(0, next.length - DAMAGE_FEED_LIMIT))) {
    clearDamageRemovalTimer(removed.sequence)
  }

  feed.value = trimmed
  clearDamageRemovalTimer(hit.sequence)
  damageRemovalTimers.set(hit.sequence, window.setTimeout(() => {
    removeDamageHit(hit)
  }, DAMAGE_FEED_LIFETIME_MS))
}

function processDamageEvents(): void {
  const snapshot = combat.snapshot
  const enemy = selectedEnemy.value
  if (!snapshot || !enemy || !isSoloCombat.value) return

  const batch = collectCombatDamageFeedHits(
    combat.events,
    lastProcessedDamageSequence,
    snapshot.player.actorId,
    enemy.actorId,
  )
  lastProcessedDamageSequence = batch.latestSequence
  for (const hit of batch.hits) pushDamageHit(hit)
}

async function syncBattlefieldTarget(active: boolean): Promise<void> {
  cancelBattlefieldFrame()
  clearBattlefieldLayoutMarker()
  battlefieldReady.value = false
  if (!active) return

  await nextTick()

  let attempts = 0
  const findBattlefield = (): void => {
    const target = document.querySelector<HTMLElement>('[data-combat-battlefield]')
    battlefieldReady.value = target !== null

    if (target) {
      battlefieldElement.value = target
      if (slotLayoutActive.value) target.setAttribute('data-combat-slot-layout', 'solo')
    }

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
  () => primeDamageFeedCursor(),
  { immediate: true },
)

watch(
  () => combat.events.map(event => event.sequence),
  () => processDamageEvents(),
)

watch(
  () => selectedEnemy.value?.actorId ?? null,
  () => {
    for (const entry of enemyDamageFeed.value) clearDamageRemovalTimer(entry.sequence)
    enemyDamageFeed.value = []
  },
)

watch(
  () => combat.isActive,
  active => void syncBattlefieldTarget(active),
  { immediate: true },
)

watch(
  slotLayoutActive,
  () => {
    if (combat.isActive) void syncBattlefieldTarget(true)
  },
)

onUnmounted(() => {
  cancelBattlefieldFrame()
  clearBattlefieldLayoutMarker()
  clearDamageFeed()
})
</script>

<template>
  <Teleport v-if="battlefieldReady && combat.snapshot" to="[data-combat-battlefield]">
    <template v-if="slotLayoutActive">
      <div
        class="battlefield-slot battlefield-slot--player"
        data-combat-player-slot
        aria-hidden="true"
      >
        <div class="battlefield-slot__art-stage">
          <div class="battlefield-art battlefield-art--player">
            <img v-if="playerArt" :src="playerArt" alt="" />
          </div>
        </div>

        <span
          v-if="playerLevel !== null"
          class="combat-level-badge combat-level-badge--slot"
          data-combat-player-level
        >
          Ур. {{ playerLevel }}
        </span>

        <TransitionGroup
          name="damage-stack"
          tag="div"
          class="combat-damage-feed combat-damage-feed--slot-player"
          data-combat-player-damage-feed
        >
          <span
            v-for="hit in playerDamageFeed"
            :key="hit.sequence"
            class="combat-damage-feed__hit"
            :class="{ 'combat-damage-feed__hit--critical': hit.critical }"
          >
            {{ hit.critical ? 'КРИТ ' : '' }}−{{ hit.amount }}
          </span>
        </TransitionGroup>
      </div>

      <div
        class="battlefield-slot battlefield-slot--enemy"
        data-combat-enemy-slot
        aria-hidden="true"
      >
        <div class="battlefield-slot__art-stage">
          <div
            class="battlefield-art battlefield-art--enemy"
            :class="[`battlefield-art--enemy-${enemyArtProfile}`, `battlefield-art--enemy-size-${enemySize}`]"
          >
            <img v-if="enemyArt" :src="enemyArt" :alt="selectedEnemy?.name ?? ''" />
          </div>
        </div>

        <span
          v-if="enemyLevel !== null"
          class="combat-level-badge combat-level-badge--slot combat-level-badge--slot-enemy"
          data-combat-enemy-level
        >
          Ур. {{ enemyLevel }}
        </span>

        <TransitionGroup
          name="damage-stack"
          tag="div"
          class="combat-damage-feed combat-damage-feed--slot-enemy"
          data-combat-enemy-damage-feed
        >
          <span
            v-for="hit in enemyDamageFeed"
            :key="hit.sequence"
            class="combat-damage-feed__hit"
            :class="{ 'combat-damage-feed__hit--critical': hit.critical }"
          >
            {{ hit.critical ? 'КРИТ ' : '' }}−{{ hit.amount }}
          </span>
        </TransitionGroup>
      </div>
    </template>

    <template v-else>
      <span
        v-if="playerLevel !== null"
        class="combat-level-badge combat-level-badge--player"
        data-combat-player-level
      >
        Ур. {{ playerLevel }}
      </span>
      <span
        v-if="enemyLevel !== null"
        class="combat-level-badge combat-level-badge--enemy"
        data-combat-enemy-level
      >
        Ур. {{ enemyLevel }}
      </span>

      <TransitionGroup
        v-if="isSoloCombat"
        name="damage-stack"
        tag="div"
        class="combat-damage-feed combat-damage-feed--player"
        data-combat-player-damage-feed
        aria-hidden="true"
      >
        <span
          v-for="hit in playerDamageFeed"
          :key="hit.sequence"
          class="combat-damage-feed__hit"
          :class="{ 'combat-damage-feed__hit--critical': hit.critical }"
        >
          {{ hit.critical ? 'КРИТ ' : '' }}−{{ hit.amount }}
        </span>
      </TransitionGroup>

      <TransitionGroup
        v-if="isSoloCombat"
        name="damage-stack"
        tag="div"
        class="combat-damage-feed combat-damage-feed--enemy"
        data-combat-enemy-damage-feed
        aria-hidden="true"
      >
        <span
          v-for="hit in enemyDamageFeed"
          :key="hit.sequence"
          class="combat-damage-feed__hit"
          :class="{ 'combat-damage-feed__hit--critical': hit.critical }"
        >
          {{ hit.critical ? 'КРИТ ' : '' }}−{{ hit.amount }}
        </span>
      </TransitionGroup>
    </template>
  </Teleport>
</template>

<style scoped>
/*
 * Presentation-only adapter for the approved combat reference.
 * CombatView keeps ownership of mechanics and state; this component owns the
 * solo battlefield actor scene and battlefield-local presentation feedback.
 */
:global(.combat-hud__identity > small) {
  display: none;
}

:global(.combat-hud__identity strong) {
  font-size: .8rem;
  line-height: 1.15;
}

:global(.combat-screen:not(.combat-screen--party) .battlefield) {
  min-height: 20.5rem;
}

:global(.battlefield[data-combat-slot-layout='solo'] > .player-figure),
:global(.battlefield[data-combat-slot-layout='solo'] > .enemy-figure) {
  display: none;
}

:global(.combat-screen:not(.combat-screen--party) .battlefield:not([data-combat-slot-layout='solo']) > .player-figure) {
  bottom: .2rem;
  left: 4%;
  width: 42%;
  height: clamp(13rem, 50vw, 15.5rem);
  place-items: end center;
  opacity: .96;
}

:global(.combat-screen:not(.combat-screen--party) .battlefield:not([data-combat-slot-layout='solo']) > .enemy-figure) {
  top: auto;
  right: 3%;
  bottom: 1.15rem;
  left: auto;
  width: 48%;
  height: clamp(11.5rem, 45vw, 13.5rem);
  place-items: end center;
}

:global(.combat-screen:not(.combat-screen--party) .battlefield:not([data-combat-slot-layout='solo']) > .enemy-figure::after) {
  right: 12%;
  bottom: 0;
  left: 12%;
}

.battlefield-slot {
  position: absolute;
  top: 1.4rem;
  bottom: .35rem;
  width: 39%;
  pointer-events: none;
}

.battlefield-slot--player {
  left: 2.5%;
}

.battlefield-slot--enemy {
  right: 2.5%;
}

.battlefield-slot__art-stage {
  position: absolute;
  inset: 0 0 1.65rem;
  display: flex;
  align-items: flex-end;
  justify-content: center;
}

.battlefield-art {
  position: relative;
  z-index: 2;
  display: flex;
  align-items: flex-end;
  justify-content: center;
  transform-origin: center bottom;
}

.battlefield-art img {
  width: 100%;
  height: 100%;
  object-fit: contain;
  object-position: center bottom;
  user-select: none;
  filter: drop-shadow(0 .75rem 1rem rgb(0 0 0 / 58%));
  pointer-events: none;
}

.battlefield-art--player {
  width: 56%;
  height: 61%;
  transform: translate3d(-2%, 0, 0) scale(1.8);
}

.battlefield-art--enemy {
  width: 100%;
  height: 84%;
  transform: translate3d(2%, 0, 0) scale(.74);
}

.battlefield-art--enemy-beast {
  transform: translate3d(2%, 0, 0) scale(.5);
}

.battlefield-art--enemy-size-Elite {
  transform: translate3d(2%, 0, 0) scale(.82);
}

.battlefield-art--enemy-size-Boss {
  transform: translate3d(2%, 0, 0) scale(.9);
}

.combat-level-badge {
  position: absolute;
  z-index: 5;
  bottom: .7rem;
  display: inline-flex;
  min-height: 1.45rem;
  align-items: center;
  justify-content: center;
  padding: 3px 8px;
  border: 1px solid rgb(205 177 113 / 66%);
  border-radius: 999px;
  background: rgb(7 11 18 / 90%);
  box-shadow: 0 5px 16px rgb(0 0 0 / 34%);
  color: rgb(224 211 170);
  font-size: .58rem;
  font-weight: 850;
  letter-spacing: .03em;
  line-height: 1;
  pointer-events: none;
  backdrop-filter: blur(5px);
}

.combat-level-badge--player {
  left: 4%;
}

.combat-level-badge--enemy {
  right: 4%;
  border-color: rgb(216 95 114 / 56%);
  color: rgb(239 199 205);
}

.combat-level-badge--slot {
  bottom: .15rem;
  left: 50%;
  transform: translateX(-50%);
}

.combat-level-badge--slot-enemy {
  border-color: rgb(216 95 114 / 56%);
  color: rgb(239 199 205);
}

.combat-damage-feed {
  position: absolute;
  z-index: 6;
  bottom: 5.1rem;
  display: flex;
  width: min(34%, 8.5rem);
  flex-direction: column;
  gap: 3px;
  pointer-events: none;
}

.combat-damage-feed--player {
  left: 2.5%;
  align-items: flex-start;
}

.combat-damage-feed--enemy {
  right: 2.5%;
  align-items: flex-end;
}

.combat-damage-feed--slot-player,
.combat-damage-feed--slot-enemy {
  bottom: 4.6rem;
  width: min(48%, 7rem);
}

.combat-damage-feed--slot-player {
  left: 0;
  align-items: flex-start;
}

.combat-damage-feed--slot-enemy {
  right: 0;
  align-items: flex-end;
}

.combat-damage-feed__hit {
  display: inline-flex;
  min-width: 2.8rem;
  min-height: 1.35rem;
  align-items: center;
  justify-content: center;
  padding: 3px 6px;
  border: 1px solid rgb(216 95 114 / 42%);
  border-radius: 6px;
  background: rgb(8 9 14 / 78%);
  box-shadow: 0 5px 13px rgb(0 0 0 / 30%);
  color: rgb(244 168 180);
  font-size: .62rem;
  font-weight: 900;
  line-height: 1;
  text-shadow: 0 1px 7px rgb(0 0 0 / 75%);
  backdrop-filter: blur(3px);
}

.combat-damage-feed--enemy .combat-damage-feed__hit,
.combat-damage-feed--slot-enemy .combat-damage-feed__hit {
  border-color: rgb(205 177 113 / 44%);
  color: rgb(239 216 159);
}

.combat-damage-feed__hit--critical {
  min-height: 1.55rem;
  padding-inline: 7px;
  border-color: rgb(244 187 93 / 76%);
  background: rgb(38 22 10 / 86%);
  color: rgb(255 225 149);
  box-shadow:
    0 5px 14px rgb(0 0 0 / 34%),
    0 0 12px rgb(244 187 93 / 18%);
  font-size: .72rem;
  letter-spacing: .02em;
}

.damage-stack-enter-active,
.damage-stack-leave-active,
.damage-stack-move {
  transition:
    opacity 180ms ease,
    transform 180ms ease;
}

.damage-stack-enter-from {
  opacity: 0;
  transform: translateY(7px) scale(.92);
}

.damage-stack-leave-to {
  opacity: 0;
  transform: translateY(-8px) scale(.94);
}

@media (max-width: 390px) {
  :global(.combat-screen:not(.combat-screen--party) .battlefield) {
    min-height: 19.25rem;
  }

  :global(.combat-screen:not(.combat-screen--party) .battlefield:not([data-combat-slot-layout='solo']) > .player-figure) {
    left: 2%;
    width: 44%;
    height: 13.5rem;
  }

  :global(.combat-screen:not(.combat-screen--party) .battlefield:not([data-combat-slot-layout='solo']) > .enemy-figure) {
    right: 1%;
    width: 50%;
    height: 11.75rem;
  }

  .battlefield-slot {
    top: 1.1rem;
    bottom: .25rem;
    width: 40%;
  }

  .battlefield-slot--player {
    left: 1.5%;
  }

  .battlefield-slot--enemy {
    right: 1.5%;
  }

  .battlefield-art--player {
    width: 58%;
    height: 60%;
    transform: translate3d(-2%, 0, 0) scale(1.72);
  }

  .battlefield-art--enemy-beast {
    transform: translate3d(2%, 0, 0) scale(.5);
  }

  .battlefield-art--enemy-size-Elite { transform: translate3d(2%, 0, 0) scale(.82); }
  .battlefield-art--enemy-size-Boss { transform: translate3d(2%, 0, 0) scale(.9); }

  .combat-damage-feed--slot-player,
  .combat-damage-feed--slot-enemy {
    bottom: 4.25rem;
    width: 50%;
  }

  .combat-damage-feed:not(.combat-damage-feed--slot-player):not(.combat-damage-feed--slot-enemy) {
    bottom: 4.65rem;
    width: 36%;
  }

  .combat-damage-feed--player {
    left: 1.5%;
  }

  .combat-damage-feed--enemy {
    right: 1.5%;
  }
}

@media (max-width: 340px) {
  :global(.combat-screen:not(.combat-screen--party) .battlefield) {
    min-height: 18rem;
  }

  :global(.combat-screen:not(.combat-screen--party) .battlefield:not([data-combat-slot-layout='solo']) > .player-figure) {
    height: 12.75rem;
  }

  :global(.combat-screen:not(.combat-screen--party) .battlefield:not([data-combat-slot-layout='solo']) > .enemy-figure) {
    height: 11rem;
  }

  .battlefield-slot {
    width: 41%;
  }

  .battlefield-art--player {
    transform: translate3d(-2%, 0, 0) scale(1.62);
  }

  .combat-level-badge {
    min-height: 1.3rem;
    padding-inline: 6px;
    font-size: .52rem;
  }

  .combat-damage-feed {
    gap: 2px;
  }

  .combat-damage-feed--slot-player,
  .combat-damage-feed--slot-enemy {
    bottom: 3.95rem;
  }

  .combat-damage-feed__hit {
    min-width: 2.45rem;
    min-height: 1.2rem;
    padding: 2px 5px;
    font-size: .56rem;
  }

  .combat-damage-feed__hit--critical {
    min-height: 1.4rem;
    font-size: .64rem;
  }
}
</style>
