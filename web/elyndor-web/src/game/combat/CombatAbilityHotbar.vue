<script setup lang="ts">
import { computed, onUnmounted, ref } from 'vue'

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

type HotbarEntry =
  | { kind: 'ability'; ability: CombatAbility }
  | { kind: 'consumable'; item: InventoryItem }

const displaySlots = computed<Array<HotbarEntry | null>>(() => {
  let consumableIndex = 0
  return props.slots.map((ability) => {
    if (ability) return { kind: 'ability', ability }
    const item = props.consumables[consumableIndex++]
    return item ? { kind: 'consumable', item } : null
  })
})

const inspectedAbility = ref<CombatAbility | null>(null)
let inspectionTimer: ReturnType<typeof setTimeout> | undefined
let suppressNextAbilityClick = false

function slotState(entry: HotbarEntry | null): string {
  if (!entry) return 'empty'
  if (entry.kind === 'ability') return props.abilityState(entry.ability)
  if (props.consumableCooldownRemaining(entry.item) > 0) return 'cooldown'
  return props.consumableCanAffect(entry.item) ? 'ready' : 'disabled'
}

function slotDisabled(entry: HotbarEntry | null): boolean {
  return !entry || (entry.kind === 'consumable' && slotState(entry) !== 'ready')
}

function activate(entry: HotbarEntry | null): void {
  if (!entry) return
  if (entry.kind === 'ability') {
    if (suppressNextAbilityClick) {
      suppressNextAbilityClick = false
      return
    }
    inspectedAbility.value = null
    if (props.abilityState(entry.ability) === 'ready') emit('use', entry.ability)
    return
  }
  if (!slotDisabled(entry)) emit('useConsumable', entry.item)
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
      v-for="(entry, index) in displaySlots"
      :key="entry?.kind === 'ability' ? entry.ability.id : entry?.kind === 'consumable' ? entry.item.definitionId : `empty-${index}`"
      type="button"
      class="combat-ability-hotbar__slot"
      :class="{
        'combat-ability-hotbar__slot--empty': !entry,
        'combat-ability-hotbar__slot--comet': entry?.kind === 'ability' && entry.ability.id === 'FIRE_COMET',
        'combat-ability-hotbar__slot--queued': entry?.kind === 'ability' && isQueued(entry.ability.id),
        'combat-ability-hotbar__slot--consumable': entry?.kind === 'consumable',
      }"
      :data-ability-slot="entry?.kind === 'ability' ? entry.ability.id : ''"
      :data-combat-consumable="entry?.kind === 'consumable' ? entry.item.definitionId : undefined"
      :data-state="slotState(entry)"
      :disabled="!entry || (entry.kind === 'consumable' && slotDisabled(entry))"
      :aria-disabled="entry?.kind === 'ability' ? slotState(entry) !== 'ready' : undefined"
      @pointerdown="entry?.kind === 'ability' && startInspection(entry.ability)"
      @pointerup="clearInspectionTimer"
      @pointercancel="clearInspectionTimer"
      @pointerleave="clearInspectionTimer"
      @contextmenu.prevent
      :aria-label="entry?.kind === 'ability' ? entry.ability.displayName : entry?.kind === 'consumable' ? `${entry.item.name}, ${entry.item.quantity}` : 'Пустой слот'"
      @click="activate(entry)"
    >
      <span class="combat-ability-hotbar__icon">
        <img v-if="entry?.kind === 'ability' && abilityIcon(entry.ability)" :src="abilityIcon(entry.ability)" alt="" />
        <IconGenerator
          v-else-if="entry?.kind === 'ability'"
          :config="{ id: `ability-${entry.ability.id}`, glyph: abilityGlyph(entry.ability), category: 'skill' }"
        />
        <IconGenerator
          v-else-if="entry?.kind === 'consumable'"
          :config="{ id: `consumable-${entry.item.definitionId}`, glyph: consumableGlyph(entry.item), category: 'consumable' }"
        />
        <i v-else />
      </span>
      <span
        v-if="entry?.kind === 'ability' && entry.ability.id === 'MAGE_FIREBALL'"
        class="combat-ability-hotbar__markers"
        :aria-label="`Криты Огненного шара: ${fireballStreak} из 3`"
      >
        <i v-for="marker in 3" :key="marker" :data-filled="marker <= fireballStreak" />
      </span>
      <span v-if="entry?.kind === 'ability' && entry.ability.id === 'FIRE_COMET' && heatActive" class="combat-ability-hotbar__proc">ЖАР</span>
      <span v-if="entry?.kind === 'ability' && entry.ability.id === 'COMBUSTION' && combustionActive" class="combat-ability-hotbar__proc">АКТ.</span>
      <span v-if="entry?.kind === 'ability' && isQueued(entry.ability.id)" class="combat-ability-hotbar__queue">{{ queuePosition(entry.ability.id) }}</span>
      <small v-if="entry">{{ entry.kind === 'ability' ? entry.ability.displayName : entry.item.name }}</small>
      <b v-if="entry?.kind === 'ability' && cooldownRemaining(entry.ability.id) > 0" class="combat-ability-hotbar__cooldown">
        {{ Math.ceil(cooldownRemaining(entry.ability.id)) }}
      </b>
      <b v-else-if="entry?.kind === 'consumable' && consumableCooldownRemaining(entry.item) > 0" class="combat-ability-hotbar__cooldown">
        {{ Math.ceil(consumableCooldownRemaining(entry.item) / 1000) }}
      </b>
      <span v-else-if="entry?.kind === 'ability' && entry.ability.resourceCost > 0" class="combat-ability-hotbar__cost">
        {{ Math.round(entry.ability.resourceCost) }}
      </span>
      <span v-else-if="entry?.kind === 'consumable'" class="combat-ability-hotbar__count">×{{ entry.item.quantity }}</span>
    </button>
  </div>
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
      <b v-if="cooldownRemaining(ability.id) > 0">{{ Math.ceil(cooldownRemaining(ability.id)) }}</b>
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
.combat-ability-hotbar { display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); gap: 4px; }
.combat-ability-hotbar__auras { display: flex; align-items: center; gap: 5px; margin-top: 5px; padding: 5px; border: 1px solid rgb(205 177 113 / 28%); border-radius: var(--ui-radius-sm); background: rgb(205 177 113 / 6%); }
.combat-ability-hotbar__auras > span { flex: none; color: var(--ui-color-gold-muted); font-size: .5rem; font-weight: 800; letter-spacing: .08em; text-transform: uppercase; }
.combat-ability-hotbar__aura { position: relative; display: inline-flex; min-width: 0; min-height: var(--ui-touch-target); flex: 1; align-items: center; justify-content: center; gap: 5px; padding: 4px 7px; border: 1px solid rgb(205 177 113 / 36%); border-radius: var(--ui-radius-sm); background: rgb(4 7 12 / 78%); color: var(--ui-color-text-primary); font: inherit; }
.combat-ability-hotbar__aura[aria-disabled='true'] { opacity: .45; }
.combat-ability-hotbar__aura img, .combat-ability-hotbar__aura :deep(.icon-generator) { width: 24px; height: 24px; flex: none; }
.combat-ability-hotbar__aura small { overflow: hidden; font-size: .52rem; text-overflow: ellipsis; white-space: nowrap; }
.combat-ability-hotbar__aura b { position: absolute; inset: 2px; display: grid; place-items: center; border-radius: inherit; background: rgb(1 3 7 / 72%); color: white; font-size: .72rem; }
.combat-ability-hotbar__slot { position: relative; display: grid; min-width: 0; min-height: clamp(43px, 13vw, 56px); place-items: center; align-content: center; gap: 2px; padding: 3px 2px; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-md); background: linear-gradient(180deg, rgb(255 255 255 / 2.5%), rgb(2 5 9 / 45%)); color: var(--ui-color-text-primary); font: inherit; }
.combat-ability-hotbar__slot[data-state='ready'] { border-color: rgb(146 136 255 / 36%); box-shadow: inset 0 0 0 1px rgb(146 136 255 / 4%); }
.combat-ability-hotbar__slot[data-state='cooldown'], .combat-ability-hotbar__slot[data-state='resource'] { opacity: .46; }
.combat-ability-hotbar__slot[data-state='disabled'] { opacity: .38; }
.combat-ability-hotbar__slot--empty { opacity: .2; }
.combat-ability-hotbar__slot--comet { border-color: color-mix(in srgb, var(--ui-modifier-fire) 65%, var(--ui-color-border)); }
.combat-ability-hotbar__icon { display: grid; width: min(38px, 9vw); height: min(38px, 9vw); place-items: center; overflow: hidden; border: 1px solid rgb(255 255 255 / 7%); border-radius: 8px; background: rgb(3 5 10 / 90%); color: #aaa3ff; }
.combat-ability-hotbar__icon img { width: 100%; height: 100%; object-fit: cover; }
.combat-ability-hotbar__icon > i { width: 11px; height: 11px; border: 1px solid var(--ui-color-border); border-radius: 50%; }
.combat-ability-hotbar__slot > small { width: 100%; overflow: hidden; color: var(--ui-color-text-muted); font-size: .38rem; line-height: 1; text-align: center; text-overflow: ellipsis; white-space: nowrap; }
.combat-ability-hotbar__cooldown { position: absolute; inset: 3px; display: grid; place-items: center; border-radius: 8px; background: rgb(1 3 7 / 70%); color: white; font-size: .78rem; }
.combat-ability-hotbar__cost { position: absolute; right: 2px; bottom: 15px; padding: 1px 3px; border-radius: 5px; background: rgb(2 4 8 / 80%); color: #bdb7ff; font-size: .38rem; }
.combat-ability-hotbar__slot--queued { border-color: rgb(155 226 201 / 55%); box-shadow: inset 0 0 0 1px rgb(155 226 201 / 18%); }
.combat-ability-hotbar__queue { position: absolute; top: 3px; left: 3px; z-index: 5; display: grid; width: 1rem; height: 1rem; place-items: center; border-radius: 50%; background: #9be2c9; color: #07110e; font-size: .48rem; font-weight: 900; }
.combat-ability-hotbar__markers { position: absolute; top: 3px; right: 3px; display: flex; gap: 1px; }
.combat-ability-hotbar__markers i { width: 4px; height: 4px; border-radius: 50%; background: rgb(255 255 255 / 20%); }
.combat-ability-hotbar__markers i[data-filled='true'] { background: #f08b63; }
.combat-ability-hotbar__proc { position: absolute; top: 3px; right: 3px; border-radius: 4px; padding: 1px 2px; background: rgb(241 123 70 / 85%); color: #fff4de; font-size: .38rem; font-weight: 900; }
.combat-ability-hotbar__slot--consumable { border-color: rgb(79 185 150 / 34%); }
.combat-ability-hotbar__count { position: absolute; right: 2px; bottom: 14px; padding: 1px 3px; border-radius: 4px; background: rgb(2 4 8 / 84%); color: #9be2c9; font-size: .42rem; font-weight: 900; }
.combat-ability-hotbar__inspection { display: flex; align-items: flex-start; justify-content: space-between; gap: 10px; margin-top: 5px; padding: 8px 10px; border: 1px solid rgb(146 136 255 / 38%); border-radius: var(--ui-radius-sm); background: rgb(8 11 20 / 96%); box-shadow: 0 5px 14px rgb(0 0 0 / 25%); }
.combat-ability-hotbar__inspection strong { color: var(--ui-color-text-primary); font-size: .72rem; }
.combat-ability-hotbar__inspection p { margin: 3px 0 0; color: var(--ui-color-text-muted); font-size: .62rem; line-height: 1.4; }
.combat-ability-hotbar__inspection > button { display: grid; width: 32px; height: 32px; flex: none; place-items: center; border: 1px solid var(--ui-color-border); border-radius: var(--ui-radius-sm); background: transparent; color: var(--ui-color-text-primary); font: inherit; }
</style>
