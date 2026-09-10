<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import type { InventoryItem, MerchantItem, MerchantSnapshot } from '@/api/contracts'
import { gameArt } from '@/assets/gameArt'
import { itemArtUrl } from '@/assets/itemArt'
import { consumableActionLabel } from '@/game/items/consumablePresentation'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIModal } from '@/ui/components'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { GlyphName } from '@/ui/icons/icon.types'

const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{ close: [] }>()
const MERCHANT_ID = 'MARCUS_SUPPLIES'
const session = useGameSessionStore()
const merchant = ref<MerchantSnapshot | null>(null)
const loading = ref(false)
const activeTab = ref<'buy' | 'sell'>('buy')
const selectedOfferId = ref<string | null>(null)
const searchQuery = ref('')
const activeFilter = ref<'all' | MerchantItem['type']>('all')
const merchantFilters = ['all', 'Equipment', 'Consumable', 'Material'] as const

const sellableItems = computed(() => session.snapshot?.character?.inventory.items
  .filter((item) =>
    !item.equippedSlot
    && !item.isLocked
    && item.sellPriceGold > 0,
  ) ?? [])
const protectedItemsCount = computed(() => session.snapshot?.character?.inventory.items
  .filter((item) =>
    !item.equippedSlot
    && item.isLocked
    && item.sellPriceGold > 0,
  ).length ?? 0)
const visibleOffers = computed(() => {
  const query = searchQuery.value.trim().toLocaleLowerCase()
  return merchant.value?.items.filter(item => {
    const matchesType = activeFilter.value === 'all' || item.type === activeFilter.value
    const matchesQuery = !query
      || item.name.toLocaleLowerCase().includes(query)
      || item.description.toLocaleLowerCase().includes(query)
    return matchesType && matchesQuery
  }) ?? []
})
const selectedOffer = computed(() =>
  visibleOffers.value.find(item => item.definitionId === selectedOfferId.value)
    ?? visibleOffers.value[0]
    ?? null,
)

watch(() => props.open, (open) => {
  if (open) {
    activeTab.value = 'buy'
    searchQuery.value = ''
    activeFilter.value = 'all'
    void loadMerchant()
  }
})

async function loadMerchant(): Promise<void> {
  loading.value = true
  try {
    merchant.value = await session.getMerchant(MERCHANT_ID)
    if (
      merchant.value
      && !merchant.value.items.some(item => item.definitionId === selectedOfferId.value)
    ) {
      selectedOfferId.value = merchant.value.items[0]?.definitionId ?? null
    }
  } finally {
    loading.value = false
  }
}

function selectOffer(item: MerchantItem): void {
  selectedOfferId.value = item.definitionId
}

function filterLabel(filter: 'all' | MerchantItem['type']): string {
  if (filter === 'Equipment') return 'Экипировка'
  if (filter === 'Consumable') return 'Расходники'
  if (filter === 'Material') return 'Материалы'
  return 'Все'
}

function itemArt(item: MerchantItem): string | undefined {
  return itemArtUrl(item.iconId)
}

function itemGlyph(item: MerchantItem): GlyphName {
  if (item.type === 'Consumable') return 'potion'
  if (item.type === 'Equipment') return 'sword'
  return 'ore'
}

function itemCategory(type: MerchantItem['type']): 'equipment' | 'consumable' | 'resource' {
  if (type === 'Consumable') return 'consumable'
  if (type === 'Equipment') return 'equipment'
  return 'resource'
}

function itemTypeLabel(item: MerchantItem): string {
  if (item.type === 'Consumable') return 'Расходник'
  if (item.type === 'Equipment') return 'Экипировка'
  return 'Материал'
}

function rarityLabel(item: MerchantItem): string {
  if (item.rarity === 'Uncommon') return 'Необычный'
  if (item.rarity === 'Rare') return 'Редкий'
  if (item.rarity === 'Epic') return 'Эпический'
  if (item.rarity === 'Legendary') return 'Легендарный'
  if (item.rarity === 'Unique') return 'Уникальный'
  return 'Обычный'
}

