<script setup lang="ts">
import { computed, ref } from 'vue'

import type { EquipmentSlot, InventoryItem, KnownAbility } from '@/api/contracts'
import { gameArt } from '@/assets/gameArt'
import { itemArtUrl } from '@/assets/itemArt'
import {
  abilityDescription,
  abilityName,
  abilityTargetLabel,
  abilityTypeLabel,
  classLabel,
  raceLabel,
} from '@/game/character/characterPresentation'
import { resolveAbilityArt } from '@/game/talents/talentArt'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIModal, UIPanel } from '@/ui/components'

const emit = defineEmits<{
  'select-empty-slot': [slot: EquipmentSlot]
}>()

const session = useGameSessionStore()
const character = computed(() => session.snapshot?.character)
const selectedItem = ref<InventoryItem | null>(null)
const selectedEquipmentSlot = ref<EquipmentSlot | null>(null)
const equipmentActionError = ref<string | null>(null)
const selectedAbility = ref<KnownAbility | null>(null)

type PaperdollSide = 'left' | 'right'

interface PaperdollSlot {
  id: string
  inventorySlot: EquipmentSlot
  label: string
  item: InventoryItem | null
  glyph: string
  side: PaperdollSide
}

const equipment = computed<PaperdollSlot[]>(() => {
  const equipped = character.value?.inventory.equipped
  return [
    { id: 'head', inventorySlot: 'Head', label: 'Шлем', item: equipped?.head ?? null, glyph: '◈', side: 'left' },
    { id: 'cloak', inventorySlot: 'Cloak', label: 'Плащ', item: equipped?.cloak ?? null, glyph: '◒', side: 'left' },
    {
      id: 'mainHand',
      inventorySlot: 'MainHand',
      label: 'Основная рука',
      item: equipped?.mainHand ?? equipped?.weapon ?? null,
      glyph: '⚔',
      side: 'left',
    },
    { id: 'hands', inventorySlot: 'Hands', label: 'Перчатки', item: equipped?.hands ?? null, glyph: '◫', side: 'left' },
    { id: 'ring1', inventorySlot: 'Ring1', label: 'Кольцо I', item: equipped?.ring1 ?? null, glyph: '✧', side: 'left' },
    { id: 'chest', inventorySlot: 'Chest', label: 'Нагрудник', item: equipped?.chest ?? null, glyph: '⬟', side: 'right' },
    {
      id: 'amulet',
      inventorySlot: 'Amulet',
      label: 'Амулет',
      item: equipped?.amulet ?? equipped?.accessory ?? null,
      glyph: '✦',
      side: 'right',
    },
    { id: 'offHand', inventorySlot: 'OffHand', label: 'Вторая рука', item: equipped?.offHand ?? null, glyph: '🛡', side: 'right' },
    { id: 'legs', inventorySlot: 'Legs', label: 'Поножи', item: equipped?.legs ?? null, glyph: '▥', side: 'right' },
    {
      id: 'feet',
      inventorySlot: 'Feet',
      label: 'Обувь',
      item: equipped?.feet ?? equipped?.boots ?? null,
      glyph: '⌁',
      side: 'right',
    },
    { id: 'ring2', inventorySlot: 'Ring2', label: 'Кольцо II', item: equipped?.ring2 ?? null, glyph: '✧', side: 'right' },
  ]
})
const leftEquipment = computed(() => equipment.value.filter(slot => slot.side === 'left'))
const rightEquipment = computed(() => equipment.value.filter(slot => slot.side === 'right'))
const rangerPieces = computed(() => equipment.value.filter((slot) => slot.item?.setId === 'RANGER_SET').length)
const talentAbilities = computed(() => character.value?.knownAbilities.filter((ability) => ability.sourceTalentId) ?? [])
const baselineAbilities = computed(() => character.value?.knownAbilities.filter((ability) => !ability.sourceTalentId) ?? [])
const xpTarget = computed(() => character.value?.xpToNextLevel ?? 0)
const xpRemaining = computed(() => Math.max(0, xpTarget.value - (character.value?.experience ?? 0)))

