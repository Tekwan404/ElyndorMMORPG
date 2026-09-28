<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useTradeStore } from '@/game/economy/tradeStore'
import { formatGoldInput, parseGoldInput } from '@/game/economy/commerce'
import { useGameSessionStore } from '@/stores/gameSession'
import { formatMoney } from '@/shared/money'
import ItemIcon from '@/game/items/components/ItemIcon.vue'

const trade = useTradeStore()
const session = useGameSessionStore()
const selected = ref<string[]>([])
const goldInput = ref('0')
const localError = ref('')
const inventory = computed(() => session.snapshot?.character?.inventory.items ?? [])
const offered = computed(() => trade.items.filter(item => trade.otherItems.includes(item.id)))
const available = computed(() => inventory.value.filter(item =>
  (!item.transactionLocked || trade.ownItems.includes(item.id))
  && !item.isLocked && item.bindState !== 'BOUND' && !item.equippedSlot,
))
watch([() => trade.current?.id, () => trade.current?.revision], () => {
  selected.value = [...trade.ownItems]
  goldInput.value = formatGoldInput(trade.ownGold)
  localError.value = ''
}, { immediate: true })
watch(() => trade.current?.state, state => {
  if (state === 'COMPLETED') void session.refreshSnapshot()
})
function toggle(id: string): void {
  selected.value = selected.value.includes(id) ? selected.value.filter(x => x !== id) : [...selected.value, id]
}
async function saveOffer(): Promise<void> {
  try {
    localError.value = ''
    await trade.offer(selected.value, parseGoldInput(goldInput.value))
  } catch (error) { localError.value = error instanceof Error ? error.message : 'Неверная сумма' }
}
</script>

<template>
  <section class="trade-panel" data-trade-panel>
    <h2>Обмен</h2>
    <p v-if="trade.error || localError" role="alert" class="trade-error">{{ localError || trade.error }}</p>
    <p v-if="!trade.current">Нет активного запроса на обмен.</p>
    <template v-else>
      <p v-if="trade.current.state === 'COMPLETED'">Обмен завершён. Предметы и золото обновлены.</p>
      <p v-else-if="trade.current.state !== 'OPEN'">Обмен отменён.</p>
      <template v-else>
        <p v-if="!trade.joined">Игрок предлагает обмен. Подтвердите участие, чтобы составить предложение.</p>
        <button v-if="!trade.joined" type="button" :disabled="trade.pending || !trade.connected" @click="trade.join">Принять запрос</button>
        <button v-if="!trade.joined" type="button" :disabled="trade.pending || !trade.connected" @click="trade.decline">Отклонить</button>
        <div class="trade-columns">
          <section>
            <h3>Вы <span v-if="trade.ownLocked">· зафиксировано</span></h3>
            <p>Золото: {{ formatMoney(trade.ownGold) }}</p>
            <ul><li v-for="id in trade.ownItems" :key="id">{{ trade.items.find(x => x.id === id)?.name ?? id }}</li></ul>
          </section>
          <section>
            <h3>Игрок <span v-if="trade.bothLocked">· зафиксировано</span></h3>
            <p>Золото: {{ formatMoney(trade.otherGold) }}</p>
            <ul><li v-for="item in offered" :key="item.id" class="trade-item">
              <ItemIcon :icon-id="item.iconId" :item-id="item.id" :name="item.name" :type="item.type" :rarity="item.rarity" />
              {{ item.name }} <small v-if="item.quantity > 1">×{{ item.quantity }}</small>
            </li></ul>
          </section>
        </div>
        <template v-if="trade.joined">
          <h3>Предложить предметы</h3>
          <div class="trade-items">
            <label v-for="item in available" :key="item.id" class="trade-choice">
              <input type="checkbox" :checked="selected.includes(item.id)" :disabled="trade.pending" @change="toggle(item.id)" />
              <ItemIcon :icon-id="item.iconId" :item-id="item.id" :name="item.name" :type="item.type" :rarity="item.rarity" />
              <span>{{ item.name }}</span>
            </label>
          </div>
          <label class="trade-gold">Золото <input v-model="goldInput" inputmode="decimal" autocomplete="off" :disabled="trade.pending" aria-label="Золото для обмена" /></label>
          <p class="trade-hint">Изменение предложения снимает фиксацию и подтверждения обоих игроков.</p>
          <div class="trade-actions">
            <button type="button" :disabled="trade.pending" @click="saveOffer">Обновить предложение</button>
            <button type="button" :disabled="trade.pending || trade.ownLocked" @click="trade.lock">Зафиксировать</button>
            <button type="button" :disabled="trade.pending || !trade.bothLocked || trade.ownConfirmed" @click="trade.confirm">Подтвердить</button>
            <button type="button" class="trade-cancel" :disabled="trade.pending" @click="trade.cancel">Отмена</button>
          </div>
          <p v-if="trade.ownConfirmed">Ожидаем подтверждения второго игрока.</p>
        </template>
      </template>
    </template>
  </section>
</template>

<style scoped>
.trade-panel { display:grid; gap:12px; padding:14px; border:1px solid var(--ui-color-border); border-radius:var(--ui-radius-md); background:var(--ui-gradient-panel); }
h2,h3,p,ul { margin:0; } h2,h3 { font-family:var(--ui-font-display); } h3 { font-size:1rem; } h3 span,.trade-hint { color:var(--ui-color-text-muted); font-size:.75rem; }
.trade-columns { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:8px; }
.trade-columns section { min-width:0; padding:10px; border:1px solid var(--ui-color-border); border-radius:var(--ui-radius-md); background:rgb(2 5 10 / 50%); }
.trade-columns ul { padding-left:14px; } .trade-item,.trade-choice { display:flex; align-items:center; gap:7px; min-width:0; }
.trade-item :deep(.item-icon),.trade-choice :deep(.item-icon) { width:36px; height:36px; flex:none; }
.trade-items { display:grid; gap:5px; max-height:190px; overflow:auto; }
.trade-choice { min-height:44px; padding:4px 8px; border:1px solid var(--ui-color-border); border-radius:var(--ui-radius-sm); }
.trade-choice span { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
.trade-gold { display:grid; gap:4px; } .trade-gold input { width:100%; min-height:44px; padding:8px; border:1px solid var(--ui-color-border); border-radius:var(--ui-radius-sm); background:#090c13; color:inherit; font:inherit; }
.trade-actions { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:8px; }
button { min-height:44px; padding:7px; border:1px solid var(--ui-color-gold-muted); border-radius:var(--ui-radius-sm); background:#171713; color:var(--ui-color-text-primary); font:inherit; }
button:disabled { opacity:.45; } .trade-cancel { border-color:var(--ui-color-border); } .trade-error { color:var(--ui-color-danger,#ff8d8d); overflow-wrap:anywhere; }
</style>
