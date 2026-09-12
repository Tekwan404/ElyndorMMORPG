<script setup lang="ts">
import { RouterView } from 'vue-router'

import CombatTelemetryOverlay from '@/game/combat/CombatTelemetryOverlay.vue'
import BetaErrorOverlay from '@/ui/BetaErrorOverlay.vue'
</script>

<template>
  <RouterView />
  <CombatTelemetryOverlay />
  <BetaErrorOverlay />
</template>
