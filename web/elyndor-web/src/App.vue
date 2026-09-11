<script setup lang="ts">
import { RouterView } from 'vue-router'

import CombatTelemetryOverlay from '@/game/combat/CombatTelemetryOverlay.vue'
</script>

<template>
  <RouterView />
  <CombatTelemetryOverlay />
</template>
