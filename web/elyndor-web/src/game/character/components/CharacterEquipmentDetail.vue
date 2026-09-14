<script setup lang="ts">
import { computed } from 'vue'

import type { CharacterSnapshot, InventoryItem } from '@/api/contracts'
import { itemArtUrl } from '@/assets/itemArt'
import {
  allowedClassesLabel,
  categoryLabel,
  formatNumber,
  formatPercent,
  itemLevel,
  itemStatRows,
  rarityLabel,
  slotLabel,
  type DetailedInventoryItem,
} from '@/game/character/equipmentPresentation'
import { ItemQualityStars } from '@/ui/components'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { GlyphName, Rarity } from '@/ui/icons/icon.types'

const props = defineProps<{
  item: InventoryItem
  character: CharacterSnapshot
}>()

const details = computed(() => props.item as DetailedInventoryItem)
const rows = computed(() => itemStatRows(props.item))
const level = computed(() => itemLevel(props.item))
const category = computed(() => categoryLabel(props.item))
const classes = computed(() => allowedClassesLabel(props.item))
const art = computed(() => itemArtUrl(props.item.iconId))
const breakdown = computed(() => props.character.statBreakdown as Record<string, { finalValue: number } | undefined>)

const impactRows = computed(() => {
  const result: { id: string; label: string; value: string; effective?: string }[] = []
  const item = details.value

  if (item.stats.armor) {
    const effective = breakdown.value.armorDamageReductionPercent?.finalValue
    result.push({
      id: 'armor',
      label: 'Броня героя',
      value: formatNumber(props.character.stats.armor),
      effective: effective === undefined ? undefined : `${formatPercent(effective)} снижения физ. урона`,
    })
  }

  if (item.stats.magicResistance) {
    const effective = breakdown.value.magicDamageReductionPercent?.finalValue
    result.push({
      id: 'magicResistance',
      label: 'Сопротивление магии',
      value: formatNumber(props.character.stats.magicResistance),
      effective: effective === undefined ? undefined : `${formatPercent(effective)} снижения маг. урона`,
    })
  }

  if ((item.blockChancePercent ?? 0) > 0) {
    const current = breakdown.value.blockChance?.finalValue
    if (current !== undefined) {
      result.push({ id: 'blockChance', label: 'Шанс блока героя', value: formatPercent(current) })
    }
  }

  if ((item.blockValueMax ?? 0) > 0) {
    const min = breakdown.value.blockValueMin?.finalValue
    const max = breakdown.value.blockValueMax?.finalValue
    if (max !== undefined) {
      result.push({
        id: 'blockValue',
        label: 'Сила блока героя',
        value: min !== undefined && min !== max ? `${formatNumber(min)}–${formatNumber(max)}` : formatNumber(max),
      })
    }
  }

  return result
})

function glyph(item: InventoryItem): GlyphName {
  if (item.slot === 'Weapon' || item.slot === 'MainHand') return 'sword'
  if (item.slot === 'OffHand') return 'shield'
  if (item.slot === 'Head') return 'helmet'
  if (item.slot === 'Boots' || item.slot === 'Feet') return 'boots'
  if (item.slot === 'Amulet' || item.slot === 'Ring1' || item.slot === 'Ring2') return 'ring'
  return 'armor'
}

function rarity(item: InventoryItem): Rarity {
  return item.rarity.toLowerCase() as Rarity
}
</script>

<template>
  <article class="equipment-detail" :data-item-rarity="item.rarity">
    <header class="equipment-detail__identity">
      <span class="equipment-detail__icon" :data-rarity="item.rarity">
        <img v-if="art" :src="art" :alt="item.name" decoding="async" />
        <IconGenerator
          v-else
          :config="{ id: `equipped-detail-${item.id}`, glyph: glyph(item), category: 'equipment', rarity: rarity(item) }"
        />
      </span>
      <div class="equipment-detail__identity-copy">
        <p>{{ rarityLabel(item) }} · {{ slotLabel(item) }}</p>
        <strong v-if="category">{{ category }}</strong>
        <ItemQualityStars
          v-if="item.generatedItem"
          :id="`equipped-detail-quality-${item.id}`"
          :stars="item.generatedItem.stars"
        />
      </div>
    </header>

    <section class="equipment-detail__requirements" aria-label="Требования предмета">
      <div data-item-level>
        <small>Уровень предмета</small>
        <strong v-if="level !== null">{{ level }}</strong>
        <strong v-else>—</strong>
      </div>
      <div data-required-level :class="{ blocked: character.level < item.requiredLevel }">
        <small>Требуется уровень</small>
        <strong>{{ item.requiredLevel }}</strong>
      </div>
      <div v-if="classes" class="equipment-detail__classes">
        <small>Класс</small>
        <strong>{{ classes }}</strong>
      </div>
    </section>

    <p v-if="level === null" class="equipment-detail__legacy-note">
      Для этого предмета старого формата Item Level ещё не назначен. Required Level показан отдельно и не используется как его замена.
    </p>

    <section v-if="item.generatedItem" class="equipment-detail__quality" aria-label="Качество предмета">
      <div>
        <small>Мощь</small>
        <strong>{{ formatNumber(item.generatedItem.itemPower) }} / {{ formatNumber(item.generatedItem.maxItemPower) }}</strong>
      </div>
      <div>
        <small>Качество ролла</small>
        <strong>{{ formatPercent(item.generatedItem.rollQuality) }}</strong>
      </div>
      <span v-if="item.generatedItem.isPerfect">Идеальный ролл</span>
    </section>

    <section v-if="rows.length" class="equipment-detail__section">
      <h3>Характеристики предмета</h3>
      <dl class="equipment-detail__stats">
        <div v-for="row in rows" :key="row.id" :data-item-stat="row.id">
          <dt>{{ row.label }}</dt>
          <dd>{{ row.value }}</dd>
        </div>
      </dl>
    </section>

    <section v-if="impactRows.length" class="equipment-detail__section equipment-detail__impact">
      <div class="equipment-detail__section-heading">
        <h3>В текущем состоянии героя</h3>
        <small>Итог с экипировкой и талантами</small>
      </div>
      <dl class="equipment-detail__stats">
        <div v-for="row in impactRows" :key="row.id" :data-current-stat="row.id">
          <dt>{{ row.label }}</dt>
          <dd>
            <strong>{{ row.value }}</strong>
            <small v-if="row.effective">{{ row.effective }}</small>
          </dd>
        </div>
      </dl>
    </section>

    <p v-if="item.weaponBaseAttackIntervalSeconds" class="equipment-detail__note">
      Базовый интервал оружия: {{ formatNumber(item.weaponBaseAttackIntervalSeconds) }} сек.
    </p>
    <p v-if="item.setId" class="equipment-detail__note">Предмет входит в комплект экипировки.</p>
    <p v-if="item.description" class="equipment-detail__description">{{ item.description }}</p>
  </article>
