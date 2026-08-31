<script setup lang="ts">
import { computed, ref } from 'vue'

import {
  UIButton,
  UIAbilityButton,
  UICastBar,
  UICard,
  UIHealthBar,
  UIItemSlot,
  UILoadingState,
  UIModal,
  UIPanel,
  UITabs,
  UIToast,
  UIEffectBadge,
} from '@/ui/components'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { IconConfig, IconState, Rarity } from '@/ui/icons/icon.types'
import { EFFECT_ICON_PRESETS } from '@/ui/icons/presets/effects'
import { ITEM_ICON_PRESETS } from '@/ui/icons/presets/items'

const activeTab = ref('inventory')
const modalOpen = ref(false)
const tabs = [
  { value: 'inventory', label: 'Inventory' },
  { value: 'character', label: 'Character' },
  { value: 'locked', label: 'Locked', disabled: true },
]
const rarities: readonly Rarity[] = ['common', 'uncommon', 'rare', 'epic', 'legendary', 'unique']
const states: readonly IconState[] = ['selected', 'equipped', 'locked', 'disabled', 'new']
const rarityIcons = computed<IconConfig[]>(() =>
  rarities.map((rarity) => ({ id: rarity, glyph: 'ring', category: 'equipment', rarity })),
)
const stateIcons = computed<IconConfig[]>(() =>
  states.map((state) => ({
    id: state,
    glyph: state === 'locked' ? 'chest' : 'sword',
    category: 'utility',
    state,
  })),
)
</script>

<template>
  <main class="playground">
    <header class="playground__hero">
      <p class="playground__eyebrow">ELYNDOR UI FOUNDATION</p>
      <h1>Arcane Minimal</h1>
      <p>Cold arcane structure, quiet surfaces, and gold reserved for exceptional hierarchy.</p>
    </header>

    <UIPanel>
      <template #title>Actions</template>
      <div class="button-grid">
        <UIButton>Enter world</UIButton><UIButton variant="secondary">Inspect</UIButton>
        <UIButton variant="ghost">Cancel</UIButton><UIButton variant="danger">Abandon</UIButton>
        <UIButton loading>Travelling</UIButton><UIButton disabled>Unavailable</UIButton>
      </div>
    </UIPanel>

    <UIPanel>
      <template #title>Combat kernel primitives</template>
      <div class="combat-primitives">
        <div class="ability-row">
          <UIAbilityButton :icon="ITEM_ICON_PRESETS.flameblade" label="Arcane strike" :resource-cost="20" />
          <UIAbilityButton :icon="ITEM_ICON_PRESETS.frostStaff" label="Frost ward" :resource-cost="12" :cooldown-remaining="4.2" :cooldown-total="8" />
        </div>
        <UICastBar label="Arcane convergence" :elapsed="1.3" :duration="2" />
        <div class="effect-row">
          <UIEffectBadge :icon="EFFECT_ICON_PRESETS.frozen" label="Arcane ward" :remaining-seconds="8" />
          <UIEffectBadge :icon="EFFECT_ICON_PRESETS.burning" label="Burning" :stacks="3" :remaining-seconds="4.2" harmful />
        </div>
      </div>
    </UIPanel>

    <UIPanel>
      <template #title>Surfaces and resources</template>
      <div class="card-grid">
        <UICard
          ><strong>Quiet surface</strong>
          <p>Static information without unnecessary glow.</p></UICard
        >
        <UICard interactive selected
          ><strong>Selected path</strong>
          <p>Glow communicates active intent.</p></UICard
        >
        <UICard interactive disabled
          ><strong>Sealed path</strong>
          <p>Disabled remains readable.</p></UICard
        >
      </div>
      <div class="bars">
        <UIHealthBar label="Health" :value="728" :max="1000" />
        <UIHealthBar label="Rage" tone="rage" :value="62" :max="100" />
        <UIHealthBar label="Focus" tone="focus" :value="84" :max="100" />
        <UIHealthBar label="Mana" tone="mana" :value="43" :max="100" />
      </div>
    </UIPanel>

    <UIPanel>
      <template #title>Tabs and feedback</template>
      <UITabs v-model="activeTab" :tabs="tabs" label="Playground sections" />
      <p class="selection" data-testid="active-tab">Active: {{ activeTab }}</p>
      <div class="toast-grid">
        <UIToast tone="success" title="Reward claimed">Inventory updated.</UIToast>
        <UIToast tone="warning" title="Low resource">Recovery is recommended.</UIToast>
        <UIToast tone="danger" title="Travel blocked">The path is no longer valid.</UIToast>
        <UIToast title="World synchronized">Authoritative state restored.</UIToast>
      </div>
      <UIButton variant="secondary" data-open-modal @click="modalOpen = true">Open modal</UIButton>
    </UIPanel>

    <UIPanel>
      <template #title>System states</template>
      <div class="state-grid">
        <UILoadingState state="loading" title="Restoring world" />
        <UILoadingState state="empty" title="No discoveries" />
        <UILoadingState
          state="error"
          title="Connection lost"
          message="Try again when the path stabilizes."
        />
      </div>
    </UIPanel>

    <UIPanel
      ><template #title>Rarity language</template>
      <div class="slot-grid">
        <UIItemSlot
          v-for="icon in rarityIcons"
          :key="icon.id"
          :icon="icon"
          :label="icon.rarity ?? 'common'"
        /></div
    ></UIPanel>

    <UIPanel>
      <template #title>Modifiers and cooldown</template>
      <div class="slot-grid">
        <UIItemSlot :icon="ITEM_ICON_PRESETS.flameblade" label="Flameblade" />
        <UIItemSlot :icon="ITEM_ICON_PRESETS.frostStaff" label="Frost staff" :cooldown="64" />
        <UIItemSlot :icon="ITEM_ICON_PRESETS.poisonDagger" label="Poison dagger" />
        <UIItemSlot :icon="ITEM_ICON_PRESETS.healingPotion" label="Potion" :quantity="4" />
        <UIItemSlot :icon="ITEM_ICON_PRESETS.lockedChest" label="Ancient chest" />
        <UIItemSlot :icon="ITEM_ICON_PRESETS.newOre" label="Moon ore" :quantity="12" />
      </div>
      <div class="effect-row" aria-label="Effect icons">
        <IconGenerator
          v-for="effect in EFFECT_ICON_PRESETS"
          :key="effect.id"
          class="effect-icon"
          :config="effect"
          :label="effect.id"
        />
      </div>
    </UIPanel>

    <UIPanel
      ><template #title>Interaction states</template>
      <div class="slot-grid">
        <UIItemSlot
          v-for="icon in stateIcons"
          :key="icon.id"
          :icon="icon"
          :label="icon.state ?? 'default'"
        /></div
    ></UIPanel>

    <UIModal :open="modalOpen" title="Arcane seal" @close="modalOpen = false">
      This controlled modal uses a body teleport, dialog semantics, backdrop close, and Escape
      handling.
      <template #actions><UIButton @click="modalOpen = false">Confirm</UIButton></template>
    </UIModal>
  </main>
