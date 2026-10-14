<script setup lang="ts">
import { onMounted, ref } from 'vue'
import crystalArt from '@/assets/store/ether-crystal.webp'
import { useGameSessionStore } from '@/stores/gameSession'
const emit = defineEmits<{ open: [] }>()
const session = useGameSessionStore()
const failed = ref(false)
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
    aria-label="Магазин за кристаллы"
    :title="
      failed
        ? 'Баланс недоступен. Откройте магазин, чтобы повторить загрузку.'
        : 'Кристаллы · Осколки Эфира'
    "
    @click="emit('open')"
  >
    <img :src="crystalArt" alt="" /><span
      ><small>Кристаллы</small>
      <strong>{{ session.crystalBalance?.toLocaleString('ru-RU') ?? '—' }}</strong></span
    >
    <b aria-hidden="true">+</b>
  </button>
</template>

<style scoped>
.crystal-wallet {
  display: flex;
  align-items: center;
  gap: 5px;
  max-width: 160px;
  min-height: 44px;
  padding: 4px 8px 4px 2px;
  border: 1px solid #8670b0;
  border-radius: 8px;
  background: linear-gradient(120deg, #292038, #111a29);
  color: #eee1ff;
  text-align: left;
  cursor: pointer;
}
.crystal-wallet img {
  width: 33px;
  height: 33px;
  object-fit: contain;
}
.crystal-wallet span {
  display: grid;
  gap: 1px;
  min-width: 0;
}
.crystal-wallet small {
  font-size: 10px;
  color: #cab6e6;
}
.crystal-wallet strong {
  max-width: 84px;
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 12px;
}
.crystal-wallet b {
  margin-left: 4px;
  color: #d5b6ff;
  font-size: 18px;
}
.crystal-wallet:focus-visible {
  outline: 2px solid #dbc39a;
  outline-offset: 2px;
}
</style>
