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
    <header class="hero-card">
      <div class="hero-card__copy">
        <p class="eyebrow">Персонаж</p>
        <h1>{{ character.name }}</h1>
        <p>{{ raceLabel(character.raceId) }} · {{ classLabel(character.classId) }} · уровень {{ character.level }}</p>
        <small>{{ genderLabel(character.genderId) }} персонаж</small>
        <strong class="gold">● {{ character.gold }} золота</strong>
      </div>
      <img v-if="character.classId === 'WARRIOR'" :src="gameArt.characters.warrior" alt="Воин" />
    </header>

    <UIPanel>
      <template #title>Развитие героя</template>
      <div class="xp-row"><span>Уровень {{ character.level }}</span><strong>{{ character.experience }} / {{ xpTarget }} опыта</strong></div>
      <div class="xp-track"><i :style="{ width: `${xpTarget > 0 ? Math.min(100, character.experience / xpTarget * 100) : 100}%` }" /></div>
      <small>До следующего уровня: {{ xpRemaining }} опыта</small>
    </UIPanel>

    <UIPanel class="vitals-panel">
      <template #title>Состояние</template>
      <UIHealthBar label="Здоровье" :value="character.vitals.currentHp" :max="character.vitals.maxHp" />
      <UIHealthBar :label="resourceLabel(character.vitals.resourceType)" :tone="character.vitals.resourceType === 'RAGE' ? 'rage' : character.vitals.resourceType === 'MANA' ? 'mana' : 'focus'" :value="character.vitals.currentResource" :max="character.vitals.maxResource" />
    </UIPanel>

    <UIPanel>
      <template #title>Надетое снаряжение</template>
      <div class="equipment-grid">
        <button v-for="slot in equipment" :key="slot.id" type="button" class="equipment-slot" :class="{ filled: slot.item }" :disabled="!slot.item" @click="selectedItem = slot.item">
          <small>{{ slot.label }}</small><span>{{ itemGlyph(slot.item, slot.glyph) }}</span><strong>{{ slot.item?.name ?? 'Пусто' }}</strong>
        </button>
      </div>
      <div class="set-progress">
        <strong>Комплект Следопыта · {{ rangerPieces }} / 6</strong>
        <span :class="{ active: rangerPieces >= 3 }">3 предмета: +5% скорости атаки</span>
        <span :class="{ active: rangerPieces >= 6 }">6 предметов: ещё +10% скорости атаки и +5% уклонения</span>
      </div>
    </UIPanel>

    <UIPanel v-if="character.classId === 'WARRIOR'">
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
.overview{display:grid;width:min(100%,var(--ui-content-width));margin-inline:auto;gap:var(--ui-space-4);padding:var(--ui-space-5) var(--ui-space-4) var(--ui-space-7)}
.hero-card{display:grid;grid-template-columns:minmax(0,1fr) 8rem;align-items:end;min-height:12rem;padding:var(--ui-space-5);overflow:hidden;border:1px solid var(--ui-color-border-strong);border-radius:var(--ui-radius-lg);background:radial-gradient(circle at 85% 20%,rgb(96 82 255 / 18%),transparent 45%),var(--ui-color-surface-2)}
.hero-card__copy{display:grid;gap:var(--ui-space-1)}.hero-card h1,.hero-card p{margin:0}.hero-card img{width:100%;max-height:11rem;object-fit:contain}.eyebrow{color:var(--ui-color-primary);font-size:var(--ui-font-size-xs);font-weight:700;text-transform:uppercase}.gold{margin-top:var(--ui-space-2);color:#e8c866}
.xp-row{display:flex;justify-content:space-between;gap:var(--ui-space-3)}.xp-track{height:.55rem;margin:var(--ui-space-2) 0;overflow:hidden;border-radius:999px;background:var(--ui-color-surface-3)}.xp-track i{display:block;height:100%;background:var(--ui-color-primary)}
.vitals-panel :deep(.ui-panel__body){display:grid;gap:var(--ui-space-3)}
.equipment-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:var(--ui-space-2)}.equipment-slot{display:grid;min-height:7rem;align-content:center;justify-items:center;gap:var(--ui-space-1);padding:var(--ui-space-2);border:1px solid var(--ui-color-border);border-radius:var(--ui-radius-md);background:var(--ui-color-surface-2);color:var(--ui-color-text-muted);font:inherit;text-align:center}.equipment-slot.filled{border-color:var(--ui-color-primary);color:var(--ui-color-text-primary)}.equipment-slot span{font-size:1.5rem;color:var(--ui-color-primary)}.equipment-slot strong{font-size:var(--ui-font-size-xs)}
.set-progress{display:grid;gap:var(--ui-space-1);margin-top:var(--ui-space-3);padding:var(--ui-space-3);border:1px solid var(--ui-color-border);border-radius:var(--ui-radius-md)}.set-progress span{color:var(--ui-color-text-muted);font-size:var(--ui-font-size-sm)}.set-progress span.active{color:var(--ui-color-success)}
.hint{margin:0 0 var(--ui-space-3);color:var(--ui-color-text-muted)}.ability-grid{display:grid;gap:var(--ui-space-2)}.ability-grid button{display:grid;grid-template-columns:auto 1fr;align-items:center;gap:var(--ui-space-3);padding:var(--ui-space-2);border:1px solid var(--ui-color-border);border-radius:var(--ui-radius-md);background:var(--ui-color-surface-2);color:inherit;font:inherit;text-align:left}.ability-grid button>span:last-child{display:grid}.ability-grid small{color:var(--ui-color-text-muted)}.ability-icon{display:grid;width:2.75rem;height:2.75rem;place-items:center;overflow:hidden;border-radius:var(--ui-radius-md);background:var(--ui-color-background)}.ability-icon img{width:100%;height:100%;object-fit:cover}
.detail{display:grid;gap:var(--ui-space-3)}.detail p{margin:0;color:var(--ui-color-text-muted)}.detail dl{display:grid;gap:var(--ui-space-1);margin:0}.detail dl div{display:flex;justify-content:space-between;gap:var(--ui-space-3);padding:var(--ui-space-2);border-bottom:1px solid var(--ui-color-border)}.detail dd{margin:0;color:var(--ui-color-text-primary)}
@media(max-width:400px){.equipment-grid{grid-template-columns:repeat(2,minmax(0,1fr))}}
</style>
