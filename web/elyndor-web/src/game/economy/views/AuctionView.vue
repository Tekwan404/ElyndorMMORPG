<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import {
  commerceMessage,
  commerceRarityLabel,
  createAuctionBatch,
  loadAuction,
  loadAuctionSellableItems,
  parseMoneyInput,
  previewAuctionBatch,
  settleAuction,
  type AuctionBatchFeePreview,
  type AuctionLot,
  type AuctionSellableItem,
} from '@/game/economy/commerce'
import { useGameSessionStore } from '@/stores/gameSession'
import { formatMoney, moneyUnits } from '@/shared/money'
import ItemIcon from '@/game/items/components/ItemIcon.vue'

type Tab = 'buy' | 'sell' | 'mine'

const session = useGameSessionStore()
const tab = ref<Tab>('buy')
const lots = ref<AuctionLot[]>([])
const search = ref('')
const kind = ref('all')
const page = ref(0)
const hasMore = ref(false)
const selectedItems = ref<string[]>([])
const sellableItems = ref<AuctionSellableItem[]>([])
const sellKind = ref('all')
const sellLoading = ref(false)
const priceGold = ref('0')
const priceSilver = ref('0')
const priceBronze = ref('0')
const feePreview = ref<AuctionBatchFeePreview | null>(null)
const loading = ref(false)
const busy = ref(false)
const error = ref('')
const notice = ref('')
const confirmingLot = ref<string | null>(null)
let latestRequest = 0

const visibleLots = computed(() => lots.value)
const selectedSellableItems = computed(() => {
  const selected = new Set(selectedItems.value)
  return sellableItems.value.filter(item => selected.has(item.itemId))
})
const filteredSellableItems = computed(() =>
  sellKind.value === 'all'
    ? sellableItems.value
    : sellableItems.value.filter(item => item.type.toLowerCase() === sellKind.value.toLowerCase()),
)
const sellKinds = [
  ['all', 'Все'],
  ['Equipment', 'Снаряжение'],
  ['Consumable', 'Расходники'],
  ['Material', 'Материалы'],
] as const

const statLabels: Record<string, string> = {
  STRENGTH: 'Сила',
  AGILITY: 'Ловкость',
  INTELLECT: 'Интеллект',
  STAMINA: 'Выносливость',
  ATTACK_POWER: 'Сила атаки',
  SPELL_POWER: 'Сила заклинаний',
  ARMOR: 'Броня',
  MAGIC_RESISTANCE: 'Маг. сопротивление',
  CRITICAL_CHANCE: 'Крит. шанс',
  CRITICAL_DAMAGE: 'Крит. урон',
  ACCURACY: 'Меткость',
  DODGE: 'Уклонение',
  BLOCK_CHANCE: 'Шанс блока',
  BLOCK_VALUE: 'Сила блока',
  ARMOR_PENETRATION: 'Пробивание брони',
  MAGIC_PENETRATION: 'Маг. пробивание',
  ATTACK_SPEED: 'Скорость атаки',
}

function statLabel(id: string): string {
  return statLabels[id.toUpperCase()] ?? id.replace(/_/g, ' ').toLowerCase()
}

function compactNumber(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(2).replace(/\.?0+$/, '')
}

function listingCountLabel(count: number): string {
  const mod100 = count % 100
  const mod10 = count % 10
  if (mod100 >= 11 && mod100 <= 14) return `${count} лотов`
  if (mod10 === 1) return `${count} лот`
  if (mod10 >= 2 && mod10 <= 4) return `${count} лота`
  return `${count} лотов`
}

function rolledStatsText(lot: AuctionLot): string {
  if (!lot.rolledStats) return ''
  const values = [
    ['Сила', lot.rolledStats.strength],
    ['Ловкость', lot.rolledStats.agility],
    ['Интеллект', lot.rolledStats.intellect],
    ['Выносливость', lot.rolledStats.stamina],
  ] as const
  return values
    .filter(([, value]) => value !== null)
    .map(([label, value]) => `${label} ${compactNumber(value!)}`)
    .join(' · ')
}

function toggleSellItem(itemId: string): void {
  selectedItems.value = selectedItems.value.includes(itemId)
    ? selectedItems.value.filter(id => id !== itemId)
    : [...selectedItems.value, itemId]
}

function quoteMatchesSelection(quote: AuctionBatchFeePreview, itemIds: string[], amount: number): boolean {
  return moneyUnits(quote.pricePerItem) === BigInt(amount)
    && quote.itemIds.length === itemIds.length
    && quote.itemIds.every((id, index) => id === itemIds[index])
}

