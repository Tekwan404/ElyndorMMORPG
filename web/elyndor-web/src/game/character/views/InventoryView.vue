<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import type { EquipmentSlot, InventoryItem } from '@/api/contracts'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UILoadingState, UIModal } from '@/ui/components'

const props = defineProps<{
  slotFilter?: EquipmentSlot | null
}>()

const BAG_CAPACITY = 40
const session = useGameSessionStore()
const character = computed(() => session.snapshot?.character)
const inventory = computed(() => character.value?.inventory)
const selectedItem = ref<InventoryItem | null>(null)
const equipmentActionError = ref<string | null>(null)
const typeFilter = ref<'all' | 'equipment' | 'material' | 'consumable'>('all')
const rarityFilter = ref<'all' | InventoryItem['rarity']>('all')
const sortMode = ref<'default' | 'rarity' | 'level' | 'name'>('default')
const newItemIds = ref<Set<string>>(new Set())
const contextualSlot = computed(() => props.slotFilter ?? null)
const isContextualSlotMode = computed(() => contextualSlot.value !== null)

const bagItems = computed(() => inventory.value?.items.filter((item) => !item.equippedSlot) ?? [])
const filteredItems = computed(() => bagItems.value.filter((item) => {
  const contextualMatches = contextualSlot.value === null
    || (item.type === 'Equipment' && item.slot !== null && slotsMatch(item, contextualSlot.value))
  const typeMatches = isContextualSlotMode.value
    || typeFilter.value === 'all'
    || (typeFilter.value === 'equipment' && item.type === 'Equipment')
    || (typeFilter.value === 'material' && item.type === 'Material')
    || (typeFilter.value === 'consumable' && item.type === 'Consumable')
  const rarityMatches = rarityFilter.value === 'all' || item.rarity === rarityFilter.value
  return contextualMatches && typeMatches && rarityMatches
}))
const sortedItems = computed(() => {
  const items = [...filteredItems.value]
  if (sortMode.value === 'rarity') {
    items.sort((left, right) => rarityRank(right.rarity) - rarityRank(left.rarity)
      || left.name.localeCompare(right.name))
  } else if (sortMode.value === 'level') {
    items.sort((left, right) => right.requiredLevel - left.requiredLevel
      || rarityRank(right.rarity) - rarityRank(left.rarity))
  } else if (sortMode.value === 'name') {
    items.sort((left, right) => left.name.localeCompare(right.name))
  }
  return items
})
const visibleCells = computed(() => {
  if (isContextualSlotMode.value || typeFilter.value !== 'all' || rarityFilter.value !== 'all') return sortedItems.value
  return Array.from({ length: BAG_CAPACITY }, (_, index) => sortedItems.value[index] ?? null)
})
const usedSlots = computed(() => bagItems.value.length)
const comparisonItem = computed(() =>
  selectedItem.value?.type === 'Equipment'
    ? equippedItemForSlot(selectedItem.value)
    : null,
)
const comparisonRows = computed(() => {
  const candidate = selectedItem.value
  const equipped = comparisonItem.value
  if (!candidate || !equipped) return []

  return comparisonStats
    .map(stat => {
      const candidateValue = stat.value(candidate)
      const equippedValue = stat.value(equipped)
      const delta = candidateValue - equippedValue
      return {
        label: stat.label,
        candidateValue,
        equippedValue,
        delta,
      }
    })
    .filter(row => row.candidateValue !== 0 || row.equippedValue !== 0)
})

const comparisonStats: readonly {
  label: string
  value: (item: InventoryItem) => number
}[] = [
  { label: 'Сила', value: item => item.stats.strength },
  { label: 'Ловкость', value: item => item.stats.agility },
  { label: 'Интеллект', value: item => item.stats.intellect },
  { label: 'Выносливость', value: item => item.stats.stamina },
  { label: 'Макс. здоровье', value: item => item.stats.maxHp },
  { label: 'Сила атаки', value: item => item.stats.attackPower },
  { label: 'Сила заклинаний', value: item => item.stats.spellPower },
  { label: 'Крит. шанс', value: item => item.stats.criticalChance },
  { label: 'Броня', value: item => item.stats.armor },
  { label: 'Сопротивление магии', value: item => item.stats.magicResistance },
  { label: 'Уклонение', value: item => item.stats.dodge + item.dodgePercent },
  { label: 'Скорость атаки', value: item => item.stats.attackSpeed + item.attackSpeedPercent },
  { label: 'Макс. ресурс', value: item => item.stats.maxResource },
]

