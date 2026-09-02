<script setup lang="ts">
import { computed, ref } from 'vue'

import type { CharacterStats, KnownAbility } from '@/api/contracts'
import { gameArt } from '@/assets/gameArt'
import { resolveAbilityArt } from '@/game/talents/talentArt'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIHealthBar, UIModal, UIPanel } from '@/ui/components'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { IconConfig } from '@/ui/icons/icon.types'

const session = useGameSessionStore()
const character = computed(() => session.snapshot?.character)
const selectedAbility = ref<KnownAbility | null>(null)
const talentAbilities = computed(() => character.value?.knownAbilities.filter((ability) => ability.sourceTalentId) ?? [])
const baselineAbilities = computed(() => character.value?.knownAbilities.filter((ability) => !ability.sourceTalentId) ?? [])

type StatRow = { id: keyof CharacterStats; label: string; percent?: boolean; multiplier?: boolean }

const groups: { title: string; rows: StatRow[] }[] = [
  {
    title: 'Основные',
    rows: [
      { id: 'strength', label: 'Сила' },
      { id: 'agility', label: 'Ловкость' },
      { id: 'intellect', label: 'Интеллект' },
      { id: 'stamina', label: 'Выносливость' },
    ],
  },
  {
    title: 'Атака',
    rows: [
      { id: 'attackPower', label: 'Сила атаки' },
      { id: 'spellPower', label: 'Сила заклинаний' },
      { id: 'criticalChance', label: 'Шанс крита', percent: true },
      { id: 'criticalDamage', label: 'Критический урон', percent: true },
      { id: 'accuracy', label: 'Меткость' },
      { id: 'armorPenetration', label: 'Пробивание брони' },
      { id: 'magicPenetration', label: 'Пробивание магии' },
      { id: 'attackSpeed', label: 'Скорость атаки', multiplier: true },
    ],
  },
  {
    title: 'Защита',
    rows: [
      { id: 'armor', label: 'Броня' },
      { id: 'magicResistance', label: 'Сопротивление магии' },
      { id: 'dodge', label: 'Уклонение', percent: true },
    ],
  },
]

const portraitIcon = computed<IconConfig>(() => ({
  id: `portrait-${character.value?.classId.toLowerCase() ?? 'hero'}`,
  glyph:
    character.value?.classId === 'WARRIOR'
      ? 'shield'
      : character.value?.classId === 'ARCHER'
        ? 'bow'
        : 'staff',
  category: 'utility',
  modifier:
    character.value?.classId === 'WARRIOR'
      ? 'fire'
      : character.value?.classId === 'ARCHER'
        ? 'poison'
        : 'ice',
  rarity: 'rare',
}))
const resourceTone = computed<'rage' | 'focus' | 'mana'>(() => {
  const value = character.value?.vitals.resourceType.toLowerCase()
  return value === 'rage' || value === 'mana' ? value : 'focus'
})
const resourceLabel = computed(() => {
  const value = character.value?.vitals.resourceType.toLowerCase() ?? 'resource'
  return value === 'rage' ? 'Ярость' : value === 'mana' ? 'Мана' : 'Фокус'
})

const abilityPresentation: Record<string, { name: string; description: string }> = {
  STRIKE: { name: 'Удар', description: 'Базовая атака воина. Наносит физический урон и помогает накапливать ярость.' },
  WILD_STRIKE: { name: 'Дикий удар', description: 'Мощная одиночная атака, открываемая талантом. Наносит повышенный физический урон.' },
  WHIRLWIND: { name: 'Вихрь', description: 'Размашистая атака по всем противникам в бою. Открывается соответствующим талантом.' },
  BASTION: { name: 'Бастион', description: 'Защитная способность, временно заметно снижающая входящий урон.' },
  BERSERK: { name: 'Берсерк', description: 'Боевой режим, временно усиливающий силу атаки, шанс критического удара и скорость атаки.' },
  SHIELD_BASH: { name: 'Удар щитом', description: 'Физический удар щитом с коротким оглушением цели.' },
  PROVOKE: { name: 'Провокация', description: 'Заставляет выбранного противника удерживать внимание на воине.' },
  HEAVY_BLOW: { name: 'Тяжёлый удар', description: 'Сильный одиночный физический удар с повышенным коэффициентом силы атаки.' },
  BATTLE_FOCUS: { name: 'Боевой фокус', description: 'Кратковременно повышает силу атаки.' },
  BATTLE_SHOUT: { name: 'Боевой клич', description: 'Мгновенно даёт дополнительную ярость.' },
}

function format(row: StatRow): string {
  const value = character.value?.stats[row.id] ?? 0
  if (row.multiplier) return `${value.toFixed(2)}×`
  if (row.percent) return `${value.toFixed(2).replace(/\.00$/, '')}%`
  return Number.isInteger(value) ? value.toString() : value.toFixed(1)
}

function isPrimary(id: keyof CharacterStats): boolean {
  return character.value?.primaryAttribute.toLowerCase() === id.toLowerCase()
}

