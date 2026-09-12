<script setup lang="ts">
import { RouterView } from 'vue-router'

import BossCombatLogReporter from '@/game/combat/BossCombatLogReporter.vue'
import CombatBlockFeedback from '@/game/combat/CombatBlockFeedback.vue'
import CombatTelemetryOverlay from '@/game/combat/CombatTelemetryOverlay.vue'
import BetaErrorOverlay from '@/ui/BetaErrorOverlay.vue'
import '@/styles/betaUxPolish.css'
</script>

<template>
  <RouterView />
  <BossCombatLogReporter />
  <CombatTelemetryOverlay />
  <CombatBlockFeedback />
  <BetaErrorOverlay />
</template>
