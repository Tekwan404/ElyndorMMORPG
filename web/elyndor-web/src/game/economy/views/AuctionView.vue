<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { commerceMessage, commerceRarityLabel, createAuction, loadAuction, parseGoldInput, settleAuction, type AuctionLot } from '@/game/economy/commerce'
import { useGameSessionStore } from '@/stores/gameSession'
import { formatMoney } from '@/shared/money'
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
watch(tab, () => { ++latestRequest; loading.value = false; search.value = ''; kind.value = 'all'; confirmingLot.value = null; void refresh() }, { immediate: true })
function nextPage(): void { if (!hasMore.value || loading.value) return; void refresh(true, page.value + 1) }

async function listItem(): Promise<void> {
  if (!selectedItem.value || busy.value) return
  busy.value = true; error.value = ''; notice.value = ''
  try {
    const amount = parseGoldInput(price.value)
    if (amount <= 0) throw new RangeError('Цена должна быть больше нуля.')
    await createAuction(selectedItem.value, amount)
    notice.value = 'Лот выставлен на аукцион.'
    selectedItem.value = ''; price.value = ''
    await session.refreshSnapshot()
    tab.value = 'mine'
  } catch (failure) { error.value = failure instanceof RangeError ? failure.message : commerceMessage(failure instanceof Error ? failure.message : 'auction_create_failed') }
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
        <select v-model="selectedItem" required><option value="">Выберите из сумки</option><option v-for="item in inventory" :key="item.id" :value="item.id">{{ item.name }}{{ item.quantity > 1 ? ` ×${item.quantity}` : '' }}</option></select>
      </label>
      <label>Цена выкупа, Gold <input v-model="price" inputmode="decimal" placeholder="Например, 12.5" required /></label>
      <p>Лот действует 48 часов. Плата за выставление не возвращается; непроданное придёт в почту.</p>
      <button type="submit" :disabled="busy || !selectedItem">{{ busy ? 'Выставляем…' : 'Выставить' }}</button>
    </form>
    <template v-else>
      <form class="auction-search" @submit.prevent="refresh()"><input v-model="search" aria-label="Поиск предметов" placeholder="Найти предмет" /><button type="submit" :disabled="loading">Найти</button></form>
      <label class="auction-filter">Тип <select v-model="kind" @change="refresh()"><option value="all">Все</option><option value="Equipment">Снаряжение</option><option value="Material">Материалы</option><option value="Consumable">Расходники</option></select></label>
      <p v-if="loading">Загружаем лоты…</p>
      <p v-else-if="!visibleLots.length">{{ tab === 'mine' ? 'Активных лотов пока нет.' : 'По запросу нет доступных лотов.' }}</p>
      <div v-else class="auction-list">
        <article v-for="lot in visibleLots" :key="lot.id" class="auction-card">
          <ItemIcon :icon-id="lot.iconId" :item-id="lot.itemId" :name="lot.name" :type="lot.type" :rarity="lot.rarity" />
          <div class="auction-card__body"><strong>{{ lot.name }}</strong><small>{{ commerceRarityLabel(lot.rarity) }} · ×{{ lot.quantity }} · {{ lot.sellerName }}</small><span>{{ formatMoney(lot.price) }}</span></div>
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
.auction-card { display:grid; grid-template-columns:48px minmax(0,1fr) auto; gap:8px; align-items:center; padding:8px; border:1px solid var(--ui-color-border); border-radius:var(--ui-radius-md); background:var(--ui-gradient-panel); }
.auction-card :deep(.item-icon) { width:48px; height:48px; } .auction-card__body { display:grid; min-width:0; gap:2px; } .auction-card__body strong { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; } .auction-card__body small { color:var(--ui-color-text-muted); } .auction-card__body span { color:var(--ui-color-gold); }
.auction-card button { max-width:95px; overflow-wrap:anywhere; } .error { color:var(--ui-color-danger,#ff8d8d); } .notice { color:#a8deb1; }
@media (max-width:380px) { .auction-card { grid-template-columns:40px minmax(0,1fr); } .auction-card button { grid-column:2; max-width:none; } }
</style>
