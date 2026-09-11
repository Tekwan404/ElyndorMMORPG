<script setup lang="ts">
import { onMounted, ref } from 'vue'

import type { PremiumStoreSnapshot } from '@/api/contracts'
import { itemArtUrl } from '@/assets/itemArt'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UILoadingState } from '@/ui/components'

const session = useGameSessionStore()
const store = ref<PremiumStoreSnapshot | null>(null)
const error = ref<string | null>(null)

async function load(): Promise<void> {
  error.value = null
  try { store.value = await session.getPremiumStore() } catch { error.value = 'Не удалось загрузить магазин.' }
}

async function purchase(sku: string): Promise<void> {
  const result = await session.buyPremiumStoreOffer(sku)
  if (!result) { error.value = 'Покупка не выполнена.'; return }
  await load()
}

onMounted(load)
</script>

<template>
  <section class="store-view">
    <header><small>КРИСТАЛЛЫ</small><strong>{{ store?.crystalBalance ?? 0 }}</strong></header>
    <UILoadingState v-if="!store && !error" state="loading" title="Загружаем магазин" />
    <p v-else-if="error" class="store-error">{{ error }}</p>
    <article v-for="offer in store?.offers ?? []" :key="offer.sku" class="store-offer">
      <img v-if="offer.iconId" :src="itemArtUrl(offer.iconId)" alt="" />
      <div><strong>{{ offer.name }} ×{{ offer.quantity }}</strong><small>{{ offer.description }}</small></div>
      <UIButton :disabled="!offer.canPurchase || session.mutationPending || (store?.crystalBalance ?? 0) < offer.crystalPrice" :loading="session.mutationPending" @click="purchase(offer.sku)">{{ offer.crystalPrice }} крист.</UIButton>
    </article>
  </section>
</template>

<style scoped>
.store-view{display:grid;gap:8px}.store-view>header{display:flex;justify-content:space-between;align-items:center;padding:10px;border:1px solid rgb(139 214 255 / 34%);background:linear-gradient(110deg,rgb(90 177 255 / 12%),transparent)}.store-view header small{color:#8bd6ff;font-weight:800;letter-spacing:.1em}.store-view header strong{color:#d9f1ff}.store-offer{display:grid;grid-template-columns:46px minmax(0,1fr) auto;align-items:center;gap:8px;padding:9px;border-bottom:1px solid var(--ui-color-border)}.store-offer img{width:44px;height:44px;object-fit:contain}.store-offer>div{display:grid;gap:2px}.store-offer small{color:var(--ui-color-text-muted);font-size:var(--ui-font-size-xs)}.store-offer :deep(.ui-button){min-height:var(--ui-touch-target);font-size:var(--ui-font-size-xs)}.store-error{color:var(--ui-color-danger)}
</style>
