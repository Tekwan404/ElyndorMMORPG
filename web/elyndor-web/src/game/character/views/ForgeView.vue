<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import type { InventoryItem, ItemReforgePreview, ItemReforgeResponse, ItemSalvagePreview } from '@/api/contracts'
import { itemArtUrl } from '@/assets/itemArt'
import { availableForgeMaterialQuantity, forgeableAffixes, forgeItemAvailability, forgeStatLabel, reforgeResultAffixes, shouldRestorePendingReforge } from '@/game/character/forge/forgePresentation'
import { useGameSessionStore } from '@/stores/gameSession'
import { ItemQualityStars, UIButton, UILoadingState } from '@/ui/components'
import IconGenerator from '@/ui/icons/IconGenerator.vue'

const session = useGameSessionStore()
type ForgeMode = 'reforge' | 'upgrade' | 'salvage'

const selectedItemId = ref<string | null>(null)
const selectedSlotKey = ref<string | null>(null)
const activeMode = ref<ForgeMode>('reforge')
const preview = ref<ItemReforgePreview | null>(null)
const pending = ref<ItemReforgeResponse | null>(null)
const actionError = ref<string | null>(null)
const loadingPreview = ref(false)
const salvagePreview = ref<ItemSalvagePreview | null>(null)

const character = computed(() => session.snapshot?.character ?? null)
const gold = computed(() => character.value?.gold ?? 0)
const allEquipment = computed(() => character.value?.inventory.items.filter(item => item.type === 'Equipment') ?? [])
const sortedEquipment = computed(() => [...allEquipment.value].sort((left, right) => {
  const leftAvailable = forgeItemAvailability(left).available ? 1 : 0
  const rightAvailable = forgeItemAvailability(right).available ? 1 : 0
  if (leftAvailable !== rightAvailable) return rightAvailable - leftAvailable
  return (right.generatedItem?.itemPower ?? 0) - (left.generatedItem?.itemPower ?? 0)
}))
const availableEquipmentCount = computed(() => allEquipment.value.filter(item => forgeItemAvailability(item).available).length)
const selectedItem = computed(() => allEquipment.value.find(item => item.id === selectedItemId.value) ?? null)
const selectedAvailability = computed(() => selectedItem.value ? forgeItemAvailability(selectedItem.value) : null)
const affixes = computed(() => selectedItem.value ? forgeableAffixes(selectedItem.value) : [])
const selectedAffix = computed(() => affixes.value.find(affix => affix.slotKey === selectedSlotKey.value) ?? null)
const pendingAffixes = computed(() => pending.value
  ? reforgeResultAffixes(pending.value.current, pending.value.proposed, pending.value.slotKey)
  : { current: null, proposed: null })
const totalReforgeStones = computed(() => character.value?.inventory.items
  .filter(item => item.definitionId === 'REFORGE_STONE')
  .reduce((total, item) => total + item.quantity, 0) ?? 0)
const reforgeStones = computed(() => availableForgeMaterialQuantity(
  character.value?.inventory.items ?? [],
  'REFORGE_STONE',
))
const selectedPowerPercent = computed(() => {
  const generated = selectedItem.value?.generatedItem
  if (!generated || generated.maxItemPower <= 0) return 0
  return Math.max(0, Math.min(100, generated.itemPower / generated.maxItemPower * 100))
})
const canAffordReforge = computed(() => {
  const cost = preview.value?.cost
  if (!cost) return false
  if (gold.value < cost.gold || reforgeStones.value < cost.materialQuantity) return false
  if (cost.catalystQuantity > 0 && cost.catalystItemId) {
    return availableForgeMaterialQuantity(
      character.value?.inventory.items ?? [],
      cost.catalystItemId,
    ) >= cost.catalystQuantity
  }
  return true
})
const reforgeShortage = computed(() => {
  const cost = preview.value?.cost
  if (!cost) return null
  if (gold.value < cost.gold) return `Не хватает золота: нужно ${cost.gold}, у вас ${gold.value}.`
  if (reforgeStones.value < cost.materialQuantity) return `Не хватает Камней перековки: нужно ${cost.materialQuantity}, доступно ${reforgeStones.value}.`
  if (cost.catalystQuantity > 0 && cost.catalystItemId) {
    const available = availableForgeMaterialQuantity(character.value?.inventory.items ?? [], cost.catalystItemId)
    if (available < cost.catalystQuantity) return `Не хватает катализатора: нужно ${cost.catalystQuantity}, доступно ${available}.`
  }
  return null
})
const resultComparison = computed(() => {
  const current = pendingAffixes.value.current
  const proposed = pendingAffixes.value.proposed
  if (!current || !proposed) return { label: '', tone: 'neutral' }
  if (current.statId !== proposed.statId) return { label: 'Новая характеристика', tone: 'neutral' }
  const delta = proposed.value - current.value
  if (delta > 0) return { label: `+${format(delta)}`, tone: 'positive' }
  if (delta < 0) return { label: format(delta), tone: 'negative' }
  return { label: 'Без изменения значения', tone: 'neutral' }
})

