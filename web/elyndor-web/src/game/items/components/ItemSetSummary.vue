<script setup lang="ts">
import { computed } from 'vue'
import type { InventoryItem } from '@/api/contracts'
const props = defineProps<{ setId: string; items: readonly InventoryItem[] }>()
const equippedCount = computed(
  () =>
    props.items.filter((item) => item.setId === props.setId && item.equippedSlot !== null).length,
)
</script>

<template>
  <section class="item-set-summary" data-item-set aria-label="Комплект предмета">
    <strong>Предмет комплекта</strong>
    <span>Предметов этого комплекта надето: {{ equippedCount }}</span>
    <small>Активные бонусы учитываются сервером в характеристиках героя.</small>
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
