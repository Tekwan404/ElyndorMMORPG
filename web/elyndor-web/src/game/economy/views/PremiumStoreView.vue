<script setup lang="ts">
import { onMounted, ref } from 'vue'

import type { PremiumStoreSnapshot } from '@/api/contracts'
import { itemArtUrl } from '@/assets/itemArt'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UILoadingState } from '@/ui/components'

const session = useGameSessionStore()
const store = ref<PremiumStoreSnapshot | null>(null)
const error = ref<string | null>(null)
const promoCode = ref('')

async function load(): Promise<void> {
  error.value = null
  try { store.value = await session.getPremiumStore() } catch { error.value = 'Не удалось загрузить магазин.' }
}

async function purchase(sku: string): Promise<void> {
  const result = await session.buyPremiumStoreOffer(sku)
  if (!result) { error.value = 'Покупка не выполнена.'; return }
  await load()
}

async function redeem(): Promise<void> {
  if (!promoCode.value.trim()) return
  const result = await session.redeemPromoCode(promoCode.value)
  if (!result) { error.value = 'Промокод не удалось применить.'; return }
  promoCode.value = ''
  await load()
}

onMounted(load)
</script>

<template>
  <section class="store-view">
    <header><small>КРИСТАЛЛЫ</small><strong>{{ store?.crystalBalance ?? 0 }}</strong></header>
    <UILoadingState v-if="!store && !error" state="loading" title="Загружаем магазин" />
    <p v-else-if="error" class="store-error">{{ error }}</p>
    <form class="promo-form" @submit.prevent="redeem">
      <label for="promo-code">Промокод</label>
      <input id="promo-code" v-model="promoCode" autocomplete="off" maxlength="64" placeholder="ВВЕДИТЕ КОД" :disabled="session.mutationPending" />
      <UIButton type="submit" variant="secondary" :disabled="!promoCode.trim() || session.mutationPending" :loading="session.mutationPending">Применить</UIButton>
    </form>
    <article v-for="offer in store?.offers ?? []" :key="offer.sku" class="store-offer">
      <img v-if="offer.iconId" :src="itemArtUrl(offer.iconId)" alt="" />
      <div><strong>{{ offer.name }} ×{{ offer.quantity }}</strong><small>{{ offer.description }}</small></div>
      <UIButton :disabled="!offer.canPurchase || session.mutationPending || (store?.crystalBalance ?? 0) < offer.crystalPrice" :loading="session.mutationPending" @click="purchase(offer.sku)">{{ offer.crystalPrice }} крист.</UIButton>
    </article>
  </section>
</template>

<style scoped>
.store-view{display:grid;gap:8px}.store-view>header{display:flex;justify-content:space-between;align-items:center;padding:10px;border:1px solid rgb(139 214 255 / 34%);background:linear-gradient(110deg,rgb(90 177 255 / 12%),transparent)}.store-view header small{color:#8bd6ff;font-weight:800;letter-spacing:.1em}.store-view header strong{color:#d9f1ff}.promo-form{display:grid;grid-template-columns:minmax(0,1fr) auto;gap:6px;align-items:end;padding:9px;border:1px solid var(--ui-color-border)}.promo-form label{grid-column:1/-1;color:var(--ui-color-text-muted);font-size:var(--ui-font-size-xs)}.promo-form input{min-width:0;min-height:var(--ui-touch-target);padding:0 10px;border:1px solid var(--ui-color-border);background:rgb(0 0 0 / 18%);color:var(--ui-color-text-primary);font:inherit}.store-offer{display:grid;grid-template-columns:46px minmax(0,1fr) auto;align-items:center;gap:8px;padding:9px;border-bottom:1px solid var(--ui-color-border)}.store-offer img{width:44px;height:44px;object-fit:contain}.store-offer>div{display:grid;gap:2px}.store-offer small{color:var(--ui-color-text-muted);font-size:var(--ui-font-size-xs)}.store-offer :deep(.ui-button),.promo-form :deep(.ui-button){min-height:var(--ui-touch-target);font-size:var(--ui-font-size-xs)}.store-error{color:var(--ui-color-danger)}
</style>
