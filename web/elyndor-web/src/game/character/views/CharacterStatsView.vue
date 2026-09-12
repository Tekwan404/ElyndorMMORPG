<script setup lang="ts">
import { computed, ref } from 'vue'

import type { CharacterStatBreakdown, CharacterStats } from '@/api/contracts'
import { classLabel } from '@/game/character/characterPresentation'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIModal, UIPanel } from '@/ui/components'

const session = useGameSessionStore()
const character = computed(() => session.snapshot?.character)
const selectedStat = ref<StatRow | null>(null)

type ExtraStatId = 'blockChance' | 'blockValueMin' | 'blockValueMax'
type EffectiveStatId = 'armorDamageReductionPercent' | 'magicDamageReductionPercent'
type StatId = keyof CharacterStats | ExtraStatId

type StatRow = {
  id: StatId
  label: string
  description: string
  percent?: boolean
  multiplier?: boolean
  effectiveId?: EffectiveStatId
  effectiveLabel?: string
}

const groups: { title: string; description: string; rows: StatRow[] }[] = [
  {
    title: 'Основные характеристики',
    description: 'Базовые параметры героя. Они растут с уровнем и изменяются экипировкой и талантами.',
    rows: [
      { id: 'strength', label: 'Сила', description: 'Повышает физическую силу атаки воина и дополнительно увеличивает броню.' },
      { id: 'agility', label: 'Ловкость', description: 'Повышает шанс критического удара, уклонение и частично влияет на силу атаки.' },
      { id: 'intellect', label: 'Интеллект', description: 'Повышает силу заклинаний и сопротивление магии.' },
      { id: 'stamina', label: 'Выносливость', description: 'Увеличивает максимальное здоровье, броню и сопротивление магии.' },
    ],
  },
  {
    title: 'Атака',
    description: 'Параметры, которые определяют силу и стабильность наносимого урона.',
    rows: [
      { id: 'attackPower', label: 'Сила атаки', description: 'Основной показатель физического урона. Для воина складывается из Силы, Ловкости и бонусов талантов.' },
      { id: 'spellPower', label: 'Сила заклинаний', description: 'Определяет мощность магических способностей и зависит от Интеллекта.' },
      { id: 'criticalChance', label: 'Шанс критического удара', description: 'Вероятность нанести критический удар с повышенным уроном.', percent: true },
      { id: 'criticalDamage', label: 'Критический урон', description: 'Дополнительный урон, который наносит успешный критический удар.', percent: true },
      { id: 'accuracy', label: 'Меткость', description: 'Вероятность успешно попасть атакой по цели до учёта её уклонения.', percent: true },
      { id: 'armorPenetration', label: 'Пробивание брони', description: 'Прямой параметр пробивания физической защиты. Это не рейтинг брони и не эффективное снижение урона.', percent: true },
      { id: 'magicPenetration', label: 'Пробивание магии', description: 'Прямой параметр пробивания магической защиты. Это не рейтинг сопротивления магии.', percent: true },
      { id: 'attackSpeed', label: 'Скорость атаки', description: 'Множитель скорости обычных атак. Значение 1× соответствует базовой скорости.', multiplier: true },
    ],
  },
  {
    title: 'Защита',
    description: 'Сырые защитные рейтинги и их реальный боевой эффект по серверной формуле.',
    rows: [
      { id: 'maxHp', label: 'Максимальное здоровье', description: 'Максимальный запас здоровья. Основной источник — Выносливость.' },
      {
        id: 'armor',
        label: 'Броня',
        description: 'Рейтинг физической защиты. Итоговое снижение физического урона рассчитывается сервером отдельно и показано рядом.',
        effectiveId: 'armorDamageReductionPercent',
        effectiveLabel: 'снижения физического урона',
      },
      {
        id: 'magicResistance',
        label: 'Сопротивление магии',
        description: 'Рейтинг магической защиты. Итоговое снижение магического урона рассчитывается сервером отдельно и показано рядом.',
        effectiveId: 'magicDamageReductionPercent',
        effectiveLabel: 'снижения магического урона',
      },
      { id: 'dodge', label: 'Уклонение', description: 'Вероятность полностью избежать подходящей для уклонения атаки.', percent: true },
      { id: 'blockChance', label: 'Шанс блока', description: 'Вероятность заблокировать физическую атаку при наличии подходящего щита.', percent: true },
      { id: 'blockValueMin', label: 'Сила блока — минимум', description: 'Минимальное количество входящего физического урона, которое может поглотить успешный блок.' },
      { id: 'blockValueMax', label: 'Сила блока — максимум', description: 'Максимальное количество входящего физического урона, которое может поглотить успешный блок.' },
    ],
  },
]

const primaryAttributeLabel = computed(() => {
  if (character.value?.primaryAttribute === 'STRENGTH') return 'Сила'
  if (character.value?.primaryAttribute === 'AGILITY') return 'Ловкость'
  return 'Интеллект'
})

