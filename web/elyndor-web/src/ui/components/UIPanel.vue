<template>
  <section class="ui-panel">
    <header v-if="$slots.title || $slots.actions" class="ui-panel__header">
      <div class="ui-panel__title"><slot name="title" /></div>
      <div class="ui-panel__actions"><slot name="actions" /></div>
    </header>
    <div class="ui-panel__body"><slot /></div>
  </section>
</template>

<style scoped>
.ui-panel {
  overflow: hidden;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-lg);
  background: var(--ui-gradient-panel);
  box-shadow: var(--ui-shadow-inset), var(--ui-shadow-panel);
}

.ui-panel__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ui-space-3);
  padding: var(--ui-space-4);
  border-bottom: 1px solid var(--ui-color-border);
  background: rgb(255 255 255 / 1.5%);
}

.ui-panel__title {
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-lg);
  font-weight: var(--ui-font-weight-semibold);
  letter-spacing: .01em;
}

.ui-panel__actions {
  display: flex;
  gap: var(--ui-space-2);
}

.ui-panel__body {
  padding: var(--ui-space-4);
}
</style>
