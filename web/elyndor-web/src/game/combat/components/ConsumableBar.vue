<script setup lang="ts">
import { computed } from 'vue'

import type { InventoryItem } from '@/api/contracts'
import ItemIcon from '@/game/items/components/ItemIcon.vue'

const props = defineProps<{
  items: readonly InventoryItem[]
  cooldownRemaining: (item: InventoryItem) => number
  canUse: (item: InventoryItem) => boolean
  isPending: (item: InventoryItem) => boolean
}>()
const emit = defineEmits<{ use: [item: InventoryItem] }>()

const groupedItems = computed<InventoryItem[]>(() => {
  const grouped = new Map<string, InventoryItem>()

  for (const item of props.items) {
    const existing = grouped.get(item.definitionId)
    grouped.set(
      item.definitionId,
      existing
        ? {
            ...existing,
            quantity: existing.quantity + item.quantity,
          }
        : { ...item },
    )
  }

  return [...grouped.values()]
})

function available(item: InventoryItem): boolean {
  return !props.isPending(item) && props.cooldownRemaining(item) <= 0 && props.canUse(item)
}
</script>

<template>
  <section
    v-if="groupedItems.length"
    class="consumable-bar"
    aria-label="Расходники"
    data-consumable-bar
  >
    <div class="consumable-bar__items">
      <button
        v-for="item in groupedItems"
        :key="item.definitionId"
        type="button"
        :data-combat-consumable="item.definitionId"
        :data-rarity="item.rarity"
        :disabled="!available(item)"
        :aria-label="`${item.name}, ${item.quantity} шт.`"
        @click="emit('use', item)"
      >
        <ItemIcon
          :icon-id="item.iconId"
          :item-id="item.definitionId"
          :name="item.name"
          :type="item.type"
          :rarity="item.rarity"
          decorative
          loading="eager"
        />
        <b class="consumable-bar__quantity">×{{ item.quantity }}</b>
        <em v-if="cooldownRemaining(item) > 0">{{ Math.ceil(cooldownRemaining(item) / 1000) }}с</em>
      </button>
    </div>
  </section>
</template>

<style scoped>
.consumable-bar {
  display: flex;
  min-width: 0;
  align-items: center;
}
.consumable-bar__items {
  display: flex;
  min-width: 0;
  gap: 0.28rem;
  overflow-x: auto;
  scrollbar-width: none;
}
.consumable-bar__items::-webkit-scrollbar {
  display: none;
}
.consumable-bar button {
  position: relative;
  display: grid;
  width: 46px;
  height: 46px;
  min-width: 46px;
  flex: none;
  place-items: center;
  padding: 0.18rem;
  border: 1px solid rgb(82 171 143 / 30%);
  border-radius: 8px;
  background: rgb(5 14 15 / 76%);
  color: #e3e0d8;
  font: inherit;
  cursor: pointer;
  touch-action: manipulation;
}
.consumable-bar button:disabled {
  filter: grayscale(0.65);
  opacity: 0.5;
}
.consumable-bar button :deep(.item-icon) {
  width: 38px;
  height: 38px;
}
.consumable-bar__quantity {
  position: absolute;
  right: 0.08rem;
  bottom: 0.06rem;
  min-width: 1.25rem;
  padding: 0.06rem 0.16rem;
  border-radius: 4px;
  background: rgb(2 7 8 / 88%);
  color: #e8f7f1;
  font-size: 0.5rem;
  line-height: 1;
  text-align: center;
  text-shadow: 0 1px 2px #000;
}
.consumable-bar button em {
  position: absolute;
  inset: 0;
  display: grid;
  place-items: center;
  border-radius: inherit;
  background: rgb(2 7 8 / 82%);
  color: white;
  font-size: 0.7rem;
  font-style: normal;
}
</style>
