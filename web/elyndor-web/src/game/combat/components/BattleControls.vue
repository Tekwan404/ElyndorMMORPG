<script setup lang="ts">
defineProps<{
  autoAttackEnabled: boolean
  disabled: boolean
  training: boolean
}>()
const emit = defineEmits<{ toggleAutoAttack: []; flee: []; resetTraining: [] }>()
</script>

<template>
  <section class="battle-controls" aria-label="Управление боем">
    <button type="button" class="battle-controls__auto" :data-active="autoAttackEnabled" :disabled="disabled" @click="emit('toggleAutoAttack')">
      <span aria-hidden="true">⚔</span>
      <b>Автоатака</b>
      <small>{{ autoAttackEnabled ? 'Включена' : 'Выключена' }}</small>
    </button>
    <button v-if="training" type="button" :disabled="disabled" @click="emit('resetTraining')">
      <span aria-hidden="true">↻</span><b>Сбросить</b><small>Новый замер</small>
    </button>
    <button v-else type="button" :disabled="disabled" @click="emit('flee')">
      <span aria-hidden="true">↗</span><b>Сбежать</b><small>Остаться в локации</small>
    </button>
  </section>
</template>

<style scoped>
.battle-controls { display: grid; grid-template-columns: 1.25fr 1fr; gap: .4rem; }
.battle-controls button { display: grid; min-height: 48px; grid-template-columns: 2rem 1fr; grid-template-rows: 1fr 1fr; align-items: center; column-gap: .4rem; padding: .35rem .5rem; border: 1px solid rgb(177 151 91 / 35%); border-radius: 8px; background: #090c13; color: #e9e1d4; font: inherit; text-align: left; }
.battle-controls button > span { display: grid; width: 2rem; height: 2rem; grid-row: 1 / 3; place-items: center; color: #c9c2d8; font-size: 1rem; }
.battle-controls button b { align-self: end; font-size: .62rem; }
.battle-controls button small { align-self: start; color: #8f8b88; font-size: .48rem; }
.battle-controls__auto[data-active='true'] { border-color: #d4ae55; background: radial-gradient(circle at 30% 50%, rgb(107 75 25 / 38%), #0b0d13 65%); box-shadow: inset 0 0 14px rgb(223 175 70 / 14%); }
.battle-controls__auto[data-active='true'] > span { color: #f1cd71; }
.battle-controls button:focus-visible { outline: 2px solid #ebcf7f; outline-offset: 2px; }
</style>
