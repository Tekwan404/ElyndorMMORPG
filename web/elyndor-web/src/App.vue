<script setup lang="ts">
import { onMounted } from 'vue'

import AppShell from '@/app/AppShell.vue'
import { useGameSessionStore } from '@/stores/gameSession'
import { initializeTelegramWebApp } from '@/telegram/telegramWebApp'

const session = useGameSessionStore()

onMounted(() => {
  initializeTelegramWebApp()
  void session.start()
})
</script>

<template>
  <AppShell />
</template>