async function buy(definitionId: string): Promise<void> {
  const updated = await session.buyMerchantItem(MERCHANT_ID, definitionId, 1)
  if (updated) merchant.value = updated
}

function inventoryItemArt(item: InventoryItem): string | undefined {
  return itemArtUrl(item.iconId)
}

function inventoryItemGlyph(item: InventoryItem): GlyphName {
  if (item.type === 'Equipment') return 'sword'
  if (item.type === 'Consumable') return 'potion'
  return 'ore'
}

function inventoryItemTypeLabel(item: InventoryItem): string {
  if (item.type === 'Equipment') return 'ЭКИПИРОВКА'
  if (item.type === 'Consumable') return 'РАСХОДНИК'
  return 'МАТЕРИАЛ'
}

async function sell(item: InventoryItem, quantity: number): Promise<void> {
  const updated = await session.sellMerchantItem(MERCHANT_ID, item.id, quantity)
  if (updated) merchant.value = updated
}
</script>

<template>
  <UIModal :open="open" title="Торговец" @close="emit('close')">
    <section class="merchant">
      <header
        class="merchant__npc"
        :style="{ '--merchant-scene': `url(${gameArt.world.capital})` }"
      >
        <div class="merchant__npc-shade" />
        <div class="merchant__portrait" aria-hidden="true">М</div>
        <div class="merchant__identity">
          <small>ТОРГОВЕЦ ПРИПАСАМИ</small>
          <h2>{{ merchant?.name ?? 'Маркус' }}</h2>
          <p>{{ merchant?.description ?? 'Здесь можно купить лечебные припасы и продать добытые материалы.' }}</p>
        </div>
        <div class="merchant__wallet">
          <small>Ваше золото</small>
          <strong>● {{ merchant?.gold ?? session.snapshot?.character?.gold ?? 0 }}</strong>
        </div>
      </header>

      <nav class="merchant-tabs" aria-label="Разделы торговца">
        <button
          type="button"
          data-merchant-tab="buy"
          :class="{ active: activeTab === 'buy' }"
          @click="activeTab = 'buy'"
        >
          Купить
        </button>
        <button
          type="button"
          data-merchant-tab="sell"
          :class="{ active: activeTab === 'sell' }"
          @click="activeTab = 'sell'"
        >
          Продать
        </button>
        <button type="button" disabled title="Будет добавлено вместе с системой обратного выкупа">Выкуп</button>
      </nav>

      <section v-if="activeTab === 'buy'" class="merchant-panel merchant-panel--buy">
        <header class="merchant-panel__heading">
          <div>
            <small>ВИТРИНА</small>
            <strong>Припасы для следующего похода</strong>
          </div>
          <span>{{ visibleOffers.length }} / {{ merchant?.items.length ?? 0 }} поз.</span>
        </header>

        <div class="merchant-filter" aria-label="Фильтр витрины">
          <label class="merchant-filter__search">
            <span class="sr-only">Поиск товара</span>
            <span aria-hidden="true">⌕</span>
            <input v-model="searchQuery" type="search" placeholder="Найти припасы" />
          </label>
          <div class="merchant-filter__chips" role="group" aria-label="Категория товара">
            <button
              v-for="filter in merchantFilters"
              :key="filter"
              type="button"
              :class="{ active: activeFilter === filter }"
              @click="activeFilter = filter"
            >{{ filterLabel(filter) }}</button>
          </div>
        </div>

        <div v-if="visibleOffers.length" class="merchant-buy">
          <div class="merchant-shelf" aria-label="Товары торговца">
            <button
              v-for="item in visibleOffers"
              :key="item.definitionId"
              type="button"
              class="offer-card"
              :class="{ active: selectedOffer?.definitionId === item.definitionId }"
              :data-rarity="item.rarity"
              :data-merchant-offer="item.definitionId"
              :aria-pressed="selectedOffer?.definitionId === item.definitionId"
              @click="selectOffer(item)"
            >
              <span class="offer-card__icon">
                <img v-if="itemArt(item)" :src="itemArt(item)" :alt="item.name" loading="lazy" decoding="async" />
                <IconGenerator
                  v-else
                  :config="{ id: `merchant-${item.definitionId}`, glyph: itemGlyph(item), category: itemCategory(item.type) }"
                />
              </span>
              <span class="offer-card__copy">
                <small>{{ rarityLabel(item) }}</small>
                <strong>{{ item.name }}</strong>
                <b>● {{ item.buyPriceGold }}</b>
              </span>
            </button>
          </div>

          <article v-if="selectedOffer" class="merchant-detail" data-merchant-detail>
            <div class="merchant-detail__identity">
              <span class="merchant-detail__icon" :data-rarity="selectedOffer.rarity">
                <img v-if="itemArt(selectedOffer)" :src="itemArt(selectedOffer)" :alt="selectedOffer.name" decoding="async" />
                <IconGenerator
                  v-else
                  :config="{ id: `merchant-detail-${selectedOffer.definitionId}`, glyph: itemGlyph(selectedOffer), category: itemCategory(selectedOffer.type) }"
                />
              </span>
              <div>
                <small>{{ rarityLabel(selectedOffer) }} · {{ itemTypeLabel(selectedOffer) }}</small>
                <h3>{{ selectedOffer.name }}</h3>
              </div>
            </div>

            <p>{{ selectedOffer.description }}</p>
            <div v-if="selectedOffer.type === 'Consumable' && selectedOffer.consumableActions.length" class="merchant-detail__effect">
              <span>Эффект</span>
              <strong>{{ selectedOffer.consumableActions.map(consumableActionLabel).join(' · ') }}</strong>
              <small v-if="selectedOffer.consumableCooldownSeconds">Кулдаун категории: {{ selectedOffer.consumableCooldownSeconds }} сек.</small>
            </div>

            <footer class="merchant-detail__purchase">
              <div>
                <small>Цена</small>
                <strong>● {{ selectedOffer.buyPriceGold }}</strong>
              </div>
              <UIButton
                data-buy-selected
                :loading="session.mutationPending"
                :disabled="session.mutationPending || (merchant?.gold ?? 0) < selectedOffer.buyPriceGold"
                @click="buy(selectedOffer.definitionId)"
              >
                Купить · ● {{ selectedOffer.buyPriceGold }}
              </UIButton>
            </footer>
          </article>
        </div>

        <p v-if="loading" class="muted">Маркус раскладывает товар…</p>
        <p v-else-if="merchant && merchant.items.length === 0" class="muted">На витрине пока ничего нет.</p>
        <p v-else-if="merchant && visibleOffers.length === 0" class="muted">По этому фильтру товаров нет.</p>
      </section>

      <section v-else class="merchant-panel merchant-panel--sell">
        <header class="merchant-panel__heading">
          <div>
            <small>ПРОДАЖА</small>
            <strong>Предметы из вашего рюкзака</strong>
          </div>
          <span>{{ sellableItems.length }} поз.</span>
        </header>

        <p v-if="protectedItemsCount > 0" class="protected-hint">
          <IconGenerator :config="{ id: 'merchant-locked-items', glyph: 'lock', category: 'utility', state: 'locked' }" />
          Защищённые предметы скрыты из продажи: {{ protectedItemsCount }}
        </p>

        <div v-if="sellableItems.length" class="sell-list">
          <article v-for="item in sellableItems" :key="item.id" class="sell-card" :data-sell-item="item.id">
            <span class="sell-card__icon" :data-rarity="item.rarity">
              <img v-if="inventoryItemArt(item)" :src="inventoryItemArt(item)" :alt="item.name" loading="lazy" decoding="async" />
              <IconGenerator
                v-else
                :config="{ id: `merchant-inventory-${item.id}`, glyph: inventoryItemGlyph(item), category: item.type === 'Equipment' ? 'equipment' : item.type === 'Consumable' ? 'consumable' : 'resource' }"
              />
            </span>
            <div class="sell-card__copy">
              <small>{{ inventoryItemTypeLabel(item) }}</small>
              <strong>{{ item.name }}</strong>
              <span>В рюкзаке: {{ item.quantity }}</span>
            </div>
            <div class="sell-card__price">
              <small>за штуку</small>
              <strong>● {{ item.sellPriceGold }}</strong>
            </div>
            <div class="sell-card__actions">
              <UIButton variant="ghost" :disabled="session.mutationPending" @click="sell(item, 1)">
                1 шт.
              </UIButton>
              <UIButton
                :disabled="session.mutationPending"
                @click="sell(item, item.quantity)"
              >
                {{ item.quantity > 1 ? 'Всё' : 'Продать' }} · {{ item.sellPriceGold * item.quantity }}
              </UIButton>
            </div>
          </article>
        </div>

        <p v-else class="muted">В рюкзаке пока нет предметов, которые Маркус готов купить.</p>
      </section>
    </section>
  </UIModal>
