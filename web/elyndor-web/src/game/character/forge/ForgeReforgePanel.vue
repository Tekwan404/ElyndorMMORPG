<script setup lang="ts">
import MoneyAmount from '@/ui/components/MoneyAmount.vue'
import { UIButton, UILoadingState } from '@/ui/components'
import {
  forgeAffixQuality,
  forgeItemAvailability,
  forgePercent,
  forgeStatLabel,
  forgeStatRange,
  forgeStatValue,
} from './forgePresentation'
import type { useForgeWorkbench } from './useForgeWorkbench'

defineProps<{ work: ReturnType<typeof useForgeWorkbench> }>()
</script>

<template>
  <div class="reforge">
    <template v-if="work.pending.value && work.resultAffixes.value">
      <h3>Выберите, что оставить</h3>
      <p>Стоимость уже списана.</p>
      <div class="reforge-result" aria-live="polite">
        <div>
          <small>Текущая</small>
          <strong>{{ forgeStatLabel(work.resultAffixes.value.current?.statId ?? '') }}</strong>
          <b>{{
            work.resultAffixes.value.current
              ? forgeStatValue(
                  work.resultAffixes.value.current.statId,
                  work.resultAffixes.value.current.value,
                )
              : '—'
          }}</b>
          <small v-if="work.resultAffixes.value.current">
            {{ forgePercent(forgeAffixQuality(work.resultAffixes.value.current)) }}
            · T{{ work.resultAffixes.value.current.affixTier }}
          </small>
        </div>
        <div>
          <small>Новая</small>
          <strong>{{ forgeStatLabel(work.resultAffixes.value.proposed?.statId ?? '') }}</strong>
          <b>{{
            work.resultAffixes.value.proposed
              ? forgeStatValue(
                  work.resultAffixes.value.proposed.statId,
                  work.resultAffixes.value.proposed.value,
                )
              : '—'
          }}</b>
          <small v-if="work.resultAffixes.value.proposed">
            {{ forgePercent(forgeAffixQuality(work.resultAffixes.value.proposed)) }}
            · T{{ work.resultAffixes.value.proposed.affixTier }}
          </small>
        </div>
      </div>
      <div class="reforge-actions">
        <UIButton variant="ghost" :loading="work.busy.value" @click="work.decide(false)">
          Оставить текущую
        </UIButton>
        <UIButton :loading="work.busy.value" @click="work.decide(true)">
          Применить новую
        </UIButton>
      </div>
    </template>

    <template v-else-if="work.selected.value">
      <div v-if="work.selected.value.generatedItem" class="reforge-quality">
        <span>
          <small>Качество предмета</small>
          <b>{{ forgePercent(work.selected.value.generatedItem.rollQuality) }}</b>
        </span>
        <span>
          <small>Сила предмета</small>
          <b>
            {{ work.selected.value.generatedItem.itemPower.toFixed(2) }}
            /
            {{ work.selected.value.generatedItem.maxItemPower.toFixed(2) }}
          </b>
        </span>
      </div>

      <h3>Текущие характеристики</h3>
      <div class="reforge-affixes" aria-label="Характеристика для замены">
        <button
          v-for="affix in work.affixes.value"
          :key="affix.slotKey"
          type="button"
          :data-forge-affix="affix.slotKey"
          :aria-pressed="work.slotKey.value === affix.slotKey"
          :disabled="affix.isGuaranteed || work.busy.value"
          @click="work.selectAffix(affix.slotKey)"
        >
          <span>
            {{ forgeStatLabel(affix.statId) }}
            <small>
              {{ forgePercent(forgeAffixQuality(affix)) }} · T{{ affix.affixTier
              }}<span
                v-if="affix.isGuaranteed"
                class="reforge-lock"
                role="img"
                aria-label="Гарантированная характеристика"
              >
                · 🔒
              </span>
            </small>
          </span>
          <b>{{ forgeStatValue(affix.statId, affix.value) }}</b>
        </button>
      </div>

      <p v-if="!forgeItemAvailability(work.selected.value).available">
        {{ forgeItemAvailability(work.selected.value).reason }}
      </p>

      <UILoadingState v-if="work.loading.value" state="loading" title="Проверяем стоимость…" />

      <template v-else-if="work.preview.value">
        <details
          v-if="work.preview.value.possibleAffixes?.length"
          class="reforge-pool"
          data-forge-possible-affixes
        >
          <summary>
            <span>Может выпасть</span>
            <small>{{ work.preview.value.possibleAffixes.length }} вариантов</small>
          </summary>
          <div class="reforge-pool__list">
            <div
              v-for="candidate in work.preview.value.possibleAffixes"
              :key="candidate.statId"
              class="reforge-pool__row"
            >
              <span>{{ forgeStatLabel(candidate.statId) }}</span>
              <b>{{ forgeStatRange(candidate.statId, candidate.min, candidate.max) }}</b>
            </div>
          </div>
        </details>

        <p v-if="!work.canReforge.value" role="status">
          Не хватает монет или материалов для смены характеристики.
        </p>

        <UIButton
          :disabled="!work.canReforge.value || work.busy.value"
          :loading="work.busy.value"
          data-forge-roll
          @click="work.roll"
        >
          Перековать за&nbsp;<MoneyAmount :amount="work.preview.value.cost.gold" />
          <span>&nbsp;· {{ work.preview.value.cost.materialQuantity }} камней</span>
          <span
            v-if="
              work.preview.value.cost.catalystItemId && work.preview.value.cost.catalystQuantity
            "
          >
            &nbsp;· {{ work.preview.value.cost.catalystQuantity }}
            {{ work.materialLabel(work.preview.value.cost.catalystItemId) }}
          </span>
        </UIButton>
      </template>

      <p>
        Меняется только выбранное свойство. После перековки можно оставить текущий вариант.
        Звёзды и качество предмета сохраняются.
      </p>
    </template>
  </div>
