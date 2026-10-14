<script setup lang="ts">
import { onMounted } from 'vue'
import MerchantPortrait from '@/game/world/components/MerchantPortrait.vue'
import ArenaHonorShop from '@/game/pvp/components/ArenaHonorShop.vue'
import { useArenaStore } from '@/game/pvp/arenaStore'
import { arenaErrorMessage } from '@/game/pvp/arenaPresentation'
const arena = useArenaStore()
onMounted(() => arena.loadShop())
</script>

<template>
  <section class="market-honor">
    <header>
      <MerchantPortrait
        class="market-honor__portrait"
        merchant-id="ARENA_QUARTERMASTER"
        name="Интендант арены"
      />
      <div>
        <h2>Интендант арены</h2>
        <p>Честь добывают в бою. Здесь она становится снаряжением.</p>
      </div>
    </header>
    <ArenaHonorShop
      :shop="arena.shop"
      :loading="arena.shopPending"
      :pending-item-id="arena.shopPurchasePendingId"
      :error-message="arena.shopErrorCode ? arenaErrorMessage(arena.shopErrorCode) : null"
      @refresh="arena.loadShop"
      @buy="arena.buyHonorItem"
    />
  </section>
</template>

<style scoped>
.market-honor {
  padding: 0 12px 24px;
}
.market-honor header {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 16px;
}
.market-honor__portrait {
  flex-shrink: 0;
  width: 80px;
  height: 96px;
  object-fit: cover;
  object-position: top;
}
.market-honor h2 {
  font:
    700 23px Georgia,
    serif;
  color: #e5c48d;
}
.market-honor p {
  color: #b6afa1;
  font-size: 13px;
  line-height: 1.5;
}
</style>
