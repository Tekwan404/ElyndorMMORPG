<script setup lang="ts">
import { useId } from 'vue'
import {
  inventoryCategories,
  type InventoryCategory,
  type InventorySort,
} from '../inventoryOrganization'
import { UIButton } from '@/ui/components'

defineProps<{
  counts: Record<InventoryCategory, number>
  contextualLabel: string | null
  newCount: number
  filterCount: number
}>()
const category = defineModel<InventoryCategory>('category', { required: true })
const search = defineModel<string>('search', { required: true })
const sort = defineModel<InventorySort>('sort', { required: true })
const newOnly = defineModel<boolean>('newOnly', { required: true })
const view = defineModel<'list' | 'grid' | 'mini'>('view', { required: true })

function cycleView(): void {
  view.value = view.value === 'list' ? 'grid' : view.value === 'grid' ? 'mini' : 'list'
}

function viewLabel(): string {
  if (view.value === 'list') return 'Компактные иконки'
  if (view.value === 'grid') return 'Иконки'
  return 'Крупные иконки'
}
defineEmits<{ filters: [] }>()
const searchId = useId()
</script>

<template>
  <section class="inventory-browser" aria-label="Поиск и организация предметов">
    <div
      v-if="!contextualLabel"
      class="inventory-browser__categories"
      role="group"
      aria-label="Категории предметов"
    >
      <button
        v-for="tab in inventoryCategories"
        :key="tab.id"
        type="button"
        :data-inventory-category="tab.id"
        :aria-pressed="category === tab.id"
        @click="category = tab.id"
      >
        <span>{{ tab.label }}</span
        ><small>{{ counts[tab.id] }}</small>
      </button>
    </div>
    <p v-else class="inventory-browser__context">Выбираем: {{ contextualLabel }}</p>
    <div class="inventory-browser__search">
      <label class="sr-only" :for="searchId">Поиск по названию предмета</label>
      <input
        :id="searchId"
        v-model="search"
        data-inventory-search
        type="search"
        placeholder="Найти предмет…"
        autocomplete="off"
      />
      <button
        v-if="search"
        type="button"
        data-clear-inventory-search
        aria-label="Очистить поиск"
        @click="search = ''"
      >
        ×
      </button>
    </div>
    <div class="inventory-browser__controls">
      <label class="inventory-browser__sort">
        <span class="sr-only">Сортировка предметов</span>
        <select v-model="sort" aria-label="Сортировка предметов" data-inventory-sort>
          <option value="default">По назначению</option>
          <option value="received">Как получено</option>
          <option value="new">Новые сначала</option>
          <option value="rarity">По редкости</option>
          <option value="slot">По слоту</option>
          <option value="level">По требуемому уровню</option>
          <option value="name">По названию</option>
        </select>
      </label>
      <button
        type="button"
        class="inventory-browser__new"
        data-inventory-new-filter
        :aria-pressed="newOnly"
        @click="newOnly = !newOnly"
      >
        Новые<span v-if="newCount"> · {{ newCount }}</span>
      </button>
      <UIButton variant="secondary" data-open-inventory-filters @click="$emit('filters')"
        >Фильтры<span v-if="filterCount"> · {{ filterCount }}</span></UIButton
      >
      <button
        type="button"
        class="inventory-browser__view"
        :data-inventory-view="view"
        :aria-label="viewLabel()"
        :title="viewLabel()"
        @click="cycleView"
      >
        <span aria-hidden="true">{{ view === 'list' ? '▦' : view === 'grid' ? '▦▦' : '☷' }}</span>
      </button>
    </div>
  </section>
</template>

<style scoped>
.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}
.inventory-browser {
  display: grid;
  gap: 10px;
  min-width: 0;
}
.inventory-browser__categories {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 4px;
  padding: 4px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: var(--ui-color-surface-2);
}
.inventory-browser button {
  min-height: 44px;
  font: inherit;
  cursor: pointer;
}
.inventory-browser__categories button {
  display: grid;
  gap: 3px;
  place-content: center;
  min-width: 0;
  border: 1px solid transparent;
  border-radius: var(--ui-radius-sm);
  background: transparent;
  color: var(--ui-color-text-secondary);
  font-size: 0.75rem;
}
.inventory-browser__categories small {
  font-size: 0.7rem;
  opacity: 0.75;
}
.inventory-browser button[aria-pressed='true'] {
  border-color: var(--ui-color-gold);
  background: color-mix(in srgb, var(--ui-color-gold) 10%, transparent);
  color: var(--ui-color-gold);
}
.inventory-browser__search {
  position: relative;
}
.inventory-browser__search input {
  box-sizing: border-box;
  width: 100%;
  min-height: 44px;
  padding: 10px 44px 10px 12px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: var(--ui-color-surface-2);
  color: var(--ui-color-text-primary);
  font: inherit;
  font-size: 16px;
}
.inventory-browser__search button {
  position: absolute;
  right: 0;
  top: 0;
  width: 44px;
  border: 0;
  background: transparent;
  color: var(--ui-color-text-secondary);
  font-size: 1.5rem;
}
.inventory-browser__controls {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto auto 44px;
  align-items: center;
  gap: 6px;
}
.inventory-browser__sort {
  flex: 1;
  min-width: 0;
}
.inventory-browser__sort select {
  width: 100%;
  min-height: 44px;
  padding: 8px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  background: var(--ui-color-surface-2);
  color: var(--ui-color-text-primary);
  font: inherit;
  font-size: 0.8rem;
}
.inventory-browser .inventory-browser__new {
  padding: 6px 10px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  background: transparent;
  color: var(--ui-color-text-secondary);
  font-size: 0.8rem;
}
.inventory-browser__context {
  margin: 0;
  color: var(--ui-color-gold);
}
.inventory-browser__view {
  width: 44px;
  flex: 0 0 44px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  background: transparent;
  color: var(--ui-color-text-secondary);
  font-size: 1.3rem;
}
.inventory-browser :is(button, input, select):focus-visible {
  outline: 2px solid var(--ui-color-gold);
  outline-offset: 2px;
}
</style>
