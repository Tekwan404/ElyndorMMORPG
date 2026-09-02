<script setup lang="ts">
import { computed, ref } from 'vue'

import CharacterOverviewView from '@/game/character/views/CharacterOverviewView.vue'
import CharacterStatsView from '@/game/character/views/CharacterStatsView.vue'
import InventoryView from '@/game/character/views/InventoryView.vue'
import WarriorTalentTreeView from '@/game/talents/views/WarriorTalentTreeView.vue'
import { useGameSessionStore } from '@/stores/gameSession'

type HeroTab = 'character' | 'inventory' | 'stats' | 'talents'

const session = useGameSessionStore()
const activeTab = ref<HeroTab>('character')
const isWarrior = computed(() => session.snapshot?.character?.classId === 'WARRIOR')
const tabs: readonly { id: HeroTab; label: string; available: boolean | 'warrior' }[] = [
  { id: 'character', label: 'Персонаж', available: true },
  { id: 'inventory', label: 'Инвентарь', available: true },
  { id: 'stats', label: 'Характеристики', available: true },
  { id: 'talents', label: 'Таланты', available: 'warrior' },
]

function isAvailable(tab: (typeof tabs)[number]): boolean {
  return tab.available === true || (tab.available === 'warrior' && isWarrior.value)
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
    <WarriorTalentTreeView v-else-if="activeTab === 'talents' && isWarrior" />
    <InventoryView v-else-if="activeTab === 'inventory'" />
    <CharacterStatsView v-else />
  </section>
</template>

<style scoped>
.hero-view { min-height: 100%; }
.hero-tabs {
  position: sticky;
  z-index: var(--ui-z-sticky);
  top: 0;
  display: flex;
  overflow-x: auto;
  border-bottom: 1px solid var(--ui-color-border);
  background: rgb(13 18 32 / 96%);
  backdrop-filter: blur(10px);
  scrollbar-width: none;
}
.hero-tabs::-webkit-scrollbar { display: none; }
.hero-tabs button {
  position: relative;
  min-width: max-content;
  min-height: var(--ui-touch-target);
  flex: 1 0 auto;
  padding: var(--ui-space-2) var(--ui-space-3);
  border: 0;
  background: transparent;
  color: var(--ui-color-text-muted);
  font: inherit;
  font-size: var(--ui-font-size-sm);
}
.hero-tabs button.active { color: var(--ui-color-text-primary); }
.hero-tabs button.active::after {
  position: absolute;
  right: var(--ui-space-3);
  bottom: 0;
  left: var(--ui-space-3);
  height: 2px;
  background: var(--ui-color-primary);
  box-shadow: var(--ui-glow-magic);
  content: '';
}
.hero-tabs button:disabled { opacity: 0.38; }
</style>
