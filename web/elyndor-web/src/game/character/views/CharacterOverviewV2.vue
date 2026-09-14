<script setup lang="ts">
import { computed, ref } from 'vue'

import type { CharacterStats, EquipmentSlot, InventoryItem } from '@/api/contracts'
import { resolveCharacterArt } from '@/assets/characterArt'
import { itemArtUrl } from '@/assets/itemArt'
import { classLabel, raceLabel } from '@/game/character/characterPresentation'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIModal } from '@/ui/components'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { GlyphName, Rarity } from '@/ui/icons/icon.types'

const emit = defineEmits<{
  'select-empty-slot': [slot: EquipmentSlot]
  'open-stats': []
}>()

const session = useGameSessionStore()
const character = computed(() => session.snapshot?.character)
const selectedItem = ref<InventoryItem | null>(null)
const selectedEquipmentSlot = ref<EquipmentSlot | null>(null)
const equipmentActionError = ref<string | null>(null)

interface PaperdollSlot {
  id: string
  inventorySlot: EquipmentSlot
  label: string
  glyph: GlyphName
  item: InventoryItem | null
  side: 'left' | 'right'
}

interface StatSummary {
  id: string
  label: string
  value: string
  effective?: string
}

const characterArt = computed(() => {
  const current = character.value
  return current ? resolveCharacterArt(current.classId, current.genderId, 'transparent') : null
})

const equipment = computed<PaperdollSlot[]>(() => {
  const equipped = character.value?.inventory.equipped
  return [
    { id: 'head', inventorySlot: 'Head', label: 'Шлем', item: equipped?.head ?? null, glyph: 'helmet', side: 'left' },
    { id: 'shoulders', inventorySlot: 'Shoulders', label: 'Наплечники', item: equipped?.shoulders ?? null, glyph: 'armor', side: 'left' },
    { id: 'cloak', inventorySlot: 'Cloak', label: 'Плащ', item: equipped?.cloak ?? null, glyph: 'scroll', side: 'left' },
    { id: 'hands', inventorySlot: 'Hands', label: 'Перчатки', item: equipped?.hands ?? null, glyph: 'armor', side: 'left' },
    { id: 'mainHand', inventorySlot: 'MainHand', label: 'Оружие', item: equipped?.mainHand ?? equipped?.weapon ?? null, glyph: 'sword', side: 'left' },
    { id: 'ring1', inventorySlot: 'Ring1', label: 'Кольцо I', item: equipped?.ring1 ?? null, glyph: 'ring', side: 'left' },
    { id: 'chest', inventorySlot: 'Chest', label: 'Нагрудник', item: equipped?.chest ?? null, glyph: 'armor', side: 'right' },
    { id: 'amulet', inventorySlot: 'Amulet', label: 'Амулет', item: equipped?.amulet ?? equipped?.accessory ?? null, glyph: 'star', side: 'right' },
    { id: 'legs', inventorySlot: 'Legs', label: 'Поножи', item: equipped?.legs ?? null, glyph: 'armor', side: 'right' },
    { id: 'feet', inventorySlot: 'Feet', label: 'Обувь', item: equipped?.feet ?? equipped?.boots ?? null, glyph: 'boots', side: 'right' },
    { id: 'offHand', inventorySlot: 'OffHand', label: 'Вторая рука', item: equipped?.offHand ?? null, glyph: 'shield', side: 'right' },
    { id: 'ring2', inventorySlot: 'Ring2', label: 'Кольцо II', item: equipped?.ring2 ?? null, glyph: 'ring', side: 'right' },
  ]
})

const leftEquipment = computed(() => equipment.value.filter(slot => slot.side === 'left'))
const rightEquipment = computed(() => equipment.value.filter(slot => slot.side === 'right'))
const equippedCount = computed(() => equipment.value.filter(slot => slot.item).length)

