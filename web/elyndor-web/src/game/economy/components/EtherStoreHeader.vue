<script setup lang="ts">
import crystalArt from '@/assets/store/ether-crystal.webp'
import vaultArt from '@/assets/store/crystal-vault.webp'
defineProps<{ balance: number | null }>()
const emit = defineEmits<{ 'open-currency': [] }>()
</script>

<template>
  <header class="ether-vault" :style="{ '--vault-art': 'url(' + vaultArt + ')' }" data-ether-store>
    <div class="ether-vault__title">
      <small>Особая лавка Элиндора</small>
      <h1>Магазин<br />за кристаллы</h1>
      <p>Новый образ. Больше возможностей.<br />Ваш вклад в мир Elyndor.</p>
    </div>
    <div class="ether-vault__wallet">
      <img :src="crystalArt" alt="" />
      <div>
        <small>Ваши кристаллы</small>
        <strong>{{ balance?.toLocaleString('ru-RU') ?? '—' }} <span>Осколки Эфира</span></strong>
      </div>
      <button
        type="button"
        data-open-crystals
        aria-label="Пополнить кристаллы"
        @click="emit('open-currency')"
      >
        Пополнить <b>+</b>
      </button>
    </div>
  </header>
</template>

<style scoped>
.ether-vault {
  position: relative;
  display: grid;
  align-content: space-between;
  min-height: 310px;
  overflow: hidden;
  border: 1px solid #7d674a;
  border-radius: 10px;
  background:
    linear-gradient(90deg, rgb(7 12 22 / 92%), rgb(7 12 22 / 25%) 75%),
    var(--vault-art) center / cover;
  box-shadow: inset 0 0 0 3px rgb(187 153 93 / 7%);
}
.ether-vault__title {
  padding: 24px 20px;
}
.ether-vault__title small {
  color: #cfb78c;
  font-size: 12px;
}
.ether-vault h1 {
  margin: 10px 0;
  color: #f6e9d0;
  font:
    700 clamp(29px, 7vw, 42px)/1.05 Georgia,
    serif;
  text-shadow: 0 2px 12px #000;
}
.ether-vault__title p {
  margin: 0;
  color: #ccc3b7;
  font-size: 13px;
  line-height: 1.6;
}
.ether-vault__wallet {
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: 80px;
  padding: 12px;
  border-top: 1px solid #65516d;
  background: rgb(11 12 23 / 92%);
}
.ether-vault__wallet img {
  width: 44px;
  height: 44px;
  object-fit: contain;
}
.ether-vault__wallet div {
  display: grid;
  gap: 4px;
  min-width: 0;
}
.ether-vault__wallet small {
  color: #c0b2cd;
  font-size: 11px;
}
.ether-vault__wallet strong {
  color: #f0e1ff;
  font-size: 18px;
  overflow-wrap: anywhere;
}
.ether-vault__wallet span {
  display: block;
  font-size: 10px;
  font-weight: 400;
  color: #ad9dbf;
}
.ether-vault__wallet button {
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: 46px;
  margin-left: auto;
  padding: 8px 11px;
  border: 1px solid #be9de5;
  border-radius: 6px;
  background: linear-gradient(#655083, #3d3054);
  color: #fff3ff;
  font:
    700 12px system-ui,
    sans-serif;
  cursor: pointer;
}
.ether-vault__wallet button:focus-visible {
  outline: 2px solid #f2d491;
  outline-offset: 3px;
}
.ether-vault__wallet b {
  font-size: 20px;
}
@media (min-width: 700px) {
  .ether-vault {
    min-height: 350px;
  }
  .ether-vault__title {
    padding: 32px;
  }
  .ether-vault__wallet span {
    display: inline;
    margin-left: 8px;
  }
}
</style>
