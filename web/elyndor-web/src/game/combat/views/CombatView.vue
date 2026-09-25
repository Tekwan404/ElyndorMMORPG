<script setup lang="ts">
import BattleScreen from './BattleScreen.vue'

const emit = defineEmits<{ leave: [] }>()
</script>

<template>
  <BattleScreen @leave="emit('leave')" />
</template>
