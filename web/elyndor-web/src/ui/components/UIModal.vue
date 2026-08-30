<script setup lang="ts">
import { onBeforeUnmount, onMounted, useId } from 'vue'

const props = defineProps<{ open: boolean; title: string }>()
const emit = defineEmits<{ close: [] }>()
const titleId = useId()

function onKeydown(event: KeyboardEvent) {
  if (props.open && event.key === 'Escape') emit('close')
}

onMounted(() => document.addEventListener('keydown', onKeydown))
onBeforeUnmount(() => document.removeEventListener('keydown', onKeydown))
</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="ui-modal" @click.self="$emit('close')">
      <section class="ui-modal__dialog" role="dialog" aria-modal="true" :aria-labelledby="titleId">
        <header class="ui-modal__header">
          <h2 :id="titleId">{{ title }}</h2>
          <button
            data-modal-close
            class="ui-modal__close"
            type="button"
            aria-label="Close"
            @click="$emit('close')"
          >
            ×
          </button>
        </header>
        <div class="ui-modal__body"><slot /></div>
        <footer v-if="$slots.actions" class="ui-modal__actions"><slot name="actions" /></footer>
      </section>
    </div>
  </Teleport>
</template>

<style scoped>
.ui-modal {
  position: fixed;
  z-index: var(--ui-z-modal);
  inset: 0;
  display: grid;
  align-items: end;
  padding: var(--ui-space-3) var(--ui-space-3) calc(var(--ui-space-3) + var(--ui-safe-area-bottom));
  background: var(--ui-color-overlay);
}
.ui-modal__dialog {
  width: min(100%, var(--ui-content-width));
  max-height: calc(var(--ui-viewport-height) - var(--ui-space-7));
  margin-inline: auto;
  overflow: auto;
  border: 1px solid var(--ui-color-border-strong);
  border-radius: var(--ui-radius-lg);
  background: var(--ui-color-surface-1);
  box-shadow: var(--ui-shadow-modal);
}
.ui-modal__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-3);
  padding: var(--ui-space-4);
  border-bottom: 1px solid var(--ui-color-border);
}
.ui-modal__header h2 {
  margin: 0;
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-lg);
}
.ui-modal__close {
  width: var(--ui-touch-target);
  height: var(--ui-touch-target);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: transparent;
  color: var(--ui-color-text-secondary);
  font: inherit;
  font-size: var(--ui-font-size-xl);
  cursor: pointer;
}
.ui-modal__body {
  padding: var(--ui-space-4);
  color: var(--ui-color-text-secondary);
}
.ui-modal__actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--ui-space-2);
  padding: var(--ui-space-4);
  border-top: 1px solid var(--ui-color-border);
}
@media (min-width: 540px) {
  .ui-modal {
    align-items: center;
  }
}
</style>
