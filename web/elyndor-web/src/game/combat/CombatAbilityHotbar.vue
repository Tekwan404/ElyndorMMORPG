<script setup lang="ts">
import { onUnmounted, ref } from 'vue'

import type { CombatAbility, InventoryItem } from '@/api/contracts'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { GlyphName } from '@/ui/icons/icon.types'

const props = defineProps<{
  slots: Array<CombatAbility | null>
  auraAbilities: CombatAbility[]
  consumables: InventoryItem[]
  queuedAbilityIds: string[]
  fireballStreak: number
  heatActive: boolean
  combustionActive: boolean
  abilityState: (ability: CombatAbility) => 'cooldown' | 'resource' | 'ready'
  abilityIcon: (ability: CombatAbility) => string | undefined
  abilityGlyph: (ability: CombatAbility) => GlyphName
  cooldownRemaining: (abilityId: string) => number
  consumableCooldownRemaining: (item: InventoryItem) => number
  consumableCanAffect: (item: InventoryItem) => boolean
  consumableGlyph: (item: InventoryItem) => GlyphName
}>()

const emit = defineEmits<{
  use: [ability: CombatAbility]
  useConsumable: [item: InventoryItem]
}>()

const inspectedAbility = ref<CombatAbility | null>(null)
let inspectionTimer: ReturnType<typeof setTimeout> | undefined
let suppressNextAbilityClick = false

function consumableState(item: InventoryItem): 'cooldown' | 'ready' | 'disabled' {
  if (props.consumableCooldownRemaining(item) > 0) return 'cooldown'
  return props.consumableCanAffect(item) ? 'ready' : 'disabled'
}

function activateAbility(ability: CombatAbility | null): void {
  if (!ability) return
  if (suppressNextAbilityClick) {
    suppressNextAbilityClick = false
    return
  }
  inspectedAbility.value = null
  if (props.abilityState(ability) === 'ready') emit('use', ability)
}

function activateConsumable(item: InventoryItem): void {
  if (consumableState(item) === 'ready') emit('useConsumable', item)
}

function startInspection(ability: CombatAbility): void {
  clearInspectionTimer()
  suppressNextAbilityClick = false
  inspectionTimer = setTimeout(() => {
    inspectedAbility.value = ability
    suppressNextAbilityClick = true
    inspectionTimer = undefined
  }, 500)
}

function clearInspectionTimer(): void {
  if (inspectionTimer !== undefined) {
    clearTimeout(inspectionTimer)
    inspectionTimer = undefined
  }
}

function activateAura(ability: CombatAbility): void {
  if (suppressNextAbilityClick) {
    suppressNextAbilityClick = false
    return
  }
  inspectedAbility.value = null
  if (props.abilityState(ability) === 'ready') emit('use', ability)
}

function isQueued(abilityId: string): boolean {
  return props.queuedAbilityIds.includes(abilityId)
}

function queuePosition(abilityId: string): number {
  return props.queuedAbilityIds.indexOf(abilityId) + 1
}

onUnmounted(clearInspectionTimer)
</script>

