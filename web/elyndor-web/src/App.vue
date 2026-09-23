<script setup lang="ts">
import { RouterView } from 'vue-router'

import BossCombatLogReporter from '@/game/combat/BossCombatLogReporter.vue'
import CombatBattlefieldPresentation from '@/game/combat/CombatBattlefieldPresentation.vue'
import CombatBlockFeedback from '@/game/combat/CombatBlockFeedback.vue'
import CombatHotbarOrderSync from '@/game/combat/CombatHotbarOrderSync.vue'
import CombatLogPreview from '@/game/combat/CombatLogPreview.vue'
import BetaErrorOverlay from '@/ui/BetaErrorOverlay.vue'
import '@/styles/betaUxPolish.css'
</script>

<template>
  <RouterView />
  <CombatHotbarOrderSync />
  <BossCombatLogReporter />
  <CombatBattlefieldPresentation />
  <CombatBlockFeedback />
  <CombatLogPreview />
  <BetaErrorOverlay />
</template>