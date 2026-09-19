<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import type { KnownAbility } from '@/api/contracts'
import { abilityArtUrl } from '@/assets/abilityArt'
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
  if (ability.resourceCost > 0) parts.push(`Стоимость: ${ability.resourceCost}`)
  if (ability.cooldownSeconds > 0) parts.push(`Перезарядка: ${ability.cooldownSeconds} с`)
  return parts.join(' · ') || 'Без затрат и перезарядки'
}

function abilityIcon(ability: KnownAbility): string | undefined {
  return abilityArtUrl(ability.iconId)
}
</script>

<template>
  <section class="hotbar-settings" data-combat-hotbar-settings>
    <header class="hotbar-settings__header">
      <small>ПАНЕЛЬ БОЯ</small>
      <h2>Порядок способностей</h2>
      <p>Выберите две способности, чтобы поменять их местами. В бою отображаются первые 12.</p>
    </header>

    <section v-if="orderedAbilities.length" class="hotbar-settings__active" aria-label="Активная панель">
      <strong class="hotbar-settings__section-label">В БОЮ · 12 ЯЧЕЕК</strong>
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
          <span class="hotbar-slot__icon" :class="{ 'hotbar-slot__icon--empty': !ability }">
            <img
              v-if="ability && abilityIcon(ability)"
              :src="abilityIcon(ability)"
              :alt="ability.displayName"
              data-ability-icon
              loading="lazy"
              decoding="async"
            />
            <span v-else-if="ability" class="hotbar-slot__icon-fallback" aria-hidden="true">
              {{ ability.displayName.slice(0, 1).toUpperCase() }}
            </span>
            <span v-else class="hotbar-slot__icon-fallback" aria-hidden="true">+</span>
            <span class="hotbar-slot__number">{{ index + 1 }}</span>
          </span>
          <span class="hotbar-slot__copy">
            <strong>{{ ability?.displayName ?? 'Пустая ячейка' }}</strong>
            <small>{{ ability ? abilityMeta(ability) : 'Свободно' }}</small>
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
          <span class="hotbar-slot__icon">
            <img
              v-if="abilityIcon(ability)"
              :src="abilityIcon(ability)"
              :alt="ability.displayName"
              data-ability-icon
              loading="lazy"
              decoding="async"
            />
            <span v-else class="hotbar-slot__icon-fallback" aria-hidden="true">
              {{ ability.displayName.slice(0, 1).toUpperCase() }}
            </span>
            <span class="hotbar-slot__number">Р</span>
          </span>
          <span class="hotbar-slot__copy"><strong>{{ ability.displayName }}</strong><small>{{ abilityMeta(ability) }}</small></span>
          <span class="hotbar-slot__state">{{ selectedAbilityId === ability.id ? 'Выбрано' : 'Резерв' }}</span>
        </button>
      </div>
    </section>

    <div v-if="!orderedAbilities.length" class="hotbar-settings__empty">У героя пока нет боевых способностей.</div>

    <div v-if="selectedAbilityId" class="hotbar-settings__hint" role="status">
      Теперь выберите вторую способность.
    </div>

    <footer class="hotbar-settings__footer">
      <span>Порядок сохраняется для этого героя на устройстве.</span>
      <UIButton variant="ghost" @click="resetOrder">Сбросить</UIButton>
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
  min-height: 72px;
  gap: 3px;
  padding: 4px 2px;
  place-items: center;
  text-align: center;
}

.hotbar-settings__active .hotbar-slot__icon {
  width: min(44px, 100%);
  aspect-ratio: 1;
}

.hotbar-settings__active .hotbar-slot__copy strong {
  display: block;
  max-width: 100%;
  font-size: .46rem;
}

.hotbar-settings__active .hotbar-slot__copy small,
.hotbar-settings__active .hotbar-slot__state {
  display: none;
}

.hotbar-slot--empty {
  opacity: .42;
}

.hotbar-settings__reserve-list {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 6px;
}

.hotbar-slot {
  display: grid;
  grid-template-columns: 2.6rem minmax(0, 1fr) auto;
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
  box-shadow: 0 0 12px rgb(205 177 113 / 14%), inset 0 0 0 1px rgb(205 177 113 / 18%);
}

.hotbar-slot--reserve {
  opacity: .78;
}

.hotbar-slot__icon {
  position: relative;
  display: grid;
  width: 2.6rem;
  aspect-ratio: 1;
  place-items: center;
  overflow: hidden;
  border: 1px solid rgb(205 177 113 / 46%);
  border-radius: 5px;
  background: radial-gradient(circle at 50% 35%, rgb(146 136 255 / 16%), rgb(3 5 9 / 98%));
  box-shadow:
    inset 0 0 0 1px rgb(255 255 255 / 7%),
    inset 0 0 12px rgb(0 0 0 / 58%);
}

.hotbar-slot__icon img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.hotbar-slot__icon-fallback {
  color: var(--ui-color-gold-muted);
  font: 700 .8rem var(--ui-font-display);
}

.hotbar-slot__number {
  position: absolute;
  right: 2px;
  bottom: 2px;
  display: grid;
  min-width: 1rem;
  height: 1rem;
  padding: 0 3px;
  place-items: center;
  border: 1px solid rgb(205 177 113 / 42%);
  border-radius: 3px;
  background: rgb(2 4 8 / 92%);
  box-shadow: 0 1px 3px rgb(0 0 0 / 72%);
  color: #f3dfad;
  font: 800 .5rem var(--ui-font-display);
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
    grid-template-columns: 2.35rem minmax(0, 1fr);
  }

  .hotbar-slot__icon {
    width: 2.35rem;
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