<template>
  <div class="combat-ability-hotbar" aria-label="Боевые способности" data-combat-hotbar>
    <button
      v-for="(ability, index) in slots"
      :key="ability?.id ?? `empty-${index}`"
      type="button"
      class="combat-ability-hotbar__slot"
      :class="{
        'combat-ability-hotbar__slot--empty': !ability,
        'combat-ability-hotbar__slot--comet': ability?.id === 'FIRE_COMET',
        'combat-ability-hotbar__slot--queued': ability && isQueued(ability.id),
      }"
      :data-ability-slot="ability?.id ?? ''"
      :data-state="ability ? abilityState(ability) : 'empty'"
      :disabled="!ability"
      :aria-disabled="ability ? abilityState(ability) !== 'ready' : true"
      :aria-label="ability?.displayName ?? 'Пустой слот'"
      @pointerdown="ability && startInspection(ability)"
      @pointerup="clearInspectionTimer"
      @pointercancel="clearInspectionTimer"
      @pointerleave="clearInspectionTimer"
      @contextmenu.prevent
      @click="activateAbility(ability)"
    >
      <span class="combat-ability-hotbar__icon">
        <img v-if="ability && abilityIcon(ability)" :src="abilityIcon(ability)" alt="" />
        <IconGenerator
          v-else-if="ability"
          :config="{ id: `ability-${ability.id}`, glyph: abilityGlyph(ability), category: 'skill' }"
        />
        <i v-else />
      </span>
      <span
        v-if="ability?.id === 'MAGE_FIREBALL'"
        class="combat-ability-hotbar__markers"
        :aria-label="`Криты Огненного шара: ${fireballStreak} из 3`"
      >
        <i v-for="marker in 3" :key="marker" :data-filled="marker <= fireballStreak" />
      </span>
      <span v-if="ability?.id === 'FIRE_COMET' && heatActive" class="combat-ability-hotbar__proc">ЖАР</span>
      <span v-if="ability?.id === 'COMBUSTION' && combustionActive" class="combat-ability-hotbar__proc">АКТ.</span>
      <span v-if="ability && isQueued(ability.id)" class="combat-ability-hotbar__queue">{{ queuePosition(ability.id) }}</span>
      <small v-if="ability">{{ ability.displayName }}</small>
      <b v-if="ability && cooldownRemaining(ability.id) > 0" class="combat-ability-hotbar__cooldown">
        {{ Math.ceil(cooldownRemaining(ability.id)) }}с
      </b>
      <span v-else-if="ability && ability.resourceCost > 0" class="combat-ability-hotbar__cost">
        {{ Math.round(ability.resourceCost) }}
      </span>
    </button>
  </div>

  <section v-if="consumables.length" class="combat-consumables" aria-label="Расходники" data-combat-consumables>
    <header class="combat-consumables__header">
      <strong>Расходники</strong>
      <small>Быстрое использование</small>
    </header>
    <div class="combat-consumables__list">
      <button
        v-for="item in consumables"
        :key="item.definitionId"
        type="button"
        class="combat-consumables__item"
        :data-combat-consumable="item.definitionId"
        :data-state="consumableState(item)"
        :disabled="consumableState(item) !== 'ready'"
        :aria-label="`${item.name}, ${item.quantity}`"
        @click="activateConsumable(item)"
      >
        <span class="combat-consumables__icon">
          <IconGenerator
            :config="{ id: `consumable-${item.definitionId}`, glyph: consumableGlyph(item), category: 'consumable' }"
          />
        </span>
        <span class="combat-consumables__copy">
          <small>{{ item.name }}</small>
          <b>×{{ item.quantity }}</b>
        </span>
        <strong v-if="consumableCooldownRemaining(item) > 0" class="combat-consumables__cooldown">
          {{ Math.ceil(consumableCooldownRemaining(item) / 1000) }}с
        </strong>
      </button>
    </div>
  </section>

  <section v-if="auraAbilities.length" class="combat-ability-hotbar__auras" aria-label="Ауры" data-combat-auras>
    <span>Ауры</span>
    <button
      v-for="ability in auraAbilities"
      :key="ability.id"
      type="button"
      class="combat-ability-hotbar__aura"
      :data-aura-ability="ability.id"
      :aria-disabled="abilityState(ability) !== 'ready'"
      :aria-label="ability.displayName"
      @pointerdown="startInspection(ability)"
      @pointerup="clearInspectionTimer"
      @pointercancel="clearInspectionTimer"
      @pointerleave="clearInspectionTimer"
      @contextmenu.prevent
      @click="activateAura(ability)"
    >
      <img v-if="abilityIcon(ability)" :src="abilityIcon(ability)" alt="" />
      <IconGenerator
        v-else
        :config="{ id: `aura-${ability.id}`, glyph: abilityGlyph(ability), category: 'skill' }"
      />
      <small>{{ ability.displayName }}</small>
      <b v-if="cooldownRemaining(ability.id) > 0">{{ Math.ceil(cooldownRemaining(ability.id)) }}с</b>
    </button>
  </section>

  <section v-if="inspectedAbility" class="combat-ability-hotbar__inspection" data-ability-inspection aria-live="polite">
    <div>
      <strong>{{ inspectedAbility.displayName }}</strong>
      <p>{{ inspectedAbility.description || 'Описание способности пока не добавлено.' }}</p>
    </div>
    <button type="button" aria-label="Закрыть описание способности" @click="inspectedAbility = null">×</button>
  </section>
</template>

