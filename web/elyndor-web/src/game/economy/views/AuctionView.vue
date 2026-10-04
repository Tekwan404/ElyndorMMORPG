<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { commerceMessage, commerceRarityLabel, createAuction, loadAuction, loadAuctionSellableItems, parseMoneyInput, previewAuction, settleAuction, type AuctionFeePreview, type AuctionLot, type AuctionSellableItem } from '@/game/economy/commerce'
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
const selectedItem = ref('')
const sellableItems = ref<AuctionSellableItem[]>([])
const sellKind = ref('all')
const sellLoading = ref(false)
const priceGold = ref('0')
const priceSilver = ref('0')
const priceBronze = ref('0')
const feePreview = ref<AuctionFeePreview | null>(null)
const loading = ref(false)
const busy = ref(false)
const error = ref('')
const notice = ref('')
const confirmingLot = ref<string | null>(null)
let latestRequest = 0
const visibleLots = computed(() => lots.value)
const selectedSellableItem = computed(() => sellableItems.value.find(item => item.itemId === selectedItem.value) ?? null)
const filteredSellableItems = computed(() => sellKind.value === 'all'
  ? sellableItems.value
  : sellableItems.value.filter(item => item.type.toLowerCase() === sellKind.value.toLowerCase()))
const sellKinds = [
  ['all', 'Все'],
  ['Equipment', 'Снаряжение'],
  ['Consumable', 'Расходники'],
  ['Material', 'Материалы'],
] as const

const statLabels: Record<string, string> = {
  STRENGTH: 'Сила', AGILITY: 'Ловкость', INTELLECT: 'Интеллект', STAMINA: 'Выносливость',
  ATTACK_POWER: 'Сила атаки', SPELL_POWER: 'Сила заклинаний', ARMOR: 'Броня',
  MAGIC_RESISTANCE: 'Маг. сопротивление', CRITICAL_CHANCE: 'Крит. шанс', CRITICAL_DAMAGE: 'Крит. урон',
  ACCURACY: 'Меткость', DODGE: 'Уклонение', BLOCK_CHANCE: 'Шанс блока', BLOCK_VALUE: 'Сила блока',
  ARMOR_PENETRATION: 'Пробивание брони', MAGIC_PENETRATION: 'Маг. пробивание', ATTACK_SPEED: 'Скорость атаки',
}
function statLabel(id: string): string { return statLabels[id.toUpperCase()] ?? id.replace(/_/g, ' ').toLowerCase() }
function compactNumber(value: number): string { return Number.isInteger(value) ? String(value) : value.toFixed(2).replace(/\.?0+$/, '') }
function rolledStatsText(lot: AuctionLot): string {
  if (!lot.rolledStats) return ''
  const values = [
    ['Сила', lot.rolledStats.strength], ['Ловкость', lot.rolledStats.agility],
    ['Интеллект', lot.rolledStats.intellect], ['Выносливость', lot.rolledStats.stamina],
  ] as const
  return values.filter(([, value]) => value !== null).map(([label, value]) => `${label} ${compactNumber(value!)}`).join(' · ')
}

async function refresh(append = false, requestedPage = 0): Promise<void> {
  if (tab.value === 'sell') return
  const request = ++latestRequest
  loading.value = true; error.value = ''
  try {
    const result = await loadAuction(tab.value === 'mine', search.value, kind.value === 'all' ? '' : kind.value, requestedPage)
    if (request !== latestRequest) return
    page.value = requestedPage
    lots.value = append ? [...lots.value, ...result] : result
    hasMore.value = result.length === 50
  }
  catch (failure) { if (request === latestRequest) error.value = commerceMessage(failure instanceof Error ? failure.message : 'auction_load_failed') }
  finally { if (request === latestRequest) loading.value = false }
}
async function refreshSellableItems(): Promise<void> {
  sellLoading.value = true; error.value = ''
  try {
    sellableItems.value = await loadAuctionSellableItems()
    if (selectedItem.value && !sellableItems.value.some(item => item.itemId === selectedItem.value)) selectedItem.value = ''
  }
  catch (failure) { error.value = commerceMessage(failure instanceof Error ? failure.message : 'auction_load_failed') }
  finally { sellLoading.value = false }
}
watch(tab, () => {
  ++latestRequest
  loading.value = false
  search.value = ''
  kind.value = 'all'
  sellKind.value = 'all'
  confirmingLot.value = null
  feePreview.value = null
  if (tab.value === 'sell') void refreshSellableItems()
  else void refresh()
}, { immediate: true })
watch([selectedItem, priceGold, priceSilver, priceBronze], () => { feePreview.value = null; error.value = ''; notice.value = '' })
function nextPage(): void { if (!hasMore.value || loading.value) return; void refresh(true, page.value + 1) }

