<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'
import { RouterView } from 'vue-router'

import { startPresenceHeartbeat, stopPresenceHeartbeat } from '@/api/presence'
import BossCombatLogReporter from '@/game/combat/BossCombatLogReporter.vue'
import CombatBlockFeedback from '@/game/combat/CombatBlockFeedback.vue'
import CombatHotbarOrderSync from '@/game/combat/CombatHotbarOrderSync.vue'
import CombatPartyDamageFeed from '@/game/combat/CombatPartyDamageFeed.vue'
import BetaErrorOverlay from '@/ui/BetaErrorOverlay.vue'
import '@/styles/betaUxPolish.css'

onMounted(startPresenceHeartbeat)
onUnmounted(stopPresenceHeartbeat)
</script>

<template>
  <RouterView />
  <CombatHotbarOrderSync />
  <BossCombatLogReporter />
  <CombatPartyDamageFeed />
  <CombatBlockFeedback />
  <BetaErrorOverlay />
</template>
