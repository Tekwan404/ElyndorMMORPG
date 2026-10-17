<script setup lang="ts">
import MerchantPortrait from '@/game/world/components/MerchantPortrait.vue'
import crystalArt from '@/assets/store/ether-crystal.webp'
import { MARKET_MERCHANTS } from '@/game/world/marketMerchants'
import { marketMerchant } from '@/game/world/marketMerchants'
import { computed } from 'vue'
import { useGameSessionStore } from '@/stores/gameSession'

const session = useGameSessionStore()
const merchants = computed(() =>
  MARKET_MERCHANTS.map((merchant) =>
    marketMerchant(
      session.snapshot?.character?.raceId === 'UNDEAD' ? 'UNDEAD_' + merchant.id : merchant.id,
    ),
  ),
)

const emit = defineEmits<{
  'open-merchant': [merchantId: string]
  'open-honor': []
  'open-store': []
}>()
</script>

<template>
  <section class="market-district" aria-label="Торговые ряды">
    <header class="market-district__intro">
      <h2>Торговые ряды</h2>
      <p>Сталь звенит, зелья поблёскивают. Найдите своего торговца.</p>
    </header>
    <div class="market-district__stalls">
      <button
        v-for="merchant in merchants"
        :key="merchant.id"
        type="button"
        class="market-stall"
        :data-market-merchant="merchant.id"
        @click="emit('open-merchant', merchant.id)"
      >
        <span class="market-stall__portrait"
          ><MerchantPortrait :merchant-id="merchant.id" decorative
        /></span>
        <span class="market-stall__copy"
          ><small>{{ merchant.role }}</small
          ><strong>{{ merchant.name }}</strong> <span>{{ merchant.description }}</span
          ><b>Смотреть товары ›</b></span
        >
      </button>
    </div>
    <button
      class="market-stall market-stall--honor"
      type="button"
      data-market-honor
      @click="emit('open-honor')"
    >
      <span class="market-stall__portrait"
        ><MerchantPortrait merchant-id="ARENA_QUARTERMASTER" decorative
      /></span>
      <span class="market-stall__copy"
        ><small>Награды арены</small><strong>Магазин чести</strong>
        <span>Заслуженное в боях снаряжение за честь.</span><b>К интенданту ›</b></span
      >
    </button>
    <button class="market-crystals" type="button" data-market-crystals @click="emit('open-store')">
      <img :src="crystalArt" alt="" /><span
        ><strong>Магазин за кристаллы</strong>
        <small>Облики, артефакты и полезные припасы</small></span
      ><b aria-hidden="true">›</b>
    </button>
  </section>
</template>

<style scoped>
.market-district {
  display: grid;
  gap: 16px;
  padding: 4px 12px 24px;
}
.market-district__intro h2 {
  margin: 0;
  font:
    700 26px Georgia,
    serif;
  color: #ead5a5;
}
.market-district__intro p {
  margin: 7px 0 0;
  color: #b6afa1;
  font-size: 13px;
  line-height: 1.5;
}
.market-district__stalls {
  display: grid;
  gap: 10px;
}
.market-stall {
  display: grid;
  grid-template-columns: 100px minmax(0, 1fr);
  min-height: 148px;
  padding: 0;
  overflow: hidden;
  border: 1px solid #655033;
  border-radius: 8px;
  background: linear-gradient(110deg, #252119, #11151b);
  color: #eee5d5;
  text-align: left;
  cursor: pointer;
}
.market-stall__portrait {
  display: block;
  overflow: hidden;
  background: radial-gradient(ellipse at bottom, #655137, #11151b);
}
.market-stall__portrait img {
  width: 100%;
  height: 100%;
  max-height: 180px;
  object-fit: cover;
  object-position: top center;
}
.market-stall__copy {
  display: grid;
  align-content: center;
  gap: 6px;
  padding: 14px 12px;
}
.market-stall__copy small {
  color: #c8ab72;
  font-size: 12px;
}
.market-stall__copy strong {
  font:
    700 19px Georgia,
    serif;
}
.market-stall__copy span {
  font-size: 12px;
  line-height: 1.4;
  color: #b6afa1;
}
.market-stall__copy b {
  margin-top: 3px;
  color: #e4c486;
  font-size: 12px;
}
.market-stall--honor {
  border-color: #72563d;
  background: linear-gradient(110deg, #38251f, #17151a);
}
.market-crystals {
  display: flex;
  align-items: center;
  gap: 12px;
  min-height: 92px;
  padding: 12px;
  border: 1px solid #9674bd;
  border-radius: 8px;
  color: #eee4ff;
  background: linear-gradient(110deg, #302342, #111b2b);
  text-align: left;
  cursor: pointer;
}
.market-crystals img {
  width: 60px;
  height: 60px;
  object-fit: contain;
}
.market-crystals span {
  display: grid;
  gap: 6px;
  min-width: 0;
}
.market-crystals strong {
  font:
    700 19px Georgia,
    serif;
}
.market-crystals small {
  color: #c3b7d2;
  line-height: 1.4;
}
.market-crystals b {
  margin-left: auto;
  font-size: 24px;
}
.market-stall:focus-visible,
.market-crystals:focus-visible {
  outline: 2px solid #dfc184;
  outline-offset: 3px;
}
@media (min-width: 700px) {
  .market-district__stalls {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
