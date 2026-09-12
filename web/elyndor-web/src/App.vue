<script setup lang="ts">
import { RouterView } from 'vue-router'

import BossCombatLogReporter from '@/game/combat/BossCombatLogReporter.vue'
import CombatTelemetryOverlay from '@/game/combat/CombatTelemetryOverlay.vue'
import BetaErrorOverlay from '@/ui/BetaErrorOverlay.vue'
import '@/styles/betaUxPolish.css'
</script>

<template>
  <RouterView />
  <BossCombatLogReporter />
  <CombatTelemetryOverlay />
  <BetaErrorOverlay />
</template>
