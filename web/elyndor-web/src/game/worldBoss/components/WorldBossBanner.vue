<script setup lang="ts">
import { computed } from 'vue'

import { useWorldBossStore } from '@/game/worldBoss/worldBossStore'

defineEmits<{ open: [] }>()

const worldBoss = useWorldBossStore()
const boss = computed(() => worldBoss.active)
const healthPercent = computed(() => {
  const current = boss.value
  if (!current || current.maxHealth <= 0) return 0
  return Math.max(0, Math.min(100, (current.currentHealth / current.maxHealth) * 100))
})

const formatter = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 })
</script>

<template>
  <button
    v-if="boss && boss.currentHealth > 0"
    class="world-boss-banner"
    type="button"
    data-world-boss-banner
    @click="$emit('open')"
  >
    <span class="world-boss-banner__crest" aria-hidden="true">W</span>
    <span class="world-boss-banner__body">
      <span class="world-boss-banner__eyebrow">Мировой босс · ур. {{ boss.level }}</span>
      <strong>{{ boss.name }}</strong>
      <span class="world-boss-banner__bar" aria-hidden="true">
        <i :style="{ width: `${healthPercent}%` }" />
      </span>
      <small>
        {{ formatter.format(boss.currentHealth) }} / {{ formatter.format(boss.maxHealth) }}
        · {{ boss.phaseName }}
      </small>
    </span>
    <span class="world-boss-banner__action">Открыть</span>
  </button>
</template>

<style scoped>
.world-boss-banner {
  display: grid;
  width: 100%;
  min-height: 76px;
  grid-template-columns: 48px minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--ui-space-3);
  padding: var(--ui-space-3);
  border: 1px solid color-mix(in srgb, var(--ui-color-danger) 46%, var(--ui-color-border));
  border-radius: var(--ui-radius-lg);
  background:
    radial-gradient(circle at 8% 28%, rgb(175 56 48 / 18%), transparent 32%),
    linear-gradient(135deg, rgb(28 18 17 / 98%), rgb(14 16 23 / 98%));
  box-shadow: var(--ui-shadow-inset), 0 10px 26px rgb(0 0 0 / 24%);
  color: var(--ui-color-text-primary);
  text-align: left;
  cursor: pointer;
}

.world-boss-banner__crest {
  display: grid;
  width: 44px;
  height: 44px;
  place-items: center;
  border: 1px solid rgb(218 146 102 / 44%);
  border-radius: 50%;
  background: radial-gradient(circle, #7a3028, #241416 70%);
  color: #f0c69b;
  font: 800 1.05rem var(--ui-font-display);
  box-shadow: inset 0 0 0 3px rgb(0 0 0 / 22%);
}

.world-boss-banner__body {
  display: grid;
  min-width: 0;
  gap: 3px;
}

.world-boss-banner__body strong {
  overflow: hidden;
  color: #f0d7bd;
  font: 700 .94rem var(--ui-font-display);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.world-boss-banner__eyebrow,
.world-boss-banner__body small {
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
}

.world-boss-banner__eyebrow {
  color: #c98e72;
  font-weight: 700;
  letter-spacing: .06em;
  text-transform: uppercase;
}

.world-boss-banner__bar {
  display: block;
  height: 5px;
  overflow: hidden;
  border-radius: var(--ui-radius-round);
  background: rgb(0 0 0 / 48%);
}

.world-boss-banner__bar i {
  display: block;
  height: 100%;
  background: linear-gradient(90deg, #812f2f, #c85b45);
  transition: width var(--ui-transition-normal);
}

.world-boss-banner__action {
  color: var(--ui-color-gold);
  font-size: var(--ui-font-size-xs);
  font-weight: 700;
}

@media (max-width: 390px) {
  .world-boss-banner {
    grid-template-columns: 42px minmax(0, 1fr);
  }

  .world-boss-banner__action {
    display: none;
  }
}
</style>