function canonicalSlot(slot: EquipmentSlot): EquipmentSlot {
  if (slot === 'Weapon') return 'MainHand'
  if (slot === 'Boots') return 'Feet'
  if (slot === 'Accessory') return 'Amulet'
  return slot
}

function isOneHandWeapon(item: InventoryItem): boolean {
  return item.type === 'Equipment'
    && item.weaponCategory !== null
    && item.weaponHandsRequired === 1
}

function slotsMatch(item: InventoryItem, requestedSlot: EquipmentSlot): boolean {
  if (!item.slot) return false
  const itemSlot = canonicalSlot(item.slot)
  const target = canonicalSlot(requestedSlot)
  if (target === 'OffHand' && itemSlot === 'MainHand' && isOneHandWeapon(item)) return true
  return itemSlot === target
}

function slotLabel(slot: EquipmentSlot | null): string {
  if (!slot) return ''
  const labels: Partial<Record<EquipmentSlot, string>> = {
    MainHand: 'основную руку',
    OffHand: 'вторую руку',
    Head: 'шлем',
    Chest: 'нагрудник',
    Hands: 'перчатки',
    Legs: 'поножи',
    Feet: 'обувь',
    Cloak: 'плащ',
    Amulet: 'амулет',
    Ring1: 'первое кольцо',
    Ring2: 'второе кольцо',
    Weapon: 'оружие',
    Boots: 'обувь',
    Accessory: 'амулет',
  }
  return labels[slot] ?? 'снаряжение'
}

function rarityRank(rarity: InventoryItem['rarity']): number {
  if (rarity === 'Unique') return 6
  if (rarity === 'Legendary') return 5
  if (rarity === 'Epic') return 4
  if (rarity === 'Rare') return 3
  if (rarity === 'Uncommon') return 2
  return 1
}

function equippedItemForSlot(item: InventoryItem): InventoryItem | null {
  const equipped = inventory.value?.equipped
  if (!equipped || !item.slot) return null

  if (item.slot === 'MainHand') return equipped.mainHand ?? equipped.weapon ?? null
  if (item.slot === 'OffHand') return equipped.offHand ?? null
  if (item.slot === 'Weapon') return equipped.weapon ?? equipped.mainHand ?? null
  if (item.slot === 'Head') return equipped.head
  if (item.slot === 'Chest') return equipped.chest
  if (item.slot === 'Hands') return equipped.hands ?? null
  if (item.slot === 'Legs') return equipped.legs
  if (item.slot === 'Feet') return equipped.feet ?? equipped.boots ?? null
  if (item.slot === 'Boots') return equipped.boots ?? equipped.feet ?? null
  if (item.slot === 'Cloak') return equipped.cloak ?? null
  if (item.slot === 'Amulet') return equipped.amulet ?? equipped.accessory ?? null
  if (item.slot === 'Ring1') return equipped.ring1 ?? null
  if (item.slot === 'Ring2') return equipped.ring2 ?? null
  if (item.slot === 'Accessory') return equipped.accessory ?? equipped.amulet ?? null
  return null
}

function comparisonDeltaLabel(delta: number): string {
  if (delta > 0) return `+${formatNumber(delta)}`
  if (delta < 0) return formatNumber(delta)
  return '0'
}

function formatNumber(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(2).replace(/\.00$/, '')
}

function openItem(item: InventoryItem | null): void {
  selectedItem.value = item
  equipmentActionError.value = null
  if (item) markItemSeen(item.id)
}

function seenStorageKey(): string | null {
  const characterId = character.value?.id
  return characterId ? `elyndor.inventory.seen.${characterId}` : null
}