const primaryStat = computed(() => {
  const current = character.value
  if (!current) return null
  if (current.primaryAttribute === 'STRENGTH') return { label: 'Сила', value: current.stats.strength }
  if (current.primaryAttribute === 'AGILITY') return { label: 'Ловкость', value: current.stats.agility }
  return { label: 'Интеллект', value: current.stats.intellect }
})

function breakdownValue(id: string): number | null {
  const breakdown = character.value?.statBreakdown as Record<string, { finalValue: number }> | undefined
  return breakdown?.[id]?.finalValue ?? null
}

function percent(value: number): string {
  return `${value.toFixed(2).replace(/\.00$/, '')}%`
}

function number(value: number): string {
  const rounded = Math.round(value * 10) / 10
  return Number.isInteger(rounded) ? String(rounded) : rounded.toFixed(1)
}

const keyStats = computed<StatSummary[]>(() => {
  const current = character.value
  if (!current || !primaryStat.value) return []

  const offensive = current.classId === 'MAGE'
    ? { id: 'spellPower', label: 'Сила заклинаний', value: number(current.stats.spellPower) }
    : { id: 'attackPower', label: 'Сила атаки', value: number(current.stats.attackPower) }

  const result: StatSummary[] = [
    { id: 'hp', label: 'Здоровье', value: `${number(current.vitals.maxHp)}` },
    { id: 'primary', label: primaryStat.value.label, value: number(primaryStat.value.value) },
    offensive,
    { id: 'criticalChance', label: 'Крит. шанс', value: percent(current.stats.criticalChance) },
    {
      id: 'armor',
      label: 'Броня',
      value: number(current.stats.armor),
      effective: breakdownValue('armorDamageReductionPercent') === null ? undefined : `${percent(breakdownValue('armorDamageReductionPercent')!)} физ. снижения`,
    },
    {
      id: 'magicResistance',
      label: 'Сопр. магии',
      value: number(current.stats.magicResistance),
      effective: breakdownValue('magicDamageReductionPercent') === null ? undefined : `${percent(breakdownValue('magicDamageReductionPercent')!)} маг. снижения`,
    },
    { id: 'dodge', label: 'Уклонение', value: percent(current.stats.dodge) },
    { id: 'attackSpeed', label: 'Скорость атаки', value: `${current.stats.attackSpeed.toFixed(2)}×` },
  ]

  const blockChance = breakdownValue('blockChance')
  const blockMin = breakdownValue('blockValueMin')
  const blockMax = breakdownValue('blockValueMax')
  if (blockChance !== null) result.push({ id: 'blockChance', label: 'Шанс блока', value: percent(blockChance) })
  if (blockMin !== null || blockMax !== null) {
    result.push({ id: 'blockValue', label: 'Сила блока', value: `${number(blockMin ?? 0)}–${number(blockMax ?? blockMin ?? 0)}` })
  }

  return result
})

function openEquipmentSlot(slot: PaperdollSlot): void {
  if (!slot.item) {
    emit('select-empty-slot', slot.inventorySlot)
    return
  }
  selectedItem.value = slot.item
  selectedEquipmentSlot.value = slot.inventorySlot
  equipmentActionError.value = null
}

function closeItem(): void {
  selectedItem.value = null
  selectedEquipmentSlot.value = null
  equipmentActionError.value = null
}

async function unequipSelected(): Promise<void> {
  if (!selectedEquipmentSlot.value || session.mutationPending) return
  await session.unequip(selectedEquipmentSlot.value)
  equipmentActionError.value = session.errorCode
  if (!equipmentActionError.value) closeItem()
}

function equipmentErrorMessage(code: string | null): string | null {
  if (!code) return null
  if (code === 'inventory_equipment_change_in_combat') return 'Снаряжение нельзя менять во время боя.'
  if (code === 'inventory_invalid_slot') return 'Сервер не распознал слот снаряжения.'
  if (code === 'inventory_item_transaction_locked') return 'Предмет занят другой операцией. Повторите попытку.'
  return 'Не удалось изменить снаряжение. Повторите попытку.'
}

