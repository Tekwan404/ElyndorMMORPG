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

type RuntimeInventoryItem = InventoryItem & {
  itemLevel?: number | null
  blockChancePercent?: number
  blockValueMin?: number
  blockValueMax?: number
  weaponDamageMin?: number | null
  weaponDamageMax?: number | null
}

interface PaperdollSlot {
  id: string
  inventorySlot: EquipmentSlot
  label: string
  glyph: GlyphName
  item: InventoryItem | null
  side: 'left' | 'right'
}

interface CharacterStatRow {
  id: string
  label: string
  value: string
  effective?: string
  primary?: boolean
}

interface CharacterStatSection {
  id: string
  title: string
  rows: CharacterStatRow[]
}

interface ItemStatRow {
  id: string
  label: string
  value: string
  tone?: 'normal' | 'accent'
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

function breakdownValue(id: string): number | null {
  const breakdown = character.value?.statBreakdown as Record<string, { finalValue: number }> | undefined
  return breakdown?.[id]?.finalValue ?? null
}

function formatNumber(value: number): string {
  const rounded = Math.round(value * 100) / 100
  return Number.isInteger(rounded) ? String(rounded) : rounded.toFixed(2).replace(/0+$/, '').replace(/\.$/, '')
}

function formatPercent(value: number): string {
  return `${formatNumber(value)}%`
}

function primaryAttributeId(): keyof Pick<CharacterStats, 'strength' | 'agility' | 'intellect'> | null {
  const current = character.value
  if (!current) return null
  if (current.primaryAttribute === 'STRENGTH') return 'strength'
  if (current.primaryAttribute === 'AGILITY') return 'agility'
  return 'intellect'
}

function resourceLabel(type: string): string {
  if (type === 'RAGE') return 'Ярость'
  if (type === 'FOCUS') return 'Концентрация'
  if (type === 'MANA') return 'Мана'
  return type
}

const statSections = computed<CharacterStatSection[]>(() => {
  const current = character.value
  if (!current) return []
  const stats = current.stats
  const primary = primaryAttributeId()
  const armorReduction = breakdownValue('armorDamageReductionPercent')
  const magicReduction = breakdownValue('magicDamageReductionPercent')
  const blockChance = breakdownValue('blockChance')
  const blockMin = breakdownValue('blockValueMin')
  const blockMax = breakdownValue('blockValueMax')

  const defenseRows: CharacterStatRow[] = [
    {
      id: 'armor',
      label: 'Броня',
      value: formatNumber(stats.armor),
      effective: armorReduction === null ? undefined : `${formatPercent(armorReduction)} физ. снижения`,
    },
    {
      id: 'magicResistance',
      label: 'Сопротивление магии',
      value: formatNumber(stats.magicResistance),
      effective: magicReduction === null ? undefined : `${formatPercent(magicReduction)} маг. снижения`,
    },
    { id: 'dodge', label: 'Уклонение', value: formatPercent(stats.dodge) },
  ]

  if (blockChance !== null) {
    defenseRows.push({ id: 'blockChance', label: 'Шанс блока', value: formatPercent(blockChance) })
  }
  if (blockMin !== null || blockMax !== null) {
    defenseRows.push({
      id: 'blockValue',
      label: 'Сила блока',
      value: `${formatNumber(blockMin ?? 0)}–${formatNumber(blockMax ?? blockMin ?? 0)}`,
    })
  }

  return [
    {
      id: 'primary',
      title: 'Основные',
      rows: [
        { id: 'hp', label: 'Здоровье', value: `${formatNumber(current.vitals.currentHp)} / ${formatNumber(current.vitals.maxHp)}` },
        { id: 'resource', label: resourceLabel(current.vitals.resourceType), value: `${formatNumber(current.vitals.currentResource)} / ${formatNumber(current.vitals.maxResource)}` },
        { id: 'strength', label: 'Сила', value: formatNumber(stats.strength), primary: primary === 'strength' },
        { id: 'agility', label: 'Ловкость', value: formatNumber(stats.agility), primary: primary === 'agility' },
        { id: 'intellect', label: 'Интеллект', value: formatNumber(stats.intellect), primary: primary === 'intellect' },
        { id: 'stamina', label: 'Выносливость', value: formatNumber(stats.stamina) },
      ],
    },
    {
      id: 'offense',
      title: 'Атака',
      rows: [
        { id: 'attackPower', label: 'Сила атаки', value: formatNumber(stats.attackPower) },
        { id: 'spellPower', label: 'Сила заклинаний', value: formatNumber(stats.spellPower) },
        { id: 'criticalChance', label: 'Крит. шанс', value: formatPercent(stats.criticalChance) },
        { id: 'criticalDamage', label: 'Крит. урон', value: formatPercent(stats.criticalDamage) },
        { id: 'accuracy', label: 'Меткость', value: formatPercent(stats.accuracy) },
        { id: 'armorPenetration', label: 'Пробивание брони', value: formatPercent(stats.armorPenetration) },
        { id: 'magicPenetration', label: 'Пробивание магии', value: formatPercent(stats.magicPenetration) },
        { id: 'attackSpeed', label: 'Скорость атаки', value: `${formatNumber(stats.attackSpeed)}×` },
      ],
    },
    { id: 'defense', title: 'Защита', rows: defenseRows },
  ]
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
    Common: 'Обычный',
    Uncommon: 'Необычный',
    Rare: 'Редкий',
    Epic: 'Эпический',
    Legendary: 'Легендарный',
    Unique: 'Уникальный',
  }
  return labels[item.rarity] ?? item.rarity
}

function slotLabel(slot: EquipmentSlot | null): string {
  if (!slot) return 'Не экипируется'
  const labels: Partial<Record<EquipmentSlot, string>> = {
    MainHand: 'Основная рука',
    OffHand: 'Вторая рука',
    Head: 'Шлем',
    Shoulders: 'Наплечники',
    Chest: 'Нагрудник',
    Hands: 'Перчатки',
    Legs: 'Поножи',
    Feet: 'Обувь',
    Cloak: 'Плащ',
    Amulet: 'Амулет',
    Ring1: 'Кольцо',
    Ring2: 'Кольцо',
    Weapon: 'Оружие',
    Boots: 'Обувь',
    Accessory: 'Аксессуар',
  }
  return labels[slot] ?? slot
}

function className(id: string): string {
  if (id === 'WARRIOR' || id === 'ARCHER' || id === 'MAGE') return classLabel(id)
  return id
}

function classRestriction(item: InventoryItem): string {
  if (!item.allowedClassIds.length) return 'Все классы'
  return item.allowedClassIds.map(className).join(', ')
}

function categoryLabel(item: InventoryItem): string | null {
  const value = item.weaponCategory ?? item.armorCategory
  if (!value) return null
  const labels: Record<string, string> = {
    ONE_HAND_SWORD: 'Одноручный меч',
    TWO_HAND_SWORD: 'Двуручный меч',
    AXE: 'Топор',
    MACE: 'Булава',
    BOW: 'Лук',
    DAGGER: 'Кинжал',
    STAFF: 'Посох',
    WAND: 'Жезл',
    HEAVY: 'Тяжёлая броня',
    LEATHER: 'Кожаная броня',
    CLOTH: 'Тканевая броня',
  }
  return labels[value] ?? value
}

const itemStatLabels: Record<keyof CharacterStats | 'maxResource', string> = {
  strength: 'Сила',
  agility: 'Ловкость',
  intellect: 'Интеллект',
  stamina: 'Выносливость',
  maxHp: 'Здоровье',
  attackPower: 'Сила атаки',
  spellPower: 'Сила заклинаний',
  criticalChance: 'Крит. шанс',
  criticalDamage: 'Крит. урон',
  accuracy: 'Меткость',
  armorPenetration: 'Пробивание брони',
  magicPenetration: 'Пробивание магии',
  attackSpeed: 'Скорость атаки',
  armor: 'Броня',
  magicResistance: 'Сопротивление магии',
  dodge: 'Уклонение',
  maxResource: 'Макс. ресурс',
}

const percentItemStats = new Set([
  'criticalChance',
  'criticalDamage',
  'accuracy',
  'armorPenetration',
  'magicPenetration',
  'attackSpeed',
  'dodge',
])

function itemStats(item: InventoryItem): ItemStatRow[] {
  const stats = item.stats as unknown as Record<string, number>
  return Object.entries(itemStatLabels)
    .map(([id, label]) => ({ id, label, raw: stats[id] ?? 0 }))
    .filter(row => row.raw !== 0)
    .map(row => ({
      id: row.id,
      label: row.label,
      value: `${row.raw > 0 ? '+' : ''}${formatNumber(row.raw)}${percentItemStats.has(row.id) ? '%' : ''}`,
      tone: 'accent' as const,
    }))
}

function runtimeItem(item: InventoryItem): RuntimeInventoryItem {
  return item as RuntimeInventoryItem
}

function itemLevel(item: InventoryItem): number | null {
  const runtime = runtimeItem(item)
  return runtime.itemLevel ?? item.generatedItem?.itemLevel ?? null
}

function weaponDamage(item: InventoryItem): string | null {
  const runtime = runtimeItem(item)
  if (runtime.weaponDamageMin === null || runtime.weaponDamageMin === undefined) return null
  if (runtime.weaponDamageMax === null || runtime.weaponDamageMax === undefined) return null
  return `${formatNumber(runtime.weaponDamageMin)}–${formatNumber(runtime.weaponDamageMax)}`
}

function blockChance(item: InventoryItem): number {
  return runtimeItem(item).blockChancePercent ?? 0
}

function blockValue(item: InventoryItem): string | null {
  const runtime = runtimeItem(item)
  const min = runtime.blockValueMin ?? 0
  const max = runtime.blockValueMax ?? 0
  if (min === 0 && max === 0) return null
  return `${formatNumber(min)}–${formatNumber(max || min)}`
}

function bindLabel(item: InventoryItem): string | null {
  if (!item.bindState) return null
  if (item.bindState === 'BOUND') return 'Привязан'
  if (item.bindState === 'UNBOUND') return 'Не привязан'
  return item.bindState
}

function affixLabel(statId: string): string {
  const normalized = statId.toUpperCase()
  const labels: Record<string, string> = {
    STRENGTH: 'Сила',
    AGILITY: 'Ловкость',
    INTELLECT: 'Интеллект',
    STAMINA: 'Выносливость',
    MAX_HP: 'Здоровье',
    ATTACK_POWER: 'Сила атаки',
    SPELL_POWER: 'Сила заклинаний',
    CRITICAL_CHANCE: 'Крит. шанс',
    CRITICAL_DAMAGE: 'Крит. урон',
    ACCURACY: 'Меткость',
    ARMOR: 'Броня',
    MAGIC_RESISTANCE: 'Сопротивление магии',
    DODGE: 'Уклонение',
    ARMOR_PENETRATION: 'Пробивание брони',
    MAGIC_PENETRATION: 'Пробивание магии',
    ATTACK_SPEED: 'Скорость атаки',
    BLOCK_CHANCE: 'Шанс блока',
    BLOCK_VALUE: 'Сила блока',
  }
  return labels[normalized] ?? statId
}

function affixValue(statId: string, value: number): string {
  const percentStats = ['CRITICAL_CHANCE', 'CRITICAL_DAMAGE', 'ACCURACY', 'DODGE', 'ARMOR_PENETRATION', 'MAGIC_PENETRATION', 'ATTACK_SPEED', 'BLOCK_CHANCE']
  return `+${formatNumber(value)}${percentStats.includes(statId.toUpperCase()) ? '%' : ''}`
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
        <div class="hero-card__status" aria-label="Заполненные слоты экипировки">
          <span>{{ equippedCount }}/{{ equipment.length }}</span>
          <small>слотов</small>
        </div>
      </header>

      <div class="paperdoll-v2">
        <div class="equipment-column" aria-label="Снаряжение слева">
          <button
            v-for="slot in leftEquipment"
            :key="slot.id"
            type="button"
            class="equipment-slot"
            :class="{ filled: slot.item }"
            :data-equipment-slot="slot.id"
            :data-filled="Boolean(slot.item)"
            :data-rarity="slot.item?.rarity"
            :aria-label="`${slot.label}: ${slot.item?.name ?? 'пусто'}`"
            @click="openEquipmentSlot(slot)"
          >
            <span class="equipment-slot__icon">
              <img v-if="itemArt(slot.item)" :src="itemArt(slot.item)" :alt="slot.item?.name ?? slot.label" loading="lazy" decoding="async" />
              <IconGenerator
                v-else
                :config="{ id: `paperdoll-v2-${slot.id}`, glyph: itemGlyph(slot.item, slot.glyph), category: 'equipment', rarity: itemRarity(slot.item) }"
              />
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
          <button
            v-for="slot in rightEquipment"
            :key="slot.id"
            type="button"
            class="equipment-slot"
            :class="{ filled: slot.item }"
            :data-equipment-slot="slot.id"
            :data-filled="Boolean(slot.item)"
            :data-rarity="slot.item?.rarity"
            :aria-label="`${slot.label}: ${slot.item?.name ?? 'пусто'}`"
            @click="openEquipmentSlot(slot)"
          >
            <span class="equipment-slot__icon">
              <img v-if="itemArt(slot.item)" :src="itemArt(slot.item)" :alt="slot.item?.name ?? slot.label" loading="lazy" decoding="async" />
              <IconGenerator
                v-else
                :config="{ id: `paperdoll-v2-${slot.id}`, glyph: itemGlyph(slot.item, slot.glyph), category: 'equipment', rarity: itemRarity(slot.item) }"
              />
            </span>
            <small>{{ slot.label }}</small>
          </button>
        </div>
      </div>
    </section>

    <section class="combat-sheet">
      <header class="combat-sheet__heading">
        <div>
          <p class="eyebrow">Характеристики</p>
          <h2>Боевые показатели</h2>
          <small>Итоговые значения из текущего серверного состояния.</small>
        </div>
        <button type="button" data-open-full-stats @click="emit('open-stats')">Разбор ›</button>
      </header>

      <div class="stat-sections">
        <section v-for="section in statSections" :key="section.id" class="stat-section" :data-stat-section="section.id">
          <h3>{{ section.title }}</h3>
          <dl>
            <div v-for="row in section.rows" :key="row.id" class="stat-row" :class="{ primary: row.primary }" :data-stat="row.id">
              <dt>
                {{ row.label }}
                <small v-if="row.primary">основная</small>
              </dt>
              <dd>
                <strong>{{ row.value }}</strong>
                <small v-if="row.effective">{{ row.effective }}</small>
              </dd>
            </div>
          </dl>
        </section>
      </div>
    </section>

    <UIModal :open="selectedItem !== null" :title="selectedItem?.name ?? ''" @close="closeItem">
      <article v-if="selectedItem" class="item-detail">
        <header class="item-detail__hero" :data-rarity="selectedItem.rarity">
          <div class="item-detail__icon">
            <img v-if="itemArt(selectedItem)" :src="itemArt(selectedItem)" :alt="selectedItem.name" />
            <IconGenerator
              v-else
              :config="{ id: 'selected-equipment', glyph: itemGlyph(selectedItem, 'armor'), category: 'equipment', rarity: itemRarity(selectedItem) }"
            />
          </div>
          <div class="item-detail__identity">
            <strong>{{ selectedItem.name }}</strong>
            <span>{{ rarityLabel(selectedItem) }}</span>
            <small>{{ slotLabel(selectedItem.slot) }}<template v-if="categoryLabel(selectedItem)"> · {{ categoryLabel(selectedItem) }}</template></small>
          </div>
        </header>

        <section class="item-meta" aria-label="Требования предмета">
          <div>
            <span>Уровень предмета</span>
            <strong>{{ itemLevel(selectedItem) ?? '—' }}</strong>
          </div>
          <div>
            <span>Требуется уровень</span>
            <strong :class="{ danger: character.level < selectedItem.requiredLevel }">{{ selectedItem.requiredLevel }}</strong>
          </div>
          <div>
            <span>Класс</span>
            <strong>{{ classRestriction(selectedItem) }}</strong>
          </div>
          <div v-if="bindLabel(selectedItem)">
            <span>Состояние</span>
            <strong>{{ bindLabel(selectedItem) }}</strong>
          </div>
        </section>

        <section v-if="itemStats(selectedItem).length" class="item-section">
          <h3>Характеристики</h3>
          <dl class="item-stat-list">
            <div v-for="row in itemStats(selectedItem)" :key="row.id">
              <dt>{{ row.label }}</dt>
              <dd :class="{ accent: row.tone === 'accent' }">{{ row.value }}</dd>
            </div>
          </dl>
        </section>

        <section v-if="weaponDamage(selectedItem) || selectedItem.weaponBaseAttackIntervalSeconds" class="item-section">
          <h3>Оружие</h3>
          <dl class="item-stat-list">
            <div v-if="weaponDamage(selectedItem)"><dt>Урон оружия</dt><dd>{{ weaponDamage(selectedItem) }}</dd></div>
            <div v-if="selectedItem.weaponBaseAttackIntervalSeconds"><dt>Базовый интервал атаки</dt><dd>{{ formatNumber(selectedItem.weaponBaseAttackIntervalSeconds) }} сек.</dd></div>
          </dl>
        </section>

        <section v-if="blockChance(selectedItem) > 0 || blockValue(selectedItem)" class="item-section item-section--shield">
          <h3>Блок</h3>
          <dl class="item-stat-list">
            <div v-if="blockChance(selectedItem) > 0"><dt>Шанс блока</dt><dd class="accent">{{ formatPercent(blockChance(selectedItem)) }}</dd></div>
            <div v-if="blockValue(selectedItem)"><dt>Сила блока</dt><dd class="accent">{{ blockValue(selectedItem) }}</dd></div>
          </dl>
        </section>

        <section v-if="selectedItem.generatedItem" class="item-section item-section--quality">
          <div class="item-section__title-row">
            <h3>Качество экземпляра</h3>
            <span v-if="selectedItem.generatedItem.isPerfect">Идеальный ролл</span>
          </div>
          <dl class="item-stat-list">
            <div><dt>Сила предмета</dt><dd>{{ formatNumber(selectedItem.generatedItem.itemPower) }} / {{ formatNumber(selectedItem.generatedItem.maxItemPower) }}</dd></div>
            <div><dt>Качество ролла</dt><dd>{{ formatPercent(selectedItem.generatedItem.rollQuality) }}</dd></div>
            <div><dt>Звёзды</dt><dd>{{ selectedItem.generatedItem.stars }} / 5</dd></div>
          </dl>

          <div v-if="selectedItem.generatedItem.affixes.length" class="affix-list">
            <h4>Аффиксы</h4>
            <div v-for="affix in selectedItem.generatedItem.affixes" :key="affix.slotKey">
              <span>{{ affixLabel(affix.statId) }}</span>
              <strong>{{ affixValue(affix.statId, affix.value) }}</strong>
              <small>Тир {{ affix.affixTier }}<template v-if="affix.isGuaranteed"> · гарантированный</template></small>
            </div>
          </div>
        </section>

        <section v-if="selectedItem.setId" class="item-section item-section--set">
          <h3>Комплект</h3>
          <p>{{ selectedItem.setId }}</p>
        </section>

        <section v-if="selectedItem.description" class="item-section item-section--description">
          <h3>Описание</h3>
          <p>{{ selectedItem.description }}</p>
        </section>

        <p v-if="itemLevel(selectedItem) === null" class="item-note">
          У этого экземпляра сервер пока не передал Item Level. Required Level показан отдельно и не используется как подмена уровня предмета.
        </p>
        <p v-if="character.level < selectedItem.requiredLevel" class="item-warning">
          Требуется {{ selectedItem.requiredLevel }} уровень. Сейчас у героя {{ character.level }}.
        </p>
        <p v-if="equipmentErrorMessage(equipmentActionError)" class="item-warning" role="alert">
          {{ equipmentErrorMessage(equipmentActionError) }}
        </p>
      </article>

      <template #actions>
        <UIButton
          v-if="selectedEquipmentSlot"
          data-unequip-selected
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
.overview-v2 {
  display: grid;
  width: min(100%, var(--ui-content-width));
  margin-inline: auto;
  gap: var(--ui-space-3);
  padding: var(--ui-space-3) var(--ui-space-3) var(--ui-space-7);
}

.hero-card,
.combat-sheet {
  overflow: hidden;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: calc(var(--ui-radius-lg) + 2px);
  background: linear-gradient(180deg, rgb(14 20 33 / 98%), rgb(6 10 17 / 98%));
  box-shadow: var(--ui-shadow-inset), var(--ui-shadow-elevated);
}

.hero-card {
  background:
    radial-gradient(circle at 50% 35%, rgb(146 136 255 / 16%), transparent 15rem),
    linear-gradient(180deg, rgb(14 20 33 / 98%), rgb(6 10 17 / 98%));
}

.hero-card__header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: var(--ui-space-3);
  padding: var(--ui-space-4) var(--ui-space-4) var(--ui-space-2);
}

.hero-card__header h1,
.hero-card__header p,
.combat-sheet h2,
.combat-sheet p {
  margin: 0;
}

.hero-card__header h1 {
  font-family: var(--ui-font-display);
  font-size: clamp(1.5rem, 7vw, 2.15rem);
}

.hero-card__header > div > p:last-child {
  margin-top: 3px;
  color: var(--ui-color-text-secondary);
  font-size: var(--ui-font-size-xs);
}

.eyebrow {
  color: #bcb6ff;
  font-size: .6rem;
  font-weight: 800;
  letter-spacing: .11em;
  text-transform: uppercase;
}

.hero-card__status {
  display: grid;
  justify-items: end;
}

.hero-card__status span {
  color: var(--ui-color-gold);
  font-size: 1.1rem;
  font-weight: 800;
}

.hero-card__status small {
  color: var(--ui-color-text-muted);
  font-size: .62rem;
}

.paperdoll-v2 {
  display: grid;
  grid-template-columns: 58px minmax(0, 1fr) 58px;
  align-items: center;
  gap: 8px;
  min-height: 22rem;
  padding: var(--ui-space-2) var(--ui-space-3) var(--ui-space-4);
}

.equipment-column {
  display: grid;
  gap: 8px;
}

.equipment-slot {
  display: grid;
  width: 56px;
  min-height: 58px;
  place-items: center;
  gap: 2px;
  padding: 3px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background:
    radial-gradient(circle at 50% 18%, rgb(255 255 255 / 4%), transparent 60%),
    rgb(4 8 14 / 90%);
  box-shadow: inset 0 0 0 1px rgb(255 255 255 / 2%);
  color: var(--ui-color-text-muted);
  font: inherit;
}

.equipment-slot:not(.filled) {
  opacity: .5;
}

.equipment-slot.filled {
  color: var(--ui-color-text-primary);
}

.equipment-slot[data-rarity='Uncommon'] { border-color: color-mix(in srgb, var(--ui-color-success) 68%, var(--ui-color-border)); }
.equipment-slot[data-rarity='Rare'] { border-color: #4d8ee8; }
.equipment-slot[data-rarity='Epic'] { border-color: #9a70e8; box-shadow: 0 0 12px rgb(154 112 232 / 12%); }
.equipment-slot[data-rarity='Legendary'],
.equipment-slot[data-rarity='Unique'] { border-color: var(--ui-color-gold); box-shadow: 0 0 14px rgb(232 200 102 / 14%); }

.equipment-slot__icon {
  display: grid;
  width: 44px;
  height: 40px;
  place-items: center;
  overflow: hidden;
  border-radius: calc(var(--ui-radius-md) - 2px);
  background: rgb(2 5 9 / 70%);
}

.equipment-slot__icon img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.equipment-slot small {
  width: 100%;
  overflow: hidden;
  font-size: .43rem;
  font-weight: 800;
  text-align: center;
  text-overflow: ellipsis;
  text-transform: uppercase;
  white-space: nowrap;
}

.hero-figure {
  display: grid;
  min-width: 0;
  align-self: stretch;
  align-content: end;
  justify-items: center;
  padding-bottom: 2px;
  text-align: center;
}

.hero-figure__art {
  position: relative;
  display: grid;
  width: 100%;
  min-height: 17.5rem;
  place-items: end center;
}

.hero-figure__art::before {
  position: absolute;
  inset: 14% 4% 7%;
  border-radius: 50%;
  background: radial-gradient(circle, rgb(112 96 230 / 15%), transparent 65%);
  content: '';
}

.hero-figure__art img {
  position: relative;
  z-index: 1;
  width: 100%;
  max-height: 18.5rem;
  object-fit: contain;
  object-position: center bottom;
  filter: drop-shadow(0 12px 14px rgb(0 0 0 / 42%));
}

.hero-figure__fallback {
  display: grid;
  width: 8rem;
  height: 13rem;
  place-items: center;
  border: 1px solid var(--ui-color-border);
  border-radius: 45% 45% 30% 30%;
  background: rgb(20 27 43 / 80%);
  font-size: 3rem;
}

.hero-figure > strong {
  font-family: var(--ui-font-display);
}

.hero-figure > small {
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
}

.combat-sheet {
  padding: var(--ui-space-4);
}

.combat-sheet__heading {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: var(--ui-space-3);
  margin-bottom: var(--ui-space-3);
}

.combat-sheet__heading > div {
  display: grid;
  gap: 2px;
}

.combat-sheet h2 {
  font-family: var(--ui-font-display);
  font-size: 1.2rem;
}

.combat-sheet__heading small {
  color: var(--ui-color-text-muted);
  font-size: .62rem;
}

.combat-sheet__heading button {
  min-height: var(--ui-touch-target);
  padding: 0;
  border: 0;
  background: transparent;
  color: #bcb6ff;
  font: inherit;
  font-size: var(--ui-font-size-xs);
  font-weight: 700;
  white-space: nowrap;
}

.stat-sections {
  display: grid;
  gap: var(--ui-space-3);
}

.stat-section {
  overflow: hidden;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: rgb(6 10 18 / 62%);
}

.stat-section h3 {
  margin: 0;
  padding: 8px var(--ui-space-3);
  border-bottom: 1px solid var(--ui-color-border);
  color: var(--ui-color-text-secondary);
  font-size: .62rem;
  letter-spacing: .08em;
  text-transform: uppercase;
}

.stat-section dl,
.item-stat-list {
  margin: 0;
}

.stat-row,
.item-stat-list > div {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--ui-space-3);
  min-height: 2.65rem;
  padding: 6px var(--ui-space-3);
  border-bottom: 1px solid rgb(255 255 255 / 4%);
}

.stat-row:last-child,
.item-stat-list > div:last-child {
  border-bottom: 0;
}

.stat-row.primary {
  background: linear-gradient(90deg, rgb(146 136 255 / 10%), transparent);
}

.stat-row dt,
.item-stat-list dt {
  color: var(--ui-color-text-secondary);
  font-size: var(--ui-font-size-sm);
}

.stat-row dt small {
  margin-left: 5px;
  color: #bcb6ff;
  font-size: .53rem;
  font-weight: 700;
  text-transform: uppercase;
}

.stat-row dd,
.item-stat-list dd {
  display: grid;
  justify-items: end;
  gap: 1px;
  margin: 0;
  text-align: right;
}

.stat-row dd strong {
  color: var(--ui-color-text-primary);
  font-variant-numeric: tabular-nums;
}

.stat-row dd small {
  color: var(--ui-color-success);
  font-size: .58rem;
  white-space: nowrap;
}

.item-detail {
  display: grid;
  gap: var(--ui-space-3);
}

.item-detail__hero {
  display: grid;
  grid-template-columns: 72px minmax(0, 1fr);
  align-items: center;
  gap: var(--ui-space-3);
  padding: var(--ui-space-3);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-lg);
  background: linear-gradient(135deg, rgb(146 136 255 / 8%), rgb(8 12 20 / 92%));
}

.item-detail__hero[data-rarity='Legendary'],
.item-detail__hero[data-rarity='Unique'] {
  border-color: color-mix(in srgb, var(--ui-color-gold) 65%, var(--ui-color-border));
  background: linear-gradient(135deg, rgb(232 200 102 / 9%), rgb(8 12 20 / 92%));
}

.item-detail__hero[data-rarity='Epic'] {
  border-color: color-mix(in srgb, var(--ui-color-primary) 64%, var(--ui-color-border));
}

.item-detail__icon {
  display: grid;
  width: 72px;
  height: 72px;
  place-items: center;
  overflow: hidden;
  border: 1px solid rgb(255 255 255 / 10%);
  border-radius: var(--ui-radius-md);
  background: rgb(2 5 9 / 78%);
  font-size: 2rem;
}

.item-detail__icon img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.item-detail__identity {
  display: grid;
  min-width: 0;
  gap: 3px;
}

.item-detail__identity strong {
  font-family: var(--ui-font-display);
  font-size: 1.05rem;
  line-height: 1.15;
}

.item-detail__identity span {
  color: var(--ui-color-gold);
  font-size: var(--ui-font-size-sm);
  font-weight: 700;
}

.item-detail__identity small {
  color: var(--ui-color-text-muted);
}

.item-meta {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1px;
  overflow: hidden;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: var(--ui-color-border);
}

.item-meta > div {
  display: grid;
  gap: 2px;
  padding: 9px var(--ui-space-3);
  background: var(--ui-color-surface-2);
}

.item-meta span {
  color: var(--ui-color-text-muted);
  font-size: .58rem;
  text-transform: uppercase;
}

.item-meta strong {
  overflow-wrap: anywhere;
  font-size: var(--ui-font-size-sm);
}

.item-meta strong.danger {
  color: var(--ui-color-danger);
}

.item-section {
  overflow: hidden;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: rgb(7 11 19 / 72%);
}

.item-section h3,
.item-section h4,
.item-section p {
  margin: 0;
}

.item-section > h3,
.item-section__title-row {
  padding: 8px var(--ui-space-3);
  border-bottom: 1px solid var(--ui-color-border);
}

.item-section h3 {
  color: var(--ui-color-text-secondary);
  font-size: .68rem;
  letter-spacing: .07em;
  text-transform: uppercase;
}

.item-stat-list dd {
  color: var(--ui-color-text-primary);
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}

.item-stat-list dd.accent {
  color: var(--ui-color-success);
}

.item-section--shield {
  border-color: color-mix(in srgb, var(--ui-color-secondary) 45%, var(--ui-color-border));
}

.item-section--quality {
  border-color: color-mix(in srgb, var(--ui-color-primary) 45%, var(--ui-color-border));
}

.item-section__title-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-2);
}

