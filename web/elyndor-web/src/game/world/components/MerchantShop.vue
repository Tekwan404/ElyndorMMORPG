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
const activeTab = ref<'buy' | 'sell'>('buy')

const materials = computed(() => session.snapshot?.character?.inventory.items
  .filter((item) => item.type === 'Material' && !item.equippedSlot && item.sellPriceGold > 0) ?? [])

watch(() => props.open, (open) => {
  if (open) {
    activeTab.value = 'buy'
    void loadMerchant()
  }
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
  <UIModal :open="open" title="Торговец" @close="emit('close')">
    <section class="merchant">
      <header class="merchant__npc">
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
        <button type="button" :class="{ active: activeTab === 'buy' }" @click="activeTab = 'buy'">Купить</button>
        <button type="button" :class="{ active: activeTab === 'sell' }" @click="activeTab = 'sell'">Продать</button>
        <button type="button" disabled title="Будет добавлено вместе с buyback-системой">Выкуп</button>
      </nav>

      <section v-if="activeTab === 'buy'" class="merchant-panel">
        <header class="merchant-panel__heading">
          <div>
            <small>Витрина</small>
            <strong>Припасы для следующего похода</strong>
          </div>
          <span>{{ merchant?.items.length ?? 0 }} поз.</span>
        </header>

        <article v-for="item in merchant?.items ?? []" :key="item.definitionId" class="trade-row">
          <span class="trade-row__icon">✚</span>
          <div class="trade-row__copy">
            <strong>{{ item.name }}</strong>
            <small>{{ item.description }}</small>
            <b v-if="item.healAmount">+{{ item.healAmount }} здоровья</b>
          </div>
          <div class="trade-row__action">
            <span>● {{ item.buyPriceGold }}</span>
            <UIButton
              :loading="session.mutationPending"
              :disabled="session.mutationPending || (merchant?.gold ?? 0) < item.buyPriceGold"
              @click="buy(item.definitionId)"
            >
              Купить
            </UIButton>
          </div>
        </article>
        <p v-if="loading" class="muted">Маркус раскладывает товар…</p>
      </section>

      <section v-else class="merchant-panel">
        <header class="merchant-panel__heading">
          <div>
            <small>Продажа</small>
            <strong>Материалы из вашего рюкзака</strong>
          </div>
          <span>{{ materials.length }} поз.</span>
        </header>

        <article v-for="item in materials" :key="item.id" class="trade-row">
          <span class="trade-row__icon trade-row__icon--material">◆</span>
          <div class="trade-row__copy">
            <strong>{{ item.name }} ×{{ item.quantity }}</strong>
            <small>Цена за штуку</small>
            <b>● {{ item.sellPriceGold }}</b>
          </div>
          <div class="sell-actions">
            <UIButton variant="ghost" :disabled="session.mutationPending" @click="sell(item, 1)">1 шт.</UIButton>
            <UIButton :disabled="session.mutationPending" @click="sell(item, item.quantity)">Всё · {{ item.sellPriceGold * item.quantity }}</UIButton>
          </div>
        </article>
        <p v-if="materials.length === 0" class="muted">В рюкзаке пока нет материалов, которые Маркус готов купить.</p>
      </section>
    </section>
  </UIModal>
</template>

<style scoped>
.merchant {
  display: grid;
  gap: 0;
  overflow: hidden;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-lg);
  background:
    radial-gradient(circle at 12% 0, rgb(146 136 255 / 9%), transparent 12rem),
    linear-gradient(180deg, rgb(13 18 30 / 92%), rgb(7 10 17 / 96%));
}

.merchant__npc {
  display: grid;
  grid-template-columns: 4.5rem minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--ui-space-3);
  padding: var(--ui-space-4);
  border-bottom: 1px solid var(--ui-color-border);
}

.merchant__portrait {
  display: grid;
  width: 4.5rem;
  height: 4.5rem;
  place-items: center;
  border: 1px solid color-mix(in srgb, var(--ui-color-primary) 45%, var(--ui-color-border));
  border-radius: 50%;
  background:
    radial-gradient(circle at 35% 25%, rgb(255 255 255 / 10%), transparent 34%),
    linear-gradient(180deg, rgb(36 37 61 / 92%), rgb(11 15 25 / 98%));
  color: #d6d2ff;
  font-family: var(--ui-font-display);
  font-size: 2rem;
  box-shadow: 0 0 0 3px rgb(146 136 255 / 6%);
}