const breakdowns = computed<Record<string, CharacterStatBreakdown>>(() =>
  (character.value?.statBreakdown ?? {}) as Record<string, CharacterStatBreakdown>,
)

function statValue(row: StatRow): number {
  const stats = character.value?.stats as Record<string, number | undefined> | undefined
  return stats?.[row.id] ?? breakdowns.value[row.id]?.finalValue ?? 0
}

function format(row: StatRow, value?: number): string {
  const resolved = value ?? statValue(row)
  if (row.multiplier) return `${resolved.toFixed(2)}×`
  if (row.percent) return formatPercent(resolved)
  return Number.isInteger(resolved) ? resolved.toString() : resolved.toFixed(1)
}

function formatPercent(value: number): string {
  return `${value.toFixed(2).replace(/\.00$/, '')}%`
}

function effectivePercent(row: StatRow): number | null {
  if (!row.effectiveId) return null
  return breakdowns.value[row.effectiveId]?.finalValue ?? null
}

function isPrimary(id: StatId): boolean {
  return character.value?.primaryAttribute.toLowerCase() === id.toLowerCase()
}

function breakdownFor(row: StatRow): CharacterStatBreakdown | null {
  return breakdowns.value[row.id] ?? null
}

function sourceLabel(source: string): string {
  if (source === 'CLASS_BASE') return 'База класса'
  if (source === 'LEVEL_GROWTH') return 'Рост за уровни'
  if (source === 'EQUIPMENT') return 'Экипировка'
  if (source === 'TALENT_FLAT') return 'Таланты'
  if (source === 'TALENT_PERCENT') return 'Процентные бонусы талантов'
  if (source === 'EFFECTS') return 'Активные эффекты'
  if (source === 'FORMULA_BASE') return 'Базовое значение формулы'
  if (source === 'STRENGTH') return 'Вклад Силы'
  if (source === 'AGILITY') return 'Вклад Ловкости'
  if (source === 'INTELLECT') return 'Вклад Интеллекта'
  if (source === 'STAMINA') return 'Вклад Выносливости'
  if (source === 'EQUIPMENT_BONUS') return 'Бонус экипировки'
  if (source === 'TALENT_BONUS') return 'Бонус талантов'
  return source
}

function contributionValue(row: StatRow, value: number): string {
  const sign = value > 0 ? '+' : ''
  if (row.multiplier) return `${sign}${value.toFixed(2)}`
  if (row.percent) return `${sign}${value.toFixed(2).replace(/\.00$/, '')}%`
  return `${sign}${Number.isInteger(value) ? value : value.toFixed(1)}`
}
</script>

<template>
  <section v-if="character" class="stats-view">
    <header class="stats-header">
      <div>
        <p>Характеристики</p>
        <h1>{{ character.name }}</h1>
        <small>{{ classLabel(character.classId) }} · уровень {{ character.level }} · основной параметр: {{ primaryAttributeLabel }}</small>
      </div>
    </header>

    <p class="stats-intro">Нажмите на любую характеристику, чтобы увидеть, что она делает и из каких источников складывается её текущее значение.</p>

    <UIPanel v-for="group in groups" :key="group.title" class="stat-group">
      <template #title>{{ group.title }}</template>
      <p class="group-description">{{ group.description }}</p>
      <div class="stat-list">
        <button
          v-for="row in group.rows"
          :key="row.id"
          type="button"
          class="stat-row"
          :data-stat="row.id"
          :class="{ primary: isPrimary(row.id) }"
          @click="selectedStat = row"
        >
          <span>
            <strong>{{ row.label }}</strong>
            <small v-if="isPrimary(row.id)">Основная характеристика класса</small>
            <small v-else>Нажмите для подробностей</small>
          </span>
          <div class="stat-row__value">
            <b>{{ format(row) }}</b>
            <small v-if="effectivePercent(row) !== null">
              {{ formatPercent(effectivePercent(row)!) }} {{ row.effectiveLabel }}
            </small>
          </div>
          <i>›</i>
        </button>
      </div>
    </UIPanel>

    <UIModal :open="selectedStat !== null" :title="selectedStat?.label ?? ''" @close="selectedStat = null">
      <article v-if="selectedStat" class="stat-detail">
        <div class="stat-detail__value">
          <small>Текущее значение</small>
          <strong>{{ format(selectedStat) }}</strong>
        </div>
        <div v-if="effectivePercent(selectedStat) !== null" class="stat-detail__effective">
          <small>Эффективное значение</small>
          <strong>{{ formatPercent(effectivePercent(selectedStat)!) }}</strong>
          <span>{{ selectedStat.effectiveLabel }} · до учёта пробивания атакующего</span>
        </div>
        <p>{{ selectedStat.description }}</p>

        <section>
          <h3>Из чего складывается</h3>
          <dl v-if="breakdownFor(selectedStat)?.contributions.length">
            <div v-for="(entry, index) in breakdownFor(selectedStat)?.contributions" :key="`${entry.source}-${index}`">
              <dt>{{ sourceLabel(entry.source) }}</dt>
              <dd>{{ contributionValue(selectedStat, entry.value) }}</dd>
            </div>
          </dl>
          <p v-else class="no-breakdown">Для этой характеристики сейчас нет дополнительных источников.</p>
        </section>

        <p class="stat-detail__note">Итоговое значение рассчитывается сервером по текущему уровню, экипировке и активному набору талантов.</p>
      </article>
    </UIModal>
  </section>