function itemArt(item: InventoryItem): string | undefined {
  return itemArtUrl(item.iconId)
}

function selectItem(item: InventoryItem): void {
  selectedItemId.value = item.id
  selectedSlotKey.value = item.reforgeSlotKey ?? forgeableAffixes(item)[0]?.slotKey ?? null
  activeMode.value = 'reforge'
  preview.value = null
  pending.value = null
  actionError.value = null
  salvagePreview.value = null
}

function clearSelection(): void {
  selectedItemId.value = null
  selectedSlotKey.value = null
  activeMode.value = 'reforge'
  salvagePreview.value = null
  actionError.value = null
}

function setMode(mode: ForgeMode): void {
  if (pending.value && mode !== 'reforge') return
  activeMode.value = mode
  actionError.value = null
}

async function refreshSelection(): Promise<void> {
  const item = selectedItem.value
  const slotKey = selectedSlotKey.value
  preview.value = null
  pending.value = null
  actionError.value = null
  if (!item || !slotKey) return

  loadingPreview.value = true
  try {
    if (shouldRestorePendingReforge(item)) {
      pending.value = await session.getPendingReforge(item.id)
    }
    if (pending.value) {
      activeMode.value = 'reforge'
      return
    }
    if (!selectedAvailability.value?.available) return

    preview.value = await session.getReforgePreview(item.id, slotKey)
    if (!preview.value) actionError.value = reforgeErrorMessage(session.errorCode)
  } finally {
    loadingPreview.value = false
  }
}

watch([selectedItemId, selectedSlotKey], refreshSelection)

