<script setup lang="ts">
import { computed } from 'vue'

import IconGenerator from '@/ui/icons/IconGenerator.vue'

const props = withDefaults(
  defineProps<{
    stars: number | null | undefined
    id?: string
  }>(),
  {
    id: 'item-quality',
  },
)

const normalizedStars = computed<number | null>(() => {
  if (!Number.isInteger(props.stars) || !props.stars || props.stars < 1 || props.stars > 5)
    return null
  return props.stars
})

const qualityAriaLabel = computed(() =>
  `\u041a\u0430\u0447\u0435\u0441\u0442\u0432\u043e \u043f\u0440\u0435\u0434\u043c\u0435\u0442\u0430: ${normalizedStars.value} \u0438\u0437 5`,
)
</script>

<template>
  <span
    v-if="normalizedStars !== null"
    class="item-quality-stars"
    data-item-quality-stars
    :aria-label="qualityAriaLabel"
  >
    <span
      v-for="star in 5"
      :key="star"
      class="item-quality-stars__star"
      :data-quality-star="star <= normalizedStars ? 'filled' : 'empty'"
      aria-hidden="true"
    >
      <IconGenerator
        :config="{
          id: `${id}-${star}`,
          glyph: 'star',
          category: 'utility',
        }"
      />
    </span>
  </span>
</template>

<style scoped>
.item-quality-stars {
  display: inline-flex;
  align-items: center;
  gap: 1px;
  color: var(--ui-color-gold);
}

.item-quality-stars__star {
  display: inline-grid;
  width: 0.8rem;
  height: 0.8rem;
}

.item-quality-stars__star[data-quality-star='empty'] {
  color: var(--ui-color-text-muted);
  opacity: 0.32;
}

.item-quality-stars__star :deep(.icon-generator) {
  border: 0;
  border-radius: 0;
  background: transparent;
  box-shadow: none;
  color: inherit;
}

.item-quality-stars__star :deep(.icon-generator svg) {
  width: 100%;
  height: 100%;
}

.item-quality-stars__star :deep(.icon-generator__glyph) {
  fill: currentColor;
  stroke: currentColor;
}

.item-quality-stars__star :deep(.icon-generator__rarity-accent) {
  display: none;
}
</style>