</template>

<style scoped>
.reforge {
  display: grid;
  gap: 12px;
  min-width: 0;
}
.reforge h3 {
  margin: 0;
  font: 1.05rem var(--ui-font-display);
}
.reforge p {
  margin: 0;
  color: var(--ui-color-text-muted);
  font-size: 0.8rem;
  line-height: 1.5;
}
.reforge-quality {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
}
.reforge-quality > span {
  display: grid;
  gap: 4px;
  min-width: 0;
  padding: 10px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  background: var(--ui-color-surface-1);
}
.reforge-quality small {
  color: var(--ui-color-text-muted);
  font-size: 0.68rem;
}
.reforge-quality b {
  overflow-wrap: anywhere;
  color: var(--ui-color-gold);
  font-size: 0.9rem;
}
.reforge-affixes {
  display: grid;
  gap: 6px;
}
.reforge-affixes button {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  min-height: 48px;
  padding: 10px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  background: var(--ui-color-surface-1);
  color: var(--ui-color-text-primary);
  font: inherit;
  text-align: left;
}
.reforge-affixes button[aria-pressed='true'] {
  border-color: var(--ui-color-gold);
  background: rgb(232 200 102 / 9%);
}
.reforge-affixes small {
  display: block;
  margin-top: 2px;
  color: var(--ui-color-text-muted);
  font-size: 0.7rem;
}
.reforge-lock {
  color: var(--ui-color-gold-muted);
}
.reforge-affixes b {
  flex: 0 0 auto;
  color: var(--ui-color-gold);
}
.reforge-affixes button:disabled {
  opacity: 0.6;
}
.reforge-affixes button:focus-visible {
  outline: 2px solid var(--ui-color-focus);
}
.reforge-pool {
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  background: var(--ui-color-surface-1);
}
.reforge-pool summary {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 12px;
  color: var(--ui-color-text-primary);
  cursor: pointer;
  list-style: none;
}
.reforge-pool summary::-webkit-details-marker {
  display: none;
}
.reforge-pool summary::after {
  content: '⌄';
  color: var(--ui-color-gold);
}
.reforge-pool[open] summary::after {
  content: '⌃';
}
.reforge-pool summary small {
  margin-left: auto;
  color: var(--ui-color-text-muted);
}
.reforge-pool__list {
  display: grid;
  border-top: 1px solid var(--ui-color-border);
}
.reforge-pool__row {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  padding: 8px 12px;
  font-size: 0.78rem;
}
.reforge-pool__row + .reforge-pool__row {
  border-top: 1px solid rgb(255 255 255 / 4%);
}
.reforge-pool__row b {
  color: var(--ui-color-text-secondary);
  font-weight: 600;
}
.reforge-result,
.reforge-actions {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
}
.reforge-result > div {
  display: grid;
  gap: 6px;
  padding: 12px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  overflow-wrap: anywhere;
}
.reforge-result b {
  color: var(--ui-color-gold);
  font-size: 1.3rem;
}
.reforge-result small {
  color: var(--ui-color-text-muted);
}
@media (max-width: 380px) {
  .reforge-quality {
    grid-template-columns: 1fr;
  }
}
</style>
