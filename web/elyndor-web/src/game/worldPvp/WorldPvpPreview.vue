<script setup lang="ts">
import WorldPvpView from './WorldPvpView.vue'
import { worldPvpPreviewDashboard } from './worldPvpPreviewDashboard'
</script>

<template>
  <main class="world-pvp-preview">
    <WorldPvpView :dashboard="worldPvpPreviewDashboard" preview />
  </main>
</template>

<style scoped>
.world-pvp-preview {
  width: min(100%, 720px);
  min-height: 100vh;
  margin: 0 auto;
  padding: 20px 12px 48px;
  background: #0e1118;
}
</style>
