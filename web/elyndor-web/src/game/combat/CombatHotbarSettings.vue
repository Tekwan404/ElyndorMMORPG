<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import type { KnownAbility } from '@/api/contracts'
import { isAuraAbility } from '@/game/combat/combatAbilityGroups'
import {
  loadCombatHotbarOrder,
  normalizeCombatHotbarOrder,
  resetCombatHotbarOrder,
  saveCombatHotbarOrder,
} from '@/game/combat/combatHotbarSettings'
import { UIButton } from '@/ui/components'

const HOTBAR_SLOT_COUNT = 12

const props = defineProps<{
  characterId: string
  abilities: KnownAbility[]
}>()

const selectedAbilityId = ref<string | null>(null)
const order = ref<string[]>([])

const hotbarAbilities = computed(() => props.abilities.filter((ability) => !isAuraAbility(ability.id)))
const abilityById = computed(() => new Map(hotbarAbilities.value.map(ability => [ability.id, ability])))
const orderedAbilities = computed(() => order.value
  .map(abilityId => abilityById.value.get(abilityId))
  .filter((ability): ability is KnownAbility => ability !== undefined))
const activeSlots = computed<(KnownAbility | null)[]>(() =>
  Array.from({ length: HOTBAR_SLOT_COUNT }, (_, index) => orderedAbilities.value[index] ?? null))
const reserveAbilities = computed(() => orderedAbilities.value.slice(HOTBAR_SLOT_COUNT))

watch(
  [() => props.characterId, () => props.abilities.map(ability => ability.id).join('|')],
  () => {
    order.value = loadCombatHotbarOrder(props.characterId, hotbarAbilities.value.map(ability => ability.id))
    selectedAbilityId.value = null
  },
  { immediate: true },
)

function selectSlot(abilityId: string): void {
  if (selectedAbilityId.value === null) {
    selectedAbilityId.value = abilityId
    return
  }

  if (selectedAbilityId.value === abilityId) {
    selectedAbilityId.value = null
    return
  }

  const sourceIndex = order.value.indexOf(selectedAbilityId.value)
  const targetIndex = order.value.indexOf(abilityId)
  if (sourceIndex < 0 || targetIndex < 0) {
    selectedAbilityId.value = null
    return
  }

  const next = [...order.value]
  const sourceAbilityId = next[sourceIndex]!
  next[sourceIndex] = next[targetIndex]!
  next[targetIndex] = sourceAbilityId
  order.value = next
  saveCombatHotbarOrder(props.characterId, next)
  selectedAbilityId.value = null
}

function resetOrder(): void {
  resetCombatHotbarOrder(props.characterId)
  order.value = normalizeCombatHotbarOrder(hotbarAbilities.value.map(ability => ability.id), [])
  selectedAbilityId.value = null
}

function abilityMeta(ability: KnownAbility): string {
  const parts: string[] = []
  if (ability.resourceCost > 0) parts.push(`Ресурс ${ability.resourceCost}`)
  if (ability.cooldownSeconds > 0) parts.push(`КД ${ability.cooldownSeconds}с`)
  return parts.join(' · ') || 'Без стоимости и перезарядки'
}
</script>

<template>
  <section class="hotbar-settings" data-combat-hotbar-settings>
    <header class="hotbar-settings__header">
      <small>БОЕВОЙ ИНТЕРФЕЙС</small>
      <h2>Панель способностей</h2>
      <p>Нажми способность, затем другую — они поменяются местами. Первые 12 слотов отображаются в бою в двух рядах по шесть.</p>
    </header>

    <section v-if="orderedAbilities.length" class="hotbar-settings__active" aria-label="Активная панель">
      <strong class="hotbar-settings__section-label">АКТИВНАЯ ПАНЕЛЬ · 12</strong>
      <div class="hotbar-settings__slots" role="list" aria-label="Порядок боевых способностей">
      <button
        v-for="(ability, index) in activeSlots"
        :key="ability?.id ?? `empty-${index}`"
        class="hotbar-slot"
        :class="{
          'hotbar-slot--selected': selectedAbilityId === ability?.id,
          'hotbar-slot--empty': !ability,
        }"
        type="button"
        role="listitem"
        :disabled="!ability"
        :aria-pressed="selectedAbilityId === ability?.id"
        :data-hotbar-slot="index + 1"
        @click="ability && selectSlot(ability.id)"
      >
        <span class="hotbar-slot__number">{{ index + 1 }}</span>
        <span class="hotbar-slot__copy">
          <strong>{{ ability?.displayName ?? 'Пустой слот' }}</strong>
          <small>{{ ability ? abilityMeta(ability) : 'Свободная позиция' }}</small>
        </span>
        <span v-if="ability" class="hotbar-slot__state">
          {{ selectedAbilityId === ability.id ? 'Выбрано' : 'В бою' }}
        </span>
      </button>
      </div>
    </section>

    <section v-if="reserveAbilities.length" class="hotbar-settings__reserve" aria-label="Резерв способностей">
      <strong class="hotbar-settings__section-label">РЕЗЕРВ</strong>
      <div class="hotbar-settings__reserve-list">
        <button
          v-for="ability in reserveAbilities"
          :key="ability.id"
          class="hotbar-slot hotbar-slot--reserve"
          :class="{ 'hotbar-slot--selected': selectedAbilityId === ability.id }"
          type="button"
          :aria-pressed="selectedAbilityId === ability.id"
          @click="selectSlot(ability.id)"
        >
          <span class="hotbar-slot__number">R</span>
          <span class="hotbar-slot__copy"><strong>{{ ability.displayName }}</strong><small>{{ abilityMeta(ability) }}</small></span>
          <span class="hotbar-slot__state">{{ selectedAbilityId === ability.id ? 'Выбрано' : 'Резерв' }}</span>
        </button>
      </div>
    </section>

    <div v-else class="hotbar-settings__empty">У героя пока нет доступных боевых способностей.</div>

    <div v-if="selectedAbilityId" class="hotbar-settings__hint" role="status">
      Теперь выбери второй слот — способности поменяются местами.
    </div>

    <footer class="hotbar-settings__footer">
      <span>Раскладка сохраняется для этого героя на устройстве.</span>
      <UIButton variant="ghost" @click="resetOrder">Сбросить порядок</UIButton>
    </footer>
  </section>
