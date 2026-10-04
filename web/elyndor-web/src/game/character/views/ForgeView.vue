<script setup lang="ts">
import { computed } from 'vue'
import MoneyAmount from '@/ui/components/MoneyAmount.vue'
import { UIButton, UILoadingState, UIModal } from '@/ui/components'
import UIConfirmation from '@/ui/components/UIConfirmation.vue'
import { useConfirmation } from '@/ui/composables/useConfirmation'
import ForgeItemCard from '../forge/ForgeItemCard.vue'
import ForgeReforgePanel from '../forge/ForgeReforgePanel.vue'
import ForgeEnhancementPanel from '../forge/ForgeEnhancementPanel.vue'
import { useForgeWorkbench } from '../forge/useForgeWorkbench'
import { canSalvage } from '../forge/forgeWorkbenchPresentation'

const emit = defineEmits<{ close: [] }>()
const work = useForgeWorkbench()
const {
  mode,
  source,
  category,
  selectedId,
  selectedIds,
  displayed,
  equipment,
  selected,
  loading,
  busy,
  error,
  notice,
  gold,
  stones,
  rewards,
  salvageReady,
} = work
const confirmation = useConfirmation()
const backpackCount = computed(
  () => equipment.value.filter((item) => item.equippedSlot === null).length,
)
const modes = [
  { id: 'reforge', label: 'Сменить характеристику', icon: '↻' },
  { id: 'upgrade', label: 'Усиление +1…+5', icon: '⚒' },
  { id: 'salvage', label: 'Разбор', icon: '♢' },
] as const
const categories = [
  { id: 'weapon', label: 'Оружие' },
  { id: 'armor', label: 'Броня' },
  { id: 'accessory', label: 'Аксессуары' },
  { id: 'artifact', label: 'Артефакты' },
] as const
async function confirmSalvage() {
  if (!salvageReady.value || busy.value) return
  const accepted = await confirmation.ask({
    title: 'Разобрать выбранные предметы?',
    message: `Предметов: ${selectedIds.value.length}. Они будут уничтожены безвозвратно. Надетые и защищённые вещи не затрагиваются.`,
    confirmLabel: `Разобрать ${selectedIds.value.length} предметов`,
  })
  if (accepted) await work.salvage()
}
</script>

<template>
  <UIModal :open="true" title="Кузница" fullscreen :busy="busy" @close="emit('close')">
    <template #header-extra
      ><div class="forge-wallet" aria-label="Ресурсы кузницы">
        <span><MoneyAmount :amount="gold" /></span
        ><span
          >Камни <b>{{ stones }}</b></span
        >
      </div></template
    >
    <section class="forge" data-forge-workshop :aria-busy="busy || loading">
      <nav class="forge-modes" aria-label="Режим кузницы">
        <button
          v-for="entry in modes"
          :key="entry.id"
          type="button"
          :data-forge-mode="entry.id"
          :aria-pressed="mode === entry.id"
          :disabled="busy || !!work.pending.value"
          @click="work.setMode(entry.id)"
        >
          <span aria-hidden="true">{{ entry.icon }}</span
          >{{ entry.label }}
        </button>
      </nav>
      <nav class="forge-source" aria-label="Расположение предметов">
        <button
          type="button"
          data-forge-filter="backpack"
          :aria-pressed="source === 'backpack'"
          :disabled="busy"
          @click="source = 'backpack'"
        >
          В рюкзаке ({{ backpackCount }})</button
        ><button
          type="button"
          data-forge-filter="equipped"
          :aria-pressed="source === 'equipped'"
          :disabled="busy"
          @click="source = 'equipped'"
        >
          Надето ({{ equipment.length - backpackCount }})
        </button>
      </nav>
      <nav class="forge-categories" aria-label="Тип предмета">
        <button
          v-for="entry in categories"
          :key="entry.id"
          type="button"
          :data-forge-category="entry.id"
          :aria-pressed="category === entry.id"
          :disabled="busy"
          @click="category = category === entry.id ? null : entry.id"
        >
          {{ entry.label }}
        </button>
      </nav>
      <p v-if="mode === 'salvage'" class="forge-hint">
        Надетые и защищённые предметы нельзя разобрать.
      </p>
      <p v-if="notice" class="forge-success" role="status">{{ notice }}</p>
      <div v-if="error" class="forge-error" role="alert">
        {{ error }}
        <UIButton variant="ghost" :disabled="busy" @click="work.retry">Повторить загрузку</UIButton>
      </div>
      <div class="forge-grid" aria-label="Предметы для кузницы">
        <ForgeItemCard
          v-for="item in displayed"
          :key="item.id"
          :item="item"
          :selected="mode === 'salvage' ? selectedIds.includes(item.id) : selectedId === item.id"
          :multiple="mode === 'salvage'"
          :disabled="busy || !!work.pending.value || (mode === 'salvage' && !canSalvage(item))"
          @select="work.select(item)"
        />
      </div>
      <UILoadingState
        v-if="!displayed.length"
        state="empty"
        title="Нет подходящих предметов"
        :message="
          category === 'artifact'
            ? 'Пространственные артефакты не перековываются, не усиливаются и не разбираются. Управляйте ими в инвентаре.'
            : 'Измените тип предмета или переключитесь между рюкзаком и надетыми вещами.'
        "
      />
      <section v-if="mode === 'salvage'" class="forge-work" data-forge-salvage>
        <header class="forge-batch-heading">
          <h3>Выбрано предметов: {{ selectedIds.length }}</h3>
          <div class="forge-batch-actions">
            <UIButton
              variant="ghost"
              :disabled="busy || !selectedIds.length"
              data-forge-clear
              @click="work.clearBatch"
              >Снять выбор</UIButton
            ><UIButton
              variant="ghost"
              :disabled="busy || !backpackCount"
              data-forge-select-all
              @click="work.selectAll"
              >Выбрать всё в рюкзаке</UIButton
            >
          </div>
        </header>
        <p class="forge-hint">
          Выбор сохраняется при смене фильтра. Разбираются только выбранные вещи из рюкзака.
        </p>
        <UILoadingState v-if="loading" state="loading" title="Рассчитываем материалы…" />
        <dl v-else-if="salvageReady" class="forge-rewards">
          <div v-for="[id, count] in rewards" :key="id">
            <dt>{{ work.materialLabel(id) }}</dt>
            <dd>+{{ count }}</dd>
          </div>
        </dl>
        <UIButton
          :disabled="!salvageReady || busy"
          :loading="busy"
          data-forge-salvage-submit
          @click="confirmSalvage"
          >Разобрать {{ selectedIds.length }} предметов</UIButton
        >
      </section>
      <section v-else-if="selected" class="forge-work" data-forge-detail>
        <ForgeItemCard :item="selected" selected :interactive="false" /><ForgeReforgePanel
          v-if="mode === 'reforge'"
          :work="work"
        /><ForgeEnhancementPanel v-else :work="work" />
      </section>
      <p v-else class="forge-hint forge-hint--choose">
        Выберите предмет — здесь появятся характеристики и стоимость действия.
      </p>
    </section>
  </UIModal>
  <UIConfirmation :request="confirmation.request.value" @resolve="confirmation.settle" />