function openEquipmentSlot(slot: PaperdollSlot): void {
  if (slot.item) {
    selectedItem.value = slot.item
    selectedEquipmentSlot.value = slot.inventorySlot
    equipmentActionError.value = null
    return
  }

  emit('select-empty-slot', slot.inventorySlot)
}

async function unequipSelected(): Promise<void> {
  if (!selectedEquipmentSlot.value || session.mutationPending) return
  await session.unequip(selectedEquipmentSlot.value)
  equipmentActionError.value = session.errorCode
  if (!equipmentActionError.value) {
    selectedItem.value = null
    selectedEquipmentSlot.value = null
  }
}

function equipmentErrorMessage(code: string | null): string | null {
  if (!code) return null
  if (code === 'inventory_equipment_change_in_combat') return 'Снаряжение нельзя менять во время боя.'
  if (code === 'inventory_invalid_slot') return 'Сервер не распознал слот снаряжения.'
  return 'Не удалось изменить снаряжение. Повторите попытку.'
}

function itemArt(item: InventoryItem | null): string | undefined {
  return itemArtUrl(item?.iconId)
}

function formatCombatNumber(value: number): string {
  const rounded = Math.round(value * 100) / 100
  return Number.isInteger(rounded) ? String(rounded) : rounded.toFixed(2).replace(/0+$/, '').replace(/\.$/, '')
}

function weaponTiming(item: InventoryItem | null): { interval: number; aps: number } | null {
  if (!item?.weaponBaseAttackIntervalSeconds || item.weaponBaseAttackIntervalSeconds <= 0) return null
  const multiplier = Math.max(0.1, character.value?.stats.attackSpeed ?? 1)
  const interval = item.weaponBaseAttackIntervalSeconds / multiplier
  return { interval, aps: 1 / interval }
}

const mainHandTiming = computed(() => {
  const equipped = character.value?.inventory.equipped
  return weaponTiming(equipped?.mainHand ?? equipped?.weapon ?? null)
})
const offHandTiming = computed(() => weaponTiming(character.value?.inventory.equipped.offHand ?? null))
const totalAttacksPerSecond = computed(() =>
  (mainHandTiming.value?.aps ?? 0) + (offHandTiming.value?.aps ?? 0),
)

function itemGlyph(item: InventoryItem | null, fallback: string): string {
  if (!item) return fallback
  if (item.slot === 'Weapon' || item.slot === 'MainHand') return '⚔'
  if (item.slot === 'OffHand') return '🛡'
  if (item.slot === 'Head') return '◈'
  if (item.slot === 'Chest') return '⬟'
  if (item.slot === 'Hands') return '◫'
  if (item.slot === 'Legs') return '▥'
  if (item.slot === 'Boots' || item.slot === 'Feet') return '⌁'
  if (item.slot === 'Cloak') return '◒'
  if (item.slot === 'Amulet' || item.slot === 'Ring1' || item.slot === 'Ring2') return '✧'
  return '✦'
}

function itemStats(item: InventoryItem): string[] {
  return [
    item.stats.strength ? `Сила +${item.stats.strength}` : '',
    item.stats.agility ? `Ловкость +${item.stats.agility}` : '',
    item.stats.intellect ? `Интеллект +${item.stats.intellect}` : '',
    item.stats.stamina ? `Выносливость +${item.stats.stamina}` : '',
    item.attackSpeedPercent ? `Скорость атаки +${item.attackSpeedPercent}%` : '',
    item.dodgePercent ? `Уклонение +${item.dodgePercent}%` : '',
  ].filter(Boolean)
}

function abilityArt(ability: KnownAbility): string | null {
  if (ability.id === 'STRIKE') return gameArt.warriorAbilities.strike
  if (ability.id === 'SHIELD_BASH') return gameArt.warriorAbilities.shieldBash
  if (ability.id === 'PROVOKE') return gameArt.warriorAbilities.provoke
  if (ability.id === 'BASTION') return gameArt.warriorAbilities.bastion
  return resolveAbilityArt(ability.id)
}

function abilityInitials(ability: KnownAbility): string {
  return abilityName(ability).split(' ').map((word) => word[0]).join('').slice(0, 2).toUpperCase()
}
</script>

