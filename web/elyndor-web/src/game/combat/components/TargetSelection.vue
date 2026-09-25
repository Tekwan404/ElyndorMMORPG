<script setup lang="ts">
defineProps<{ label?: string }>()
</script>

<template>
  <span class="target-selection" data-target-selection aria-hidden="true">
    <span class="target-selection__corner target-selection__corner--tl" />
    <span class="target-selection__corner target-selection__corner--tr" />
    <span class="target-selection__corner target-selection__corner--bl" />
    <span class="target-selection__corner target-selection__corner--br" />
    <b>{{ label ?? 'ЦЕЛЬ' }}</b>
  </span>
</template>

<style scoped>
.target-selection {
  position: absolute;
  z-index: 4;
  inset: 0;
  border-radius: 12px;
  filter: drop-shadow(0 0 7px rgb(232 197 111 / 50%));
  pointer-events: none;
}

.target-selection b {
  position: absolute;
  top: .3rem;
  left: 50%;
  padding: .12rem .38rem;
  border: 1px solid rgb(232 197 111 / 62%);
  border-radius: 999px;
  background: rgb(8 9 14 / 82%);
  color: #f4d98d;
  font-size: .48rem;
  letter-spacing: .08em;
  transform: translateX(-50%);
}

.target-selection__corner {
  position: absolute;
  width: 1rem;
  height: 1rem;
  border-color: #ebca72;
  border-style: solid;
}
.target-selection__corner--tl { top: 0; left: 0; border-width: 2px 0 0 2px; border-radius: 10px 0 0; }
.target-selection__corner--tr { top: 0; right: 0; border-width: 2px 2px 0 0; border-radius: 0 10px 0 0; }
.target-selection__corner--bl { bottom: 0; left: 0; border-width: 0 0 2px 2px; border-radius: 0 0 0 10px; }
.target-selection__corner--br { right: 0; bottom: 0; border-width: 0 2px 2px 0; border-radius: 0 0 10px; }

@media (prefers-reduced-motion: no-preference) {
  .target-selection { animation: selection-breathe 1.8s ease-in-out infinite; }
}

@keyframes selection-breathe {
  50% { filter: drop-shadow(0 0 11px rgb(232 197 111 / 75%)); }
}
</style>
