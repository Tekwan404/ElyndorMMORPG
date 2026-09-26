<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'
import { RouterView } from 'vue-router'

import { startPresenceHeartbeat, stopPresenceHeartbeat } from '@/api/presence'
import TrainingDummyCombatLogReporter from '@/game/combat/TrainingDummyCombatLogReporter.vue'
import CombatBlockFeedback from '@/game/combat/CombatBlockFeedback.vue'
import CombatHotbarOrderSync from '@/game/combat/CombatHotbarOrderSync.vue'
import BetaErrorOverlay from '@/ui/BetaErrorOverlay.vue'
import '@/styles/betaUxPolish.css'

onMounted(startPresenceHeartbeat)
onUnmounted(stopPresenceHeartbeat)
</script>

<template>
  <RouterView />
  <CombatHotbarOrderSync />
  <TrainingDummyCombatLogReporter />
  <CombatBlockFeedback />
  <BetaErrorOverlay />
</template>