function itemArt(item: InventoryItem | null): string | undefined {
  return itemArtUrl(item?.iconId)
}

function itemGlyph(item: InventoryItem | null, fallback: GlyphName): GlyphName {
  if (!item) return fallback
  if (item.slot === 'Weapon' || item.slot === 'MainHand') return 'sword'
  if (item.slot === 'OffHand') return 'shield'
  if (item.slot === 'Head') return 'helmet'
  if (['Shoulders', 'Chest', 'Hands', 'Legs'].includes(item.slot ?? '')) return 'armor'
  if (item.slot === 'Boots' || item.slot === 'Feet') return 'boots'
  if (item.slot === 'Cloak') return 'scroll'
  if (item.slot === 'Amulet' || item.slot === 'Ring1' || item.slot === 'Ring2') return 'ring'
  return 'star'
}

function itemRarity(item: InventoryItem | null): Rarity | undefined {
  return item?.rarity.toLowerCase() as Rarity | undefined
}

function rarityLabel(item: InventoryItem): string {
  const labels: Record<string, string> = {
    Common: 'Обычный', Uncommon: 'Необычный', Rare: 'Редкий', Epic: 'Эпический', Legendary: 'Легендарный', Unique: 'Уникальный',
  }
  return labels[item.rarity] ?? item.rarity
}

function slotLabel(slot: EquipmentSlot | null): string {
  if (!slot) return 'Не экипируется'
  const labels: Partial<Record<EquipmentSlot, string>> = {
    MainHand: 'Основная рука', OffHand: 'Вторая рука', Head: 'Шлем', Shoulders: 'Наплечники', Chest: 'Нагрудник', Hands: 'Перчатки',
    Legs: 'Поножи', Feet: 'Обувь', Cloak: 'Плащ', Amulet: 'Амулет', Ring1: 'Кольцо', Ring2: 'Кольцо', Weapon: 'Оружие', Boots: 'Обувь', Accessory: 'Аксессуар',
  }
  return labels[slot] ?? slot
}

function classRestriction(item: InventoryItem): string {
  if (!item.allowedClassIds.length) return 'Все классы'
  return item.allowedClassIds.map(id => classLabel(id as 'WARRIOR' | 'ARCHER' | 'MAGE')).join(', ')
}

const itemStatLabels: Record<keyof CharacterStats | 'maxResource', string> = {
  strength: 'Сила', agility: 'Ловкость', intellect: 'Интеллект', stamina: 'Выносливость', maxHp: 'Здоровье', attackPower: 'Сила атаки',
  spellPower: 'Сила заклинаний', criticalChance: 'Крит. шанс', criticalDamage: 'Крит. урон', accuracy: 'Меткость', armorPenetration: 'Пробивание брони',
  magicPenetration: 'Пробивание магии', attackSpeed: 'Скорость атаки', armor: 'Броня', magicResistance: 'Сопротивление магии', dodge: 'Уклонение', maxResource: 'Макс. ресурс',
}

const percentItemStats = new Set(['criticalChance', 'criticalDamage', 'accuracy', 'armorPenetration', 'magicPenetration', 'dodge'])

function itemStats(item: InventoryItem): { id: string; label: string; value: string }[] {
  const stats = item.stats as unknown as Record<string, number>
  const rows = Object.entries(itemStatLabels)
    .map(([id, label]) => ({ id, label, raw: stats[id] ?? 0 }))
    .filter(row => row.raw !== 0)
    .map(row => ({ id: row.id, label: row.label, value: `+${number(row.raw)}${percentItemStats.has(row.id) ? '%' : ''}` }))

  if (item.attackSpeedPercent) rows.push({ id: 'attackSpeedPercent', label: 'Скорость атаки', value: `+${number(item.attackSpeedPercent)}%` })
  if (item.dodgePercent) rows.push({ id: 'dodgePercent', label: 'Уклонение', value: `+${number(item.dodgePercent)}%` })
  return rows
}

