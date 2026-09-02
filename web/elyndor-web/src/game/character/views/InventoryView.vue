<script setup lang="ts">
import { computed, ref } from 'vue'

import type { InventoryItem } from '@/api/contracts'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UILoadingState, UIModal, UIPanel } from '@/ui/components'

const BAG_CAPACITY = 40
const session = useGameSessionStore()
const character = computed(() => session.snapshot?.character)
const inventory = computed(() => character.value?.inventory)
const selectedItem = ref<InventoryItem | null>(null)
const typeFilter = ref<'all' | 'equipment' | 'material'>('all')
const rarityFilter = ref<'all' | 'Common' | 'Uncommon' | 'Rare'>('all')

const bagItems = computed(() => inventory.value?.items.filter((item) => !item.equippedSlot) ?? [])
const filteredItems = computed(() => bagItems.value.filter((item) => {
  const typeMatches = typeFilter.value === 'all'
    || (typeFilter.value === 'equipment' && item.type === 'Equipment')
    || (typeFilter.value === 'material' && item.type === 'Material')
  const rarityMatches = rarityFilter.value === 'all' || item.rarity === rarityFilter.value
  return typeMatches && rarityMatches
}))
const visibleCells = computed(() => {
  if (typeFilter.value !== 'all' || rarityFilter.value !== 'all') return filteredItems.value
  return Array.from({ length: BAG_CAPACITY }, (_, index) => bagItems.value[index] ?? null)
})
const usedSlots = computed(() => bagItems.value.length)

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
  return item.slot === 'Weapon' ? 'Оружие' : item.slot === 'Head' ? 'Головной убор' : 'Нагрудник'
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
  selectedItem.value = null
}
</script>

<template>
  <section class="inventory-view">
    <header class="inventory-header">
      <div>
        <p>Инвентарь</p>
        <h1>Рюкзак</h1>
        <small>Здесь хранятся добытые материалы и предметы, которые сейчас не надеты.</small>
      </div>
      <b>{{ usedSlots }} / {{ BAG_CAPACITY }}</b>
    </header>

    <UIPanel v-if="inventory" class="filters-panel">
      <template #title>Фильтры</template>
      <div class="filter-group">
        <small>Тип предмета</small>
        <div class="filter-chips">
          <button type="button" :class="{ active: typeFilter === 'all' }" @click="typeFilter = 'all'">Все</button>
          <button type="button" :class="{ active: typeFilter === 'equipment' }" @click="typeFilter = 'equipment'">Снаряжение</button>
          <button type="button" :class="{ active: typeFilter === 'material' }" @click="typeFilter = 'material'">Материалы</button>
        </div>
      </div>
      <div class="filter-group">
        <small>Редкость</small>
        <div class="filter-chips filter-chips--rarity">
          <button type="button" :class="{ active: rarityFilter === 'all' }" @click="rarityFilter = 'all'">Любая</button>
          <button type="button" :class="{ active: rarityFilter === 'Common' }" @click="rarityFilter = 'Common'">Обычная</button>
          <button type="button" :class="{ active: rarityFilter === 'Uncommon' }" @click="rarityFilter = 'Uncommon'">Необычная</button>
          <button type="button" :class="{ active: rarityFilter === 'Rare' }" @click="rarityFilter = 'Rare'">Редкая</button>
        </div>
      </div>
    </UIPanel>

    <UIPanel v-if="inventory" class="bag-panel">
      <template #title>
        {{ typeFilter === 'all' && rarityFilter === 'all' ? `Ячейки рюкзака · ${usedSlots} / ${BAG_CAPACITY}` : `Найдено предметов: ${filteredItems.length}` }}
      </template>
      <div v-if="visibleCells.length && (filteredItems.length || (typeFilter === 'all' && rarityFilter === 'all'))" class="bag-grid" aria-label="Содержимое рюкзака">
        <button
          v-for="(item, index) in visibleCells"
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
            <i v-if="item.type === 'Equipment'" class="bag-cell__type">СН</i>
          </template>
        </button>
      </div>
      <UILoadingState
        v-else-if="bagItems.length === 0"
        state="empty"
        title="Рюкзак пуст"
        message="Побеждайте противников в Шепчущем лесу, чтобы находить материалы и снаряжение."
      />
      <UILoadingState
        v-else
        state="empty"
        title="Ничего не найдено"
        message="Попробуйте изменить выбранные фильтры."
      />
    </UIPanel>

    <UIModal :open="selectedItem !== null" :title="selectedItem?.name ?? ''" @close="selectedItem = null">
      <article v-if="selectedItem" class="item-detail" :data-rarity="selectedItem.rarity">
        <div class="item-detail__identity">
          <span class="item-detail__icon">{{ itemGlyph(selectedItem) }}</span>
          <div>
            <p>{{ rarityLabel(selectedItem) }} · {{ typeLabel(selectedItem) }}</p>
            <strong v-if="selectedItem.quantity > 1">Количество: {{ selectedItem.quantity }}</strong>
            <strong v-else>В рюкзаке</strong>
          </div>
        </div>
        <p v-if="selectedItem.description" class="item-detail__description">{{ selectedItem.description }}</p>
        <dl v-if="statRows(selectedItem).length">
          <div v-for="row in statRows(selectedItem)" :key="row"><dt>{{ row }}</dt></div>
        </dl>
        <p v-if="selectedItem.type === 'Material'" class="item-detail__hint">Материал сохранён для будущей системы ремёсел и создания предметов.</p>
        <p v-if="selectedItem.type === 'Equipment'" class="item-detail__requirement">
          Требуемый уровень: {{ selectedItem.requiredLevel }}
        </p>
      </article>
      <template #actions>
        <UIButton
          v-if="selectedItem?.type === 'Equipment'"
          :loading="session.mutationPending"
          :disabled="session.mutationPending || (character?.level ?? 0) < selectedItem.requiredLevel"
          @click="equipSelected"
        >
          Надеть
        </UIButton>
      </template>
    </UIModal>
  </section>
