<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import crystalArt from '@/assets/store/ether-crystal.webp'
import { useGameSessionStore } from '@/stores/gameSession'
const emit = defineEmits<{ open: [] }>()
const session = useGameSessionStore()
const failed = ref(false)
const balanceText = computed(() => session.crystalBalance?.toLocaleString('ru-RU') ?? '—')
onMounted(async () => {
  if (session.crystalBalance !== null) return
  try {
    await session.getPremiumStore()
  } catch {
    failed.value = true
  }
})
</script>

<template>
  <button
    class="crystal-wallet"
    type="button"
    data-crystal-wallet
    :aria-label="`Магазин за кристаллы · ${balanceText}`"
    :title="
      failed
        ? 'Баланс недоступен. Откройте магазин, чтобы повторить загрузку.'
        : 'Кристаллы · Осколки Эфира'
    "
    @click="emit('open')"
  >
    <img :src="crystalArt" alt="" /><strong>{{ balanceText }}</strong>
    <b aria-hidden="true">+</b>
  </button>
</template>

<style scoped>
.crystal-wallet {
  display: flex;
  align-items: center;
  gap: 5px;
  max-width: 140px;
  min-height: 28px;
  padding: 2px 7px 2px 4px;
  border: 1px solid #8670b0;
  border-radius: 6px;
  background: linear-gradient(120deg, #292038, #111a29);
  color: #eee1ff;
  text-align: left;
  cursor: pointer;
  position: relative;
}
.crystal-wallet::before {
  position: absolute;
  inset: -6px 0;
  content: '';
}
.crystal-wallet img {
  width: 18px;
  height: 20px;
  object-fit: contain;
}
.crystal-wallet strong {
  max-width: 84px;
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 11px;
  white-space: nowrap;
  font-variant-numeric: tabular-nums;
}
.crystal-wallet b {
  margin-left: 1px;
  color: #d5b6ff;
  font-size: 14px;
}
.crystal-wallet:focus-visible {
  outline: 2px solid #dbc39a;
  outline-offset: 2px;
}
</style>
