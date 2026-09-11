<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import type { InventoryItem, ItemAffix, ItemReforgePreview, ItemReforgeResponse, ItemSalvagePreview } from '@/api/contracts'
import { itemArtUrl } from '@/assets/itemArt'
import { forgeableAffixes, forgeItemAvailability, forgeStatLabel } from '@/game/character/forge/forgePresentation'
import { useGameSessionStore } from '@/stores/gameSession'
import { ItemQualityStars, UIButton, UILoadingState } from '@/ui/components'
import IconGenerator from '@/ui/icons/IconGenerator.vue'

const session = useGameSessionStore()
const selectedItemId = ref<string | null>(null)
const selectedSlotKey = ref<string | null>(null)
const preview = ref<ItemReforgePreview | null>(null)
const pending = ref<ItemReforgeResponse | null>(null)
const actionError = ref<string | null>(null)
const loadingPreview = ref(false)
const salvagePreview = ref<ItemSalvagePreview | null>(null)

const character = computed(() => session.snapshot?.character ?? null)
const allEquipment = computed(() => character.value?.inventory.items.filter(item => item.type === 'Equipment') ?? [])
const selectedItem = computed(() => allEquipment.value.find(item => item.id === selectedItemId.value) ?? null)
const selectedAvailability = computed(() => selectedItem.value ? forgeItemAvailability(selectedItem.value) : null)
const affixes = computed(() => selectedItem.value ? forgeableAffixes(selectedItem.value) : [])
const selectedAffix = computed<ItemAffix | null>(() =>
  affixes.value.find(affix => affix.slotKey === selectedSlotKey.value) ?? null,
)
const reforgeStones = computed(() => character.value?.inventory.items
  .filter(item => item.definitionId === 'REFORGE_STONE')
  .reduce((total, item) => total + item.quantity, 0) ?? 0)

function itemArt(item: InventoryItem): string | undefined {
  return itemArtUrl(item.iconId)
}

function selectItem(item: InventoryItem): void {
  selectedItemId.value = item.id
  selectedSlotKey.value = item.reforgeSlotKey ?? forgeableAffixes(item)[0]?.slotKey ?? null
  preview.value = null
  pending.value = null
  actionError.value = null
  salvagePreview.value = null
}

function clearSelection(): void { selectedItemId.value = null; selectedSlotKey.value = null; salvagePreview.value = null }

async function refreshSelection(): Promise<void> {
  const item = selectedItem.value
  const slotKey = selectedSlotKey.value
  preview.value = null
  pending.value = null
  actionError.value = null
  if (!item || !slotKey || !selectedAvailability.value?.available) return

  loadingPreview.value = true
  try {
    pending.value = await session.getPendingReforge(item.id)
    if (!pending.value) {
      preview.value = await session.getReforgePreview(item.id, slotKey)
      if (!preview.value) actionError.value = reforgeErrorMessage(session.errorCode)
    }
  } finally {
    loadingPreview.value = false
  }
}

watch([selectedItemId, selectedSlotKey], refreshSelection)

async function roll(): Promise<void> {
  const item = selectedItem.value
  const slotKey = selectedSlotKey.value
  if (!item || !slotKey || !preview.value || session.mutationPending) return
  actionError.value = null
  pending.value = await session.rollReforge(item.id, slotKey)
  if (!pending.value) actionError.value = reforgeErrorMessage(session.errorCode)
  preview.value = null
}

async function decide(acceptProposed: boolean): Promise<void> {
  if (!pending.value || session.mutationPending) return
  actionError.value = null
  const resolved = await session.decideReforge(pending.value.operationId, acceptProposed)
  if (!resolved) {
    actionError.value = reforgeErrorMessage(session.errorCode)
    return
  }
  pending.value = null
  await refreshSelection()
}

async function upgradeStars(): Promise<void> {
  const item = selectedItem.value
  if (!item || session.mutationPending) return
  actionError.value = null
  const result = await session.upgradeItemStars(item.id)
  if (!result) actionError.value = starUpgradeErrorMessage(session.errorCode)
}

async function prepareSalvage(): Promise<void> {
  if (!selectedItem.value || session.mutationPending) return
  actionError.value = null
  salvagePreview.value = await session.getSalvagePreview(selectedItem.value.id)
  if (!salvagePreview.value) actionError.value = 'Снимите предмет с экипировки перед разбором.'
}

async function confirmSalvage(): Promise<void> {
  if (!selectedItem.value || !salvagePreview.value || session.mutationPending) return
  const result = await session.salvageItem(selectedItem.value.id, salvagePreview.value.requiresConfirmation)
  if (result) clearSelection()
  else actionError.value = 'Не удалось разобрать предмет.'
}

