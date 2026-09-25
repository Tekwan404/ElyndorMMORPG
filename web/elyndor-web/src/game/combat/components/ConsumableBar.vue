<script setup lang="ts">
import type { InventoryItem } from '@/api/contracts'
import ItemIcon from '@/game/items/components/ItemIcon.vue'

const props = defineProps<{
  items: readonly InventoryItem[]
  cooldownRemaining: (item: InventoryItem) => number
  canUse: (item: InventoryItem) => boolean
  disabled: boolean
}>()
const emit = defineEmits<{ use: [item: InventoryItem] }>()

function available(item: InventoryItem): boolean {
  return !props.disabled && props.cooldownRemaining(item) <= 0 && props.canUse(item)
}
</script>

<template>
  <section v-if="items.length" class="consumable-bar" aria-label="Расходники" data-consumable-bar>
    <strong>Расходники</strong>
    <div class="consumable-bar__items">
      <button
        v-for="item in items"
        :key="item.definitionId"
        type="button"
        :data-combat-consumable="item.definitionId"
        :disabled="!available(item)"
        :aria-label="`${item.name}, ${item.quantity} шт.`"
        @click="emit('use', item)"
      >
        <ItemIcon :icon-id="item.iconId" :item-id="item.definitionId" :name="item.name" :type="item.type" :rarity="item.rarity" decorative loading="eager" />
        <span><small>{{ item.name }}</small><b>×{{ item.quantity }}</b></span>
        <em v-if="cooldownRemaining(item) > 0">{{ Math.ceil(cooldownRemaining(item) / 1000) }}с</em>
      </button>
    </div>
  </section>
</template>

<style scoped>
.consumable-bar { display: flex; min-width: 0; align-items: center; gap: .45rem; }
.consumable-bar > strong { flex: none; color: #9db9ae; font-size: .5rem; }
.consumable-bar__items { display: flex; min-width: 0; gap: .3rem; overflow-x: auto; scrollbar-width: none; }
.consumable-bar__items::-webkit-scrollbar { display: none; }
.consumable-bar button { position: relative; display: grid; min-width: 6.3rem; min-height: 44px; flex: none; grid-template-columns: 34px 1fr; align-items: center; gap: .3rem; padding: .24rem .38rem; border: 1px solid rgb(82 171 143 / 30%); border-radius: 7px; background: rgb(5 14 15 / 76%); color: #e3e0d8; font: inherit; text-align: left; }
.consumable-bar button:disabled { filter: grayscale(.65); opacity: .5; }
.consumable-bar button :deep(.item-icon) { width: 34px; height: 34px; }
.consumable-bar button span { display: grid; min-width: 0; }
.consumable-bar button small { overflow: hidden; font-size: .46rem; text-overflow: ellipsis; white-space: nowrap; }
.consumable-bar button b { color: #91d7bd; font-size: .52rem; }
.consumable-bar button em { position: absolute; inset: 0; display: grid; place-items: center; border-radius: inherit; background: rgb(2 7 8 / 82%); color: white; font-size: .7rem; font-style: normal; }
</style>
