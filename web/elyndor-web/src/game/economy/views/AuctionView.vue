<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { commerceMessage, commerceRarityLabel, createAuction, loadAuction, parseGoldInput, previewAuction, settleAuction, type AuctionFeePreview, type AuctionLot } from '@/game/economy/commerce'
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
const price = ref('')
const feePreview = ref<AuctionFeePreview | null>(null)
const loading = ref(false)
const busy = ref(false)
const error = ref('')
const notice = ref('')
const confirmingLot = ref<string | null>(null)
let latestRequest = 0
const inventory = computed(() => session.snapshot?.character?.inventory.items.filter(item =>
  !item.transactionLocked && !item.isLocked && item.bindState !== 'BOUND' && !item.equippedSlot,
) ?? [])
const visibleLots = computed(() => lots.value)

const statLabels: Record<string, string> = {
  STRENGTH: 'Сила', AGILITY: 'Ловкость', INTELLECT: 'Интеллект', STAMINA: 'Выносливость',
  ATTACK_POWER: 'Сила атаки', SPELL_POWER: 'Сила заклинаний', ARMOR: 'Броня',
  MAGIC_RESISTANCE: 'Маг. сопротивление', CRITICAL_CHANCE: 'Крит. шанс', CRITICAL_DAMAGE: 'Крит. урон',
  ACCURACY: 'Меткость', DODGE: 'Уклонение', BLOCK_CHANCE: 'Шанс блока', BLOCK_VALUE: 'Сила блока',
  ARMOR_PENETRATION: 'Пробивание брони', MAGIC_PENETRATION: 'Маг. пробивание', ATTACK_SPEED: 'Скорость атаки',
}
function statLabel(id: string): string { return statLabels[id.toUpperCase()] ?? id.replaceAll('_', ' ').toLowerCase() }
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
watch(tab, () => { ++latestRequest; loading.value = false; search.value = ''; kind.value = 'all'; confirmingLot.value = null; feePreview.value = null; void refresh() }, { immediate: true })
watch([selectedItem, price], () => { feePreview.value = null; error.value = ''; notice.value = '' })
function nextPage(): void { if (!hasMore.value || loading.value) return; void refresh(true, page.value + 1) }

async function listItem(): Promise<void> {
  if (!selectedItem.value || busy.value) return
  busy.value = true; error.value = ''; notice.value = ''
  try {
    const amount = parseGoldInput(price.value)
    if (amount <= 0) throw new RangeError('Цена должна быть больше нуля.')
    const quote = feePreview.value
    if (!quote || quote.itemId !== selectedItem.value || moneyUnits(quote.price) !== BigInt(amount)) {
      feePreview.value = await previewAuction(selectedItem.value, amount)
      return
    }
    const created = await createAuction(selectedItem.value, amount, quote)
    notice.value = `Лот выставлен. Плата за выставление: ${formatMoney(created.fee)}.`
    selectedItem.value = ''; price.value = ''; feePreview.value = null
    await session.refreshSnapshot()
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
      <label>Предмет
        <select v-model="selectedItem" required><option value="">Выберите из сумки</option><option v-for="item in inventory" :key="item.id" :value="item.id">{{ item.generatedItem?.displayName ?? item.name }}{{ item.quantity > 1 ? ` ×${item.quantity}` : '' }}</option></select>
      </label>
      <label>Цена выкупа, Gold <input v-model="price" inputmode="decimal" placeholder="Например, 12.5" required /></label>
      <div v-if="feePreview" class="auction-quote" role="status">
        <strong>Проверьте перед выставлением</strong>
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
.auction-card { display:grid; grid-template-columns:48px minmax(0,1fr) auto; gap:8px; align-items:center; padding:8px; border:1px solid var(--ui-color-border); border-radius:var(--ui-radius-md); background:var(--ui-gradient-panel); }
.auction-card :deep(.item-icon) { width:48px; height:48px; } .auction-card__body { display:grid; min-width:0; gap:3px; } .auction-card__body strong { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; } .auction-card__body small { color:var(--ui-color-text-muted); }
.auction-card__roll-summary { display:flex; flex-wrap:wrap; gap:3px 8px; } .auction-card__rolled { color:#d3c7ad !important; }
.auction-card__affixes { display:flex; flex-wrap:wrap; gap:3px 5px; } .auction-card__affixes span { padding:2px 5px; border-radius:5px; background:rgba(255,255,255,.045); color:#b9d5b4; font-size:.68rem; }
.auction-card__price { color:var(--ui-color-gold); }
.auction-card button { max-width:95px; overflow-wrap:anywhere; } .error { color:var(--ui-color-danger,#ff8d8d); } .notice { color:#a8deb1; }
@media (max-width:380px) { .auction-card { grid-template-columns:40px minmax(0,1fr); align-items:start; } .auction-card button { grid-column:2; max-width:none; } }
</style>