</template>

<style scoped>
.merchant {
  display: grid;
  overflow: hidden;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-lg);
  background:
    radial-gradient(circle at 15% 0, rgb(146 136 255 / 8%), transparent 14rem),
    linear-gradient(180deg, rgb(13 18 30 / 96%), rgb(6 9 16 / 99%));
}

.merchant__npc {
  --merchant-scene: none;

  position: relative;
  display: grid;
  grid-template-columns: 4.6rem minmax(0, 1fr) auto;
  align-items: end;
  gap: var(--ui-space-3);
  min-height: 8.5rem;
  padding: var(--ui-space-4);
  overflow: hidden;
  border-bottom: 1px solid var(--ui-color-border);
  background:
    var(--merchant-scene) center 42% / cover,
    var(--ui-color-surface-1);
}

.merchant__npc-shade {
  position: absolute;
  inset: 0;
  background:
    linear-gradient(180deg, rgb(4 6 10 / 28%), rgb(5 8 14 / 94%)),
    linear-gradient(90deg, rgb(7 10 17 / 28%), transparent 65%);
  pointer-events: none;
}

.merchant__portrait,
.merchant__identity,
.merchant__wallet {
  position: relative;
  z-index: 1;
}

.merchant__portrait {
  display: grid;
  width: 4.6rem;
  height: 4.6rem;
  place-items: center;
  border: 1px solid color-mix(in srgb, var(--ui-color-gold) 34%, var(--ui-color-border));
  border-radius: 50%;
  background:
    radial-gradient(circle at 38% 24%, rgb(255 255 255 / 10%), transparent 33%),
    linear-gradient(180deg, rgb(49 42 34 / 96%), rgb(12 14 20 / 99%));
  box-shadow: 0 0 0 3px rgb(232 200 102 / 5%), 0 .6rem 1.4rem rgb(0 0 0 / 30%);
  color: #ead9a2;
  font-family: var(--ui-font-display);
  font-size: 2rem;
}