function starUpgradeErrorMessage(code: string | null): string {
  const messages: Record<string, string> = {
    star_upgrade_max_stars: 'Предмет уже достиг ★★★★★.',
    star_upgrade_not_enough_gold: 'Недостаточно золота для улучшения.',
    star_upgrade_not_enough_stones: 'Недостаточно Камней перековки.',
    star_upgrade_missing_catalyst: 'Для ★★★★★ требуется Ядро подземелья.',
    star_upgrade_item_locked: 'Предмет защищён. Снимите блокировку в инвентаре.',
  }
  return code ? (messages[code] ?? 'Не удалось улучшить качество предмета.') : 'Не удалось улучшить качество предмета.'
}

function reforgeErrorMessage(code: string | null): string {
  const messages: Record<string, string> = {
    reforge_item_locked: 'Предмет защищён. Снимите блокировку в инвентаре.',
    reforge_item_equipped: 'Снимите предмет с экипировки перед перековкой.',
    reforge_item_transaction_locked: 'С этим предметом уже выполняется операция.',
    reforge_not_enough_gold: 'Недостаточно золота.',
    reforge_not_enough_material: 'Недостаточно Камней перековки.',
    character_operation_in_combat: 'Кузница недоступна во время боя.',
  }
  return code ? (messages[code] ?? 'Не удалось выполнить перековку. Попробуйте ещё раз.') : 'Не удалось выполнить перековку.'
}

function format(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(2).replace(/\.00$/, '')
}

function rarityLabel(item: InventoryItem): string {
  const labels: Record<InventoryItem['rarity'], string> = {
    Common: 'Обычный', Uncommon: 'Необычный', Rare: 'Редкий', Epic: 'Эпический', Legendary: 'Легендарный', Unique: 'Уникальный',
  }
  return labels[item.rarity]
}

function affixValue(item: ItemReforgeResponse['current'], slotKey: string): number {
  return item.affixes.find(affix => affix.slotKey === slotKey)?.value ?? 0
}
</script>

