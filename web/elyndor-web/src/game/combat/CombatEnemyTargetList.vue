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

function hasVisibleResource(enemy: CombatActorSnapshot): boolean {
  return enemy.maxResource > 0 && enemy.resourceType !== 'NONE'
}

function resourceRatio(enemy: CombatActorSnapshot): number {
  if (enemy.maxResource <= 0) return 0
  return Math.min(100, Math.max(0, (enemy.resource / enemy.maxResource) * 100))
}

function resourceLabel(enemy: CombatActorSnapshot): string {
  if (enemy.resourceType === 'MANA') return 'Мана'
  if (enemy.resourceType === 'FOCUS') return 'Фокус'
  if (enemy.resourceType === 'RAGE') return 'Ярость'
  return enemy.resourceType
}

function accessibleLabel(enemy: CombatActorSnapshot): string {
  const selected = enemy.actorId === props.selectedTargetActorId ? ', выбранная цель' : ''
  const aggro = enemy.currentAggroTargetActorId ? `, агро: ${props.aggroName(enemy)}` : ''
  const resource = hasVisibleResource(enemy)
    ? `, ${resourceLabel(enemy).toLowerCase()} ${Math.ceil(enemy.resource)} из ${Math.ceil(enemy.maxResource)}`
    : ''
  return `${enemy.name}, здоровье ${Math.round(props.healthRatio(enemy))}%${resource}${selected}${aggro}`
}
</script>

<template>
  <nav
    v-if="enemies.length > 1 || enemies.some(hasVisibleResource)"
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
        <template v-if="hasVisibleResource(enemy)">
          <i class="combat-enemy-targets__resource" :data-resource="enemy.resourceType" aria-hidden="true">
            <b :style="{ width: `${resourceRatio(enemy)}%` }" />
          </i>
          <small class="combat-enemy-targets__resource-copy">
            {{ resourceLabel(enemy) }} · {{ Math.ceil(enemy.resource) }} / {{ Math.ceil(enemy.maxResource) }}
          </small>
        </template>
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
.combat-enemy-targets__vitals .combat-enemy-targets__resource { margin-top: 1px; }
.combat-enemy-targets__vitals .combat-enemy-targets__resource b { background: linear-gradient(90deg, #4559aa, #7aa7ff); }
.combat-enemy-targets__vitals .combat-enemy-targets__resource[data-resource='FOCUS'] b { background: linear-gradient(90deg, #9a6b24, #e0b85d); }
.combat-enemy-targets__vitals .combat-enemy-targets__resource[data-resource='RAGE'] b { background: linear-gradient(90deg, #8f3434, #df6a5d); }
.combat-enemy-targets__resource-copy { color: #9fbaf8; }
.combat-enemy-targets__portrait { display: grid; width: 2rem; height: 2rem; grid-column: 1; grid-row: 1 / 4; place-items: center; }
.combat-enemy-targets__portrait :deep(.icon-generator) { border-color: rgb(216 95 114 / 35%); color: #efa1ae; }

.combat-enemy-targets--battlefield {
  position: absolute;
  z-index: 4;
  top: 8px;
  right: 8px;
  width: min(42%, 11rem);
}

.combat-enemy-targets--battlefield button {
  min-height: 34px;
  grid-template-columns: 1.6rem minmax(0, 1fr);
  column-gap: 4px;
  padding: 3px 4px;
}

.combat-enemy-targets--battlefield .combat-enemy-targets__portrait {
  width: 1.6rem;
  height: 1.6rem;
}

.combat-enemy-targets--battlefield .combat-enemy-targets__aggro,
.combat-enemy-targets--battlefield .combat-enemy-targets__selected { font-size: .38rem; }
.combat-enemy-targets--battlefield .combat-enemy-targets__vitals { gap: 1px; }
.combat-enemy-targets--battlefield .combat-enemy-targets__vitals small { font-size: .42rem; }
</style>
