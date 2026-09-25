<script setup lang="ts">
import { computed } from 'vue'

import type { CombatActorSnapshot } from '@/api/contracts'
import { resolveCharacterArt } from '@/assets/characterArt'
import { useGameSessionStore } from '@/stores/gameSession'

const props = defineProps<{
  allies: readonly CombatActorSnapshot[]
  localActorId: string
  selectedActorId: string | null
  aggroActorIds: readonly string[]
  disabled: boolean
}>()
const emit = defineEmits<{ select: [actorId: string] }>()
const session = useGameSessionStore()

function art(actor: CombatActorSnapshot): string | null {
  const local = actor.actorId === props.localActorId
  return resolveCharacterArt(
    actor.definitionId,
    actor.genderId ?? (local ? session.snapshot?.character?.genderId : null) ?? 'MALE',
    'transparent',
    local ? session.snapshot?.character?.activeSkinId ?? actor.skinId : actor.skinId,
  )
}

function healthRatio(actor: CombatActorSnapshot): number {
  return actor.maxHp > 0 ? Math.max(0, Math.min(100, actor.hp / actor.maxHp * 100)) : 0
}

const visibleAllies = computed(() => props.allies.slice(0, 5))
</script>

<template>
  <nav class="allies-strip" aria-label="Участники группы" data-allies-strip>
    <button
      v-for="ally in visibleAllies"
      :key="ally.actorId"
      type="button"
      class="allies-strip__actor"
      :data-ally-strip-actor="ally.actorId"
      :data-selected="selectedActorId === ally.actorId"
      :data-aggro="aggroActorIds.includes(ally.actorId)"
      :aria-pressed="selectedActorId === ally.actorId"
      :aria-label="`${ally.name}, ${Math.round(healthRatio(ally))}% HP${aggroActorIds.includes(ally.actorId) ? ', под агро' : ''}`"
      :disabled="disabled || ally.hp <= 0"
      @click="emit('select', ally.actorId)"
    >
      <span class="allies-strip__portrait">
        <img v-if="art(ally)" :src="art(ally)!" alt="" loading="lazy" decoding="async" sizes="46px">
        <b v-else>{{ ally.name.slice(0, 1) }}</b>
      </span>
      <span class="allies-strip__health"><i :style="{ width: `${healthRatio(ally)}%` }" /></span>
      <small v-if="aggroActorIds.includes(ally.actorId)" aria-hidden="true">◆</small>
    </button>
  </nav>
</template>

<style scoped>
.allies-strip { display: flex; min-width: 0; align-items: center; justify-content: center; gap: .25rem; }
.allies-strip__actor { position: relative; display: grid; width: 46px; min-width: 44px; height: 48px; min-height: 44px; grid-template-rows: 1fr 4px; padding: 2px; overflow: hidden; border: 1px solid rgb(177 157 105 / 28%); border-radius: 8px; background: rgb(6 8 13 / 78%); color: #e8dfd0; cursor: pointer; touch-action: manipulation; }
.allies-strip__actor[data-selected='true'] { border-color: #e4c66f; box-shadow: 0 0 0 1px rgb(228 198 111 / 20%), 0 0 10px rgb(228 198 111 / 26%); }
.allies-strip__actor[data-aggro='true'] { background: linear-gradient(180deg, rgb(66 17 26 / 82%), rgb(7 8 13 / 90%)); }
.allies-strip__actor:focus-visible { outline: 2px solid #f0d57f; outline-offset: 2px; }
.allies-strip__portrait { display: grid; min-height: 0; place-items: end center; overflow: hidden; }
.allies-strip__portrait img { width: 100%; height: 100%; object-fit: contain; object-position: center 18%; }
.allies-strip__health { display: block; overflow: hidden; border-radius: 999px; background: #160b0e; }
.allies-strip__health i { display: block; height: 100%; background: #bd5065; }
.allies-strip__actor > small { position: absolute; top: 2px; right: 3px; color: #ff8d9a; font-size: .5rem; text-shadow: 0 0 4px #000; }
</style>
