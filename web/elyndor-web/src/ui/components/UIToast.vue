<script setup lang="ts">
withDefaults(defineProps<{ tone?: 'success' | 'warning' | 'danger' | 'info'; title?: string; placement?: 'inline' | 'overlay' }>(), {
  tone: 'info',
  placement: 'inline',
})
</script>

<template>
  <aside class="ui-toast" :class="[`ui-toast--${tone}`, `ui-toast--${placement}`]"
    :role="tone === 'danger' ? 'alert' : 'status'" :aria-live="tone === 'danger' ? 'assertive' : 'polite'" aria-atomic="true">
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
.ui-toast--overlay {
  position: fixed;
  z-index: var(--ui-z-toast);
  bottom: calc(var(--ui-safe-area-bottom) + var(--ui-space-3));
  left: max(var(--ui-space-3), var(--ui-safe-area-left));
  right: max(var(--ui-space-3), var(--ui-safe-area-right));
  width: auto;
  max-width: var(--ui-content-width);
  max-height: 30dvh;
  margin-inline: auto;
  overflow-y: auto;
  overflow-wrap: anywhere;
  box-shadow: var(--ui-shadow-elevated);
}
</style>