async function roll(): Promise<void> {
  const item = selectedItem.value
  const slotKey = selectedSlotKey.value
  if (!item || !slotKey || !preview.value || !canAffordReforge.value || session.mutationPending) return
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
  if (!salvagePreview.value) actionError.value = 'Не удалось подготовить разбор. Проверьте состояние предмета.'
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
    star_upgrade_item_equipped: 'Снимите предмет с экипировки перед улучшением.',
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
    reforge_not_enough_catalyst: 'Недостаточно катализатора.',
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
</script>

<template>
  <section class="forge-view" data-forge-workshop>
    <header class="forge-header">
      <div class="forge-header__copy">
        <p>КОВКА И ПЕРЕКОВКА</p>
        <h2>Кузница</h2>
        <span>Выберите предмет, затем действие. На экране остаётся только то, что нужно для текущей операции.</span>
      </div>
      <div class="forge-wallet" aria-label="Ресурсы кузницы">
        <div class="forge-wallet__resource forge-wallet__resource--gold">
          <span class="forge-wallet__coin" aria-hidden="true">◆</span>
          <span><small>Золото</small><strong>{{ gold }}</strong></span>
        </div>
        <div class="forge-wallet__resource">
          <span class="forge-wallet__icon"><IconGenerator :config="{ id: 'forge-stone', glyph: 'ore', category: 'resource' }" /></span>
          <span><small>Камни</small><strong>{{ reforgeStones }}</strong></span>
        </div>
      </div>
    </header>

    <section v-if="allEquipment.length" class="forge-layout">
      <div v-if="!selectedItem" class="forge-items" aria-label="Предметы для кузницы">
        <header class="forge-items__header">
          <div><small>СНАРЯЖЕНИЕ</small><strong>Выберите предмет</strong></div>
          <span>{{ availableEquipmentCount }} доступно</span>
        </header>
        <button
          v-for="item in sortedEquipment"
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
            <span class="forge-item__topline">
              <strong>{{ item.name }}</strong>
              <b :class="forgeItemAvailability(item).available ? 'is-ready' : 'is-blocked'">
                {{ forgeItemAvailability(item).available ? 'Готов' : 'Недоступен' }}
              </b>
            </span>
            <small>{{ rarityLabel(item) }} · ур. {{ item.requiredLevel }} · мощь {{ format(item.generatedItem?.itemPower ?? 0) }}</small>
            <ItemQualityStars v-if="item.generatedItem" :id="`forge-${item.id}`" :stars="item.generatedItem.stars" />
            <em v-if="!forgeItemAvailability(item).available">{{ forgeItemAvailability(item).reason }}</em>
          </span>
          <span class="forge-item__chevron" aria-hidden="true">›</span>
        </button>
      </div>

      <UILoadingState
        v-if="!selectedItem"
        class="forge-empty"
        state="empty"
        title="Выберите предмет"
        message="Доступные предметы подняты вверх списка. Заблокированные остаются видимыми с причиной."
      />

      <article v-else class="forge-detail" :data-rarity="selectedItem.rarity">
        <UIButton variant="ghost" class="forge-back" @click="clearSelection">← Все предметы</UIButton>

        <header class="forge-detail__identity">
          <span class="forge-detail__art" :data-rarity="selectedItem.rarity">
            <img v-if="itemArt(selectedItem)" :src="itemArt(selectedItem)" :alt="selectedItem.name" />
            <IconGenerator v-else :config="{ id: `forge-detail-${selectedItem.id}`, glyph: 'sword', category: 'weapon' }" />
          </span>
          <div class="forge-detail__copy">
            <small>{{ rarityLabel(selectedItem) }} · ур. {{ selectedItem.requiredLevel }}</small>
            <h2>{{ selectedItem.name }}</h2>
            <ItemQualityStars v-if="selectedItem.generatedItem" :id="`forge-detail-stars-${selectedItem.id}`" :stars="selectedItem.generatedItem.stars" />
            <div v-if="selectedItem.generatedItem" class="forge-power">
              <span><b>Мощь {{ format(selectedItem.generatedItem.itemPower) }}</b><small>из {{ format(selectedItem.generatedItem.maxItemPower) }}</small></span>
              <span class="forge-power__track"><i :style="{ width: `${selectedPowerPercent}%` }" /></span>
            </div>
          </div>
        </header>

        <p v-if="!selectedAvailability?.available && !pending" class="forge-notice" role="alert">
          <strong>С этим предметом сейчас нельзя работать.</strong>
          <span>{{ selectedAvailability?.reason }}</span>
        </p>

        <template v-if="selectedAvailability?.available || pending">
          <nav class="forge-modes" aria-label="Действия кузницы">
            <button type="button" :class="{ active: activeMode === 'reforge' }" @click="setMode('reforge')">
              <span>↻</span><strong>Перековка</strong><small>Сменить один стат</small>
            </button>
            <button type="button" :class="{ active: activeMode === 'upgrade' }" :disabled="!!pending" @click="setMode('upgrade')">
              <span>★</span><strong>Качество</strong><small>Поднять звезду</small>
            </button>
            <button type="button" :class="{ active: activeMode === 'salvage' }" :disabled="!!pending" @click="setMode('salvage')">
              <span>♢</span><strong>Разбор</strong><small>Вернуть материалы</small>
            </button>
          </nav>

          <section v-if="activeMode === 'reforge'" class="forge-panel forge-panel--reforge">
            <template v-if="pending">
              <header class="forge-section-heading">
                <span>РЕЗУЛЬТАТ</span>
                <strong>Выберите, что оставить</strong>
                <small>Стоимость уже списана. Решение обязательно завершает текущую перековку.</small>
              </header>

              <div class="forge-result" aria-live="polite">
                <div class="forge-result__card forge-result__card--current">
                  <small>ТЕКУЩАЯ</small>
                  <strong>{{ forgeStatLabel(pendingAffixes.current?.statId ?? pending.slotKey) }}</strong>
                  <b>{{ format(pendingAffixes.current?.value ?? 0) }}</b>
                  <span v-if="pendingAffixes.current">Диапазон {{ format(pendingAffixes.current.min) }}–{{ format(pendingAffixes.current.max) }}</span>
                </div>
                <div class="forge-result__arrow" aria-hidden="true">→</div>
                <div class="forge-result__card forge-result__card--proposed">
                  <small>НОВАЯ</small>
                  <strong>{{ forgeStatLabel(pendingAffixes.proposed?.statId ?? pending.slotKey) }}</strong>
                  <b>{{ format(pendingAffixes.proposed?.value ?? 0) }}</b>
                  <span v-if="pendingAffixes.proposed">Диапазон {{ format(pendingAffixes.proposed.min) }}–{{ format(pendingAffixes.proposed.max) }}</span>
                  <em :data-tone="resultComparison.tone">{{ resultComparison.label }}</em>
                </div>
              </div>

              <div class="forge-actions forge-actions--decision">
                <UIButton variant="ghost" :loading="session.mutationPending" @click="decide(false)">Оставить текущую</UIButton>
                <UIButton :loading="session.mutationPending" @click="decide(true)">Применить новую</UIButton>
              </div>
            </template>

            <template v-else>
              <header class="forge-section-heading">
                <span>ШАГ 1</span>
                <strong>Выберите характеристику</strong>
                <small>Меняется только один случайный аффикс. Остальные параметры предмета сохраняются.</small>
              </header>

              <section class="forge-affixes" aria-label="Характеристики предмета">
                <button
                  v-for="affix in affixes"
                  :key="affix.slotKey"
                  type="button"
                  :data-forge-affix="affix.slotKey"
                  :class="{ active: selectedSlotKey === affix.slotKey }"
                  @click="selectedSlotKey = affix.slotKey"
                >
                  <span class="forge-affix__mark">{{ selectedSlotKey === affix.slotKey ? '●' : '○' }}</span>
                  <span class="forge-affix__copy"><strong>{{ forgeStatLabel(affix.statId) }}</strong><small>Диапазон {{ format(affix.min) }}–{{ format(affix.max) }}</small></span>
                  <b>{{ format(affix.value) }}</b>
                </button>
              </section>

              <section class="forge-preview" aria-live="polite">
                <template v-if="loadingPreview">
                  <div class="forge-preview__loading"><span class="forge-spinner" /> Проверяем стоимость…</div>
                </template>
                <template v-else-if="preview">
                  <header class="forge-section-heading forge-section-heading--compact">
                    <span>ШАГ 2</span>
                    <strong>Подтвердите перековку</strong>
                    <small v-if="selectedAffix">Выбрано: {{ forgeStatLabel(selectedAffix.statId) }} {{ format(selectedAffix.value) }}</small>
                  </header>

                  <div class="forge-costs">
                    <div :class="{ insufficient: gold < preview.cost.gold }">
                      <small>Золото</small>
                      <strong>{{ preview.cost.gold }}</strong>
                      <span>у вас {{ gold }}</span>
                    </div>
                    <div :class="{ insufficient: reforgeStones < preview.cost.materialQuantity }">
                      <small>Камни перековки</small>
                      <strong>{{ preview.cost.materialQuantity }}</strong>
                      <span>доступно {{ reforgeStones }}</span>
                    </div>
                  </div>

                  <p class="forge-preview__hint">После оплаты выпадет новая характеристика. Вы сможете сравнить её с текущей и только потом выбрать, какую оставить.</p>
                  <p v-if="reforgeShortage" class="forge-shortage">{{ reforgeShortage }}</p>
                  <UIButton :disabled="!canAffordReforge" :loading="session.mutationPending" data-forge-roll @click="roll">
                    Перековать характеристику
                  </UIButton>
                  <small class="forge-attempt">Попыток перековки предмета: {{ selectedItem.reforgeCount ?? 0 }}</small>
                </template>
              </section>
            </template>
          </section>

          <section v-else-if="activeMode === 'upgrade'" class="forge-panel forge-panel--upgrade">
            <header class="forge-section-heading">
              <span>КАЧЕСТВО ПРЕДМЕТА</span>
              <strong>Усиление без смены характеристик</strong>
              <small>Повышение звезды усиливает существующие rolled-характеристики, но не меняет их типы.</small>
            </header>

            <div v-if="selectedItem.generatedItem" class="forge-upgrade-hero">
              <div><small>СЕЙЧАС</small><strong>★{{ selectedItem.generatedItem.stars }}</strong></div>
              <span aria-hidden="true">→</span>
              <div><small>ПОСЛЕ</small><strong>{{ selectedItem.generatedItem.stars < 5 ? `★${selectedItem.generatedItem.stars + 1}` : '★★★★★' }}</strong></div>
            </div>

            <template v-if="selectedItem.generatedItem && selectedItem.generatedItem.stars < 5">
              <div class="forge-info-box">
                <strong>{{ selectedItem.generatedItem.stars === 4 ? 'Финальное улучшение' : 'Следующий уровень качества' }}</strong>
                <span>{{ selectedItem.generatedItem.stars === 4 ? 'Для ★★★★★ потребуется Ядро подземелья.' : 'Сервер проверит золото и Камни перековки перед списанием.' }}</span>
              </div>
              <UIButton :loading="session.mutationPending" data-forge-star-upgrade @click="upgradeStars">
                Улучшить до ★{{ selectedItem.generatedItem.stars + 1 }}
              </UIButton>
              <small class="forge-safe-note">Если ресурсов недостаточно, улучшение не произойдёт и ничего не будет списано.</small>
            </template>
            <div v-else class="forge-maxed">
              <strong>Максимальное качество</strong>
              <span>Предмет уже достиг ★★★★★.</span>
            </div>
          </section>

          <section v-else class="forge-panel forge-panel--salvage">
            <header class="forge-section-heading">
              <span>РАЗБОР</span>
              <strong>Разобрать предмет на материалы</strong>
              <small>Сначала покажем награду. Удаление предмета произойдёт только после отдельного подтверждения.</small>
            </header>

            <template v-if="!salvagePreview">
              <div class="forge-danger-box">
                <strong>Предмет будет уничтожен</strong>
                <span>Перед подтверждением вы увидите точное количество возвращаемых материалов.</span>
              </div>
              <UIButton variant="secondary" :loading="session.mutationPending" @click="prepareSalvage">Показать результат разбора</UIButton>
            </template>
            <template v-else>
              <div class="forge-salvage-reward">
                <small>ВЫ ПОЛУЧИТЕ</small>
                <div><span class="forge-wallet__icon"><IconGenerator :config="{ id: 'salvage-stone', glyph: 'ore', category: 'resource' }" /></span><strong>Камень перековки ×{{ salvagePreview.reward.reforgeStoneQuantity }}</strong></div>
                <div v-if="salvagePreview.reward.materialQuantity > 0"><span class="forge-salvage-reward__dot" /><strong>Материал ×{{ salvagePreview.reward.materialQuantity }}</strong></div>
              </div>
              <div class="forge-actions forge-actions--salvage">
                <UIButton variant="ghost" @click="salvagePreview = null">Назад</UIButton>
                <UIButton variant="danger" :loading="session.mutationPending" @click="confirmSalvage">Разобрать навсегда</UIButton>
              </div>
            </template>
          </section>
        </template>

        <p v-if="actionError" class="forge-error" role="alert">{{ actionError }}</p>
      </article>
    </section>

    <UILoadingState
      v-else
      state="empty"
      title="Нет снаряжения"
      message="Найдите случайно сгенерированное снаряжение — после этого оно появится в кузнице."
    />
  </section>
</template>

<style scoped>
.forge-view { display: grid; gap: var(--ui-space-3); }
.forge-header, .forge-detail, .forge-items { border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: linear-gradient(145deg, rgb(23 32 51 / 96%), rgb(10 15 27 / 96%)); }
.forge-header { position: relative; display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: center; gap: var(--ui-space-3); overflow: hidden; padding: var(--ui-space-3); border-color: rgb(232 200 102 / 28%); background: radial-gradient(circle at 12% 0%, rgb(232 200 102 / 14%), transparent 38%), linear-gradient(120deg, rgb(24 29 38 / 98%), rgb(12 17 29 / 98%)); }
.forge-header::after { position: absolute; right: -38px; bottom: -64px; width: 150px; height: 150px; border: 1px solid rgb(232 200 102 / 10%); border-radius: 50%; content: ''; pointer-events: none; }
.forge-header__copy { min-width: 0; }
.forge-header p, .forge-header h2, .forge-header span, .forge-detail h2 { margin: 0; }
.forge-header p, .forge-items small, .forge-section-heading > span { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); letter-spacing: .09em; }
.forge-header h2 { margin-top: 2px; font-size: var(--ui-font-size-xl); }
.forge-header__copy > span { display: block; max-width: 500px; margin-top: 4px; color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-sm); }
.forge-wallet { position: relative; z-index: 1; display: grid; grid-template-columns: repeat(2, minmax(82px, 1fr)); gap: 6px; }
.forge-wallet__resource { display: flex; align-items: center; gap: 7px; min-width: 84px; padding: 7px 9px; border: 1px solid rgb(232 200 102 / 18%); border-radius: var(--ui-radius-md); background: rgb(5 8 13 / 52%); }
.forge-wallet__resource > span:last-child { display: grid; line-height: 1.05; }
.forge-wallet__resource small { color: var(--ui-color-text-muted); font-size: 10px; }
.forge-wallet__resource strong { margin-top: 3px; color: var(--ui-color-text-primary); font-size: var(--ui-font-size-sm); }
.forge-wallet__icon { display: grid; width: 26px; height: 26px; flex: 0 0 26px; }
.forge-wallet__coin { display: grid; width: 26px; height: 26px; flex: 0 0 26px; border: 1px solid rgb(232 200 102 / 42%); border-radius: 50%; background: rgb(232 200 102 / 10%); color: var(--ui-color-gold); font-size: 12px; place-items: center; }
.forge-layout { display: grid; gap: var(--ui-space-4); }
.forge-items { padding: var(--ui-space-2); }
.forge-items__header { display: flex; align-items: end; justify-content: space-between; gap: var(--ui-space-2); margin: 2px 2px var(--ui-space-2); padding: 0 var(--ui-space-1); }
.forge-items__header > div { display: grid; gap: 2px; }
.forge-items__header > span { padding: 3px 7px; border: 1px solid rgb(94 203 151 / 22%); border-radius: 999px; background: rgb(94 203 151 / 7%); color: var(--ui-color-success); font-size: var(--ui-font-size-xs); white-space: nowrap; }
.forge-item { display: grid; width: 100%; min-height: 76px; grid-template-columns: auto minmax(0, 1fr) auto; align-items: center; gap: var(--ui-space-3); margin-top: 3px; padding: var(--ui-space-2); border: 1px solid transparent; border-radius: var(--ui-radius-md); background: transparent; color: var(--ui-color-text-primary); font: inherit; text-align: left; cursor: pointer; transition: border-color var(--ui-transition-fast), background var(--ui-transition-fast), transform var(--ui-transition-fast); }
.forge-item:hover { border-color: var(--ui-color-border); background: rgb(255 255 255 / 2%); }
.forge-item:active { transform: scale(.995); }
.forge-item.active { border-color: var(--ui-color-primary); background: rgb(132 121 250 / 10%); }
.forge-item.unavailable { opacity: .62; }
.forge-item__art, .forge-detail__art { display: grid; width: 52px; height: 52px; flex: 0 0 52px; place-items: center; overflow: hidden; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: rgb(0 0 0 / 24%); }
.forge-item__art img, .forge-detail__art img { width: 100%; height: 100%; object-fit: cover; }
.forge-item__copy { display: grid; min-width: 0; gap: 3px; }
.forge-item__topline { display: flex; min-width: 0; align-items: center; justify-content: space-between; gap: 8px; }
.forge-item__topline > strong { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.forge-item__topline b { flex: 0 0 auto; padding: 2px 6px; border-radius: 999px; font-size: 10px; font-weight: var(--ui-font-weight-semibold); }
.forge-item__topline .is-ready { background: rgb(94 203 151 / 10%); color: var(--ui-color-success); }
.forge-item__topline .is-blocked { background: rgb(226 106 125 / 10%); color: var(--ui-color-danger); }
.forge-item__copy small { letter-spacing: 0; }
.forge-item__copy em { overflow: hidden; color: var(--ui-color-warning); font-size: var(--ui-font-size-xs); font-style: normal; text-overflow: ellipsis; white-space: nowrap; }
.forge-item__chevron { color: var(--ui-color-text-muted); font-size: 26px; }
.forge-detail { padding: var(--ui-space-3); }
.forge-detail__identity { display: flex; gap: var(--ui-space-3); padding-bottom: var(--ui-space-3); border-bottom: 1px solid var(--ui-color-border); }
.forge-detail__art { width: 66px; height: 66px; flex-basis: 66px; }
.forge-detail__copy { display: grid; min-width: 0; align-content: start; gap: 4px; flex: 1; }
.forge-detail__copy > small { color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-xs); }
.forge-detail__copy h2 { overflow-wrap: anywhere; font-size: var(--ui-font-size-lg); }
.forge-power { display: grid; gap: 5px; margin-top: 2px; }
.forge-power > span:first-child { display: flex; align-items: center; justify-content: space-between; gap: 8px; color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-xs); }
.forge-power > span:first-child b { color: var(--ui-color-text-primary); }
.forge-power__track { display: block; width: 100%; height: 5px; overflow: hidden; border-radius: 999px; background: rgb(255 255 255 / 6%); }
.forge-power__track i { display: block; height: 100%; border-radius: inherit; background: linear-gradient(90deg, var(--ui-color-primary), var(--ui-color-gold)); }
.forge-back { margin-bottom: var(--ui-space-3); }
.forge-notice, .forge-error { margin: var(--ui-space-3) 0 0; padding: var(--ui-space-3); border: 1px solid rgb(226 106 125 / 24%); border-radius: var(--ui-radius-md); background: rgb(226 106 125 / 7%); color: var(--ui-color-danger); }
.forge-notice { display: grid; gap: 3px; }
.forge-notice span { color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-sm); }
.forge-modes { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 6px; margin-top: var(--ui-space-3); padding: 5px; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: rgb(0 0 0 / 14%); }
.forge-modes button { display: grid; min-width: 0; min-height: 62px; gap: 1px; place-items: center; padding: 6px 4px; border: 1px solid transparent; border-radius: var(--ui-radius-sm); background: transparent; color: var(--ui-color-text-secondary); font: inherit; cursor: pointer; }
.forge-modes button > span { color: var(--ui-color-gold); font-size: 16px; }
.forge-modes button strong { font-size: var(--ui-font-size-xs); }
.forge-modes button small { overflow: hidden; max-width: 100%; color: var(--ui-color-text-muted); font-size: 10px; text-overflow: ellipsis; white-space: nowrap; }
.forge-modes button.active { border-color: rgb(146 136 255 / 34%); background: linear-gradient(180deg, rgb(132 121 250 / 14%), rgb(132 121 250 / 5%)); color: var(--ui-color-text-primary); }
.forge-modes button:disabled { cursor: not-allowed; opacity: .35; }
.forge-panel { display: grid; gap: var(--ui-space-3); margin-top: var(--ui-space-3); padding: var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: rgb(0 0 0 / 10%); }
.forge-panel--upgrade { border-color: rgb(232 200 102 / 24%); background: radial-gradient(circle at 50% -20%, rgb(232 200 102 / 10%), transparent 48%), rgb(0 0 0 / 10%); }
.forge-panel--salvage { border-color: rgb(226 106 125 / 18%); }
.forge-section-heading { display: grid; gap: 3px; }
.forge-section-heading > span { color: var(--ui-color-gold); }
.forge-section-heading > strong { font-size: var(--ui-font-size-md); }
.forge-section-heading > small { color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-sm); line-height: 1.35; }
.forge-section-heading--compact { margin-bottom: 2px; }
.forge-affixes { display: grid; gap: 7px; }
.forge-affixes button { display: grid; grid-template-columns: auto minmax(0, 1fr) auto; align-items: center; gap: 9px; min-height: var(--ui-touch-target); padding: 9px 10px; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: rgb(255 255 255 / 1.5%); color: var(--ui-color-text-primary); font: inherit; text-align: left; cursor: pointer; }
.forge-affixes button.active { border-color: rgb(146 136 255 / 52%); background: linear-gradient(90deg, rgb(132 121 250 / 14%), rgb(132 121 250 / 4%)); box-shadow: inset 3px 0 rgb(146 136 255 / 70%); }
.forge-affix__mark { color: var(--ui-color-primary); }
.forge-affix__copy { display: grid; min-width: 0; gap: 2px; }
.forge-affix__copy small { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.forge-affixes button > b { font-size: var(--ui-font-size-md); }
.forge-preview { display: grid; gap: var(--ui-space-3); padding-top: var(--ui-space-3); border-top: 1px solid var(--ui-color-border); }
.forge-preview__loading { display: flex; align-items: center; justify-content: center; gap: 8px; min-height: 90px; color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-sm); }
.forge-spinner { width: 16px; height: 16px; border: 2px solid currentColor; border-right-color: transparent; border-radius: 50%; animation: forge-spin .8s linear infinite; }
.forge-costs { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
.forge-costs > div { display: grid; gap: 2px; padding: 10px; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); background: rgb(0 0 0 / 15%); }
.forge-costs small, .forge-costs span { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.forge-costs strong { font-size: var(--ui-font-size-lg); }
.forge-costs .insufficient { border-color: rgb(226 106 125 / 28%); background: rgb(226 106 125 / 5%); }
.forge-costs .insufficient strong { color: var(--ui-color-danger); }
.forge-preview__hint { margin: 0; color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-sm); line-height: 1.4; }
.forge-shortage { margin: 0; color: var(--ui-color-danger); font-size: var(--ui-font-size-sm); }
.forge-attempt { color: var(--ui-color-text-muted); text-align: center; }
.forge-result { display: grid; grid-template-columns: minmax(0, 1fr) auto minmax(0, 1fr); align-items: stretch; gap: 8px; }
.forge-result__card { display: grid; min-width: 0; gap: 3px; padding: var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: rgb(0 0 0 / 16%); }
.forge-result__card--proposed { border-color: rgb(146 136 255 / 36%); background: rgb(132 121 250 / 7%); }
.forge-result__card small { color: var(--ui-color-text-muted); font-size: 10px; letter-spacing: .08em; }
.forge-result__card strong { overflow-wrap: anywhere; font-size: var(--ui-font-size-sm); }
.forge-result__card b { color: var(--ui-color-gold); font-size: var(--ui-font-size-xl); }
.forge-result__card span { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.forge-result__card em { margin-top: 3px; color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-xs); font-style: normal; }
.forge-result__card em[data-tone='positive'] { color: var(--ui-color-success); }
.forge-result__card em[data-tone='negative'] { color: var(--ui-color-danger); }
.forge-result__arrow { display: grid; color: var(--ui-color-gold); place-items: center; }
.forge-actions { display: grid; grid-template-columns: 1fr 1fr; gap: var(--ui-space-2); }
.forge-upgrade-hero { display: grid; grid-template-columns: 1fr auto 1fr; align-items: center; gap: 12px; }
.forge-upgrade-hero > div { display: grid; gap: 4px; padding: var(--ui-space-3); border: 1px solid rgb(232 200 102 / 22%); border-radius: var(--ui-radius-md); background: rgb(232 200 102 / 5%); text-align: center; }
.forge-upgrade-hero small { color: var(--ui-color-text-muted); font-size: 10px; letter-spacing: .08em; }
.forge-upgrade-hero strong { color: var(--ui-color-gold); font-size: var(--ui-font-size-xl); }
.forge-upgrade-hero > span { color: var(--ui-color-gold); }
.forge-info-box, .forge-danger-box, .forge-maxed { display: grid; gap: 3px; padding: var(--ui-space-3); border-radius: var(--ui-radius-md); }
.forge-info-box { border: 1px solid rgb(232 200 102 / 18%); background: rgb(232 200 102 / 5%); }
.forge-danger-box { border: 1px solid rgb(226 106 125 / 22%); background: rgb(226 106 125 / 5%); }
.forge-maxed { border: 1px solid rgb(94 203 151 / 20%); background: rgb(94 203 151 / 5%); }
.forge-info-box span, .forge-danger-box span, .forge-maxed span { color: var(--ui-color-text-secondary); font-size: var(--ui-font-size-sm); }
.forge-safe-note { color: var(--ui-color-text-muted); text-align: center; }
.forge-salvage-reward { display: grid; gap: 8px; padding: var(--ui-space-3); border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: rgb(0 0 0 / 14%); }
.forge-salvage-reward > small { color: var(--ui-color-text-muted); font-size: 10px; letter-spacing: .08em; }
.forge-salvage-reward > div { display: flex; align-items: center; gap: 8px; }
.forge-salvage-reward__dot { width: 9px; height: 9px; border-radius: 50%; background: var(--ui-color-secondary); }
.forge-empty { min-height: 220px; }
@keyframes forge-spin { to { transform: rotate(360deg); } }
@media (max-width: 560px) {
  .forge-header { grid-template-columns: 1fr; }
  .forge-wallet { grid-template-columns: 1fr 1fr; }
  .forge-header__copy > span { font-size: var(--ui-font-size-xs); }
  .forge-result { grid-template-columns: 1fr; }
  .forge-result__arrow { transform: rotate(90deg); }
  .forge-actions { grid-template-columns: 1fr; }
  .forge-modes button small { display: none; }
  .forge-modes button { min-height: 52px; }
}
@media (min-width: 720px) {
  .forge-layout { grid-template-columns: minmax(230px, .72fr) minmax(0, 1.28fr); align-items: start; }
  .forge-items { position: sticky; top: 62px; max-height: calc(100dvh - 84px); overflow: auto; }
}
</style>
