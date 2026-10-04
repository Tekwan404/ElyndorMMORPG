<script setup lang="ts">
import MoneyAmount from '@/ui/components/MoneyAmount.vue'
import { UIButton, UILoadingState } from '@/ui/components'
import { forgeItemAvailability, forgeStatLabel } from './forgePresentation'
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
          <small>Текущая</small
          ><strong>{{ forgeStatLabel(work.resultAffixes.value.current?.statId ?? '') }}</strong
          ><b>{{ work.resultAffixes.value.current?.value }}</b>
        </div>
        <div>
          <small>Новая</small
          ><strong>{{ forgeStatLabel(work.resultAffixes.value.proposed?.statId ?? '') }}</strong
          ><b>{{ work.resultAffixes.value.proposed?.value }}</b>
        </div>
      </div>
      <div class="reforge-actions">
        <UIButton variant="ghost" :loading="work.busy.value" @click="work.decide(false)"
          >Оставить текущую</UIButton
        ><UIButton :loading="work.busy.value" @click="work.decide(true)">Применить новую</UIButton>
      </div>
    </template>
    <template v-else-if="work.selected.value">
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
          <span
            >{{ forgeStatLabel(affix.statId)
            }}<small v-if="affix.isGuaranteed">Базовая · не меняется</small></span
          ><b>{{ affix.value }}</b>
        </button>
      </div>
      <p v-if="!forgeItemAvailability(work.selected.value).available">
        {{ forgeItemAvailability(work.selected.value).reason }}
      </p>
      <UILoadingState v-if="work.loading.value" state="loading" title="Проверяем стоимость…" />
      <template v-else-if="work.preview.value">
        <div class="reforge-cost">
          <span>Стоимость</span><MoneyAmount :amount="work.preview.value.cost.gold" /><span
            >{{ work.materialLabel(work.preview.value.cost.materialItemId) }} ×{{
              work.preview.value.cost.materialQuantity
            }}</span
          ><span
            v-if="
              work.preview.value.cost.catalystItemId && work.preview.value.cost.catalystQuantity
            "
            >{{ work.materialLabel(work.preview.value.cost.catalystItemId) }} ×{{
              work.preview.value.cost.catalystQuantity
            }}</span
          >
        </div>
        <p v-if="!work.canReforge.value" role="status">
          Не хватает монет или материалов для смены характеристики.
        </p>
        <UIButton
          :disabled="!work.canReforge.value || work.busy.value"
          :loading="work.busy.value"
          data-forge-roll
          @click="work.roll"
          >Сменить характеристику</UIButton
        >
      </template>
      <p>
        Меняется только выбранное свойство. После перековки можно оставить текущий вариант. Звёзды и
        качество сохраняются.
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
.reforge-affixes {
  display: grid;
  gap: 6px;
}
.reforge-affixes button {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  min-height: 44px;
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
  color: var(--ui-color-text-muted);
  font-size: 0.7rem;
}
.reforge-affixes button:disabled {
  opacity: 0.6;
}
.reforge-affixes button:focus-visible {
  outline: 2px solid var(--ui-color-focus);
}
.reforge-cost {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
  padding: 12px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  font-size: 0.85rem;
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
</style>