</template>

<style scoped>
.equipment-detail { display: grid; gap: var(--ui-space-4); }
.equipment-detail__identity { display: grid; grid-template-columns: 4.5rem minmax(0, 1fr); align-items: center; gap: var(--ui-space-3); }
.equipment-detail__icon { display: grid; width: 4.5rem; aspect-ratio: 1; place-items: center; overflow: hidden; border: 1px solid var(--ui-color-border-strong); border-radius: var(--ui-radius-md); background: rgb(2 5 9 / 72%); font-size: 2rem; }
.equipment-detail__icon[data-rarity='Rare'] { border-color: var(--ui-color-secondary); }
.equipment-detail__icon[data-rarity='Epic'] { border-color: var(--ui-color-primary); }
.equipment-detail__icon[data-rarity='Legendary'], .equipment-detail__icon[data-rarity='Unique'] { border-color: var(--ui-color-gold); box-shadow: 0 0 16px rgb(232 200 102 / 12%); }
.equipment-detail__icon img { width: 100%; height: 100%; object-fit: cover; }
.equipment-detail__identity-copy { display: grid; min-width: 0; gap: 4px; }
.equipment-detail__identity-copy p, .equipment-detail__identity-copy strong { margin: 0; }
.equipment-detail__identity-copy p { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.equipment-detail__identity-copy > strong { color: var(--ui-color-text-primary); font-size: var(--ui-font-size-sm); }
.equipment-detail__requirements { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1px; overflow: hidden; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: var(--ui-color-border); }
.equipment-detail__requirements > div { display: grid; gap: 2px; padding: var(--ui-space-3); background: var(--ui-color-surface-2); }
.equipment-detail__requirements small, .equipment-detail__quality small { color: var(--ui-color-text-muted); font-size: .58rem; font-weight: 700; letter-spacing: .05em; text-transform: uppercase; }
.equipment-detail__requirements strong { color: var(--ui-color-text-primary); font-size: var(--ui-font-size-md); }
.equipment-detail__requirements .blocked strong { color: var(--ui-color-danger); }
.equipment-detail__classes { grid-column: 1 / -1; }
.equipment-detail__legacy-note { margin: calc(var(--ui-space-2) * -1) 0 0; color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); line-height: 1.4; }
.equipment-detail__quality { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--ui-space-2); padding: var(--ui-space-3); border: 1px solid rgb(232 200 102 / 20%); border-radius: var(--ui-radius-md); background: rgb(232 200 102 / 5%); }
.equipment-detail__quality > div { display: grid; gap: 2px; }
.equipment-detail__quality > span { grid-column: 1 / -1; color: var(--ui-color-gold); font-size: var(--ui-font-size-xs); font-weight: 800; letter-spacing: .08em; text-transform: uppercase; }
.equipment-detail__section { display: grid; gap: var(--ui-space-2); }
.equipment-detail__section h3 { margin: 0; color: var(--ui-color-text-primary); font-size: var(--ui-font-size-sm); }
.equipment-detail__section-heading { display: flex; align-items: baseline; justify-content: space-between; gap: var(--ui-space-2); }
.equipment-detail__section-heading small { color: var(--ui-color-text-muted); font-size: .58rem; }
.equipment-detail__stats { display: grid; gap: 1px; margin: 0; overflow: hidden; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: var(--ui-color-border); }
.equipment-detail__stats > div { display: flex; align-items: center; justify-content: space-between; gap: var(--ui-space-3); min-height: 2.45rem; padding: 7px var(--ui-space-3); background: var(--ui-color-surface-2); }
.equipment-detail__stats dt { color: var(--ui-color-text-secondary); }
.equipment-detail__stats dd { display: grid; justify-items: end; gap: 1px; margin: 0; color: var(--ui-color-success); font-variant-numeric: tabular-nums; text-align: right; }
.equipment-detail__stats dd small { color: var(--ui-color-success); font-size: .62rem; }
.equipment-detail__impact { padding-top: var(--ui-space-2); border-top: 1px solid var(--ui-color-border); }
.equipment-detail__impact .equipment-detail__stats dd { color: var(--ui-color-primary); }
.equipment-detail__note, .equipment-detail__description { margin: 0; color: var(--ui-color-text-muted); font-size: var(--ui-font-size-sm); line-height: 1.45; }
.equipment-detail__description { padding: var(--ui-space-3); border-left: 2px solid rgb(205 177 113 / 28%); background: rgb(255 255 255 / 2%); color: var(--ui-color-text-secondary); }
</style>
