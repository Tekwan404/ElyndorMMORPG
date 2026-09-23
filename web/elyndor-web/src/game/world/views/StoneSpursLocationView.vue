<script setup lang="ts">
import WorldView from './WorldView.vue'

const props = withDefaults(defineProps<{ openGuild?: boolean }>(), { openGuild: false })
const emit = defineEmits<{ 'open-party': [] }>()
</script>

<template>
  <WorldView
    :open-guild="props.openGuild"
    @open-party="emit('open-party')"
  />
</template>