<template>
  <section class="forge-view">
    <header class="forge-header">
      <div>
        <p>МАСТЕРСКАЯ</p>
        <h1>Кузница</h1>
        <span>Перековывайте случайную характеристику предмета.</span>
      </div>
      <div class="forge-resource" aria-label="Камни перековки">
        <IconGenerator :config="{ id: 'forge-stone', glyph: 'ore', category: 'resource' }" />
        <strong>{{ reforgeStones }}</strong>
        <small>камней</small>
      </div>
    </header>

    <section v-if="allEquipment.length" class="forge-layout">
      <div v-if="!selectedItem" class="forge-items" aria-label="Предметы для кузницы">
        <header><small>СНАРЯЖЕНИЕ</small><strong>Выберите предмет</strong></header>
        <button
          v-for="item in allEquipment"
          :key="item.id"
          class="forge-item"
          :class="{ active: item.id === selectedItemId, unavailable: !forgeItemAvailability(item).available }"
          :data-forge-item="item.id"
          type="button"
          @click="selectItem(item)"
        >
          <span class="forge-item__art" :data-rarity="item.rarity">
            <img v-if="itemArt(item)" :src="itemArt(item)" :alt="item.name" loading="lazy" />
            <IconGenerator v-else :config="{ id: `forge-item-${item.id}`, glyph: 'sword', category: 'weapon' }" />
          </span>
          <span class="forge-item__copy">
            <strong>{{ item.name }}</strong>
            <small>{{ rarityLabel(item) }} · Мощь {{ format(item.generatedItem?.itemPower ?? 0) }}</small>
            <ItemQualityStars v-if="item.generatedItem" :id="`forge-${item.id}`" :stars="item.generatedItem.stars" />
          </span>
        </button>
      </div>

      <UILoadingState
        v-if="!selectedItem"
        class="forge-empty"
        state="empty"
        title="Выберите предмет"
        message="Кузница работает только со снятым случайно сгенерированным снаряжением."
      />
      <article v-else class="forge-detail" :data-rarity="selectedItem.rarity">
        <UIButton variant="ghost" class="forge-back" @click="clearSelection">← К предметам</UIButton>
        <header class="forge-detail__identity">
          <span class="forge-detail__art">
            <img v-if="itemArt(selectedItem)" :src="itemArt(selectedItem)" :alt="selectedItem.name" />
            <IconGenerator v-else :config="{ id: `forge-detail-${selectedItem.id}`, glyph: 'sword', category: 'weapon' }" />
          </span>
          <div>
            <small>{{ rarityLabel(selectedItem) }} · Ур. {{ selectedItem.requiredLevel }}</small>
            <h2>{{ selectedItem.name }}</h2>
            <ItemQualityStars v-if="selectedItem.generatedItem" :id="`forge-detail-stars-${selectedItem.id}`" :stars="selectedItem.generatedItem.stars" />
            <strong>Мощь предмета {{ format(selectedItem.generatedItem?.itemPower ?? 0) }} / {{ format(selectedItem.generatedItem?.maxItemPower ?? 0) }}</strong>
          </div>
        </header>

        <p v-if="!selectedAvailability?.available" class="forge-notice" role="alert">{{ selectedAvailability?.reason }}</p>
        <template v-else>
          <section class="forge-affixes" aria-label="Характеристики предмета">
            <header><small>ХАРАКТЕРИСТИКИ</small><strong>Выберите одну для перековки</strong></header>
            <button
              v-for="affix in affixes"
              :key="affix.slotKey"
              type="button"
              :data-forge-affix="affix.slotKey"
              :class="{ active: selectedSlotKey === affix.slotKey }"
              @click="selectedSlotKey = affix.slotKey"
            >
              <span>{{ forgeStatLabel(affix.statId) }}</span>
              <strong>{{ format(affix.value) }}</strong>
              <small>Диапазон: {{ format(affix.min) }}–{{ format(affix.max) }}</small>
            </button>
          </section>

          <section v-if="pending" class="forge-result" aria-live="polite">
            <small>РЕЗУЛЬТАТ ПЕРЕКОВКИ</small>
            <strong>{{ forgeStatLabel(selectedAffix?.statId ?? pending.slotKey) }}: {{ format(affixValue(pending.current, pending.slotKey)) }} → {{ format(affixValue(pending.proposed, pending.slotKey)) }}</strong>
            <p>Стоимость уже списана. Оставьте текущую характеристику или примените результат.</p>
            <div class="forge-actions">
              <UIButton variant="ghost" :loading="session.mutationPending" @click="decide(false)">Оставить текущую</UIButton>
              <UIButton :loading="session.mutationPending" @click="decide(true)">Применить результат</UIButton>
            </div>
          </section>
          <section v-else class="forge-preview" aria-live="polite">
            <template v-if="loadingPreview"><span>Проверяем кузницу…</span></template>
            <template v-else-if="preview">
              <small>ПЕРЕД ПЕРЕКОВКОЙ</small>
              <p>Изменится только выбранная характеристика. Редкость, звёзды, мощь и остальные характеристики сохранятся.</p>
              <dl>
                <div><dt>Золото</dt><dd>{{ preview.cost.gold }}</dd></div>
                <div><dt>Камни перековки</dt><dd>{{ preview.cost.materialQuantity }}</dd></div>
              </dl>
              <UIButton :loading="session.mutationPending" data-forge-roll @click="roll">Перековать характеристику</UIButton>
            </template>
          </section>
          <section v-if="selectedItem.generatedItem && selectedItem.generatedItem.stars < 5" class="forge-star-upgrade">
            <small>КАЧЕСТВО ПРЕДМЕТА</small>
            <p>Улучшение повысит качество на одну звезду и усилит rolled характеристики. Для ★★★★★ потребуется Ядро подземелья.</p>
            <UIButton :loading="session.mutationPending" data-forge-star-upgrade @click="upgradeStars">Улучшить до ★{{ selectedItem.generatedItem.stars + 1 }}</UIButton>
          </section>
          <section class="forge-salvage">
            <template v-if="!salvagePreview"><UIButton variant="danger" :loading="session.mutationPending" @click="prepareSalvage">Разобрать предмет</UIButton></template>
            <template v-else><p>Предмет будет удалён. Камень перековки: {{ salvagePreview.reward.reforgeStoneQuantity }}.</p><UIButton variant="danger" :loading="session.mutationPending" @click="confirmSalvage">Подтвердить разбор</UIButton></template>
          </section>
        </template>
        <p v-if="actionError" class="forge-error" role="alert">{{ actionError }}</p>
      </article>
    </section>
    <UILoadingState
      v-else
      state="empty"
      title="Нет снаряжения"
      message="Найдите случайно сгенерированное снаряжение и снимите его перед перековкой."
    />
  </section>
</template>

