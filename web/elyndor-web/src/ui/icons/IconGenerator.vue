<script setup lang="ts">
import { computed } from 'vue'

import { GLYPHS } from './glyphs'
import { resolveIcon } from './icon-renderer'
import type { IconConfig } from './icon.types'

const props = defineProps<{ config: IconConfig; label?: string }>()
const icon = computed(() => resolveIcon(props.config))
</script>

<template>
  <span
    class="icon-generator"
    :class="icon.classes"
    :data-icon-id="icon.id"
    :data-rarity="icon.rarity"
    :data-state="icon.state"
  >
    <svg
      viewBox="0 0 24 24"
      fill="none"
      :role="label ? 'img' : undefined"
      :aria-label="label"
      :aria-hidden="label ? undefined : 'true'"
    >
      <g
        class="icon-generator__glyph"
        stroke="currentColor"
        stroke-width="1.5"
        stroke-linecap="round"
        stroke-linejoin="round"
      >
        <path v-for="path in icon.glyph.paths" :key="path" :d="path" />
      </g>
      <g
        v-if="icon.modifier"
        class="icon-generator__modifier"
        stroke="currentColor"
        stroke-width="1.35"
        stroke-linecap="round"
        stroke-linejoin="round"
      >
        <path v-for="path in icon.modifier.paths" :key="path" :d="path" />
      </g>
      <path class="icon-generator__rarity-accent" d="M3 20h6" />
      <g
        v-if="icon.state === 'locked'"
        class="icon-generator__lock"
        stroke="currentColor"
        stroke-width="1.8"
        stroke-linecap="round"
        stroke-linejoin="round"
      >
        <path v-for="path in GLYPHS.lock.paths" :key="path" :d="path" />
      </g>
      <path v-if="icon.state === 'equipped'" class="icon-generator__equipped" d="m16 19 2 2 4-5" />
      <circle v-if="icon.state === 'new'" class="icon-generator__new" cx="20" cy="4" r="2" />
    </svg>
  </span>
</template>

<style scoped>
.icon-generator {
  --icon-rarity: var(--ui-rarity-common);
  --icon-rarity-glow: var(--ui-rarity-glow-common);
  --icon-modifier: var(--ui-color-text-secondary);

  position: relative;
  display: inline-grid;
  width: 100%;
  height: 100%;
  overflow: hidden;
  border: 1px solid var(--icon-rarity);
  border-radius: var(--ui-radius-sm);
  background: var(--ui-color-surface-1);
  box-shadow: var(--icon-rarity-glow);
  color: var(--ui-color-text-secondary);
  place-items: center;
  transition:
    border-color var(--ui-transition-fast),
    box-shadow var(--ui-transition-fast),
    opacity var(--ui-transition-fast);
}

.icon-generator svg {
  width: 74%;
  height: 74%;
  overflow: visible;
}

.icon-generator__glyph {
  color: var(--ui-color-text-secondary);
}

.icon-generator__modifier {
  color: var(--icon-modifier);
  opacity: 0.44;
  transform: translate(11px, -1px) scale(0.42);
  transform-origin: center;
}

.icon-generator__rarity-accent {
  stroke: var(--icon-rarity);
  stroke-width: 2;
}

.icon-generator__lock {
  color: var(--ui-color-text-primary);
  transform: translate(7px, 7px) scale(0.42);
  transform-origin: center;
}

.icon-generator__equipped {
  fill: none;
  stroke: var(--ui-color-success);
  stroke-width: 2;
  stroke-linecap: round;
  stroke-linejoin: round;
}

.icon-generator__new {
  fill: var(--ui-color-secondary);
  stroke: var(--ui-color-background);
  stroke-width: 1;
}

.icon--uncommon {
  --icon-rarity: var(--ui-rarity-uncommon);
  --icon-rarity-glow: var(--ui-rarity-glow-uncommon);
}
.icon--rare {
  --icon-rarity: var(--ui-rarity-rare);
  --icon-rarity-glow: var(--ui-rarity-glow-rare);
}
.icon--epic {
  --icon-rarity: var(--ui-rarity-epic);
  --icon-rarity-glow: var(--ui-rarity-glow-epic);
}
.icon--legendary {
  --icon-rarity: var(--ui-rarity-legendary);
  --icon-rarity-glow: var(--ui-rarity-glow-legendary);
}
.icon--unique {
  --icon-rarity: var(--ui-rarity-unique);
  --icon-rarity-glow: var(--ui-rarity-glow-unique);
}
.icon--fire {
  --icon-modifier: var(--ui-modifier-fire);
}
.icon--ice {
  --icon-modifier: var(--ui-modifier-ice);
}
.icon--lightning {
  --icon-modifier: var(--ui-modifier-lightning);
}
.icon--poison {
  --icon-modifier: var(--ui-modifier-poison);
}
.icon--holy {
  --icon-modifier: var(--ui-modifier-holy);
}
.icon--shadow {
  --icon-modifier: var(--ui-modifier-shadow);
}

.icon--selected {
  box-shadow: var(--ui-glow-selected);
}

.icon--locked::after {
  position: absolute;
  inset: 0;
  background: var(--ui-color-overlay);
  content: '';
}

.icon--locked svg {
  position: relative;
  z-index: 1;
}

.icon--disabled {
  filter: grayscale(0.85);
  opacity: 0.42;
}
</style>
