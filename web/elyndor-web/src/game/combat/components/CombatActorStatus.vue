<script setup lang="ts">
import { computed } from 'vue'
import type { CombatActorSnapshot } from '@/api/contracts'
import { resourceLabel } from '@/game/character/characterPresentation'

const props = defineProps<{ actor: CombatActorSnapshot; fallbackLevel?: number }>()
const level = computed(() => props.actor.level || props.fallbackLevel)
const resourceName = computed(() => resourceLabel(props.actor.resourceType))
const hasResource = computed(
  () => props.actor.resourceType !== 'NONE' && props.actor.maxResource > 0,
)
const healthPercent = computed(() => percent(props.actor.hp, props.actor.maxHp))
const healthState = computed(() =>
  healthPercent.value <= 25 ? 'critical' : healthPercent.value <= 50 ? 'low' : 'normal',
)
function percent(value: number, max: number): number {
  return max > 0 ? Math.max(0, Math.min(100, (value / max) * 100)) : 0
}
function valueText(value: number, max: number): string {
  return `${Math.ceil(Math.max(0, Math.min(value, max)))} / ${Math.ceil(Math.max(0, max))}`
}
</script>

<template>
  <span class="combat-actor-status" :data-actor-status="actor.actorId">
    <span class="combat-actor-status__name" :title="actor.name"
      ><strong>{{ actor.name }}</strong
      ><small v-if="level">{{ level }}</small></span
    >
    <span
      class="combat-actor-status__bar combat-actor-status__bar--hp"
      :data-health-state="healthState"
      role="progressbar"
      :aria-label="`HP ${actor.name}`"
      :aria-valuenow="Math.max(0, Math.min(actor.hp, actor.maxHp))"
      :aria-valuemax="actor.maxHp"
      aria-valuemin="0"
    >
      <i :style="{ width: `${healthPercent}%` }" /><b aria-hidden="true">{{
        valueText(actor.hp, actor.maxHp)
      }}</b>
    </span>
    <span
      v-if="hasResource"
      class="combat-actor-status__bar combat-actor-status__bar--resource"
      :data-resource="actor.resourceType"
      role="progressbar"
      :aria-label="`${resourceName} ${actor.name}`"
      :aria-valuenow="Math.max(0, Math.min(actor.resource, actor.maxResource))"
      :aria-valuemax="actor.maxResource"
      aria-valuemin="0"
    >
      <i :style="{ width: `${percent(actor.resource, actor.maxResource)}%` }" /><b
        aria-hidden="true"
        >{{ valueText(actor.resource, actor.maxResource) }}</b
      >
    </span>
  </span>
</template>

<style scoped>
.combat-actor-status {
  display: grid;
  min-width: 0;
  gap: 3px;
  width: 100%;
}
.combat-actor-status__name {
  display: flex;
  min-width: 0;
  justify-content: space-between;
  align-items: baseline;
  gap: 4px;
  line-height: 1.15;
}
.combat-actor-status__name strong {
  overflow: hidden;
  font-size: var(--combat-status-name-size, clamp(9px, 2.5vw, 12px));
  text-overflow: ellipsis;
  white-space: nowrap;
}
.combat-actor-status__name small {
  flex: none;
  color: #b9afa3;
  font-size: var(--combat-status-font-size, 8px);
}
.combat-actor-status__bar {
  position: relative;
  display: grid;
  height: var(--combat-status-bar-height, 14px);
  min-width: 0;
  place-items: center;
  overflow: hidden;
  border-radius: 4px;
  background: #080a10;
  box-shadow: inset 0 1px 2px #000;
}
.combat-actor-status__bar i {
  position: absolute;
  inset-block: 0;
  left: 0;
  border-radius: inherit;
  background: linear-gradient(90deg, #8c3044, #dd6678);
  transition: width 160ms ease;
}
.combat-actor-status__bar b {
  position: relative;
  padding-inline: 2px;
  color: #fff4e5;
  font: 700 var(--combat-status-font-size, 9px)/1 var(--ui-font-body);
  font-variant-numeric: tabular-nums;
  text-shadow:
    0 1px 2px #000,
    0 0 3px #000;
  white-space: nowrap;
}
.combat-actor-status__bar[data-health-state='low'] i {
  background: linear-gradient(90deg, #9a4c2c, #e59545);
}
.combat-actor-status__bar[data-health-state='critical'] i {
  background: linear-gradient(90deg, #8d142c, #ff405c);
}
.combat-actor-status__bar[data-resource='MANA'] i {
  background: linear-gradient(90deg, #3c57a9, #719bf0);
}
.combat-actor-status__bar[data-resource='FOCUS'] i {
  background: linear-gradient(90deg, #827028, #cbb457);
}
.combat-actor-status__bar[data-resource='RAGE'] i {
  background: linear-gradient(90deg, #963c23, #d78c41);
}
@media (prefers-reduced-motion: reduce) {
  .combat-actor-status__bar i {
    transition: none;
  }
}
</style>
