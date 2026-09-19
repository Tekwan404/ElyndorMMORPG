<script setup lang="ts">
import { computed, nextTick, onUnmounted, ref, watch } from 'vue'

import { useCombatSessionStore } from '@/stores/combatSession'
import { useGameSessionStore } from '@/stores/gameSession'

const combat = useCombatSessionStore()
const session = useGameSessionStore()
const battlefieldReady = ref(false)
let battlefieldFrame: number | null = null

const selectedEnemy = computed(() => {
  const snapshot = combat.snapshot
  if (!snapshot) return null

  const enemies = snapshot.enemies ?? [snapshot.enemy]
  const selectedActorId = snapshot.selectedTargetActorId ?? snapshot.enemy.actorId
  return enemies.find(enemy => enemy.actorId === selectedActorId) ?? snapshot.enemy
})

const playerLevel = computed(() =>
  session.snapshot?.character?.level ?? combat.snapshot?.player.level ?? null,
)
const enemyLevel = computed(() =>
  selectedEnemy.value?.level ?? combat.encounterPresentation?.level ?? null,
)

function cancelBattlefieldFrame(): void {
  if (battlefieldFrame !== null) {
    window.cancelAnimationFrame(battlefieldFrame)
    battlefieldFrame = null
  }
}

async function syncBattlefieldTarget(active: boolean): Promise<void> {
  cancelBattlefieldFrame()
  battlefieldReady.value = false
  if (!active) return

  await nextTick()

  let attempts = 0
  const findBattlefield = (): void => {
    battlefieldReady.value = document.querySelector('[data-combat-battlefield]') !== null
    if (battlefieldReady.value || attempts >= 4) {
      battlefieldFrame = null
      return
    }

    attempts += 1
    battlefieldFrame = window.requestAnimationFrame(findBattlefield)
  }

  findBattlefield()
}

watch(
  () => combat.isActive,
  active => void syncBattlefieldTarget(active),
  { immediate: true },
)

onUnmounted(cancelBattlefieldFrame)
</script>

<template>
  <Teleport v-if="battlefieldReady && combat.snapshot" to="[data-combat-battlefield]">
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
  </Teleport>
</template>

<style scoped>
/*
 * Presentation-only adapter for the approved combat reference.
 * CombatView keeps ownership of mechanics and state; this component only
 * adjusts composition and adds level badges inside the existing battlefield.
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

:global(.combat-screen:not(.combat-screen--party) .player-figure) {
  bottom: .2rem;
  left: 4%;
  width: 42%;
  height: clamp(13rem, 50vw, 15.5rem);
  place-items: end center;
  opacity: .96;
}

:global(.combat-screen:not(.combat-screen--party) .enemy-figure) {
  top: auto;
  right: 3%;
  bottom: 1.15rem;
  left: auto;
  width: 48%;
  height: clamp(11.5rem, 45vw, 13.5rem);
  place-items: end center;
}

:global(.combat-screen:not(.combat-screen--party) .enemy-figure::after) {
  right: 12%;
  bottom: 0;
  left: 12%;
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

@media (max-width: 390px) {
  :global(.combat-screen:not(.combat-screen--party) .battlefield) {
    min-height: 19.25rem;
  }

  :global(.combat-screen:not(.combat-screen--party) .player-figure) {
    left: 2%;
    width: 44%;
    height: 13.5rem;
  }

  :global(.combat-screen:not(.combat-screen--party) .enemy-figure) {
    right: 1%;
    width: 50%;
    height: 11.75rem;
  }
}

@media (max-width: 340px) {
  :global(.combat-screen:not(.combat-screen--party) .battlefield) {
    min-height: 18rem;
  }

  :global(.combat-screen:not(.combat-screen--party) .player-figure) {
    height: 12.75rem;
  }

  :global(.combat-screen:not(.combat-screen--party) .enemy-figure) {
    height: 11rem;
  }

  .combat-level-badge {
    min-height: 1.3rem;
    padding-inline: 6px;
    font-size: .52rem;
  }
}
</style>
