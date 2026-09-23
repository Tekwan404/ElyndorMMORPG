<script setup lang="ts">
import type { CombatActorSnapshot } from '@/api/contracts'
import IconGenerator from '@/ui/icons/IconGenerator.vue'

const props = defineProps<{
  ally: CombatActorSnapshot | null
}>()

function healthPercent(ally: CombatActorSnapshot): number {
  return Math.round((ally.hp / Math.max(ally.maxHp, 1)) * 100)
}
</script>

<template>
  <Transition name="frontline-shift" mode="out-in">
    <aside
      v-if="ally"
      :key="ally.actorId"
      class="combat-frontline"
      :aria-label="`Под агро: ${ally.name}, здоровье ${healthPercent(ally)}%`"
    >
      <span class="combat-frontline__shield" aria-hidden="true">
        <IconGenerator :config="{ id: `frontline-${ally.actorId}`, glyph: 'shield', category: 'utility' }" />
      </span>
      <span class="combat-frontline__label">АГРО</span>
      <strong>{{ ally.name }}</strong>
      <span class="combat-frontline__hp">{{ healthPercent(ally) }}%</span>
    </aside>
  </Transition>
</template>

<style scoped>
.combat-frontline {
  position: absolute;
  top: .5rem;
  left: 50%;
  z-index: 5;
  display: flex;
  max-width: min(72%, 15rem);
  min-height: 30px;
  align-items: center;
  gap: 5px;
  padding: 3px 7px 3px 4px;
  border: 1px solid rgb(205 177 113 / 48%);
  border-radius: var(--ui-radius-round);
  background: rgb(6 10 18 / 90%);
  box-shadow: 0 5px 14px rgb(0 0 0 / 26%);
  color: var(--ui-color-text-primary);
  transform: translateX(-50%);
  backdrop-filter: blur(6px);
}

.combat-frontline__shield {
  display: grid;
  width: 22px;
  height: 22px;
  flex: 0 0 22px;
  place-items: center;
  border: 1px solid rgb(205 177 113 / 40%);
  border-radius: 50%;
  background: rgb(205 177 113 / 10%);
  color: var(--ui-color-gold);
}

.combat-frontline__shield :deep(.icon-generator) {
  width: 18px;
  height: 18px;
  border: 0;
  background: transparent;
  box-shadow: none;
}

.combat-frontline__label {
  color: var(--ui-color-gold-muted);
  font-size: .46rem;
  font-weight: 900;
  letter-spacing: .08em;
}

.combat-frontline strong {
  overflow: hidden;
  min-width: 0;
  font-size: .56rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.combat-frontline__hp {
  flex: 0 0 auto;
  color: #9be2c9;
  font-size: .54rem;
  font-weight: 900;
}

.frontline-shift-enter-active,
.frontline-shift-leave-active {
  transition: opacity 180ms ease, transform 180ms ease;
}

.frontline-shift-enter-from,
.frontline-shift-leave-to {
  opacity: 0;
  transform: translate(-50%, -6px);
}

@media (max-width: 360px) {
  .combat-frontline {
    max-width: 84%;
    padding-right: 6px;
  }
}
</style>
