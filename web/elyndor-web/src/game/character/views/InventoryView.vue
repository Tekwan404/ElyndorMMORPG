<script setup lang="ts">
import { computed, ref } from 'vue'

import type { EquipmentSlot, InventoryItem } from '@/api/contracts'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UILoadingState, UIModal, UIPanel } from '@/ui/components'

const BAG_CAPACITY = 40
const session = useGameSessionStore()
const character = computed(() => session.snapshot?.character)
const inventory = computed(() => character.value?.inventory)
const selectedItem = ref<InventoryItem | null>(null)

const slots: readonly { id: EquipmentSlot; key: 'weapon' | 'head' | 'chest'; label: string; glyph: string }[] = [
  { id: 'Head', key: 'head', label: 'Голова', glyph: 'Ш' },
  { id: 'Weapon', key: 'weapon', label: 'Оружие', glyph: 'М' },
  { id: 'Chest', key: 'chest', label: 'Нагрудник', glyph: 'Д' },
]

const bagItems = computed(() => inventory.value?.items.filter((item) => !item.equippedSlot) ?? [])
const bagCells = computed(() => Array.from({ length: BAG_CAPACITY }, (_, index) => bagItems.value[index] ?? null))
const usedSlots = computed(() => bagItems.value.length)

function equippedFor(key: 'weapon' | 'head' | 'chest'): InventoryItem | null {
  return inventory.value?.equipped[key] ?? null
}

function statRows(item: InventoryItem): string[] {
  return [
    item.stats.strength ? `Сила +${item.stats.strength}` : '',
    item.stats.agility ? `Ловкость +${item.stats.agility}` : '',
    item.stats.intellect ? `Интеллект +${item.stats.intellect}` : '',
    item.stats.stamina ? `Выносливость +${item.stats.stamina}` : '',
  ].filter(Boolean)
}

function rarityLabel(item: InventoryItem): string {
  if (item.rarity === 'Rare') return 'Редкий'
  if (item.rarity === 'Uncommon') return 'Необычный'
  return 'Обычный'
}

function typeLabel(item: InventoryItem): string {
  if (item.type === 'Material') return 'Материал'
  return item.slot === 'Weapon' ? 'Оружие' : item.slot === 'Head' ? 'Голова' : 'Нагрудник'
}

function itemGlyph(item: InventoryItem): string {
  if (item.type === 'Material') return '◆'
  if (item.slot === 'Weapon') return '⚔'
  if (item.slot === 'Head') return '◈'
  return '⬟'
}

async function equipSelected(): Promise<void> {
  const item = selectedItem.value
  if (!item || item.type !== 'Equipment') return
  await session.equip(item.id)
  selectedItem.value = session.snapshot?.character?.inventory.items.find((candidate) => candidate.id === item.id) ?? null
}

async function unequipSelected(): Promise<void> {
  const item = selectedItem.value
  if (!item?.equippedSlot) return
  await session.unequip(item.equippedSlot)
  selectedItem.value = session.snapshot?.character?.inventory.items.find((candidate) => candidate.id === item.id) ?? null
}
</script>

<template>
  <section class="inventory-view">
    <header class="inventory-header">
      <div><p>Герой</p><h1>Инвентарь</h1></div>
      <small>{{ usedSlots }} / {{ BAG_CAPACITY }}</small>
    </header>

    <UIPanel v-if="inventory" class="equipment-panel">
      <template #title>Экипировка</template>
      <div class="equipment-layout">
        <button
          v-for="slot in slots"
          :key="slot.id"
          class="equipment-slot"
          :class="{ 'equipment-slot--filled': equippedFor(slot.key) }"
          type="button"
          :aria-label="equippedFor(slot.key) ? `${slot.label}: ${equippedFor(slot.key)?.name}` : `${slot.label}: пусто`"
          @click="selectedItem = equippedFor(slot.key)"
        >
          <small>{{ slot.label }}</small>
          <span class="slot-icon">{{ equippedFor(slot.key) ? itemGlyph(equippedFor(slot.key)!) : slot.glyph }}</span>
          <strong v-if="equippedFor(slot.key)">{{ equippedFor(slot.key)?.name }}</strong>
          <span v-else class="empty">Пусто</span>
        </button>
      </div>
      <p class="equipment-hint">Нажмите на надетый предмет, чтобы посмотреть характеристики или снять его.</p>
    </UIPanel>

    <UIPanel v-if="inventory" class="bag-panel">
      <template #title>Рюкзак · {{ usedSlots }} / {{ BAG_CAPACITY }}</template>
      <div v-if="bagItems.length" class="bag-grid" aria-label="Ячейки рюкзака">
        <button
          v-for="(item, index) in bagCells"
          :key="item?.id ?? `empty-${index}`"
          class="bag-cell"
          :class="{ 'bag-cell--empty': !item }"
          :data-rarity="item?.rarity"
          type="button"
          :disabled="!item"
          :aria-label="item?.name ?? 'Пустая ячейка'"
          @click="selectedItem = item"
        >
          <template v-if="item">
            <span class="bag-cell__icon">{{ itemGlyph(item) }}</span>
            <b v-if="item.quantity > 1" class="bag-cell__quantity">{{ item.quantity }}</b>
          </template>
        </button>
      </div>
      <UILoadingState
        v-else
        state="empty"
        title="Рюкзак пуст"
        message="Побеждайте противников в Шепчущем лесу, чтобы получить материалы и экипировку."
      />
    </UIPanel>

    <UIModal :open="selectedItem !== null" :title="selectedItem?.name ?? ''" @close="selectedItem = null">
      <article v-if="selectedItem" class="item-detail" :data-rarity="selectedItem.rarity">
        <div class="item-detail__identity">
          <span class="item-detail__icon">{{ itemGlyph(selectedItem) }}</span>
          <div>
            <p>{{ rarityLabel(selectedItem) }} · {{ typeLabel(selectedItem) }}</p>
            <strong v-if="selectedItem.quantity > 1">Количество: {{ selectedItem.quantity }}</strong>
            <strong v-else-if="selectedItem.equippedSlot">Надето</strong>
            <strong v-else>В рюкзаке</strong>
          </div>
        </div>
        <p v-if="selectedItem.description" class="item-detail__description">{{ selectedItem.description }}</p>
        <dl v-if="statRows(selectedItem).length">
          <div v-for="row in statRows(selectedItem)" :key="row"><dt>{{ row }}</dt></div>
        </dl>
        <p v-if="selectedItem.type === 'Equipment'" class="item-detail__requirement">
          Требуемый уровень: {{ selectedItem.requiredLevel }}
        </p>
      </article>
      <template #actions>
        <UIButton
          v-if="selectedItem?.type === 'Equipment' && !selectedItem.equippedSlot"
          :loading="session.mutationPending"
          :disabled="session.mutationPending || (character?.level ?? 0) < selectedItem.requiredLevel"
          @click="equipSelected"
        >
          Надеть
        </UIButton>
        <UIButton
          v-else-if="selectedItem?.equippedSlot"
          variant="secondary"
          :loading="session.mutationPending"
          :disabled="session.mutationPending"
          @click="unequipSelected"
        >
          Снять
        </UIButton>
      </template>
    </UIModal>
  </section>
