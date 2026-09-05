<script setup lang="ts">
withDefaults(defineProps<{ tone?: 'success' | 'warning' | 'danger' | 'info'; title?: string }>(), {
  tone: 'info',
})
</script>

<template>
  <aside class="ui-toast" :class="`ui-toast--${tone}`" role="status">
    <strong v-if="title">{{ title }}</strong>
    <div><slot /></div>
  </aside>
</template>

<style scoped>
.ui-toast {
  --ui-toast-accent: var(--ui-color-primary);
  display: grid;
  gap: var(--ui-space-1);
  padding: var(--ui-space-3) var(--ui-space-4);
  border: 1px solid color-mix(in srgb, var(--ui-toast-accent) 58%, var(--ui-color-border));
  border-left: 3px solid var(--ui-toast-accent);
  border-radius: var(--ui-radius-md);
  background:
    linear-gradient(90deg, color-mix(in srgb, var(--ui-toast-accent) 8%, transparent), transparent 45%),
    var(--ui-gradient-panel);
  box-shadow: var(--ui-shadow-inset);
  color: var(--ui-color-text-secondary);
}

.ui-toast strong {
  color: var(--ui-color-text-primary);
  font-size: var(--ui-font-size-sm);
}

.ui-toast--success { --ui-toast-accent: var(--ui-color-success); }
.ui-toast--warning { --ui-toast-accent: var(--ui-color-warning); }
.ui-toast--danger { --ui-toast-accent: var(--ui-color-danger); }
</style>