function syncNewItems(): void {
  const key = seenStorageKey()
  if (!key) {
    newItemIds.value = new Set()
    return
  }

  const currentIds = bagItems.value.map(item => item.id)
  try {
    const raw = globalThis.localStorage?.getItem(key)
    if (raw === null) {
      globalThis.localStorage?.setItem(key, JSON.stringify(currentIds))
      newItemIds.value = new Set()
      return
    }

    const parsed = JSON.parse(raw) as unknown
    const seen = new Set(Array.isArray(parsed)
      ? parsed.filter((value): value is string => typeof value === 'string')
      : [])
    newItemIds.value = new Set(currentIds.filter(id => !seen.has(id)))
  } catch {
    newItemIds.value = new Set()
  }
}

function markItemSeen(itemId: string): void {
  const key = seenStorageKey()
  if (!key || !newItemIds.value.has(itemId)) return

  try {
    const raw = globalThis.localStorage?.getItem(key)
    const parsed = raw ? JSON.parse(raw) as unknown : []
    const seen = new Set(Array.isArray(parsed)
      ? parsed.filter((value): value is string => typeof value === 'string')
      : [])
    seen.add(itemId)
    globalThis.localStorage?.setItem(key, JSON.stringify([...seen]))
  } catch {
    // NEW is a presentation hint only; storage failure must not block inventory actions.
  }

  const next = new Set(newItemIds.value)
  next.delete(itemId)
  newItemIds.value = next
}

watch(
  () => [character.value?.id ?? '', ...bagItems.value.map(item => item.id)].join('|'),
  syncNewItems,
  { immediate: true },
)

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

function inventoryActionError(code: string | null): string | null {
  if (!code) return null
  if (code === 'inventory_armor_category_restricted') return 'Этот тип брони недоступен вашему классу.'
  if (code === 'inventory_weapon_category_restricted') return 'Этот тип оружия недоступен вашему классу.'
  if (code === 'inventory_off_hand_category_restricted') return 'Этот предмет нельзя взять во вторую руку вашим классом.'
  if (code === 'inventory_class_restricted') return 'Этот предмет предназначен для другого класса.'
  if (code === 'inventory_required_level') return 'Недостаточный уровень для этого предмета.'
  if (code === 'inventory_equipment_change_in_combat') return 'Снаряжение нельзя менять во время боя.'
  if (code === 'inventory_two_handed_conflict') return 'Двуручное оружие конфликтует со второй рукой.'
  if (code === 'inventory_dual_wield_permission_required') return 'Второе одноручное оружие требует таланта «Двойной Удар» в активном билде Берсерка.'
  return 'Не удалось изменить снаряжение.'
}

async function equipSelected(targetSlot?: EquipmentSlot): Promise<void> {
  const item = selectedItem.value
  if (!item || item.type !== 'Equipment') return
  await session.equip(item.id, targetSlot)
  equipmentActionError.value = session.errorCode
  if (!equipmentActionError.value) selectedItem.value = null
}

async function useSelected(): Promise<void> {
  const item = selectedItem.value
  if (!item || item.type !== 'Consumable') return
  await session.useConsumable(item.id)
  selectedItem.value = null
}

async function toggleSelectedLock(): Promise<void> {
  const item = selectedItem.value
  if (!item || session.mutationPending) return

  await session.setItemLock(item.id, !item.isLocked)
  const refreshed = session.snapshot?.character?.inventory.items.find(
    candidate => candidate.id === item.id,
  )
  if (refreshed) selectedItem.value = refreshed
}
</script>