<style scoped>
.forge-view { display: grid; gap: var(--ui-space-4); padding: var(--ui-space-4); }
.forge-header, .forge-detail, .forge-items { border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-lg); background: linear-gradient(145deg, rgb(23 32 51 / 94%), rgb(10 15 27 / 94%)); box-shadow: var(--ui-shadow-panel); }
.forge-header { display: flex; align-items: center; justify-content: space-between; gap: var(--ui-space-3); padding: var(--ui-space-4); }
.forge-header p, .forge-header h1, .forge-header span, .forge-detail h2 { margin: 0; }
.forge-header p, .forge-items small, .forge-affixes header small, .forge-preview small, .forge-result small { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); letter-spacing: .08em; }
.forge-header h1 { margin-top: 2px; font-size: var(--ui-font-size-xl); }
.forge-header span { display: block; margin-top: 4px; color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-sm); }
.forge-resource { display: grid; grid-template-columns: auto auto; align-items: center; column-gap: 4px; min-width: 72px; color: var(--ui-color-gold); }
.forge-resource :deep(.icon-generator) { width: 28px; height: 28px; grid-row: span 2; }
.forge-resource small { color: var(--ui-color-text-muted); }
.forge-layout { display: grid; gap: var(--ui-space-4); }
.forge-items { padding: var(--ui-space-3); }
.forge-items > header { display: grid; gap: 2px; margin-bottom: var(--ui-space-2); }
.forge-item { display: flex; width: 100%; min-height: 64px; align-items: center; gap: var(--ui-space-3); padding: var(--ui-space-2); border: 1px solid transparent; border-radius: var(--ui-radius-md); background: transparent; color: var(--ui-color-text-primary); font: inherit; text-align: left; cursor: pointer; }
.forge-item.active { border-color: var(--ui-color-primary); background: rgb(132 121 250 / 10%); }
.forge-item.unavailable { opacity: .58; }
.forge-item__art, .forge-detail__art { display: grid; width: 48px; height: 48px; flex: 0 0 48px; place-items: center; overflow: hidden; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: rgb(0 0 0 / 22%); }
.forge-item__art img, .forge-detail__art img { width: 100%; height: 100%; object-fit: cover; }
.forge-item__copy { display: grid; min-width: 0; gap: 2px; }
.forge-item__copy strong { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.forge-item__copy small { letter-spacing: 0; }
.forge-detail { padding: var(--ui-space-4); }
.forge-detail__identity { display: flex; gap: var(--ui-space-3); }
.forge-detail__identity > div { display: grid; align-content: start; gap: 4px; }
.forge-detail__identity small, .forge-detail__identity > div > strong { color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-sm); }
.forge-notice, .forge-error { margin: var(--ui-space-4) 0 0; color: var(--ui-color-danger); }
.forge-affixes { display: grid; gap: var(--ui-space-2); margin-top: var(--ui-space-4); }
.forge-affixes header { display: grid; gap: 2px; }
.forge-affixes button { display: grid; grid-template-columns: 1fr auto; align-items: center; min-height: var(--ui-touch-target); padding: var(--ui-space-2) var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: rgb(0 0 0 / 15%); color: var(--ui-color-text-primary); font: inherit; text-align: left; }
.forge-affixes button.active { border-color: var(--ui-color-primary); background: rgb(132 121 250 / 11%); }
.forge-affixes button small { grid-column: 1 / -1; color: var(--ui-color-text-muted); }
.forge-preview, .forge-result { display: grid; gap: var(--ui-space-3); margin-top: var(--ui-space-4); padding: var(--ui-space-3); border: 1px solid rgb(146 136 255 / 28%); border-radius: var(--ui-radius-md); background: rgb(104 91 218 / 7%); }
.forge-star-upgrade { display: grid; gap: var(--ui-space-3); margin-top: var(--ui-space-4); padding: var(--ui-space-3); border: 1px solid rgb(222 184 92 / 30%); border-radius: var(--ui-radius-md); background: rgb(168 117 32 / 8%); }
.forge-star-upgrade small { color: var(--ui-color-gold); font-size: var(--ui-font-size-xs); letter-spacing: .08em; }
.forge-star-upgrade p { margin: 0; color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-sm); }
.forge-back { margin-bottom: var(--ui-space-3); }
.forge-salvage { display: grid; gap: var(--ui-space-2); margin-top: var(--ui-space-4); }
.forge-salvage p { margin: 0; color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-sm); }
.forge-preview p, .forge-result p { margin: 0; color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-sm); }
.forge-preview dl { display: grid; grid-template-columns: 1fr 1fr; gap: var(--ui-space-2); margin: 0; }
.forge-preview dl div { display: grid; padding: var(--ui-space-2); border-radius: var(--ui-radius-sm); background: rgb(0 0 0 / 16%); }
.forge-preview dt { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.forge-preview dd { margin: 2px 0 0; font-weight: var(--ui-font-weight-semibold); }
.forge-actions { display: grid; grid-template-columns: 1fr 1fr; gap: var(--ui-space-2); }
.forge-empty { min-height: 220px; }
@media (min-width: 720px) { .forge-layout { grid-template-columns: minmax(210px, .7fr) minmax(0, 1.3fr); align-items: start; } .forge-items { position: sticky; top: 62px; } }
</style>