.merchant__identity {
  display: grid;
  min-width: 0;
  gap: 2px;
  text-shadow: 0 2px 8px rgb(0 0 0 / 75%);
}

.merchant__identity small,
.merchant__wallet small,
.merchant-panel__heading small,
.offer-card small,
.merchant-detail small,
.sell-card small {
  color: var(--ui-color-text-muted);
  font-size: .53rem;
  font-weight: 800;
  letter-spacing: .07em;
  text-transform: uppercase;
}

.merchant__identity h2,
.merchant__identity p {
  margin: 0;
}

.merchant__identity h2 {
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-xl);
}

.merchant__identity p {
  max-width: 30rem;
  color: #c2c8d4;
  font-size: .68rem;
  line-height: 1.4;
}

.merchant__wallet {
  display: grid;
  justify-items: end;
  gap: 2px;
  padding: 6px 9px;
  border: 1px solid rgb(232 200 102 / 20%);
  border-radius: var(--ui-radius-md);
  background: rgb(8 10 14 / 72%);
  white-space: nowrap;
  backdrop-filter: blur(7px);
}

.merchant__wallet strong {
  color: var(--ui-color-gold);
  font-size: .75rem;
  font-variant-numeric: tabular-nums;
}

.merchant-tabs {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  border-bottom: 1px solid var(--ui-color-border);
  background: rgb(255 255 255 / 1.5%);
}

