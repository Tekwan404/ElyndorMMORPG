<script setup lang="ts">
import { computed, ref } from 'vue'

import type { InventoryItem, KnownAbility } from '@/api/contracts'
import { gameArt } from '@/assets/gameArt'
import {
  abilityDescription,
  abilityName,
  abilityTargetLabel,
  abilityTypeLabel,
  classLabel,
  genderLabel,
  raceLabel,
  resourceLabel,
} from '@/game/character/characterPresentation'
import { resolveAbilityArt } from '@/game/talents/talentArt'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIHealthBar, UIModal, UIPanel } from '@/ui/components'

const session = useGameSessionStore()
const character = computed(() => session.snapshot?.character)
const selectedItem = ref<InventoryItem | null>(null)
const selectedAbility = ref<KnownAbility | null>(null)

const equipment = computed(() => [
  { id: 'head', label: 'Шлем', item: character.value?.inventory.equipped.head ?? null, glyph: '◈' },
  { id: 'chest', label: 'Нагрудник', item: character.value?.inventory.equipped.chest ?? null, glyph: '⬟' },
  { id: 'legs', label: 'Штаны', item: character.value?.inventory.equipped.legs ?? null, glyph: '▥' },
  { id: 'boots', label: 'Ботинки', item: character.value?.inventory.equipped.boots ?? null, glyph: '⌁' },
  { id: 'weapon', label: 'Оружие', item: character.value?.inventory.equipped.weapon ?? null, glyph: '⚔' },
  { id: 'accessory', label: 'Аксессуар', item: character.value?.inventory.equipped.accessory ?? null, glyph: '✦' },
])
const rangerPieces = computed(() => equipment.value.filter((slot) => slot.item?.setId === 'RANGER_SET').length)
const talentAbilities = computed(() => character.value?.knownAbilities.filter((ability) => ability.sourceTalentId) ?? [])
const baselineAbilities = computed(() => character.value?.knownAbilities.filter((ability) => !ability.sourceTalentId) ?? [])
const xpTarget = computed(() => character.value?.xpToNextLevel ?? 0)
const xpRemaining = computed(() => Math.max(0, xpTarget.value - (character.value?.experience ?? 0)))

function itemGlyph(item: InventoryItem | null, fallback: string): string {
  if (!item) return fallback
  if (item.slot === 'Weapon') return '⚔'
  if (item.slot === 'Head') return '◈'
  if (item.slot === 'Chest') return '⬟'
  if (item.slot === 'Legs') return '▥'
  if (item.slot === 'Boots') return '⌁'
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
          <h1>{{ character.name }}</h1>
          <p>{{ raceLabel(character.raceId) }} · {{ classLabel(character.classId) }}</p>
        </div>
        <div class="paperdoll__meta">
          <strong>Уровень {{ character.level }}</strong>
          <span class="gold">● {{ character.gold }}</span>
        </div>
      </header>

      <div class="paperdoll__stage">
        <div class="equipment-column equipment-column--left" aria-label="Снаряжение слева">
          <button
            v-for="slot in equipment.slice(0, 3)"
            :key="slot.id"
            type="button"
            class="equipment-slot"
            :class="{ filled: slot.item }"
            :disabled="!slot.item"
            @click="selectedItem = slot.item"
          >
            <span class="equipment-slot__icon">{{ itemGlyph(slot.item, slot.glyph) }}</span>
            <span class="equipment-slot__copy">
              <small>{{ slot.label }}</small>
              <strong>{{ slot.item?.name ?? 'Пусто' }}</strong>
            </span>
          </button>
        </div>

        <div class="paperdoll__figure">
          <div class="hero-figure">
            <img v-if="character.classId === 'WARRIOR'" :src="gameArt.characters.warrior" alt="Воин" />
            <div v-else class="hero-figure__fallback" role="img" :aria-label="classLabel(character.classId)">
              <span>{{ character.name.slice(0, 1).toUpperCase() }}</span>
              <small>{{ classLabel(character.classId) }}</small>
            </div>
          </div>
          <div class="paperdoll__vitals">
            <UIHealthBar label="Здоровье" :value="character.vitals.currentHp" :max="character.vitals.maxHp" />
            <UIHealthBar :label="resourceLabel(character.vitals.resourceType)" :tone="character.vitals.resourceType === 'RAGE' ? 'rage' : character.vitals.resourceType === 'MANA' ? 'mana' : 'focus'" :value="character.vitals.currentResource" :max="character.vitals.maxResource" />
          </div>
        </div>

        <div class="equipment-column equipment-column--right" aria-label="Снаряжение справа">
          <button
            v-for="slot in equipment.slice(3)"
            :key="slot.id"
            type="button"
            class="equipment-slot"
            :class="{ filled: slot.item }"
            :disabled="!slot.item"
            @click="selectedItem = slot.item"
          >
            <span class="equipment-slot__copy">
              <small>{{ slot.label }}</small>
              <strong>{{ slot.item?.name ?? 'Пусто' }}</strong>
            </span>
            <span class="equipment-slot__icon">{{ itemGlyph(slot.item, slot.glyph) }}</span>
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
        <div class="set-progress">
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

    <UIModal :open="selectedItem !== null" :title="selectedItem?.name ?? ''" @close="selectedItem = null">
      <article v-if="selectedItem" class="detail">
        <p>{{ selectedItem.description }}</p>
        <dl><div v-for="row in itemStats(selectedItem)" :key="row"><dt>{{ row }}</dt></div></dl>
        <p v-if="selectedItem.weaponBaseAttackIntervalSeconds">Базовый интервал автоатаки: {{ selectedItem.weaponBaseAttackIntervalSeconds }} сек.</p>
        <p v-if="selectedItem.setId">Часть комплекта Следопыта.</p>
      </article>
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
  grid-template-columns: minmax(0, 1fr) minmax(7.5rem, 1.18fr) minmax(0, 1fr);
  align-items: center;
  gap: var(--ui-space-2);
  min-height: 18rem;
  padding: var(--ui-space-2) var(--ui-space-3) var(--ui-space-3);
}