async function listItem(): Promise<void> {
  if (!selectedItem.value || busy.value) return
  busy.value = true; error.value = ''; notice.value = ''
  try {
    const amount = parseMoneyInput(priceGold.value, priceSilver.value, priceBronze.value)
    if (amount <= 0) throw new RangeError('Цена должна быть больше нуля.')
    const quote = feePreview.value
    if (!quote || quote.itemId !== selectedItem.value || moneyUnits(quote.price) !== BigInt(amount)) {
      feePreview.value = await previewAuction(selectedItem.value, amount)
      return
    }
    const created = await createAuction(selectedItem.value, amount, quote)
    notice.value = `Лот выставлен. Плата за выставление: ${formatMoney(created.fee)}.`
    selectedItem.value = ''
    priceGold.value = '0'; priceSilver.value = '0'; priceBronze.value = '0'
    feePreview.value = null
    await Promise.all([session.refreshSnapshot(), refreshSellableItems()])
    tab.value = 'mine'
  } catch (failure) {
    const code = failure instanceof Error ? failure.message : 'auction_create_failed'
    if (code === 'auction_quote_changed') feePreview.value = null
    error.value = failure instanceof RangeError ? failure.message : commerceMessage(code)
  }
  finally { busy.value = false }
}

async function settle(id: string): Promise<void> {
  if (busy.value) return
  if (tab.value === 'buy' && confirmingLot.value !== id) { confirmingLot.value = id; return }
  busy.value = true; error.value = ''; notice.value = ''
  try {
    await settleAuction(id, tab.value === 'mine' ? 'cancel' : 'buy')
    notice.value = tab.value === 'mine' ? 'Лот отменён. Предмет вернулся в инвентарь или почту.' : 'Куплено. Предмет ждёт в почте.'
    await Promise.all([refresh(), session.refreshSnapshot()])
  } catch (failure) { error.value = commerceMessage(failure instanceof Error ? failure.message : 'auction_action_failed') }
  finally { busy.value = false; confirmingLot.value = null }
}
</script>

