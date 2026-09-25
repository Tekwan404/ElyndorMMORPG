<script setup lang="ts">
import { computed } from 'vue'

import type { CombatActorSnapshot } from '@/api/contracts'
import { monsterArtUrl } from '@/assets/monsterArt'
import AlliesStrip from './AlliesStrip.vue'

const props = defineProps<{
  localActor: CombatActorSnapshot
  enemy: CombatActorSnapshot
  allies: readonly CombatActorSnapshot[]
  selectedFriendlyActorId: string | null
  aggroActorIds: readonly string[]
  disabled: boolean
}>()
const emit = defineEmits<{ selectFriendly: [actorId: string] }>()

const party = computed(() => props.allies.length > 1)
const resourceLabel = computed(() => {
  if (props.localActor.resourceType === 'MANA') return 'Мана'
  if (props.localActor.resourceType === 'FOCUS') return 'Фокус'
  return 'Ярость'
})
const enemyArt = computed(() => monsterArtUrl(props.enemy.artId, props.enemy.definitionId))

function ratio(value: number, max: number): number {
  return max > 0 ? Math.max(0, Math.min(100, (value / max) * 100)) : 0
}
</script>

<template>
  <header class="battle-header" :data-party="party">
    <section
      class="battle-header__status battle-header__status--player"
      aria-label="Состояние героя"
    >
      <div class="battle-header__identity">
        <strong>{{ localActor.name }}</strong>
        <small>Ур. {{ localActor.level ?? 1 }}</small>
      </div>
      <label
        >Здоровье <b>{{ Math.ceil(localActor.hp) }} / {{ Math.ceil(localActor.maxHp) }}</b></label
      >
      <span class="battle-header__bar battle-header__bar--health"
        ><i :style="{ width: `${ratio(localActor.hp, localActor.maxHp)}%` }"
      /></span>
      <label
        >{{ resourceLabel }}
        <b>{{ Math.ceil(localActor.resource) }} / {{ Math.ceil(localActor.maxResource) }}</b></label
      >
      <span class="battle-header__bar" :data-resource="localActor.resourceType"
        ><i
          :style="{
            width: `${ratio(localActor.resource, localActor.maxResource)}%`,
          }"
      /></span>
    </section>

    <AlliesStrip
      v-if="party"
      class="battle-header__allies"
      :allies="allies"
      :local-actor-id="localActor.actorId"
      :selected-actor-id="selectedFriendlyActorId"
      :aggro-actor-ids="aggroActorIds"
      :disabled="disabled"
      @select="emit('selectFriendly', $event)"
    />

    <section
      class="battle-header__status battle-header__status--enemy"
      aria-label="Состояние противника"
    >
      <span class="battle-header__enemy-portrait" aria-hidden="true">
        <img v-if="enemyArt" :src="enemyArt" alt="" />
        <b v-else>{{ enemy.name.slice(0, 1) }}</b>
      </span>
      <div class="battle-header__identity">
        <strong>{{ enemy.name }}</strong>
        <small>Ур. {{ enemy.level ?? 1 }}</small>
      </div>
      <label
        >Здоровье <b>{{ Math.ceil(enemy.hp) }} / {{ Math.ceil(enemy.maxHp) }}</b></label
      >
      <span class="battle-header__bar battle-header__bar--health"
        ><i :style="{ width: `${ratio(enemy.hp, enemy.maxHp)}%` }"
      /></span>
    </section>
  </header>
</template>

<style scoped>
.battle-header {
  display: grid;
  min-height: 5.1rem;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  gap: 0.45rem;
}
.battle-header[data-party='true'] {
  grid-template-columns: minmax(7.4rem, 1fr) auto minmax(7.4rem, 1fr);
}
.battle-header__status {
  position: relative;
  display: grid;
  align-content: center;
  gap: 2px;
  min-width: 0;
  padding: 0.48rem 0.58rem;
  border: 1px solid rgb(178 151 89 / 40%);
  border-radius: 9px;
  background: linear-gradient(135deg, rgb(14 17 25 / 94%), rgb(6 8 13 / 90%));
  box-shadow: inset 0 0 18px rgb(0 0 0 / 25%);
}
.battle-header__status--enemy {
  padding-right: 3.15rem;
  border-color: rgb(184 71 91 / 50%);
}
.battle-header__identity {
  display: flex;
  min-width: 0;
  align-items: baseline;
  justify-content: space-between;
  gap: 0.35rem;
}
.battle-header__identity strong {
  overflow: hidden;
  font-family: var(--ui-font-display);
  font-size: clamp(0.72rem, 3.2vw, 0.95rem);
  text-overflow: ellipsis;
  white-space: nowrap;
}
.battle-header__identity small,
.battle-header__status label {
  color: #afa89e;
  font-size: clamp(0.46rem, 2vw, 0.58rem);
}
.battle-header__status label {
  display: flex;
  justify-content: space-between;
}
.battle-header__status label b {
  color: #e9e2d6;
  font-weight: 700;
}
.battle-header__bar {
  display: block;
  height: 5px;
  overflow: hidden;
  border-radius: 999px;
  background: #090b10;
  box-shadow: inset 0 1px 2px #000;
}
.battle-header__bar i {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, #9f6729, #dda845);
}
.battle-header__bar--health i {
  background: linear-gradient(90deg, #93344b, #dc6579);
}
.battle-header__bar[data-resource='MANA'] i {
  background: linear-gradient(90deg, #3c57a9, #719bf0);
}
.battle-header__bar[data-resource='FOCUS'] i {
  background: linear-gradient(90deg, #8b6b2e, #e0bd5f);
}
.battle-header__allies {
  align-self: center;
}
.battle-header__enemy-portrait {
  position: absolute;
  top: 0.25rem;
  right: 0.25rem;
  display: grid;
  width: 2.6rem;
  height: 2.6rem;
  place-items: center;
  overflow: hidden;
  border: 1px solid rgb(194 79 98 / 46%);
  border-radius: 50%;
  background: #090c13;
}
.battle-header__enemy-portrait img {
  width: 100%;
  height: 100%;
  object-fit: contain;
}

@media (max-width: 520px) {
  .battle-header[data-party='true'] {
    grid-template-columns: 1fr 1fr;
  }
  .battle-header__allies {
    grid-column: 1 / -1;
    grid-row: 2;
    min-height: 48px;
    order: 3;
  }
  .battle-header__status {
    min-height: 4.55rem;
    padding: 0.38rem 0.44rem;
  }
  .battle-header__status--enemy {
    padding-right: 2.75rem;
  }
  .battle-header__enemy-portrait {
    width: 2.25rem;
    height: 2.25rem;
  }
}
</style>
