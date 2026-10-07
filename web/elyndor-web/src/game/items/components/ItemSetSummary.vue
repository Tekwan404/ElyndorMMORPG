<script setup lang="ts">
import { computed } from 'vue'
import type { InventoryItem } from '@/api/contracts'
const props = defineProps<{ setId: string; items: readonly InventoryItem[] }>()
const summary = computed(() => props.items.find((item) => item.setId === props.setId)?.setSummary)
const equippedCount = computed(
  () =>
    new Set(
      props.items
        .filter((item) => item.setId === props.setId && item.equippedSlot !== null)
        .map((item) => item.definitionId),
    ).size,
)
</script>

<template>
  <section class="item-set-summary" data-item-set aria-label="Комплект предмета">
    <template v-if="summary">
      <strong>{{ summary.name }} — {{ equippedCount }}/{{ summary.totalPieces }}</strong>
      <span
        v-for="bonus in summary.bonuses"
        :key="bonus.requiredPieces"
        :data-bonus-active="equippedCount >= bonus.requiredPieces"
      >
        {{ equippedCount >= bonus.requiredPieces ? '✓' : '○' }} {{ bonus.requiredPieces }} предм. —
        {{ bonus.description }}
      </span>
      <small
        >Осталось доступно частей комплекта:
        {{ Math.max(0, summary.totalPieces - equippedCount) }}</small
      >
    </template>
    <template v-else>
      <strong>Предмет комплекта</strong>
      <span>Предметов этого комплекта надето: {{ equippedCount }}</span>
    </template>
  </section>
</template>

<style scoped>
.item-set-summary {
  display: grid;
  gap: var(--ui-space-1);
  padding: var(--ui-space-3);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
}
.item-set-summary strong {
  color: var(--ui-color-gold);
}
.item-set-summary span,
.item-set-summary small {
  font-size: var(--ui-font-size-sm);
  color: var(--ui-color-text-secondary);
}
</style>