<template>
  <section v-if="character" class="overview">
    <section class="paperdoll">
      <header class="paperdoll__identity">
        <div>
          <p class="eyebrow">Герой</p>
          <h1>Снаряжение</h1>
          <p>{{ raceLabel(character.raceId) }} · {{ classLabel(character.classId) }}</p>
          <small class="public-code">ELY ID · {{ character.publicCode ?? '—' }}</small>
        </div>
        <div class="paperdoll__meta">
          <strong>Уровень {{ character.level }}</strong>
          <span class="gold">● {{ character.gold }}</span>
        </div>
      </header>

      <div class="paperdoll__stage">
        <div class="equipment-column equipment-column--left" aria-label="Снаряжение слева">
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
            :title="slot.item?.name ?? slot.label"
            @click="openEquipmentSlot(slot)"
          >
            <span class="equipment-slot__icon">
              <img v-if="itemArt(slot.item)" :src="itemArt(slot.item)" :alt="slot.item?.name ?? slot.label" loading="lazy" decoding="async" />
              <template v-else>{{ itemGlyph(slot.item, slot.glyph) }}</template>
            </span>
            <small class="equipment-slot__label">{{ slot.label }}</small>
          </button>
        </div>

        <div class="paperdoll__figure">
          <div class="hero-figure">
            <span class="hero-figure__sigil" aria-hidden="true">◆</span>
            <img v-if="character.classId === 'WARRIOR'" :src="gameArt.characters.warrior" alt="Воин" />
            <div v-else class="hero-figure__fallback" role="img" :aria-label="classLabel(character.classId)">
              <span>{{ character.name.slice(0, 1).toUpperCase() }}</span>
              <small>{{ classLabel(character.classId) }}</small>
            </div>
          </div>
          <div class="hero-figure__caption">
            <strong data-hero-name>{{ character.name }}</strong>
            <small>ур. {{ character.level }} · {{ classLabel(character.classId) }}</small>
          </div>
        </div>

        <div class="equipment-column equipment-column--right" aria-label="Снаряжение справа">
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
            :title="slot.item?.name ?? slot.label"
            @click="openEquipmentSlot(slot)"
          >
            <span class="equipment-slot__icon">
              <img v-if="itemArt(slot.item)" :src="itemArt(slot.item)" :alt="slot.item?.name ?? slot.label" loading="lazy" decoding="async" />
              <template v-else>{{ itemGlyph(slot.item, slot.glyph) }}</template>
            </span>
            <small class="equipment-slot__label">{{ slot.label }}</small>
          </button>
        </div>
      </div>

      <div class="progression">
        <div class="progression__heading">
          <div>
            <small>Развитие героя</small>
            <strong>{{ character.experience }} / {{ xpTarget }} опыта</strong>
          </div>
          <span>до уровня: {{ xpRemaining }}</span>
        </div>
        <div class="xp-track">
          <i :style="{ width: `${xpTarget > 0 ? Math.min(100, character.experience / xpTarget * 100) : 100}%` }" />
        </div>
      </div>

      <div class="equipment-summary">
        <div>
          <small>Надетое снаряжение</small>
          <strong>{{ equipment.filter((slot) => slot.item).length }} / {{ equipment.length }} слотов</strong>
        </div>
        <div v-if="mainHandTiming" class="attack-timing">
          <small>Текущая автоатака</small>
          <strong>
            Основная: {{ formatCombatNumber(mainHandTiming.interval) }} сек. · {{ formatCombatNumber(mainHandTiming.aps) }} уд/с
          </strong>
          <span v-if="offHandTiming">
            Вторая: {{ formatCombatNumber(offHandTiming.interval) }} сек. · {{ formatCombatNumber(offHandTiming.aps) }} уд/с
          </span>
          <b v-if="offHandTiming">Итого: {{ formatCombatNumber(totalAttacksPerSecond) }} уд/с</b>
        </div>
        <div v-if="rangerPieces > 0" class="set-progress">
          <span>Следопыт {{ rangerPieces }}/6</span>
          <i :class="{ active: rangerPieces >= 3 }">3</i>
          <i :class="{ active: rangerPieces >= 6 }">6</i>
        </div>
      </div>
    </section>

    <UIPanel v-if="character.classId === 'WARRIOR'" class="abilities-panel">
      <template #title>Боевые способности</template>
      <p class="hint">Здесь только то, что реально доступно персонажу. Новые активные способности открываются талантами.</p>
      <div class="ability-grid">
        <button v-for="ability in [...baselineAbilities, ...talentAbilities]" :key="ability.id" type="button" @click="selectedAbility = ability">
          <span class="ability-icon"><img v-if="abilityArt(ability)" :src="abilityArt(ability)!" alt="" /><b v-else>{{ abilityInitials(ability) }}</b></span>
          <span><strong>{{ abilityName(ability) }}</strong><small>{{ ability.sourceTalentName ?? 'Базовое действие класса' }}</small></span>
        </button>
      </div>
    </UIPanel>

    <UIModal
      :open="selectedItem !== null"
      :title="selectedItem?.name ?? ''"
      @close="selectedItem = null; selectedEquipmentSlot = null; equipmentActionError = null"
    >
      <article v-if="selectedItem" class="detail">
        <p>{{ selectedItem.description }}</p>
        <dl><div v-for="row in itemStats(selectedItem)" :key="row"><dt>{{ row }}</dt></div></dl>
        <p v-if="selectedItem.weaponBaseAttackIntervalSeconds">
          Скорость оружия: {{ formatCombatNumber(selectedItem.weaponBaseAttackIntervalSeconds) }} сек. базово
          <template v-if="weaponTiming(selectedItem)">
            · {{ formatCombatNumber(weaponTiming(selectedItem)!.interval) }} сек. сейчас
            · {{ formatCombatNumber(weaponTiming(selectedItem)!.aps) }} уд/с
          </template>
        </p>
        <p v-if="selectedItem.setId">Часть комплекта Следопыта.</p>
        <p v-if="equipmentErrorMessage(equipmentActionError)" class="detail__error" role="alert">
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

    <UIModal :open="selectedAbility !== null" :title="selectedAbility ? abilityName(selectedAbility) : ''" @close="selectedAbility = null">
      <article v-if="selectedAbility" class="detail">
        <p>{{ abilityDescription(selectedAbility) }}</p>
        <dl>
          <div><dt>Стоимость</dt><dd>{{ selectedAbility.resourceCost }} ярости</dd></div>
          <div><dt>Перезарядка</dt><dd>{{ selectedAbility.cooldownSeconds }} сек.</dd></div>
          <div><dt>Тип</dt><dd>{{ abilityTypeLabel(selectedAbility.type) }}</dd></div>
          <div><dt>Цель</dt><dd>{{ abilityTargetLabel(selectedAbility.targetType) }}</dd></div>
          <div v-if="selectedAbility.sourceTalentName"><dt>Источник</dt><dd>{{ selectedAbility.sourceTalentName }}</dd></div>
        </dl>
      </article>
    </UIModal>
  </section>
