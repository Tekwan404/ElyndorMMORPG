<script setup lang="ts">
import { computed, ref, watch } from 'vue'

import type { InventoryItem, MerchantSnapshot } from '@/api/contracts'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIModal } from '@/ui/components'

const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{ close: [] }>()
const MERCHANT_ID = 'MARCUS_SUPPLIES'
const session = useGameSessionStore()
const merchant = ref<MerchantSnapshot | null>(null)
const loading = ref(false)

const materials = computed(() => session.snapshot?.character?.inventory.items
  .filter((item) => item.type === 'Material' && !item.equippedSlot && item.sellPriceGold > 0) ?? [])

watch(() => props.open, (open) => {
  if (open) void loadMerchant()
})

async function loadMerchant(): Promise<void> {
  loading.value = true
  try {
    merchant.value = await session.getMerchant(MERCHANT_ID)
  } finally {
    loading.value = false
  }
}

async function buy(definitionId: string): Promise<void> {
  const updated = await session.buyMerchantItem(MERCHANT_ID, definitionId, 1)
  if (updated) merchant.value = updated
}

async function sell(item: InventoryItem, quantity: number): Promise<void> {
  const updated = await session.sellMerchantMaterial(MERCHANT_ID, item.id, quantity)
  if (updated) merchant.value = updated
}
</script>

<template>
  <UIModal :open="open" title="Лавка Маркуса" @close="emit('close')">
    <section class="shop">
      <header class="shop__header">
        <div>
          <small>Торговец припасами</small>
          <h2>{{ merchant?.name ?? 'Маркус' }}</h2>
          <p>{{ merchant?.description ?? 'Здесь можно купить лечебные припасы и продать добытые материалы.' }}</p>
        </div>
        <strong class="gold">● {{ merchant?.gold ?? session.snapshot?.character?.gold ?? 0 }} золота</strong>
      </header>

      <div class="shop__section">
        <div class="section-title"><strong>Купить</strong><small>Припасы для следующего похода</small></div>
        <article v-for="item in merchant?.items ?? []" :key="item.definitionId" class="trade-row">
          <span class="trade-row__icon">✚</span>
          <div class="trade-row__copy">
            <strong>{{ item.name }}</strong>
            <small>{{ item.description }}</small>
            <b v-if="item.healAmount">Восстанавливает {{ item.healAmount }} здоровья</b>
          </div>
          <div class="trade-row__action">
            <span>{{ item.buyPriceGold }} зол.</span>
            <UIButton :loading="session.mutationPending" :disabled="session.mutationPending || (merchant?.gold ?? 0) < item.buyPriceGold" @click="buy(item.definitionId)">Купить</UIButton>
          </div>
        </article>
        <p v-if="loading" class="muted">Маркус раскладывает товар…</p>
      </div>

      <div class="shop__section">
        <div class="section-title"><strong>Продать материалы</strong><small>Лишняя добыча превращается в золото</small></div>
        <article v-for="item in materials" :key="item.id" class="trade-row">
          <span class="trade-row__icon">◆</span>
          <div class="trade-row__copy">
            <strong>{{ item.name }} ×{{ item.quantity }}</strong>
            <small>{{ item.sellPriceGold }} золота за штуку</small>
          </div>
          <div class="sell-actions">
            <UIButton variant="ghost" :disabled="session.mutationPending" @click="sell(item, 1)">Продать 1</UIButton>
            <UIButton :disabled="session.mutationPending" @click="sell(item, item.quantity)">Всё · {{ item.sellPriceGold * item.quantity }}</UIButton>
          </div>
        </article>
        <p v-if="materials.length === 0" class="muted">В рюкзаке пока нет материалов для продажи.</p>
      </div>
    </section>
  </UIModal>
</template>

<style scoped>
.shop { display:grid; gap:var(--ui-space-5); }
.shop__header { display:flex; justify-content:space-between; gap:var(--ui-space-4); padding-bottom:var(--ui-space-3); border-bottom:1px solid var(--ui-color-border); }
.shop__header h2,.shop__header p { margin:0; }
.shop__header div { display:grid; gap:var(--ui-space-1); }
.shop__header small,.shop__header p,.muted,.section-title small,.trade-row__copy small { color:var(--ui-color-text-muted); }
.gold { align-self:start; white-space:nowrap; color:#e8c866; }
.shop__section { display:grid; gap:var(--ui-space-2); }
.section-title { display:grid; gap:2px; margin-bottom:var(--ui-space-1); }
.trade-row { display:grid; grid-template-columns:auto minmax(0,1fr) auto; align-items:center; gap:var(--ui-space-3); padding:var(--ui-space-3); border:1px solid var(--ui-color-border); border-radius:var(--ui-radius-md); background:var(--ui-color-surface-2); }
.trade-row__icon { display:grid; width:2.8rem; height:2.8rem; place-items:center; border:1px solid var(--ui-color-border-strong); border-radius:var(--ui-radius-md); color:var(--ui-color-primary); font-size:1.3rem; }
.trade-row__copy { display:grid; gap:2px; min-width:0; }
.trade-row__copy small { line-height:1.35; }
.trade-row__copy b { color:var(--ui-color-success); font-size:var(--ui-font-size-xs); }
.trade-row__action,.sell-actions { display:grid; justify-items:end; gap:var(--ui-space-1); }
.trade-row__action span { color:#e8c866; font-weight:700; }
@media(max-width:520px){ .shop__header{display:grid}.trade-row{grid-template-columns:auto minmax(0,1fr)}.trade-row__action,.sell-actions{grid-column:1/-1; grid-template-columns:1fr 1fr; width:100%}.trade-row__action span{grid-column:1/-1;justify-self:end} }
</style>