<template>
  <section class="inventory-view">
    <header class="inventory-header">
      <div>
        <p>{{ isContextualSlotMode ? 'Снаряжение' : 'Инвентарь' }}</p>
        <h1>{{ isContextualSlotMode ? `Выберите: ${slotLabel(contextualSlot)}` : 'Рюкзак' }}</h1>
      </div>
      <div class="capacity" :class="{ 'capacity--warning': usedSlots >= BAG_CAPACITY - 4 }">
        <strong>{{ usedSlots }}</strong><span>/ {{ BAG_CAPACITY }}</span>
      </div>
    </header>

    <section v-if="inventory" class="inventory-tools" aria-label="Фильтры инвентаря">
      <div v-if="!isContextualSlotMode" class="filter-row">
        <small>Тип</small>
        <div class="filter-chips filter-chips--scroll">
          <button type="button" :class="{ active: typeFilter === 'all' }" @click="typeFilter = 'all'">Все</button>
          <button type="button" :class="{ active: typeFilter === 'equipment' }" @click="typeFilter = 'equipment'">Снаряжение</button>
          <button type="button" :class="{ active: typeFilter === 'consumable' }" @click="typeFilter = 'consumable'">Расходники</button>
          <button type="button" :class="{ active: typeFilter === 'material' }" @click="typeFilter = 'material'">Материалы</button>
        </div>
      </div>
      <div class="filter-row filter-row--rarity">
        <small>Редкость</small>
        <div class="filter-chips filter-chips--scroll">
          <button type="button" :class="{ active: rarityFilter === 'all' }" @click="rarityFilter = 'all'">Любая</button>
          <button type="button" :class="{ active: rarityFilter === 'Common' }" @click="rarityFilter = 'Common'">Обычная</button>
          <button type="button" :class="{ active: rarityFilter === 'Uncommon' }" @click="rarityFilter = 'Uncommon'">Необычная</button>
          <button type="button" :class="{ active: rarityFilter === 'Rare' }" @click="rarityFilter = 'Rare'">Редкая</button>
          <button type="button" :class="{ active: rarityFilter === 'Epic' }" @click="rarityFilter = 'Epic'">Эпическая</button>
          <button type="button" :class="{ active: rarityFilter === 'Legendary' }" @click="rarityFilter = 'Legendary'">Легендарная</button>
          <button type="button" :class="{ active: rarityFilter === 'Unique' }" @click="rarityFilter = 'Unique'">Уникальная</button>
        </div>
      </div>
      <div class="filter-row filter-row--sort">
        <small>Порядок</small>
        <label class="sort-select">
          <span class="sr-only">Сортировка предметов</span>
          <select v-model="sortMode" data-inventory-sort>
            <option value="default">Как получено</option>
            <option value="rarity">По редкости</option>
            <option value="level">По уровню</option>
            <option value="name">По названию</option>
          </select>
        </label>
      </div>
    </section>

    <section v-if="inventory" class="bag-surface">
      <header class="bag-surface__header">
        <div>
          <small>{{ isContextualSlotMode ? 'Подходящий слот' : typeFilter === 'all' && rarityFilter === 'all' ? 'Все предметы' : 'Результат фильтра' }}</small>
          <strong>{{ isContextualSlotMode ? `${filteredItems.length} подходит` : typeFilter === 'all' && rarityFilter === 'all' ? `${usedSlots} занято` : `${filteredItems.length} найдено` }}</strong>
        </div>
        <span v-if="isContextualSlotMode">Выбор снаряжения</span>
        <span v-else-if="typeFilter !== 'all' || rarityFilter !== 'all'">Фильтр активен</span>
      </header>

      <div
        v-if="bagItems.length > 0 && visibleCells.length && (filteredItems.length || (typeFilter === 'all' && rarityFilter === 'all'))"
        class="bag-grid"
      >
        <button
          v-for="(item, index) in visibleCells"
          :key="item?.id ?? `empty-${index}`"
          class="bag-cell"
          :class="{ 'bag-cell--empty': !item }"
          :data-rarity="item?.rarity"
          :data-item-id="item?.id"
          :data-new="item ? newItemIds.has(item.id) : undefined"
          :data-locked="item?.isLocked ?? undefined"
          type="button"
          :disabled="!item"
          :aria-label="item?.name ?? 'Пустая ячейка'"
          @click="openItem(item)"
        >
          <template v-if="item">
            <span v-if="newItemIds.has(item.id)" class="bag-cell__new">NEW</span>
            <span v-if="item.isLocked" class="bag-cell__lock" aria-label="Предмет защищён">◆</span>
            <span class="bag-cell__icon">{{ itemGlyph(item) }}</span>
            <b v-if="item.quantity > 1" class="bag-cell__quantity">{{ item.quantity }}</b>
            <i class="bag-cell__rarity" aria-hidden="true" />
          </template>
        </button>
      </div>

      <UILoadingState
        v-else-if="bagItems.length === 0"
        state="empty"
        title="Рюкзак пуст"
        message="Исследуйте мир и побеждайте противников, чтобы находить добычу."
      />
      <UILoadingState
        v-else
        state="empty"
        title="Ничего не найдено"
        :message="isContextualSlotMode ? 'В рюкзаке нет предметов для выбранного слота.' : 'Измените выбранные фильтры.'"
      />
    </section>

    <UIModal :open="selectedItem !== null" :title="selectedItem?.name ?? ''" @close="selectedItem = null">
      <article v-if="selectedItem" class="item-detail">
        <div class="item-detail__identity">
          <span class="item-detail__icon" :data-rarity="selectedItem.rarity">{{ itemGlyph(selectedItem) }}</span>
          <div>
            <p>{{ rarityLabel(selectedItem) }} · {{ typeLabel(selectedItem) }}</p>
            <strong>Количество: {{ selectedItem.quantity }}</strong>
            <span v-if="selectedItem.isLocked" class="item-detail__locked">◆ ЗАЩИЩЕНО</span>
          </div>
        </div>
        <p class="item-detail__description">{{ selectedItem.description }}</p>
        <dl v-if="statRows(selectedItem).length">
          <div v-for="row in statRows(selectedItem)" :key="row"><dt>{{ row }}</dt></div>
        </dl>

        <section v-if="comparisonItem" class="item-comparison" aria-label="Сравнение с надетым предметом">
          <header>
            <div>
              <small>СРАВНЕНИЕ</small>
              <strong>Сейчас: {{ comparisonItem.name }}</strong>
            </div>
            <span>{{ typeLabel(comparisonItem) }}</span>
          </header>
          <div v-if="comparisonRows.length" class="comparison-grid">
            <div v-for="row in comparisonRows" :key="row.label">
              <span>{{ row.label }}</span>
              <small>{{ formatNumber(row.equippedValue) }} → {{ formatNumber(row.candidateValue) }}</small>
              <b :data-delta="row.delta > 0 ? 'up' : row.delta < 0 ? 'down' : 'same'">
                {{ comparisonDeltaLabel(row.delta) }}
              </b>
            </div>
          </div>
          <p v-else class="item-detail__hint">Характеристики предметов совпадают.</p>
        </section>
        <p v-if="selectedItem.weaponBaseAttackIntervalSeconds" class="item-detail__hint">Базовый интервал автоатаки: {{ selectedItem.weaponBaseAttackIntervalSeconds }} сек.</p>
        <p v-if="selectedItem.setId" class="item-detail__hint">Часть комплекта Следопыта. Бонусы активируются за 3 и 6 надетых предметов.</p>
        <p v-if="selectedItem.type === 'Material' && !selectedItem.isLocked" class="item-detail__hint">Можно сохранить для ремесла или продать Маркусу за {{ selectedItem.sellPriceGold }} золота за штуку.</p>
        <p v-if="selectedItem.type === 'Material' && selectedItem.isLocked" class="item-detail__hint item-detail__hint--locked">Предмет защищён от продажи торговцу. Снимите защиту, если захотите его продать.</p>
        <p v-if="selectedItem.type === 'Consumable'" class="item-detail__hint">Восстанавливает {{ selectedItem.healAmount }} здоровья. В бою общий кулдаун зелий — {{ selectedItem.consumableCooldownSeconds }} сек.</p>
        <p
          v-if="selectedItem.type === 'Equipment' && inventoryActionError(equipmentActionError)"
          class="item-detail__error"
          role="alert"
        >
          {{ inventoryActionError(equipmentActionError) }}
        </p>
      </article>
      <template #actions>
        <template v-if="selectedItem?.type === 'Equipment' && isOneHandWeapon(selectedItem)">
          <UIButton
            v-if="!isContextualSlotMode || canonicalSlot(contextualSlot!) === 'MainHand'"
            data-equip-target="MainHand"
            :loading="session.mutationPending"
            :disabled="session.mutationPending || (character?.level ?? 0) < selectedItem.requiredLevel"
            @click="equipSelected('MainHand')"
          >
            В основную руку
          </UIButton>
          <UIButton
            v-if="!isContextualSlotMode || canonicalSlot(contextualSlot!) === 'OffHand'"
            data-equip-target="OffHand"
            :loading="session.mutationPending"
            :disabled="session.mutationPending || (character?.level ?? 0) < selectedItem.requiredLevel"
            @click="equipSelected('OffHand')"
          >
            Во вторую руку
          </UIButton>
        </template>
        <UIButton
          v-else-if="selectedItem?.type === 'Equipment'"
          :loading="session.mutationPending"
          :disabled="session.mutationPending || (character?.level ?? 0) < selectedItem.requiredLevel"
          @click="equipSelected()"
        >
          Надеть
        </UIButton>
        <UIButton
          v-if="selectedItem?.type === 'Consumable'"
          :loading="session.mutationPending"
          :disabled="session.mutationPending || (character?.vitals.currentHp ?? 0) >= (character?.vitals.maxHp ?? 0)"
          @click="useSelected"
        >
          Использовать
        </UIButton>
        <UIButton
          v-if="selectedItem"
          variant="secondary"
          data-item-lock-action
          :loading="session.mutationPending"
          :disabled="session.mutationPending"
          @click="toggleSelectedLock"
        >
          {{ selectedItem.isLocked ? 'Снять защиту' : 'Защитить' }}
        </UIButton>
      </template>
    </UIModal>
  </section>
