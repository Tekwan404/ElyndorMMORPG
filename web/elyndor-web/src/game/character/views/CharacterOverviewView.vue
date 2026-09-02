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
  { id: 'head', label: 'Голова', item: character.value?.inventory.equipped.head ?? null, glyph: '◈' },
  { id: 'weapon', label: 'Оружие', item: character.value?.inventory.equipped.weapon ?? null, glyph: '⚔' },
  { id: 'chest', label: 'Нагрудник', item: character.value?.inventory.equipped.chest ?? null, glyph: '⬟' },
])
const talentAbilities = computed(() => character.value?.knownAbilities.filter((ability) => ability.sourceTalentId) ?? [])
const baselineAbilities = computed(() => character.value?.knownAbilities.filter((ability) => !ability.sourceTalentId) ?? [])
const xpTarget = computed(() => character.value?.xpToNextLevel ?? 0)
const xpRemaining = computed(() => Math.max(0, xpTarget.value - (character.value?.experience ?? 0)))

function itemGlyph(item: InventoryItem | null, fallback: string): string {
  if (!item) return fallback
  if (item.slot === 'Weapon') return '⚔'
  if (item.slot === 'Head') return '◈'
  return '⬟'
}

function rarityLabel(item: InventoryItem): string {
  if (item.rarity === 'Rare') return 'Редкий'
  if (item.rarity === 'Uncommon') return 'Необычный'
  return 'Обычный'
}