</template>

<style scoped>
.overview {
  display: grid;
  width: min(100%, var(--ui-content-width));
  margin-inline: auto;
  gap: var(--ui-space-3);
  padding: var(--ui-space-3) var(--ui-space-3) var(--ui-space-7);
}

.paperdoll {
  position: relative;
  overflow: hidden;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: calc(var(--ui-radius-lg) + 2px);
  background:
    radial-gradient(circle at 50% 26%, rgb(146 136 255 / 18%), transparent 15rem),
    radial-gradient(circle at 50% 70%, rgb(74 184 207 / 6%), transparent 14rem),
    linear-gradient(180deg, rgb(15 21 35 / 98%), rgb(7 11 19 / 98%));
  box-shadow: var(--ui-shadow-inset), var(--ui-shadow-elevated);
}

.paperdoll::after {
  position: absolute;
  right: 18%;
  bottom: 0;
  left: 18%;
  height: 1px;
  background: linear-gradient(90deg, transparent, rgb(146 136 255 / 55%), transparent);
  content: '';
}

.paperdoll__identity {
  position: relative;
  z-index: 2;
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: var(--ui-space-3);
  padding: var(--ui-space-4) var(--ui-space-4) var(--ui-space-2);
}

.paperdoll__identity > div:first-child {
  min-width: 0;
}