</template>

<style scoped>
.playground {
  display: grid;
  width: min(100%, var(--ui-content-width));
  min-height: var(--ui-viewport-height);
  margin-inline: auto;
  gap: var(--ui-space-4);
  padding: calc(var(--ui-space-5) + var(--ui-safe-area-top))
    calc(var(--ui-space-3) + var(--ui-safe-area-right))
    calc(var(--ui-space-7) + var(--ui-safe-area-bottom))
    calc(var(--ui-space-3) + var(--ui-safe-area-left));
}
.playground__hero {
  padding-block: var(--ui-space-6) var(--ui-space-3);
}
.playground__hero h1 {
  margin: var(--ui-space-1) 0 var(--ui-space-2);
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-2xl);
  color: var(--ui-color-text-primary);
}
.playground__hero p {
  max-width: var(--ui-content-width);
  margin: 0;
  color: var(--ui-color-text-secondary);
}
.playground__eyebrow {
  color: var(--ui-color-primary) !important;
  font-size: var(--ui-font-size-xs);
  font-weight: var(--ui-font-weight-bold);
  letter-spacing: var(--ui-space-1);
}
.button-grid,
.card-grid,
.toast-grid,
.state-grid {
  display: grid;
  gap: var(--ui-space-3);
}
.button-grid {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}
.card-grid {
  margin-bottom: var(--ui-space-5);
}
.card-grid p {
  margin: var(--ui-space-1) 0 0;
  color: var(--ui-color-text-muted);
}
.bars {
  display: grid;
  gap: var(--ui-space-3);
}
.selection {
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-sm);
}
.toast-grid {
  margin-block: var(--ui-space-4);
}
.state-grid {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}
.slot-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(var(--ui-icon-slot-md), 1fr));
  gap: var(--ui-space-3);
}
.effect-row {
  display: flex;
  gap: var(--ui-space-2);
  margin-top: var(--ui-space-5);
}
.combat-primitives { display: grid; gap: var(--ui-space-4); }
.ability-row { display: flex; flex-wrap: wrap; gap: var(--ui-space-3); }
.effect-icon {
  width: var(--ui-icon-slot-sm);
  height: var(--ui-icon-slot-sm);
}
@media (max-width: 420px) {
  .state-grid {
    grid-template-columns: 1fr;
  }
}
</style>