</template>

<style scoped>
.inventory-view {
  display: grid;
  width: min(100%, var(--ui-content-width));
  margin-inline: auto;
  gap: var(--ui-space-3);
  padding: var(--ui-space-4) var(--ui-space-3) var(--ui-space-7);
}

.inventory-header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: var(--ui-space-3);
}

.inventory-header > div:first-child {
  display: grid;
  gap: 2px;
}

.inventory-header p,
.inventory-header h1 {
  margin: 0;
}

.inventory-header p {
  color: var(--ui-color-primary);
  font-size: .6rem;
  font-weight: 700;
  letter-spacing: .1em;
  text-transform: uppercase;
}

.inventory-header h1 {
  font-family: var(--ui-font-display);
  font-size: clamp(1.65rem, 7vw, 2.15rem);
}

.capacity {
  display: flex;
  align-items: baseline;
  gap: 3px;
  padding: 6px 9px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-round);
  background: var(--ui-color-surface-2);
  color: var(--ui-color-text-muted);
  font-variant-numeric: tabular-nums;
}

.capacity strong {
  color: var(--ui-color-text-primary);
  font-size: var(--ui-font-size-md);
}

.capacity span {
  font-size: .68rem;
}

.capacity--warning {
  border-color: color-mix(in srgb, var(--ui-color-warning) 48%, var(--ui-color-border));
  color: var(--ui-color-warning);
}

