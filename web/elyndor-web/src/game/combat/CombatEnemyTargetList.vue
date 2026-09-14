<script setup lang="ts">
import type { CombatActorSnapshot } from '@/api/contracts'
import IconGenerator from '@/ui/icons/IconGenerator.vue'

const props = defineProps<{
  enemies: CombatActorSnapshot[]
  selectedTargetActorId: string
  disabled: boolean
  healthRatio: (enemy: CombatActorSnapshot) => number
  aggroName: (enemy: CombatActorSnapshot) => string
}>()

const emit = defineEmits<{
  select: [targetActorId: string]
}>()

function accessibleLabel(enemy: CombatActorSnapshot): string {
  const selected = enemy.actorId === props.selectedTargetActorId ? ', выбранная цель' : ''
  const aggro = enemy.currentAggroTargetActorId ? `, агро: ${props.aggroName(enemy)}` : ''
  return `${enemy.name}, здоровье ${Math.round(props.healthRatio(enemy))}%${selected}${aggro}`
}
</script>

<template>
  <nav
    v-if="enemies.length > 1"
    class="combat-enemy-targets"
    aria-label="Выбор цели"
    data-combat-targets
  >
    <button
      v-for="enemy in enemies"
      :key="enemy.actorId"
      type="button"
      :class="{ active: enemy.actorId === selectedTargetActorId }"
      :aria-label="accessibleLabel(enemy)"
      :disabled="disabled"
      :data-target-actor-id="enemy.actorId"
      @click="emit('select', enemy.actorId)"
    >
      <span>{{ enemy.name }}</span>
      <b v-if="enemy.actorId === selectedTargetActorId" class="combat-enemy-targets__selected">ЦЕЛЬ</b>
      <em v-if="enemy.currentAggroTargetActorId" class="combat-enemy-targets__aggro">Агро: {{ aggroName(enemy) }}</em>
      <div class="combat-enemy-targets__vitals">
        <i aria-hidden="true"><b :style="{ width: `${healthRatio(enemy)}%` }" /></i>
        <small>{{ Math.ceil(enemy.hp) }} / {{ Math.ceil(enemy.maxHp) }} · {{ Math.round(healthRatio(enemy)) }}%</small>
      </div>
      <span class="combat-enemy-targets__portrait" aria-hidden="true">
        <IconGenerator :config="{ id: `target-${enemy.actorId}`, glyph: 'skull', category: 'utility' }" />
      </span>
    </button>
  </nav>
</template>

<style scoped>
.combat-enemy-targets { display: grid; gap: 5px; }
.combat-enemy-targets button { display: grid; min-height: var(--ui-touch-target); grid-template-columns: 2.1rem minmax(0, 1fr); grid-template-rows: auto auto auto; align-items: center; column-gap: 7px; padding: 5px 7px; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); background: rgb(5 8 13 / 82%); color: var(--ui-color-text-primary); font: inherit; text-align: left; }
.combat-enemy-targets button.active { border-color: rgb(216 95 114 / 65%); box-shadow: inset 0 0 0 1px rgb(216 95 114 / 15%); }
.combat-enemy-targets button > span:first-child { grid-column: 2; overflow: hidden; font-size: var(--ui-font-size-xs); font-weight: 800; text-overflow: ellipsis; white-space: nowrap; }
.combat-enemy-targets__aggro { grid-column: 2; overflow: hidden; color: var(--ui-color-gold-muted); font-size: .5rem; font-style: normal; text-overflow: ellipsis; white-space: nowrap; }
.combat-enemy-targets__selected { grid-column: 2; width: max-content; border-radius: var(--ui-radius-round); padding: 1px 4px; background: rgb(216 95 114 / 22%); color: #efa1ae; font-size: .42rem; letter-spacing: .06em; }
.combat-enemy-targets__vitals { display: grid; grid-column: 2; gap: 2px; }
.combat-enemy-targets__vitals i { display: block; height: 4px; overflow: hidden; border-radius: var(--ui-radius-round); background: rgb(255 255 255 / 9%); }
.combat-enemy-targets__vitals i b { display: block; height: 100%; border-radius: inherit; background: linear-gradient(90deg, #a44e62, #e38d98); }
.combat-enemy-targets__vitals small { font-size: var(--ui-font-size-xs); }
.combat-enemy-targets__portrait { display: grid; width: 2rem; height: 2rem; grid-column: 1; grid-row: 1 / 4; place-items: center; }
.combat-enemy-targets__portrait :deep(.icon-generator) { border-color: rgb(216 95 114 / 35%); color: #efa1ae; }
</style>