.merchant-tabs button {
  position: relative;
  min-height: 2.85rem;
  border: 0;
  border-right: 1px solid rgb(255 255 255 / 5%);
  background: transparent;
  color: var(--ui-color-text-muted);
  font: inherit;
  font-size: .68rem;
  font-weight: 800;
  text-transform: uppercase;
}

.merchant-tabs button:last-child {
  border-right: 0;
}

.merchant-tabs button.active {
  background: linear-gradient(180deg, rgb(232 200 102 / 7%), transparent);
  color: #ead9a2;
}

.merchant-tabs button.active::after {
  position: absolute;
  right: 22%;
  bottom: -1px;
  left: 22%;
  height: 2px;
  border-radius: var(--ui-radius-round);
  background: var(--ui-color-gold);
  box-shadow: 0 0 8px rgb(232 200 102 / 35%);
  content: '';
}

.merchant-tabs button:disabled {
  opacity: .25;
}

.merchant-panel {
  display: grid;
}

.merchant-panel__heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-3);
  padding: var(--ui-space-3) var(--ui-space-4);
  border-bottom: 1px solid rgb(255 255 255 / 5%);
}

.merchant-panel__heading > div {
  display: grid;
  gap: 2px;
}

.merchant-panel__heading strong {
  font-size: var(--ui-font-size-sm);
}

.merchant-panel__heading > span {
  color: var(--ui-color-text-muted);
  font-size: .6rem;
}

.merchant-filter {
  display: grid;
  gap: 7px;
  padding: 9px var(--ui-space-4);
  border-bottom: 1px solid rgb(255 255 255 / 5%);
  background: rgb(4 7 12 / 56%);
}

.merchant-filter__search {
  display: grid;
  grid-template-columns: 1.5rem minmax(0, 1fr);
  align-items: center;
  gap: 5px;
  min-height: 36px;
  padding: 0 9px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-sm);
  background: rgb(12 17 24 / 94%);
  color: var(--ui-color-gold);
}

.merchant-filter__search input {
  width: 100%;
  min-width: 0;
  border: 0;
  outline: 0;
  background: transparent;
  color: var(--ui-color-text-primary);
  font: inherit;
  font-size: .7rem;
}

.merchant-filter__search input::placeholder {
  color: var(--ui-color-text-muted);
}

.merchant-filter__chips {
  display: flex;
  gap: 5px;
  overflow-x: auto;
  scrollbar-width: none;
}

.merchant-filter__chips::-webkit-scrollbar {
  display: none;
}

.merchant-filter__chips button {
  flex: 0 0 auto;
  min-height: 28px;
  padding: 4px 8px;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-round);
  background: transparent;
  color: var(--ui-color-text-muted);
  font: inherit;
  font-size: .55rem;
  white-space: nowrap;
}

.merchant-filter__chips button.active {
  border-color: rgb(209 170 98 / 58%);
  background: rgb(209 170 98 / 12%);
  color: #f0d28e;
}

.merchant-buy {
  display: grid;
  grid-template-columns: minmax(8.5rem, .8fr) minmax(0, 1.2fr);
  min-height: 17rem;
}