<template>
  <section class="auction-view" data-auction-view>
    <header><small>ТОРГОВАЯ ПЛОЩАДКА</small><h2>Аукцион</h2></header>
    <nav class="auction-tabs" aria-label="Разделы аукциона">
      <button v-for="entry in ([['buy','Купить'],['sell','Продать'],['mine','Мои лоты']] as const)" :key="entry[0]" type="button" :aria-current="tab === entry[0] ? 'page' : undefined" @click="tab = entry[0]">{{ entry[1] }}</button>
    </nav>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <p v-if="notice" role="status" class="notice">{{ notice }}</p>
    <form v-if="tab === 'sell'" class="auction-form" @submit.prevent="listItem">
      <div class="auction-sell__heading">
        <strong>Выберите предмет</strong>
        <small>Надетые и недоступные для торговли предметы здесь не показываются.</small>
      </div>
      <div class="auction-sell__filters" aria-label="Категории предметов">
        <button v-for="entry in sellKinds" :key="entry[0]" type="button" :aria-pressed="sellKind === entry[0]"
          @click="sellKind = entry[0]">{{ entry[1] }}</button>
      </div>
      <p v-if="sellLoading">Загружаем доступные предметы…</p>
      <div v-else-if="filteredSellableItems.length" class="auction-item-picker" data-auction-item-picker>
        <button v-for="item in filteredSellableItems" :key="item.itemId" type="button"
          class="auction-item-picker__item" :class="{ 'is-selected': selectedItem === item.itemId }"
          :aria-pressed="selectedItem === item.itemId" :aria-label="`Выбрать: ${item.name}`"
          @click="selectedItem = item.itemId">
          <span class="auction-item-picker__icon">
            <ItemIcon :icon-id="item.iconId" :item-id="item.itemId" :name="item.name" :type="item.type" :rarity="item.rarity" />
            <b v-if="item.quantity > 1">×{{ item.quantity }}</b>
          </span>
          <span class="auction-item-picker__text">
            <strong>{{ item.name }}{{ item.enhancementLevel > 0 ? ` +${item.enhancementLevel}` : '' }}</strong>
            <small>{{ commerceRarityLabel(item.rarity) }}<template v-if="item.itemLevel !== null"> · ур. {{ item.itemLevel }}</template></small>
          </span>
        </button>
      </div>
      <p v-else class="auction-sell__empty">Нет предметов, которые сейчас можно выставить на аукцион.</p>

      <article v-if="selectedSellableItem" class="auction-selected-item">
        <ItemIcon :icon-id="selectedSellableItem.iconId" :item-id="selectedSellableItem.itemId"
          :name="selectedSellableItem.name" :type="selectedSellableItem.type" :rarity="selectedSellableItem.rarity" />
        <div>
          <strong>{{ selectedSellableItem.name }}{{ selectedSellableItem.enhancementLevel > 0 ? ` +${selectedSellableItem.enhancementLevel}` : '' }}</strong>
          <small>{{ commerceRarityLabel(selectedSellableItem.rarity) }}
            <template v-if="selectedSellableItem.itemLevel !== null"> · ур. предмета {{ selectedSellableItem.itemLevel }}</template>
            <template v-if="selectedSellableItem.itemPower !== null"> · мощность {{ compactNumber(selectedSellableItem.itemPower) }}</template>
          </small>
        </div>
      </article>

      <fieldset class="auction-price">
        <legend>Цена выкупа</legend>
        <label><span>Золото</span><input v-model="priceGold" aria-label="Золото" inputmode="numeric" pattern="[0-9]*" /></label>
        <label><span>Серебро</span><input v-model="priceSilver" aria-label="Серебро" inputmode="numeric" pattern="[0-9]*" /></label>
        <label><span>Бронза</span><input v-model="priceBronze" aria-label="Бронза" inputmode="numeric" pattern="[0-9]*" /></label>
      </fieldset>
      <div v-if="feePreview" class="auction-quote" role="status">
        <strong>Проверьте перед выставлением</strong>
        <span>Цена: {{ formatMoney(feePreview.price) }}</span>
        <span>Плата за выставление: {{ formatMoney(feePreview.fee) }} — спишется сейчас и не возвращается.</span>
        <span>Налог при продаже: {{ formatMoney(feePreview.tax) }}.</span>
        <span>После продажи вы получите: {{ formatMoney(feePreview.sellerProceeds) }}.</span>
      </div>
      <p>Лот действует 48 часов. Непроданный предмет придёт в почту.</p>
      <button type="submit" :disabled="busy || !selectedItem">{{ busy ? 'Проверяем…' : feePreview ? 'Подтвердить выставление' : 'Рассчитать комиссию' }}</button>
    </form>
    <template v-else>
      <form class="auction-search" @submit.prevent="refresh()"><input v-model="search" aria-label="Поиск предметов" placeholder="Найти предмет" /><button type="submit" :disabled="loading">Найти</button></form>
      <label class="auction-filter">Тип <select v-model="kind" @change="refresh()"><option value="all">Все</option><option value="Equipment">Снаряжение</option><option value="Material">Материалы</option><option value="Consumable">Расходники</option></select></label>
      <p v-if="loading">Загружаем лоты…</p>
      <p v-else-if="!visibleLots.length">{{ tab === 'mine' ? 'Активных лотов пока нет.' : 'По запросу нет доступных лотов.' }}</p>
      <div v-else class="auction-list">
        <article v-for="lot in visibleLots" :key="lot.id" class="auction-card">
          <ItemIcon :icon-id="lot.iconId" :item-id="lot.itemId" :name="lot.name" :type="lot.type" :rarity="lot.rarity" />
          <div class="auction-card__body">
            <strong>{{ lot.name }}{{ lot.enhancementLevel > 0 ? ` +${lot.enhancementLevel}` : '' }}</strong>
            <small>{{ commerceRarityLabel(lot.rarity) }} · ×{{ lot.quantity }} · {{ lot.sellerName }}</small>
            <small v-if="lot.itemLevel !== null || lot.itemPower !== null || lot.stars !== null" class="auction-card__roll-summary">
              <span v-if="lot.itemLevel !== null">ур. предмета {{ lot.itemLevel }}</span>
              <span v-if="lot.itemPower !== null">мощность {{ compactNumber(lot.itemPower) }}</span>
              <span v-if="lot.stars !== null">★ {{ lot.stars }}{{ lot.isPerfect ? ' · идеальный' : '' }}</span>
            </small>
            <small v-if="rolledStatsText(lot)" class="auction-card__rolled">{{ rolledStatsText(lot) }}</small>
            <div v-if="lot.affixes.length" class="auction-card__affixes">
              <span v-for="affix in lot.affixes" :key="affix.slotKey">{{ statLabel(affix.statId) }} +{{ compactNumber(affix.value) }} · T{{ affix.affixTier }}</span>
            </div>
            <span class="auction-card__price">{{ formatMoney(lot.price) }}</span>
          </div>
          <button type="button" :disabled="busy" @click="settle(lot.id)">{{ tab === 'mine' ? 'Отменить' : confirmingLot === lot.id ? 'Подтвердить' : 'Выкупить' }}</button>
        </article>
      </div>
      <button v-if="hasMore" type="button" :disabled="loading" @click="nextPage">Показать ещё</button>
    </template>
  </section>