function itemLevel(item: InventoryItem): number | null {
  return item.generatedItem?.itemLevel ?? null
}

function bindLabel(item: InventoryItem): string | null {
  if (!item.bindState) return null
  if (item.bindState === 'BOUND') return 'Привязан'
  if (item.bindState === 'UNBOUND') return 'Не привязан'
  return item.bindState
}
</script>

<template>
  <section v-if="character" class="overview-v2">
    <section class="hero-card">
      <header class="hero-card__header">
        <div>
          <p class="eyebrow">Персонаж</p>
          <h1>{{ character.name }}</h1>
          <p>{{ raceLabel(character.raceId) }} · {{ classLabel(character.classId) }} · ур. {{ character.level }}</p>
        </div>
        <div class="hero-card__status">
          <span>{{ equippedCount }}/{{ equipment.length }}</span>
          <small>слотов</small>
        </div>
      </header>

      <div class="paperdoll-v2">
        <div class="equipment-column" aria-label="Снаряжение слева">
          <button v-for="slot in leftEquipment" :key="slot.id" type="button" class="equipment-slot" :class="{ filled: slot.item }" :data-rarity="slot.item?.rarity" @click="openEquipmentSlot(slot)">
            <span class="equipment-slot__icon">
              <img v-if="itemArt(slot.item)" :src="itemArt(slot.item)" :alt="slot.item?.name ?? slot.label" />
              <IconGenerator v-else :config="{ id: `paperdoll-v2-${slot.id}`, glyph: itemGlyph(slot.item, slot.glyph), category: 'equipment', rarity: itemRarity(slot.item) }" />
            </span>
            <small>{{ slot.label }}</small>
          </button>
        </div>

        <div class="hero-figure">
          <div class="hero-figure__art">
            <img v-if="characterArt" :src="characterArt" :alt="classLabel(character.classId)" />
            <div v-else class="hero-figure__fallback">{{ character.name.slice(0, 1).toUpperCase() }}</div>
          </div>
          <strong>{{ classLabel(character.classId) }}</strong>
          <small>Уровень {{ character.level }}</small>
        </div>

        <div class="equipment-column" aria-label="Снаряжение справа">
          <button v-for="slot in rightEquipment" :key="slot.id" type="button" class="equipment-slot" :class="{ filled: slot.item }" :data-rarity="slot.item?.rarity" @click="openEquipmentSlot(slot)">
            <span class="equipment-slot__icon">
              <img v-if="itemArt(slot.item)" :src="itemArt(slot.item)" :alt="slot.item?.name ?? slot.label" />
              <IconGenerator v-else :config="{ id: `paperdoll-v2-${slot.id}`, glyph: itemGlyph(slot.item, slot.glyph), category: 'equipment', rarity: itemRarity(slot.item) }" />
            </span>
            <small>{{ slot.label }}</small>
          </button>
        </div>
      </div>
    </section>

    <section class="combat-summary">
      <div class="combat-summary__heading">
        <div>
          <p class="eyebrow">Боевые показатели</p>
          <h2>Что реально даёт герой</h2>
        </div>
        <button type="button" @click="emit('open-stats')">Все характеристики ›</button>
      </div>
      <div class="stat-grid">
        <article v-for="stat in keyStats" :key="stat.id">
          <span>{{ stat.label }}</span>
          <strong>{{ stat.value }}</strong>
          <small v-if="stat.effective">{{ stat.effective }}</small>
        </article>
      </div>
    </section>

    <UIModal :open="selectedItem !== null" :title="selectedItem?.name ?? ''" @close="closeItem">
      <article v-if="selectedItem" class="item-detail">
        <header class="item-detail__hero" :data-rarity="selectedItem.rarity">
          <div class="item-detail__icon">
            <img v-if="itemArt(selectedItem)" :src="itemArt(selectedItem)" :alt="selectedItem.name" />
            <IconGenerator v-else :config="{ id: 'selected-equipment', glyph: itemGlyph(selectedItem, 'armor'), category: 'equipment', rarity: itemRarity(selectedItem) }" />
          </div>
          <div>
            <strong>{{ selectedItem.name }}</strong>
            <span>{{ rarityLabel(selectedItem) }}</span>
            <small>{{ slotLabel(selectedItem.slot) }}</small>
          </div>
        </header>

        <section class="item-meta">
          <div v-if="itemLevel(selectedItem) !== null"><span>Уровень предмета</span><strong>{{ itemLevel(selectedItem) }}</strong></div>
          <div><span>Требуется уровень</span><strong :class="{ danger: character.level < selectedItem.requiredLevel }">{{ selectedItem.requiredLevel }}</strong></div>
          <div><span>Класс</span><strong>{{ classRestriction(selectedItem) }}</strong></div>
          <div v-if="bindLabel(selectedItem)"><span>Состояние</span><strong>{{ bindLabel(selectedItem) }}</strong></div>
        </section>

        <section v-if="itemStats(selectedItem).length" class="item-section">
          <h3>Характеристики</h3>
          <dl>
            <div v-for="row in itemStats(selectedItem)" :key="row.id"><dt>{{ row.label }}</dt><dd>{{ row.value }}</dd></div>
          </dl>
        </section>

        <section v-if="selectedItem.weaponBaseAttackIntervalSeconds" class="item-section">
          <h3>Оружие</h3>
          <dl><div><dt>Базовый интервал атаки</dt><dd>{{ number(selectedItem.weaponBaseAttackIntervalSeconds) }} сек.</dd></div></dl>
        </section>

        <section v-if="selectedItem.generatedItem" class="item-section">
          <h3>Качество предмета</h3>
          <dl>
            <div><dt>Сила предмета</dt><dd>{{ number(selectedItem.generatedItem.itemPower) }} / {{ number(selectedItem.generatedItem.maxItemPower) }}</dd></div>
            <div><dt>Качество ролла</dt><dd>{{ percent(selectedItem.generatedItem.rollQuality) }}</dd></div>
            <div><dt>Звёзды</dt><dd>{{ selectedItem.generatedItem.stars }}</dd></div>
          </dl>
        </section>

        <section v-if="selectedItem.setId" class="item-section item-section--accent"><h3>Комплект</h3><p>{{ selectedItem.setId }}</p></section>
        <section v-if="selectedItem.description" class="item-section"><h3>Описание</h3><p>{{ selectedItem.description }}</p></section>

        <p v-if="character.level < selectedItem.requiredLevel" class="item-warning">Требуется {{ selectedItem.requiredLevel }} уровень. Сейчас у героя {{ character.level }}.</p>
        <p v-if="equipmentErrorMessage(equipmentActionError)" class="item-warning" role="alert">{{ equipmentErrorMessage(equipmentActionError) }}</p>
      </article>
      <template #actions>
        <UIButton v-if="selectedEquipmentSlot" variant="secondary" :loading="session.mutationPending" :disabled="session.mutationPending" @click="unequipSelected">Снять</UIButton>
      </template>
    </UIModal>
  </section>