</template>

<style scoped>
.inventory-view { display: grid; gap: var(--ui-space-4); padding: var(--ui-space-5) var(--ui-space-4) var(--ui-space-7); }
.inventory-header { display: flex; align-items: end; justify-content: space-between; gap: var(--ui-space-3); }
.inventory-header p, .inventory-header h1 { margin: 0; }
.inventory-header p { color: var(--ui-color-primary); font-size: var(--ui-font-size-xs); letter-spacing: .1em; text-transform: uppercase; }
.inventory-header h1 { font-family: var(--ui-font-display); }
.inventory-header small { color: var(--ui-color-text-muted); }
.equipment-layout { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: var(--ui-space-2); }
.equipment-slot { display: grid; min-width: 0; min-height: 8.25rem; align-content: center; justify-items: center; gap: var(--ui-space-1); padding: var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: var(--ui-color-surface-2); color: var(--ui-color-text-muted); font: inherit; text-align: center; }
.equipment-slot--filled { border-color: var(--ui-color-primary); background: var(--ui-color-surface-3); color: var(--ui-color-text-primary); }
.equipment-slot small { font-size: var(--ui-font-size-xs); text-transform: uppercase; }
.equipment-slot strong { display: -webkit-box; overflow: hidden; color: var(--ui-color-text-primary); font-size: var(--ui-font-size-xs); -webkit-box-orient: vertical; -webkit-line-clamp: 2; }
.slot-icon { display: grid; width: 3.25rem; height: 3.25rem; place-items: center; border: 1px solid var(--ui-color-border-strong); border-radius: var(--ui-radius-md); background: var(--ui-color-background); color: var(--ui-color-primary); font-size: 1.45rem; }
.empty { font-size: var(--ui-font-size-xs); }
.equipment-hint { margin: var(--ui-space-3) 0 0; color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.bag-grid { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: var(--ui-space-2); }
.bag-cell { position: relative; aspect-ratio: 1; min-width: 0; border: 1px solid var(--ui-color-border-strong); border-radius: var(--ui-radius-md); background: var(--ui-color-surface-2); color: var(--ui-color-text-primary); font: inherit; }
.bag-cell[data-rarity='Uncommon'] { border-color: var(--ui-color-success); }
.bag-cell[data-rarity='Rare'] { border-color: var(--ui-color-primary); box-shadow: inset 0 0 1rem rgb(92 110 255 / 10%); }
.bag-cell--empty { opacity: .42; }
.bag-cell__icon { display: grid; height: 100%; place-items: center; font-size: 1.4rem; }
.bag-cell__quantity { position: absolute; right: 3px; bottom: 2px; min-width: 1rem; padding: 0 3px; border-radius: var(--ui-radius-sm); background: rgb(6 9 18 / 88%); color: white; font-size: .68rem; font-variant-numeric: tabular-nums; }
.item-detail { display: grid; gap: var(--ui-space-4); }
.item-detail__identity { display: flex; align-items: center; gap: var(--ui-space-3); }
.item-detail__icon { display: grid; width: 4rem; height: 4rem; flex: 0 0 auto; place-items: center; border: 1px solid var(--ui-color-border-strong); border-radius: var(--ui-radius-md); background: var(--ui-color-surface-2); color: var(--ui-color-primary); font-size: 1.8rem; }
.item-detail__identity p, .item-detail__description, .item-detail__requirement { margin: 0; }
.item-detail__identity p, .item-detail__description, .item-detail__requirement { color: var(--ui-color-text-muted); }
.item-detail__identity div { display: grid; gap: var(--ui-space-1); }
.item-detail dl { display: grid; gap: var(--ui-space-1); margin: 0; padding: var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); }
.item-detail dl div { color: var(--ui-color-success); }
.item-detail dt { font-weight: var(--ui-font-weight-semibold); }
@media (max-width: 360px) {
  .inventory-view { padding-inline: var(--ui-space-3); }
  .equipment-layout { grid-template-columns: repeat(3, minmax(0, 1fr)); }
  .equipment-slot { min-height: 7rem; padding-inline: var(--ui-space-1); }
  .slot-icon { width: 2.75rem; height: 2.75rem; }
  .bag-grid { gap: var(--ui-space-1); }
}
</style>