.merchant__identity {
  display: grid;
  min-width: 0;
  gap: 2px;
}

.merchant__identity small,
.merchant__wallet small,
.merchant-panel__heading small {
  color: var(--ui-color-text-muted);
  font-size: .56rem;
  font-weight: 700;
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
  color: var(--ui-color-text-muted);
  font-size: .7rem;
  line-height: 1.35;
}

.merchant__wallet {
  display: grid;
  justify-items: end;
  gap: 3px;
  padding: 7px 9px;
  border: 1px solid rgb(232 200 102 / 16%);
  border-radius: var(--ui-radius-md);
  background: rgb(232 200 102 / 4%);
  white-space: nowrap;
}

.merchant__wallet strong {
  color: var(--ui-color-gold);
  font-size: var(--ui-font-size-sm);
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
  min-height: 2.8rem;
  border: 0;
  border-right: 1px solid rgb(255 255 255 / 5%);
  background: transparent;
  color: var(--ui-color-text-muted);
  font: inherit;
  font-size: .7rem;
  font-weight: 700;
  text-transform: uppercase;
}

.merchant-tabs button:last-child {
  border-right: 0;
}

.merchant-tabs button.active {
  background: linear-gradient(180deg, rgb(146 136 255 / 8%), transparent);
  color: #d5d2ff;
}

.merchant-tabs button.active::after {
  position: absolute;
  right: 22%;
  bottom: -1px;
  left: 22%;
  height: 2px;
  border-radius: var(--ui-radius-round);
  background: var(--ui-color-primary);
  box-shadow: 0 0 8px rgb(146 136 255 / 42%);
  content: '';
}

.merchant-tabs button:disabled {
  opacity: .28;
}

.merchant-panel {
  display: grid;
  gap: 0;
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
  font-size: .62rem;
}

.trade-row {
  display: grid;
  grid-template-columns: 3.2rem minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--ui-space-3);
  padding: var(--ui-space-3) var(--ui-space-4);
  border-bottom: 1px solid rgb(255 255 255 / 6%);
  background: linear-gradient(90deg, rgb(255 255 255 / 1.2%), transparent 68%);
}

.trade-row:last-of-type {
  border-bottom: 0;
}

.trade-row__icon {
  display: grid;
  width: 3.2rem;
  height: 3.2rem;
  place-items: center;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: var(--ui-radius-md);
  background: var(--ui-color-surface-2);
  color: var(--ui-color-primary);
  font-size: 1.35rem;
}

.trade-row__icon--material {
  color: var(--ui-color-secondary);
}

.trade-row__copy {
  display: grid;
  min-width: 0;
  gap: 2px;
}

.trade-row__copy strong {
  overflow: hidden;
  font-size: var(--ui-font-size-sm);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.trade-row__copy small {
  color: var(--ui-color-text-muted);
  font-size: .67rem;
  line-height: 1.35;
}

.trade-row__copy b {
  color: var(--ui-color-success);
  font-size: .65rem;
}

.trade-row__action,
.sell-actions {
  display: grid;
  justify-items: end;
  gap: 5px;
}

.trade-row__action span {
  color: var(--ui-color-gold);
  font-size: .72rem;
  font-weight: 700;
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

  .trade-row {
    grid-template-columns: 2.8rem minmax(0, 1fr);
    gap: var(--ui-space-2);
    padding-inline: var(--ui-space-3);
  }

  .trade-row__icon {
    width: 2.8rem;
    height: 2.8rem;
  }

  .trade-row__action,
  .sell-actions {
    grid-column: 1 / -1;
    grid-template-columns: 1fr 1fr;
    width: 100%;
  }

  .trade-row__action span {
    grid-column: 1 / -1;
    justify-self: end;
  }

  .trade-row__action :deep(.ui-button) {
    grid-column: 1 / -1;
    width: 100%;
  }

  .sell-actions :deep(.ui-button) {
    width: 100%;
  }
}
</style>