.merchant-shelf {
  display: grid;
  align-content: start;
  gap: 1px;
  max-height: 19rem;
  overflow-y: auto;
  border-right: 1px solid rgb(255 255 255 / 6%);
  background: rgb(3 6 11 / 42%);
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}

.offer-card {
  display: grid;
  grid-template-columns: 3rem minmax(0, 1fr);
  align-items: center;
  gap: var(--ui-space-2);
  min-width: 0;
  padding: var(--ui-space-3);
  border: 0;
  border-bottom: 1px solid rgb(255 255 255 / 5%);
  background: linear-gradient(90deg, rgb(255 255 255 / 1.2%), transparent);
  color: var(--ui-color-text-secondary);
  font: inherit;
  text-align: left;
}

.offer-card.active {
  background:
    linear-gradient(90deg, rgb(232 200 102 / 10%), transparent 78%),
    rgb(255 255 255 / 1.5%);
  box-shadow: inset 2px 0 0 var(--ui-color-gold);
}

.offer-card__icon {
  display: grid;
  width: 3rem;
  height: 3rem;
  place-items: center;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: var(--ui-radius-md);
  background: rgb(5 8 14 / 92%);
  color: #aaa3ff;
  font-size: 1.25rem;
}

.offer-card[data-rarity='Uncommon'] .offer-card__icon {
  border-color: rgb(79 185 150 / 42%);
  color: #84d5bb;
}

.offer-card[data-rarity='Rare'] .offer-card__icon {
  border-color: rgb(88 149 205 / 46%);
}

.offer-card[data-rarity='Epic'] .offer-card__icon {
  border-color: rgb(146 136 255 / 55%);
}

.offer-card__copy {
  display: grid;
  min-width: 0;
  gap: 2px;
}

.offer-card__copy strong {
  overflow: hidden;
  font-size: .7rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.offer-card__copy b {
  color: var(--ui-color-gold);
  font-size: .64rem;
}

.merchant-detail {
  display: grid;
  align-content: start;
  gap: var(--ui-space-3);
  padding: var(--ui-space-4);
  background:
    radial-gradient(circle at 100% 0, rgb(232 200 102 / 7%), transparent 12rem);
}

.merchant-detail__identity {
  display: grid;
  grid-template-columns: 4rem minmax(0, 1fr);
  align-items: center;
  gap: var(--ui-space-3);
}

.merchant-detail__icon {
  display: grid;
  width: 4rem;
  height: 4rem;
  place-items: center;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: var(--ui-radius-md);
  background: rgb(4 7 12 / 86%);
  color: #aaa3ff;
  font-size: 1.7rem;
}

.merchant-detail__icon[data-rarity='Uncommon'] {
  border-color: rgb(79 185 150 / 48%);
  color: #84d5bb;
}

.merchant-detail__identity h3 {
  margin: 2px 0 0;
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-lg);
}

.merchant-detail > p {
  margin: 0;
  color: var(--ui-color-text-muted);
  font-size: .7rem;
  line-height: 1.5;
}

.offer-card__icon img,
.merchant-detail__icon img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.merchant-detail__effect {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-3);
  padding: var(--ui-space-2) var(--ui-space-3);
  border: 1px solid rgb(79 185 150 / 16%);
  border-radius: var(--ui-radius-md);
  background: rgb(79 185 150 / 4%);
}

.merchant-detail__effect span {
  color: var(--ui-color-text-muted);
  font-size: .61rem;
}

.merchant-detail__effect strong {
  color: #84d5bb;
  font-size: .68rem;
}

.merchant-detail__purchase {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: var(--ui-space-3);
  margin-top: auto;
  padding-top: var(--ui-space-3);
  border-top: 1px solid rgb(255 255 255 / 6%);
}

.merchant-detail__purchase > div {
  display: grid;
  gap: 1px;
}

.merchant-detail__purchase strong {
  color: var(--ui-color-gold);
  font-size: var(--ui-font-size-md);
}

