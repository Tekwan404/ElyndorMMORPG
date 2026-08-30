<script setup lang="ts">
import { computed } from 'vue'

import type { CharacterStats } from '@/api/contracts'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIHealthBar, UIPanel } from '@/ui/components'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { IconConfig } from '@/ui/icons/icon.types'

const session = useGameSessionStore()
const character = computed(() => session.snapshot?.character)

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
  return `${value.charAt(0).toUpperCase()}${value.slice(1)}`
})

function format(row: StatRow): string {
  const value = character.value?.stats[row.id] ?? 0
  if (row.multiplier) return `${value.toFixed(2)}×`
  if (row.percent) return `${value.toFixed(2).replace(/\.00$/, '')}%`
  return Number.isInteger(value) ? value.toString() : value.toFixed(1)
}

function isPrimary(id: keyof CharacterStats): boolean {
  return character.value?.primaryAttribute.toLowerCase() === id.toLowerCase()
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
      <IconGenerator
        class="portrait"
        :config="portraitIcon"
        :label="`Класс ${character.classId}`"
      />
    </header>

    <UIPanel class="vitals">
      <template #title>Состояние</template>
      <UIHealthBar
        label="Health"
        :value="character.vitals.currentHp"
        :max="character.vitals.maxHp"
      />
      <UIHealthBar
        :label="resourceLabel"
        :tone="resourceTone"
        :value="character.vitals.currentResource"
        :max="character.vitals.maxResource"
      />
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
  </section>
</template>

<style scoped>
.character {
  display: grid;
  width: min(100%, var(--ui-content-width));
  margin-inline: auto;
  gap: var(--ui-space-4);
  padding: var(--ui-space-6) calc(var(--ui-space-4) + var(--ui-safe-area-right)) var(--ui-space-7)
    calc(var(--ui-space-4) + var(--ui-safe-area-left));
}
.character__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-4);
}
.kicker,
.summary,
.version {
  color: var(--ui-color-text-muted);
}
.kicker {
  margin: 0;
  color: var(--ui-color-primary);
  font-size: var(--ui-font-size-xs);
  font-weight: var(--ui-font-weight-bold);
  letter-spacing: var(--ui-space-1);
  text-transform: uppercase;
}
h1 {
  margin: var(--ui-space-1) 0;
  color: var(--ui-color-text-primary);
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-2xl);
}
.summary {
  margin: 0;
}
.portrait {
  width: var(--ui-icon-slot-lg);
  height: var(--ui-icon-slot-lg);
  flex: 0 0 auto;
}
.vitals :deep(.ui-panel__body) {
  display: grid;
  gap: var(--ui-space-3);
}
dl {
  margin: 0;
}
dl div {
  display: flex;
  min-height: var(--ui-touch-target);
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-3);
  padding: var(--ui-space-2);
  border-bottom: 1px solid var(--ui-color-border);
}
dl div:last-child {
  border-bottom: 0;
}
dt {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--ui-space-2);
  color: var(--ui-color-text-secondary);
}
dt small {
  color: var(--ui-color-primary);
  font-size: var(--ui-font-size-xs);
  text-transform: uppercase;
}
dd {
  margin: 0;
  color: var(--ui-color-text-primary);
  font-variant-numeric: tabular-nums;
}
.primary {
  background: var(--ui-color-surface-3);
}
.version {
  margin: 0;
  font-size: var(--ui-font-size-xs);
  text-align: right;
}
@media (max-width: 360px) {
  .character {
    padding-inline: calc(var(--ui-space-3) + var(--ui-safe-area-left))
      calc(var(--ui-space-3) + var(--ui-safe-area-right));
  }
  .portrait {
    width: var(--ui-icon-slot-md);
    height: var(--ui-icon-slot-md);
  }
}
</style>
