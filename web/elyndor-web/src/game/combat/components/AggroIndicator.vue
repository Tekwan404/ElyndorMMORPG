<script setup lang="ts">
defineProps<{ threatPercent?: number | null }>()
</script>

<template>
  <span class="aggro-indicator" data-aggro-indicator>
    <span aria-hidden="true">◆</span>
    <b>АГРО</b>
    <small v-if="threatPercent != null">{{ Math.round(threatPercent) }}%</small>
  </span>
</template>

<style scoped>
.aggro-indicator {
  position: absolute;
  z-index: 5;
  top: .25rem;
  right: .2rem;
  display: inline-flex;
  min-height: 1.35rem;
  align-items: center;
  gap: .18rem;
  padding: .15rem .32rem;
  border: 1px solid rgb(241 102 112 / 65%);
  border-radius: 999px;
  background: rgb(42 9 15 / 88%);
  box-shadow: 0 0 12px rgb(230 66 80 / 36%);
  color: #ff9ea8;
  line-height: 1;
  pointer-events: none;
}
.aggro-indicator span { font-size: .48rem; }
.aggro-indicator b { font-size: .5rem; letter-spacing: .07em; }
.aggro-indicator small { color: #f6c0c5; font-size: .46rem; }
</style>