async function refresh(append = false, requestedPage = 0): Promise<void> {
  if (tab.value === 'sell') return
  const request = ++latestRequest
  loading.value = true
  error.value = ''
  try {
    const result = await loadAuction(
      tab.value === 'mine',
      search.value,
      kind.value === 'all' ? '' : kind.value,
      requestedPage,
    )
    if (request !== latestRequest) return
    page.value = requestedPage
    lots.value = append ? [...lots.value, ...result] : result
    hasMore.value = result.length === 50
  } catch (failure) {
    if (request === latestRequest) {
      error.value = commerceMessage(
        failure instanceof Error ? failure.message : 'auction_load_failed',
      )
    }
  } finally {
    if (request === latestRequest) loading.value = false
  }
}

async function refreshSellableItems(): Promise<void> {
  sellLoading.value = true
  error.value = ''
  try {
    sellableItems.value = await loadAuctionSellableItems()
    const available = new Set(sellableItems.value.map(item => item.itemId))
    selectedItems.value = selectedItems.value.filter(itemId => available.has(itemId))
  } catch (failure) {
    error.value = commerceMessage(
      failure instanceof Error ? failure.message : 'auction_load_failed',
    )
  } finally {
    sellLoading.value = false
  }
}

watch(
  tab,
  () => {
    ++latestRequest
    loading.value = false
    search.value = ''
    kind.value = 'all'
    sellKind.value = 'all'
    confirmingLot.value = null
    feePreview.value = null
    if (tab.value === 'sell') {
      selectedItems.value = []
      void refreshSellableItems()
    } else {
      void refresh()
    }
  },
  { immediate: true },
)

watch(
  [selectedItems, priceGold, priceSilver, priceBronze],
  () => {
    feePreview.value = null
    error.value = ''
  },
)

function nextPage(): void {
  if (!hasMore.value || loading.value) return
  void refresh(true, page.value + 1)
}

async function listItems(): Promise<void> {
  if (!selectedItems.value.length || busy.value) return
  busy.value = true
  error.value = ''
  notice.value = ''
  try {
    const amount = parseMoneyInput(priceGold.value, priceSilver.value, priceBronze.value)
    if (amount <= 0) throw new RangeError('Цена должна быть больше нуля.')

    const itemIds = [...selectedItems.value]
    const quote = feePreview.value
    if (!quote || !quoteMatchesSelection(quote, itemIds, amount)) {
      feePreview.value = await previewAuctionBatch(itemIds, amount)
      return
    }

    const created = await createAuctionBatch(itemIds, amount, quote)
    const listedCount = created.listings.length
    selectedItems.value = []
    priceGold.value = '0'
    priceSilver.value = '0'
    priceBronze.value = '0'
    feePreview.value = null
    await Promise.all([session.refreshSnapshot(), refreshSellableItems()])
    tab.value = 'mine'
    notice.value = `Выставлено ${listingCountLabel(listedCount)} по одной цене. Общая плата: ${formatMoney(created.totalFee)}.`
  } catch (failure) {
    const code = failure instanceof Error ? failure.message : 'auction_create_failed'
    if (code === 'auction_quote_changed') feePreview.value = null
    error.value = failure instanceof RangeError ? failure.message : commerceMessage(code)
  } finally {
    busy.value = false
  }
}

async function settle(id: string): Promise<void> {
  if (busy.value) return
  if (tab.value === 'buy' && confirmingLot.value !== id) {
    confirmingLot.value = id
    return
  }

  busy.value = true
  error.value = ''
  notice.value = ''
  try {
    await settleAuction(id, tab.value === 'mine' ? 'cancel' : 'buy')
    notice.value =
      tab.value === 'mine'
        ? 'Лот отменён. Предмет вернулся в инвентарь или почту.'
        : 'Куплено. Предмет ждёт в почте.'
    await Promise.all([refresh(), session.refreshSnapshot()])
  } catch (failure) {
    error.value = commerceMessage(
      failure instanceof Error ? failure.message : 'auction_action_failed',
    )
  } finally {
    busy.value = false
    confirmingLot.value = null
  }
}
</script>