.inventory-tools {
  display: grid;
  gap: var(--ui-space-2);
  padding-block: var(--ui-space-2);
  border-block: 1px solid rgb(255 255 255 / 6%);
}

.filter-row {
  display: grid;
  grid-template-columns: 3.2rem minmax(0, 1fr);
  align-items: center;
  gap: var(--ui-space-2);
}

.filter-row > small {
  color: var(--ui-color-text-muted);
  font-size: .58rem;
  font-weight: 700;
  letter-spacing: .05em;
  text-transform: uppercase;
}

.filter-chips {
  display: flex;
  gap: 6px;
}

.filter-chips--scroll {
  overflow-x: auto;
  padding-bottom: 2px;
  scrollbar-width: none;
}

.filter-chips--scroll::-webkit-scrollbar {
  display: none;
}

.filter-chips button {
  min-height: 2rem;
  flex: 0 0 auto;
  padding: 0 var(--ui-space-3);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-round);
  background: rgb(255 255 255 / 2%);
  color: var(--ui-color-text-muted);
  font: inherit;
  font-size: .62rem;
  white-space: nowrap;
}

.filter-chips button.active {
  border-color: color-mix(in srgb, var(--ui-color-primary) 58%, var(--ui-color-border));
  background: rgb(146 136 255 / 9%);
  color: #d5d2ff;
}