</template>

<style scoped>
.inventory-view { display: grid; width: min(100%, var(--ui-content-width)); margin-inline: auto; gap: var(--ui-space-4); padding: var(--ui-space-5) var(--ui-space-4) var(--ui-space-7); }
.inventory-header { display: flex; align-items: end; justify-content: space-between; gap: var(--ui-space-3); }
.inventory-header > div { display: grid; gap: var(--ui-space-1); }
.inventory-header p, .inventory-header h1, .inventory-header small { margin: 0; }
.inventory-header p { color: var(--ui-color-primary); font-size: var(--ui-font-size-xs); font-weight: var(--ui-font-weight-bold); letter-spacing: .1em; text-transform: uppercase; }
.inventory-header h1 { font-family: var(--ui-font-display); }
.inventory-header small { max-width: 24rem; color: var(--ui-color-text-muted); line-height: var(--ui-line-height-normal); }
.inventory-header > b { flex: 0 0 auto; color: var(--ui-color-primary); font-variant-numeric: tabular-nums; }
.filters-panel :deep(.ui-panel__body) { display: grid; gap: var(--ui-space-3); }
.filter-group { display: grid; gap: var(--ui-space-2); }
.filter-group > small { color: var(--ui-color-text-muted); }
.filter-chips { display: flex; flex-wrap: wrap; gap: var(--ui-space-2); }
.filter-chips button { min-height: 2.25rem; padding: 0 var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-round); background: var(--ui-color-surface-2); color: var(--ui-color-text-muted); font: inherit; font-size: var(--ui-font-size-xs); }
.filter-chips button.active { border-color: var(--ui-color-primary); background: rgb(105 93 255 / 12%); color: var(--ui-color-text-primary); }
.bag-grid { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: var(--ui-space-2); }
.bag-cell { position: relative; aspect-ratio: 1; min-width: 0; border: 1px solid var(--ui-color-border-strong); border-radius: var(--ui-radius-md); background: var(--ui-color-surface-2); color: var(--ui-color-text-primary); font: inherit; }
.bag-cell[data-rarity='Uncommon'] { border-color: var(--ui-color-success); }
.bag-cell[data-rarity='Rare'] { border-color: var(--ui-color-primary); box-shadow: inset 0 0 1rem rgb(92 110 255 / 10%); }
.bag-cell--empty { opacity: .32; }
.bag-cell__icon { display: grid; height: 100%; place-items: center; font-size: 1.4rem; }
.bag-cell__quantity { position: absolute; right: 3px; bottom: 2px; min-width: 1rem; padding: 0 3px; border-radius: var(--ui-radius-sm); background: rgb(6 9 18 / 88%); color: white; font-size: .68rem; font-variant-numeric: tabular-nums; }
.bag-cell__type { position: absolute; top: 3px; left: 3px; padding: 1px 3px; border-radius: var(--ui-radius-sm); background: rgb(105 93 255 / 20%); color: var(--ui-color-primary); font-size: .55rem; font-style: normal; font-weight: 700; }
.item-detail { display: grid; gap: var(--ui-space-4); }
.item-detail__identity { display: flex; align-items: center; gap: var(--ui-space-3); }
.item-detail__icon { display: grid; width: 4rem; height: 4rem; flex: 0 0 auto; place-items: center; border: 1px solid var(--ui-color-border-strong); border-radius: var(--ui-radius-md); background: var(--ui-color-surface-2); color: var(--ui-color-primary); font-size: 1.8rem; }
.item-detail__identity p, .item-detail__description, .item-detail__requirement, .item-detail__hint { margin: 0; }
.item-detail__identity p, .item-detail__description, .item-detail__requirement, .item-detail__hint { color: var(--ui-color-text-muted); line-height: var(--ui-line-height-normal); }
.item-detail__identity div { display: grid; gap: var(--ui-space-1); }
.item-detail dl { display: grid; gap: var(--ui-space-1); margin: 0; padding: var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); }
.item-detail dl div { color: var(--ui-color-success); }
.item-detail dt { font-weight: var(--ui-font-weight-semibold); }
.item-detail__hint { padding: var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: var(--ui-color-surface-2); font-size: var(--ui-font-size-sm); }
@media (max-width: 360px) { .inventory-view { padding-inline: var(--ui-space-3); } .filter-chips { gap: var(--ui-space-1); } .filter-chips button { padding-inline: var(--ui-space-2); } .bag-grid { gap: var(--ui-space-1); } }
</style>