.item-section__title-row span {
  color: var(--ui-color-gold);
  font-size: .56rem;
  font-weight: 800;
  text-transform: uppercase;
}

.affix-list {
  display: grid;
  gap: 1px;
  border-top: 1px solid var(--ui-color-border);
}

.affix-list h4 {
  padding: 8px var(--ui-space-3) 5px;
  color: var(--ui-color-text-muted);
  font-size: .6rem;
  text-transform: uppercase;
}

.affix-list > div {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 2px var(--ui-space-2);
  padding: 7px var(--ui-space-3);
  background: rgb(146 136 255 / 4%);
}

.affix-list span {
  color: var(--ui-color-text-secondary);
  font-size: var(--ui-font-size-sm);
}

.affix-list strong {
  color: var(--ui-color-success);
  font-size: var(--ui-font-size-sm);
}

.affix-list small {
  grid-column: 1 / -1;
  color: var(--ui-color-text-muted);
  font-size: .55rem;
}

.item-section--set {
  border-color: color-mix(in srgb, var(--ui-color-gold) 36%, var(--ui-color-border));
}

.item-section--set p,
.item-section--description p {
  padding: var(--ui-space-3);
  color: var(--ui-color-text-secondary);
  line-height: var(--ui-line-height-normal);
  white-space: pre-line;
}

.item-note,
.item-warning {
  margin: 0;
  padding: var(--ui-space-3);
  border-radius: var(--ui-radius-md);
  font-size: var(--ui-font-size-sm);
  line-height: var(--ui-line-height-normal);
}

.item-note {
  border: 1px solid var(--ui-color-border);
  background: rgb(255 255 255 / 2%);
  color: var(--ui-color-text-muted);
}

.item-warning {
  border: 1px solid color-mix(in srgb, var(--ui-color-danger) 48%, var(--ui-color-border));
  background: color-mix(in srgb, var(--ui-color-danger) 8%, transparent);
  color: var(--ui-color-danger);
}

@media (max-width: 370px) {
  .paperdoll-v2 {
    grid-template-columns: 52px minmax(0, 1fr) 52px;
    gap: 5px;
    padding-inline: 8px;
  }

  .equipment-slot {
    width: 50px;
    min-height: 54px;
  }

  .equipment-slot__icon {
    width: 40px;
    height: 37px;
  }

  .hero-figure__art {
    min-height: 16rem;
  }

  .item-meta {
    grid-template-columns: 1fr;
  }
}
</style>