function itemStats(item: InventoryItem): string[] {
  return [
    item.stats.strength ? `Сила +${item.stats.strength}` : '',
    item.stats.agility ? `Ловкость +${item.stats.agility}` : '',
    item.stats.intellect ? `Интеллект +${item.stats.intellect}` : '',
    item.stats.stamina ? `Выносливость +${item.stats.stamina}` : '',
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
    <header class="hero-card">
      <div class="hero-card__copy">
        <p class="eyebrow">Персонаж</p>
        <h1>{{ character.name }}</h1>
        <p>{{ raceLabel(character.raceId) }} · {{ classLabel(character.classId) }} · уровень {{ character.level }}</p>
        <small>{{ genderLabel(character.genderId) }} персонаж</small>
      </div>
      <img v-if="character.classId === 'WARRIOR'" :src="gameArt.characters.warrior" alt="Воин" />
    </header>

    <UIPanel class="progress-panel">
      <template #title>Развитие героя</template>
      <div class="xp-row">
        <span>Уровень {{ character.level }}</span>
        <strong>{{ character.experience }} / {{ xpTarget }} опыта</strong>
      </div>
      <div class="xp-track" role="progressbar" aria-label="Опыт" :aria-valuenow="character.experience" :aria-valuemax="xpTarget">
        <i :style="{ width: `${xpTarget > 0 ? Math.min(100, character.experience / xpTarget * 100) : 100}%` }" />
      </div>
      <small v-if="character.xpToNextLevel > 0">До следующего уровня: {{ xpRemaining }} опыта</small>
      <small v-else>Достигнут максимальный уровень текущей версии.</small>
    </UIPanel>

    <UIPanel class="vitals-panel">
      <template #title>Состояние</template>
      <UIHealthBar label="Здоровье" :value="character.vitals.currentHp" :max="character.vitals.maxHp" />
      <UIHealthBar
        :label="resourceLabel(character.vitals.resourceType)"
        :tone="character.vitals.resourceType === 'RAGE' ? 'rage' : character.vitals.resourceType === 'MANA' ? 'mana' : 'focus'"
        :value="character.vitals.currentResource"
        :max="character.vitals.maxResource"
      />
    </UIPanel>

    <UIPanel class="equipment-panel">
      <template #title>Надетое снаряжение</template>
      <div class="equipment-grid">
        <button
          v-for="slot in equipment"
          :key="slot.id"
          type="button"
          class="equipment-slot"
          :class="{ filled: slot.item }"
          :disabled="!slot.item"
          @click="selectedItem = slot.item"
        >
          <small>{{ slot.label }}</small>
          <span>{{ itemGlyph(slot.item, slot.glyph) }}</span>
          <strong>{{ slot.item?.name ?? 'Пусто' }}</strong>
        </button>
      </div>
      <p class="panel-hint">Здесь видно только то, что надето на героя. Остальные предметы находятся во вкладке «Инвентарь».</p>
    </UIPanel>

    <UIPanel v-if="character.classId === 'WARRIOR'" class="abilities-panel">
      <template #title>Боевые способности</template>
      <p class="panel-hint">Дополнительные способности появляются только после изучения соответствующих талантов.</p>
      <div v-if="talentAbilities.length" class="ability-grid">
        <button
          v-for="ability in talentAbilities"
          :key="ability.id"
          type="button"
          @click="selectedAbility = ability"
        >
          <span class="ability-icon">
            <img v-if="abilityArt(ability)" :src="abilityArt(ability)!" alt="" />
            <b v-else>{{ abilityInitials(ability) }}</b>
          </span>
          <span><strong>{{ abilityName(ability) }}</strong><small>{{ ability.sourceTalentName }}</small></span>
        </button>
      </div>
      <p v-else class="empty-copy">Активные способности из талантов ещё не изучены.</p>
      <button
        v-for="ability in baselineAbilities"
        :key="ability.id"
        class="baseline-ability"
        type="button"
        @click="selectedAbility = ability"
      >
        Базовое действие · {{ abilityName(ability) }}
      </button>
    </UIPanel>

    <UIModal :open="selectedItem !== null" :title="selectedItem?.name ?? ''" @close="selectedItem = null">
      <article v-if="selectedItem" class="detail">
        <p>{{ rarityLabel(selectedItem) }} снаряжение · надето</p>
        <p>{{ selectedItem.description }}</p>
        <dl v-if="itemStats(selectedItem).length">
          <div v-for="row in itemStats(selectedItem)" :key="row"><dt>{{ row }}</dt></div>
        </dl>
        <small>Требуемый уровень: {{ selectedItem.requiredLevel }}</small>
      </article>
    </UIModal>

    <UIModal :open="selectedAbility !== null" :title="selectedAbility ? abilityName(selectedAbility) : ''" @close="selectedAbility = null">
      <article v-if="selectedAbility" class="detail ability-detail">
        <div class="ability-detail__head">
          <span class="ability-icon ability-icon--large">
            <img v-if="abilityArt(selectedAbility)" :src="abilityArt(selectedAbility)!" :alt="abilityName(selectedAbility)" />
            <b v-else>{{ abilityInitials(selectedAbility) }}</b>
          </span>
          <div>
            <p v-if="selectedAbility.sourceTalentName">Получено из таланта «{{ selectedAbility.sourceTalentName }}»</p>
            <p v-else>Базовая способность класса</p>
          </div>
        </div>
        <p>{{ abilityDescription(selectedAbility) }}</p>
        <dl>
          <div><dt>Стоимость</dt><dd>{{ selectedAbility.resourceCost }} ярости</dd></div>
          <div><dt>Перезарядка</dt><dd>{{ selectedAbility.cooldownSeconds }} сек.</dd></div>
          <div><dt>Тип</dt><dd>{{ abilityTypeLabel(selectedAbility.type) }}</dd></div>
          <div><dt>Цель</dt><dd>{{ abilityTargetLabel(selectedAbility.targetType) }}</dd></div>
        </dl>
      </article>
    </UIModal>
  </section>
</template>

<style scoped>
.overview { display: grid; width: min(100%, var(--ui-content-width)); margin-inline: auto; gap: var(--ui-space-4); padding: var(--ui-space-5) var(--ui-space-4) var(--ui-space-7); }
.hero-card { position: relative; display: grid; min-height: 12rem; grid-template-columns: minmax(0, 1fr) 8.5rem; align-items: end; overflow: hidden; padding: var(--ui-space-5); border: 1px solid var(--ui-color-border-strong); border-radius: var(--ui-radius-lg); background: radial-gradient(circle at 85% 20%, rgb(96 82 255 / 18%), transparent 45%), linear-gradient(145deg, var(--ui-color-surface-3), var(--ui-color-surface-1)); box-shadow: var(--ui-shadow-panel); }
.hero-card::after { position: absolute; right: -3rem; bottom: -5rem; width: 11rem; height: 11rem; border-radius: 50%; background: rgb(105 93 255 / 10%); content: ''; filter: blur(16px); }
.hero-card__copy { z-index: 1; display: grid; gap: var(--ui-space-1); }
.hero-card__copy h1, .hero-card__copy p { margin: 0; }
.hero-card__copy h1 { font-family: var(--ui-font-display); font-size: var(--ui-font-size-2xl); }
.hero-card__copy p, .hero-card__copy small { color: var(--ui-color-text-muted); }
.eyebrow { color: var(--ui-color-primary) !important; font-size: var(--ui-font-size-xs); font-weight: var(--ui-font-weight-bold); letter-spacing: .1em; text-transform: uppercase; }
.hero-card img { z-index: 1; width: 100%; max-height: 11.5rem; object-fit: contain; object-position: center bottom; filter: drop-shadow(0 .8rem 1rem rgb(0 0 0 / 55%)); }
.progress-panel :deep(.ui-panel__body), .vitals-panel :deep(.ui-panel__body) { display: grid; gap: var(--ui-space-3); }
.xp-row { display: flex; justify-content: space-between; gap: var(--ui-space-3); font-size: var(--ui-font-size-sm); }
.xp-row strong { color: var(--ui-color-primary); }
.xp-track { height: .55rem; overflow: hidden; border-radius: var(--ui-radius-round); background: var(--ui-color-surface-3); }
.xp-track i { display: block; height: 100%; border-radius: inherit; background: var(--ui-color-primary); box-shadow: var(--ui-glow-magic); }
.progress-panel small, .panel-hint, .empty-copy { color: var(--ui-color-text-muted); }
.equipment-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: var(--ui-space-2); }
.equipment-slot { display: grid; min-width: 0; min-height: 7.5rem; align-content: center; justify-items: center; gap: var(--ui-space-1); padding: var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: var(--ui-color-surface-2); color: var(--ui-color-text-muted); font: inherit; text-align: center; }
.equipment-slot.filled { border-color: var(--ui-color-primary); color: var(--ui-color-text-primary); }
.equipment-slot > span { display: grid; width: 3rem; height: 3rem; place-items: center; border: 1px solid var(--ui-color-border-strong); border-radius: var(--ui-radius-md); background: var(--ui-color-background); color: var(--ui-color-primary); font-size: 1.45rem; }
.equipment-slot small { font-size: var(--ui-font-size-xs); text-transform: uppercase; }
.equipment-slot strong { display: -webkit-box; overflow: hidden; font-size: var(--ui-font-size-xs); -webkit-box-orient: vertical; -webkit-line-clamp: 2; }
.panel-hint, .empty-copy { margin: var(--ui-space-3) 0 0; font-size: var(--ui-font-size-sm); line-height: var(--ui-line-height-normal); }
.ability-grid { display: grid; gap: var(--ui-space-2); margin-top: var(--ui-space-3); }
.ability-grid button { display: grid; grid-template-columns: auto minmax(0, 1fr); align-items: center; gap: var(--ui-space-3); width: 100%; padding: var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: var(--ui-color-surface-2); color: inherit; font: inherit; text-align: left; }
.ability-grid button > span:last-child { display: grid; gap: 2px; }
.ability-grid small { color: var(--ui-color-text-muted); }
.ability-icon { display: grid; width: 2.75rem; height: 2.75rem; place-items: center; overflow: hidden; border: 1px solid var(--ui-color-border-strong); border-radius: var(--ui-radius-md); background: var(--ui-color-background); color: var(--ui-color-primary); }
.ability-icon img { width: 100%; height: 100%; object-fit: cover; }
.ability-icon--large { width: 4rem; height: 4rem; flex: 0 0 auto; }
.baseline-ability { margin-top: var(--ui-space-3); padding: var(--ui-space-2) var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: transparent; color: var(--ui-color-text-secondary); font: inherit; }
.detail { display: grid; gap: var(--ui-space-3); }
.detail p { margin: 0; color: var(--ui-color-text-muted); line-height: var(--ui-line-height-normal); }
.detail dl { display: grid; gap: var(--ui-space-1); margin: 0; }
.detail dl div { display: flex; justify-content: space-between; gap: var(--ui-space-3); padding: var(--ui-space-2); border-bottom: 1px solid var(--ui-color-border); }
.detail dd { margin: 0; text-align: right; }
.ability-detail__head { display: flex; align-items: center; gap: var(--ui-space-3); }
@media (max-width: 360px) { .overview { padding-inline: var(--ui-space-3); } .hero-card { grid-template-columns: minmax(0, 1fr) 6rem; padding: var(--ui-space-4); } .equipment-slot { min-height: 6.8rem; padding-inline: var(--ui-space-1); } }
</style>