</template>

<style scoped>
.overview-v2 { display: grid; width: min(100%, var(--ui-content-width)); margin-inline: auto; gap: var(--ui-space-3); padding: var(--ui-space-3) var(--ui-space-3) var(--ui-space-7); }
.hero-card, .combat-summary { overflow: hidden; border: 1px solid var(--ui-color-border-strong); border-radius: calc(var(--ui-radius-lg) + 2px); background: linear-gradient(180deg, rgb(14 20 33 / 98%), rgb(6 10 17 / 98%)); box-shadow: var(--ui-shadow-inset), var(--ui-shadow-elevated); }
.hero-card { background: radial-gradient(circle at 50% 35%, rgb(146 136 255 / 16%), transparent 15rem), linear-gradient(180deg, rgb(14 20 33 / 98%), rgb(6 10 17 / 98%)); }
.hero-card__header { display: flex; align-items: end; justify-content: space-between; gap: var(--ui-space-3); padding: var(--ui-space-4) var(--ui-space-4) var(--ui-space-2); }
.hero-card__header h1, .hero-card__header p, .combat-summary h2, .combat-summary p { margin: 0; }
.hero-card__header h1 { font-family: var(--ui-font-display); font-size: clamp(1.5rem, 7vw, 2.15rem); }
.hero-card__header > div > p:last-child { margin-top: 3px; color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-xs); }
.eyebrow { color: #bcb6ff; font-size: .6rem; font-weight: 800; letter-spacing: .11em; text-transform: uppercase; }
.hero-card__status { display: grid; justify-items: end; }
.hero-card__status span { color: var(--ui-color-gold); font-size: 1.1rem; font-weight: 800; }
.hero-card__status small { color: var(--ui-color-text-muted); }
.paperdoll-v2 { display: grid; grid-template-columns: 58px minmax(0, 1fr) 58px; align-items: center; gap: 8px; min-height: 22rem; padding: var(--ui-space-2) var(--ui-space-3) var(--ui-space-4); }
.equipment-column { display: grid; gap: 8px; }
.equipment-slot { display: grid; width: 56px; min-height: 58px; place-items: center; gap: 2px; padding: 3px; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: rgb(4 8 14 / 90%); color: var(--ui-color-text-muted); font: inherit; }
.equipment-slot:not(.filled) { opacity: .52; }
.equipment-slot.filled { color: var(--ui-color-text-primary); }
.equipment-slot[data-rarity='Rare'] { border-color: #4d8ee8; }
.equipment-slot[data-rarity='Epic'] { border-color: #9a70e8; box-shadow: 0 0 12px rgb(154 112 232 / 12%); }
.equipment-slot[data-rarity='Legendary'], .equipment-slot[data-rarity='Unique'] { border-color: var(--ui-color-gold); box-shadow: 0 0 14px rgb(232 200 102 / 14%); }
.equipment-slot__icon { display: grid; width: 44px; height: 40px; place-items: center; overflow: hidden; border-radius: calc(var(--ui-radius-md) - 2px); background: rgb(2 5 9 / 70%); }
.equipment-slot__icon img { width: 100%; height: 100%; object-fit: cover; }
.equipment-slot small { width: 100%; overflow: hidden; font-size: .43rem; font-weight: 800; text-align: center; text-overflow: ellipsis; text-transform: uppercase; white-space: nowrap; }
.hero-figure { display: grid; min-width: 0; align-self: stretch; align-content: end; justify-items: center; padding-bottom: 2px; text-align: center; }
.hero-figure__art { position: relative; display: grid; width: 100%; min-height: 17.5rem; place-items: end center; }
.hero-figure__art::before { position: absolute; inset: 14% 4% 7%; border-radius: 50%; background: radial-gradient(circle, rgb(112 96 230 / 15%), transparent 65%); content: ''; }
.hero-figure__art img { position: relative; z-index: 1; width: 100%; max-height: 18.5rem; object-fit: contain; object-position: center bottom; filter: drop-shadow(0 12px 14px rgb(0 0 0 / 42%)); }
.hero-figure__fallback { display: grid; width: 8rem; height: 13rem; place-items: center; border: 1px solid var(--ui-color-border); border-radius: 45% 45% 30% 30%; background: rgb(20 27 43 / 80%); font-size: 3rem; }
.hero-figure > strong { font-family: var(--ui-font-display); }
.hero-figure > small { color: var(--ui-color-text-muted); }
.combat-summary { padding: var(--ui-space-4); }
.combat-summary__heading { display: flex; align-items: end; justify-content: space-between; gap: var(--ui-space-3); margin-bottom: var(--ui-space-3); }
.combat-summary h2 { margin-top: 3px; font-family: var(--ui-font-display); font-size: 1.15rem; }
.combat-summary__heading button { border: 0; background: transparent; color: #bcb6ff; font: inherit; font-size: var(--ui-font-size-xs); font-weight: 700; }
.stat-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1px; overflow: hidden; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: var(--ui-color-border); }
.stat-grid article { display: grid; min-height: 4.3rem; align-content: center; gap: 2px; padding: var(--ui-space-2) var(--ui-space-3); background: rgb(9 14 24 / 96%); }
.stat-grid span { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.stat-grid strong { color: var(--ui-color-text-primary); font-size: 1rem; font-variant-numeric: tabular-nums; }
.stat-grid small { color: var(--ui-color-success); font-size: .64rem; line-height: 1.2; }
.item-detail { display: grid; gap: var(--ui-space-3); }
.item-detail__hero { display: grid; grid-template-columns: 74px minmax(0, 1fr); align-items: center; gap: var(--ui-space-3); padding-bottom: var(--ui-space-3); border-bottom: 1px solid var(--ui-color-border); }
.item-detail__icon { display: grid; width: 70px; height: 70px; place-items: center; overflow: hidden; border: 1px solid var(--ui-color-border-strong); border-radius: var(--ui-radius-md); background: rgb(2 5 9 / 80%); }
.item-detail__icon img { width: 100%; height: 100%; object-fit: cover; }
.item-detail__hero > div:last-child { display: grid; gap: 2px; }
.item-detail__hero strong { font-family: var(--ui-font-display); font-size: 1.05rem; }
.item-detail__hero span { color: #bcb6ff; font-size: var(--ui-font-size-sm); font-weight: 700; }
.item-detail__hero small { color: var(--ui-color-text-muted); }
.item-detail__hero[data-rarity='Legendary'] span, .item-detail__hero[data-rarity='Unique'] span { color: var(--ui-color-gold); }
.item-meta { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1px; overflow: hidden; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: var(--ui-color-border); }
.item-meta > div { display: grid; gap: 2px; padding: var(--ui-space-2) var(--ui-space-3); background: var(--ui-color-surface-2); }
.item-meta span { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.item-meta strong { font-size: var(--ui-font-size-sm); }
.item-meta .danger { color: var(--ui-color-danger); }
.item-section { padding-top: var(--ui-space-1); }
.item-section h3 { margin: 0 0 var(--ui-space-2); color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-xs); letter-spacing: .08em; text-transform: uppercase; }
.item-section dl { display: grid; gap: 4px; margin: 0; }
.item-section dl > div { display: flex; justify-content: space-between; gap: var(--ui-space-3); padding: 5px 0; border-bottom: 1px solid rgb(255 255 255 / 4%); }
.item-section dt { color: var(--ui-color-text-secondary); }
.item-section dd { margin: 0; color: var(--ui-color-success); font-weight: 700; font-variant-numeric: tabular-nums; }
.item-section p { margin: 0; color: var(--ui-color-text-secondary); line-height: var(--ui-line-height-normal); }
.item-section--accent { padding: var(--ui-space-3); border: 1px solid rgb(232 200 102 / 22%); border-radius: var(--ui-radius-md); background: rgb(232 200 102 / 5%); }
.item-warning { margin: 0; padding: var(--ui-space-3); border: 1px solid rgb(224 103 103 / 32%); border-radius: var(--ui-radius-md); background: rgb(224 103 103 / 8%); color: #f0aaaa; }

@media (max-width: 360px) {
  .overview-v2 { padding-inline: var(--ui-space-2); }
  .paperdoll-v2 { grid-template-columns: 52px minmax(0, 1fr) 52px; gap: 5px; padding-inline: var(--ui-space-2); }
  .equipment-slot { width: 50px; min-height: 54px; }
  .equipment-slot__icon { width: 39px; height: 37px; }
  .hero-figure__art { min-height: 16.5rem; }
  .stat-grid { grid-template-columns: 1fr 1fr; }
}
</style>