.paperdoll__identity h1,
.paperdoll__identity p {
  margin: 0;
}

.paperdoll__identity h1 {
  overflow: hidden;
  font-family: var(--ui-font-display);
  font-size: clamp(1.65rem, 7vw, 2.3rem);
  line-height: 1;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.paperdoll__identity > div > p:not(.eyebrow) {
  margin-top: 3px;
  color: var(--ui-color-text-secondary);
  font-size: var(--ui-font-size-xs);
}

.public-code {
  color: var(--ui-color-primary);
  font-size: .62rem;
  letter-spacing: .08em;
}

.eyebrow {
  margin-bottom: 3px !important;
  color: #bcb6ff;
  font-size: .6rem;
  font-weight: 700;
  letter-spacing: .1em;
  text-transform: uppercase;
}

.paperdoll__meta {
  display: grid;
  justify-items: end;
  gap: 3px;
  white-space: nowrap;
}

.paperdoll__meta strong {
  font-size: var(--ui-font-size-xs);
}

.gold {
  color: var(--ui-color-gold);
  font-size: var(--ui-font-size-sm);
  font-weight: 700;
}

.paperdoll__stage {
  position: relative;
  z-index: 1;
  display: grid;
  grid-template-columns: 58px minmax(9rem, 1fr) 58px;
  align-items: center;
  gap: var(--ui-space-2);
  min-height: 22rem;
  padding: var(--ui-space-2) var(--ui-space-3) var(--ui-space-4);
}

.paperdoll__stage::before {
  position: absolute;
  top: 8%;
  right: 22%;
  bottom: 7%;
  left: 22%;
  border: 1px solid rgb(146 136 255 / 8%);
  border-radius: 48% 48% 38% 38%;
  background:
    radial-gradient(circle at 50% 38%, rgb(146 136 255 / 10%), transparent 54%),
    linear-gradient(180deg, rgb(255 255 255 / 1.5%), transparent 56%);
  content: '';
  pointer-events: none;
}

.equipment-column {
  position: relative;
  z-index: 2;
  display: grid;
  align-content: center;
  gap: 8px;
}

.equipment-slot {
  position: relative;
  display: grid;
  width: 56px;
  min-height: 58px;
  place-items: center;
  gap: 2px;
  padding: 3px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background:
    radial-gradient(circle at 50% 24%, rgb(255 255 255 / 4%), transparent 52%),
    linear-gradient(180deg, rgb(13 18 29 / 94%), rgb(4 7 12 / 94%));
  box-shadow: var(--ui-shadow-inset), 0 5px 12px rgb(0 0 0 / 18%);
  color: var(--ui-color-text-muted);
  font: inherit;
}

.equipment-slot.filled {
  border-color: color-mix(in srgb, var(--ui-color-primary) 46%, var(--ui-color-border));
  color: var(--ui-color-text-primary);
}

.equipment-slot:not(.filled) {
  opacity: .54;
}

.equipment-slot[data-rarity='Uncommon'] {
  border-color: color-mix(in srgb, var(--ui-color-success) 62%, var(--ui-color-border));
}

.equipment-slot[data-rarity='Rare'] {
  border-color: color-mix(in srgb, var(--ui-color-secondary) 72%, var(--ui-color-border));
}

.equipment-slot[data-rarity='Epic'] {
  border-color: color-mix(in srgb, var(--ui-color-primary) 82%, var(--ui-color-border));
  box-shadow: var(--ui-shadow-inset), 0 0 12px rgb(146 136 255 / 11%);
}

.equipment-slot[data-rarity='Legendary'],
.equipment-slot[data-rarity='Unique'] {
  border-color: var(--ui-color-gold);
  box-shadow: var(--ui-shadow-inset), 0 0 14px rgb(232 200 102 / 13%);
}

.equipment-slot__icon {
  display: grid;
  width: 44px;
  height: 40px;
  place-items: center;
  border: 1px solid rgb(255 255 255 / 5%);
  border-radius: calc(var(--ui-radius-md) - 2px);
  background: rgb(2 5 9 / 66%);
  color: #aaa4f5;
  font-size: 1.15rem;
}

.equipment-slot__icon img {
  width: 100%;
  height: 100%;
  border-radius: inherit;
  object-fit: cover;
}

.equipment-slot.filled .equipment-slot__icon {
  background:
    radial-gradient(circle at 50% 30%, rgb(146 136 255 / 12%), transparent 62%),
    rgb(3 6 11 / 82%);
}

.equipment-slot__label {
  width: 100%;
  overflow: hidden;
  color: var(--ui-color-text-muted);
  font-size: .43rem;
  font-weight: 700;
  letter-spacing: .02em;
  line-height: 1.05;
  text-align: center;
  text-overflow: ellipsis;
  text-transform: uppercase;
  white-space: nowrap;
}

.paperdoll__figure {
  position: relative;
  z-index: 1;
  display: grid;
  align-self: stretch;
  align-content: end;
  gap: 6px;
}

.hero-figure {
  position: relative;
  display: grid;
  min-height: 17.5rem;
  place-items: end center;
}

.hero-figure::before {
  position: absolute;
  inset: 9% 7% 3%;
  border-radius: 50%;
  background: radial-gradient(circle, rgb(94 81 201 / 15%), transparent 64%);
  filter: blur(8px);
  content: '';
}

.hero-figure::after {
  position: absolute;
  right: 8%;
  bottom: 0;
  left: 8%;
  height: 1.4rem;
  border-radius: 50%;
  background: radial-gradient(ellipse, rgb(0 0 0 / 58%), transparent 70%);
  content: '';
}

.hero-figure__sigil {
  position: absolute;
  top: 16%;
  color: rgb(146 136 255 / 11%);
  font-size: 7rem;
  line-height: 1;
  transform: rotate(45deg);
}

.hero-figure img {
  position: relative;
  z-index: 1;
  width: min(100%, 12.5rem);
  max-height: 17.5rem;
  object-fit: contain;
  filter: drop-shadow(0 1rem 1.45rem rgb(0 0 0 / 58%));
}

.hero-figure__fallback {
  position: relative;
  z-index: 1;
  display: grid;
  width: 8.5rem;
  height: 14rem;
  place-items: center;
  align-content: center;
  gap: var(--ui-space-2);
  border: 1px solid rgb(146 136 255 / 15%);
  border-radius: 48% 48% 34% 34%;
  background: linear-gradient(180deg, rgb(146 136 255 / 9%), rgb(3 6 11 / 48%));
  color: var(--ui-color-text-muted);
}

.hero-figure__fallback span {
  color: #cbc7ff;
  font-family: var(--ui-font-display);
  font-size: 2.8rem;
}

.hero-figure__fallback small {
  font-size: .6rem;
}

.hero-figure__caption {
  position: relative;
  z-index: 2;
  display: grid;
  justify-items: center;
  gap: 1px;
  text-align: center;
}

.hero-figure__caption strong {
  max-width: 100%;
  overflow: hidden;
  font-family: var(--ui-font-display);
  font-size: .78rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.hero-figure__caption small {
  color: var(--ui-color-text-muted);
  font-size: .5rem;
}

.progression,
.attack-timing {
  display: grid;
  gap: 2px;
  min-width: 0;
  color: var(--ui-color-text-secondary);
  font-size: .58rem;
}

.attack-timing small { color: var(--ui-color-text-muted); }
.attack-timing strong,
.attack-timing b { color: #d5d2ff; font-weight: 700; }
.attack-timing span { color: var(--ui-color-text-secondary); }

.equipment-summary {
  position: relative;
  z-index: 2;
  margin-inline: var(--ui-space-3);
  padding: var(--ui-space-3);
  border-top: 1px solid rgb(255 255 255 / 7%);
}

.progression__heading,
.equipment-summary {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-3);
}

.progression__heading > div,
.equipment-summary > div:first-child {
  display: grid;
  gap: 2px;
}

.progression__heading small,
.equipment-summary small {
  color: var(--ui-color-text-muted);
  font-size: .57rem;
  letter-spacing: .06em;
  text-transform: uppercase;
}

.progression__heading strong,
.equipment-summary strong {
  font-size: var(--ui-font-size-xs);
}

.progression__heading > span {
  color: var(--ui-color-text-muted);
  font-size: .62rem;
}

.xp-track {
  height: 8px;
  margin-top: var(--ui-space-2);
  overflow: hidden;
  border-radius: var(--ui-radius-round);
  background: rgb(2 4 8 / 72%);
}

.xp-track i {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, #645dc7, var(--ui-color-primary), var(--ui-color-secondary));
  box-shadow: 0 0 10px rgb(146 136 255 / 28%);
}

.equipment-summary {
  margin-bottom: var(--ui-space-3);
  border-bottom: 1px solid rgb(255 255 255 / 5%);
  border-radius: var(--ui-radius-md);
  background: rgb(255 255 255 / 1.5%);
}

.set-progress {
  display: flex;
  align-items: center;
  gap: 5px;
  color: var(--ui-color-text-muted);
  font-size: .62rem;
}

.set-progress i {
  display: grid;
  width: 1.45rem;
  height: 1.45rem;
  place-items: center;
  border: 1px solid var(--ui-color-border);
  border-radius: 50%;
  background: var(--ui-color-background);
  color: var(--ui-color-text-muted);
  font-style: normal;
}

.set-progress i.active {
  border-color: var(--ui-color-success);
  color: var(--ui-color-success);
}

.hint {
  margin: 0 0 var(--ui-space-3);
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-sm);
  line-height: 1.5;
}

.ability-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--ui-space-2);
}