.sort-select {
  display: block;
  min-width: 0;
}

.sort-select select {
  width: min(100%, 15rem);
  min-height: 2rem;
  padding: 0 var(--ui-space-3);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-round);
  background: var(--ui-color-surface-2);
  color: var(--ui-color-text-secondary);
  font: inherit;
  font-size: .62rem;
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
}

.bag-surface {
  display: grid;
  gap: var(--ui-space-3);
  padding: var(--ui-space-3);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-lg);
  background:
    radial-gradient(circle at 50% 0, rgb(146 136 255 / 5%), transparent 13rem),
    linear-gradient(180deg, rgb(14 19 31 / 72%), rgb(7 10 17 / 70%));
  box-shadow: var(--ui-shadow-inset);
}

.bag-surface__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-3);
}

.bag-surface__header > div {
  display: grid;
  gap: 1px;
}

.bag-surface__header small,
.bag-surface__header > span {
  color: var(--ui-color-text-muted);
  font-size: .58rem;
}

.bag-surface__header strong {
  font-size: var(--ui-font-size-sm);
}

.bag-surface__header > span {
  padding: 4px 7px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-round);
}

.bag-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: var(--ui-space-2);
}

.bag-cell {
  position: relative;
  aspect-ratio: 1;
  overflow: hidden;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: var(--ui-radius-md);
  background:
    radial-gradient(circle at 50% 35%, rgb(255 255 255 / 4%), transparent 62%),
    var(--ui-color-surface-2);
  box-shadow: inset 0 0 0 1px rgb(255 255 255 / 2%);
  color: var(--ui-color-text-primary);
}

.bag-cell[data-rarity='Uncommon'] { border-color: color-mix(in srgb, var(--ui-color-success) 70%, var(--ui-color-border)); }
.bag-cell[data-rarity='Rare'] { border-color: color-mix(in srgb, var(--ui-color-secondary) 72%, var(--ui-color-border)); }
.bag-cell[data-rarity='Epic'] { border-color: color-mix(in srgb, var(--ui-color-primary) 82%, var(--ui-color-border)); }
.bag-cell[data-rarity='Legendary'],
.bag-cell[data-rarity='Unique'] {
  border-color: var(--ui-color-gold);
  box-shadow: inset 0 0 0 1px rgb(255 255 255 / 3%), 0 0 12px rgb(232 200 102 / 10%);
}

.bag-cell[data-locked='true'] {
  box-shadow: inset 0 0 0 1px rgb(232 200 102 / 12%), 0 0 12px rgb(232 200 102 / 8%);
}

.bag-cell--empty {
  opacity: .24;
}

.bag-cell__icon {
  display: grid;
  height: 100%;
  place-items: center;
  font-size: clamp(1.35rem, 7vw, 1.85rem);
}

.bag-cell__new {
  position: absolute;
  z-index: 2;
  top: 4px;
  left: 4px;
  padding: 2px 4px;
  border: 1px solid rgb(184 177 255 / 48%);
  border-radius: 4px;
  background: rgb(20 17 44 / 92%);
  color: #c8c3ff;
  font-size: .47rem;
  font-weight: 800;
  letter-spacing: .04em;
}

.bag-cell__lock {
  position: absolute;
  z-index: 2;
  top: 4px;
  right: 4px;
  display: grid;
  width: 1rem;
  height: 1rem;
  place-items: center;
  border: 1px solid rgb(232 200 102 / 48%);
  border-radius: 50%;
  background: rgb(31 27 12 / 92%);
  color: var(--ui-color-gold);
  font-size: .45rem;
}

.bag-cell__quantity {
  position: absolute;
  right: 4px;
  bottom: 3px;
  padding: 1px 4px;
  border-radius: 5px;
  background: #080b14e8;
  color: white;
  font-size: .67rem;
}

.bag-cell__rarity {
  position: absolute;
  right: 15%;
  bottom: 0;
  left: 15%;
  height: 2px;
  border-radius: var(--ui-radius-round);
  background: currentColor;
  opacity: .34;
}