<style scoped>
.combat-ability-hotbar { display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); gap: 5px; }
.combat-ability-hotbar__slot { position: relative; display: grid; min-width: 0; min-height: 60px; place-items: center; align-content: center; gap: 3px; padding: 4px 3px; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: linear-gradient(180deg, rgb(255 255 255 / 2.5%), rgb(2 5 9 / 45%)); color: var(--ui-color-text-primary); font: inherit; }
.combat-ability-hotbar__slot[data-state='ready'] { border-color: rgb(146 136 255 / 42%); box-shadow: inset 0 0 0 1px rgb(146 136 255 / 6%); }
.combat-ability-hotbar__slot[data-state='cooldown'], .combat-ability-hotbar__slot[data-state='resource'] { opacity: .52; }
.combat-ability-hotbar__slot--empty { display: none; }
.combat-ability-hotbar__slot--comet { border-color: color-mix(in srgb, var(--ui-modifier-fire) 65%, var(--ui-color-border)); }
.combat-ability-hotbar__icon { display: grid; width: 40px; height: 40px; place-items: center; overflow: hidden; border: 1px solid rgb(255 255 255 / 7%); border-radius: 8px; background: rgb(3 5 10 / 90%); color: #aaa3ff; }
.combat-ability-hotbar__icon img { width: 100%; height: 100%; object-fit: cover; }
.combat-ability-hotbar__icon > i { width: 11px; height: 11px; border: 1px solid var(--ui-color-border); border-radius: 50%; }
.combat-ability-hotbar__slot > small { width: 100%; overflow: hidden; color: var(--ui-color-text-secondary); font-size: .52rem; font-weight: 650; line-height: 1.1; text-align: center; text-overflow: ellipsis; white-space: nowrap; }
.combat-ability-hotbar__cooldown { position: absolute; inset: 3px; display: grid; place-items: center; border-radius: 8px; background: rgb(1 3 7 / 76%); color: white; font-size: .82rem; font-variant-numeric: tabular-nums; }
.combat-ability-hotbar__cost { position: absolute; right: 3px; bottom: 17px; padding: 1px 3px; border-radius: 5px; background: rgb(2 4 8 / 84%); color: #bdb7ff; font-size: .45rem; }
.combat-ability-hotbar__slot--queued { border-color: rgb(155 226 201 / 55%); box-shadow: inset 0 0 0 1px rgb(155 226 201 / 18%); }
.combat-ability-hotbar__queue { position: absolute; top: 3px; left: 3px; z-index: 5; display: grid; width: 1rem; height: 1rem; place-items: center; border-radius: 50%; background: #9be2c9; color: #07110e; font-size: .48rem; font-weight: 900; }
.combat-ability-hotbar__markers { position: absolute; top: 3px; right: 3px; display: flex; gap: 1px; }
.combat-ability-hotbar__markers i { width: 4px; height: 4px; border-radius: 50%; background: rgb(255 255 255 / 20%); }
.combat-ability-hotbar__markers i[data-filled='true'] { background: #f08b63; }
.combat-ability-hotbar__proc { position: absolute; top: 3px; right: 3px; border-radius: 4px; padding: 1px 2px; background: rgb(241 123 70 / 85%); color: #fff4de; font-size: .38rem; font-weight: 900; }

.combat-consumables { display: grid; gap: 5px; margin-top: 1px; padding-top: 6px; border-top: 1px solid rgb(79 185 150 / 18%); }
.combat-consumables__header { display: flex; align-items: baseline; justify-content: space-between; gap: 8px; padding-inline: 2px; }
.combat-consumables__header strong { color: #b9ddd1; font-size: .55rem; letter-spacing: .04em; text-transform: uppercase; }
.combat-consumables__header small { color: var(--ui-color-text-muted); font-size: .48rem; }
.combat-consumables__list { display: flex; gap: 5px; overflow-x: auto; padding-bottom: 1px; scrollbar-width: none; }
.combat-consumables__list::-webkit-scrollbar { display: none; }
.combat-consumables__item { position: relative; display: grid; min-width: 7.2rem; min-height: var(--ui-touch-target); flex: 0 0 auto; grid-template-columns: 34px minmax(0, 1fr); align-items: center; gap: 6px; padding: 4px 7px 4px 5px; border: 1px solid rgb(79 185 150 / 30%); border-radius: var(--ui-radius-sm); background: rgb(4 11 12 / 72%); color: var(--ui-color-text-primary); font: inherit; text-align: left; }
.combat-consumables__item[data-state='disabled'], .combat-consumables__item[data-state='cooldown'] { opacity: .48; }
.combat-consumables__icon { display: grid; width: 34px; height: 34px; place-items: center; overflow: hidden; border: 1px solid rgb(79 185 150 / 20%); border-radius: 8px; background: rgb(3 8 9 / 86%); color: #9be2c9; }
.combat-consumables__icon :deep(.icon-generator) { width: 100%; height: 100%; }
.combat-consumables__copy { display: grid; min-width: 0; gap: 1px; }
.combat-consumables__copy small { overflow: hidden; color: var(--ui-color-text-secondary); font-size: .5rem; text-overflow: ellipsis; white-space: nowrap; }
.combat-consumables__copy b { color: #9be2c9; font-size: .52rem; }
.combat-consumables__cooldown { position: absolute; inset: 2px; display: grid; place-items: center; border-radius: inherit; background: rgb(1 5 6 / 78%); color: white; font-size: .72rem; font-variant-numeric: tabular-nums; }

.combat-ability-hotbar__auras { display: flex; align-items: center; gap: 5px; margin-top: 5px; padding: 5px; border: 1px solid rgb(205 177 113 / 28%); border-radius: var(--ui-radius-sm); background: rgb(205 177 113 / 6%); }
.combat-ability-hotbar__auras > span { flex: none; color: var(--ui-color-gold-muted); font-size: .5rem; font-weight: 800; letter-spacing: .08em; text-transform: uppercase; }
.combat-ability-hotbar__aura { position: relative; display: inline-flex; min-width: 0; min-height: var(--ui-touch-target); flex: 1; align-items: center; justify-content: center; gap: 5px; padding: 4px 7px; border: 1px solid rgb(205 177 113 / 36%); border-radius: var(--ui-radius-sm); background: rgb(4 7 12 / 78%); color: var(--ui-color-text-primary); font: inherit; }
.combat-ability-hotbar__aura[aria-disabled='true'] { opacity: .45; }
.combat-ability-hotbar__aura img, .combat-ability-hotbar__aura :deep(.icon-generator) { width: 24px; height: 24px; flex: none; }
.combat-ability-hotbar__aura small { overflow: hidden; font-size: .52rem; text-overflow: ellipsis; white-space: nowrap; }
.combat-ability-hotbar__aura b { position: absolute; inset: 2px; display: grid; place-items: center; border-radius: inherit; background: rgb(1 3 7 / 72%); color: white; font-size: .72rem; }
.combat-ability-hotbar__inspection { display: flex; align-items: flex-start; justify-content: space-between; gap: 10px; margin-top: 5px; padding: 8px 10px; border: 1px solid rgb(146 136 255 / 38%); border-radius: var(--ui-radius-sm); background: rgb(8 11 20 / 96%); box-shadow: 0 5px 14px rgb(0 0 0 / 25%); }
.combat-ability-hotbar__inspection strong { color: var(--ui-color-text-primary); font-size: .72rem; }
.combat-ability-hotbar__inspection p { margin: 3px 0 0; color: var(--ui-color-text-muted); font-size: .62rem; line-height: 1.4; }
.combat-ability-hotbar__inspection > button { display: grid; width: 32px; height: 32px; flex: none; place-items: center; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); background: transparent; color: var(--ui-color-text-primary); font: inherit; }

@media (max-width: 520px) {
  .combat-ability-hotbar { grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 6px; }
  .combat-ability-hotbar__slot { min-height: 68px; padding-block: 5px; }
  .combat-ability-hotbar__icon { width: 44px; height: 44px; }
  .combat-ability-hotbar__slot > small { font-size: .56rem; }
  .combat-ability-hotbar__cooldown { font-size: .86rem; }
  .combat-consumables__item { min-width: 6.7rem; }
}

@media (max-width: 360px) {
  .combat-ability-hotbar { gap: 4px; }
  .combat-ability-hotbar__slot { min-height: 64px; }
  .combat-ability-hotbar__icon { width: 40px; height: 40px; }
  .combat-ability-hotbar__slot > small { font-size: .52rem; }
  .combat-consumables__header small { display: none; }
}
</style>