</template>

<style scoped>
.hotbar-settings {
  display: grid;
  gap: 10px;
  padding: 12px;
  border: 1px solid rgb(205 177 113 / 30%);
  border-radius: var(--ui-radius-md);
  background: var(--ui-gradient-panel);
}

.hotbar-settings__header {
  display: grid;
  gap: 4px;
}

.hotbar-settings__header small {
  color: var(--ui-color-gold);
  font-size: .52rem;
  font-weight: 800;
  letter-spacing: .12em;
}

.hotbar-settings__header h2 {
  margin: 0;
  font-family: var(--ui-font-display);
  font-size: 1rem;
}

.hotbar-settings__header p,
.hotbar-settings__footer > span,
.hotbar-settings__empty,
.hotbar-settings__hint {
  margin: 0;
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
  line-height: 1.45;
}

.hotbar-settings__slots {
  display: grid;
  gap: 6px;
  grid-template-columns: repeat(6, minmax(0, 1fr));
}

.hotbar-settings__active,
.hotbar-settings__reserve {
  display: grid;
  gap: 6px;
}

.hotbar-settings__section-label {
  color: var(--ui-color-gold-muted);
  font-size: .52rem;
  letter-spacing: .1em;
}

.hotbar-settings__active .hotbar-slot {
  grid-template-columns: 1fr;
  min-height: 52px;
  gap: 2px;
  padding: 3px 1px;
  place-items: center;
  text-align: center;
}

.hotbar-settings__active .hotbar-slot__number {
  width: 1.25rem;
  height: 1.25rem;
  font-size: .52rem;
}

.hotbar-settings__active .hotbar-slot__copy strong {
  font-size: .42rem;
}

.hotbar-settings__active .hotbar-slot__copy small,
.hotbar-settings__active .hotbar-slot__state {
  display: none;
}

.hotbar-slot--empty {
  opacity: .48;
}

.hotbar-settings__reserve-list {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 6px;
}

.hotbar-slot {
  display: grid;
  grid-template-columns: 2rem minmax(0, 1fr) auto;
  align-items: center;
  gap: 9px;
  min-height: var(--ui-touch-target);
  padding: 8px 9px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  background: rgb(5 8 13 / 82%);
  color: var(--ui-color-text-primary);
  font: inherit;
  text-align: left;
}

.hotbar-slot:active {
  transform: translateY(1px);
}

.hotbar-slot--selected {
  border-color: var(--ui-color-gold);
  background: rgb(205 177 113 / 12%);
  box-shadow: inset 0 0 0 1px rgb(205 177 113 / 18%);
}

.hotbar-slot--reserve {
  opacity: .72;
}

.hotbar-slot__number {
  display: grid;
  width: 2rem;
  height: 2rem;
  place-items: center;
  border: 1px solid rgb(205 177 113 / 38%);
  border-radius: var(--ui-radius-sm);
  color: var(--ui-color-gold);
  font: 700 .78rem var(--ui-font-display);
}

.hotbar-slot__copy {
  display: grid;
  min-width: 0;
  gap: 2px;
}

.hotbar-slot__copy strong {
  overflow: hidden;
  font-family: var(--ui-font-display);
  font-size: .72rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.hotbar-slot__copy small,
.hotbar-slot__state {
  color: var(--ui-color-text-muted);
  font-size: .52rem;
}

.hotbar-slot__state {
  color: var(--ui-color-gold-muted);
  font-weight: 800;
  text-transform: uppercase;
}

.hotbar-settings__hint {
  padding: 7px 8px;
  border-left: 2px solid var(--ui-color-gold);
  background: rgb(205 177 113 / 8%);
  color: var(--ui-color-text-primary);
}

.hotbar-settings__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding-top: 4px;
  border-top: 1px solid rgb(205 177 113 / 16%);
}

.hotbar-settings__footer :deep(.ui-button) {
  flex: none;
}

@media (max-width: 380px) {
  .hotbar-slot {
    grid-template-columns: 1.8rem minmax(0, 1fr);
  }

  .hotbar-slot__number {
    width: 1.8rem;
    height: 1.8rem;
  }

  .hotbar-slot__state {
    grid-column: 2;
  }

  .hotbar-settings__footer {
    align-items: stretch;
    flex-direction: column;
  }

  .hotbar-settings__reserve-list {
    grid-template-columns: 1fr;
  }
}
</style>