function abilityName(ability: KnownAbility): string {
  return abilityPresentation[ability.id]?.name ?? ability.id.replace(/_/g, ' ')
}

function abilityDescription(ability: KnownAbility): string {
  return abilityPresentation[ability.id]?.description
    ?? `Активная способность. Тип: ${ability.type}. Цель: ${ability.targetType}.`
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
  <section v-if="character" class="character">
    <header class="character__header">
      <div>
        <p class="kicker">Герой</p>
        <h1>{{ character.name }}</h1>
        <p class="summary">Уровень {{ character.level }} · {{ character.classId }}</p>
      </div>
      <img
        v-if="character.classId === 'WARRIOR'"
        class="warrior-portrait"
        :src="gameArt.characters.warrior"
        alt="Воин"
      />
      <IconGenerator
        v-else
        class="portrait"
        :config="portraitIcon"
        :label="`Класс ${character.classId}`"
      />
    </header>

    <UIPanel class="vitals">
      <template #title>Состояние</template>
      <UIHealthBar label="Здоровье" :value="character.vitals.currentHp" :max="character.vitals.maxHp" />
      <UIHealthBar
        :label="resourceLabel"
        :tone="resourceTone"
        :value="character.vitals.currentResource"
        :max="character.vitals.maxResource"
      />
    </UIPanel>

    <UIPanel v-if="character.classId === 'WARRIOR'" class="abilities">
      <template #title>Боевые способности</template>
      <p class="abilities__hint">Здесь показаны только активные способности, которые реально открыты вашим текущим билдом талантов.</p>

      <div v-if="talentAbilities.length" class="ability-list" aria-label="Способности из талантов">
        <button
          v-for="ability in talentAbilities"
          :key="ability.id"
          class="ability-row"
          type="button"
          :data-ability="ability.id"
          @click="selectedAbility = ability"
        >
          <span class="ability-row__icon">
            <img v-if="abilityArt(ability)" :src="abilityArt(ability)!" alt="" />
            <b v-else>{{ abilityInitials(ability) }}</b>
          </span>
          <span class="ability-row__copy">
            <strong>{{ abilityName(ability) }}</strong>
            <small>{{ ability.sourceTalentName }} · {{ ability.resourceCost }} яр. · КД {{ ability.cooldownSeconds }}с</small>
          </span>
          <span class="ability-row__chevron">›</span>
        </button>
      </div>
      <p v-else class="abilities__empty">Активные способности из талантов ещё не изучены.</p>

      <div v-if="baselineAbilities.length" class="baseline">
        <small>Базовое действие</small>
        <button v-for="ability in baselineAbilities" :key="ability.id" type="button" @click="selectedAbility = ability">
          {{ abilityName(ability) }}
        </button>
      </div>
    </UIPanel>

    <UIPanel v-for="group in groups" :key="group.title" class="stat-group">
      <template #title>{{ group.title }}</template>
      <dl>
        <div
          v-for="row in group.rows"
          :key="row.id"
          :data-stat="row.id"
          :class="{ primary: isPrimary(row.id) }"
        >
          <dt>{{ row.label }}<small v-if="isPrimary(row.id)">основной</small></dt>
          <dd>{{ format(row) }}</dd>
        </div>
      </dl>
    </UIPanel>

    <p class="version">Balance {{ character.classProfileVersion }}</p>

    <UIModal :open="selectedAbility !== null" :title="selectedAbility ? abilityName(selectedAbility) : ''" @close="selectedAbility = null">
      <article v-if="selectedAbility" class="ability-detail">
        <div class="ability-detail__identity">
          <span class="ability-detail__icon">
            <img v-if="abilityArt(selectedAbility)" :src="abilityArt(selectedAbility)!" :alt="abilityName(selectedAbility)" />
            <b v-else>{{ abilityInitials(selectedAbility) }}</b>
          </span>
          <div>
            <p v-if="selectedAbility.sourceTalentName">Талант · {{ selectedAbility.sourceTalentName }}</p>
            <p v-else>Базовая способность класса</p>
            <strong>Работает в текущем combat runtime</strong>
          </div>
        </div>
        <p class="ability-detail__description">{{ abilityDescription(selectedAbility) }}</p>
        <dl>
          <div><dt>Стоимость</dt><dd>{{ selectedAbility.resourceCost }} ярости</dd></div>
          <div><dt>Перезарядка</dt><dd>{{ selectedAbility.cooldownSeconds }} сек.</dd></div>
          <div><dt>Тип</dt><dd>{{ selectedAbility.type }}</dd></div>
        </dl>
      </article>
    </UIModal>
  </section>
</template>

<style scoped>
.character { display: grid; width: min(100%, var(--ui-content-width)); margin-inline: auto; gap: var(--ui-space-4); padding: var(--ui-space-6) calc(var(--ui-space-4) + var(--ui-safe-area-right)) var(--ui-space-7) calc(var(--ui-space-4) + var(--ui-safe-area-left)); }
.character__header { display: flex; align-items: center; justify-content: space-between; gap: var(--ui-space-4); }
.kicker, .summary, .version { color: var(--ui-color-text-muted); }
.kicker { margin: 0; color: var(--ui-color-primary); font-size: var(--ui-font-size-xs); font-weight: var(--ui-font-weight-bold); letter-spacing: var(--ui-space-1); text-transform: uppercase; }
h1 { margin: var(--ui-space-1) 0; color: var(--ui-color-text-primary); font-family: var(--ui-font-display); font-size: var(--ui-font-size-2xl); }
.summary { margin: 0; }
.portrait { width: var(--ui-icon-slot-lg); height: var(--ui-icon-slot-lg); flex: 0 0 auto; }
.warrior-portrait { width: clamp(6rem, 26vw, 8.5rem); height: 8.5rem; flex: 0 0 auto; object-fit: contain; object-position: center bottom; filter: drop-shadow(0 0.5rem 0.8rem rgb(0 0 0 / 55%)); }
.vitals :deep(.ui-panel__body) { display: grid; gap: var(--ui-space-3); }
.abilities__hint, .abilities__empty { margin: 0 0 var(--ui-space-3); color: var(--ui-color-text-muted); font-size: var(--ui-font-size-sm); line-height: var(--ui-line-height-normal); }
.ability-list { display: grid; gap: var(--ui-space-2); }
.ability-row { display: grid; grid-template-columns: auto minmax(0, 1fr) auto; min-height: var(--ui-touch-target); align-items: center; gap: var(--ui-space-3); width: 100%; padding: var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: var(--ui-color-surface-2); color: inherit; font: inherit; text-align: left; }
.ability-row__icon { display: grid; width: 2.75rem; height: 2.75rem; place-items: center; overflow: hidden; border: 1px solid var(--ui-color-border-strong); border-radius: var(--ui-radius-md); background: var(--ui-color-background); color: var(--ui-color-primary); }
.ability-row__icon img { width: 100%; height: 100%; object-fit: cover; }
.ability-row__copy { display: grid; min-width: 0; gap: 2px; }
.ability-row__copy strong { color: var(--ui-color-text-primary); }
.ability-row__copy small { overflow: hidden; color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); text-overflow: ellipsis; white-space: nowrap; }
.ability-row__chevron { color: var(--ui-color-primary); font-size: 1.5rem; }
.baseline { display: flex; align-items: center; gap: var(--ui-space-2); margin-top: var(--ui-space-3); padding-top: var(--ui-space-3); border-top: 1px solid var(--ui-color-border); }
.baseline small { color: var(--ui-color-text-muted); }
.baseline button { min-height: 2rem; padding: 0 var(--ui-space-2); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); background: transparent; color: var(--ui-color-text-secondary); font: inherit; font-size: var(--ui-font-size-xs); }
dl { margin: 0; }
.stat-group dl div { display: flex; min-height: var(--ui-touch-target); align-items: center; justify-content: space-between; gap: var(--ui-space-3); padding: var(--ui-space-2); border-bottom: 1px solid var(--ui-color-border); }
.stat-group dl div:last-child { border-bottom: 0; }
dt { display: flex; flex-wrap: wrap; align-items: center; gap: var(--ui-space-2); color: var(--ui-color-text-secondary); }
dt small { color: var(--ui-color-primary); font-size: var(--ui-font-size-xs); text-transform: uppercase; }
dd { margin: 0; color: var(--ui-color-text-primary); font-variant-numeric: tabular-nums; }
.primary { background: var(--ui-color-surface-3); }
.version { margin: 0; font-size: var(--ui-font-size-xs); text-align: right; }
.ability-detail { display: grid; gap: var(--ui-space-4); }
.ability-detail__identity { display: flex; align-items: center; gap: var(--ui-space-3); }
.ability-detail__identity p, .ability-detail__description { margin: 0; color: var(--ui-color-text-muted); }
.ability-detail__identity div { display: grid; gap: var(--ui-space-1); }
.ability-detail__icon { display: grid; width: 4rem; height: 4rem; flex: 0 0 auto; place-items: center; overflow: hidden; border: 1px solid var(--ui-color-primary); border-radius: var(--ui-radius-md); background: var(--ui-color-surface-2); color: var(--ui-color-primary); }
.ability-detail__icon img { width: 100%; height: 100%; object-fit: cover; }
.ability-detail dl { display: grid; gap: var(--ui-space-1); }
.ability-detail dl div { display: flex; justify-content: space-between; gap: var(--ui-space-3); padding: var(--ui-space-2); border-bottom: 1px solid var(--ui-color-border); }
@media (max-width: 360px) { .character { padding-inline: calc(var(--ui-space-3) + var(--ui-safe-area-left)) calc(var(--ui-space-3) + var(--ui-safe-area-right)); } .warrior-portrait { width: 5.5rem; height: 7rem; } }
</style>