.protected-hint {
  margin: 0;
  padding: var(--ui-space-2) var(--ui-space-4);
  border-bottom: 1px solid rgb(255 255 255 / 5%);
  background: rgb(146 136 255 / 4%);
  color: #b9b4e8;
  font-size: .62rem;
}

.sell-list {
  display: grid;
}

.sell-card {
  display: grid;
  grid-template-columns: 3rem minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--ui-space-3);
  padding: var(--ui-space-3) var(--ui-space-4);
  border-bottom: 1px solid rgb(255 255 255 / 6%);
}

.sell-card:last-child {
  border-bottom: 0;
}

.sell-card__icon {
  display: grid;
  width: 3rem;
  height: 3rem;
  place-items: center;
  border: 1px solid rgb(75 164 178 / 30%);
  border-radius: var(--ui-radius-md);
  background: rgb(5 8 14 / 78%);
  color: #77bbc5;
  font-size: 1.1rem;
}

.sell-card__icon img {
  width: 100%;
  height: 100%;
  border-radius: inherit;
  object-fit: cover;
}

.sell-card__copy {
  display: grid;
  min-width: 0;
  gap: 1px;
}

.sell-card__copy strong {
  overflow: hidden;
  font-size: .7rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sell-card__copy span {
  color: var(--ui-color-text-muted);
  font-size: .62rem;
}

.sell-card__price {
  display: grid;
  justify-items: end;
  gap: 1px;
  white-space: nowrap;
}

.sell-card__price strong {
  color: var(--ui-color-gold);
  font-size: .7rem;
}

.sell-card__actions {
  grid-column: 1 / -1;
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--ui-space-2);
}

.sell-card__actions :deep(.ui-button) {
  width: 100%;
}

.muted {
  margin: 0;
  padding: var(--ui-space-4);
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-sm);
  text-align: center;
}

@media (max-width: 520px) {
  .merchant__npc {
    grid-template-columns: 3.8rem minmax(0, 1fr);
    min-height: 8rem;
    padding: var(--ui-space-3);
  }

  .merchant__portrait {
    width: 3.8rem;
    height: 3.8rem;
  }

  .merchant__wallet {
    grid-column: 1 / -1;
    grid-template-columns: 1fr auto;
    align-items: center;
    justify-items: stretch;
  }

  .merchant__wallet strong {
    justify-self: end;
  }

  .merchant-buy {
    grid-template-columns: 1fr;
  }

  .merchant-shelf {
    grid-template-columns: repeat(2, minmax(0, 1fr));
    max-height: 13rem;
    border-right: 0;
    border-bottom: 1px solid rgb(255 255 255 / 6%);
  }

  .offer-card {
    grid-template-columns: 2.6rem minmax(0, 1fr);
    padding: var(--ui-space-2);
  }

  .offer-card__icon {
    width: 2.6rem;
    height: 2.6rem;
  }

  .merchant-detail {
    min-height: 12rem;
    padding: var(--ui-space-3);
  }

  .merchant-detail__purchase {
    align-items: center;
  }

  .sell-card {
    grid-template-columns: 2.7rem minmax(0, 1fr) auto;
    gap: var(--ui-space-2);
    padding-inline: var(--ui-space-3);
  }

  .sell-card__icon {
    width: 2.7rem;
    height: 2.7rem;
  }
}

@media (max-width: 355px) {
  .merchant-shelf {
    grid-template-columns: 1fr;
  }

  .merchant-detail__purchase {
    display: grid;
    grid-template-columns: 1fr;
  }

  .merchant-detail__purchase :deep(.ui-button) {
    width: 100%;
  }

  .sell-card {
    grid-template-columns: 2.7rem minmax(0, 1fr);
  }

  .sell-card__price {
    grid-column: 1 / -1;
    grid-template-columns: 1fr auto;
    justify-items: stretch;
  }

  .sell-card__price strong {
    justify-self: end;
  }
}
</style>