</template>

<style scoped>
.forge {
  display: grid;
  gap: 14px;
  min-width: 0;
}
.forge-wallet {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  font-size: 0.8rem;
}
.forge-wallet > span {
  padding: 6px 9px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
}
.forge-modes,
.forge-categories,
.forge-source {
  display: grid;
  gap: 8px;
  min-width: 0;
}
.forge-modes {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}
.forge-source {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}
.forge-categories {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}
.forge-modes button,
.forge-source button,
.forge-categories button {
  min-width: 0;
  min-height: 44px;
  padding: 8px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  color: var(--ui-color-text-secondary);
  background: var(--ui-color-surface-1);
  font: inherit;
  font-size: 0.85rem;
  cursor: pointer;
  overflow-wrap: anywhere;
}
.forge-modes button {
  display: grid;
  gap: 6px;
  align-content: center;
  min-height: 84px;
  font-family: var(--ui-font-display);
}
.forge-modes button span {
  font-size: 1.5rem;
  color: var(--ui-color-gold-muted);
}
.forge button[aria-pressed='true'] {
  border-color: var(--ui-color-gold);
  color: var(--ui-color-gold);
  background: linear-gradient(145deg, rgb(232 200 102 / 14%), var(--ui-color-surface-1));
}
.forge button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
.forge button:focus-visible {
  outline: 2px solid var(--ui-color-focus);
  outline-offset: 2px;
}
.forge-grid {
  display: grid;
  gap: 10px;
  grid-template-columns: repeat(2, minmax(0, 1fr));
}
.forge-work {
  --ui-gradient-primary: linear-gradient(135deg, #f3d17c, #b98838);
  display: grid;
  gap: 14px;
  min-width: 0;
  padding: 16px;
  border: 1px solid var(--ui-color-gold-muted);
  border-radius: var(--ui-radius-md);
  background: linear-gradient(140deg, rgb(232 200 102 / 5%), var(--ui-color-surface-1));
}
.forge-work :deep(.ui-button--primary) {
  border-color: var(--ui-color-gold);
  box-shadow:
    var(--ui-shadow-inset),
    0 4px 14px rgb(185 136 56 / 15%);
}
.forge-batch-heading h3 {
  margin: 0 0 12px;
  font-family: var(--ui-font-display);
  font-size: 1.1rem;
}
.forge-batch-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}
.forge-hint {
  margin: 0;
  font-size: 0.8rem;
  line-height: 1.5;
  color: var(--ui-color-text-muted);
}
.forge-hint--choose {
  text-align: center;
  padding: 20px 0;
}
.forge-rewards {
  display: grid;
  gap: 8px;
  margin: 0;
}
.forge-rewards > div {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  padding: 10px;
  border-bottom: 1px solid var(--ui-color-border);
  font-size: 0.9rem;
}
.forge-rewards dd {
  margin: 0;
  color: var(--ui-color-gold);
  font-weight: 700;
}
.forge-error,
.forge-success {
  margin: 0;
  padding: 12px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  font-size: 0.85rem;
}
.forge-error {
  color: var(--ui-color-danger);
}
.forge-success {
  color: var(--ui-color-success);
}
@media (max-width: 680px) {
  .forge-grid {
    grid-template-columns: 1fr;
  }
  .forge-work {
    padding: 12px;
  }
}
@media (max-width: 400px) {
  .forge-categories button {
    padding: 6px 2px;
    font-size: 0.72rem;
  }
  .forge-modes button {
    padding: 6px 4px;
    font-size: 0.78rem;
  }
  .forge-wallet {
    font-size: 0.72rem;
    gap: 5px;
  }
}
</style>