<template>
  <section class="auction-view" data-auction-view>
    <header><small>ТОРГОВАЯ ПЛОЩАДКА</small><h2>Аукцион</h2></header>

    <nav class="auction-tabs" aria-label="Разделы аукциона">
      <button
        v-for="entry in ([['buy', 'Купить'], ['sell', 'Продать'], ['mine', 'Мои лоты']] as const)"
        :key="entry[0]"
        type="button"
        :aria-current="tab === entry[0] ? 'page' : undefined"
        @click="tab = entry[0]"
      >
        {{ entry[1] }}
      </button>
    </nav>

    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <p v-if="notice" role="status" class="notice">{{ notice }}</p>

    <form v-if="tab === 'sell'" class="auction-form" @submit.prevent="listItems">
      <div class="auction-sell__heading">
        <strong>Выберите предметы</strong>
        <small>Можно выбрать несколько разных вещей и выставить каждую отдельным лотом по одной цене.</small>
        <small>Надетые и недоступные для торговли предметы здесь не показываются.</small>
      </div>

      <div class="auction-sell__filters" aria-label="Категории предметов">
        <button
          v-for="entry in sellKinds"
          :key="entry[0]"
          type="button"
          :aria-pressed="sellKind === entry[0]"
          @click="sellKind = entry[0]"
        >
          {{ entry[1] }}
        </button>
      </div>

      <p v-if="sellLoading">Загружаем доступные предметы…</p>
      <div
        v-else-if="filteredSellableItems.length"
        class="auction-item-picker"
        data-auction-item-picker
      >
        <button
          v-for="item in filteredSellableItems"
          :key="item.itemId"
          type="button"
          class="auction-item-picker__item"
          :class="{ 'is-selected': selectedItems.includes(item.itemId) }"
          :aria-pressed="selectedItems.includes(item.itemId)"
          :aria-label="`${selectedItems.includes(item.itemId) ? 'Убрать' : 'Выбрать'}: ${item.name}`"
          @click="toggleSellItem(item.itemId)"
        >
          <span class="auction-item-picker__icon">
            <ItemIcon
              :icon-id="item.iconId"
              :item-id="item.itemId"
              :name="item.name"
              :type="item.type"
              :rarity="item.rarity"
            />
            <b v-if="item.quantity > 1">×{{ item.quantity }}</b>
            <i v-if="selectedItems.includes(item.itemId)" aria-hidden="true">✓</i>
          </span>
          <span class="auction-item-picker__text">
            <strong>
              {{ item.name }}{{ item.enhancementLevel > 0 ? ` +${item.enhancementLevel}` : '' }}
            </strong>
            <small>
              {{ commerceRarityLabel(item.rarity)
              }}<template v-if="item.itemLevel !== null"> · ур. {{ item.itemLevel }}</template>
            </small>
          </span>
        </button>
      </div>
      <p v-else class="auction-sell__empty">
        Нет предметов, которые сейчас можно выставить на аукцион.
      </p>

      <div v-if="selectedSellableItems.length" class="auction-selected-items" data-auction-selected-items>
        <div class="auction-selected-items__heading">
          <strong>Выбрано: {{ selectedSellableItems.length }}</strong>
          <button type="button" @click="selectedItems = []">Снять выбор</button>
        </div>
        <div class="auction-selected-items__list">
          <span v-for="item in selectedSellableItems" :key="item.itemId">
            {{ item.name }}{{ item.enhancementLevel > 0 ? ` +${item.enhancementLevel}` : '' }}
          </span>
        </div>
      </div>

      <fieldset class="auction-price">
        <legend>Цена каждого лота</legend>
        <label>
          <span>Золото</span>
          <input v-model="priceGold" aria-label="Золото" inputmode="numeric" pattern="[0-9]*" />
        </label>
        <label>
          <span>Серебро</span>
          <input v-model="priceSilver" aria-label="Серебро" inputmode="numeric" pattern="[0-9]*" />
        </label>
        <label>
          <span>Бронза</span>
          <input v-model="priceBronze" aria-label="Бронза" inputmode="numeric" pattern="[0-9]*" />
        </label>
      </fieldset>

      <div v-if="feePreview" class="auction-quote" role="status">
        <strong>Проверьте перед выставлением</strong>
        <span>{{ listingCountLabel(feePreview.itemIds.length) }}</span>
        <span>Цена каждого: {{ formatMoney(feePreview.pricePerItem) }}</span>
        <span>Плата за один лот: {{ formatMoney(feePreview.feePerItem) }}</span>
        <span>Общая плата сейчас: {{ formatMoney(feePreview.totalFee) }}</span>
        <span>Налог с одного проданного лота: {{ formatMoney(feePreview.taxPerItem) }}</span>
        <span>Если продадутся все: {{ formatMoney(feePreview.totalSellerProceeds) }}</span>
      </div>

      <p>Каждый лот действует 48 часов. Непроданные предметы придут в почту.</p>
      <button type="submit" :disabled="busy || selectedItems.length === 0">
        {{
          busy
            ? 'Проверяем…'
            : feePreview
              ? `Выставить ${listingCountLabel(selectedItems.length)}`
              : 'Рассчитать комиссию'
        }}
      </button>
    </form>

    <template v-else>
      <form class="auction-search" @submit.prevent="refresh()">
        <input v-model="search" aria-label="Поиск предметов" placeholder="Найти предмет" />
        <button type="submit" :disabled="loading">Найти</button>
      </form>

      <label class="auction-filter">
        Тип
        <select v-model="kind" @change="refresh()">
          <option value="all">Все</option>
          <option value="Equipment">Снаряжение</option>
          <option value="Material">Материалы</option>
          <option value="Consumable">Расходники</option>
        </select>
      </label>

      <p v-if="loading">Загружаем лоты…</p>
      <p v-else-if="!visibleLots.length">
        {{ tab === 'mine' ? 'Активных лотов пока нет.' : 'По запросу нет доступных лотов.' }}
      </p>

      <div v-else class="auction-list">
        <article v-for="lot in visibleLots" :key="lot.id" class="auction-card">
          <ItemIcon
            :icon-id="lot.iconId"
            :item-id="lot.itemId"
            :name="lot.name"
            :type="lot.type"
            :rarity="lot.rarity"
          />
          <div class="auction-card__body">
            <strong>
              {{ lot.name }}{{ lot.enhancementLevel > 0 ? ` +${lot.enhancementLevel}` : '' }}
            </strong>
            <small>
              {{ commerceRarityLabel(lot.rarity) }} · ×{{ lot.quantity }} · {{ lot.sellerName }}
            </small>
            <small
              v-if="lot.itemLevel !== null || lot.itemPower !== null || lot.stars !== null"
              class="auction-card__roll-summary"
            >
              <span v-if="lot.itemLevel !== null">ур. предмета {{ lot.itemLevel }}</span>
              <span v-if="lot.itemPower !== null">мощность {{ compactNumber(lot.itemPower) }}</span>
              <span v-if="lot.stars !== null">
                ★ {{ lot.stars }}{{ lot.isPerfect ? ' · идеальный' : '' }}
              </span>
            </small>
            <small v-if="rolledStatsText(lot)" class="auction-card__rolled">
              {{ rolledStatsText(lot) }}
            </small>
            <div v-if="lot.affixes.length" class="auction-card__affixes">
              <span v-for="affix in lot.affixes" :key="affix.slotKey">
                {{ statLabel(affix.statId) }} +{{ compactNumber(affix.value) }}
                · T{{ affix.affixTier }} · {{ compactNumber(affix.rollQuality) }}%
              </span>
            </div>
            <span class="auction-card__price">{{ formatMoney(lot.price) }}</span>
          </div>
          <button type="button" :disabled="busy" @click="settle(lot.id)">
            {{ tab === 'mine' ? 'Отменить' : confirmingLot === lot.id ? 'Подтвердить' : 'Выкупить' }}
          </button>
        </article>
      </div>

      <button v-if="hasMore" type="button" :disabled="loading" @click="nextPage">
        Показать ещё
      </button>
    </template>
  </section>