.equipment-column {
  display: grid;
  gap: var(--ui-space-2);
}

.equipment-slot {
  display: grid;
  min-width: 0;
  grid-template-columns: 2.55rem minmax(0, 1fr);
  align-items: center;
  gap: var(--ui-space-2);
  min-height: 3.5rem;
  padding: 6px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: linear-gradient(180deg, rgb(255 255 255 / 2%), rgb(3 6 11 / 38%));
  box-shadow: var(--ui-shadow-inset);
  color: var(--ui-color-text-muted);
  font: inherit;
  text-align: left;
}

.equipment-column--right .equipment-slot {
  grid-template-columns: minmax(0, 1fr) 2.55rem;
  text-align: right;
}

.equipment-slot.filled {
  border-color: color-mix(in srgb, var(--ui-color-primary) 48%, var(--ui-color-border));
  background: linear-gradient(180deg, rgb(146 136 255 / 8%), rgb(3 6 11 / 36%));
  color: var(--ui-color-text-primary);
}

.equipment-slot__icon {
  display: grid;
  width: 2.55rem;
  height: 2.55rem;
  place-items: center;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: var(--ui-radius-md);
  background: rgb(4 7 12 / 78%);
  color: var(--ui-color-primary);
  font-size: 1.2rem;
}

.equipment-slot__copy {
  display: grid;
  min-width: 0;
  gap: 1px;
}

.equipment-slot__copy small {
  color: var(--ui-color-text-muted);
  font-size: .55rem;
  text-transform: uppercase;
}

.equipment-slot__copy strong {
  overflow: hidden;
  font-size: .66rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.paperdoll__figure {
  display: grid;
  align-self: stretch;
  align-content: end;
  gap: var(--ui-space-2);
}

.hero-figure {
  position: relative;
  display: grid;
  min-height: 13rem;
  place-items: end center;
}

.hero-figure::after {
  position: absolute;
  right: 10%;
  bottom: 0;
  left: 10%;
  height: 1.3rem;
  border-radius: 50%;
  background: radial-gradient(ellipse, rgb(0 0 0 / 48%), transparent 70%);
  content: '';
}

.hero-figure img {
  position: relative;
  z-index: 1;
  width: min(100%, 11rem);
  max-height: 14rem;
  object-fit: contain;
  filter: drop-shadow(0 .9rem 1.3rem rgb(0 0 0 / 52%));
}

.hero-figure__fallback {
  position: relative;
  z-index: 1;
  display: grid;
  width: 8rem;
  height: 12rem;
  place-items: center;
  align-content: center;
  gap: var(--ui-space-2);
  border: 1px solid rgb(146 136 255 / 16%);
  border-radius: 48% 48% 32% 32%;
  background: linear-gradient(180deg, rgb(146 136 255 / 10%), rgb(3 6 11 / 48%));
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

.paperdoll__vitals {
  display: grid;
  gap: 4px;
}

.progression,
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
    grid-template-columns: minmax(0, .92fr) minmax(6.2rem, 1.08fr) minmax(0, .92fr);
    gap: 5px;
    min-height: 16rem;
    padding-inline: var(--ui-space-2);
  }

  .equipment-slot {
    grid-template-columns: 2.25rem minmax(0, 1fr);
    gap: 5px;
    padding: 4px;
  }

  .equipment-column--right .equipment-slot {
    grid-template-columns: minmax(0, 1fr) 2.25rem;
  }

  .equipment-slot__icon {
    width: 2.25rem;
    height: 2.25rem;
  }

  .equipment-slot__copy strong {
    font-size: .59rem;
  }

  .hero-figure {
    min-height: 11.5rem;
  }

  .ability-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 355px) {
  .equipment-slot__copy {
    display: none;
  }

  .equipment-slot,
  .equipment-column--right .equipment-slot {
    grid-template-columns: 1fr;
    justify-items: center;
  }

  .paperdoll__stage {
    grid-template-columns: 3.1rem minmax(7rem, 1fr) 3.1rem;
  }

  .paperdoll__identity {
    padding-inline: var(--ui-space-3);
  }
}
</style>
