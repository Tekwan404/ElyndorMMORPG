<script setup lang="ts">
import { useId, useTemplateRef } from 'vue'
import { useModalLayer } from '@/ui/composables/useModalLayer'

const props = defineProps<{ open: boolean; title: string; busy?: boolean }>()
const emit = defineEmits<{ close: [] }>()
const titleId = useId()

const dialog = useTemplateRef<HTMLElement>('dialog')
function close() {
  if (!props.busy) emit('close')
}
const { layerIndex } = useModalLayer({ open: () => props.open, root: dialog, close })
</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="ui-modal" :style="{ zIndex: `calc(var(--ui-z-modal) + ${layerIndex})` }" @click.self="close">
      <section ref="dialog" class="ui-modal__dialog" role="dialog" aria-modal="true" :aria-labelledby="titleId" :aria-busy="busy || undefined" tabindex="-1">
        <header class="ui-modal__header">
          <h2 :id="titleId">{{ title }}</h2>
          <button
            data-modal-close
            class="ui-modal__close"
            type="button"
            aria-label="Закрыть"
            :disabled="busy"
            @click="close"
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
  background: rgb(2 4 8 / 78%);
  backdrop-filter: blur(4px);
}
.ui-modal__dialog {
  width: min(100%, var(--ui-content-width));
  max-height: calc(var(--ui-viewport-height) - var(--ui-safe-area-top) - var(--ui-safe-area-bottom) - var(--ui-space-7));
  margin-inline: auto;
  overflow: auto;
  overscroll-behavior: contain;
  border: 1px solid rgb(205 177 113 / 42%);
  border-radius: var(--ui-radius-lg);
  background:
    linear-gradient(180deg, rgb(20 25 31 / 98%), rgb(6 9 14 / 99%));
  box-shadow: var(--ui-shadow-modal);
}
.ui-modal__header {
  position: sticky;
  top: 0;
  z-index: 1;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-3);
  padding: var(--ui-space-4);
  border-bottom: 1px solid rgb(205 177 113 / 24%);
  background: var(--ui-color-surface-1);
}
.ui-modal__header h2 {
  margin: 0;
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-lg);
}
.ui-modal__close {
  width: var(--ui-touch-target);
  height: var(--ui-touch-target);
  border: 1px solid rgb(205 177 113 / 32%);
  border-radius: var(--ui-radius-md);
  background: transparent;
  color: #e1c584;
  font: inherit;
  font-size: var(--ui-font-size-xl);
  cursor: pointer;
}
.ui-modal__body {
  padding: var(--ui-space-4);
  color: var(--ui-color-text-secondary);
}
.ui-modal__actions {
  position: sticky;
  bottom: 0;
  flex-wrap: wrap;
  background: var(--ui-color-surface-1);
  display: flex;
  justify-content: flex-end;
  gap: var(--ui-space-2);
  padding: var(--ui-space-4);
  border-top: 1px solid var(--ui-color-border);
}
.ui-modal__close:focus-visible { outline: 2px solid var(--ui-color-focus); outline-offset: 2px; }
.ui-modal__close:disabled { opacity: .5; }
@media (min-width: 540px) {
  .ui-modal {
    align-items: center;
  }
}
</style>
