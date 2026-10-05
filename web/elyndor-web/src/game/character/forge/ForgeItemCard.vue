<script setup lang="ts">
import type { InventoryItem } from '@/api/contracts'
import ItemIcon from '@/game/items/components/ItemIcon.vue'
import { ItemQualityStars } from '@/ui/components'
import { itemRarityLabel } from '@/game/character/forge/forgeWorkbenchPresentation'

withDefaults(
  defineProps<{
    item: InventoryItem
    selected?: boolean
    multiple?: boolean
    disabled?: boolean
    interactive?: boolean
  }>(),
  { interactive: true },
)
defineEmits<{ select: [] }>()
</script>

<template>
  <component
    :is="interactive ? 'button' : 'article'"
    class="forge-card"
    :class="{ 'forge-card--selected': selected }"
    :type="interactive ? 'button' : undefined"
    :data-forge-item="item.id"
    :aria-pressed="interactive ? (selected ?? false) : undefined"
    :disabled="interactive ? disabled : undefined"
    @click="interactive && $emit('select')"
  >
    <span v-if="multiple" class="forge-card__check" aria-hidden="true">{{
      selected ? '✓' : ''
    }}</span>
    <span class="forge-card__icon" :data-rarity="item.rarity">
      <ItemIcon
        :icon-id="item.iconId"
        :item-id="item.id"
        :name="item.name"
        :type="item.type"
        :equipment-slot="item.slot"
        :rarity="item.rarity"
      />
    </span>
    <span class="forge-card__copy">
      <strong>{{ item.name }}</strong>
      <span :data-rarity="item.rarity"
        >{{ itemRarityLabel(item.rarity) }} · ур.
        {{ item.generatedItem?.itemLevel ?? item.requiredLevel }}</span
      >
      <ItemQualityStars
        v-if="item.generatedItem"
        :id="`forge-${item.id}`"
        :stars="item.generatedItem.stars"
      />
      <small v-if="item.generatedItem" class="forge-card__quality">
        Качество {{ item.generatedItem.rollQuality.toFixed(2).replace(/\.00$/, '') }}%
      </small>
      <small v-if="disabled">{{
        multiple && item.equippedSlot
          ? 'Надетую вещь нельзя разобрать'
          : item.isLocked
            ? 'Предмет защищён'
            : 'Предмет недоступен'
      }}</small>
    </span>
    <span class="forge-card__power"
      ><small>Мощь</small><b>{{ Math.round(item.generatedItem?.itemPower ?? 0) }}</b></span
    >
  </component>
</template>

<style scoped>
.forge-card {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 12px;
  width: 100%;
  min-width: 0;
  padding: 12px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: linear-gradient(140deg, #111b27, #080d14);
  color: var(--ui-color-text-primary);
  font: inherit;
  text-align: left;
  cursor: pointer;
}
.forge-card:has(.forge-card__check) {
  grid-template-columns: 22px auto minmax(0, 1fr) auto;
}
.forge-card--selected {
  border-color: var(--ui-color-gold);
  box-shadow:
    inset 0 0 0 1px var(--ui-color-gold),
    0 0 14px rgb(232 200 102 / 10%);
  background: linear-gradient(130deg, #262518, #0b131d);
}
.forge-card:focus-visible {
  outline: 2px solid var(--ui-color-focus);
  outline-offset: 2px;
}
.forge-card:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
article.forge-card {
  cursor: default;
}
.forge-card__icon {
  display: grid;
  width: 64px;
  height: 64px;
  overflow: hidden;
  border-radius: var(--ui-radius-sm);
}
.forge-card__copy {
  display: grid;
  min-width: 0;
  gap: 5px;
}
.forge-card__copy strong {
  display: -webkit-box;
  min-height: 2.4em;
  overflow: hidden;
  overflow-wrap: anywhere;
  font-family: var(--ui-font-display);
  font-size: 1rem;
  line-height: 1.2;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}
.forge-card__copy > span,
.forge-card__copy > small {
  font-size: 0.75rem;
  color: var(--ui-color-text-secondary);
}
.forge-card__power {
  display: grid;
  gap: 3px;
  text-align: right;
}
.forge-card__power small {
  color: var(--ui-color-text-muted);
  font-size: 0.7rem;
}
.forge-card__power b {
  font-size: 1.05rem;
}
.forge-card__check {
  display: grid;
  width: 22px;
  height: 22px;
  border: 1px solid var(--ui-color-gold-muted);
  border-radius: 4px;
  color: var(--ui-color-gold);
  place-items: center;
}
[data-rarity='Epic'] {
  color: #c08bea;
}
[data-rarity='Legendary'] {
  color: #eba942;
}
[data-rarity='Unique'] {
  color: #ec756c;
}
[data-rarity='Rare'] {
  color: #80b9ed;
}
[data-rarity='Uncommon'] {
  color: #82c990;
}
@media (max-width: 400px) {
  .forge-card {
    gap: 8px;
    padding: 10px;
  }
  .forge-card__icon {
    width: 50px;
    height: 50px;
  }
  .forge-card__copy strong {
    font-size: 0.88rem;
  }
}
</style>
