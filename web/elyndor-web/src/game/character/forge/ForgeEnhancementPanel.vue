<script setup lang="ts">
import MoneyAmount from '@/ui/components/MoneyAmount.vue'
import { UIButton, UILoadingState } from '@/ui/components'
import type { useForgeWorkbench } from './useForgeWorkbench'
defineProps<{ work: ReturnType<typeof useForgeWorkbench> }>()
</script>
<template>
  <div class="enhancement">
    <UILoadingState v-if="work.loading.value" state="loading" title="Проверяем усиление…" />
    <template v-else-if="work.enhancement.value">
      <h3>Текущее усиление +{{ work.enhancement.value.currentEnhancementLevel }}</h3>
      <ol class="enhancement-levels" aria-label="Уровни усиления">
        <li
          v-for="level in 5"
          :key="level"
          :class="{
            current: level === work.enhancement.value.currentEnhancementLevel,
            next: level === work.enhancement.value.targetEnhancementLevel,
          }"
        >
          +{{ level }}
        </li>
      </ol>
      <p class="enhancement-power">
        Базовая мощь {{ Math.round(work.enhancement.value.intrinsicItemPower ?? 0) }}
        <span aria-hidden="true">→</span> после усиления
        <b>{{ Math.round(work.enhancement.value.finalItemPower ?? 0) }}</b>
      </p>
      <p>Звёзды и качество предмета не меняются</p>
      <template v-if="!work.enhancement.value.isMaximumEnhancement">
        <dl class="enhancement-costs">
          <div>
            <dt>Монеты</dt>
            <dd><MoneyAmount :amount="work.enhancement.value.cost.gold" /></dd>
          </div>
          <div>
            <dt>{{ work.materialLabel(work.enhancement.value.cost.enhancementMaterialItemId) }}</dt>
            <dd>
              {{ work.available(work.enhancement.value.cost.enhancementMaterialItemId) }} /
              {{ work.enhancement.value.cost.enhancementMaterialQuantity }}
            </dd>
          </div>
          <div
            v-if="
              work.enhancement.value.cost.catalystItemId &&
              work.enhancement.value.cost.catalystQuantity
            "
          >
            <dt>{{ work.materialLabel(work.enhancement.value.cost.catalystItemId) }}</dt>
            <dd>
              {{ work.available(work.enhancement.value.cost.catalystItemId) }} /
              {{ work.enhancement.value.cost.catalystQuantity }}
            </dd>
          </div>
        </dl>
        <p v-if="!work.canEnhance.value" role="status">
          Не хватает монет или материалов для усиления.
        </p>
        <UIButton
          :disabled="!work.canEnhance.value || work.busy.value"
          :loading="work.busy.value"
          data-forge-enhancement
          @click="work.enhance"
          >Усилить до +{{ work.enhancement.value.targetEnhancementLevel }}</UIButton
        >
      </template>
      <p v-else>Достигнуто максимальное усиление +5.</p>
    </template>
    <p v-else>
      Усиление недоступно: предмет защищён, занят другим действием или не поддерживает усиление.
    </p>
  </div>
</template>
<style scoped>
.enhancement {
  display: grid;
  gap: 12px;
  min-width: 0;
}
.enhancement h3 {
  margin: 0;
  font: 1.05rem var(--ui-font-display);
}
.enhancement p {
  margin: 0;
  font-size: 0.85rem;
  line-height: 1.5;
  color: var(--ui-color-text-secondary);
}
.enhancement-levels {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 8px;
  padding: 0;
  margin: 0;
  list-style: none;
}
.enhancement-levels li {
  padding: 10px 0;
  text-align: center;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  color: var(--ui-color-text-muted);
}
.enhancement-levels .current {
  border-color: var(--ui-color-gold);
  color: var(--ui-color-gold);
  background: rgb(232 200 102 / 12%);
}
.enhancement-levels .next {
  border-style: dashed;
  color: var(--ui-color-text-primary);
}
.enhancement-power {
  padding: 12px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
}
.enhancement-power b {
  color: var(--ui-color-success);
}
.enhancement-costs {
  display: grid;
  gap: 8px;
  margin: 0;
}
.enhancement-costs > div {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 0;
  border-bottom: 1px solid var(--ui-color-border);
  font-size: 0.85rem;
}
.enhancement-costs dd {
  margin: 0;
  font-weight: 700;
}
</style>
