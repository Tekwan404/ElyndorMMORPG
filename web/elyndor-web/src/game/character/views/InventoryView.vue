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
const typeFilter = ref<'all' | 'equipment' | 'material' | 'consumable'>('all')
const rarityFilter = ref<'all' | InventoryItem['rarity']>('all')

const bagItems = computed(() => inventory.value?.items.filter((item) => !item.equippedSlot) ?? [])
const filteredItems = computed(() => bagItems.value.filter((item) => {
  const typeMatches = typeFilter.value === 'all'
    || (typeFilter.value === 'equipment' && item.type === 'Equipment')
    || (typeFilter.value === 'material' && item.type === 'Material')
    || (typeFilter.value === 'consumable' && item.type === 'Consumable')
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
    item.stats.maxHp ? `Макс. здоровье +${item.stats.maxHp}` : '',
    item.stats.attackPower ? `Сила атаки +${item.stats.attackPower}` : '',
    item.stats.spellPower ? `Сила заклинаний +${item.stats.spellPower}` : '',
    item.stats.criticalChance ? `Крит. шанс +${item.stats.criticalChance}%` : '',
    item.stats.criticalDamage ? `Крит. урон +${item.stats.criticalDamage}%` : '',
    item.stats.accuracy ? `Точность +${item.stats.accuracy}%` : '',
    item.stats.attackSpeed ? `Скорость атаки +${item.stats.attackSpeed}%` : '',
    item.stats.armor ? `Броня +${item.stats.armor}` : '',
    item.stats.magicResistance ? `Сопротивление магии +${item.stats.magicResistance}` : '',
    item.stats.dodge ? `Уклонение +${item.stats.dodge}%` : '',
    item.stats.armorPenetration ? `Пробивание брони +${item.stats.armorPenetration}%` : '',
    item.stats.magicPenetration ? `Пробивание магии +${item.stats.magicPenetration}%` : '',
    item.stats.maxResource ? `Макс. ресурс +${item.stats.maxResource}` : '',
  ].filter(Boolean)
}

function rarityLabel(item: InventoryItem): string {
  if (item.rarity === 'Unique') return 'Уникальный'
  if (item.rarity === 'Legendary') return 'Легендарный'
  if (item.rarity === 'Epic') return 'Эпический'
  if (item.rarity === 'Rare') return 'Редкий'
  if (item.rarity === 'Uncommon') return 'Необычный'
  return 'Обычный'
}

function typeLabel(item: InventoryItem): string {
  if (item.type === 'Material') return 'Материал'
  if (item.type === 'Consumable') return 'Расходник'
  const labels: Record<string, string> = {
    MainHand: 'Основная рука', OffHand: 'Вторая рука', Weapon: 'Оружие',
    Head: 'Шлем', Chest: 'Нагрудник', Hands: 'Перчатки', Legs: 'Штаны',
    Feet: 'Обувь', Boots: 'Ботинки', Cloak: 'Плащ', Amulet: 'Амулет',
    Ring1: 'Кольцо', Ring2: 'Кольцо', Accessory: 'Аксессуар',
  }
  return item.slot ? labels[item.slot] ?? 'Снаряжение' : 'Снаряжение'
}

function itemGlyph(item: InventoryItem): string {
  if (item.type === 'Material') return '◆'
  if (item.type === 'Consumable') return '✚'
  if (item.slot === 'Weapon' || item.slot === 'MainHand' || item.slot === 'OffHand') return '⚔'
  if (item.slot === 'Head') return '◈'
  if (item.slot === 'Chest') return '⬟'
  if (item.slot === 'Legs') return '▥'
  if (item.slot === 'Boots' || item.slot === 'Feet') return '⌁'
  if (item.slot === 'Hands') return '◫'
  if (item.slot === 'Cloak') return '◒'
  if (item.slot === 'Amulet' || item.slot === 'Ring1' || item.slot === 'Ring2') return '✧'
  return '✦'
}

async function equipSelected(): Promise<void> {
  const item = selectedItem.value
  if (!item || item.type !== 'Equipment') return
  await session.equip(item.id)
  selectedItem.value = null
}

async function useSelected(): Promise<void> {
  const item = selectedItem.value
  if (!item || item.type !== 'Consumable') return
  await session.useConsumable(item.id)
  selectedItem.value = null
}
</script>

<template>
  <section class="inventory-view">
    <header class="inventory-header">
      <div>
        <p>Инвентарь</p>
        <h1>Рюкзак</h1>
        <small>Добыча, расходники и снаряжение, которое сейчас не надето.</small>
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
          <button type="button" :class="{ active: typeFilter === 'consumable' }" @click="typeFilter = 'consumable'">Расходники</button>
          <button type="button" :class="{ active: typeFilter === 'material' }" @click="typeFilter = 'material'">Материалы</button>
        </div>
      </div>
      <div class="filter-group">
        <small>Редкость</small>
        <div class="filter-chips">
          <button type="button" :class="{ active: rarityFilter === 'all' }" @click="rarityFilter = 'all'">Любая</button>
          <button type="button" :class="{ active: rarityFilter === 'Common' }" @click="rarityFilter = 'Common'">Обычная</button>
          <button type="button" :class="{ active: rarityFilter === 'Uncommon' }" @click="rarityFilter = 'Uncommon'">Необычная</button>
          <button type="button" :class="{ active: rarityFilter === 'Rare' }" @click="rarityFilter = 'Rare'">Редкая</button>
          <button type="button" :class="{ active: rarityFilter === 'Epic' }" @click="rarityFilter = 'Epic'">Эпическая</button>
          <button type="button" :class="{ active: rarityFilter === 'Legendary' }" @click="rarityFilter = 'Legendary'">Легендарная</button>
          <button type="button" :class="{ active: rarityFilter === 'Unique' }" @click="rarityFilter = 'Unique'">Уникальная</button>
        </div>
      </div>
    </UIPanel>

    <UIPanel v-if="inventory" class="bag-panel">
      <template #title>{{ typeFilter === 'all' && rarityFilter === 'all' ? `Ячейки рюкзака · ${usedSlots} / ${BAG_CAPACITY}` : `Найдено: ${filteredItems.length}` }}</template>
      <div v-if="bagItems.length > 0 && visibleCells.length && (filteredItems.length || (typeFilter === 'all' && rarityFilter === 'all'))" class="bag-grid">
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
          </template>
        </button>
      </div>
      <UILoadingState v-else-if="bagItems.length === 0" state="empty" title="Рюкзак пуст" message="Исследуйте мир и побеждайте противников, чтобы находить добычу." />
      <UILoadingState v-else state="empty" title="Ничего не найдено" message="Измените выбранные фильтры." />
    </UIPanel>

    <UIModal :open="selectedItem !== null" :title="selectedItem?.name ?? ''" @close="selectedItem = null">
      <article v-if="selectedItem" class="item-detail">
        <div class="item-detail__identity">
          <span class="item-detail__icon">{{ itemGlyph(selectedItem) }}</span>
          <div>
            <p>{{ rarityLabel(selectedItem) }} · {{ typeLabel(selectedItem) }}</p>
            <strong>Количество: {{ selectedItem.quantity }}</strong>
          </div>
        </div>
        <p class="item-detail__description">{{ selectedItem.description }}</p>
        <dl v-if="statRows(selectedItem).length">
          <div v-for="row in statRows(selectedItem)" :key="row"><dt>{{ row }}</dt></div>
        </dl>
        <p v-if="selectedItem.weaponBaseAttackIntervalSeconds" class="item-detail__hint">Базовый интервал автоатаки: {{ selectedItem.weaponBaseAttackIntervalSeconds }} сек.</p>
        <p v-if="selectedItem.setId" class="item-detail__hint">Часть комплекта Следопыта. Бонусы активируются за 3 и 6 надетых предметов.</p>
        <p v-if="selectedItem.type === 'Material'" class="item-detail__hint">Можно сохранить для ремесла или продать Маркусу за {{ selectedItem.sellPriceGold }} золота за штуку.</p>
        <p v-if="selectedItem.type === 'Consumable'" class="item-detail__hint">Восстанавливает {{ selectedItem.healAmount }} здоровья. В бою общий кулдаун зелий — {{ selectedItem.consumableCooldownSeconds }} сек.</p>
      </article>
      <template #actions>
        <UIButton v-if="selectedItem?.type === 'Equipment'" :loading="session.mutationPending" :disabled="session.mutationPending || (character?.level ?? 0) < selectedItem.requiredLevel" @click="equipSelected">Надеть</UIButton>
        <UIButton v-if="selectedItem?.type === 'Consumable'" :loading="session.mutationPending" :disabled="session.mutationPending || (character?.vitals.currentHp ?? 0) >= (character?.vitals.maxHp ?? 0)" @click="useSelected">Использовать</UIButton>
      </template>
    </UIModal>
  </section>
</template>

<style scoped>
.inventory-view { display:grid; width:min(100%,var(--ui-content-width)); margin-inline:auto; gap:var(--ui-space-4); padding:var(--ui-space-5) var(--ui-space-4) var(--ui-space-7); }
.inventory-header { display:flex; align-items:end; justify-content:space-between; gap:var(--ui-space-3); }
.inventory-header div,.filter-group,.item-detail { display:grid; gap:var(--ui-space-2); }
.inventory-header p,.inventory-header h1,.inventory-header small,.item-detail p { margin:0; }
.inventory-header p { color:var(--ui-color-primary); font-size:var(--ui-font-size-xs); font-weight:700; letter-spacing:.1em; text-transform:uppercase; }
.inventory-header h1 { font-family:var(--ui-font-display); }
.inventory-header small,.filter-group small,.item-detail p { color:var(--ui-color-text-muted); }
.filter-chips { display:flex; flex-wrap:wrap; gap:var(--ui-space-2); }
.filter-chips button { min-height:2.25rem; padding:0 var(--ui-space-3); border:1px solid var(--ui-color-border); border-radius:var(--ui-radius-round); background:var(--ui-color-surface-2); color:var(--ui-color-text-muted); font:inherit; font-size:var(--ui-font-size-xs); }
.filter-chips button.active { border-color:var(--ui-color-primary); color:var(--ui-color-text-primary); }
.bag-grid { display:grid; grid-template-columns:repeat(5,minmax(0,1fr)); gap:var(--ui-space-2); }
.bag-cell { position:relative; aspect-ratio:1; border:1px solid var(--ui-color-border-strong); border-radius:var(--ui-radius-md); background:var(--ui-color-surface-2); color:var(--ui-color-text-primary); }
.bag-cell[data-rarity='Uncommon'] { border-color:var(--ui-color-success); }
.bag-cell[data-rarity='Rare'] { border-color:var(--ui-color-primary); }
.bag-cell[data-rarity='Epic'], .bag-cell[data-rarity='Legendary'], .bag-cell[data-rarity='Unique'] { border-color:var(--ui-color-accent, var(--ui-color-primary)); }
.bag-cell--empty { opacity:.28; }
.bag-cell__icon { display:grid; height:100%; place-items:center; font-size:1.45rem; }
.bag-cell__quantity { position:absolute; right:3px; bottom:2px; padding:0 4px; border-radius:4px; background:#080b14dd; color:white; font-size:.7rem; }
.item-detail__identity { display:flex; align-items:center; gap:var(--ui-space-3); }
.item-detail__icon { display:grid; width:4rem; height:4rem; place-items:center; border:1px solid var(--ui-color-border-strong); border-radius:var(--ui-radius-md); background:var(--ui-color-surface-2); color:var(--ui-color-primary); font-size:1.8rem; }
.item-detail dl { display:grid; gap:var(--ui-space-1); margin:0; padding:var(--ui-space-3); border:1px solid var(--ui-color-border); border-radius:var(--ui-radius-md); }
.item-detail dl div { color:var(--ui-color-success); }
.item-detail__hint { padding:var(--ui-space-3); border:1px solid var(--ui-color-border); border-radius:var(--ui-radius-md); background:var(--ui-color-surface-2); font-size:var(--ui-font-size-sm); }
@media (max-width:360px) { .inventory-view{padding-inline:var(--ui-space-3)} .bag-grid{gap:var(--ui-space-1)} }
</style>