.ability-grid button {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr);
  align-items: center;
  gap: var(--ui-space-2);
  min-height: var(--ui-control-height-lg);
  padding: var(--ui-space-2);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: linear-gradient(180deg, rgb(255 255 255 / 2%), rgb(3 6 11 / 28%));
  color: inherit;
  font: inherit;
  text-align: left;
}

.ability-grid button > span:last-child {
  display: grid;
  min-width: 0;
}

.ability-grid button strong,
.ability-grid small {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ability-grid small {
  color: var(--ui-color-text-muted);
  font-size: .62rem;
}

.ability-icon {
  display: grid;
  width: 2.8rem;
  height: 2.8rem;
  place-items: center;
  overflow: hidden;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: var(--ui-color-background);
}

.ability-icon img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.detail__error {
  margin: 0;
  color: #ef9bab;
  font-size: var(--ui-font-size-xs);
}

.detail {
  display: grid;
  gap: var(--ui-space-3);
}

.detail p {
  margin: 0;
  color: var(--ui-color-text-muted);
}

.detail dl {
  display: grid;
  gap: var(--ui-space-1);
  margin: 0;
}

.detail dl div {
  display: flex;
  justify-content: space-between;
  gap: var(--ui-space-3);
  padding: var(--ui-space-2);
  border-bottom: 1px solid var(--ui-color-border);
}

.detail dd {
  margin: 0;
  color: var(--ui-color-text-primary);
}

@media (max-width: 430px) {
  .paperdoll__stage {
    grid-template-columns: 52px minmax(7.5rem, 1fr) 52px;
    gap: 5px;
    min-height: 20rem;
    padding-inline: var(--ui-space-2);
  }

  .equipment-slot {
    width: 50px;
    min-height: 54px;
  }

  .equipment-slot__icon {
    width: 40px;
    height: 37px;
  }

  .hero-figure {
    min-height: 15.5rem;
  }

  .hero-figure img {
    max-height: 15.5rem;
  }

  .ability-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 355px) {
  .paperdoll__stage {
    grid-template-columns: 47px minmax(7rem, 1fr) 47px;
    gap: 3px;
    padding-inline: 6px;
  }

  .equipment-slot {
    width: 46px;
    min-height: 51px;
    padding-inline: 2px;
  }

  .equipment-slot__icon {
    width: 37px;
    height: 35px;
  }

  .equipment-slot__label {
    font-size: .39rem;
  }

  .paperdoll__identity {
    padding-inline: var(--ui-space-3);
  }
}
</style>