</template>

<style scoped>
.auction-view { display:grid; gap:12px; } header small { color:var(--ui-color-gold); letter-spacing:.12em; font-size:.65rem; } h2 { margin:2px 0; font-family:var(--ui-font-display); }
.auction-tabs { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:5px; } .auction-tabs button[aria-current] { border-color:var(--ui-color-gold); color:var(--ui-color-gold); }
button,select,input { min-height:44px; padding:8px; border:1px solid var(--ui-color-border); border-radius:var(--ui-radius-sm); background:#0a0d14; color:var(--ui-color-text-primary); font:inherit; }
button { cursor:pointer; } button:disabled { opacity:.5; } .auction-search { display:grid; grid-template-columns:minmax(0,1fr) auto; gap:6px; } .auction-filter { display:flex; align-items:center; gap:8px; }
.auction-list,.auction-form { display:grid; gap:8px; } .auction-form label { display:grid; gap:5px; } .auction-form p { margin:0; color:var(--ui-color-text-muted); font-size:.8rem; }
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
.auction-item-picker__text { display:grid; min-width:0; gap:2px; }
.auction-item-picker__text strong { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; font-size:.75rem; }
.auction-item-picker__text small { color:var(--ui-color-text-muted); font-size:.65rem; }
.auction-selected-item { display:grid; grid-template-columns:52px minmax(0,1fr); gap:9px; align-items:center; padding:9px; border:1px solid color-mix(in srgb,var(--ui-color-gold) 45%,var(--ui-color-border)); border-radius:var(--ui-radius-md); background:rgba(196,157,86,.06); }
.auction-selected-item :deep(.item-icon) { width:52px; height:52px; }
.auction-selected-item div { display:grid; gap:3px; min-width:0; }
.auction-selected-item strong { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
.auction-selected-item small { color:var(--ui-color-text-muted); }
.auction-price { margin:0; padding:9px; display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:6px; border:1px solid var(--ui-color-border); border-radius:var(--ui-radius-sm); }
.auction-price legend { padding:0 5px; color:var(--ui-color-text-muted); font-size:.76rem; }
.auction-price label { gap:3px !important; }
.auction-price label span { font-size:.7rem; color:var(--ui-color-text-muted); }
.auction-price input { min-width:0; width:100%; text-align:center; font-variant-numeric:tabular-nums; }
.auction-card { display:grid; grid-template-columns:48px minmax(0,1fr) auto; gap:8px; align-items:center; padding:8px; border:1px solid var(--ui-color-border); border-radius:var(--ui-radius-md); background:var(--ui-gradient-panel); }
.auction-card :deep(.item-icon) { width:48px; height:48px; } .auction-card__body { display:grid; min-width:0; gap:3px; } .auction-card__body strong { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; } .auction-card__body small { color:var(--ui-color-text-muted); }
.auction-card__roll-summary { display:flex; flex-wrap:wrap; gap:3px 8px; } .auction-card__rolled { color:#d3c7ad !important; }
.auction-card__affixes { display:flex; flex-wrap:wrap; gap:3px 5px; } .auction-card__affixes span { padding:2px 5px; border-radius:5px; background:rgba(255,255,255,.045); color:#b9d5b4; font-size:.68rem; }
.auction-card__price { color:var(--ui-color-gold); }
.auction-card button { max-width:95px; overflow-wrap:anywhere; } .error { color:var(--ui-color-danger,#ff8d8d); } .notice { color:#a8deb1; }
@media (max-width:380px) { .auction-item-picker { grid-template-columns:1fr; } .auction-card { grid-template-columns:40px minmax(0,1fr); align-items:start; } .auction-card button { grid-column:2; max-width:none; } }
</style>