.item-detail {
  display: grid;
  gap: var(--ui-space-3);
}

.item-detail p {
  margin: 0;
}

.item-detail__identity {
  display: flex;
  align-items: center;
  gap: var(--ui-space-3);
}

.item-detail__identity > div {
  display: grid;
  gap: 2px;
}

.item-detail__identity p,
.item-detail__description {
  color: var(--ui-color-text-muted);
}

.item-detail__locked {
  width: fit-content;
  margin-top: 2px;
  padding: 3px 6px;
  border: 1px solid rgb(232 200 102 / 38%);
  border-radius: var(--ui-radius-round);
  color: var(--ui-color-gold);
  font-size: .54rem;
  font-weight: 800;
  letter-spacing: .07em;
}

.item-detail__icon {
  display: grid;
  width: 4.4rem;
  height: 4.4rem;
  place-items: center;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: var(--ui-radius-md);
  background: var(--ui-color-surface-2);
  color: var(--ui-color-primary);
  font-size: 2rem;
}

.item-detail__icon[data-rarity='Legendary'],
.item-detail__icon[data-rarity='Unique'] {
  border-color: var(--ui-color-gold);
  color: var(--ui-color-gold);
}

.item-detail dl {
  display: grid;
  gap: var(--ui-space-1);
  margin: 0;
  padding: var(--ui-space-3);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: rgb(255 255 255 / 1.5%);
}

.item-detail dl div {
  color: var(--ui-color-success);
}

.item-comparison {
  display: grid;
  gap: var(--ui-space-2);
  padding: var(--ui-space-3);
  border: 1px solid color-mix(in srgb, var(--ui-color-primary) 34%, var(--ui-color-border));
  border-radius: var(--ui-radius-md);
  background: linear-gradient(180deg, rgb(146 136 255 / 6%), rgb(255 255 255 / 1%));
}

.item-comparison > header {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: var(--ui-space-2);
}

.item-comparison > header > div {
  display: grid;
  gap: 2px;
}

.item-comparison > header small,
.item-comparison > header > span {
  color: var(--ui-color-text-muted);
  font-size: .58rem;
}

.item-comparison > header small {
  color: #b8b1ff;
  font-weight: 700;
  letter-spacing: .08em;
}

.comparison-grid {
  display: grid;
  gap: 5px;
}

.comparison-grid > div {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto 3.6rem;
  align-items: center;
  gap: var(--ui-space-2);
  padding-top: 5px;
  border-top: 1px solid rgb(255 255 255 / 5%);
  font-size: .67rem;
}

.comparison-grid small {
  color: var(--ui-color-text-muted);
  font-variant-numeric: tabular-nums;
}

.comparison-grid b {
  justify-self: end;
  color: var(--ui-color-text-muted);
  font-variant-numeric: tabular-nums;
}

.comparison-grid b[data-delta='up'] {
  color: var(--ui-color-success);
}

.comparison-grid b[data-delta='down'] {
  color: var(--ui-color-danger);
}

.item-detail__error {
  margin: 0;
  padding: var(--ui-space-2) var(--ui-space-3);
  border: 1px solid rgb(216 95 114 / 28%);
  border-radius: var(--ui-radius-md);
  background: rgb(216 95 114 / 6%);
  color: #ef9bab;
  font-size: .68rem;
}

.item-detail__hint {
  padding: var(--ui-space-3);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: var(--ui-color-surface-2);
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-sm);
}

.item-detail__hint--locked {
  border-color: rgb(232 200 102 / 26%);
  background: linear-gradient(90deg, rgb(232 200 102 / 6%), var(--ui-color-surface-2));
  color: #d8c77e;
}

@media (min-width: 520px) {
  .bag-grid {
    grid-template-columns: repeat(5, minmax(0, 1fr));
  }
}

@media (max-width: 360px) {
  .inventory-view {
    padding-inline: var(--ui-space-2);
  }

  .bag-surface {
    padding-inline: var(--ui-space-2);
  }

  .bag-grid {
    gap: 6px;
  }

  .filter-row {
    grid-template-columns: 2.8rem minmax(0, 1fr);
  }
}
</style>