</template>

<style scoped>
.auction-view { display:grid; gap:12px; }
header small { color:var(--ui-color-gold); letter-spacing:.12em; font-size:.65rem; }
h2 { margin:2px 0; font-family:var(--ui-font-display); }
.auction-tabs { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:5px; }
.auction-tabs button[aria-current] { border-color:var(--ui-color-gold); color:var(--ui-color-gold); }
button,select,input { min-height:44px; padding:8px; border:1px solid var(--ui-color-border); border-radius:var(--ui-radius-sm); background:#0a0d14; color:var(--ui-color-text-primary); font:inherit; }
button { cursor:pointer; }
button:disabled { opacity:.5; }
.auction-search { display:grid; grid-template-columns:minmax(0,1fr) auto; gap:6px; }
.auction-filter { display:flex; align-items:center; gap:8px; }
.auction-list,.auction-form { display:grid; gap:8px; }
.auction-form label { display:grid; gap:5px; }
.auction-form p { margin:0; color:var(--ui-color-text-muted); font-size:.8rem; }
.auction-quote { display:grid; gap:4px; padding:10px; border:1px solid color-mix(in srgb, var(--ui-color-gold) 55%, var(--ui-color-border)); border-radius:var(--ui-radius-sm); background:rgba(196,157,86,.08); font-size:.8rem; }
.auction-quote strong { color:var(--ui-color-gold); }
.auction-sell__heading { display:grid; gap:2px; }
.auction-sell__heading small,.auction-sell__empty { color:var(--ui-color-text-muted); font-size:.78rem; }
.auction-sell__filters { display:flex; gap:5px; overflow-x:auto; padding-bottom:2px; }
.auction-sell__filters button { min-height:36px; white-space:nowrap; padding:6px 9px; font-size:.75rem; }
.auction-sell__filters button[aria-pressed="true"] { border-color:var(--ui-color-gold); color:var(--ui-color-gold); }
.auction-item-picker { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:6px; max-height:290px; overflow:auto; }
.auction-item-picker__item { min-height:64px; display:grid; grid-template-columns:46px minmax(0,1fr); gap:7px; align-items:center; padding:6px; text-align:left; }
.auction-item-picker__item.is-selected { border-color:var(--ui-color-gold); background:rgba(196,157,86,.1); box-shadow:inset 0 0 0 1px rgba(196,157,86,.25); }
.auction-item-picker__icon { position:relative; width:46px; height:46px; }
.auction-item-picker__icon :deep(.item-icon) { width:46px; height:46px; }
.auction-item-picker__icon b { position:absolute; right:-2px; bottom:-2px; padding:1px 4px; border-radius:5px; background:#080a0e; font-size:.65rem; }
.auction-item-picker__icon i { position:absolute; left:-3px; top:-3px; display:grid; width:18px; height:18px; place-items:center; border-radius:50%; background:var(--ui-color-gold); color:#111; font-size:.7rem; font-style:normal; font-weight:800; }
.auction-item-picker__text { display:grid; min-width:0; gap:2px; }
.auction-item-picker__text strong { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; font-size:.75rem; }
.auction-item-picker__text small { color:var(--ui-color-text-muted); font-size:.65rem; }
.auction-selected-items { display:grid; gap:7px; padding:9px; border:1px solid color-mix(in srgb,var(--ui-color-gold) 45%,var(--ui-color-border)); border-radius:var(--ui-radius-md); background:rgba(196,157,86,.06); }
.auction-selected-items__heading { display:flex; align-items:center; justify-content:space-between; gap:8px; }
.auction-selected-items__heading button { min-height:30px; padding:4px 7px; font-size:.7rem; }
.auction-selected-items__list { display:flex; flex-wrap:wrap; gap:5px; }
.auction-selected-items__list span { max-width:100%; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; padding:3px 6px; border-radius:6px; background:rgba(255,255,255,.05); color:var(--ui-color-text-secondary); font-size:.7rem; }
.auction-price { margin:0; padding:9px; display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:6px; border:1px solid var(--ui-color-border); border-radius:var(--ui-radius-sm); }
.auction-price legend { padding:0 5px; color:var(--ui-color-text-muted); font-size:.76rem; }
.auction-price label { gap:3px !important; }
.auction-price label span { font-size:.7rem; color:var(--ui-color-text-muted); }
.auction-price input { min-width:0; width:100%; text-align:center; font-variant-numeric:tabular-nums; }
.auction-card { display:grid; grid-template-columns:48px minmax(0,1fr) auto; gap:8px; align-items:center; padding:8px; border:1px solid var(--ui-color-border); border-radius:var(--ui-radius-md); background:var(--ui-gradient-panel); }
.auction-card :deep(.item-icon) { width:48px; height:48px; }
.auction-card__body { display:grid; min-width:0; gap:3px; }
.auction-card__body strong { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
.auction-card__body small { color:var(--ui-color-text-muted); }
.auction-card__roll-summary { display:flex; flex-wrap:wrap; gap:3px 8px; }
.auction-card__rolled { color:#d3c7ad !important; }
.auction-card__affixes { display:flex; flex-wrap:wrap; gap:3px 5px; }
.auction-card__affixes span { padding:2px 5px; border-radius:5px; background:rgba(255,255,255,.045); color:#b9d5b4; font-size:.68rem; }
.auction-card__price { color:var(--ui-color-gold); }
.auction-card button { max-width:95px; overflow-wrap:anywhere; }
.error { color:var(--ui-color-danger,#ff8d8d); }
.notice { color:#a8deb1; }

@media (max-width:380px) {
  .auction-item-picker { grid-template-columns:1fr; }
  .auction-card { grid-template-columns:40px minmax(0,1fr); align-items:start; }
  .auction-card button { grid-column:2; max-width:none; }
}
</style>
