<script setup lang="ts">
import type { InventoryItem } from '@/api/contracts'
import ItemIcon from './ItemIcon.vue'
import ItemQualityStars from '@/ui/components/ItemQualityStars.vue'
import { itemRarityLabel } from '@/game/items/itemRarity'
withDefaults(defineProps<{ item: InventoryItem; subtitle?: string; showQuantity?: boolean }>(), {
  showQuantity: true,
})
</script>

<template>
  <header class="item-identity" :data-rarity="item.rarity">
    <span class="item-identity__icon">
      <ItemIcon
        :icon-id="item.iconId"
        :item-id="item.id"
        :name="item.name"
        :type="item.type"
        :equipment-slot="item.slot"
        :rarity="item.rarity"
        loading="eager"
      />
    </span>
    <div class="item-identity__text">
      <strong>{{ item.name }}</strong>
      <span
        >{{ itemRarityLabel(item.rarity)
        }}<template v-if="subtitle"> · {{ subtitle }}</template></span
      >
      <ItemQualityStars
        v-if="item.generatedItem"
        :id="`item-identity-${item.id}`"
        :stars="item.generatedItem.stars"
      />
      <small v-if="showQuantity">Количество: {{ item.quantity }}</small>
      <small v-if="item.isLocked">Предмет защищён</small>
    </div>
  </header>
</template>

<style scoped>
.item-identity {
  --item-rarity: var(--ui-rarity-common);
  display: flex;
  align-items: center;
  gap: var(--ui-space-3);
  min-width: 0;
  padding: var(--ui-space-3);
  border: 1px solid color-mix(in srgb, var(--item-rarity) 65%, var(--ui-color-border));
  border-radius: var(--ui-radius-md);
  background: var(--ui-gradient-panel);
}
.item-identity[data-rarity='Uncommon'] {
  --item-rarity: var(--ui-rarity-uncommon);
}
.item-identity[data-rarity='Rare'] {
  --item-rarity: var(--ui-rarity-rare);
}
.item-identity[data-rarity='Epic'] {
  --item-rarity: var(--ui-rarity-epic);
}
.item-identity[data-rarity='Legendary'] {
  --item-rarity: var(--ui-rarity-legendary);
}
.item-identity[data-rarity='Unique'] {
  --item-rarity: var(--ui-rarity-unique);
}
.item-identity__icon {
  width: var(--ui-icon-slot-lg);
  height: var(--ui-icon-slot-lg);
  flex: none;
}
.item-identity__icon :deep(.item-icon) {
  width: 100%;
  height: 100%;
}
.item-identity__text {
  display: grid;
  gap: var(--ui-space-1);
  min-width: 0;
  overflow-wrap: anywhere;
}
.item-identity__text strong {
  color: var(--ui-color-text-primary);
  font-family: var(--ui-font-display);
}
.item-identity__text span,
.item-identity__text small {
  color: var(--ui-color-text-secondary);
  font-size: var(--ui-font-size-sm);
}
</style>
