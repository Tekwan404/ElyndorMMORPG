<script setup lang="ts">
import { computed, ref } from 'vue'

import CharacterOverviewView from '@/game/character/views/CharacterOverviewView.vue'
import CharacterStatsView from '@/game/character/views/CharacterStatsView.vue'
import InventoryView from '@/game/character/views/InventoryView.vue'
import TalentTreeView from '@/game/talents/views/TalentTreeView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

type HeroTab = 'character' | 'inventory' | 'stats' | 'talents'

const session = useGameSessionStore()
const activeTab = ref<HeroTab>('character')
const hasTalentTree = computed(() => ['WARRIOR', 'MAGE'].includes(session.snapshot?.character?.classId ?? ''))
const tabs: readonly { id: HeroTab; label: string; available: boolean | 'talents' }[] = [
  { id: 'character', label: 'Персонаж', available: true },
  { id: 'inventory', label: 'Инвентарь', available: true },
  { id: 'stats', label: 'Характеристики', available: true },
  { id: 'talents', label: 'Таланты', available: 'talents' },
]

function isAvailable(tab: (typeof tabs)[number]): boolean {
  return tab.available === true || (tab.available === 'talents' && hasTalentTree.value)
}
</script>

<template>
  <section class="hero-view">
    <nav class="hero-tabs" aria-label="Разделы героя">
      <button
        v-for="tab in tabs"
        :key="tab.id"
        :data-hero-tab="tab.id"
        type="button"
        :disabled="!isAvailable(tab)"
        :class="{ active: activeTab === tab.id }"
        :aria-current="activeTab === tab.id ? 'page' : undefined"
        @click="activeTab = tab.id"
      >
        {{ tab.label }}
      </button>
    </nav>
    <CharacterOverviewView v-if="activeTab === 'character'" />
    <TalentTreeView v-else-if="activeTab === 'talents' && hasTalentTree" />
    <InventoryView v-else-if="activeTab === 'inventory'" />
    <CharacterStatsView v-else />
  </section>
</template>

<style scoped>
.hero-view {
  min-height: 100%;
}

.hero-tabs {
  position: sticky;
  z-index: var(--ui-z-sticky);
  top: 0;
  display: flex;
  gap: 3px;
  overflow-x: auto;
  padding: var(--ui-space-2) var(--ui-space-3);
  border-bottom: 1px solid var(--ui-color-border);
  background: linear-gradient(180deg, rgb(12 18 30 / 98%), rgb(8 12 20 / 94%));
  box-shadow: 0 8px 20px rgb(0 0 0 / 15%);
  backdrop-filter: blur(14px);
  scrollbar-width: none;
}

.hero-tabs::-webkit-scrollbar {
  display: none;
}

.hero-tabs button {
  position: relative;
  min-width: max-content;
  min-height: 40px;
  flex: 1 0 auto;
  padding: var(--ui-space-2) var(--ui-space-3);
  border: 1px solid transparent;
  border-radius: var(--ui-radius-md);
  background: transparent;
  color: var(--ui-color-text-muted);
  font: inherit;
  font-size: var(--ui-font-size-sm);
  font-weight: var(--ui-font-weight-medium);
  cursor: pointer;
  transition:
    color var(--ui-transition-fast),
    border-color var(--ui-transition-fast),
    background var(--ui-transition-fast);
}

.hero-tabs button.active {
  border-color: var(--ui-color-border);
  background: linear-gradient(180deg, rgb(146 136 255 / 13%), rgb(255 255 255 / 2%));
  box-shadow: var(--ui-shadow-inset);
  color: #d5d2ff;
}

.hero-tabs button.active::after {
  position: absolute;
  right: 28%;
  bottom: 3px;
  left: 28%;
  height: 2px;
  border-radius: var(--ui-radius-round);
  background: var(--ui-color-primary);
  box-shadow: 0 0 9px rgb(146 136 255 / 48%);
  content: '';
}

.hero-tabs button:disabled {
  cursor: not-allowed;
  opacity: .3;
}
</style>
