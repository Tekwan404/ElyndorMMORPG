<script setup lang="ts">
import crystalArt from '@/assets/store/ether-crystal.webp'
import { UIButton, UIModal } from '@/ui/components'
defineProps<{ open: boolean; balance: number | null }>()
const emit = defineEmits<{ close: [] }>()
</script>

<template>
  <UIModal :open="open" title="Кристаллы Элиндора" @close="emit('close')">
    <section class="crystal-info">
      <img :src="crystalArt" alt="Осколки Эфира" />
      <h2>Маленький осколок.<br />Большой мир.</h2>
      <p>
        Осколки Эфира — кристаллы для покупок в особой лавке. Облики, артефакты и припасы для вашего
        героя.
      </p>
      <div class="crystal-info__balance">
        <span>Ваш баланс</span
        ><strong>{{ balance?.toLocaleString('ru-RU') ?? '—' }} кристаллов</strong>
      </div>
      <p class="crystal-info__notice" role="status">
        Пополнение пока недоступно. Как только оплата появится, здесь можно будет приобрести
        кристаллы и поддержать развитие Elyndor.
      </p>
      <UIButton variant="secondary" @click="emit('close')">Вернуться в магазин</UIButton>
    </section>
  </UIModal>
</template>

<style scoped>
.crystal-info {
  display: grid;
  justify-items: center;
  gap: 16px;
  padding: 4px 4px 12px;
  text-align: center;
}
.crystal-info img {
  width: 100px;
  height: 112px;
  object-fit: contain;
  filter: drop-shadow(0 0 24px #6753a455);
}
.crystal-info h2 {
  margin: 0;
  font:
    700 27px/1.15 Georgia,
    serif;
  color: #eee0fb;
}
.crystal-info p {
  margin: 0;
  color: #c6bdce;
  font-size: 14px;
  line-height: 1.55;
}
.crystal-info__balance {
  display: grid;
  gap: 6px;
  width: 100%;
  padding: 14px;
  border-block: 1px solid #6e5777;
}
.crystal-info__balance span {
  font-size: 12px;
  color: #ae9cbc;
}
.crystal-info__balance strong {
  color: #dfc6ff;
}
.crystal-info .crystal-info__notice {
  color: #b7a88a;
  font-size: 13px;
}
</style>
