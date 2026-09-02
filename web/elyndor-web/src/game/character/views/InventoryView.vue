<script setup lang="ts">
import { computed } from 'vue'

import type { EquipmentSlot, InventoryItem } from '@/api/contracts'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UICard, UILoadingState, UIPanel } from '@/ui/components'

const session = useGameSessionStore()
const character = computed(() => session.snapshot?.character)
const inventory = computed(() => character.value?.inventory)
const materials = computed(() => inventory.value?.items.filter((item) => item.type === 'Material') ?? [])
const equipmentItems = computed(() => inventory.value?.items.filter((item) => item.type === 'Equipment') ?? [])
const materialUnits = computed(() => materials.value.reduce((total, item) => total + item.quantity, 0))
const slots: readonly { id: EquipmentSlot; label: string }[] = [
  { id: 'Weapon', label: 'Оружие' },
  { id: 'Head', label: 'Голова' },
  { id: 'Chest', label: 'Нагрудник' },
]

function statSummary(item: InventoryItem): string {
  return [
    item.stats.strength ? `Сила +${item.stats.strength}` : '',
    item.stats.agility ? `Ловкость +${item.stats.agility}` : '',
    item.stats.intellect ? `Интеллект +${item.stats.intellect}` : '',
    item.stats.stamina ? `Выносливость +${item.stats.stamina}` : '',
  ].filter(Boolean).join(' · ')
}

function rarityLabel(item: InventoryItem): string {
  if (item.rarity === 'Rare') return 'Редкий'
  if (item.rarity === 'Uncommon') return 'Необычный'
  return 'Обычный'
}
</script>

<template>
  <section class="inventory-view">
    <header><p>Экипировка и добыча</p><h1>Инвентарь</h1></header>
    <UIPanel v-if="inventory" class="equipment">
      <template #title>Экипировка</template>
      <div class="equipment__slots">
        <UICard v-for="slot in slots" :key="slot.id" class="equipment-slot">
          <small>{{ slot.label }}</small>
          <template v-if="inventory.equipped[slot.id]">
            <strong>{{ inventory.equipped[slot.id]?.name }}</strong>
            <span>{{ statSummary(inventory.equipped[slot.id]!) }}</span>
            <UIButton variant="ghost" :disabled="session.mutationPending" @click="session.unequip(slot.id)">Снять</UIButton>
          </template>
          <span v-else class="empty">Пусто</span>
        </UICard>
      </div>
    </UIPanel>

    <UIPanel v-if="inventory && materials.length" class="loot-section">
      <template #title>Материалы для крафта · {{ materialUnits }}</template>
      <p class="section-hint">Собирайте добычу с монстров — эти материалы пригодятся в будущей системе крафта.</p>
      <div class="inventory-list">
        <UICard v-for="item in materials" :key="item.id" class="inventory-item material-item" :data-rarity="item.rarity">
          <div>
            <div class="item-heading">
              <strong>{{ item.name }}</strong>
              <b class="quantity">×{{ item.quantity }}</b>
            </div>
            <small>Материал · {{ rarityLabel(item) }}</small>
            <span>{{ item.description }}</span>
          </div>
        </UICard>
      </div>
    </UIPanel>

    <UIPanel v-if="inventory && equipmentItems.length" class="loot-section">
      <template #title>Снаряжение в сумке</template>
      <div class="inventory-list">
        <UICard v-for="item in equipmentItems" :key="item.id" class="inventory-item" :data-rarity="item.rarity">
          <div>
            <strong>{{ item.name }}</strong>
            <small>{{ rarityLabel(item) }} · уровень {{ item.requiredLevel }}</small>
            <span v-if="statSummary(item)">{{ statSummary(item) }}</span>
          </div>
          <UIButton v-if="!item.equippedSlot" variant="secondary" :disabled="session.mutationPending || (character?.level ?? 0) < item.requiredLevel" @click="session.equip(item.id)">Надеть</UIButton>
          <span v-else class="equipped">Надето</span>
        </UICard>
      </div>
    </UIPanel>

    <UIPanel v-if="inventory && !inventory.items.length">
      <template #title>Сумка · 0 / 40</template>
      <UILoadingState state="empty" title="Сумка пуста" message="Побеждайте противников в Шепчущем лесу, чтобы получить материалы и экипировку." />
    </UIPanel>
  </section>
</template>

<style scoped>
.inventory-view { display: grid; gap: var(--ui-space-4); padding: var(--ui-space-5) var(--ui-space-4) var(--ui-space-7); }
header p, header h1 { margin: 0; }
header p { color: var(--ui-color-primary); font-size: var(--ui-font-size-xs); letter-spacing: .1em; text-transform: uppercase; }
header h1 { font-family: var(--ui-font-display); }
.equipment__slots, .inventory-list { display: grid; gap: var(--ui-space-2); }
.equipment__slots { grid-template-columns: repeat(3, minmax(0, 1fr)); }
.equipment-slot { display: grid; min-height: 8rem; align-content: start; gap: var(--ui-space-2); padding: var(--ui-space-3); }
.equipment-slot small, .inventory-item small, .inventory-item span, .equipment-slot span { color: var(--ui-color-text-muted); font-size: var(--ui-font-size-xs); }
.equipment-slot strong, .inventory-item strong { color: var(--ui-color-text-primary); }
.empty { margin-block: auto; text-align: center; }
.loot-section { display: grid; gap: var(--ui-space-3); }
.section-hint { margin: 0 0 var(--ui-space-3); color: var(--ui-color-text-muted); font-size: var(--ui-font-size-sm); line-height: var(--ui-line-height-normal); }
.inventory-item { display: flex; align-items: center; justify-content: space-between; gap: var(--ui-space-3); border-left: 2px solid var(--ui-color-border-strong); }
.inventory-item[data-rarity='Uncommon'] { border-left-color: var(--ui-color-success); }
.inventory-item[data-rarity='Rare'] { border-left-color: var(--ui-color-primary); }
.inventory-item > div { display: grid; min-width: 0; gap: var(--ui-space-1); }
.material-item { align-items: stretch; }
.item-heading { display: flex; align-items: baseline; justify-content: space-between; gap: var(--ui-space-3); }
.quantity { flex: 0 0 auto; color: var(--ui-color-secondary); font-size: var(--ui-font-size-sm); }
.equipped { color: var(--ui-color-success) !important; font-weight: var(--ui-font-weight-bold); }
@media (max-width: 360px) { .equipment__slots { grid-template-columns: 1fr; } .equipment-slot { min-height: auto; } }
</style>