</template>

<style scoped>
.stats-view { display: grid; width: min(100%, var(--ui-content-width)); margin-inline: auto; gap: var(--ui-space-4); padding: var(--ui-space-5) var(--ui-space-4) var(--ui-space-7); }
.stats-header p, .stats-header h1 { margin: 0; }
.stats-header p { color: var(--ui-color-primary); font-size: var(--ui-font-size-xs); font-weight: var(--ui-font-weight-bold); letter-spacing: .1em; text-transform: uppercase; }
.stats-header h1 { margin-top: var(--ui-space-1); font-family: var(--ui-font-display); font-size: var(--ui-font-size-2xl); }
.stats-header small, .stats-intro, .group-description { color: var(--ui-color-text-muted); }
.stats-intro { margin: 0; line-height: var(--ui-line-height-normal); }
.group-description { margin: 0 0 var(--ui-space-3); font-size: var(--ui-font-size-sm); line-height: var(--ui-line-height-normal); }
.stat-list { display: grid; gap: 1px; overflow: hidden; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: var(--ui-color-border); }
.stat-row { display: grid; grid-template-columns: minmax(0, 1fr) auto auto; align-items: center; gap: var(--ui-space-3); min-height: var(--ui-touch-target); padding: var(--ui-space-2) var(--ui-space-3); border: 0; background: var(--ui-color-surface-2); color: inherit; font: inherit; text-align: left; }
.stat-row.primary { background: linear-gradient(90deg, rgb(105 93 255 / 14%), var(--ui-color-surface-2)); }
.stat-row > span { display: grid; min-width: 0; gap: 2px; }
.stat-row strong { color: var(--ui-color-text-primary); }
.stat-row small { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.stat-row__value { display: grid; justify-items: end; gap: 1px; text-align: right; }
.stat-row__value b { color: var(--ui-color-primary); font-size: var(--ui-font-size-md); font-variant-numeric: tabular-nums; }
.stat-row__value small { max-width: 11rem; color: var(--ui-color-success); line-height: 1.2; }
.stat-row > i { color: var(--ui-color-text-muted); font-size: 1.4rem; font-style: normal; }
.stat-detail { display: grid; gap: var(--ui-space-4); }
.stat-detail > p { margin: 0; color: var(--ui-color-text-muted); line-height: var(--ui-line-height-normal); }
.stat-detail__value { display: flex; align-items: end; justify-content: space-between; gap: var(--ui-space-3); padding: var(--ui-space-3); border: 1px solid var(--ui-color-primary); border-radius: var(--ui-radius-md); background: rgb(105 93 255 / 9%); }
.stat-detail__value small { color: var(--ui-color-text-muted); }
.stat-detail__value strong { color: var(--ui-color-primary); font-family: var(--ui-font-display); font-size: var(--ui-font-size-2xl); }
.stat-detail__effective { display: grid; grid-template-columns: 1fr auto; align-items: center; gap: 2px var(--ui-space-3); padding: var(--ui-space-3); border: 1px solid var(--ui-color-success); border-radius: var(--ui-radius-md); background: rgb(61 184 129 / 8%); }
.stat-detail__effective small, .stat-detail__effective span { color: var(--ui-color-text-muted); }
.stat-detail__effective strong { color: var(--ui-color-success); font-size: var(--ui-font-size-xl); font-variant-numeric: tabular-nums; }
.stat-detail__effective span { grid-column: 1 / -1; font-size: var(--ui-font-size-xs); }
.stat-detail section { display: grid; gap: var(--ui-space-2); }
.stat-detail h3 { margin: 0; font-size: var(--ui-font-size-md); }
.stat-detail dl { display: grid; gap: 1px; margin: 0; overflow: hidden; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: var(--ui-color-border); }
.stat-detail dl div { display: flex; justify-content: space-between; gap: var(--ui-space-3); padding: var(--ui-space-2) var(--ui-space-3); background: var(--ui-color-surface-2); }
.stat-detail dt { color: var(--ui-color-text-secondary); }
.stat-detail dd { margin: 0; color: var(--ui-color-success); font-weight: var(--ui-font-weight-semibold); font-variant-numeric: tabular-nums; }
.no-breakdown { margin: 0; color: var(--ui-color-text-muted); }
.stat-detail__note { padding-top: var(--ui-space-3); border-top: 1px solid var(--ui-color-border); font-size: var(--ui-font-size-xs); }
@media (max-width: 360px) { .stats-view { padding-inline: var(--ui-space-3); } .stat-row { padding-inline: var(--ui-space-2); } .stat-row__value small { max-width: 8rem; } }
</style>
