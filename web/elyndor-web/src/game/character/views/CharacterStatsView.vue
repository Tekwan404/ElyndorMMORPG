<script setup lang="ts">
import { computed } from 'vue'

import type { CharacterStats } from '@/api/contracts'
import { useGameSessionStore } from '@/stores/gameSession'

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
  <section v-if="character" class="character" :data-class="character.classId">
    <p class="kicker">Герой</p>
    <h1>{{ character.name }}</h1>
    <p class="summary">Уровень {{ character.level }} · {{ character.classId }}</p>

    <div class="portrait" aria-hidden="true">
      <span>{{
        character.classId === 'WARRIOR' ? '⚔' : character.classId === 'ARCHER' ? '➶' : '✦'
      }}</span>
    </div>

    <div class="vitals-summary">
      <span>HP {{ character.vitals.currentHp }} / {{ character.vitals.maxHp }}</span>
      <span
        >{{ character.vitals.resourceType }} {{ character.vitals.currentResource }} /
        {{ character.vitals.maxResource }}</span
      >
    </div>

    <section v-for="group in groups" :key="group.title" class="stat-group">
      <h2>{{ group.title }}</h2>
      <dl>
        <div v-for="row in group.rows" :key="row.id" :class="{ primary: isPrimary(row.id) }">
          <dt>{{ row.label }}<small v-if="isPrimary(row.id)">основной</small></dt>
          <dd>{{ format(row) }}</dd>
        </div>
      </dl>
    </section>

    <p class="version">Balance {{ character.classProfileVersion }}</p>
  </section>
</template>

<style scoped lang="scss">
.character {
  width: min(100%, 480px);
  margin: auto;
  padding: 22px 16px 34px;
}
.kicker,
.summary,
.version {
  color: var(--color-text-muted);
}
.kicker {
  margin: 0;
  color: var(--color-gold);
  font-size: 0.7rem;
  letter-spacing: 0.16em;
  text-transform: uppercase;
}
h1 {
  margin: 4px 0;
  color: #f3ead7;
  font:
    2rem Georgia,
    serif;
}
.summary {
  margin-top: 0;
}
.portrait {
  display: grid;
  min-height: 145px;
  margin: 16px 0;
  border-block: 1px solid rgb(200 169 99 / 30%);
  background: radial-gradient(circle, rgb(90 77 178 / 35%), transparent 65%);
  place-items: center;
}
.portrait span {
  color: #d5ba77;
  font-size: 4.5rem;
  filter: drop-shadow(0 0 18px #6459b8);
}
[data-class='WARRIOR'] .portrait {
  background: radial-gradient(circle, rgb(151 70 64 / 34%), transparent 65%);
}
[data-class='ARCHER'] .portrait {
  background: radial-gradient(circle, rgb(46 133 113 / 34%), transparent 65%);
}
.vitals-summary {
  display: flex;
  gap: 8px;
  justify-content: space-between;
  color: var(--color-text-secondary);
  font-size: 0.75rem;
}
.stat-group h2 {
  margin: 22px 0 8px;
  color: var(--color-gold);
  font-size: 0.72rem;
  letter-spacing: 0.12em;
  text-transform: uppercase;
}
dl {
  margin: 0;
  border-block: 1px solid var(--color-border);
}
dl div {
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 40px;
  padding: 7px 10px;
  border-bottom: 1px solid rgb(120 137 167 / 12%);
}
dl div:last-child {
  border: 0;
}
dt {
  display: flex;
  gap: 8px;
  color: var(--color-text-secondary);
}
dt small {
  color: var(--color-gold);
  font-size: 0.58rem;
  text-transform: uppercase;
}
dd {
  margin: 0;
  color: #f1e9d8;
  font-variant-numeric: tabular-nums;
}
.primary {
  background: linear-gradient(90deg, rgb(183 146 65 / 12%), transparent);
}
.version {
  margin-top: 18px;
  font-size: 0.65rem;
  text-align: right;
}
</style>
